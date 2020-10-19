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

namespace MatterHackers.GlfwProvider
{
	public class GlfwWindowProvider : ISystemWindowProvider
	{
		public IReadOnlyList<SystemWindow> OpenWindows { get; }

		public SystemWindow TopWindow { get; private set; }

		private bool viewPortHasBeenSet = false;

		private void SetAndClearViewPort()
		{
			// Reset The Current Viewport
			GL.Viewport(0, 0, (int)TopWindow.Width, (int)TopWindow.Height);
			viewPortHasBeenSet = true;

			// The following lines set the screen up for a perspective view. Meaning things in the distance get smaller.
			// This creates a realistic looking scene.
			// The perspective is calculated with a 45 degree viewing angle based on the windows width and height.
			// The 0.1f, 100.0f is the starting point and ending point for how deep we can draw into the screen.

			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();
			GL.Scissor(0, 0, (int)TopWindow.Width, (int)TopWindow.Height);

			NewGraphics2D().Clear(new ColorF(1, 1, 1, 1));
		}

		public Graphics2D NewGraphics2D()
		{
			if (!viewPortHasBeenSet)
			{
				SetAndClearViewPort();
			}

			Graphics2D graphics2D = new Graphics2DOpenGL((int)TopWindow.Width, (int)TopWindow.Height, GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		// Creates or connects a PlatformWindow to the given SystemWindow
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			TopWindow = systemWindow;
			var glfwGl = new GlfwGL();
			// set the gl renderer to the Glfw specific one rather than the OpenTk one
			GL.Instance = glfwGl;

			// Create window
			glfwWindow = Glfw.CreateWindow((int)systemWindow.Width, (int)systemWindow.Height, systemWindow.Title, Monitor.None, Window.None);
			Glfw.MakeContextCurrent(glfwWindow);
			OpenGL.Gl.Import(Glfw.GetProcAddress);

			// Effectively enables VSYNC by setting to 1.
			Glfw.SwapInterval(1);

			applicationWindow = new GlfwPlatformWindow(this, glfwWindow);
			systemWindow.PlatformWindow = applicationWindow;

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

			Glfw.SetWindowSizeCallback(glfwWindow, sizeCallback);

			// Set a key callback
			Glfw.SetKeyCallback(glfwWindow, keyCallback);
			Glfw.SetCursorPositionCallback(glfwWindow, cursorPositionCallback);
			Glfw.SetMouseButtonCallback(glfwWindow, mouseButtonCallback);

			while (!Glfw.WindowShouldClose(glfwWindow))
			{
				// Poll for OS events and swap front/back buffers
				Glfw.WaitEvents();

				DrawAndUpdate(systemWindow);
			}
		}

		private void DrawAndUpdate(SystemWindow systemWindow)
		{
			Graphics2D graphics2D = new Graphics2DOpenGL((int)systemWindow.Width, (int)systemWindow.Height, GuiWidget.DeviceScale);
			graphics2D.PushTransform();
			systemWindow.OnDrawBackground(graphics2D);
			systemWindow.OnDraw(graphics2D);

			Glfw.SwapBuffers(glfwWindow);
		}

		private void sizeCallback(IntPtr window, int width, int height)
		{
			TopWindow.Size = new VectorMath.Vector2(width, height);
			GL.Viewport(0, 0, width, height); // Use all of the glControl painting area
			DrawAndUpdate(TopWindow);
		}

		private static double mouseX;
		private static double mouseY;
		private static MouseButtons mouseButton;
		private GlfwPlatformWindow applicationWindow;
		private Window glfwWindow;

		private void cursorPositionCallback(IntPtr window, double x, double y)
		{
			mouseX = x;
			mouseY = TopWindow.Height - y;
			TopWindow.OnMouseMove(new MouseEventArgs(mouseButton, 0, mouseX, mouseY, 0));
		}

		private Dictionary<MouseButton, long> lastMouseDownTime = new Dictionary<MouseButton, long>();
		private Dictionary<MouseButton, int> clickCount = new Dictionary<MouseButton, int>();

		private void mouseButtonCallback(IntPtr window, MouseButton button, InputState state, ModifierKeys modifiers)
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
				TopWindow.OnMouseDown(new MouseEventArgs(mouseButton, clickCount[button], mouseX, mouseY, 0));
			}
			else if (state == InputState.Release)
			{
				TopWindow.OnMouseUp(new MouseEventArgs(mouseButton, clickCount[button], mouseX, mouseY, 0));
			}
		}

		private void keyCallback(IntPtr windowIn, GLFW.Keys key, int scanCode, InputState state, ModifierKeys mods)
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

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			systemWindow.PlatformWindow.CloseSystemWindow(systemWindow);
		}
	}
}