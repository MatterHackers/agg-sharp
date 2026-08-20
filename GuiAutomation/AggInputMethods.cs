/*
Copyright (c) 2026, Lars Brubaker
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
using System.Reflection;
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

		/// <summary>
		/// Sends one mouse button transition, mirroring the Win32 mouse_event signature. For DOWN
		/// events <paramref name="cButtons"/> carries the click number the event should report
		/// (pass 2 for the second press of a double click, anything else means a single click).
		/// The caller states this explicitly rather than the input method inferring it from
		/// wall-clock spacing, so a loaded machine cannot silently turn an intended double click
		/// into two singles. Hardware input methods ignore it - the OS counts real clicks itself.
		/// </summary>
		void CreateMouseEvent(int dwFlags, int dx, int dy, int cButtons, int dwExtraInfo);

		void PressModifierKeys(Keys modifierKeys);

		void ReleaseModifierKeys(Keys modifierKeys);

		void Type(string textToType);
	}

	public class AggInputMethods : IInputMethod
	{
		private Point2D currentMousePosition;
		private readonly AutomationRunner automationRunner;

		public bool DrawSimulatedMouse { get; set; } = true;

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
			var sz = default(Size);
			return sz.Height;
		}

		public Point2D CurrentMousePosition()
		{
			return currentMousePosition;
		}

		public void SetCursorPosition(int x, int y)
		{
			var openWindows = SystemWindow.AllOpenSystemWindows.ToList();
			var topSystemWindow = openWindows.LastOrDefault();

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
					var aggEvent = new MouseEventArgs(MouseButtons.Left, 0, windowPosition.x, windowPosition.y, 0);
					UiThread.RunOnIdle(() =>
					{
						systemWindow.OnMouseMove(aggEvent);
						systemWindow.Invalidate();
					});
				}
				else
				{
					var aggEvent = new MouseEventArgs(MouseButtons.None, 0, windowPosition.x, windowPosition.y, 0);
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
			// The Clicks values here deliberately mirror what WinForms delivers for real input (see
			// WinformsEventSink): a down reports 1, except the second press of a double click which
			// reports 2 - and EVERY up reports 1, even the one ending a double click. Widgets that
			// need to act on the up of a double click (GuiWidget.IsDoubleClick) remember the down's
			// click count themselves, exactly because production ups never carry a 2.
			bool isDownEvent = dwFlags == MouseConsts.MOUSEEVENTF_LEFTDOWN
				|| dwFlags == MouseConsts.MOUSEEVENTF_RIGHTDOWN
				|| dwFlags == MouseConsts.MOUSEEVENTF_MIDDLEDOWN;

			bool isUpEvent = dwFlags == MouseConsts.MOUSEEVENTF_LEFTUP
				|| dwFlags == MouseConsts.MOUSEEVENTF_RIGHTUP
				|| dwFlags == MouseConsts.MOUSEEVENTF_MIDDLEUP;

			// Send mouse event into the first SystemWindow (reverse order) containing the mouse
			foreach (var systemWindow in SystemWindow.AllOpenSystemWindows.Reverse().ToList())
			{
				Point2D windowPosition = AutomationRunner.ScreenToSystemWindow(currentMousePosition, systemWindow);
				if (systemWindow.LocalBounds.Contains(windowPosition))
				{
					MouseButtons mouseButtons = MapButtons(dwFlags);
					if (isDownEvent)
					{
						// The caller states the click number (see IInputMethod.CreateMouseEvent);
						// only "second press of a double click" is meaningful, everything else is 1.
						// Captured into a local so a down queued behind another down cannot read the
						// later event's count when the idle action finally runs.
						var clicks = cButtons == 2 ? 2 : 1;
						this.ClickCount = clicks;

						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseDown(new MouseEventArgs(mouseButtons, clicks, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
					else if (isUpEvent)
					{
						UiThread.RunOnIdle(() =>
						{
							systemWindow.OnMouseUp(new MouseEventArgs(mouseButtons, 1, windowPosition.x, windowPosition.y, 0));
							systemWindow.Invalidate();
						});

						// Stop processing after first match
						break;
					}
				}
			}

			this.LeftButtonDown = dwFlags == MouseConsts.MOUSEEVENTF_LEFTDOWN;
			this.MiddleButtonDown = dwFlags == MouseConsts.MOUSEEVENTF_MIDDLEDOWN;
			this.RightButtonDown = dwFlags == MouseConsts.MOUSEEVENTF_RIGHTDOWN;
		}

		private MouseButtons MapButtons(int cButtons)
		{
			switch (cButtons)
			{
				case MouseConsts.MOUSEEVENTF_LEFTDOWN:
				case MouseConsts.MOUSEEVENTF_LEFTUP:
					return MouseButtons.Left;

				case MouseConsts.MOUSEEVENTF_RIGHTDOWN:
				case MouseConsts.MOUSEEVENTF_RIGHTUP:
					return MouseButtons.Right;

				case MouseConsts.MOUSEEVENTF_MIDDLEDOWN:
				case MouseConsts.MOUSEEVENTF_MIDDLEUP:
					return MouseButtons.Middle;
			}

			return MouseButtons.Left;
		}

		/// <summary>
		/// Puts a type string through the widget tree, one stroke at a time.
		/// <para>
		/// See <see cref="TypedKeyParser"/> for the spelling. Everything that decides *what* the string
		/// means lives there so it can be tested without a window; this method only decides how a stroke
		/// is delivered.
		/// </para>
		/// </summary>
		/// <param name="textToType">The keys to send, e.g. "^z", "{Enter}", "some text".</param>
		public void Type(string textToType)
		{
			var systemWindow = FindRootSystemWindow();

			// Not a key at all - the platform's close chord, which the agg input method answers by closing
			// the window rather than by finding something to route Alt+F4 to. Parsed up front so a bad
			// type string throws on the caller's thread, where the test can see it, rather than inside an
			// idle callback.
			bool isCloseChord = textToType == "%{F4}";
			var strokes = isCloseChord ? null : TypedKeyParser.Parse(textToType);

			// Setup reset event to block until input received
			var resetEvent = new AutoResetEvent(false);

			UiThread.RunOnIdle(() =>
			{
				if (isCloseChord)
				{
					((SystemWindow)systemWindow).Close();
				}
				else
				{
					foreach (var stroke in strokes)
					{
						SendStroke(stroke, systemWindow);
					}
				}

				resetEvent.Set();
			});

			resetEvent.WaitOne();
		}

		/// <summary>
		/// Sends one stroke's down, up and (when it types a character) press.
		/// <para>
		/// The modifier state is set around the stroke as well as carried in the event, because widgets
		/// read modifiers both ways - the event's <c>Control</c> flag on the way down, and
		/// <c>Keyboard.IsKeyDown</c> from code that runs later in the same gesture.
		/// </para>
		/// </summary>
		private static void SendStroke(TypedKey stroke, GuiWidget receiver)
		{
			SetModifierState(stroke.Key, true);

			try
			{
				var keyDownEvent = new KeyEventArgs(stroke.Key);
				receiver.OnKeyDown(keyDownEvent);

				var keyUpEvent = new KeyEventArgs(stroke.Key);
				receiver.OnKeyUp(keyUpEvent);

				// A widget that suppressed the key press wants the character swallowed - that is how a text
				// field keeps a shortcut it just acted on from also being typed into it.
				if (stroke.HasCharacter
					&& !keyDownEvent.SuppressKeyPress
					&& !keyUpEvent.SuppressKeyPress)
				{
					receiver.OnKeyPress(new KeyPressEventArgs(stroke.Character));
				}
			}
			finally
			{
				SetModifierState(stroke.Key, false);
			}
		}

		private static void SetModifierState(Keys key, bool down)
		{
			if ((key & Keys.Control) == Keys.Control)
			{
				Keyboard.SetKeyDownState(Keys.Control, down);
			}

			if ((key & Keys.Shift) == Keys.Shift)
			{
				Keyboard.SetKeyDownState(Keys.Shift, down);
			}

			if ((key & Keys.Alt) == Keys.Alt)
			{
				Keyboard.SetKeyDownState(Keys.Alt, down);
			}
		}

		private GuiWidget FindRootSystemWindow()
		{
			// Find the top systemWindow, then find its root
			var topWindow = SystemWindow.AllOpenSystemWindows.Last();
			return topWindow.Parents<GuiWidget>().LastOrDefault() ?? topWindow;
		}

		private static readonly List<Keys> ModifierKeyList = new List<Keys>()
		{
			Keys.ShiftKey,
			Keys.LShiftKey,
			Keys.RShiftKey,
			Keys.Shift,
			Keys.ControlKey,
			Keys.LControlKey,
			Keys.RControlKey,
			Keys.Control,
			Keys.Menu,
			Keys.Alt
		};

		private static readonly uint ModifierKeyMask = ModifierKeyList.Aggregate(0u, (acc, v) => acc | (uint)v);

		private static void TrySetModifierKeys(IPlatformWindow platformWindow, Keys mods)
		{
			var type = platformWindow.GetType();
			while (type != null)
			{
				var method = type.GetMethod("SetModifierKeys", BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.DeclaredOnly);
				if (method != null)
				{
					method.Invoke(platformWindow, new object[] { mods });
					return;
				}
				type = type.BaseType;
			}
		}

		public void PressModifierKeys(Keys modifierKeys)
		{
			var mods = (uint)modifierKeys & ModifierKeyMask;
			var keyDownEvent = new KeyEventArgs((Keys)mods);
			var systemWindow = (SystemWindow)FindRootSystemWindow();

			void setModifiers(uint m, Keys key)
			{
				var bits = (uint)key;
				if ((m & bits) == bits)
				{
					Keyboard.SetKeyDownState(key, true);
				}
			}

			mods = (uint)keyDownEvent.Modifiers;
			switch (keyDownEvent.KeyCode)
			{
				case Keys.ShiftKey:
				case Keys.LShiftKey:
				case Keys.RShiftKey:
					mods |= (uint)Keys.Shift;
					break;

				case Keys.ControlKey:
				case Keys.LControlKey:
				case Keys.RControlKey:
					mods |= (uint)Keys.Control;
					break;

				case Keys.Menu:
					mods |= (uint)Keys.Alt;
					break;
			}

			setModifiers(mods, Keys.Shift);
			setModifiers(mods, Keys.Control);
			setModifiers(mods, Keys.Alt);

			TrySetModifierKeys(systemWindow.PlatformWindow, (Keys)mods);

			systemWindow.OnKeyDown(keyDownEvent);
		}

		public void ReleaseModifierKeys(Keys modifierKeys)
		{
			var mods = (uint)modifierKeys & ModifierKeyMask;
			var keyUpEvent = new KeyEventArgs((Keys)mods);
			var systemWindow = (SystemWindow)FindRootSystemWindow();

			void unsetModifier(uint m, Keys key)
			{
				var bits = (uint)key;
				if ((m & bits) != bits)
				{
					Keyboard.SetKeyDownState(key, false);
				}
			}

			mods = (uint)keyUpEvent.Modifiers;
			switch (keyUpEvent.KeyCode)
			{
				case Keys.ShiftKey:
				case Keys.LShiftKey:
				case Keys.RShiftKey:
					mods &= ~(uint)Keys.Shift;
					break;

				case Keys.ControlKey:
				case Keys.LControlKey:
				case Keys.RControlKey:
					mods &= ~(uint)Keys.Control;
					break;

				case Keys.Menu:
					mods &= ~(uint)Keys.Alt;
					break;
			}

			unsetModifier(mods, Keys.Shift);
			unsetModifier(mods, Keys.Control);
			unsetModifier(mods, Keys.Alt);

			TrySetModifierKeys(systemWindow.PlatformWindow, (Keys)mods);

			systemWindow.OnKeyUp(keyUpEvent);
		}

		private void SendKey(Keys keyDown, char keyPressed, GuiWidget receiver)
		{
			var keyDownEvent = new KeyEventArgs(keyDown);
			receiver.OnKeyDown(keyDownEvent);
			if (!keyDownEvent.SuppressKeyPress)
			{
				receiver.OnKeyPress(new KeyPressEventArgs(keyPressed));
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
