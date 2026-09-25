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
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="UiThread.RunOnIdle(Func{Task})"/> is agg's one sanctioned fire-and-forget. Its whole point is
	/// that a fault in the async work - most of all one thrown after an await, which an async void lambda
	/// would rethrow into the pump - reaches <see cref="UiThread.UnhandledException"/> instead of being lost
	/// or taking the pump down.
	/// </summary>
	// UiThreadTestPump resets UiThread's statics, so nothing else may be driving UiThread meanwhile.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class UiThreadRunOnIdleAsyncTests
	{
		[Test]
		[Timeout(30_000)]
		public async Task AFaultAfterAnAwaitReachesUnhandledException(CancellationToken cancellationToken)
		{
			var thrown = new InvalidOperationException("thrown after an await in RunOnIdle work");
			var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
			void OnUnhandledException(Exception exception) => reported.TrySetResult(exception);

			using var pump = new UiThreadTestPump();
			UiThread.UnhandledException += OnUnhandledException;
			try
			{
				// Typed as Func<Task> so this can only bind to the Task overload - a lambda written inline
				// would silently fall back to the Action overload, as an async void, if that one went away.
				Func<Task> work = async () =>
				{
					await Task.Yield();
					throw thrown;
				};

				UiThread.RunOnIdle(work);

				var exception = await reported.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);

				await Assert.That(exception).IsSameReferenceAs(thrown)
					.Because("the work's own exception, not a wrapper, is what a crash reporter needs to see");
			}
			finally
			{
				UiThread.UnhandledException -= OnUnhandledException;
			}
		}

		/// <summary>
		/// The same fault, resumed on the thread pool rather than the pump. Past a ConfigureAwait(false) an
		/// async void rethrows on a pool thread with nobody to catch it in Debug and Release alike, so unlike
		/// the Task.Yield case above this one tells the observed path from an async void in every build.
		/// </summary>
		[Test]
		[Timeout(30_000)]
		public async Task AFaultPastConfigureAwaitFalseReachesUnhandledException(CancellationToken cancellationToken)
		{
			var thrown = new InvalidOperationException("thrown on the thread pool in RunOnIdle work");
			var reported = new TaskCompletionSource<Exception>(TaskCreationOptions.RunContinuationsAsynchronously);
			void OnUnhandledException(Exception exception) => reported.TrySetResult(exception);

			using var pump = new UiThreadTestPump();
			UiThread.UnhandledException += OnUnhandledException;
			try
			{
				Func<Task> work = async () =>
				{
					await Task.Run(() => { }).ConfigureAwait(false);
					throw thrown;
				};

				UiThread.RunOnIdle(work);

				var exception = await reported.Task.WaitAsync(TimeSpan.FromSeconds(10), cancellationToken);
				await Assert.That(exception).IsSameReferenceAs(thrown);
			}
			finally
			{
				UiThread.UnhandledException -= OnUnhandledException;
			}
		}

		/// <summary>
		/// A Task that lost more than one piece of work (a Task.WhenAll, say) reports every one of them, not
		/// one AggregateException wrapping them all.
		/// </summary>
		[Test]
		public async Task ObserveFaultsReportsEachInnerException()
		{
			var first = new InvalidOperationException("first");
			var second = new ArgumentException("second");
			var reported = new System.Collections.Generic.List<Exception>();
			void OnUnhandledException(Exception exception)
			{
				lock (reported)
				{
					reported.Add(exception);
				}
			}

			using var pump = new UiThreadTestPump();
			UiThread.UnhandledException += OnUnhandledException;
			try
			{
				// Both inputs are already faulted, so WhenAll is too, and ObserveFaults' continuation runs
				// synchronously - the reports are in by the time it returns.
				UiThread.ObserveFaults(Task.WhenAll(Task.FromException(first), Task.FromException(second)));

				await Assert.That(reported.Count).IsEqualTo(2);
				await Assert.That(reported).Contains(first);
				await Assert.That(reported).Contains(second);
			}
			finally
			{
				UiThread.UnhandledException -= OnUnhandledException;
			}
		}
	}
}
