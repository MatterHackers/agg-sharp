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
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
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

		private static bool processingOnIdle = false;

		private static readonly object SingleInvokeLock = new object();

		protected WinformsEventSink EventSink;

		private SystemWindow _systemWindow;
		private int drawCount = 0;
		private int onPaintCount;
		private bool enableIdleProcessing;

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
				}
			}
		}

		public bool IsMainWindow { get; } = false;

		public bool IsInitialized { get; set; } = false;

		public WinformsSystemWindow()
		{
			if (idleCallBackTimer == null)
			{
				idleCallBackTimer = new System.Timers.Timer();
				// call up to 100 times a second
				idleCallBackTimer.Interval = 10;
				idleCallBackTimer.Elapsed += InvokePendingOnIdleActions;
				idleCallBackTimer.Start();
			}

			// Track first window
			if (MainWindowsFormsWindow == null)
			{
				MainWindowsFormsWindow = this;
				IsMainWindow = true;
			}

			this.TitleBarHeight = RectangleToScreen(ClientRectangle).Top - this.Top;
			this.AllowDrop = true;

			string iconPath = File.Exists("application.ico") ?
				"application.ico" :
				"../MonoBundle/StaticData/application.ico";

			try
			{
				if (File.Exists(iconPath))
				{
					this.Icon = new Icon(iconPath);
				}
			}
			catch { }
		}

		protected override void OnClosed(EventArgs e)
		{
			if (IsMainWindow)
			{
				// Ensure that when the MainWindow is closed, we null the field so we can recreate the MainWindow
				MainWindowsFormsWindow = null;
			}

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
			if (!this.IsDisposed)
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

				try
				{
					if (InvokeRequired)
					{
						Invoke(new Action(UiThread.InvokePendingActions));
					}
					else
					{
						UiThread.InvokePendingActions();
					}
				}
				catch (Exception ex)
				{
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

				var graphics2D = this.NewGraphics2D();

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

				/*
				var bitmap = new Bitmap((int)SystemWindow.Width, (int)SystemWindow.Height);
				paintEventArgs.Graphics.DrawImage(bitmap, 0, 0);
				bitmap.Save($"c:\\temp\\gah-{DateTime.Now.Ticks}.png");
				*/
				CopyBackBufferToScreen(paintEventArgs.Graphics);
			}

			// use this to debug that windows are drawing and updating.
			// onPaintCount++;
			// Text = string.Format("Draw {0}, OnPaint {1}", drawCount, onPaintCount);
		}

		public abstract void CopyBackBufferToScreen(Graphics displayGraphics);

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
				systemWindow.LocalBounds = new RectangleDouble(0, 0, ClientSize.Width, ClientSize.Height);

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
			if (AggSystemWindow != null && !AggSystemWindow.HasBeenClosed)
			{
				// Call on closing and check if we can close (a "do you want to save" might cancel the close. :).
				var eventArgs = new ClosingEventArgs();
				AggSystemWindow.OnClosing(eventArgs);

				if (eventArgs.Cancel)
				{
					e.Cancel = true;
				}
				else
				{
					// Stop the RunOnIdle timer/pump
					if (this.IsMainWindow)
					{
						idleCallBackTimer.Elapsed -= InvokePendingOnIdleActions;
						idleCallBackTimer.Stop();

						// Workaround for "Cannot access disposed object." exception
						// https://stackoverflow.com/a/9669702/84369 - ".Stop() without .DoEvents() is not enough, as it'll dispose objects without waiting for your thread to finish its work"
						Application.DoEvents();
					}

					// Close the SystemWindow
					if (AggSystemWindow != null
						&& !AggSystemWindow.HasBeenClosed)
					{
						// Store that the Close operation started here
						winformAlreadyClosing = true;
						AggSystemWindow.Close();
					}
				}
			}

			base.OnClosing(e);
		}

		public ISystemWindowProvider WindowProvider { get; set; }

		public new virtual Keys ModifierKeys => (Keys)Control.ModifierKeys;

		// TODO: Why is this member named Caption instead of Title?
		public string Caption
		{
			get => this.Text;
			set => this.Text = value;
		}

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
			}
			catch (Exception e)
			{
				Console.WriteLine("WinForms Exception: " + e.Message);
			}
		}

		public void SetCursor(Cursors cursorToSet)
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

		public int TitleBarHeight { get; private set; } = 0;

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

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			// If ShowSystemWindow is called on loaded/visible SystemWindow, call BringToFront and exit
			if (systemWindow.PlatformWindow == this
				&& !SingleWindowMode)
			{
				this.BringToFront();
				return;
			}

			// Set the active SystemWindow & PlatformWindow references
			this.AggSystemWindow = systemWindow;
			systemWindow.PlatformWindow = this;

			systemWindow.AnchorAll();

			// If this isn't true, prepare for deadlocks.
			System.Diagnostics.Debug.Assert(SynchronizationContext.Current == null || SynchronizationContext.Current is WindowsFormsSynchronizationContext);

			if (firstWindow)
			{
				firstWindow = false;

				this.Show();

				// Enable idle processing now that the window is ready to handle events.
				lock (SingleInvokeLock)
				{
					enableIdleProcessing = true;
				}
				
				Application.Run(this);
			}
			else if (!SingleWindowMode)
			{
				UiThread.RunOnIdle(() =>
				{
					if (systemWindow.IsModal)
					{
						this.ShowModal();
					}
					else
					{
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

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			// Prevent our call to SystemWindow.Close from recursing
			if (winformAlreadyClosing)
			{
				return;
			}

			// Check for RootSystemWindow, close if found
			string windowTypeName = systemWindow.GetType().Name;

			if ((SingleWindowMode && windowTypeName == "RootSystemWindow")
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

			if (SingleWindowMode)
			{
				AggSystemWindow = this.WindowProvider.TopWindow;
				AggSystemWindow?.Invalidate();
			}
			else
			{
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
			public virtual bool Inspecting { get; set; } = true;
		}
	}
}
