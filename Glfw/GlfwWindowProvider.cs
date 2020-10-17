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
using System.Runtime.InteropServices;
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

		public SystemWindow TopWindow { get; }

		private bool viewPortHasBeenSet = false;
		private NativeWindow nativeWindow;

		private void SetAndClearViewPort()
		{
			// Reset The Current Viewport
			GL.Viewport(0, 0, nativeWindow.ClientBounds.Width, nativeWindow.ClientBounds.Height);
			viewPortHasBeenSet = true;

			// The following lines set the screen up for a perspective view. Meaning things in the distance get smaller.
			// This creates a realistic looking scene.
			// The perspective is calculated with a 45 degree viewing angle based on the windows width and height.
			// The 0.1f, 100.0f is the starting point and ending point for how deep we can draw into the screen.

			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();

			GL.MatrixMode(MatrixMode.Modelview);
			GL.LoadIdentity();
			GL.Scissor(0, 0, nativeWindow.ClientBounds.Width, nativeWindow.ClientBounds.Height);

			NewGraphics2D().Clear(new ColorF(1, 1, 1, 1));
		}

		public Graphics2D NewGraphics2D()
		{
			if (!viewPortHasBeenSet)
			{
				SetAndClearViewPort();
			}

			Graphics2D graphics2D = new Graphics2DOpenGL(nativeWindow.ClientBounds.Width, nativeWindow.ClientBounds.Height, GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		private static Random rand = new Random();

		private const string TITLE = "Simple Window";
		private const int WIDTH = 1024;
		private const int HEIGHT = 800;

		private const int GL_COLOR_BUFFER_BIT = 0x00004000;

		private delegate void glClearColorHandler(float r, float g, float b, float a);
		private static glClearColorHandler glClearColor;

		private delegate void glClearHandler(int mask);
		private static glClearHandler glClear;

		private static void ChangeRandomColor()
		{
			var r = (float)rand.NextDouble();
			var g = (float)rand.NextDouble();
			var b = (float)rand.NextDouble();
			glClearColor(r, g, b, 1.0f);
		}

		// Creates or connects a PlatformWindow to the given SystemWindow
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			var glfwGl = new GlfwGL();
			MatterHackers.RenderOpenGl.OpenGl.GL.Instance = glfwGl;

			// Create window
			var window = Glfw.CreateWindow(WIDTH, HEIGHT, TITLE, Monitor.None, Window.None);
			Glfw.MakeContextCurrent(window);
			OpenGL.Gl.Import(Glfw.GetProcAddress);

			// Effectively enables VSYNC by setting to 1.
			Glfw.SwapInterval(1);

			// Find center position based on window and monitor sizes
			var screenSize = Glfw.PrimaryMonitor.WorkArea;
			var x = (screenSize.Width - WIDTH) / 2;
			var y = (screenSize.Height - HEIGHT) / 2;
			Glfw.SetWindowPosition(window, x, y);

			// Set a key callback
			Glfw.SetKeyCallback(window, keyCallBack);


			glClearColor = Marshal.GetDelegateForFunctionPointer<glClearColorHandler>(Glfw.GetProcAddress("glClearColor"));
			glClear = Marshal.GetDelegateForFunctionPointer<glClearHandler>(Glfw.GetProcAddress("glClear"));


			var tick = 0L;
			ChangeRandomColor();

			while (!Glfw.WindowShouldClose(window))
			{
				// Poll for OS events and swap front/back buffers
				Glfw.PollEvents();

				Graphics2D graphics2D = new Graphics2DOpenGL(1024, 800, GuiWidget.DeviceScale);
				graphics2D.PushTransform();
				systemWindow.OnDraw(graphics2D);

				Glfw.SwapBuffers(window);

				// Change background color to something random every 60 draws
				//if (tick++ % 60 == 0)
				ChangeRandomColor();

				// Clear the buffer to the set color
				glClear(GL_COLOR_BUFFER_BIT);
			}
#if false
			using (nativeWindow = new NativeWindow(800, 600, "MyWindowTitle"))
			{
				// Main application loop
				while (!nativeWindow.IsClosing)
				{
					// OpenGL rendering
					// Implement any timing for flow control, etc (see Glfw.GetTime())
					nativeWindow.MakeCurrent();

					systemWindow.OnDraw(NewGraphics2D());

					// Swap the front/back buffers
					nativeWindow.SwapBuffers();

					// Poll native operating system events (must be called or OS will think application is hanging)
					Glfw.PollEvents();
			}
#endif
		}

		private void keyCallBack(IntPtr windowIn, GLFW.Keys key, int scanCode, InputState state, ModifierKeys mods)
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

		/*
		if (_graphicsDevice == null)
		{

			WindowCreateInfo windowCI = new WindowCreateInfo()
			{
				X = 100,
				Y = 100,
				WindowWidth = 960,
				WindowHeight = 540,
				WindowTitle = "Glfw Tutorial",
			};

			Sdl2Window window = GlfwStartup.CreateWindow(ref windowCI);

			GlfwPlatformWindow = new GlfwSystemWindow(this);

			systemWindow.PlatformWindow = GlfwPlatformWindow;

			_graphicsDevice = GlfwStartup.CreateGraphicsDevice(window, GraphicsBackend.OpenGL);

			window.KeyDown += (KeyEvent keyEvent) =>
			{
				systemWindow.OnKeyDown(
					new KeyEventArgs((Keys)keyEvent.Key));
			};

			window.KeyUp += (KeyEvent keyEvent) =>
			{
				systemWindow.OnKeyUp(
					new KeyEventArgs((Keys)keyEvent.Key));
			};

			long runNextMs = 0;

			VectorMath.Vector2 lastPosition = VectorMath.Vector2.Zero;
			while (window.Exists)
			{
				InputSnapshot inputSnapshot = window.PumpEvents();

				var position = new VectorMath.Vector2(inputSnapshot.MousePosition.X, window.Height - inputSnapshot.MousePosition.Y);

				if (lastPosition != position)
				{
					systemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, position.X, position.Y, 0));
				}

				if (inputSnapshot.WheelDelta != 0)
				{
					systemWindow.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, position.X, position.Y, (int)inputSnapshot.WheelDelta * 120));
				}

				if (runNextMs <= UiThread.CurrentTimerMs)
				{
					UiThread.InvokePendingActions();

					runNextMs = UiThread.CurrentTimerMs + 10;
				}

				foreach (var mouseEvent in inputSnapshot.MouseEvents)
				{
					MouseButtons buttons = MapMouseButtons(mouseEvent.MouseButton);
					if (inputSnapshot.IsMouseDown(mouseEvent.MouseButton))
					{
						systemWindow.OnMouseDown(new MouseEventArgs(buttons, 1, position.X, position.Y, 0));
					}
					else
					{
						systemWindow.OnMouseUp(new MouseEventArgs(buttons, 0, position.X, position.Y, 0));
					}


				}

				systemWindow.Width = GlfwPlatformWindow.Width = window.Width;
				systemWindow.Height = GlfwPlatformWindow.Height = window.Height;

				var graphics2D = GlfwPlatformWindow.NewGraphics2D();

				// We must call on draw background as this is effectively our child and that is the way it is done in GuiWidget.
				// Parents call child OnDrawBackground before they call OnDraw
				systemWindow.OnDrawBackground(graphics2D);
				systemWindow.OnDraw(graphics2D);

				_graphicsDevice.SwapBuffers();

				// Copy to screen/backbuffer

				//window.PumpEvents();
			}

			// MyOpenGLView.RootGLView.ShowSystemWindow(systemWindow);
		*/

		private MouseButtons MapMouseButtons(MouseButton mouseButton)
		{
			switch (mouseButton)
			{
				case MouseButton.Left:
					return MouseButtons.Left;
				case MouseButton.Middle:
					break;
				case MouseButton.Right:
					break;
				/*					case MouseButton.Button1:
										break;
									case MouseButton.Button2:
										break;
									case MouseButton.Button3:
										break;
				*/
				case MouseButton.Button4:
					break;
				case MouseButton.Button5:
					break;
				case MouseButton.Button6:
					break;
				case MouseButton.Button7:
					break;
				case MouseButton.Button8:
					break;
					/*
										case MouseButton.Button9:
											break;
										case MouseButton.LastButton:
											break;
					*/
			}

			return MouseButtons.None;
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			systemWindow.PlatformWindow.CloseSystemWindow(systemWindow);
		}
	}
}