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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Keeps a window from painting inside its own paint, without losing the frame the nested request asked
	/// for.
	/// </summary>
	/// <remarks>
	/// A paint can be re-entered on Windows: the UI thread is STA, so a blocking wait inside a draw (lock
	/// contention, a COM wait) pumps messages, and one of them can be the idle pump's Invoke, whose
	/// Update() paints synchronously. A nested draw runs over half-built frame state - the scene renderer's
	/// full-frame capture reported it as "A full-frame capture is already in progress.". The host asks
	/// <see cref="TryBeginPaint"/> first and skips the draw on false; WinForms has already validated that
	/// paint's region, so the host must ask for a repaint when <see cref="EndPaint"/> says one was deferred.
	/// UI thread only - it is the one thread that paints, so there is nothing to lock.
	/// </remarks>
	public sealed class PaintReentrancyGuard
	{
		private bool repaintDeferred;

		/// <summary>Gets a value indicating whether a paint that was allowed to run has not ended yet.</summary>
		public bool IsPainting { get; private set; }

		/// <summary>
		/// Starts a paint. False means one is already in progress: skip the draw - the request is remembered
		/// and handed back by <see cref="EndPaint"/>.
		/// </summary>
		/// <returns>True when the caller should paint now.</returns>
		public bool TryBeginPaint()
		{
			if (this.IsPainting)
			{
				this.repaintDeferred = true;
				return false;
			}

			this.IsPainting = true;
			return true;
		}

		/// <summary>Ends the paint <see cref="TryBeginPaint"/> allowed. Call from a finally.</summary>
		/// <returns>True when a paint was deferred while this one ran, so the caller must request another.</returns>
		public bool EndPaint()
		{
			bool repaint = this.repaintDeferred;
			this.repaintDeferred = false;
			this.IsPainting = false;
			return repaint;
		}
	}
}
