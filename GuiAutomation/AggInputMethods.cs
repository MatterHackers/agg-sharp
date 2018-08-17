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

using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace MatterHackers.GuiAutomation
{
	public interface IInputMethod : IDisposable
	{
		ImageBuffer GetCurrentScreen();
		Point2D CurrentMousePosition();
		bool LeftButtonDown { get; }
		int GetCurrentScreenHeight();
		int ClickCount { get; }
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

		public AggInputMethods(AutomationRunner automationRunner, bool drawSimulatedMouse)
		{
			this.DrawSimulatedMouse = drawSimulatedMouse;
			this.automationRunner = automationRunner;
		}

		public bool LeftButtonDown { get; private set; }
		public bool RightButtonDown { get; private set; }
		public bool MiddleButtonDown { get; private set; }

		public int ClickCount { get; private set; }

		public ImageBuffer GetCurrentScreen()
		{
			throw new NotImplementedException();
		}

		public int GetCurrentScreenHeight()
		{
			Size sz = new Size();
			return sz.Height;
		}

		public Point2D CurrentMousePosition()
		{
			return currentMousePosition;
		}

		public void SetCursorPosition(int x, int y)
		{
			var openWindows = SystemWindow.AllOpenSystemWindows.ToList();
			SystemWindow topSystemWindow = openWindows.Last();
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

			foreach (var systemWindow in openWindows)
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

		private void DrawMouse(object drawingWidget, DrawEventArgs e)
		{
			automationRunner.RenderMouse(windowToDrawSimulatedMouseOn, e.Graphics2D);
		}

		public void CreateMouseEvent(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo)
		{
			// Send mouse event into the first SystemWindow (reverse order) containing the mouse
			foreach (var systemWindow in SystemWindow.AllOpenSystemWindows.Reverse().ToList())
			{
				Point2D windowPosition = AutomationRunner.ScreenToSystemWindow(currentMousePosition, systemWindow);
				if (systemWindow.LocalBounds.Contains(windowPosition))
				{
					MouseButtons mouseButtons = MapButtons(dwFlags);
					if (dwFlags == NativeMethods.MOUSEEVENTF_LEFTDOWN)
					{
						this.ClickCount = (this.LeftButtonDown) ? 2 : 1;

						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseDown(new MouseEventArgs(mouseButtons, this.ClickCount, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_LEFTUP)
					{
						// send it to the window
						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseUp(new MouseEventArgs(mouseButtons, 0, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_RIGHTDOWN)
					{
						this.ClickCount = (this.RightButtonDown) ? 2 : 1;

						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseDown(new MouseEventArgs(mouseButtons, this.ClickCount, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_RIGHTUP)
					{
						// send it to the window
						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseUp(new MouseEventArgs(mouseButtons, 0, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_MIDDLEDOWN)
					{
						this.ClickCount = (this.MiddleButtonDown) ? 2 : 1;

						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseDown(new MouseEventArgs(mouseButtons, this.ClickCount, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
					else if (dwFlags == NativeMethods.MOUSEEVENTF_MIDDLEUP)
					{
						// send it to the window
						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseUp(new MouseEventArgs(mouseButtons, 0, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
				}
			}

			this.LeftButtonDown = (dwFlags == NativeMethods.MOUSEEVENTF_LEFTDOWN);
			this.MiddleButtonDown = (dwFlags == NativeMethods.MOUSEEVENTF_MIDDLEDOWN);
			this.RightButtonDown = (dwFlags == NativeMethods.MOUSEEVENTF_RIGHTDOWN);
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
					return MouseButtons.Right;

				case NativeMethods.MOUSEEVENTF_MIDDLEDOWN:
				case NativeMethods.MOUSEEVENTF_MIDDLEUP:
					return MouseButtons.Middle;
			}

			return MouseButtons.Left;
		}

		static Dictionary<char, Keys> charToKeys = new Dictionary<char, Keys>()
		{
			['.'] = Keys.OemPeriod
		};

		public void Type(string textToType)
		{
			var systemWindow = SystemWindow.AllOpenSystemWindows.Last();

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
						Keyboard.SetKeyDownState(Keys.Control, true);
						SendKey(Keys.Control | Keys.A, 'A', systemWindow);
						Keyboard.SetKeyDownState(Keys.Control, false);
						break;

					case "{BACKSPACE}":
						systemWindow.OnKeyDown(new KeyEventArgs(Keys.Back));
						systemWindow.OnKeyUp(new KeyEventArgs(Keys.Back));
						break;

					default:
						foreach (char character in textToType)
						{
							Keys k = (Keys)char.ToUpper(character);
							if(charToKeys.ContainsKey(character))
							{
								k = charToKeys[character];
							}
							var keyDownEvent = new KeyEventArgs(k);
							systemWindow.OnKeyDown(keyDownEvent);
							if (!keyDownEvent.SuppressKeyPress)
							{
								var keyUpEvent = new KeyEventArgs(k);
								systemWindow.OnKeyUp(keyUpEvent);
								if (!keyUpEvent.SuppressKeyPress)
								{
									systemWindow.OnKeyPress(new KeyPressEventArgs(character));
								}
							}
							else
							{
								// If you end up here unexpectedly you may need to add 
								// a mapping to charToKeys for the inputed character.
								int a = 0;
							}
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
				reciever.OnKeyPress(new KeyPressEventArgs(keyPressed));
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
