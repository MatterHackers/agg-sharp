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
using GLFW;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.RenderOpenGl;
using MatterHackers.RenderOpenGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.GlfwProvider
{
	public class GlfwSystemWindow : IPlatformWindow
	{
		private Window glfwWindow;
		private GlfwWindowProvider windowProvider;
		public int Width;
		public int Height;

		public GlfwSystemWindow(GlfwWindowProvider windowProvider, Window window)
		{
			this.glfwWindow = window;
			this.windowProvider = windowProvider;
		}

		private string _title = "";

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

		public int TitleBarHeight => 45;

		public Point2D DesktopPosition { get; set; }

		public Vector2 MinimumSize { get; set; }

		public Agg.UI.Keys ModifierKeys => Agg.UI.Keys.None;

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
			// throw new NotImplementedException();
		}

		public Graphics2D NewGraphics2D()
		{
			SetupViewport();

			Graphics2D graphics2D = new Graphics2DOpenGL(this.Width, this.Height, 1);

			// this is for testing the open gl implementation
			graphics2D = new Graphics2DOpenGL(this.Width, this.Height, GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		private void SetupViewport()
		{
			// If this throws an assert, you are calling MakeCurrent() before the glControl is done being constructed.
			// Call this function you have called Show().
			int w = Width;
			int h = Height;
			GL.MatrixMode(MatrixMode.Projection);
			GL.LoadIdentity();
			GL.Ortho(0, w, 0, h, -1, 1); // Bottom-left corner pixel has coordinate (0, 0)
			GL.Viewport(0, 0, w, h); // Use all of the glControl painting area
		}

		public void SetCursor(Cursors cursorToSet)
		{
			// windowProvider.window.
			// throw new NotImplementedException();
		}

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			// throw new NotImplementedException();
		}
	}
}
