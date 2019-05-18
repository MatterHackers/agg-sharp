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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public interface IStyleHandler
	{
		bool is_solid(int style);

		Color color(int style);

		void generate_span(Color[] span, int spanIndex, int x, int y, int len, int style);
	};

	public abstract class Graphics2D
	{
		private const int cover_full = 255;
		protected IImageByte destImageByte;
		protected IImageFloat destImageFloat;
		protected Stroke StrockedText;
		protected Stack<Affine> affineTransformStack = new Stack<Affine>();
		protected ScanlineRasterizer rasterizer;

		public enum TransformQuality
		{
			Fastest,
			Best
		}

		public TransformQuality ImageRenderQuality { get; set; } = TransformQuality.Fastest;

		public Graphics2D()
		{
			affineTransformStack.Push(Affine.NewIdentity());
		}

		public Graphics2D(IImageByte destImage, ScanlineRasterizer rasterizer)
			: this()
		{
			Initialize(destImage, rasterizer);
		}

		public void Initialize(IImageByte destImage, ScanlineRasterizer rasterizer)
		{
			destImageByte = destImage;
			destImageFloat = null;
			this.rasterizer = rasterizer;
		}

		public void Initialize(IImageFloat destImage, ScanlineRasterizer rasterizer)
		{
			destImageByte = null;
			destImageFloat = destImage;
			this.rasterizer = rasterizer;
		}

		public int TransformStackCount
		{
			get { return affineTransformStack.Count; }
		}

		/// <summary>
		/// Draws an arc representing a portion of an ellipse specified by a Rectangle structure.
		/// </summary>
		/// <param name="color">The color to draw in.</param>
		/// <param name="rect">Structure that defines the boundaries of the ellipse.</param>
		/// <param name="startAngle">Angle in degrees measured clockwise from the x-axis to the starting point of the arc.</param>
		/// <param name="sweepAngle">Angle in degrees measured clockwise from the startAngle parameter to ending point of the arc.</param>
		public void DrawArc(Color color, RectangleDouble rect, int startAngle, int sweepAngle)
		{
			throw new NotImplementedException();
		}

		public Affine PopTransform()
		{
			if (affineTransformStack.Count == 1)
			{
				throw new System.Exception("You cannot remove the last transform from the stack.");
			}

			return affineTransformStack.Pop();
		}

		public void PushTransform()
		{
			if (affineTransformStack.Count > 1000)
			{
				throw new System.Exception("You seem to be leaking transforms.  You should be popping some of them at some point.");
			}

			affineTransformStack.Push(affineTransformStack.Peek());
		}

		public Affine GetTransform()
		{
			return affineTransformStack.Peek();
		}

		public void SetTransform(Affine value)
		{
			affineTransformStack.Pop();
			affineTransformStack.Push(value);
		}

		public void DrawLine(Color color, Vector2 start, Vector2 end)
		{
			Line(start, end, color);
		}

		public ScanlineRasterizer Rasterizer
		{
			get { return rasterizer; }
		}

		public abstract IScanlineCache ScanlineCache
		{
			get;
			set;
		}

		public IImageByte DestImage
		{
			get
			{
				return destImageByte;
			}
		}

		public IImageFloat DestImageFloat
		{
			get
			{
				return destImageFloat;
			}
		}

		public abstract void Render(IVertexSource vertexSource, IColorType colorType);

		public void Render(IImageByte imageSource, Point2D position)
		{
			Render(imageSource, position.x, position.y);
		}

		public void Render(IImageByte imageSource, Vector2 position)
		{
			Render(imageSource, position.X, position.Y);
		}

		public void Render(IImageByte imageSource, Vector2 position, double width, double height)
		{
			Render(imageSource, position.X, position.Y, width, height);
		}

		public void Render(IImageByte imageSource, double x, double y)
		{
			Render(imageSource, x, y, 0, 1, 1);
		}

		public void Render(IImageByte imageSource, double x, double y, double width, double height)
		{
			Render(imageSource, x, y, 0, width / imageSource.Width, height / imageSource.Height);
		}

		public abstract void Render(IImageByte imageSource,
			double x,
			double y,
			double angleRadians,
			double scaleX,
			double scaleY);

		public abstract void Render(IImageFloat imageSource,
			double x,
			double y,
			double angleRadians,
			double scaleX,
			double scaleY);

		public void Render(IVertexSource vertexSource, double x, double y, IColorType color)
		{
			Render(new VertexSourceApplyTransform(vertexSource, Affine.NewTranslation(x, y)), color);
		}

		public void Render(IVertexSource vertexSource, Vector2 position, IColorType color)
		{
			Render(new VertexSourceApplyTransform(vertexSource, Affine.NewTranslation(position.X, position.Y)), color);
		}

		public abstract void Clear(IColorType color);

		public abstract int Width { get; }

		public abstract int Height { get; }

		public void DrawString(string text,
			Vector2 position,
			double pointSize = 12,
			Justification justification = Justification.Left,
			Baseline baseline = Baseline.Text,
			Color color = default(Color),
			bool drawFromHintedCach = false,
			Color backgroundColor = default(Color),
			bool bold = false)
		{
			DrawString(text, position.X, position.Y, pointSize, justification, baseline, color, drawFromHintedCach, backgroundColor, bold);
		}

		public void DrawString(string text,
			double x,
			double y,
			double pointSize = 12,
			Justification justification = Justification.Left,
			Baseline baseline = Baseline.Text,
			Color color = default(Color),
			bool drawFromHintedCach = false,
			Color backgroundColor = default(Color),
			bool bold = false)
		{
			TypeFacePrinter stringPrinter = new TypeFacePrinter(text, pointSize, new Vector2(x, y), justification, baseline, bold);
			if (color.Alpha0To255 == 0)
			{
				color = Color.Black;
			}

			if (backgroundColor.Alpha0To255 != 0)
			{
				FillRectangle(stringPrinter.LocalBounds, backgroundColor);
			}

			stringPrinter.DrawFromHintedCache = drawFromHintedCach;
			stringPrinter.Render(this, color);
		}

		public void Circle(Vector2 origin, double radius, Color color)
		{
			Circle(origin.X, origin.Y, radius, color);
		}

		public void Circle(double x, double y, double radius, Color color)
		{
			Ellipse elipse = new Ellipse(x, y, radius, radius);
			Render(elipse, color);
		}

		public void Line(Vector2 start, Vector2 end, Color color, double strokeWidth = 1)
		{
			Line(start.X, start.Y, end.X, end.Y, color, strokeWidth);
		}

		public virtual void Line(double x1, double y1, double x2, double y2, Color color, double strokeWidth = 1)
		{
			var lineToDraw = new VertexStorage();
			lineToDraw.remove_all();
			lineToDraw.MoveTo(x1, y1);
			lineToDraw.LineTo(x2, y2);

			this.Render(
				new Stroke(lineToDraw, strokeWidth),
				color);
		}

		public abstract void SetClippingRect(RectangleDouble rect_d);

		public abstract RectangleDouble GetClippingRect();

		public abstract void Rectangle(double left, double bottom, double right, double top, Color color, double strokeWidth = 1);

		public void Rectangle(RectangleDouble rect, Color color, double strokeWidth = 1)
		{
			Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, color, strokeWidth);
		}

		public void Rectangle(RectangleInt rect, Color color)
		{
			Rectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, color);
		}

		public void FillRectangle(RectangleDouble rect, IColorType fillColor)
		{
			FillRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, fillColor);
		}

		public void FillRectangle(RectangleInt rect, IColorType fillColor)
		{
			FillRectangle(rect.Left, rect.Bottom, rect.Right, rect.Top, fillColor);
		}

		public void FillRectangle(Vector2 leftBottom, Vector2 rightTop, IColorType fillColor)
		{
			FillRectangle(leftBottom.X, leftBottom.Y, rightTop.X, rightTop.Y, fillColor);
		}

		public abstract void FillRectangle(double left, double bottom, double right, double top, IColorType fillColor);

		public static void AssertDebugNotDefined()
		{
#if DEBUG
			throw new Exception("DEBUG is defined and should not be!");
#endif
		}
	}
}