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
using Veldrid;
using Veldrid.Sdl2;
using Veldrid.StartupUtilities;
using MatterHackers.RenderOpenGl;
using MatterHackers.Agg.UI;
using System.Collections.Generic;

namespace MatterHackers.VeldridProvider
{
	public class VeldridWindowProvider : ISystemWindowProvider
	{
		public GraphicsDevice _graphicsDevice;

		private VeldridSystemWindow veldridPlatformWindow;

		public IReadOnlyList<SystemWindow> OpenWindows { get; }

		public SystemWindow TopWindow { get; }

		/// <summary>
		/// Creates or connects a PlatformWindow to the given SystemWindow
		/// </summary>
		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			if (_graphicsDevice == null)
			{

				WindowCreateInfo windowCI = new WindowCreateInfo()
				{
					X = 100,
					Y = 100,
					WindowWidth = 960,
					WindowHeight = 540,
					WindowTitle = "Veldrid Tutorial",
				};

				Sdl2Window window = VeldridStartup.CreateWindow(ref windowCI);

				veldridPlatformWindow = new VeldridSystemWindow(this);

				systemWindow.PlatformWindow = veldridPlatformWindow;

				_graphicsDevice = VeldridStartup.CreateGraphicsDevice(window, GraphicsBackend.OpenGL);

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

				// setup our veldrid gl immediate mode emulator
				var veldridGl = new VeldridGL();
				MatterHackers.RenderOpenGl.OpenGl.GL.Instance = veldridGl;
				veldridGl.CreateResources(_graphicsDevice);

				ShaderData.Instance.CreateResources(_graphicsDevice);

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

					systemWindow.Width = veldridPlatformWindow.Width = window.Width;
					systemWindow.Height = veldridPlatformWindow.Height = window.Height;

					var graphics2D = veldridPlatformWindow.NewGraphics2D();

					// We must call on draw background as this is effectively our child and that is the way it is done in GuiWidget.
					// Parents call child OnDrawBackground before they call OnDraw
					systemWindow.OnDrawBackground(graphics2D);
					systemWindow.OnDraw(graphics2D);

					_graphicsDevice.SwapBuffers();



					// Copy to screen/backbuffer

					//window.PumpEvents();
				}

				// MyOpenGLView.RootGLView.ShowSystemWindow(systemWindow);
				veldridGl.DisposeResources();
				ShaderData.Instance.DisposeResources();
			}

			MouseButtons MapMouseButtons(MouseButton mouseButton)
			{
				switch (mouseButton)
				{
					case MouseButton.Left:
						return MouseButtons.Left;
					case MouseButton.Middle:
						break;
					case MouseButton.Right:
						break;
					case MouseButton.Button1:
						break;
					case MouseButton.Button2:
						break;
					case MouseButton.Button3:
						break;
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
					case MouseButton.Button9:
						break;
					case MouseButton.LastButton:
						break;
				}

				return MouseButtons.None;
			}

		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			systemWindow.PlatformWindow.CloseSystemWindow(systemWindow);
		}
	}
}

