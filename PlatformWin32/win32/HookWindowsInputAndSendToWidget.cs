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
using System.Windows.Forms;
using MatterHackers.Agg.PlatformAbstract;

namespace MatterHackers.Agg.UI
{
	public class HookWindowsInputAndSendToWidget
	{
		protected WidgetForWindowsFormsAbstract widgetToSendTo;
		public static bool EnableInputHook = true;

		public HookWindowsInputAndSendToWidget(ContainerControl controlToHook, WidgetForWindowsFormsAbstract widgetToSendTo)
		{
			this.widgetToSendTo = widgetToSendTo;
			controlToHook.GotFocus += new EventHandler(controlToHook_GotFocus);
			controlToHook.LostFocus += new EventHandler(controlToHook_LostFocus);

			controlToHook.KeyDown += new System.Windows.Forms.KeyEventHandler(controlToHook_KeyDown);
			controlToHook.KeyUp += new System.Windows.Forms.KeyEventHandler(controlToHook_KeyUp);
			controlToHook.KeyPress += new System.Windows.Forms.KeyPressEventHandler(controlToHook_KeyPress);

			controlToHook.MouseDown += new MouseEventHandler(controlToHook_MouseDown);
			controlToHook.MouseMove += new MouseEventHandler(formToHook_MouseMove);
			controlToHook.MouseUp += new MouseEventHandler(controlToHook_MouseUp);
			controlToHook.MouseWheel += new MouseEventHandler(controlToHook_MouseWheel);

			controlToHook.MouseCaptureChanged += new EventHandler(controlToHook_MouseCaptureChanged);

			controlToHook.MouseLeave += new EventHandler(controlToHook_MouseLeave);
		}

		private void controlToHook_MouseLeave(object sender, EventArgs e)
		{
			widgetToSendTo.OnMouseMove(new MatterHackers.Agg.UI.MouseEventArgs(MatterHackers.Agg.UI.MouseButtons.None, 0, -10, -10, 0));
		}

		private void controlToHook_GotFocus(object sender, EventArgs e)
		{
			widgetToSendTo.OnFocusChanged(e);
		}

		private void controlToHook_LostFocus(object sender, EventArgs e)
		{
			widgetToSendTo.Unfocus();
			widgetToSendTo.OnFocusChanged(e);
		}

		private void controlToHook_KeyDown(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			if (OsInformation.OperatingSystem == OSType.Mac
			   && windowsKeyEvent.KeyCode == System.Windows.Forms.Keys.Cancel)
			{
				windowsKeyEvent = new System.Windows.Forms.KeyEventArgs(System.Windows.Forms.Keys.Enter | windowsKeyEvent.Modifiers);
			}

			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent;
			if (OsInformation.OperatingSystem == OSType.Mac
				&& (windowsKeyEvent.KeyData & System.Windows.Forms.Keys.Alt) == System.Windows.Forms.Keys.Alt)
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)(System.Windows.Forms.Keys.Control | (windowsKeyEvent.KeyData & ~System.Windows.Forms.Keys.Alt)));
			}
			else
			{
				aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)windowsKeyEvent.KeyData);
			}
			widgetToSendTo.OnKeyDown(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, true);

			windowsKeyEvent.Handled = aggKeyEvent.Handled;
			windowsKeyEvent.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;
		}

		private void controlToHook_KeyUp(object sender, System.Windows.Forms.KeyEventArgs windowsKeyEvent)
		{
			MatterHackers.Agg.UI.KeyEventArgs aggKeyEvent = new MatterHackers.Agg.UI.KeyEventArgs((MatterHackers.Agg.UI.Keys)windowsKeyEvent.KeyData);
			widgetToSendTo.OnKeyUp(aggKeyEvent);

			Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, false);

			windowsKeyEvent.Handled = aggKeyEvent.Handled;
			windowsKeyEvent.SuppressKeyPress = aggKeyEvent.SuppressKeyPress;
		}

		private void controlToHook_KeyPress(object sender, System.Windows.Forms.KeyPressEventArgs windowsKeyPressEvent)
		{
			MatterHackers.Agg.UI.KeyPressEventArgs aggKeyPressEvent = new MatterHackers.Agg.UI.KeyPressEventArgs(windowsKeyPressEvent.KeyChar);
			widgetToSendTo.OnKeyPress(aggKeyPressEvent);
			windowsKeyPressEvent.Handled = aggKeyPressEvent.Handled;
		}

		private MatterHackers.Agg.UI.MouseEventArgs ConvertWindowsMouseEventToAggMouseEvent(System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			// we invert the y as we are bottom left coordinate system and windows is top left.
			int Y = windowsMouseEvent.Y;
			Y = (int)widgetToSendTo.height() - Y;

			return new MatterHackers.Agg.UI.MouseEventArgs((MatterHackers.Agg.UI.MouseButtons)windowsMouseEvent.Button, windowsMouseEvent.Clicks, windowsMouseEvent.X, Y, windowsMouseEvent.Delta);
		}

		private void controlToHook_MouseDown(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseDown(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
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

		private void controlToHook_MouseUp(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseUp(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}

		private void controlToHook_MouseCaptureChanged(object sender, EventArgs e)
		{
			if (widgetToSendTo.ChildHasMouseCaptured || widgetToSendTo.MouseCaptured)
			{
				widgetToSendTo.OnMouseUp(new MatterHackers.Agg.UI.MouseEventArgs(MouseButtons.Left, 0, -10, -10, 0));
			}
		}

		private void controlToHook_MouseWheel(object sender, System.Windows.Forms.MouseEventArgs windowsMouseEvent)
		{
			widgetToSendTo.OnMouseWheel(ConvertWindowsMouseEventToAggMouseEvent(windowsMouseEvent));
		}
	}
}