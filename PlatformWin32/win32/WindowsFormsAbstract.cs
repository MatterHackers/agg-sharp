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
using System.Runtime.InteropServices;
using System.Timers;
using System.Windows.Forms;

namespace MatterHackers.Agg.UI
{
	public abstract class WindowsFormsAbstract : Form
	{
		protected WidgetForWindowsFormsAbstract aggAppWidget;

		private static Form mainForm = null;

		private static System.Timers.Timer idleCallBackTimer = null;

		private static bool processingOnIdle = false;

		private static object singleInvokeLock = new object();

		private bool aggWidgetHasBeenClosed = false;

		public WindowsFormsAbstract()
		{
			if (idleCallBackTimer == null)
			{
				idleCallBackTimer = new System.Timers.Timer();
				mainForm = this;
				// call up to 100 times a second
				idleCallBackTimer.Interval = 10;
				idleCallBackTimer.Elapsed += InvokePendingOnIdleActions;
				idleCallBackTimer.Start();
			}

			this.TitleBarHeight = RectangleToScreen(ClientRectangle).Top - this.Top;
		}

		public static void ShowFileInFolder(string fileToShow)
		{
			string argument = "/select, \"" + Path.GetFullPath(fileToShow) + "\"";

			System.Diagnostics.Process.Start("explorer.exe", argument);
		}

		public int TitleBarHeight { get; private set; } = 0;

		protected void SetUpFormsWindow(AbstractOsMappingWidget app, SystemWindow childSystemWindow)
		{
			aggAppWidget = (WidgetForWindowsFormsAbstract)app;
			this.AllowDrop = true;

			if (File.Exists("application.ico"))
			{
				try
				{
					this.Icon = new System.Drawing.Icon("application.ico");
				}
				catch (System.ComponentModel.Win32Exception ex)
				{
					if (ex.NativeErrorCode != 0)
					{
						throw;
					}
				}
			}
			else if (File.Exists("../MonoBundle/StaticData/application.ico"))
			{
				try
				{
					this.Icon = new System.Drawing.Icon("../MonoBundle/StaticData/application.ico");
				}
				catch (System.ComponentModel.Win32Exception ex)
				{
					if (ex.NativeErrorCode != 0)
					{
						throw;
					}
				}
			}
		}

		private List<string> GetDroppedFiles(DragEventArgs drgevent)
		{
			List<string> droppedFiles = new List<string>();
			Array droppedItems = ((IDataObject)drgevent.Data).GetData(DataFormats.FileDrop) as Array;
			if (droppedItems != null)
			{
				foreach (object droppedItem in droppedItems)
				{
					string fileName = Path.GetFullPath((string)droppedItem);
					droppedFiles.Add(fileName);
				}
			}

			return droppedFiles;
		}

		private Point GetPosForAppWidget(DragEventArgs dragevent)
		{
			Point clientTop = PointToScreen(new Point(0, 0));
			Point appWidgetPos = new Point(dragevent.X - clientTop.X, (int)aggAppWidget.height() - (dragevent.Y - clientTop.Y));

			return appWidgetPos;
		}

		protected override void OnDragEnter(DragEventArgs dragevent)
		{
			List<string> droppedFiles = GetDroppedFiles(dragevent);

			Point appWidgetPos = GetPosForAppWidget(dragevent);
			FileDropEventArgs fileDropEventArgs = new FileDropEventArgs(droppedFiles, appWidgetPos.X, appWidgetPos.Y);
			aggAppWidget.OnDragEnter(fileDropEventArgs);
			if (fileDropEventArgs.AcceptDrop)
			{
				dragevent.Effect = DragDropEffects.Copy;
			}

			base.OnDragEnter(dragevent);
		}

		protected override void OnDragOver(DragEventArgs dragevent)
		{
			List<string> droppedFiles = GetDroppedFiles(dragevent);

			Point appWidgetPos = GetPosForAppWidget(dragevent);
			FileDropEventArgs fileDropEventArgs = new FileDropEventArgs(droppedFiles, appWidgetPos.X, appWidgetPos.Y);
			aggAppWidget.OnDragOver(fileDropEventArgs);
			if (fileDropEventArgs.AcceptDrop)
			{
				dragevent.Effect = DragDropEffects.Copy;
			}
			else
			{
				dragevent.Effect = DragDropEffects.None;
			}

			base.OnDragOver(dragevent);
		}

