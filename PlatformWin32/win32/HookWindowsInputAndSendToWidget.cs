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
using System.Windows.Forms;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.Agg.UI
{
	public class HookWindowsInputAndSendToWidget
	{
		public static bool EnableInputHook = true;
		protected WidgetForWindowsFormsAbstract widgetToSendTo;
		private List<string> dragFiles = null;
		ContainerControl controlToHook;

		public HookWindowsInputAndSendToWidget(ContainerControl controlToHook, WidgetForWindowsFormsAbstract widgetToSendTo)
		{
			this.controlToHook = controlToHook;
			this.widgetToSendTo = widgetToSendTo;
			controlToHook.GotFocus += new EventHandler(controlToHook_GotFocus);
			controlToHook.LostFocus += new EventHandler(controlToHook_LostFocus);

			controlToHook.KeyDown += new System.Windows.Forms.KeyEventHandler(controlToHook_KeyDown);
			controlToHook.KeyUp += new System.Windows.Forms.KeyEventHandler(controlToHook_KeyUp);
			controlToHook.KeyPress += new System.Windows.Forms.KeyPressEventHandler(controlToHook_KeyPress);

			controlToHook.MouseDown += controlToHook_MouseDown;
			controlToHook.MouseMove += formToHook_MouseMove;
			controlToHook.MouseUp += controlToHook_MouseUp;
			controlToHook.MouseWheel += controlToHook_MouseWheel;

			controlToHook.AllowDrop = true;

			controlToHook.DragDrop += ControlToHook_DragDrop;
			controlToHook.DragEnter += ControlToHook_DragEnter;
			controlToHook.DragLeave += ControlToHook_DragLeave;
			controlToHook.DragOver += ControlToHook_DragOver;

			controlToHook.MouseCaptureChanged += new EventHandler(controlToHook_MouseCaptureChanged);

			controlToHook.MouseLeave += new EventHandler(controlToHook_MouseLeave);
		}

		private void ControlToHook_DragDrop(object sender, DragEventArgs dragevent)
		{
			List<string> droppedFiles = GetDroppedFiles(dragevent);

			// do a mouse up
			widgetToSendTo.OnMouseUp(ConvertWindowsDragEventToAggMouseEvent(dragevent));

			dragFiles = null;
		}

		private void ControlToHook_DragEnter(object sender, DragEventArgs dragevent)
		{
			dragFiles = GetDroppedFiles(dragevent);

			var mouseEvent = ConvertWindowsDragEventToAggMouseEvent(dragevent);
			widgetToSendTo.OnMouseMove(mouseEvent);

			if (mouseEvent.AcceptDrop)
			{
				dragevent.Effect = DragDropEffects.Copy;
			}
		}

		private void ControlToHook_DragLeave(object sender, EventArgs dragevent)
		{
			dragFiles = null;
		}

		private void ControlToHook_DragOver(object sender, DragEventArgs dragevent)
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

		private void controlToHook_GotFocus(object sender, EventArgs e)
		{
			widgetToSendTo.OnFocusChanged(e);
		}

		private void controlToHook_KeyDown(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			if (OsInformation.OperatingSystem == OSType.Mac
			   && windowsKeyEvent.KeyCode == System.Windows.Forms.Keys.Cancel)
			{
				windowsKeyEvent = new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Enter | windowsKeyEvent.Modifiers);
			}

			KeyEventArgs aggKeyEvent;
			if (OsInformation.OperatingSystem == OSType.Mac
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
		}

		private void controlToHook_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs windowsKeyPressEvent)
		{
			KeyPressEventArgs aggKeyPressEvent = new KeyPressEventArgs(windowsKeyPressEvent.KeyChar);
			widgetToSendTo.OnKeyPress(aggKeyPressEvent);
			windowsKeyPressEvent.Handled = aggKeyPressEvent.Handled;
		}

		private void controlToHook_KeyUp(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			KeyEventArgs aggKeyEvent = new KeyEventArgs((Keys)windowsKeyEvent.KeyData);
			widgetToSendTo.OnKeyUp(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, false);

			windowsKeyEvent.Handled = aggKeyEvent.Handled;
			windowsKeyEvent.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;
		}

		private void controlToHook_LostFocus(object sender, EventArgs e)
		{
			widgetToSendTo.Unfocus();
			widgetToSendTo.OnFocusChanged(e);
		}

		private void controlToHook_MouseCaptureChanged(object sender, EventArgs e)
		{
			if (widgetToSendTo.ChildHasMouseCaptured || widgetToSendTo.MouseCaptured)
			{
				widgetToSendTo.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, -10, -10, 0));
			}
		}

		private void controlToHook_MouseDown(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseDown(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}

		private void controlToHook_MouseLeave(object sender, EventArgs e)
		{
			widgetToSendTo.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));
		}

		private void controlToHook_MouseUp(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseUp(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}

		private void controlToHook_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseWheel(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}

		private MouseEventArgs ConvertWindowsDragEventToAggMouseEvent(DragEventArgs dragevent)
		{
			System.Drawing.Point clientTop = controlToHook.PointToScreen(new System.Drawing.Point(0, 0));
			System.Drawing.Point appWidgetPos = new System.Drawing.Point(dragevent.X - clientTop.X, (int)widgetToSendTo.height() - (dragevent.Y - clientTop.Y));

			return new MouseEventArgs((MouseButtons.None), 0, appWidgetPos.X, appWidgetPos.Y, 0, dragFiles);
		}

		private MouseEventArgs ConvertWindowsMouseEventToAggMouseEvent(System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// we invert the y as we are bottom left coordinate system and windows is top left.
			int Y = windowsMouseEvent.Y;
			Y = (int)widgetToSendTo.height() - Y;

			return new MouseEventArgs((MouseButtons)windowsMouseEvent.Button, windowsMouseEvent.Clicks, windowsMouseEvent.X, Y, windowsMouseEvent.Delta, dragFiles);
		}

		private void formToHook_MouseMove(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// TODO: Remove short term workaround for automation issues where mouse events fire differently if mouse is within window region
			if (!EnableInputHook)
			{
				return;
			}

			widgetToSendTo.OnMouseMove(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
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
	}
}