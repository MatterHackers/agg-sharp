/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Runtime.Versioning;
using System.Threading;
using System.Threading.Tasks;

namespace MatterHackers.RenderCore.Testing
{
	/// <summary>
	/// A machine-wide gate that lets only one test process drive the GPU at a time.
	/// </summary>
	/// <remarks>
	/// Sharded test runs put several hosts on the machine at once, which is exactly what we want for the
	/// CPU-bound suites. The GPU is not shardable the same way: two processes submitting D3D12 work
	/// concurrently has repeatedly tripped a driver TDR (LiveKernelEvent 141) here, which takes down every
	/// GPU-accelerated application on the desktop, not just the tests. So GPU-touching spans take this
	/// named-mutex gate and serialize across processes while everything else keeps running in parallel.
	/// <para>
	/// The mutex is held by a dedicated background thread rather than by the caller. <see cref="Mutex"/> has
	/// thread affinity - only the thread that waited on it may release it - and the callers here are async
	/// test bodies whose continuations land on whatever thread the pool hands them. Handing ownership to a
	/// thread that does nothing but wait, hold, and release removes that hazard entirely, and still gives us
	/// the property a semaphore could not: if the holding process is killed mid-frame the OS marks the mutex
	/// abandoned and the next waiter is let straight in.
	/// </para>
	/// <para>
	/// Desktop-only, and marked so for the browser compile gate: every mechanism it is built from - a
	/// machine-wide named mutex, a dedicated holder thread, blocking waits, the process list - is a
	/// multi-process operating system concept that wasm does not have. A browser tab is the only "test
	/// process" there is, so there is nothing to serialize against.
	/// </para>
	/// </remarks>
	[UnsupportedOSPlatform("browser")]
	public static class GpuTestGate
	{
		/// <summary>
		/// The name of the gate mutex. Session-local (<c>Local\</c>) on purpose: test shards are started by
		/// one user in one session, and a <c>Global\</c> name would need privileges a plain test run has no
		/// business asking for.
		/// </summary>
		public const string MutexName = @"Local\MatterHackers.GpuTestGate";

		/// <summary>
		/// How long to wait for another process to finish its GPU work before giving up.
		/// <para>
		/// Generous, because the slowest single gated span (a golden suite capture, or a thumbnail of a mesh
		/// too big for one vertex buffer) is measured in tens of seconds and a loaded machine can multiply
		/// that. Long enough to never fire spuriously, short enough that a wedged holder fails a shard loudly
		/// instead of hanging the run until someone notices.
		/// </para>
		/// </summary>
		public static readonly TimeSpan AcquireTimeout = TimeSpan.FromMinutes(5);

		/// <summary>
		/// Tracks whether the current logical call context already holds the gate, so a gated harness created
		/// inside an already-gated test does not wait on a mutex its own flow owns.
		/// </summary>
		private static readonly AsyncLocal<Depth> HeldDepth = new AsyncLocal<Depth>();

		/// <summary>
		/// Takes the gate, blocking until this process owns the GPU or <see cref="AcquireTimeout"/> elapses.
		/// Dispose the result to release it; wrap the span in a <c>using</c> so a failing test still lets go.
		/// </summary>
		/// <param name="label">What is being gated, quoted back in the timeout diagnostic.</param>
		/// <returns>A scope that releases the gate when disposed.</returns>
		/// <exception cref="TimeoutException">Another process held the gate for longer than
		/// <see cref="AcquireTimeout"/>.</exception>
		public static IDisposable Acquire(string label)
		{
			if (TryEnterReentrant(out var reentrant))
			{
				return reentrant;
			}

			var holder = new Holder(label);
			try
			{
				holder.WaitUntilHeld();
			}
			catch (Exception)
			{
				// Give the nesting counter back, or every later acquire in this flow would believe the gate
				// was already held and skip the wait.
				holder.Dispose();
				throw;
			}

			return holder;
		}

		/// <summary>
		/// <see cref="Acquire(string)"/> without blocking the calling thread while another process finishes.
		/// </summary>
		/// <param name="label">What is being gated, quoted back in the timeout diagnostic.</param>
		/// <returns>A scope that releases the gate when disposed.</returns>
		public static Task<IDisposable> AcquireAsync(string label)
		{
			// Deliberately not an async method. An async method's writes to an AsyncLocal are lost to its
			// caller once it suspends, so doing the nesting bookkeeping here - synchronously, on the caller's
			// execution context - is what lets a nested acquire further down the flow see that the gate is
			// already held instead of waiting five minutes for a gate its own flow owns.
			if (TryEnterReentrant(out var reentrant))
			{
				return Task.FromResult(reentrant);
			}

			return WaitForHolder(new Holder(label));
		}

		/// <summary>Awaits a holder's acquisition, giving the nesting count back if it never gets the gate.</summary>
		private static async Task<IDisposable> WaitForHolder(Holder holder)
		{
			try
			{
				await holder.HeldTask;
			}
			catch (Exception)
			{
				holder.Dispose();
				throw;
			}

			return holder;
		}

		/// <summary>
		/// Notes one more level of nesting and, if the flow already held the gate, hands back a scope that
		/// only undoes the bookkeeping. Real acquisition would deadlock: the mutex is owned by a helper
		/// thread, so this flow's existing ownership would not count as re-entry.
		/// </summary>
		private static bool TryEnterReentrant(out IDisposable scope)
		{
			var depth = HeldDepth.Value;
			if (depth == null)
			{
				// AsyncLocal copies on write down the flow, so the counter has to be a shared object rather
				// than an int - otherwise a nested acquire's decrement would not be seen by the outer scope.
				depth = new Depth();
				HeldDepth.Value = depth;
			}

			bool alreadyHeld = depth.Count > 0;
			depth.Count++;

			scope = alreadyHeld ? new NestedScope(depth) : null;
			return alreadyHeld;
		}

