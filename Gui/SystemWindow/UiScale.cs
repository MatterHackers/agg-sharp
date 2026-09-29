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
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Turns the display scale the platform hosts report into <see cref="GuiWidget.DeviceScale"/>, and keeps
	/// it right as a window moves between monitors. Every native agg app wants this; before it existed each
	/// app (MatterCAD, the AggSharpDemo, the colmap demo) carried its own copy.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The hosts only report: <c>OsInformation.DisplayScale</c> at startup (the primary monitor, the only
	/// answer before a window exists) and <see cref="SystemWindow.DisplayScale"/> /
	/// <see cref="SystemWindow.DisplayScaleChanged"/> for the monitor a window is really on. The policy here
	/// is <c>DeviceScale = multiplier x display</c> - one design unit is one point, so a UI designed at 1x is
	/// the same physical size on a 2x display - where the multiplier is the application's (MatterCAD's user
	/// text size; 1 for an app without one).
	/// </para>
	/// <para>
	/// Static because <see cref="GuiWidget.DeviceScale"/> is process wide: there is one scale in force, so
	/// there is one record of the display it was composed for. UI thread only.
	/// </para>
	/// </remarks>
	public static class UiScale
	{
		/// <summary>
		/// Display scales are coarse values a driver reports (1, 1.25, 1.5, 2, 3), so anything closer than
		/// this is the same monitor arriving by a different route, not a change worth rebuilding the UI for.
		/// </summary>
		public const double DisplayScaleEpsilon = 0.0001;

		/// <summary>The application's multiplier over the display scale; null means 1.</summary>
		private static Func<double> multiplier;

		/// <summary>
		/// How long a rescale waits before asking <c>canRebuildNow</c> again. Internal so tests can make the
		/// retry run on the next pump instead of waiting on the clock.
		/// </summary>
		internal static double RebuildRetrySeconds { get; set; } = 0.1;

		/// <summary>
		/// Forces the display scale this class composes with, instead of believing the OS or the window.
		/// Test-only: without it every widget size in a suite would depend on whether the machine running it
		/// has a Retina screen. Null in an application.
		/// </summary>
		/// <remarks>
		/// It pins the whole model, not just startup: a followed window reports the monitor it is actually on,
		/// so a pin that only covered <see cref="ApplyAtStartup"/> would let the first per-monitor report from
		/// a Retina machine rescale the suite anyway. A test that wants to simulate a monitor change clears the
		/// pin for the duration (and sets <see cref="SystemWindow.SimulatedDisplayScale"/> to hold the host).
		/// </remarks>
		public static double? DisplayScaleOverride { get; set; }

		/// <summary>
		/// The display scale <see cref="GuiWidget.DeviceScale"/> was last composed against - the launch
		/// display, or whatever monitor a followed window has since moved to.
		/// </summary>
		public static double CurrentDisplayScale { get; private set; } = 1;

		/// <summary>
		/// Raised after every recompute of <see cref="GuiWidget.DeviceScale"/> - <see cref="ApplyAtStartup"/>,
		/// <see cref="UpdateForDisplayScale"/>, and so every rescale <see cref="Follow"/> makes, before its
		/// rebuild. Where an app re-derives sizes it keeps in device pixels outside the widget tree (a scroll
		/// bar width, a window minimum), so no route that changes the scale can forget them.
		/// </summary>
		/// <remarks>
		/// Static and process wide like the scale itself: subscribe once for the life of the app, or
		/// unsubscribe when the thing it resizes goes away. UI thread, like everything here.
		/// </remarks>
		public static event EventHandler Rescaled;

		/// <summary>The multiplier <see cref="GuiWidget.DeviceScale"/> is composed with right now.</summary>
		public static double Multiplier => multiplier?.Invoke() ?? 1;

		/// <summary>
		/// Sets <see cref="GuiWidget.DeviceScale"/> for the display the OS says the app is starting on. Call
		/// once, before any widget is built - widget sizes and fonts are fixed when a widget is built.
		/// </summary>
		/// <param name="multiplier">
		/// The application's scale over the display's (a user text size), re-read every time the scale is
		/// recomputed; null for 1.
		/// </param>
		public static void ApplyAtStartup(Func<double> multiplier = null)
		{
			UiScale.multiplier = multiplier;

			// Read defensively: a headless process may have no provider at all, and that is a 1x display.
			UpdateForDisplayScale(EffectiveDisplayScale(AggContext.OsInformation?.DisplayScale ?? 1));
		}

		/// <summary>Puts the static state back to a fresh process's, for tests that change it.</summary>
		internal static void ResetForTests()
		{
			multiplier = null;
			DisplayScaleOverride = null;
			CurrentDisplayScale = 1;
			RebuildRetrySeconds = 0.1;
		}

		/// <summary>
		/// Recomputes <see cref="GuiWidget.DeviceScale"/> for a display of <paramref name="displayScale"/>
		/// device pixels per point, with the current <see cref="Multiplier"/>. Sets the scale only: widgets
		/// already built keep their sizes until something rebuilds them. Also what an app calls after its
		/// multiplier changes, passing <see cref="CurrentDisplayScale"/>.
		/// </summary>
		public static void UpdateForDisplayScale(double displayScale)
		{
			CurrentDisplayScale = Usable(displayScale);
			GuiWidget.DeviceScale = Multiplier * CurrentDisplayScale;
			Rescaled?.Invoke(null, EventArgs.Empty);
		}

		/// <summary>
		/// The display scale to compose with, for a window or an OS reporting <paramref name="reportedDisplayScale"/>.
		/// The single place <see cref="DisplayScaleOverride"/> is honoured, so startup and a window's reports
		/// can never disagree about what display the app is on.
		/// </summary>
		public static double EffectiveDisplayScale(double reportedDisplayScale)
		{
			return Usable(DisplayScaleOverride ?? reportedDisplayScale);
		}

		/// <summary>
		/// Whether a window reporting <paramref name="reportedDisplayScale"/> is on a different display than
		/// <see cref="GuiWidget.DeviceScale"/> was last composed for.
		/// </summary>
		/// <remarks>
		/// Usually not. A window's first report always raises <see cref="SystemWindow.DisplayScaleChanged"/>,
		/// carrying the display it opened on, which startup has usually composed in already - rebuilding the
		/// whole UI for that echo would flash every high-DPI user's UI a moment after launch.
		/// </remarks>
		public static bool NeedsUpdate(double reportedDisplayScale)
		{
			return Math.Abs(EffectiveDisplayScale(reportedDisplayScale) - CurrentDisplayScale) > DisplayScaleEpsilon;
		}

		/// <summary>
		/// A display scale that can be multiplied by. A monitor hot-plug can be caught mid-transition
		/// reporting 0, and a UI laid out at scale 0 has nothing in it, so anything not finite and positive
		/// is 1 - the same guard <see cref="SystemWindow.SetDisplayScale"/> applies.
		/// </summary>
		public static double Usable(double displayScale)
		{
			return double.IsNaN(displayScale) || double.IsInfinity(displayScale) || displayScale <= 0 ? 1 : displayScale;
		}

		/// <summary>
		/// A window size in device pixels for a design size in points, at the current
		/// <see cref="GuiWidget.DeviceScale"/> and clamped to the OS desktop.
		/// </summary>
		public static (int Width, int Height) ScaledWindowSize(double designWidth, double designHeight)
		{
			return ScaledWindowSize(designWidth, designHeight, GuiWidget.DeviceScale, AggContext.OsInformation?.DesktopSize ?? new Point2D(0, 0));
		}

		/// <summary>
		/// The design size scaled by <paramref name="deviceScale"/>, then clamped to
		/// <paramref name="desktopSize"/> (device pixels) so a 150% laptop does not open a window taller than
		/// its screen. A non-positive desktop dimension means the host could not measure it and is not clamped
		/// to - that would ask for a window with no pixels in it.
		/// </summary>
		public static (int Width, int Height) ScaledWindowSize(double designWidth, double designHeight, double deviceScale, Point2D desktopSize)
		{
			int width = (int)Math.Round(designWidth * deviceScale);
			int height = (int)Math.Round(designHeight * deviceScale);
			if (desktopSize.x > 0)
			{
				width = Math.Min(width, desktopSize.x);
			}

			if (desktopSize.y > 0)
			{
				height = Math.Min(height, desktopSize.y);
			}

			return (width, height);
		}

		/// <summary>
		/// Follows <paramref name="window"/> from monitor to monitor: when it reports a display scale this app
		/// is not composed for, updates <see cref="GuiWidget.DeviceScale"/> and calls <paramref name="rebuild"/>.
		/// </summary>
		/// <param name="window">The window whose <see cref="SystemWindow.DisplayScaleChanged"/> to follow. The
		/// subscription ends by itself when it closes.</param>
		/// <param name="rebuild">Rebuilds the UI at the new <see cref="GuiWidget.DeviceScale"/>.</param>
		/// <param name="canRebuildNow">
		/// False while a rebuild must not start (a reload already running, a long job holding the UI). The
		/// rescale is then retried on the idle queue until it is true, with at most one retry pending however
		/// many changes arrive. Null means always.
		/// </param>
		/// <param name="beforeRescale">
		/// Runs just before <see cref="GuiWidget.DeviceScale"/> changes, for anything that has to see the old
		/// scale - closing a UI that saves its layout in design units divides by it.
		/// </param>
		/// <returns>Disposing it stops following early.</returns>
		/// <remarks>
		/// The order of one rescale is <paramref name="beforeRescale"/> (old scale), the new
		/// <see cref="GuiWidget.DeviceScale"/>, <see cref="Rescaled"/>, then <paramref name="rebuild"/>. Keep
		/// <paramref name="rebuild"/> to rebuilding the widget tree and put device-pixel sizes the app derives
		/// from the scale (MatterCAD's <c>ScrollBar.ScrollBarWidth</c>, its window's minimum size) in a
		/// <see cref="Rescaled"/> handler: that also runs at startup and after a text-size change, which a
		/// size recomputed only here would miss.
		/// </remarks>
		public static IDisposable Follow(SystemWindow window, Action rebuild, Func<bool> canRebuildNow = null, Action beforeRescale = null)
		{
			ArgumentNullException.ThrowIfNull(window);
			ArgumentNullException.ThrowIfNull(rebuild);
			return new DisplayScaleFollower(window, rebuild, canRebuildNow, beforeRescale);
		}

		/// <summary>One window being followed - see <see cref="Follow"/>.</summary>
		private sealed class DisplayScaleFollower : IDisposable
		{
			private readonly SystemWindow window;
			private readonly Action rebuild;
			private readonly Func<bool> canRebuildNow;
			private readonly Action beforeRescale;

			/// <summary>
			/// True while a retry is waiting in the idle queue. One retry covers any number of changes:
			/// nothing about the scale is carried in it - it re-reads the window when it runs - so a second
			/// chain would only rebuild the same UI twice.
			/// </summary>
			private bool retryPending;

			private bool disposed;

			public DisplayScaleFollower(SystemWindow window, Action rebuild, Func<bool> canRebuildNow, Action beforeRescale)
			{
				this.window = window;
				this.rebuild = rebuild;
				this.canRebuildNow = canRebuildNow;
				this.beforeRescale = beforeRescale;
				window.DisplayScaleChanged += this.Window_DisplayScaleChanged;
				window.Closed += this.Window_Closed;
			}

			public void Dispose()
			{
				if (this.disposed)
				{
					return;
				}

				this.disposed = true;
				this.window.DisplayScaleChanged -= this.Window_DisplayScaleChanged;
				this.window.Closed -= this.Window_Closed;
			}

			private void Window_Closed(object sender, EventArgs e) => this.Dispose();

			// DisplayScaleChanged is raised from the idle queue, already coalesced, so this is on the UI thread
			// and a drag across several displays arrives once, describing where the window ended up.
			private void Window_DisplayScaleChanged(object sender, EventArgs e) => this.Rescale();

			private void Rescale()
			{
				// A retry can outlive the window; rebuilding into a closed one only fails.
				if (this.disposed || this.window.HasBeenClosed)
				{
					return;
				}

				// Read now, not when the change arrived: a deferred retry must see where the window ended up,
				// which may be back on the display the UI is already built for.
				if (!NeedsUpdate(this.window.DisplayScale))
				{
					return;
				}

				if (this.canRebuildNow != null && !this.canRebuildNow())
				{
					if (!this.retryPending)
					{
						this.retryPending = true;
						UiThread.RunOnIdle(
							() =>
							{
								// Cleared as the retry begins, so a change that arrives from here on is free
								// to queue a retry of its own.
								this.retryPending = false;
								this.Rescale();
							},
							RebuildRetrySeconds);
					}

					return;
				}

				// DeviceScale changes only now, when the rebuild can actually run: a UI still being built (or
				// torn down) by whatever made us wait keeps one consistent scale throughout.
				this.beforeRescale?.Invoke();
				UpdateForDisplayScale(EffectiveDisplayScale(this.window.DisplayScale));
				this.rebuild();
			}
		}
	}
}
