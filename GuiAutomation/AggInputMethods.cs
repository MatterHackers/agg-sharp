/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.PlatformAbstract;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
#if !__ANDROID__
using System.Drawing.Imaging;
#endif

namespace MatterHackers.GuiAutomation
{
	public class AggInputMethods : NativeMethods
	{
		Point2D currentMousePosition;
		AutomationRunner automationRunner;
		public bool DrawSimulatedMouse = true;

		public override ImageBuffer GetCurrentScreen()
		{
			throw new NotImplementedException();
		}

		public AggInputMethods(AutomationRunner automationRunner, bool drawSimulatedMouse)
		{
			this.DrawSimulatedMouse = drawSimulatedMouse;
			this.automationRunner = automationRunner;
		}

		public override Point2D CurrentMousPosition()
		{
			return currentMousePosition;
		}

		SystemWindow windowToDrawSimpulatedMouseOn = null;

		public override void Dispose()
		{
			if (windowToDrawSimpulatedMouseOn != null)
			{
				windowToDrawSimpulatedMouseOn.AfterDraw -= DrawMouse;
			}
		}

		public override void SetCursorPosition(int x, int y)
		{
			SystemWindow topSystemWindow = SystemWindow.AllOpenSystemWindows[SystemWindow.AllOpenSystemWindows.Count - 1];
			if (windowToDrawSimpulatedMouseOn != topSystemWindow)
			{
				if (windowToDrawSimpulatedMouseOn != null)
				{
					windowToDrawSimpulatedMouseOn.AfterDraw -= DrawMouse;
				}
				windowToDrawSimpulatedMouseOn = topSystemWindow;
				if (windowToDrawSimpulatedMouseOn != null && DrawSimulatedMouse)
				{
					windowToDrawSimpulatedMouseOn.AfterDraw += DrawMouse;
				}
			}

			foreach (var systemWindow in SystemWindow.AllOpenSystemWindows)
			{
				currentMousePosition = new Point2D(x, y);
				Point2D windowPosition = AutomationRunner.ScreenToSystemWindow(currentMousePosition, systemWindow);
				if (LeftButtonDown)
				{
					MouseEventArgs aggEvent = new MouseEventArgs(MouseButtons.Left, 1, windowPosition.x, windowPosition.y, 0);
					UiThread.RunOnIdle(() => systemWindow.OnMouseMove(aggEvent));
				}
				else
				{
					MouseEventArgs aggEvent = new MouseEventArgs(MouseButtons.None, 1, windowPosition.x, windowPosition.y, 0);
					UiThread.RunOnIdle(() => systemWindow.OnMouseMove(aggEvent));
				}
			}
		}

		private void DrawMouse(GuiWidget drawingWidget, DrawEventArgs e)
		{
			automationRunner.RenderMouse(windowToDrawSimpulatedMouseOn, e.graphics2D);
		}

		public override void CreateMouseEvent(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo)
		{
			// figure out where this is on our agg windows
			// for now only send mouse events to the top most window
			foreach (var systemWindow in SystemWindow.AllOpenSystemWindows)
			{
				Point2D windowPosition = AutomationRunner.ScreenToSystemWindow(currentMousePosition, systemWindow);
				if (systemWindow.LocalBounds.Contains(windowPosition))
				{
					MouseButtons mouseButtons = MapButtons(cButtons);
					// create the agg event
					if (dwFlags == MOUSEEVENTF_LEFTDOWN)
					{
						MouseEventArgs aggEvent = new MouseEventArgs(mouseButtons, 1, windowPosition.x, windowPosition.y, 0);
						// send it to the window
						if (LeftButtonDown)
						{
							UiThread.RunOnIdle(() => systemWindow.OnMouseMove(aggEvent));
						}
						else
						{
							UiThread.RunOnIdle(() => systemWindow.OnMouseDown(aggEvent));
						}
					}
					else if (dwFlags == MOUSEEVENTF_LEFTUP)
					{
						MouseEventArgs aggEvent = new MouseEventArgs(mouseButtons, 0, windowPosition.x, windowPosition.y, 0);
						// send it to the window
						UiThread.RunOnIdle(() => systemWindow.OnMouseUp(aggEvent));
					}
					else if (dwFlags == MOUSEEVENTF_RIGHTDOWN)
					{

					}
					else if (dwFlags == MOUSEEVENTF_RIGHTUP)
					{

					}
					else if (dwFlags == MOUSEEVENTF_MIDDLEDOWN)
					{

					}
					else if (dwFlags == MOUSEEVENTF_MIDDLEUP)
					{

					}
				}
			}

			base.CreateMouseEvent(dwFlags, dx, dy, cButtons, dwExtraInfo);
		}

		private MouseButtons MapButtons(int cButtons)
		{
			switch (cButtons)
			{
				case MOUSEEVENTF_LEFTDOWN:
				case MOUSEEVENTF_LEFTUP:
					return MouseButtons.Left;

				case MOUSEEVENTF_RIGHTDOWN:
				case MOUSEEVENTF_RIGHTUP:
					return MouseButtons.Left;

				case MOUSEEVENTF_MIDDLEDOWN:
				case MOUSEEVENTF_MIDDLEUP:
					return MouseButtons.Left;
			}

			return MouseButtons.Left;
		}

		public override void Type(string textToType)
		{
			SystemWindow systemWindow = SystemWindow.AllOpenSystemWindows[SystemWindow.AllOpenSystemWindows.Count - 1];

			foreach (char character in textToType)
			{
				//UiThread.RunOnIdle(() => systemWindow.OnKeyDown(aggKeyEvent));
				//Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, true);

				KeyPressEventArgs aggKeyPressEvent = new KeyPressEventArgs(character);
				UiThread.RunOnIdle(() => systemWindow.OnKeyPress(aggKeyPressEvent));

				//widgetToSendTo.OnKeyUp(aggKeyEvent);
				//Keyboard.SetKeyDownState(aggKeyEvent.KeyCode, false);
			}
		}
	}
}