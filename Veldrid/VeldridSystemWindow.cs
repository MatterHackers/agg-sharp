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
using MatterHackers.VectorMath;
using MatterHackers.Agg.UI;
using MatterHackers.Agg;
using MatterHackers.RenderOpenGl;

namespace MatterHackers.VeldridProvider
{
	public class VeldridSystemWindow : IPlatformWindow
	{
		private VeldridWindowProvider windowProvider;
		public int Width;
		public int Height;

		public VeldridSystemWindow(VeldridWindowProvider windowProvider)
		{
			this.windowProvider = windowProvider;
		}

		public string Caption { get; set; }

		public int TitleBarHeight => 45;

		public Point2D DesktopPosition { get; set; }
		public Vector2 MinimumSize { get; set; }

		public Keys ModifierKeys => Keys.None;

		public void BringToFront()
		{
			//throw new NotImplementedException();
		}

		public void Close()
		{
			//throw new NotImplementedException();
		}

		public void CloseSystemWindow(SystemWindow systemWindow)
		{
			//throw new NotImplementedException();
		}

		public void Invalidate(RectangleDouble rectToInvalidate)
		{
			//throw new NotImplementedException();
		}

		public Graphics2D NewGraphics2D()
		{
			Graphics2D graphics2D = new Graphics2DVeldrid(this.Width, this.Height)
			{
				WindowProvider = windowProvider
			};

			// this is for testing the open gl implementation
			graphics2D = new Graphics2DOpenGL(this.Width, this.Height, GuiWidget.DeviceScale);
			graphics2D.PushTransform();

			return graphics2D;
		}

		public void SetCursor(Cursors cursorToSet)
		{
			// windowProvider.window.
			// throw new NotImplementedException();
		}

		public void ShowSystemWindow(SystemWindow systemWindow)
		{
			//throw new NotImplementedException();
		}
	}
}
