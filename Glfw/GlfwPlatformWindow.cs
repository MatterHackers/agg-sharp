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
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using GLFW;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.GlfwProvider
{
	public class GlfwPlatformWindow : IPlatformWindow
	{
		private static bool firstWindow = true;

		private static MouseButtons mouseButton;

		private static double mouseX;

		private static double mouseY;

		private readonly Dictionary<MouseButton, int> clickCount = new Dictionary<MouseButton, int>();

		private readonly Dictionary<MouseButton, long> lastMouseDownTime = new Dictionary<MouseButton, long>();

		private string _title = "";

		private Window glfwWindow;

		private SystemWindow aggSystemWindow
		{
			get;
			set;
		}

		private bool iconified;
		private static GlfwPlatformWindow staticThis;

		public GlfwPlatformWindow()
		{
		}

		public string Caption
		{
			get
			{
				return _title;
			}

			set
			{
				_title = value;
				Glfw.SetWindowTitle(glfwWindow, _title);
			}
		}

		public Point2D DesktopPosition
		{
			get
			{
				Glfw.GetWindowPosition(glfwWindow, out int x, out int y);

				return new Point2D(x, y);
			}

			set
			{
				Glfw.SetWindowPosition(glfwWindow, value.x, value.y);
			}
		}

		public bool Invalidated { get; set; } = true;

		public Vector2 MinimumSize
		{
			get
			{
				return this.aggSystemWindow.MinimumSize;
			}

			set
			{
				this.aggSystemWindow.MinimumSize = value;
				Glfw.SetWindowSizeLimits(glfwWindow,
					(int)aggSystemWindow.MinimumSize.X,
					(int)aggSystemWindow.MinimumSize.Y,
					-1,
					-1);
			}
		}

		public int TitleBarHeight => 45;

		public GlfwWindowProvider WindowProvider { get; set; }

		public void BringToFront()
		{
			Glfw.ShowWindow(glfwWindow);
		}

		public void Activate()
		{
			Glfw.ShowWindow(glfwWindow);
		}

		public void Close()
		{
			throw new NotImplementedException();
		}

		private readonly bool winformAlreadyClosing = false;

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			// Prevent our call to SystemWindow.Close from recursing
			if (winformAlreadyClosing)
			{
				return;
			}

			// Check for RootSystemWindow, close if found
			string windowTypeName = systemWindow.GetType().Name;

			if (windowTypeName == "RootSystemWindow")
			{
				// Close the main (first) PlatformWindow if it's being requested and not this instance
				Glfw.SetWindowShouldClose(glfwWindow, true);

				return;
			}

			aggSystemWindow = this.WindowProvider.TopWindow;
			aggSystemWindow?.Invalidate();
		}

		public void Invalidate(RectangleDouble rectToInvalidate)
		{
			Invalidated = true;
		}

		public Graphics2D NewGraphics2D()
		{
			// this is for testing the openGL implementation
			var graphics2D = new Graphics2DOpenGL((int)this.aggSystemWindow.Width,
				(int)this.aggSystemWindow.Height,
				GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		public void SetCursor(Cursors cursorToSet)
		{
			Glfw.SetCursor(glfwWindow, MapCursor(cursorToSet));
		}

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			// Set the active SystemWindow & PlatformWindow references
			systemWindow.PlatformWindow = this;

			systemWindow.AnchorAll();

			if (firstWindow)
			{
				firstWindow = false;
				this.aggSystemWindow = systemWindow;

				this.Show();
			}
			else
			{
				systemWindow.Size = new Vector2(this.aggSystemWindow.Width, this.aggSystemWindow.Height);
			}
		}

		private void CursorPositionCallback(Window window, double x, double y)
		{
			if (!IPlatformWindow.EnablePlatformWindowInput)
				return;

			mouseX = x;
			mouseY = aggSystemWindow.Height - y;
			WindowProvider.TopWindow.OnMouseMove(new MouseEventArgs(mouseButton, 0, mouseX, mouseY, 0));
		}

		private void ConditionalDrawAndRefresh(SystemWindow systemWindow)
		{
			if (this.Invalidated
				&& !iconified)
			{
				ResetViewport();

				this.Invalidated = false;
				Graphics2D graphics2D = NewGraphics2D();
				for (var i = 0; i < this.WindowProvider.OpenWindows.Count; i++)
				{
					var window = this.WindowProvider.OpenWindows[i];

					// Due to handling in CloseSystemWindow, testing can sometimes end up handling a draw event with no PlatformWindow, so skip this window.
					// TODO: Unify this stuff with PlatformWin32?
					if (window.PlatformWindow == null)
						continue;

					if (i > 0)
					{
						window.Size = systemWindow.Size;
						ResetViewport();
						graphics2D.FillRectangle(this.WindowProvider.OpenWindows[0].LocalBounds, new Agg.Color(Agg.Color.Black, 160));
					}
					
					window.OnDraw(graphics2D);
				}

				Glfw.SwapBuffers(glfwWindow);
			}
		}

		private void CharCallback(Window window, uint codePoint)
		{
			if (!IPlatformWindow.EnablePlatformWindowInput)
				return;

			WindowProvider.TopWindow.OnKeyPress(new KeyPressEventArgs((char)codePoint));
		}

		public Agg.UI.Keys ModifierKeys { get; private set; } = Agg.UI.Keys.None;

		private void UpdateKeyboard(ModifierKeys theEvent)
		{
			int keys = (int)Agg.UI.Keys.None;

			var shiftKey = theEvent.HasFlag(GLFW.ModifierKeys.Shift);
			Keyboard.SetKeyDownState(Agg.UI.Keys.Shift, shiftKey);
			if (shiftKey)
			{
				keys |= (int)Agg.UI.Keys.Shift;
			}

			var controlKey = theEvent.HasFlag(GLFW.ModifierKeys.Control)
				|| theEvent.HasFlag(GLFW.ModifierKeys.Super);
			Keyboard.SetKeyDownState(Agg.UI.Keys.Control, controlKey);
			if (controlKey)
			{
				keys |= (int)Agg.UI.Keys.Control;
			}

			var altKey = theEvent.HasFlag(GLFW.ModifierKeys.Alt);
			Keyboard.SetKeyDownState(Agg.UI.Keys.Alt, altKey);
			if (altKey)
			{
				keys |= (int)Agg.UI.Keys.Alt;
			}

			ModifierKeys = (Agg.UI.Keys)keys;
		}

		private readonly HashSet<Agg.UI.Keys> suppressedKeyDowns = new HashSet<Agg.UI.Keys>();

		private void KeyCallback(Window windowIn, GLFW.Keys key, int scanCode, InputState state, ModifierKeys mods)
		{
			if (!IPlatformWindow.EnablePlatformWindowInput)
				return;

			if (state == InputState.Press || state == InputState.Repeat)
			{
				var keyData = MapKey(key, out bool _);
				if (keyData != Agg.UI.Keys.None && keyData != Agg.UI.Keys.Modifiers)
				{
					Keyboard.SetKeyDownState(keyData, true);
				}
				else if (keyData == Agg.UI.Keys.Modifiers)
				{
					mods |= MapModifier(key);
				}
				UpdateKeyboard(mods);

				var keyEvent = new Agg.UI.KeyEventArgs(keyData | ModifierKeys);
				WindowProvider.TopWindow.OnKeyDown(keyEvent);

				if (keyEvent.SuppressKeyPress)
				{
					suppressedKeyDowns.Add(keyEvent.KeyCode);
				}
				else
				{
					// send any key that we need to that is not being sent in GLFWs CharCallback
					switch (key)
					{
						case GLFW.Keys.Enter:
						case GLFW.Keys.NumpadEnter:
							WindowProvider.TopWindow.OnKeyPress(new KeyPressEventArgs((char)13));
							break;
					}
				}
			}
			else if (state == InputState.Release)
			{
				var keyData = MapKey(key, out bool suppress);
				if (keyData != Agg.UI.Keys.None && keyData != Agg.UI.Keys.Modifiers)
				{
					Keyboard.SetKeyDownState(keyData, false);
				}
				else if (keyData == Agg.UI.Keys.Modifiers)
				{
					mods &= ~MapModifier(key);
				}
				UpdateKeyboard(mods);

				var keyEvent = new Agg.UI.KeyEventArgs(keyData | ModifierKeys);

				WindowProvider.TopWindow.OnKeyUp(keyEvent);

				if (suppressedKeyDowns.Contains(keyEvent.KeyCode))
				{
					suppressedKeyDowns.Remove(keyEvent.KeyCode);
				}
			}
		}

		private Agg.UI.Keys MapKey(GLFW.Keys key, out bool suppress)
		{
			suppress = true;

			switch (key)
			{
				case GLFW.Keys.F1:
					return Agg.UI.Keys.F1;

				case GLFW.Keys.F2:
					return Agg.UI.Keys.F2;

				case GLFW.Keys.F3:
					return Agg.UI.Keys.F3;

				case GLFW.Keys.F4:
					return Agg.UI.Keys.F4;

				case GLFW.Keys.F5:
					return Agg.UI.Keys.F5;

				case GLFW.Keys.F6:
					return Agg.UI.Keys.F6;

				case GLFW.Keys.F7:
					return Agg.UI.Keys.F7;

				case GLFW.Keys.F8:
					return Agg.UI.Keys.F8;

				case GLFW.Keys.F9:
					return Agg.UI.Keys.F9;

				case GLFW.Keys.F10:
					return Agg.UI.Keys.F10;

				case GLFW.Keys.F11:
					return Agg.UI.Keys.F11;

				case GLFW.Keys.F12:
					return Agg.UI.Keys.F12;

				case GLFW.Keys.Home:
					return Agg.UI.Keys.Home;

				case GLFW.Keys.PageUp:
					return Agg.UI.Keys.PageUp;

				case GLFW.Keys.PageDown:
					return Agg.UI.Keys.PageDown;

				case GLFW.Keys.End:
					return Agg.UI.Keys.End;

				case GLFW.Keys.Escape:
					return Agg.UI.Keys.Escape;

				case GLFW.Keys.Left:
					return Agg.UI.Keys.Left;

				case GLFW.Keys.Right:
					return Agg.UI.Keys.Right;

				case GLFW.Keys.Up:
					return Agg.UI.Keys.Up;

				case GLFW.Keys.Down:
					return Agg.UI.Keys.Down;

				case GLFW.Keys.Backspace:
					return Agg.UI.Keys.Back;

				case GLFW.Keys.Delete:
					return Agg.UI.Keys.Delete;

				case GLFW.Keys.LeftShift:
					return Agg.UI.Keys.Modifiers;

				case GLFW.Keys.LeftControl:
					return Agg.UI.Keys.Modifiers;

				case GLFW.Keys.LeftSuper:
					return Agg.UI.Keys.Modifiers;

				case GLFW.Keys.LeftAlt:
					return Agg.UI.Keys.Modifiers;

				case GLFW.Keys.RightShift:
					return Agg.UI.Keys.Modifiers;

				case GLFW.Keys.RightControl:
					return Agg.UI.Keys.Modifiers;

				case GLFW.Keys.RightAlt:
					return Agg.UI.Keys.Modifiers;

				case GLFW.Keys.RightSuper:
					return Agg.UI.Keys.Modifiers;
			}

			suppress = false;

			switch (key)
			{
				/*
				case GLFW.Keys.D0:
					return Agg.UI.Keys.D0;
				case GLFW.Keys.D1:
					return Agg.UI.Keys.D1;
				case GLFW.Keys.D2:
					return Agg.UI.Keys.D2;
				case GLFW.Keys.D3:
					return Agg.UI.Keys.D3;
				case GLFW.Keys.D4:
					return Agg.UI.Keys.D4;
				case GLFW.Keys.D5:
					return Agg.UI.Keys.D5;
				case GLFW.Keys.D6:
					return Agg.UI.Keys.D6;
				case GLFW.Keys.D7:
					return Agg.UI.Keys.D7;
				case GLFW.Keys.D8:
					return Agg.UI.Keys.D8;
				case GLFW.Keys.D9:
					return Agg.UI.Keys.D9;
				*/

				case GLFW.Keys.Numpad0:
					return Agg.UI.Keys.NumPad0;
				case GLFW.Keys.Numpad1:
					return Agg.UI.Keys.NumPad1;
				case GLFW.Keys.Numpad2:
					return Agg.UI.Keys.NumPad2;
				case GLFW.Keys.Numpad3:
					return Agg.UI.Keys.NumPad3;
				case GLFW.Keys.Numpad4:
					return Agg.UI.Keys.NumPad4;
				case GLFW.Keys.Numpad5:
					return Agg.UI.Keys.NumPad5;
				case GLFW.Keys.Numpad6:
					return Agg.UI.Keys.NumPad6;
				case GLFW.Keys.Numpad7:
					return Agg.UI.Keys.NumPad7;
				case GLFW.Keys.Numpad8:
					return Agg.UI.Keys.NumPad8;
				case GLFW.Keys.Numpad9:
					return Agg.UI.Keys.NumPad9;
				case GLFW.Keys.NumpadEnter:
					return Agg.UI.Keys.Enter;

				case GLFW.Keys.Tab:
					return Agg.UI.Keys.Tab;
				case GLFW.Keys.Enter:
					return Agg.UI.Keys.Return;

				case GLFW.Keys.A:
					return Agg.UI.Keys.A;
				case GLFW.Keys.B:
					return Agg.UI.Keys.B;
				case GLFW.Keys.C:
					return Agg.UI.Keys.C;
				case GLFW.Keys.D:
					return Agg.UI.Keys.D;
				case GLFW.Keys.E:
					return Agg.UI.Keys.E;
				case GLFW.Keys.F:
					return Agg.UI.Keys.F;
				case GLFW.Keys.G:
					return Agg.UI.Keys.G;
				case GLFW.Keys.H:
					return Agg.UI.Keys.H;
				case GLFW.Keys.I:
					return Agg.UI.Keys.I;
				case GLFW.Keys.J:
					return Agg.UI.Keys.J;
				case GLFW.Keys.K:
					return Agg.UI.Keys.K;
				case GLFW.Keys.L:
					return Agg.UI.Keys.L;
				case GLFW.Keys.M:
					return Agg.UI.Keys.M;
				case GLFW.Keys.N:
					return Agg.UI.Keys.N;
				case GLFW.Keys.O:
					return Agg.UI.Keys.O;
				case GLFW.Keys.P:
					return Agg.UI.Keys.P;
				case GLFW.Keys.Q:
					return Agg.UI.Keys.Q;
				case GLFW.Keys.R:
					return Agg.UI.Keys.R;
				case GLFW.Keys.S:
					return Agg.UI.Keys.S;
				case GLFW.Keys.T:
					return Agg.UI.Keys.T;
				case GLFW.Keys.U:
					return Agg.UI.Keys.U;
				case GLFW.Keys.V:
					return Agg.UI.Keys.V;
				case GLFW.Keys.W:
					return Agg.UI.Keys.W;
				case GLFW.Keys.X:
					return Agg.UI.Keys.X;
				case GLFW.Keys.Y:
					return Agg.UI.Keys.Y;
				case GLFW.Keys.Z:
					return Agg.UI.Keys.Z;
			}

			return Agg.UI.Keys.None;
		}

		private ModifierKeys MapModifier(GLFW.Keys key)
		{
			// When only a single modifier key is pressed by itself in Linux, Modifiers is zero but
			// when the key is released, Modifiers is set. This messes up the key state being tracked
			// in the Keyboard class and is different than in Windows. Patch up Modifiers so it is
			// consistent on both platforms.

			switch (key)
			{
				case GLFW.Keys.LeftShift:
					return GLFW.ModifierKeys.Shift;
				case GLFW.Keys.LeftControl:
				case GLFW.Keys.LeftSuper:
					return GLFW.ModifierKeys.Control;
				case GLFW.Keys.LeftAlt:
					return GLFW.ModifierKeys.Alt;
				case GLFW.Keys.RightShift:
					return GLFW.ModifierKeys.Shift;
				case GLFW.Keys.RightControl:
				case GLFW.Keys.RightSuper:
					return GLFW.ModifierKeys.Control;
				case GLFW.Keys.RightAlt:
					return GLFW.ModifierKeys.Alt;
			}

			return 0;
		}

		private Cursor MapCursor(Cursors cursorToSet)
		{
			switch (cursorToSet)
			{
				case Cursors.Arrow:
					return Glfw.CreateStandardCursor(CursorType.Arrow);

				case Cursors.Cross:
				case Cursors.Default:
					return Glfw.CreateStandardCursor(CursorType.Arrow);

				case Cursors.Hand:
					return Glfw.CreateStandardCursor(CursorType.Hand);

				case Cursors.Help:
					return Glfw.CreateStandardCursor(CursorType.Arrow);

				case Cursors.HSplit:
					return Glfw.CreateStandardCursor(CursorType.ResizeVertical);

				case Cursors.IBeam:
					return Glfw.CreateStandardCursor(CursorType.Beam);

				case Cursors.No:
				case Cursors.NoMove2D:
				case Cursors.NoMoveHoriz:
				case Cursors.NoMoveVert:
				case Cursors.PanEast:
				case Cursors.PanNE:
				case Cursors.PanNorth:
				case Cursors.PanNW:
				case Cursors.PanSE:
				case Cursors.PanSouth:
				case Cursors.PanSW:
				case Cursors.PanWest:
				case Cursors.SizeAll:
				case Cursors.SizeNESW:
				case Cursors.SizeNS:
					return Glfw.CreateStandardCursor(CursorType.ResizeVertical);

				case Cursors.SizeNWSE:
				case Cursors.SizeWE:
					return Glfw.CreateStandardCursor(CursorType.ResizeHorizontal);

				case Cursors.UpArrow:
					return Glfw.CreateStandardCursor(CursorType.Arrow);

				case Cursors.VSplit:
					return Glfw.CreateStandardCursor(CursorType.ResizeHorizontal);

				case Cursors.WaitCursor:
					return Glfw.CreateStandardCursor(CursorType.Arrow);
			}

			return Glfw.CreateStandardCursor(CursorType.Arrow);
		}

		private void MouseButtonCallback(Window window, MouseButton button, InputState state, ModifierKeys modifiers)
		{
			if (!IPlatformWindow.EnablePlatformWindowInput)
				return;

			var now = UiThread.CurrentTimerMs;
			mouseButton = MouseButtons.Left;
			switch (button)
			{
				case MouseButton.Middle:
					mouseButton = MouseButtons.Middle;
					break;

				case MouseButton.Right:
					mouseButton = MouseButtons.Right;
					break;
			}

			if (state == InputState.Press)
			{
				clickCount[button] = 1;
				if (lastMouseDownTime.ContainsKey(button))
				{
					if (lastMouseDownTime[button] > now - 500)
					{
						clickCount[button] = 2;
					}
				}

				lastMouseDownTime[button] = now;
				if (clickCount.ContainsKey(button))
				{
					WindowProvider.TopWindow.OnMouseDown(new MouseEventArgs(mouseButton, clickCount[button], mouseX, mouseY, 0));
				}
			}
			else if (state == InputState.Release)
			{
				if (clickCount.ContainsKey(button))
				{
					WindowProvider.TopWindow.OnMouseUp(new MouseEventArgs(mouseButton, clickCount[button], mouseX, mouseY, 0));
				}
			}
		}

		private void ScrollCallback(Window window, double x, double y)
		{
			if (!IPlatformWindow.EnablePlatformWindowInput)
				return;

			WindowProvider.TopWindow.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, mouseX, mouseY, (int)(y * 120)));
		}

		private void ResetViewport()
		{
			Glfw.MakeContextCurrent(glfwWindow);

			// If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
			// Call this function you have called Show().
			int w = (int)aggSystemWindow.Width;
			int h = (int)aggSystemWindow.Height;

			Glfw.GetWindowContentScale(glfwWindow, out float xScale, out float yScale);

			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();
			GL.Scissor(0, 0, w, h);

			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.Ortho(0, w, 0, h, -1, 1); // Bottom-left corner pixel has coordinate (0, 0)
			GL.Viewport(0, 0, w, h); // Use all of the glControl painting area
		}

		private void Show()
		{
			// Glfw.WindowHint(Hint.Decorated, false);
			var config = AggContext.Config.GraphicsMode;
			Glfw.WindowHint(Hint.Samples, config.FSAASamples);
			Glfw.WindowHint(Hint.Visible, false); // this line causing crash on windows tablet
			Glfw.WindowHint(Hint.CocoaRetinaFrameBuffer, true);

			var screenSize = Glfw.PrimaryMonitor.WorkArea;

			// Create window
			if (aggSystemWindow.Maximized)
			{
				aggSystemWindow.Width = screenSize.Width;
				aggSystemWindow.Height = screenSize.Height - screenSize.Y;
			}

			glfwWindow = Glfw.CreateWindow((int)aggSystemWindow.Width, (int)aggSystemWindow.Height, aggSystemWindow.Title, GLFW.Monitor.None, Window.None);

			Glfw.MakeContextCurrent(glfwWindow);

			// Effectively enables VSYNC by setting to 1.
			Glfw.SwapInterval(1);

			aggSystemWindow.PlatformWindow = this;

			Glfw.SetWindowSizeLimits(glfwWindow,
				(int)aggSystemWindow.MinimumSize.X,
				(int)aggSystemWindow.MinimumSize.Y,
				-1,
				-1);

			if (aggSystemWindow.Maximized)
			{
				// TODO: make this right
				Glfw.SetWindowPosition(glfwWindow, 0, 0);
				Glfw.MaximizeWindow(glfwWindow);
			}
			else if (aggSystemWindow.InitialDesktopPosition == new Point2D(-1, -1))
			{
				// Find center position based on window and monitor sizes
				var x = (screenSize.Width - (int)aggSystemWindow.Width) / 2;
				var y = (screenSize.Height - (int)aggSystemWindow.Height) / 2;
				Glfw.SetWindowPosition(glfwWindow, x, y);
			}
			else
			{
				Glfw.SetWindowPosition(glfwWindow,
					(int)aggSystemWindow.InitialDesktopPosition.x,
					(int)aggSystemWindow.InitialDesktopPosition.y);
			}

			staticThis = this;
			Glfw.SetWindowSizeCallback(glfwWindow, (a, b, c) => staticThis.SizeCallback(a, b, c));
			Glfw.SetWindowMaximizeCallback(glfwWindow, (a, b) => staticThis.MaximizeCallback(a, b));
			Glfw.SetWindowIconifyCallback(glfwWindow, (a, b) => staticThis.IconifyCallback(a, b));

			// Set a key callback
			Glfw.SetKeyCallback(glfwWindow, (a, b, c, d, e) => staticThis.KeyCallback(a, b, c, d, e));
			Glfw.SetCharCallback(glfwWindow, (a, b) => staticThis.CharCallback(a, b));
			Glfw.SetCursorPositionCallback(glfwWindow, (a, b, c) => staticThis.CursorPositionCallback(a, b, c));
			Glfw.SetMouseButtonCallback(glfwWindow, (a, b, c, d) => staticThis.MouseButtonCallback(a, b, c, d));
			Glfw.SetScrollCallback(glfwWindow, (a, b, c) => staticThis.ScrollCallback(a, b, c));
			Glfw.SetCloseCallback(glfwWindow, (a) => staticThis.CloseCallback(a));
			Glfw.SetDropCallback(glfwWindow, (a, b, c) => staticThis.DropCallback(a, b, c));

			var applicationIcon = StaticData.Instance.LoadIcon("application.png");

			if (applicationIcon != null)
			{
				Glfw.SetWindowIcon(glfwWindow,
					2,
					new Image[]
					{
						ConvertImageBufferToImage(applicationIcon),
						ConvertImageBufferToImage(applicationIcon.CreateScaledImage(16, 16))
					});
			}

			// set the gl renderer to the GLFW specific one rather than the OpenTk one
			var glfwGl = new GlfwGL();
			GL.Instance = glfwGl;

			Glfw.ShowWindow(glfwWindow);

			while (!Glfw.WindowShouldClose(glfwWindow))
			{
				// Poll for OS events and swap front/back buffers
				Glfw.PollEvents();
				ConditionalDrawAndRefresh(aggSystemWindow);

				// keep the event thread running
				UiThread.InvokePendingActions();

				// the mac does not report maximize changes correctly
				var maximized = Glfw.GetWindowAttribute(glfwWindow, WindowAttribute.Maximized);
				if (maximized != aggSystemWindow.Maximized)
				{
					aggSystemWindow.Maximized = maximized;
				}

				Thread.Sleep(1);
			}
		}

		public static string[] IntPtrToStringArray<TGenChar>(int size, IntPtr rRoot) where TGenChar : struct
		{
			// get the output array of pointers
			var outPointers = new IntPtr[size];
			Marshal.Copy(rRoot, outPointers, 0, size);
			string[] outputStrArray = new string[size];
			for (int i = 0; i < size; i++)
			{
				if (typeof(TGenChar) == typeof(char))
				{
					outputStrArray[i] = Marshal.PtrToStringUni(outPointers[i]);
				}
				else
				{
					outputStrArray[i] = Marshal.PtrToStringAnsi(outPointers[i]);
				}
			}

			return outputStrArray;
		}

		private void DropCallback(Window window, int count, IntPtr array)
		{
			if (!IPlatformWindow.EnablePlatformWindowInput)
				return;

			var files = IntPtrToStringArray<byte>(count, array).ToList();

			UiThread.RunOnIdle(() =>
			{
				var dropEvent = new MouseEventArgs(Agg.UI.MouseButtons.None, 0, mouseX, mouseY, 0, files);
				aggSystemWindow.OnMouseMove(dropEvent);
				aggSystemWindow.OnMouseUp(dropEvent);
			});
		}

		private Image ConvertImageBufferToImage(ImageBuffer sourceImage)
		{
			unsafe
			{
				var buffer = sourceImage.GetBuffer();
				var flippedBuffer = new byte[buffer.Length];
				var index = 0;
				for (int y = sourceImage.Height - 1; y >= 0; y--)
				{
					for (int x = 0; x < sourceImage.Width; x++)
					{
						var pixel = sourceImage.GetPixel(x, y);
						flippedBuffer[index + 0] = pixel.red;
						flippedBuffer[index + 1] = pixel.green;
						flippedBuffer[index + 2] = pixel.blue;
						flippedBuffer[index + 3] = pixel.alpha;
						index += 4;
					}
				}

				fixed (byte* pBuffer = flippedBuffer)
				{
					return new Image(sourceImage.Width, sourceImage.Height, (IntPtr)pBuffer);
				}
			}
		}

		private void CloseCallback(Window window)
		{
			var closing = new ClosingEventArgs();
			aggSystemWindow.OnClosing(closing);
			if (closing.Cancel)
			{
				Glfw.SetWindowShouldClose(glfwWindow, false);
			}
		}

		private void SizeCallback(Window window, int width, int height)
		{
			aggSystemWindow.Size = new VectorMath.Vector2(width, height);
			GL.Viewport(0, 0, width, height); // Use all of the glControl painting area
			ConditionalDrawAndRefresh(aggSystemWindow);
		}

		private void MaximizeCallback(Window window, bool maximized)
		{
			aggSystemWindow.Maximized = maximized;
		}

		private void IconifyCallback(IntPtr window, bool iconified)
		{
			this.iconified = iconified;
		}
    }
}
