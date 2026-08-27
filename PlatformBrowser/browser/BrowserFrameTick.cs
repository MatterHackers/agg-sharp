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
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// One turn of the browser host's frame loop: what a <c>requestAnimationFrame</c> callback does, in the
	/// order it has to do it.
	/// </summary>
	/// <remarks>
	/// <para>This is the body of <c>MacSystemWindow.RunEventLoop</c> with the loop taken away - the browser
	/// owns the loop and calls in once per frame - and it is a class of its own over injected delegates so
	/// that the ordering contract can be pinned by the desktop test suite. A rAF tick cannot be run in a test
	/// process, and there is every reason to know that browser events are drained before the idle queue, that
	/// a paint only happens when something asked for one, and that a throwing paint costs exactly one
	/// frame.</para>
	///
	/// <para><b>Identity, but no synchronization context.</b> Every drain claims the calling thread as agg's
	/// UI thread (<see cref="UiThread.MarkCurrentThreadAsUiThread"/>), the way each desktop pump does, so a
	/// queued action asking <see cref="UiThread.IsUiThread"/> gets the right answer. What this deliberately
	/// does NOT do is install <see cref="MainLoopSynchronizationContext"/>: on wasm the runtime's own
	/// single-threaded context already resumes continuations on this very thread, and routing them through the
	/// idle queue instead would put every await hop behind an animation frame - a chain of N genuinely
	/// suspending awaits would take N frames to unwind. The desktop hosts install it because their
	/// continuations would otherwise resume on a thread pool thread; here there is no other thread for them to
	/// resume on.</para>
	///
	/// <para><b>Nothing escapes into JS.</b> Each phase is contained and reported rather than allowed to
	/// unwind into the animation frame callback. A managed exception crossing back into JS would leave the
	/// page with a dead frame loop and an error in the console that nothing in agg ever hears about; reporting
	/// through <see cref="UiThread.ReportUnhandledException"/> is the channel the automation harness listens
	/// on, so a failure still fails loudly - it just fails alone.</para>
	/// </remarks>
	public sealed class BrowserFrameTick
	{
		private readonly Action drainBrowserEvents;
		private readonly Func<bool> canPaint;
		private readonly Action paintFrame;

		private bool insideIdleDrain;

		// True to begin with: a window that has been shown but never invalidated still owes the page its
		// first frame.
		private bool needsRedraw = true;

		/// <param name="drainBrowserEvents">Delivers the input and resize events that arrived since the last
		/// tick. Runs first: a widget must see the click before the idle action that reacts to it.</param>
		/// <param name="canPaint">Whether there is anything to paint into - a window that is still up and a
		/// render layer that exists. False during bring-up, which is what keeps
		/// <c>BrowserSystemWindow.NewGraphics2D</c>'s descriptive throw out of every frame.</param>
		/// <param name="paintFrame">Draws (and, once there is a device, presents) one frame.</param>
		public BrowserFrameTick(Action drainBrowserEvents, Func<bool> canPaint, Action paintFrame)
		{
			this.drainBrowserEvents = drainBrowserEvents ?? throw new ArgumentNullException(nameof(drainBrowserEvents));
			this.canPaint = canPaint ?? throw new ArgumentNullException(nameof(canPaint));
			this.paintFrame = paintFrame ?? throw new ArgumentNullException(nameof(paintFrame));
		}

		/// <summary>How many ticks have run. Diagnostics, and what a smoke run counts.</summary>
		public long TickCount { get; private set; }

		/// <summary>How many frames were actually painted, which is far fewer than <see cref="TickCount"/>.</summary>
		public long PaintCount { get; private set; }

		/// <summary>Whether a repaint has been asked for and not yet served.</summary>
		public bool NeedsRedraw => this.needsRedraw;

		/// <summary>
		/// Asks for a repaint on a following tick. There is no WM_PAINT here, so - exactly as on the mac and
		/// X11 hosts - invalidation is a flag the loop reads rather than a message, and the rectangle is
		/// ignored because the whole frame is redrawn either way.
		/// </summary>
		public void Invalidate() => this.needsRedraw = true;

		/// <summary>
		/// Runs one frame's worth of work: browser events, marshalled main-thread work, idle actions, paint.
		/// </summary>
		public void Tick()
		{
			this.TickCount++;

			RunPhase("browser events", this.drainBrowserEvents);

			// Parity with MacSystemWindow.RunEventLoop. In the browser this is a pass-through - the head sets
			// MainThreadDispatcher.MainThreadRequired false, because there is only one thread to be on - but a
			// host that skipped it would silently drop anything that ever did get posted.
			RunPhase("main thread dispatcher", MainThreadDispatcher.DrainPending);

			RunPhase("idle actions", this.DrainIdleActions);

			this.PaintIfNeeded();
		}

		/// <summary>
		/// Drains agg's RunOnIdle queue, claiming this thread as the UI thread first.
		/// </summary>
		/// <remarks>
		/// Guarded because an idle action can run a nested loop of its own and re-enter the tick; a nested
		/// loop that must instead let awaited continuations run calls <see cref="UiThread.DrainForNestedPump"/>,
		/// which deliberately skips this guard. Same rule as <c>MacSystemWindow.InvokeIdleActions</c>, minus
		/// its lock: browser events and animation frames run on the one thread the runtime has, so there is
		/// nothing to race with.
		/// </remarks>
		private void DrainIdleActions()
		{
			if (this.insideIdleDrain)
			{
				return;
			}

			this.insideIdleDrain = true;

			try
			{
				// Claimed on every drain rather than once at startup: anything may drain the queue (a test
				// helper, a nested pump), and whoever drained last would otherwise own the identity. See
				// UiThread.MarkCurrentThreadAsUiThread for what a wrong answer costs.
				UiThread.MarkCurrentThreadAsUiThread();
				UiThread.InvokePendingActions();
			}
			finally
			{
				this.insideIdleDrain = false;
			}
		}

		private void PaintIfNeeded()
		{
			if (!this.needsRedraw || !this.canPaint())
			{
				return;
			}

			// Cleared BEFORE the paint, exactly as MacSystemWindow.PaintFrame clears it first: a draw that
			// throws every time would otherwise re-arm itself and spin the loop at sixty failures a second,
			// burying the first and only interesting report. Cleared here, a failing frame repeats only as
			// often as something asks for a repaint.
			this.needsRedraw = false;

			this.PaintCount++;

			RunPhase("paint", this.paintFrame);
		}

		/// <summary>
		/// Runs one phase of the tick, containing and reporting whatever it throws. See the class remarks for
		/// why nothing may unwind out of a tick.
		/// </summary>
		private static void RunPhase(string phase, Action work)
		{
			try
			{
				work();
			}
			catch (Exception phaseException)
			{
				Console.Error.WriteLine(
					$"BrowserSystemWindow tick phase '{phase}' threw; this frame is abandoned and the loop continues: {phaseException}");

				UiThread.ReportUnhandledException(phaseException);
			}
		}
	}
}
