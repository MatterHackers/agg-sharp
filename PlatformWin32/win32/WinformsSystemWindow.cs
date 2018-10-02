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
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using System.Timers;
using System.Windows.Forms;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public abstract class WinformsSystemWindow : Form, IPlatformWindow
	{
		public static bool SingleWindowMode { get; set; } = false;

		public bool IsInitialized { get; set; } = false;

		public static bool EnableInputHook = true;

		private static System.Timers.Timer idleCallBackTimer = null;

		private static bool processingOnIdle = false;

		private static object singleInvokeLock = new object();

		protected WinformsEventSink EventSink;

		private SystemWindow systemWindow;
		public SystemWindow AggSystemWindow
		{
			get => systemWindow;
			set
			{
				systemWindow = value;

				if (systemWindow != null)
				{
					this.Caption = systemWindow.Title;

					if (SingleWindowMode)
					{
						// Set this system window as the event target
						this.EventSink?.SetActiveSystemWindow(systemWindow);
					}
					else
					{
						this.MinimumSize = systemWindow.MinimumSize;
					}
				}
			}
		}

		public bool IsMainWindow { get; } = false;

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
			lock (singleInvokeLock)
			{
				processingOnIdle = false;
			}
		}

		private void InvokePendingOnIdleActions(object sender, ElapsedEventArgs e)
		{
			if (!this.IsDisposed)
			{
				lock (singleInvokeLock)
				{
					if (processingOnIdle)
					{
						// If the pending invoke has not completed, skip the timer event
						return;
					}

					processingOnIdle = true;
				}

				if (InvokeRequired)
				{
					Invoke(new Action(() =>
					{
						try
						{
							UiThread.InvokePendingActions();
						}
						catch (Exception invokeException)
						{
#if DEBUG
							lock (singleInvokeLock)
							{
								processingOnIdle = false;
							}

							throw (invokeException);
#endif
						}

						lock (singleInvokeLock)
						{
							processingOnIdle = false;
						}
					}));
				}
				else
				{
					UiThread.InvokePendingActions();
				}
			}
		}

		public static bool ShowingSystemDialog = false;

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

			Rectangle rect = paintEventArgs.ClipRectangle;
			if (ClientSize.Width > 0 && ClientSize.Height > 0)
			{
				DrawCount++;

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
					for (var i = 0; i < this.WindowProvider.openWindows.Count; i++)
					{
						graphics2D.FillRectangle(this.WindowProvider.openWindows[0].LocalBounds, new Color(Color.Black, 160));
						this.WindowProvider.openWindows[i].OnDraw(graphics2D);
					}
				}

				/*
				var bitmap = new Bitmap((int)SystemWindow.Width, (int)SystemWindow.Height);
				paintEventArgs.Graphics.DrawImage(bitmap, 0, 0);
				bitmap.Save($"c:\\temp\\gah-{DateTime.Now.Ticks}.png");
				*/
				CopyBackBufferToScreen(paintEventArgs.Graphics);
			}

			OnPaintCount++;
			// use this to debug that windows are drawing and updating.
			//Text = string.Format("Draw {0}, Idle {1}, OnPaint {2}", DrawCount, IdleCount, OnPaintCount);
		}

		private int DrawCount = 0;
		private int OnPaintCount;

		public abstract void CopyBackBufferToScreen(Graphics displayGraphics);

		protected override void OnPaintBackground(PaintEventArgs e)
		{
			// don't call this so that windows will not erase the background.
			//base.OnPaintBackground(e);
		}

		protected override void OnActivated(EventArgs e)
		{
			// focus the first child of the forms window (should be the system window)
			if (AggSystemWindow != null
				&& AggSystemWindow.Children.Count > 0
				&& AggSystemWindow.Children[0] != null)
			{
				AggSystemWindow.Children[0].Focus();
			}

			base.OnActivated(e);
		}

		protected override void OnResize(EventArgs e)
		{
			AggSystemWindow.LocalBounds = new RectangleDouble(0, 0, ClientSize.Width, ClientSize.Height);

			// Wait until the control is initialized (and thus WindowState has been set) to ensure we don't wipe out
			// the persisted data before its loaded
			if (this.IsInitialized)
			{
				// Push the current maximized state into the SystemWindow where it can be used or persisted by Agg applications
				AggSystemWindow.Maximized = this.WindowState == FormWindowState.Maximized;
			}

			AggSystemWindow.Invalidate();

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

		#region WidgetForWindowsFormsAbstract/WinformsWindowWidget
		#endregion

		#region IPlatformWindow

		public new Agg.UI.Keys ModifierKeys => (Agg.UI.Keys)Control.ModifierKeys;

		/* // Can't simply override BringToFront. Change Interface method name/signature if required. Leaving as is
		 * // to call base/this.BringToFront via Interface call
		public override void BringToFront()
		{
			// Numerous articles on the web claim that Activate does not always bring to front (something MatterControl
			// suffers when running automation tests). If we were to continue to use Activate, we might consider calling
			// BringToFront after
			this.Activate();
			this.BringToFront();
		}*/

		// TODO: Why is this member named Caption instead of Title?
		public string Caption
		{
			get
			{
				return this.Text;
			}
			set
			{
				this.Text = value;
			}
		}

		public Point2D DesktopPosition
		{
			get
			{
				return new Point2D(this.DesktopLocation.X, this.DesktopLocation.Y);
			}

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
			this.ClientSize = new Size((int)systemWindow.Width, (int)systemWindow.Height);

			// Center the window if specified on the SystemWindow
			if (MainWindowsFormsWindow != this && systemWindow.CenterInParent)
			{
				Rectangle desktopBounds = MainWindowsFormsWindow.DesktopBounds;
				RectangleDouble newItemBounds = systemWindow.LocalBounds;

				this.Left = desktopBounds.X + desktopBounds.Width / 2 - (int)newItemBounds.Width / 2;
				this.Top = desktopBounds.Y + desktopBounds.Height / 2 - (int)newItemBounds.Height / 2 - TitleBarHeight / 2;
			}
			else if (systemWindow.InitialDesktopPosition == new Point2D(-1, -1))
			{
				this.CenterToScreen();
			}
			else
			{
				this.StartPosition = FormStartPosition.Manual;
				this.DesktopPosition = systemWindow.InitialDesktopPosition;
			}

			if (MainWindowsFormsWindow != this
				&& systemWindow.AlwaysOnTopOfMain)
			{
				base.Show(MainWindowsFormsWindow);
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

			if (MainWindowsFormsWindow != this && systemWindow.CenterInParent)
			{
				Rectangle mainBounds = MainWindowsFormsWindow.DesktopBounds;
				RectangleDouble newItemBounds = systemWindow.LocalBounds;

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
				this.Invalidate ();
			}
			catch (Exception e)
			{
				System.Console.WriteLine("WinForms Exception: " + e.Message);
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

		#endregion

		#region Agg Event Proxies
		/*
		protected override void OnMouseLeave(EventArgs e)
		{
			SystemWindow.OnMouseMove(new MatterHackers.Agg.UI.MouseEventArgs(MatterHackers.Agg.UI.MouseButtons.None, 0, -10, -10, 0));
			base.OnMouseLeave(e);
		}

		protected override void OnGotFocus(EventArgs e)
		{
			SystemWindow.OnFocusChanged(e);
			base.OnGotFocus(e);
		}

		protected override void OnLostFocus(EventArgs e)
		{
			SystemWindow.Unfocus();
			SystemWindow.OnFocusChanged(e);

			base.OnLostFocus(e);
		}

		protected override void OnKeyDown(System.Windows.Forms.KeyEventArgs e)
		{
			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent;
			if (OsInformation.OperatingSystem == OSType.Mac
				&& (e.KeyData & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt)
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)(System.Windows.Forms.Keys.Control | (e.KeyData & ~System.Windows.Forms.Keys.Alt)));
			}
			else
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)e.KeyData);
			}
			SystemWindow.OnKeyDown(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, true);

			e.Handled = aggKeyEvent.Handled;
			e.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;

			base.OnKeyDown(e);
		}

		protected override void OnKeyUp(System.Windows.Forms.KeyEventArgs e)
		{
			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)e.KeyData);
			SystemWindow.OnKeyUp(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, false);

			e.Handled = aggKeyEvent.Handled;
			e.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;

			base.OnKeyUp(e);
		}

		protected override void OnKeyPress(System.Windows.Forms.KeyPressEventArgs e)
		{
			MatterHackers.Agg.UI.KeyPressEventArgs aggKeyPressEvent = new MatterHackers.Agg.UI.KeyPressEventArgs(e.KeyChar);
			SystemWindow.OnKeyPress(aggKeyPressEvent);
			e.Handled = aggKeyPressEvent.Handled;

			base.OnKeyPress(e);
		}

		private MatterHackers.Agg.UI.MouseEventArgs ConvertWindowsMouseEventToAggMouseEvent(System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// we invert the y as we are bottom left coordinate system and windows is top left.
			int Y = windowsMouseEvent.Y;
			Y = (int)SystemWindow.BoundsRelativeToParent.Height - Y;

			return new MatterHackers.Agg.UI.MouseEventArgs((MatterHackers.Agg.UI.MouseButtons)windowsMouseEvent.Button, windowsMouseEvent.Clicks, windowsMouseEvent.X, Y, windowsMouseEvent.Delta);
		}

		protected override void OnMouseDown(System.Windows.Forms.MouseEventArgs e)
		{
			SystemWindow.OnMouseDown(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseDown(e);
		}

		protected override void OnMouseMove(System.Windows.Forms.MouseEventArgs e)
		{
			// TODO: Remove short term workaround for automation issues where mouse events fire differently if mouse is within window region
			if (!EnableInputHook)
			{
				return;
			}

			SystemWindow.OnMouseMove(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseMove(e);
		}

		protected override void OnMouseUp(System.Windows.Forms.MouseEventArgs e)
		{
			SystemWindow.OnMouseUp(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseUp(e);
		}

		protected override void OnMouseCaptureChanged(EventArgs e)
		{
			if (SystemWindow.ChildHasMouseCaptured || SystemWindow.MouseCaptured)
			{
				SystemWindow.OnMouseUp(new MatterHackers.Agg.UI.MouseEventArgs(Agg.UI.MouseButtons.Left, 0, -10, -10, 0));
			}
			base.OnMouseCaptureChanged(e);
		}

		protected override void OnMouseWheel(System.Windows.Forms.MouseEventArgs e)
		{
			SystemWindow.OnMouseWheel(ConvertWindowsMouseEventToAggMouseEvent(e));
			base.OnMouseWheel(e);
		}
		*/
		#endregion

		public static WinformsSystemWindow MainWindowsFormsWindow { get; private set; }

		public new Vector2 MinimumSize
		{
			get
			{
				return new Vector2(base.MinimumSize.Width, base.MinimumSize.Height);
			}
			set
			{
				Size clientSize = new Size((int)Math.Ceiling(value.X), (int)Math.Ceiling(value.Y));

				Size windowSize = new Size(
					clientSize.Width + this.Width - this.ClientSize.Width,
					clientSize.Height + this.Height - this.ClientSize.Height);

				base.MinimumSize = windowSize;
			}
		}

		private static bool firstWindow = true;

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			// If ShowSystemWindow is called on loaded/visible SystemWindow, call BringToFront and exit
			if (systemWindow.PlatformWindow == this)
			{
				this.BringToFront();
				return;
			}

			// Set the active SystemWindow & PlatformWindow references
			this.AggSystemWindow = systemWindow;
			systemWindow.PlatformWindow = this;

			systemWindow.AnchorAll();

			if (firstWindow)
			{
				firstWindow = false;

				this.Show();
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
				// TODO: Hack - figure out how to push this into non-firstWindow items
				//systemWindow.Size = new Vector2(this.Size.Width, this.Size.Height);

				systemWindow.Size = new Vector2(
						this.ClientSize.Width,
						this.ClientSize.Height);
				//systemWindow.Position = Vector2.Zero;
			}
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			// Prevent our call to SystemWindow.Close from recursing
			if (this.winformAlreadyClosing)
			{
				return;
			}

			var rootWindow = this.WindowProvider.topWindow;
			if ((systemWindow == rootWindow && SingleWindowMode)
				|| (MainWindowsFormsWindow != null && systemWindow == MainWindowsFormsWindow.systemWindow && !SingleWindowMode))
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
				AggSystemWindow = this.WindowProvider.topWindow;
				AggSystemWindow?.Invalidate();
			}
			else
			{
				if (!this.IsDisposed && !this.IsDisposed)
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

		public static Func<SystemWindow, FormInspector> InspectorCreator { get; set; }
	}
}