		/// <summary>A mutable nesting counter shared by every scope in one logical flow.</summary>
		private sealed class Depth
		{
			public int Count;
		}

		/// <summary>An acquire that found the gate already held by this flow; releasing it does nothing.</summary>
		private sealed class NestedScope : IDisposable
		{
			private Depth depth;

			public NestedScope(Depth depth) => this.depth = depth;

			public void Dispose()
			{
				if (this.depth != null)
				{
					this.depth.Count--;
					this.depth = null;
				}
			}
		}

		/// <summary>
		/// The gate itself: a background thread that waits on the named mutex, signals the caller, and holds
		/// ownership until <see cref="Dispose"/> asks it to let go.
		/// </summary>
		private sealed class Holder : IDisposable
		{
			private readonly string label;
			private readonly ManualResetEventSlim heldSignal = new ManualResetEventSlim(false);
			private readonly ManualResetEventSlim releaseSignal = new ManualResetEventSlim(false);
			private readonly TaskCompletionSource<object> heldSource
				= new TaskCompletionSource<object>(TaskCreationOptions.RunContinuationsAsynchronously);

			private readonly Thread owner;
			private readonly Depth depth;

			private Exception failure;
			private bool disposed;

			public Holder(string label)
			{
				this.label = label;
				this.depth = HeldDepth.Value;

				this.owner = new Thread(this.OwnGate)
				{
					// Named so a hung run's thread list says what it is waiting for, and background so a test
					// host that decides to exit is never held open by a gate nobody released.
					Name = "GpuTestGate " + label,
					IsBackground = true,
				};

				this.owner.Start();
			}

			/// <summary>Completes once this process owns the GPU, or faults with the timeout diagnostic.</summary>
			public Task HeldTask => this.heldSource.Task;

			/// <summary>Blocks until this process owns the GPU, or throws the timeout diagnostic.</summary>
			public void WaitUntilHeld()
			{
				this.heldSignal.Wait();
				this.ThrowIfFailed();
			}

			public void Dispose()
			{
				if (this.disposed)
				{
					return;
				}

				this.disposed = true;

				if (this.depth != null)
				{
					this.depth.Count--;
				}

				this.releaseSignal.Set();

				// Joining matters: without it the next acquire in this process could start waiting before
				// this one had actually released, turning a serialized run into a five minute stall.
				this.owner.Join();

				this.heldSignal.Dispose();
				this.releaseSignal.Dispose();
			}

			private void OwnGate()
			{
				using (var mutex = new Mutex(false, MutexName))
				{
					bool held;
					try
					{
						held = mutex.WaitOne(AcquireTimeout);
					}
					catch (AbandonedMutexException)
					{
						// The previous holder died without releasing. Its process is gone, so the GPU is
						// free and this wait succeeded - abandonment is the crash-safety we chose a mutex
						// for, not an error.
						held = true;
					}

					if (!held)
					{
						this.failure = new TimeoutException(DescribeTimeout(this.label));
						this.heldSource.TrySetException(this.failure);
						this.heldSignal.Set();
						return;
					}

					this.heldSource.TrySetResult(null);
					this.heldSignal.Set();

					try
					{
						this.releaseSignal.Wait();
					}
					finally
					{
						mutex.ReleaseMutex();
					}
				}
			}

			private void ThrowIfFailed()
			{
				if (this.failure != null)
				{
					throw new TimeoutException(this.failure.Message, this.failure);
				}
			}
		}

		/// <summary>
		/// Builds the message a blocked acquire fails with: which gate, who was waiting, and the processes
		/// most likely to be holding it - a wedged shard is otherwise invisible from inside the shard that
		/// timed out.
		/// </summary>
		private static string DescribeTimeout(string label)
		{
			var self = Process.GetCurrentProcess();
			var suspects = new List<string>();

			try
			{
				foreach (var process in Process.GetProcesses())
				{
					using (process)
					{
						if (process.Id != self.Id && LooksLikeATestHost(process.ProcessName))
						{
							suspects.Add($"{process.ProcessName} (pid {process.Id})");
						}
					}
				}
			}
			catch (Exception)
			{
				// Enumerating processes is a diagnostic nicety; a machine that refuses still gets the rest
				// of the message.
			}

			string holders = suspects.Count > 0
				? string.Join(", ", suspects)
				: "no other test host processes found - the gate may have been left held by a debugger";

			return $"Waited {AcquireTimeout.TotalMinutes:0} minutes for the GPU test gate ('{MutexName}')"
				+ $" on behalf of '{label}' in {self.ProcessName} (pid {self.Id}) and never got it."
				+ " Only one process may drive the GPU at a time; concurrent GPU work has crashed the"
				+ $" display driver on this machine. Likely holder: {holders}.";
		}

		/// <summary>Whether a process name is one of the hosts a test run shows up as.</summary>
		private static bool LooksLikeATestHost(string processName)
			=> processName.IndexOf("testhost", StringComparison.OrdinalIgnoreCase) >= 0
				|| processName.IndexOf("Agg.Tests", StringComparison.OrdinalIgnoreCase) >= 0
				|| processName.IndexOf("MatterCADTests", StringComparison.OrdinalIgnoreCase) >= 0;
	}
}
