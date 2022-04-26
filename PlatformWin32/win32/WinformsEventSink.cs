/*
Copyright (c) 2017, Lars Brubaker, John Lewin
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
using System.IO;
using System.Linq;
using System.Windows.Forms;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	public class WinformsEventSink
	{
		public static bool EnableInputHook = true;

		private SystemWindow widgetToSendTo;

		private Control controlToHook;

		private List<string> dragFiles = null;

		private WinformsSystemWindow.FormInspector inspectForm;

		public static bool AllowInspector = false;

		public WinformsEventSink(Control controlToHook, SystemWindow systemWindow)
		{
			this.controlToHook = controlToHook;
			this.widgetToSendTo = systemWindow;

			if (AllowInspector)
			{
				this.controlToHook.KeyDown += (s, e) =>
				{
					switch (e.KeyCode)
					{
						case System.Windows.Forms.Keys.F12:
							if (inspectForm != null)
							{
								// Toggle mode if window is open
								inspectForm.Inspecting = !inspectForm.Inspecting;
							}
							else
							{
								try
								{
									// Otherwise open
									inspectForm = WinformsSystemWindow.InspectorCreator.Invoke(widgetToSendTo);
									inspectForm.StartPosition = FormStartPosition.Manual;
									inspectForm.Location = new System.Drawing.Point(0, 0);
									inspectForm.FormClosed += (s2, e2) =>
									{
										inspectForm = null;
									};
									inspectForm.Show();

									// Restore focus to ensure keyboard hooks in main SystemWindow work as expected
									controlToHook.Focus();
								}
								catch { }
							}

							return;
					}
				};
			}

			controlToHook.GotFocus += ControlToHook_GotFocus;
			controlToHook.LostFocus += ControlToHook_LostFocus;

			controlToHook.KeyDown += ControlToHook_KeyDown;
			controlToHook.KeyUp += ControlToHook_KeyUp;
			controlToHook.KeyPress += ControlToHook_KeyPress;

			controlToHook.MouseDown += ControlToHook_MouseDown;
			controlToHook.MouseMove += FormToHook_MouseMove;
			controlToHook.MouseUp += ControlToHook_MouseUp;
			controlToHook.MouseWheel += ControlToHook_MouseWheel;

			controlToHook.AllowDrop = true;

			controlToHook.DragDrop += ControlToHook_DragDrop;
			controlToHook.DragEnter += ControlToHook_DragEnter;
			controlToHook.DragLeave += ControlToHook_DragLeave;
			controlToHook.DragOver += ControlToHook_DragOver;

			controlToHook.MouseCaptureChanged += ControlToHook_MouseCaptureChanged;

			controlToHook.MouseLeave += ControlToHook_MouseLeave;
		}

		public void SetActiveSystemWindow(SystemWindow systemWindow)
		{
			widgetToSendTo = systemWindow;
		}

		private void ControlToHook_DragDrop(object sender, DragEventArgs dragevent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				// do a mouse up
				widgetToSendTo.OnMouseUp(ConvertWindowsDragEventToAggMouseEvent(dragevent));

				dragFiles = null;
			}
		}

		private void ControlToHook_DragEnter(object sender, DragEventArgs dragevent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				dragFiles = GetDroppedFiles(dragevent);

				var mouseEvent = ConvertWindowsDragEventToAggMouseEvent(dragevent);
				widgetToSendTo.OnMouseMove(mouseEvent);

				if (mouseEvent.AcceptDrop)
				{
					dragevent.Effect = DragDropEffects.Copy;
				}
			}
		}

		private void ControlToHook_DragLeave(object sender, EventArgs dragevent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				dragFiles = null;
			}
		}

		private void ControlToHook_DragOver(object sender, DragEventArgs dragevent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				dragFiles = GetDroppedFiles(dragevent);

				var mouseEvent = ConvertWindowsDragEventToAggMouseEvent(dragevent);
				widgetToSendTo.OnMouseMove(mouseEvent);

				if (mouseEvent.AcceptDrop)
				{
					dragevent.Effect = DragDropEffects.Copy;
				}
				else
				{
					dragevent.Effect = DragDropEffects.None;
				}
			}
		}

		private void ControlToHook_KeyDown(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			if (AggContext.OperatingSystem == OSType.Mac
			   && windowsKeyEvent.KeyCode == System.Windows.Forms.Keys.Cancel)
			{
				windowsKeyEvent = new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Enter | windowsKeyEvent.Modifiers);
			}

			KeyEventArgs aggKeyEvent;
			if (AggContext.OperatingSystem == OSType.Mac
				&& (windowsKeyEvent.KeyData & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt)
			{
				aggKeyEvent = new KeyEventArgs((Keys)(System.Windows.Forms.Keys.Control | (windowsKeyEvent.KeyData & ~System.Windows.Forms.Keys.Alt)));
			}
			else
			{
				aggKeyEvent = new KeyEventArgs((Keys)windowsKeyEvent.KeyData);
			}

			widgetToSendTo.OnKeyDown(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, true);

			windowsKeyEvent.Handled = aggKeyEvent.Handled;
			windowsKeyEvent.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;

			// If this isn't suppressed, it swallows up the next keydown event.
			if (windowsKeyEvent.KeyCode == System.Windows.Forms.Keys.F10)
				windowsKeyEvent.SuppressKeyPress = true;
		}

		private void ControlToHook_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs windowsKeyPressEvent)
		{
			var aggKeyPressEvent = new KeyPressEventArgs(windowsKeyPressEvent.KeyChar);
			widgetToSendTo.OnKeyPress(aggKeyPressEvent);
			windowsKeyPressEvent.Handled = aggKeyPressEvent.Handled;
		}

		private void ControlToHook_KeyUp(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			// Only process the key up event if we were the ones to receive the key down event.
			// This is because the SaveFileDialog is returning us the key up event for enter after it closes.
			var aggKeyEvent = new KeyEventArgs((Keys)windowsKeyEvent.KeyData);

			if (Keyboard.IsKeyDown(aggKeyEvent.KeyCode))
			{
				widgetToSendTo.OnKeyUp(aggKeyEvent);

				Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, false);

				windowsKeyEvent.Handled = aggKeyEvent.Handled;
				windowsKeyEvent.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;
			}
		}

		private GuiWidget focusedChild = null;

		private void ControlToHook_GotFocus(object sender, EventArgs e)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				widgetToSendTo.OnFocusChanged(e);
				Keyboard.Clear();

				focusedChild?.Focus();
			}
		}

		private void ControlToHook_LostFocus(object sender, EventArgs e)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				focusedChild = null;
				GuiWidget currentWidget = widgetToSendTo;

				// try to remember the specific widget that has focus
				do
				{
					currentWidget = currentWidget.Children.Where(c => c.ContainsFocus).FirstOrDefault();

					if (currentWidget != null
						&& currentWidget.Focused)
					{
						focusedChild = currentWidget;
						break;
					}
				}
				while (currentWidget != null);

				widgetToSendTo.Unfocus();
				widgetToSendTo.OnFocusChanged(e);
			}
		}

		private void ControlToHook_MouseCaptureChanged(object sender, EventArgs e)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				if (widgetToSendTo.ChildHasMouseCaptured || widgetToSendTo.MouseCaptured)
				{
					widgetToSendTo.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, -10, -10, 0));
				}
			}
		}

		private void ControlToHook_MouseDown(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				widgetToSendTo.OnMouseDown(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
			}
		}

		private void ControlToHook_MouseLeave(object sender, EventArgs e)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				widgetToSendTo.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));
			}
		}

		private void ControlToHook_MouseUp(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				widgetToSendTo.OnMouseUp(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
			}
		}

		private void ControlToHook_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				widgetToSendTo.OnMouseWheel(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
			}
		}

		private MouseEventArgs ConvertWindowsDragEventToAggMouseEvent(DragEventArgs dragevent)
		{
			System.Drawing.Point clientTop = controlToHook.PointToScreen(new System.Drawing.Point(0, 0));
			var appWidgetPos = new System.Drawing.Point(dragevent.X - clientTop.X, (int)widgetToSendTo.Height - (dragevent.Y - clientTop.Y));

			return new MouseEventArgs(MouseButtons.None, 0, appWidgetPos.X, appWidgetPos.Y, 0, dragFiles);
		}

		private MouseEventArgs ConvertWindowsMouseEventToAggMouseEvent(System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// we invert the y as we are bottom left coordinate system and windows is top left.
			int y = windowsMouseEvent.Y;
			y = (int)widgetToSendTo.Height - y;

			return new MouseEventArgs((MouseButtons)windowsMouseEvent.Button, windowsMouseEvent.Clicks, windowsMouseEvent.X, y, windowsMouseEvent.Delta, dragFiles);
		}

		private void FormToHook_MouseMove(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			if (IPlatformWindow.EnablePlatformWindowInput)
			{
				// TODO: Remove short term workaround for automation issues where mouse events fire differently if mouse is within window region
				if (!EnableInputHook)
				{
					return;
				}

				widgetToSendTo.OnMouseMove(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
			}
		}

		private List<string> GetDroppedFiles(DragEventArgs drgevent)
		{
			var droppedFiles = new List<string>();

			if (drgevent.Data is IDataObject dataObject)
			{
				if (dataObject.GetData(DataFormats.FileDrop) is Array droppedItems)
				{
					foreach (var droppedItem in droppedItems)
					{
						droppedFiles.Add(Path.GetFullPath((string)droppedItem));
					}
				}
				else if (dataObject.GetData(DataFormats.Html) is string html)
				{
					droppedFiles.Add("html:" + html);
				}
				else if (dataObject.GetData(DataFormats.Text) is string text)
				{
					droppedFiles.Add("text:" + text);
				}
			}

			return droppedFiles;
		}
	}
}
