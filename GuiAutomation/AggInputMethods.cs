﻿/*
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

using System;
using System.Drawing;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
#if !__ANDROID__
#endif

namespace MatterHackers.GuiAutomation
{
	public interface IInputMethod : IDisposable
	{
		ImageBuffer GetCurrentScreen();
		Point2D CurrentMousePosition();
		bool LeftButtonDown { get; }
		int GetCurrentScreenHeight();
		void SetCursorPosition(int x, int y);
		void CreateMouseEvent(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);
		void Type(string textToType);
	}

	public class AggInputMethods : IInputMethod
	{
		private Point2D currentMousePosition;
		private AutomationRunner automationRunner;
		public bool DrawSimulatedMouse = true;

		private SystemWindow windowToDrawSimulatedMouseOn = null;

		public ImageBuffer GetCurrentScreen()
		{
			throw new NotImplementedException();
		}

		public int GetCurrentScreenHeight()
		{
			Size sz = new Size();
			return sz.Height;
		}

		public AggInputMethods(AutomationRunner automationRunner, bool drawSimulatedMouse)
		{
			this.DrawSimulatedMouse = drawSimulatedMouse;
			this.automationRunner = automationRunner;
		}

		public Point2D CurrentMousePosition()
		{
			return currentMousePosition;
		}

		public void SetCursorPosition(int x, int y)
		{
			SystemWindow topSystemWindow = SystemWindow.AllOpenSystemWindows[SystemWindow.AllOpenSystemWindows.Count - 1];
			if (windowToDrawSimulatedMouseOn != topSystemWindow)
			{
				if (windowToDrawSimulatedMouseOn != null)
				{
					windowToDrawSimulatedMouseOn.AfterDraw -= DrawMouse;
				}
				windowToDrawSimulatedMouseOn = topSystemWindow;
				if (windowToDrawSimulatedMouseOn != null && DrawSimulatedMouse)
				{
					windowToDrawSimulatedMouseOn.AfterDraw += DrawMouse;
				}
			}

			foreach (var systemWindow in SystemWindow.AllOpenSystemWindows)
			{
				currentMousePosition = new Point2D(x, y);
				Point2D windowPosition = AutomationRunner.ScreenToSystemWindow(currentMousePosition, systemWindow);
				if (LeftButtonDown)
				{
					MouseEventArgs aggEvent = new MouseEventArgs(MouseButtons.Left, 0, windowPosition.x, windowPosition.y, 0);
					UiThread.RunOnIdle(() =>
					{
						systemWindow.OnMouseMove(aggEvent);
						systemWindow.Invalidate();
					});
				}
				else
				{
					MouseEventArgs aggEvent = new MouseEventArgs(MouseButtons.None, 0, windowPosition.x, windowPosition.y, 0);
					UiThread.RunOnIdle(() =>
					{
						systemWindow.OnMouseMove(aggEvent);
						systemWindow.Invalidate();
					});
				}
			}
		}

		private void DrawMouse(GuiWidget drawingWidget, DrawEventArgs e)
		{
			automationRunner.RenderMouse(windowToDrawSimulatedMouseOn, e.graphics2D);
		}

		public bool LeftButtonDown { get; private set; }

		public void CreateMouseEvent(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo)
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
					if (dwFlags == NativeMethods.MOUSEEVENTF_LEFTDOWN)
					{
						// send it to the window
						if (LeftButtonDown)
						{
							MouseEventArgs aggEvent = new MouseEventArgs(mouseButtons, 0, windowPosition.x, windowPosition.y, 0);
							UiThread.RunOnIdle(() => systemWindow.OnMouseMove(aggEvent));
						}
						else
						{
							MouseEventArgs aggEvent = new MouseEventArgs(mouseButtons, 1, windowPosition.x, windowPosition.y, 0);
							UiThread.RunOnIdle(() => systemWindow.OnMouseDown(aggEvent));
						}
					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_LEFTUP)
					{
						MouseEventArgs aggEvent = new MouseEventArgs(mouseButtons, 0, windowPosition.x, windowPosition.y, 0);
						// send it to the window
						UiThread.RunOnIdle(() => systemWindow.OnMouseUp(aggEvent));
					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_RIGHTDOWN)
					{

					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_RIGHTUP)
					{

					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_MIDDLEDOWN)
					{

					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_MIDDLEUP)
					{

					}
				}
			}

			this.LeftButtonDown = (dwFlags == NativeMethods.MOUSEEVENTF_LEFTDOWN);
		}

		private MouseButtons MapButtons(int cButtons)
		{
			switch (cButtons)
			{
				case NativeMethods.MOUSEEVENTF_LEFTDOWN:
				case NativeMethods.MOUSEEVENTF_LEFTUP:
					return MouseButtons.Left;

				case NativeMethods.MOUSEEVENTF_RIGHTDOWN:
				case NativeMethods.MOUSEEVENTF_RIGHTUP:
					return MouseButtons.Left;

				case NativeMethods.MOUSEEVENTF_MIDDLEDOWN:
				case NativeMethods.MOUSEEVENTF_MIDDLEUP:
					return MouseButtons.Left;
			}

			return MouseButtons.Left;
		}

		public void Type(string textToType)
		{
			SystemWindow systemWindow = SystemWindow.AllOpenSystemWindows[SystemWindow.AllOpenSystemWindows.Count - 1];

			// Setup reset event to block until input received
			var resetEvent = new AutoResetEvent(false);

			UiThread.RunOnIdle(() =>
			{
				switch (textToType)
				{
					case "{Enter}":
						systemWindow.OnKeyDown(new KeyEventArgs(Keys.Enter));
						systemWindow.OnKeyUp(new KeyEventArgs(Keys.Enter));
						break;

					case "^a":
						SendKey(Keys.Control | Keys.A, 'A', systemWindow);
						break;

					case "{BACKSPACE}":
						systemWindow.OnKeyPress(new KeyPressEventArgs('\b'));
						break;

					default:
						foreach (char character in textToType)
						{
							systemWindow.OnKeyPress(new KeyPressEventArgs(character));
						}
						break;
				}

				resetEvent.Set();
			});

			resetEvent.WaitOne();
		}

		private void SendKey(Keys keyDown, char keyPressed, GuiWidget reciever)
		{
			KeyEventArgs keyDownEvent = new KeyEventArgs(keyDown);
			reciever.OnKeyDown(keyDownEvent);
			if (!keyDownEvent.SuppressKeyPress)
			{
				KeyPressEventArgs keyPressEvent = new KeyPressEventArgs(keyPressed);
				reciever.OnKeyPress(keyPressEvent);
			}
		}

		public void Dispose()
		{
			if (windowToDrawSimulatedMouseOn != null)
			{
				windowToDrawSimulatedMouseOn.AfterDraw -= DrawMouse;
			}
		}
	}
}