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
*/

using System;
using System.Collections.Generic;
using System.Threading;

namespace MatterHackers.RenderGl.Compat
{
	/// <summary>
	/// Releases asked for off the render thread, held until the render thread can run them.
	/// </summary>
	/// <remarks>
	/// A <see cref="GlCompatContext"/>'s caches and stores are the render thread's alone and take no locks, but
	/// agg has always let a widget be closed from any thread (MatterCAD's library view reloads its rows on a
	/// worker), and closing a double-buffered widget releases its GPU layer. Released there, it would scan and
	/// edit the pipeline cache while a frame is adding to it. So a release from another thread is queued here
	/// and run by the render thread after its next submit, or by the context's own disposal, whichever comes
	/// first - so nothing queued outlives the context.
	/// <para>
	/// The render thread is whichever thread last submitted (the constructing thread until then). One
	/// thread in the browser, so nothing is ever queued there.
	/// </para>
	/// </remarks>
	internal sealed class GlRenderThreadReleases
	{
		private readonly object gate = new object();
		private readonly List<IDisposable> pending = new List<IDisposable>();
		private int renderThreadId = Environment.CurrentManagedThreadId;
		private bool closing;

		/// <summary>Records the calling thread as the one that draws.</summary>
		public void MarkRenderThread() => Volatile.Write(ref this.renderThreadId, Environment.CurrentManagedThreadId);

		/// <summary>
		/// Queues <paramref name="resource"/>'s disposal when called off the render thread, returning true; the
		/// caller then returns without releasing anything. False on the render thread, or once the context is
		/// being disposed - its disposal is the last chance to release, whatever thread it runs on.
		/// </summary>
		public bool TryDefer(IDisposable resource)
		{
			if (Environment.CurrentManagedThreadId == Volatile.Read(ref this.renderThreadId))
			{
				return false;
			}

			lock (this.gate)
			{
				if (this.closing)
				{
					return false;
				}

				this.pending.Add(resource);
				return true;
			}
		}

		/// <summary>Runs every queued release. Call on the render thread, outside any draw.</summary>
		public void Drain()
		{
			IDisposable[] ready;
			lock (this.gate)
			{
				if (this.pending.Count == 0)
				{
					return;
				}

				ready = this.pending.ToArray();
				this.pending.Clear();
			}

			foreach (var resource in ready)
			{
				resource.Dispose();
			}
		}

		/// <summary>Stops queuing and runs what is queued: the context is going away.</summary>
		public void Close()
		{
			lock (this.gate)
			{
				this.closing = true;
			}

			this.Drain();
		}
	}
}
