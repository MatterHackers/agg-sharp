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
using GLFW;
using MatterHackers.Agg;
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

		private SystemWindow systemWindow;

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

		public Point2D DesktopPosition { get; set; }

		public bool Invalidated { get; set; }

		public Vector2 MinimumSize { get; set; }

		public Agg.UI.Keys ModifierKeys => Agg.UI.Keys.None;

		public int TitleBarHeight => 45;

		public GlfwWindowProvider WindowProvider { get; set; }

		public void BringToFront()
		{
			// throw new NotImplementedException();
		}

		public void Close()
		{
			// throw new NotImplementedException();
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			// throw new NotImplementedException();
		}

		public void Invalidate(RectangleDouble rectToInvalidate)
		{
			Invalidated = true;
		}

		public Graphics2D NewGraphics2D()
		{
			SetupViewport();

			// this is for testing the openGL implementation
			var graphics2D = new Graphics2DOpenGL((int)this.systemWindow.Width,
				(int)this.systemWindow.Height,
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
				this.systemWindow = systemWindow;

				this.Show();
			}
			else
			{
				// Notify the embedded window of its new single windows parent size

				// If client code has called ShowSystemWindow and we're minimized, we must restore in order
				// to establish correct window bounds from ClientSize below. Otherwise we're zeroed out and
				// will create invalid surfaces of (0,0)
				// if (this.WindowState == FormWindowState.Minimized)
				{
					// this.WindowState = FormWindowState.Normal;
				}

				systemWindow.Size = new Vector2(this.systemWindow.Width, this.systemWindow.Height);
			}
		}

		private void CursorPositionCallback(IntPtr window, double x, double y)
		{
			mouseX = x;
			mouseY = systemWindow.Height - y;
			systemWindow.OnMouseMove(new MouseEventArgs(mouseButton, 0, mouseX, mouseY, 0));
		}

		private void DrawAndUpdate(SystemWindow systemWindow)
		{
			if (this.Invalidated)
			{
				this.Invalidated = false;
				Graphics2D graphics2D = new Graphics2DOpenGL((int)systemWindow.Width, (int)systemWindow.Height, GuiWidget.DeviceScale);
				graphics2D.PushTransform();
				systemWindow.OnDrawBackground(graphics2D);
				systemWindow.OnDraw(graphics2D);

				Glfw.SwapBuffers(glfwWindow);
			}
		}

		private void KeyCallback(IntPtr windowIn, GLFW.Keys key, int scanCode, InputState state, ModifierKeys mods)
		{
			{
				switch (key)
				{
					case GLFW.Keys.Escape:
						// Glfw.SetWindowShouldClose(window, true);
						break;
				}
			}
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
				case Cursors.SizeNWSE:
				case Cursors.SizeWE:
				case Cursors.UpArrow:
					return Glfw.CreateStandardCursor(CursorType.Arrow);

				case Cursors.VSplit:
					return Glfw.CreateStandardCursor(CursorType.ResizeHorizontal);

				case Cursors.WaitCursor:
					return Glfw.CreateStandardCursor(CursorType.Arrow);
			}

			return Glfw.CreateStandardCursor(CursorType.Arrow);
		}

		private void MouseButtonCallback(IntPtr window, MouseButton button, InputState state, ModifierKeys modifiers)
		{
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
				systemWindow.OnMouseDown(new MouseEventArgs(mouseButton, clickCount[button], mouseX, mouseY, 0));
			}
			else if (state == InputState.Release)
			{
				systemWindow.OnMouseUp(new MouseEventArgs(mouseButton, clickCount[button], mouseX, mouseY, 0));
			}
		}

		private void ScrollCallback(IntPtr window, double x, double y)
		{
			systemWindow.OnMouseWheel(new MouseEventArgs(mouseButton, 0, mouseX, mouseY, (int)y));
		}

		private void SetupViewport()
		{
			// If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
			// Call this function you have called Show().
			int w = (int)systemWindow.Width;
			int h = (int)systemWindow.Height;
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();
			GL.Ortho(0, w, 0, h, -1, 1); // Bottom-left corner pixel has coordinate (0, 0)
			GL.Viewport(0, 0, w, h); // Use all of the glControl painting area
		}

		private void Show()
		{
			// Glfw.WindowHint(Hint.Decorated, false);
			Glfw.WindowHint(Hint.Samples, 4);

			var glfwGl = new GlfwGL();
			// set the gl renderer to the GLFW specific one rather than the OpenTk one
			GL.Instance = glfwGl;

			// Create window
			glfwWindow = Glfw.CreateWindow((int)systemWindow.Width, (int)systemWindow.Height, systemWindow.Title, Monitor.None, Window.None);
			Glfw.MakeContextCurrent(glfwWindow);
			OpenGL.Gl.Import(Glfw.GetProcAddress);

			// Effectively enables VSYNC by setting to 1.
			Glfw.SwapInterval(1);

			systemWindow.PlatformWindow = this;

			if (systemWindow.Maximized)
			{
				// TODO: make this right
				var screenSize = Glfw.PrimaryMonitor.WorkArea;
				var x = (screenSize.Width - (int)systemWindow.Width) / 2;
				var y = (screenSize.Height - (int)systemWindow.Height) / 2;
				Glfw.SetWindowPosition(glfwWindow, x, y);
			}
			else if (systemWindow.InitialDesktopPosition == new Point2D(-1, -1))
			{
				// Find center position based on window and monitor sizes
				var screenSize = Glfw.PrimaryMonitor.WorkArea;
				var x = (screenSize.Width - (int)systemWindow.Width) / 2;
				var y = (screenSize.Height - (int)systemWindow.Height) / 2;
				Glfw.SetWindowPosition(glfwWindow, x, y);
			}
			else
			{
				Glfw.SetWindowPosition(glfwWindow,
					(int)systemWindow.InitialDesktopPosition.x,
					(int)systemWindow.InitialDesktopPosition.y);
			}

			Glfw.SetWindowSizeCallback(glfwWindow, SizeCallback);

			// Set a key callback
			Glfw.SetKeyCallback(glfwWindow, KeyCallback);
			Glfw.SetCursorPositionCallback(glfwWindow, CursorPositionCallback);
			Glfw.SetMouseButtonCallback(glfwWindow, MouseButtonCallback);
			Glfw.SetScrollCallback(glfwWindow, ScrollCallback);

			while (!Glfw.WindowShouldClose(glfwWindow))
			{
				// Poll for OS events and swap front/back buffers
				Glfw.PollEvents();
				UiThread.InvokePendingActions();

				DrawAndUpdate(systemWindow);
			}
		}

		private void SizeCallback(IntPtr window, int width, int height)
		{
			systemWindow.Size = new VectorMath.Vector2(width, height);
			GL.Viewport(0, 0, width, height); // Use all of the glControl painting area
			DrawAndUpdate(systemWindow);
		}
	}
}