		protected override void OnDragDrop(DragEventArgs dragevent)
		{
			List<string> droppedFiles = GetDroppedFiles(dragevent);

			Point appWidgetPos = GetPosForAppWidget(dragevent);
			FileDropEventArgs fileDropEventArgs = new FileDropEventArgs(droppedFiles, appWidgetPos.X, appWidgetPos.Y);
			aggAppWidget.OnDragDrop(fileDropEventArgs);

			base.OnDragDrop(dragevent);
		}

		public void ReleaseOnIdleGuard()
		{
			lock(singleInvokeLock)
			{
				processingOnIdle = false;
			}
		}

		private void InvokePendingOnIdleActions(object sender, ElapsedEventArgs e)
		{
			if (aggAppWidget != null
				&& !aggWidgetHasBeenClosed)
			{
				lock(singleInvokeLock)
				{
					if (processingOnIdle)
					{
						// If the pending invoke has not completed, skip the timer event
						return;
					}

					processingOnIdle = true;
				}

				if ( InvokeRequired)
				{
					Invoke(new Action(() =>
					{
						try
						{
							UiThread.InvokePendingActions();
						}
						catch(Exception invokeException)
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
					processingOnIdle = false;
				}
			}
		}

		public bool ShowingSystemDialog = false;

		protected override void OnPaint(PaintEventArgs paintEventArgs)
		{
			base.OnPaint(paintEventArgs);

			if (ShowingSystemDialog)
			{
				// We do this because calling Invalidate within an OnPaint message will cause our
				// SaveDialog to not show its 'overwrite' dialog if needed.
				// We use the Invalidate to cause a continuous pump of the OnPaint message to call our OnIdle.
				// We could figure another solution but it must be very carful to ensure we don't break SaveDialog
				return;
			}

			Rectangle rect = paintEventArgs.ClipRectangle;
			if (ClientSize.Width > 0 && ClientSize.Height > 0)
			{
				DrawCount++;
				aggAppWidget.OnDraw(aggAppWidget.NewGraphics2D());

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
			if (aggAppWidget != null
				&& aggAppWidget.Children.Count > 0
				&& aggAppWidget.Children[0] != null)
			{
				aggAppWidget.Children[0].Focus();
			}
			base.OnActivated(e);
		}

		protected override void OnResize(EventArgs e)
		{
			aggAppWidget.LocalBounds = new RectangleDouble(0, 0, ClientSize.Width, ClientSize.Height);
			aggAppWidget.Invalidate();

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

		private bool waitingForIdleTimerToStop = false;

		protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
		{
			// Call on closing and check if we can close (a "do you want to save" might cancel the close. :).
			bool cancelClose = false;

			if(aggAppWidget.Children.Count > 0)
			{
				aggAppWidget.Children[0]?.OnClosing(out cancelClose);
			}

			if (cancelClose)
			{
				e.Cancel = true;
			}
			else
			{
				if (!aggWidgetHasBeenClosed)
				{
					aggWidgetHasBeenClosed = true;
					aggAppWidget.Close();
				}

				if (this == mainForm && !waitingForIdleTimerToStop)
				{
					waitingForIdleTimerToStop = true;
					idleCallBackTimer.Stop();
					idleCallBackTimer.Elapsed -= InvokePendingOnIdleActions;
					e.Cancel = true;
					// We just need to wait for this event to end so we can re-enter the idle loop with the time stopped
					// If we close with the idle loop timer not stopped we throw and exception.
					System.Windows.Forms.Timer delayedCloseTimer = new System.Windows.Forms.Timer();
					delayedCloseTimer.Tick += DoDelayedClose;
					delayedCloseTimer.Start();
				}
			}

			base.OnClosing(e);
		}

		private void DoDelayedClose(object sender, EventArgs e)
		{
			((System.Windows.Forms.Timer)sender).Stop();
			this.Close();
		}

		internal virtual void RequestInvalidate(Rectangle windowsRectToInvalidate)
		{
			// In mono this can throw an invalid exception sometimes. So we catch it to prevent a crash.
			// http://lists.ximian.com/pipermail/mono-bugs/2007-September/061540.html
			try
			{
				Invalidate(windowsRectToInvalidate);

				Rectangle allRectToInvalidate = new Rectangle(0, 0, (int)Width, (int)Height);
				Invalidate(allRectToInvalidate);
			}
			catch (Exception)
			{
			}
		}

		internal virtual void RequestClose()
		{
			if (!aggWidgetHasBeenClosed)
			{
				Close();
			}
		}
	}
}