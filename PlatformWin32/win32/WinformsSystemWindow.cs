//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
using System;
using System.ComponentModel;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using Agg;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public abstract class WinformsSystemWindow : Form, IPlatformWindow
	{
		public static bool SingleWindowMode { get; set; } = false;

		public static bool EnableInputHook { get; set; } = true;

		public static bool ShowingSystemDialog { get; set; } = false;

		public static WinformsSystemWindow MainWindowsFormsWindow { get; private set; }

		public static Func<SystemWindow, FormInspector> InspectorCreator { get; set; }

		private static System.Timers.Timer idleCallBackTimer = null;

		// The window whose InvokePendingOnIdleActions is currently subscribed to the shared timer. Only one
		// window drives the pump at a time; UiThread actions are marshaled through whichever window that is.
		private static WinformsSystemWindow idleTimerWindow = null;

		// Every constructed window that has not closed yet. More than one can be alive at a time (automation
		// tests run windows back to back and in parallel), and the shared idle timer must keep being driven by
		// one of them - stopping it while a window was still up left that window's RunOnIdle pump dead, so its
		// message loop (and the close that ends it) never ran again.
		private static readonly System.Collections.Generic.List<WinformsSystemWindow> LiveWindows = new System.Collections.Generic.List<WinformsSystemWindow>();

		private static bool processingOnIdle = false;

		// Set by Invalidate(RectangleDouble) and consumed by the idle pump's FlushPendingAggInvalidates.
		// Volatile: set from any thread, cleared on the UI thread.
		private volatile bool aggInvalidatePending;

		private static readonly object SingleInvokeLock = new object();

		// Guards one-time static initialization done from instance constructors
		// (idleCallBackTimer creation and the MainWindowsFormsWindow first-window latch)
		// so concurrent construction cannot create two timers or two "main" windows.
		private static readonly object StaticInitLock = new object();

		// Probe for the application icon once per process rather than in every window
		// constructor. Preserves the original probe order and silent-failure behavior.
		private static readonly Lazy<Icon> ApplicationIcon = new Lazy<Icon>(() =>
		{
			string iconPath = File.Exists("application.ico") ?
				"application.ico" :
				"../MonoBundle/StaticData/application.ico";

			try
			{
				if (File.Exists(iconPath))
				{
					return new Icon(iconPath);
				}
			}
			catch
			{
			}

			return null;
		});

		protected WinformsEventSink EventSink;

		private SystemWindow _systemWindow;
		private int drawCount = 0;
		private int onPaintCount;
		private bool enableIdleProcessing;

		// --- Unattended smoke runs -------------------------------------------------------------------
		// Read once, from the environment, because the point is to drive an *unmodified* demo: no demo has
		// to know it is being smoke tested, and with the variables unset none of this does anything.
		private static readonly int SmokeFrameTarget = ParseSmokeFrames();
		private static readonly string SmokeScreenshotPath = Environment.GetEnvironmentVariable("AGG_SMOKE_SCREENSHOT");

		private bool smokeRunFinished;

		/// <summary>
		/// How many frames a smoke run draws before it screenshots and closes itself
		/// (<c>AGG_SMOKE_FRAMES</c>), or 0 when the window should behave normally.
		/// </summary>
		public static int SmokeFrames => SmokeFrameTarget;

		/// <summary>
		/// What the renderer has to complain about, or null when it is happy. A smoke run turns a
		/// non-null value into a non-zero process exit code; the classic paths report nothing, which is
		/// as good as they can do - they fail by throwing.
		/// </summary>
		public virtual string RenderErrorReport => null;

		/// <summary>
		/// What the renderer wants said about a finished run - which backend, how many frames actually
		/// reached the screen. A smoke run prints it, which is how "60 frames drawn" is distinguished
		/// from "60 frames drawn and none of them presented".
		/// </summary>
		public virtual string RenderStatusReport => null;

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public SystemWindow AggSystemWindow
		{
			get => _systemWindow;
			set
			{
				_systemWindow = value;

				if (_systemWindow != null)
				{
					this.Caption = _systemWindow.Title;

					if (SingleWindowMode)
					{
						if (firstWindow)
						{
							this.MinimumSize = _systemWindow.MinimumSize;
						}

						// Set this system window as the event target
						this.EventSink?.SetActiveSystemWindow(_systemWindow);
					}
					else
					{
						this.MinimumSize = _systemWindow.MinimumSize;
					}

					// Seed the display scale, and re-seed it in single window mode where the same Form draws
					// a succession of SystemWindows: a dialog constructed while the shell sat on a 1x monitor
					// is about to be drawn on whatever monitor the shell is on now, and OnDpiChanged only ever
					// reaches whichever window was attached when the monitor changed.
					_systemWindow.SetDisplayScale(this.DeviceDpi / 96.0);
					this.PushDisplayUsableSize();
				}
			}
		}

		/// <summary>
		/// Tells the attached agg window how much room the monitor this Form is on has for it, in device
		/// pixels - the companion to <see cref="SystemWindow.SetDisplayScale"/>, pushed from the same places.
		/// </summary>
		/// <remarks>
		/// <c>WorkingArea</c> rather than <c>Bounds</c>: it already has the taskbar taken out of it, matching
		/// what the mac host reports from <c>visibleFrame</c>.
		/// <para>
		/// The rectangle is taken as physical pixels. WinForms' <see cref="Screen"/> hands back the monitor
		/// rectangle Win32 reports for the virtual desktop, which it does not rescale, and in a per-monitor
		/// v2 process that desktop is in physical pixels - the same space <c>ClientSize</c> is in when this
		/// Form's own bounds are pushed to <see cref="SystemWindow.SetBoundsFromPlatform"/>. A
		/// system-DPI-aware process is the one case where Windows virtualises those coordinates for the
		/// non-primary monitor; the resulting size is off by the DPI ratio there, but such a process is
		/// bitmap-scaled throughout and never sees a scale change either.
		/// </para>
		/// <para>
		/// Skipped before the handle exists: <see cref="Screen.FromControl"/> reads <c>Control.Handle</c> and
		/// would force the premature handle creation the rest of this class is careful to avoid.
		/// OnHandleCreated pushes it as soon as there is one.
		/// </para>
		/// </remarks>
		private void PushDisplayUsableSize()
		{
			var systemWindow = AggSystemWindow;
			if (systemWindow == null || !this.IsHandleCreated)
			{
				return;
			}

			var workingArea = Screen.FromControl(this).WorkingArea;

			systemWindow.SetDisplayUsableSize(new Vector2(workingArea.Width, workingArea.Height));
		}

		/// <summary>
		/// Handles the window being dragged onto a monitor with a different DPI, publishing the new scale on
		/// the attached <see cref="SystemWindow"/> so the application can rebuild its UI for it.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The base implementation does the window's own half of the work - it accepts the rectangle Windows
		/// suggests for the new monitor and rescales the Form - so this only has to publish the factor. 96 is
		/// the reference DPI Windows scales everything against, so <c>DeviceDpi / 96</c> is 1 at 100%, 1.5 at
		/// 150% and 2 at 200%, matching what the mac host reports as its backing scale factor.
		/// </para>
		/// <para>
		/// WM_DPICHANGED is only ever sent to a process that declared itself per-monitor DPI aware, which is
		/// the application's call to make (its manifest or its startup), not this library's. In a
		/// system-DPI-aware process this override simply never runs, which is the correct behaviour there:
		/// Windows bitmap-scales the window instead and its scale genuinely does not change.
		/// </para>
		/// <para>
		/// The raise is deferred by <see cref="SystemWindow.SetDisplayScale"/> onto the idle queue rather than
		/// running here. A subscriber rebuilds the whole UI, and doing that inside the WM_DPICHANGED handler
		/// would run it while Windows is still repositioning the window across the monitor boundary.
		/// </para>
		/// </remarks>
		protected override void OnDpiChanged(DpiChangedEventArgs e)
		{
			base.OnDpiChanged(e);

			AggSystemWindow?.SetDisplayScale(e.DeviceDpiNew / 96.0);
			this.PushDisplayUsableSize();
		}


		public bool IsMainWindow { get; } = false;

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public bool IsInitialized { get; set; } = false;

		public WinformsSystemWindow()
		{
			lock (StaticInitLock)
			{
				LiveWindows.Add(this);

				// Create the shared idle timer, or revive one that a previous window's teardown stopped.
				EnsureIdleTimerDriving(this);

				// Track first window
				if (MainWindowsFormsWindow == null)
				{
					MainWindowsFormsWindow = this;
					IsMainWindow = true;
				}
			}

			// TitleBarHeight is intentionally NOT computed here: RectangleToScreen would force
			// premature Win32 handle creation in the constructor. See OnHandleCreated.
			if (SystemWindow.EnableAllowDrop)
			{
				this.AllowDrop = true;
			}

			if (ApplicationIcon.Value != null)
			{
				// This Icon instance is process-shared across all windows (static Lazy).
				// Form.Dispose does not dispose an assigned Icon, so this is safe - but never
				// Dispose it per-window or every other window's icon handle dies with it.
				this.Icon = ApplicationIcon.Value;
			}
		}

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			// Compute the title bar height now that the native handle exists. Doing this in
			// the constructor forced premature handle creation via RectangleToScreen.
			titleBarHeight = RectangleToScreen(ClientRectangle).Top - this.Top;
			titleBarHeightComputed = true;

			// The constructor could not make this window the idle driver - a Form has no handle until it is
			// shown and a handle-less window cannot marshal work to the UI thread. Now that it can, offer it
			// (EnsureIdleTimerDriving keeps a working driver, so this is a no-op when one already exists).
			lock (StaticInitLock)
			{
				EnsureIdleTimerDriving(this);
			}

			// Re-seed the display scale now that there is a handle. The seed in AggSystemWindow's setter runs
			// before there is one, and a handle-less Form's DeviceDpi is the PROCESS DPI, not the DPI of the
			// monitor the window is about to appear on. Nothing else corrects it: WM_DPICHANGED is only sent
			// when a window MOVES between monitors, never for its initial placement, so a window restored onto
			// a secondary display of a different scale would report the wrong one for its whole life. In the
			// single-monitor case the value is already right and SetDisplayScale coalesces the repeat away.
			AggSystemWindow?.SetDisplayScale(this.DeviceDpi / 96.0);
			this.PushDisplayUsableSize();
		}

		/// <summary>
		/// Makes sure the process-wide idle timer exists and is running against a window that can still
		/// marshal work to the UI thread, or stops it when no such window is left. Must be called with
		/// <see cref="StaticInitLock"/> held.
		/// </summary>
		/// <param name="preferredWindow">The window to drive the pump when the current driver is gone.</param>
		/// <remarks>
		/// A driver must have its native handle: a handle-less window cannot marshal anything, so parking the
		/// pump on one is just the dead-pump bug by another name. Windows without a handle stay in
		/// <see cref="LiveWindows"/> and become eligible again from OnHandleCreated.
		/// </remarks>
		private static void EnsureIdleTimerDriving(WinformsSystemWindow preferredWindow)
		{
			WinformsSystemWindow driver = null;

			if (idleTimerWindow != null
				&& !idleTimerWindow.IsDisposed
				&& idleTimerWindow.IsHandleCreated
				&& LiveWindows.Contains(idleTimerWindow))
			{
				// Keep the current driver - swapping it needlessly would drop timer ticks.
				driver = idleTimerWindow;
			}
			else if (preferredWindow != null
				&& !preferredWindow.IsDisposed
				&& preferredWindow.IsHandleCreated
				&& LiveWindows.Contains(preferredWindow))
			{
				driver = preferredWindow;
			}
			else
			{
				foreach (var window in LiveWindows)
				{
					if (!window.IsDisposed
						&& window.IsHandleCreated)
					{
						driver = window;
						break;
					}
				}
			}

			if (driver == null)
			{
				// Nothing left to pump. Unsubscribe and stop, but keep the timer instance around so the
				// next window can revive it - disposing it here is what used to make the pump unrecoverable.
				if (idleCallBackTimer != null)
				{
					if (idleTimerWindow != null)
					{
						idleCallBackTimer.Elapsed -= idleTimerWindow.InvokePendingOnIdleActions;
					}

					idleTimerWindow = null;
					idleCallBackTimer.Stop();
				}

				return;
			}

			if (idleCallBackTimer == null)
			{
				// call up to 100 times a second
				idleCallBackTimer = new System.Timers.Timer { Interval = 10 };
			}

			if (idleTimerWindow != driver)
			{
				if (idleTimerWindow != null)
				{
					idleCallBackTimer.Elapsed -= idleTimerWindow.InvokePendingOnIdleActions;
				}

				idleCallBackTimer.Elapsed += driver.InvokePendingOnIdleActions;
				idleTimerWindow = driver;
			}

			if (!idleCallBackTimer.Enabled)
			{
				idleCallBackTimer.Start();
			}
		}

		/// <summary>
		/// Drops this window from the live set and hands the shared idle pump to another live window
		/// (or stops it when this was the last one).
		/// </summary>
		private void ReleaseIdleTimer()
		{
			lock (StaticInitLock)
			{
				LiveWindows.Remove(this);

				// Windows that were disposed without ever reaching OnClosed would otherwise be held alive
				// by this list forever.
				LiveWindows.RemoveAll(window => window.IsDisposed);

				EnsureIdleTimerDriving(null);
			}
		}

		protected override void OnClosed(EventArgs e)
		{
			ReleaseIdleTimer();

			if (IsMainWindow)
			{
				// Ensure that when the MainWindow is closed, we null the field so we can recreate the MainWindow
				MainWindowsFormsWindow = null;
			}

			// Remove the input handlers the sink wired onto the hooked control in its constructor
			EventSink?.Unhook();
			EventSink = null;

			AggSystemWindow = null;

			base.OnClosed(e);
		}

		public void ReleaseOnIdleGuard()
		{
			lock (SingleInvokeLock)
			{
				processingOnIdle = false;
			}
		}

		private void InvokePendingOnIdleActions(object sender, ElapsedEventArgs e)
		{
			// Disposing matters as much as IsDisposed: IsDisposed only goes true once Dispose has run to
			// completion, but Control.Invoke already throws ObjectDisposedException the moment Dispose
			// starts - and the handle still reports as created right through that window. A tick landing
			// there is what printed "Cannot access a disposed object" on every clean shutdown.
			if (this.IsDisposed || this.Disposing)
			{
				// This window can no longer marshal anything. Hand the pump to a window that still can
				// rather than silently swallowing every queued action from here on.
				ReleaseIdleTimer();
			}
			else
			{
				lock (SingleInvokeLock)
				{
					if (!enableIdleProcessing)
					{
						// There's a race between the idle timer calling this handler and the code to
						// start the main event loop. Reaching this handler first seems to cause the
						// app to get stuck when running the automation test suite on Linux.
						return;
					}

					if (processingOnIdle)
					{
						// If the pending invoke has not completed, skip the timer event
						return;
					}

					processingOnIdle = true;
				}

				// A failure to hand the work to the UI thread and a failure inside that work surface here as
				// the same exception type, and only the second is a real bug. This flag separates them: it is
				// set on the UI thread before any queued action runs, so an exception seen with it still
				// clear can only have come from Invoke itself never getting the work across.
				bool reachedUiThread = false;

				try
				{
					if (!IsHandleCreated)
					{
						// This handler runs on the timer's threadpool thread. InvokeRequired reports false
						// both before the handle is created and after it has been destroyed, so trusting it
						// here would run UI callbacks (GL, textures, widget code) on the timer thread and
						// latch that thread as UiThread's "ui thread". Leave the actions queued instead;
						// they run on the next tick once the window can marshal them.
						//
						// Hand the pump to a window that can marshal right now rather than ticking uselessly
						// here forever. This window is NOT removed from LiveWindows - its handle may simply not
						// exist yet, and OnHandleCreated offers it back as a driver once it does.
						lock (StaticInitLock)
						{
							EnsureIdleTimerDriving(null);
						}

						return;
					}

					if (InvokeRequired)
					{
						Invoke(new Action(() =>
						{
							reachedUiThread = true;
							UiThread.InvokePendingActions();
							FlushPendingAggInvalidates();
						}));
					}
					else
					{
						reachedUiThread = true;
						UiThread.InvokePendingActions();
						FlushPendingAggInvalidates();
					}
				}
				catch (ObjectDisposedException) when (!reachedUiThread)
				{
					// Invoke could not get the work across, which at this point only ever means teardown.
					// Two shapes of it, both benign and both reported as ObjectDisposedException:
					//   - the window is disposing or disposed (IsDisposed lags Disposing, and the handle keeps
					//     answering as created right through Dispose, so neither is a reliable test on its own)
					//   - the thread's WinForms message loop has already exited, which Invoke reports as a
					//     disposed object naming the control even though the control itself is untouched
					//     (IsDisposed, Disposing and IsHandleCreated all say it is healthy). This is what the
					//     GPU windows hit: their close tears the loop down before the form.
					// The queued actions stay queued either way; nothing is lost that a live window would run.
				}
				catch (InvalidOperationException) when (!reachedUiThread)
				{
					// The handle was destroyed between the checks above and the marshaled call - Invoke has no
					// thread to marshal to. Benign teardown race, same as above.
				}
				catch (Exception ex)
				{
					UiThread.ReportUnhandledException(ex);
					Console.WriteLine(ex.Message);
				}
				finally
				{
					lock (SingleInvokeLock)
					{
						processingOnIdle = false;
					}
				}
			}
		}

		public abstract Graphics2D NewGraphics2D();

		protected override void OnPaint(PaintEventArgs paintEventArgs)
		{
			if (AggSystemWindow == null
				|| AggSystemWindow.HasBeenClosed)
			{
				return;
			}

			// An unattended run must fail loudly rather than stopping at WinForms' modal
			// unhandled-exception dialog, which nobody is there to dismiss - and a paint that throws
			// takes the repaint pump with it, so the run would otherwise just sit there.
			if (SmokeFrameTarget > 0)
			{
				try
				{
					this.PaintFrame(paintEventArgs);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"AGG_SMOKE paint failed on frame {drawCount}: {ex}");
					Environment.ExitCode = 1;
					smokeRunFinished = true;
					this.BeginInvoke((Action)this.FinishSmokeRun);
				}

				return;
			}

			this.PaintFrame(paintEventArgs);
		}

		private void PaintFrame(PaintEventArgs paintEventArgs)
		{
			MatterHackers.RenderCore.FrameProfiler.BeginFrame();

			base.OnPaint(paintEventArgs);

			if (ShowingSystemDialog)
			{
				// We do this because calling Invalidate within an OnPaint message will cause our
				// SaveDialog to not show its 'overwrite' dialog if needed.
				// We use the Invalidate to cause a continuous pump of the OnPaint message to call our OnIdle.
				// We could figure another solution but it must be very careful to ensure we don't break SaveDialog
				return;
			}

			if (ClientSize.Width > 0 && ClientSize.Height > 0)
			{
				drawCount++;

				Graphics2D graphics2D;
				using (MatterHackers.RenderCore.FrameProfiler.Time("NewGraphics2D+Acquire"))
				{
					graphics2D = this.NewGraphics2D();
				}

				using (MatterHackers.RenderCore.FrameProfiler.Time("WidgetTreeDraw"))
				{
					if (!SingleWindowMode)
					{
						// We must call on draw background as this is effectively our child and that is the way it is done in GuiWidget.
						// Parents call child OnDrawBackground before they call OnDraw
						AggSystemWindow.OnDrawBackground(graphics2D);
						AggSystemWindow.OnDraw(graphics2D);
					}
					else
					{
						for (var i = 0; i < this.WindowProvider.OpenWindows.Count; i++)
						{
							graphics2D.FillRectangle(this.WindowProvider.OpenWindows[0].LocalBounds, new Color(Color.Black, 160));
							this.WindowProvider.OpenWindows[i].OnDraw(graphics2D);
						}
					}
				}

				// A widget that rasterized into Graphics2D.DestImage drew into a CPU buffer, not into the
				// frame. On a GPU surface that buffer is a layer this uploads and draws over the frame now,
				// after every widget has had its turn - the agg demos that rasterize by hand (aa_demo,
				// FontHinting and friends) are the consumers, and on a CPU surface there is nothing to do
				// because DestImage *is* the frame.
				if (graphics2D is Graphics2DGpu gpuGraphics && gpuGraphics.HasCpuLayer)
				{
					// A composite every frame means some widget is rasterizing on the CPU into DestImage;
					// the stack of whoever first asked is printed by Graphics2DGpu.
					MatterHackers.RenderCore.FrameProfiler.Count("CompositeCpuLayer");
					using (MatterHackers.RenderCore.FrameProfiler.Time("CompositeCpuLayer"))
					{
						gpuGraphics.CompositeCpuLayer();
					}
				}

				// Before the present, because a GPU window can only read a frame back while the frame's
				// texture is still the one being drawn into.
				CheckSmokeRunProgress();

				using (MatterHackers.RenderCore.FrameProfiler.Time("Present"))
				{
					CopyBackBufferToScreen(paintEventArgs.Graphics);
				}
			}

			MatterHackers.RenderCore.FrameProfiler.EndFrame();

			// A demo that has nothing to animate would paint once and wait forever for input that a smoke
			// run never sends, so the run pumps its own frames.
			if (SmokeFrameTarget > 0 && !smokeRunFinished)
			{
				this.Invalidate();
			}

			// use this to debug that windows are drawing and updating.
			onPaintCount++;
			// Text = string.Format("Draw {0}, OnPaint {1}", drawCount, onPaintCount);
		}

		public abstract void CopyBackBufferToScreen(Graphics displayGraphics);

		/// <summary>
		/// Counts frames for an <c>AGG_SMOKE_FRAMES</c> run and, on the target frame, asks for the
		/// screenshot and schedules the close. Called from inside the paint, after the widgets have drawn
		/// and before the present, which is the only moment both a finished frame and its pixels exist.
		/// </summary>
		private void CheckSmokeRunProgress()
		{
			if (SmokeFrameTarget <= 0 || smokeRunFinished || drawCount < SmokeFrameTarget)
			{
				return;
			}

			smokeRunFinished = true;

			if (!string.IsNullOrEmpty(SmokeScreenshotPath))
			{
				try
				{
					this.CaptureScreenshot(SmokeScreenshotPath);
				}
				catch (Exception ex)
				{
					Console.Error.WriteLine($"AGG_SMOKE screenshot failed: {ex}");
					Environment.ExitCode = 1;
				}
			}

			// Closing from inside a paint would tear down the window mid-frame (and, on a GPU window,
			// before the screenshot's present has run), so the close waits for this message to finish.
			this.BeginInvoke((Action)this.FinishSmokeRun);
		}

		private void FinishSmokeRun()
		{
			string report = this.RenderErrorReport;
			if (!string.IsNullOrEmpty(report))
			{
				Console.Error.WriteLine($"AGG_SMOKE render error: {report}");
				Environment.ExitCode = 1;
			}

			string status = this.RenderStatusReport;
			string detail = $"{drawCount} frames on {this.GetType().Name}"
				+ (string.IsNullOrEmpty(status) ? string.Empty : $" [{status}]");

			// The exit code was already right, but a run that failed used to print "ok" anyway, and the
			// console line is what a human (and every log scraper) reads first.
			if (Environment.ExitCode != 0)
			{
				Console.WriteLine($"AGG_SMOKE FAILED: {detail}");
			}
			else
			{
				Console.WriteLine($"AGG_SMOKE ok: {detail}");
			}

			// Armed before the close, not after: a close that throws or blocks is exactly the case the
			// watchdog exists for.
			StartSmokeExitWatchdog();

			try
			{
				// Closing the agg window is what tears the platform window down with it; the form's own
				// Close is only the fallback for a window that was never attached to one.
				var windowToClose = this.ShellAggWindow();

				if (windowToClose != null)
				{
					windowToClose.Close();
				}
				else
				{
					this.Close();
				}
			}
			catch (Exception ex)
			{
				// The close raced its own teardown, or a demo threw on the way out. Worth saying out loud
				// - it is the difference between "closed cleanly" and "the watchdog had to shoot it" - but
				// not worth failing the run over, since the frames all rendered.
				Console.Error.WriteLine($"AGG_SMOKE: close threw {ex.GetType().Name}: {ex}");
			}
		}

		/// <summary>
		/// The agg window whose close ends the application: the shell, not whatever is currently on top.
		/// </summary>
		/// <remarks>
		/// In single window mode <see cref="AggSystemWindow"/> is the window being drawn and given the
		/// events, which the provider re-points at every dialog that opens. Closing that only dismisses the
		/// dialog - the shell stays up, the message loop keeps running, and the process never exits. The
		/// provider keeps the shell first in <see cref="ISystemWindowProvider.OpenWindows"/> and takes the
		/// dialogs above it down with it, so closing that one window is the whole application closing.
		/// Kept identical to MacSystemWindow.
		/// </remarks>
		private SystemWindow ShellAggWindow()
		{
			return ShellWindowForClose(SingleWindowMode, this.WindowProvider, this.AggSystemWindow);
		}

		/// <summary>
		/// The instance-free half of <see cref="ShellAggWindow"/>. Kept identical to MacSystemWindow, where
		/// the unit tests for this decision live (PlatformWin32 does not build on the mac they run on).
		/// </summary>
		internal static SystemWindow ShellWindowForClose(
			bool singleWindowMode,
			ISystemWindowProvider provider,
			SystemWindow activeWindow)
		{
			if (singleWindowMode && provider != null)
			{
				var openWindows = provider.OpenWindows;

				if (openWindows.Count > 0)
				{
					return openWindows[0];
				}
			}

			return activeWindow;
		}

		/// <summary>
		/// Guarantees a smoke run terminates. Closing the window ends the message loop, but a teardown that
		/// throws part way (leaving the platform window up) or a demo that left a foreground thread running
		/// would keep the process alive forever, and an unattended run that never returns is
		/// indistinguishable from a hang in the renderer.
		/// </summary>
		/// <remarks>
		/// Firing is itself a failure and is reported as one. It used to exit with whatever code the run had
		/// earned, so a shutdown bug that only the watchdog caught scrolled past as a green run - which is
		/// exactly how a teardown exception in ListBox.RemoveChild went unnoticed.
		/// </remarks>
		private static void StartSmokeExitWatchdog()
		{
			var watchdog = new System.Threading.Timer(
				_ =>
				{
					Console.Error.WriteLine("AGG_SMOKE: the process did not exit on its own after closing; forcing exit.");
					Console.WriteLine("AGG_SMOKE FAILED: the exit watchdog had to force the process down.");
					Environment.Exit(Environment.ExitCode != 0 ? Environment.ExitCode : 1);
				},
				null,
				TimeSpan.FromSeconds(5),
				System.Threading.Timeout.InfiniteTimeSpan);

			// Nothing else holds this; keeping the reference alive is the only thing standing between the
			// timer and the collector.
			smokeExitWatchdog = watchdog;
		}

		private static System.Threading.Timer smokeExitWatchdog;

		private static int ParseSmokeFrames()
		{
			return int.TryParse(Environment.GetEnvironmentVariable("AGG_SMOKE_FRAMES"), out int frames) && frames > 0
				? frames
				: 0;
		}

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			// don't call this so that windows will not erase the background.
			// base.OnPaintBackground(e);
		}

		protected override void OnResize(EventArgs e)
		{
			var systemWindow = AggSystemWindow;
			if (systemWindow != null)
			{
				// The client area is this big whatever the application's minimum says - a per-monitor-DPI change
				// can shrink it below a minimum the application computed in pixels for the previous display, and
				// agg being y-up, laying out larger than the client area clips off the top of the window rather
				// than the bottom. The Form's own MinimumSize is what stops the user dragging it smaller.
				systemWindow.SetBoundsFromPlatform(ClientSize.Width, ClientSize.Height);

				// A resize is also how a maximize, a restore, and a docked taskbar appearing reach us, all of
				// which change how much room the monitor has left for this window.
				this.PushDisplayUsableSize();

				// Wait until the control is initialized (and thus WindowState has been set) to ensure we don't wipe out
				// the persisted data before its loaded
				if (this.IsInitialized)
				{
					// Push the current maximized state into the SystemWindow where it can be used or persisted by Agg applications
					systemWindow.Maximized = this.WindowState == FormWindowState.Maximized;
				}

				systemWindow.Invalidate();
			}

			base.OnResize(e);
		}

		protected override void SetVisibleCore(bool value)
		{
			// Force Activation/BringToFront behavior when Visibility enabled. This ensures Agg forms
			// always come to front after ShowSystemWindow()
			if (value)
			{
				this.Activate();
			}

			base.SetVisibleCore(value);
		}

		private bool winformAlreadyClosing = false;

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			if (!HandlePlatformCloseRequest(
				SingleWindowMode,
				this.WindowProvider,
				this.AggSystemWindow,
				this.SetPlatformClosing))
			{
				e.Cancel = true;
			}

			base.OnClosing(e);
		}

		/// <summary>
		/// Runs a native close request - the X button, Alt-F4, the shell asking the app to exit - against the
		/// application rather than against whatever window happens to be on top, and reports whether the form
		/// may go ahead and close. Kept identical to MacSystemWindow, where the unit tests for it live.
		/// </summary>
		/// <param name="singleWindowMode">See <see cref="SingleWindowMode"/>.</param>
		/// <param name="provider">The provider holding the open windows, if there is one.</param>
		/// <param name="activeWindow">The window currently being drawn and given events.</param>
		/// <param name="setPlatformClosing">
		/// Sets (and, if the close does not take, clears) the host's "the platform is already closing" flag.
		/// </param>
		internal static bool HandlePlatformCloseRequest(
			bool singleWindowMode,
			ISystemWindowProvider provider,
			SystemWindow activeWindow,
			Action<bool> setPlatformClosing)
		{
			// The user closed the application, not the dialog drawn inside it. Asking the dialog runs none of
			// the shell's ShouldClose/Closed handlers - window bounds persistence, save on exit - and the
			// form is destroyed immediately afterwards regardless, so that work is simply lost.
			var shellWindow = ShellWindowForClose(singleWindowMode, provider, activeWindow);

			if (shellWindow == null || shellWindow.HasBeenClosed)
			{
				return true;
			}

			// Only the shell decides whether the application may close: an open dialog does not veto here.
			// In single window mode a dialog is a widget drawn inside this window, so its titlebar button is
			// the only close that belongs to it - the X and Alt-F4 have always meant "close the application",
			// and applications that want to refuse mid-dialog do it in their own ShouldClose ("do you want
			// to save?" and friends).
			var shouldClose = new ShouldCloseEventArgs();
			shellWindow.OnShouldClose(shouldClose);

			if (shouldClose.Cancel)
			{
				return false;
			}

			setPlatformClosing?.Invoke(true);
			shellWindow.Close();

			if (!shellWindow.HasBeenClosed)
			{
				// Close asks OnShouldClose a second time and an application may cancel on that one (having
				// just put up its "save first?" dialog on the first ask). Letting the platform destroy the
				// window anyway is exactly the "closed with no Closed events" bug, so the shell that is still
				// open keeps its form.
				setPlatformClosing?.Invoke(false);
				return false;
			}

			return true;
		}

		/// <summary>
		/// Puts this form into (or back out of) the state where the platform, not the agg window, is driving
		/// the close: <see cref="CloseSystemWindow"/> becomes a no-op and the shared idle pump is handed off.
		/// </summary>
		private void SetPlatformClosing(bool closing)
		{
			// Store that the Close operation started here
			winformAlreadyClosing = closing;

			if (!this.IsMainWindow)
			{
				return;
			}

			if (closing)
			{
				// Stop the RunOnIdle timer/pump - but only if no other window still needs it. Killing
				// the shared pump outright left any window that was still up (parallel automation tests)
				// with a dead RunOnIdle and an idle message loop that could never close itself.
				ReleaseIdleTimer();

				// Workaround for "Cannot access disposed object." exception
				// https://stackoverflow.com/a/9669702/84369 - ".Stop() without .DoEvents() is not enough, as it'll dispose objects without waiting for your thread to finish its work"
				Application.DoEvents();
			}
			else
			{
				// The close did not take after all, so the application is still running and needs the pump
				// back - without this a vetoed close would leave RunOnIdle dead and nothing able to close.
				lock (StaticInitLock)
				{
					if (!LiveWindows.Contains(this))
					{
						LiveWindows.Add(this);
					}

					EnsureIdleTimerDriving(this);
				}
			}
		}

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public ISystemWindowProvider WindowProvider { get; set; }

		private Keys overrideModifierKeys = Keys.None;
		private bool modifiersOverridden = false;

		internal void SetModifierKeys(Keys modifiers)
		{
			overrideModifierKeys = modifiers;
			modifiersOverridden = true;
		}

		public new virtual Keys ModifierKeys
		{
			get
			{
				if (modifiersOverridden)
				{
					return overrideModifierKeys;
				}
				return (Keys)Control.ModifierKeys;
			}
		}

		// TODO: Why is this member named Caption instead of Title?
		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public string Caption
		{
			get => this.Text;
			set
			{
				if (this.InvokeRequired)
				{
					this.Invoke(new Action(() => this.Text = value));
				}
				else
				{
					this.Text = value;
				}
			}
		}

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public Point2D DesktopPosition
		{
			get => new Point2D(this.DesktopLocation.X, this.DesktopLocation.Y);
			set
			{
				if (!this.Visible)
				{
					this.StartPosition = FormStartPosition.Manual;
				}

				this.DesktopLocation = new Point(value.x, value.y);
			}
		}

		public new void Show()
		{
			this.ClientSize = new Size((int)AggSystemWindow.Width, (int)AggSystemWindow.Height);

			// Center the window if specified on the SystemWindow
			if (MainWindowsFormsWindow != this && AggSystemWindow.CenterInParent)
			{
				// TitleBarHeight is 0 until the Win32 handle exists (it is computed in
				// OnHandleCreated). The window is about to be shown, so forcing handle
				// creation here is no longer premature and keeps the centering math correct
				// on the first Show, matching the pre-lazy behavior.
				if (!IsHandleCreated)
				{
					_ = this.Handle;
				}

				Rectangle desktopBounds = MainWindowsFormsWindow.DesktopBounds;
				RectangleDouble newItemBounds = AggSystemWindow.LocalBounds;

				this.Left = desktopBounds.X + desktopBounds.Width / 2 - (int)newItemBounds.Width / 2;
				this.Top = desktopBounds.Y + desktopBounds.Height / 2 - (int)newItemBounds.Height / 2 - TitleBarHeight / 2;
			}
			else if (AggSystemWindow.InitialDesktopPosition == new Point2D(-1, -1))
			{
				this.CenterToScreen();
			}
			else
			{
				this.StartPosition = FormStartPosition.Manual;
				this.DesktopPosition = AggSystemWindow.InitialDesktopPosition;
			}

			if (MainWindowsFormsWindow != this
				&& AggSystemWindow.AlwaysOnTopOfMain)
			{
				Show(MainWindowsFormsWindow);
			}
			else
			{
				base.Show();
			}
		}

		public void ShowModal()
		{
			// Release the onidle guard so that the onidle pump continues processing while we block at ShowDialog below
			Task.Run(() => this.ReleaseOnIdleGuard());

			if (MainWindowsFormsWindow != this && AggSystemWindow.CenterInParent)
			{
				Rectangle mainBounds = MainWindowsFormsWindow.DesktopBounds;
				RectangleDouble newItemBounds = AggSystemWindow.LocalBounds;

				this.Left = mainBounds.X + mainBounds.Width / 2 - (int)newItemBounds.Width / 2;
				this.Top = mainBounds.Y + mainBounds.Height / 2 - (int)newItemBounds.Height / 2;
			}

			this.ShowDialog();
		}

		public void Invalidate(RectangleDouble rectToInvalidate)
		{
			// Ignore problems with buggy WinForms on Linux
			try
			{
				this.Invalidate();

				// Marking the region is not enough on the GPU hosts: the form's whole client area sits
				// under an opaque docked child (the WebGPU surface control), so Windows never turns the
				// form's own invalid region into a spontaneous WM_PAINT - a window with nothing to animate
				// simply stopped repainting, which left every Animation (the tumble cube's orbit, the logo
				// spinner) waiting forever for the AfterDraw it is gated on. The idle pump answers the flag
				// with an explicit Update(), which does deliver the paint. One flag per window coalesces a
				// burst of invalidates into at most one paint per pump tick (~100/s ceiling).
				this.aggInvalidatePending = true;
			}
			catch (Exception e)
			{
				Console.WriteLine("WinForms Exception: " + e.Message);
			}
		}

		/// <summary>
		/// Runs on the UI thread from the idle pump: forces the paint for every live window whose agg
		/// content invalidated since the last tick. Update() is a no-op for a window whose region turned
		/// out empty, so a stale flag costs nothing.
		/// </summary>
		private static void FlushPendingAggInvalidates()
		{
			WinformsSystemWindow[] windows;
			lock (StaticInitLock)
			{
				windows = LiveWindows.ToArray();
			}

			foreach (var window in windows)
			{
				if (window.aggInvalidatePending
					&& !window.IsDisposed
					&& !window.Disposing
					&& window.IsHandleCreated)
				{
					window.aggInvalidatePending = false;
					try
					{
						window.Update();
					}
					catch (ObjectDisposedException)
					{
						// The window went away between the checks and the call - teardown race, benign.
					}
				}
			}
		}

		public void SetCursor(Cursors cursorToSet)
		{
			void DoSetCursor(Cursors cursorToSet)
			{
				switch (cursorToSet)
				{
					case Cursors.Arrow:
						this.Cursor = System.Windows.Forms.Cursors.Arrow;
						break;

					case Cursors.Hand:
						this.Cursor = System.Windows.Forms.Cursors.Hand;
						break;

					case Cursors.IBeam:
						this.Cursor = System.Windows.Forms.Cursors.IBeam;
						break;
					case Cursors.Cross:
						this.Cursor = System.Windows.Forms.Cursors.Cross;
						break;
					case Cursors.Default:
						this.Cursor = System.Windows.Forms.Cursors.Default;
						break;
					case Cursors.Help:
						this.Cursor = System.Windows.Forms.Cursors.Help;
						break;
					case Cursors.HSplit:
						this.Cursor = System.Windows.Forms.Cursors.HSplit;
						break;
					case Cursors.No:
						this.Cursor = System.Windows.Forms.Cursors.No;
						break;
					case Cursors.NoMove2D:
						this.Cursor = System.Windows.Forms.Cursors.NoMove2D;
						break;
					case Cursors.NoMoveHoriz:
						this.Cursor = System.Windows.Forms.Cursors.NoMoveHoriz;
						break;
					case Cursors.NoMoveVert:
						this.Cursor = System.Windows.Forms.Cursors.NoMoveVert;
						break;
					case Cursors.PanEast:
						this.Cursor = System.Windows.Forms.Cursors.PanEast;
						break;
					case Cursors.PanNE:
						this.Cursor = System.Windows.Forms.Cursors.PanNE;
						break;
					case Cursors.PanNorth:
						this.Cursor = System.Windows.Forms.Cursors.PanNorth;
						break;
					case Cursors.PanNW:
						this.Cursor = System.Windows.Forms.Cursors.PanNW;
						break;
					case Cursors.PanSE:
						this.Cursor = System.Windows.Forms.Cursors.PanSE;
						break;
					case Cursors.PanSouth:
						this.Cursor = System.Windows.Forms.Cursors.PanSouth;
						break;
					case Cursors.PanSW:
						this.Cursor = System.Windows.Forms.Cursors.PanSW;
						break;
					case Cursors.PanWest:
						this.Cursor = System.Windows.Forms.Cursors.PanWest;
						break;
					case Cursors.SizeAll:
						this.Cursor = System.Windows.Forms.Cursors.SizeAll;
						break;
					case Cursors.SizeNESW:
						this.Cursor = System.Windows.Forms.Cursors.SizeNESW;
						break;
					case Cursors.SizeNS:
						this.Cursor = System.Windows.Forms.Cursors.SizeNS;
						break;
					case Cursors.SizeNWSE:
						this.Cursor = System.Windows.Forms.Cursors.SizeNWSE;
						break;
					case Cursors.SizeWE:
						this.Cursor = System.Windows.Forms.Cursors.SizeWE;
						break;
					case Cursors.UpArrow:
						this.Cursor = System.Windows.Forms.Cursors.UpArrow;
						break;
					case Cursors.VSplit:
						this.Cursor = System.Windows.Forms.Cursors.VSplit;
						break;
					case Cursors.WaitCursor:
						this.Cursor = System.Windows.Forms.Cursors.WaitCursor;
						break;
				}
			}

			if (this.InvokeRequired)
			{
				this.Invoke(new Action(() =>
				{
					DoSetCursor(cursorToSet);
				}));
			}
			else
			{
				DoSetCursor(cursorToSet);
			}
		}


		private int titleBarHeight = 0;
		private bool titleBarHeightComputed = false;

		/// <summary>
		/// Gets the height of the native title bar. Returns 0 until the Win32 handle exists;
		/// computed in OnHandleCreated (and lazily here if the handle already exists) rather
		/// than in the constructor to avoid forcing premature handle creation.
		/// </summary>
		public int TitleBarHeight
		{
			get
			{
				if (!titleBarHeightComputed && IsHandleCreated)
				{
					titleBarHeight = RectangleToScreen(ClientRectangle).Top - this.Top;
					titleBarHeightComputed = true;
				}

				return titleBarHeight;
			}
		}

		[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public new Vector2 MinimumSize
		{
			get => new Vector2(base.MinimumSize.Width, base.MinimumSize.Height);
			set
			{
				var clientSize = new Size((int)Math.Ceiling(value.X), (int)Math.Ceiling(value.Y));

				var windowSize = new Size(
					clientSize.Width + this.Width - this.ClientSize.Width,
					clientSize.Height + this.Height - this.ClientSize.Height);

				base.MinimumSize = windowSize;
			}
		}

		private static bool firstWindow = true;

		/// <summary>
		/// Resets the static firstWindow flag to allow fresh message loop initialization for tests
		/// </summary>
		public static void ResetFirstWindowFlag()
		{
			DebugLogger.EnableFilter("WinformsSystemWindow");
			DebugLogger.LogMessage("WinformsSystemWindow", $"ResetFirstWindowFlag called - Current firstWindow: {firstWindow}");
			firstWindow = true;
			
			// Reset all other static state for clean test isolation
			DebugLogger.LogMessage("WinformsSystemWindow", "Resetting WinForms static state");
			
			// Use the same lock as the constructor so a reset cannot interleave with
			// another thread's one-time static initialization.
			lock (StaticInitLock)
			{
				// Reset main window reference
				MainWindowsFormsWindow = null;

				// Reset idle processing state
				processingOnIdle = false;

				// Forget windows that are gone, then keep the idle pump running for any window that is
				// still live. This used to stop and dispose the shared timer unconditionally, which killed
				// RunOnIdle for a window another test was still running - that window's message loop then
				// idled forever because even its close was queued through RunOnIdle.
				LiveWindows.RemoveAll(window => window.IsDisposed);
				EnsureIdleTimerDriving(null);
			}
			
			DebugLogger.LogMessage("WinformsSystemWindow", "WinForms static state reset completed");
		}

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			DebugLogger.EnableFilter("WinformsSystemWindow");
			DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow ENTRY");
			
			DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow STEP 1");
			
			// If ShowSystemWindow is called on loaded/visible SystemWindow, call BringToFront and exit
			if (systemWindow.PlatformWindow == this
				&& !SingleWindowMode)
			{
				DebugLogger.LogMessage("WinformsSystemWindow", "Window already shown, calling BringToFront");
				this.BringToFront();
				return;
			}

			DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow STEP 2");
			
			// Set the active SystemWindow & PlatformWindow references
			this.AggSystemWindow = systemWindow;
			systemWindow.PlatformWindow = this;

			DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow STEP 3");
			
			systemWindow.AnchorAll();

			DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow STEP 4");

			// If this isn't true, prepare for deadlocks.
			//System.Diagnostics.Debug.Assert(SynchronizationContext.Current == null || SynchronizationContext.Current is WindowsFormsSynchronizationContext);
            
			if (firstWindow)
			{
				DebugLogger.LogMessage("WinformsSystemWindow", "First window - starting Application.Run message loop");
				firstWindow = false;

				DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow STEP 5 - About to call Show()");
				this.Show();
				DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow STEP 6 - Show() completed");

				// Enable idle processing now that the window is ready to handle events.
				lock (SingleInvokeLock)
				{
					enableIdleProcessing = true;
				}
				
				DebugLogger.LogMessage("WinformsSystemWindow", "ShowSystemWindow STEP 7 - About to call Application.Run()");
				Application.Run(this);
				DebugLogger.LogMessage("WinformsSystemWindow", "Application.Run completed - message loop exited");
			}
			else if (!SingleWindowMode)
			{
				DebugLogger.LogMessage("WinformsSystemWindow", "Subsequent window - calling Show via RunOnIdle");
				UiThread.RunOnIdle(() =>
				{
					if (systemWindow.IsModal)
					{
						DebugLogger.LogMessage("WinformsSystemWindow", "Showing modal window");
						this.ShowModal();
					}
					else
					{
						DebugLogger.LogMessage("WinformsSystemWindow", "Showing non-modal window");
						this.Show();
						this.BringToFront();
					}
				});
			}
			else if (SingleWindowMode)
			{
				// Notify the embedded window of its new single windows parent size

				// If client code has called ShowSystemWindow and we're minimized, we must restore in order
				// to establish correct window bounds from ClientSize below. Otherwise we're zeroed out and
				// will create invalid surfaces of (0,0)
				if (this.WindowState == FormWindowState.Minimized)
				{
					this.WindowState = FormWindowState.Normal;
				}

				systemWindow.Size = new Vector2(
						this.ClientSize.Width,
						this.ClientSize.Height);
			}
		}

		/// <summary>
		/// Captures a screenshot of the current window contents.
		/// </summary>
		public virtual void CaptureScreenshot(string path)
		{
			void saveScreenshot()
			{
				if (this.ClientSize.Width <= 0 || this.ClientSize.Height <= 0)
				{
					return;
				}

				using (var bitmap = new Bitmap(this.ClientSize.Width, this.ClientSize.Height))
				using (var graphics = Graphics.FromImage(bitmap))
				{
					CopyBackBufferToScreen(graphics);
					bitmap.Save(path);
				}
			}

			if (this.InvokeRequired)
			{
				this.Invoke((Action)saveScreenshot);
			}
			else
			{
				saveScreenshot();
			}
		}

		/// <summary>
		/// Captures a screenshot of the current window contents, completing once the file is written.
		/// </summary>
		/// <remarks>
		/// Declared here rather than left to the interface's default so that <see cref="WebGpuSystemWindow"/>
		/// can override it: interface mapping is fixed at this class, so a method introduced further down the
		/// hierarchy would never be reached through <see cref="IPlatformWindow"/>.
		/// This base capture is a synchronous GDI blit with nothing to await, so it runs inline.
		/// </remarks>
		public virtual Task CaptureScreenshotAsync(string path)
		{
			this.CaptureScreenshot(path);
			return Task.CompletedTask;
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			// Prevent our call to SystemWindow.Close from recursing
			if (winformAlreadyClosing)
			{
				return;
			}

			// Check for RootSystemWindow, close if found
			string windowTypeName = systemWindow.GetType().Name;

			bool closingRootInSingleWindowMode = SingleWindowMode && windowTypeName == "RootSystemWindow";

			// MainWindowsFormsWindow can be null here even for a root close: ResetFirstWindowFlag and OnClosed
			// null it between tests, and automation runs close the root after that has happened.
			if ((closingRootInSingleWindowMode && MainWindowsFormsWindow != null)
				|| (MainWindowsFormsWindow != null && systemWindow == MainWindowsFormsWindow.AggSystemWindow && !SingleWindowMode))
			{
				// Close the main (first) PlatformWindow if it's being requested and not this instance
				if (MainWindowsFormsWindow.InvokeRequired)
				{
					MainWindowsFormsWindow.Invoke((Action)MainWindowsFormsWindow.Close);
				}
				else
				{
					MainWindowsFormsWindow.Close();
				}

				return;
			}

			if (SingleWindowMode && !closingRootInSingleWindowMode)
			{
				AggSystemWindow = this.WindowProvider.TopWindow;
				AggSystemWindow?.Invalidate();
			}
			else
			{
				// A root close in single window mode with no MainWindowsFormsWindow lands here: close this
				// form so the message loop still exits. Falling into the "show the next top window" path
				// above would leave the run parked with nothing left to close it.
				if (!this.IsDisposed && !this.Disposing)
				{
					if (this.InvokeRequired)
					{
						this.Invoke((Action)this.Close);
					}
					else
					{
						this.Close();
					}
				}
			}
		}

		public class FormInspector : Form
		{
			[DesignerSerializationVisibility(DesignerSerializationVisibility.Hidden)]
		public virtual bool Inspecting { get; set; } = true;
		}
	}
}
