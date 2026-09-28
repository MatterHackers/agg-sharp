//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

using System;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ <c>renderer_primitives</c>: aliased one-pixel Bresenham lines in <see cref="LineColor"/>, with
	/// coordinates in 1/256 pixel so a line starts and ends on the subpixel it was given, and pixel-aligned
	/// rectangles and ellipses outlined in <see cref="LineColor"/> and filled with <see cref="FillColor"/>, all
	/// at full cover.
	/// </summary>
	public class RendererPrimitives
	{
		private readonly IImageByte destination;
		private int currentX;
		private int currentY;

		/// <param name="destination">Where the pixels go; wrap it in an <see cref="ImageClippingProxy"/> to clip.</param>
		public RendererPrimitives(IImageByte destination)
		{
			this.destination = destination;
		}

		public Color LineColor { get; set; }

		public Color FillColor { get; set; }

		/// <summary>C++ <c>coord</c>: a pixel coordinate in the subpixel units the lines take.</summary>
		public static int Coord(double c)
		{
			return Util.iround(c * (int)line_bresenham_interpolator.subpixel_scale_e.subpixel_scale);
		}

		public void MoveTo(int x, int y)
		{
			this.currentX = x;
			this.currentY = y;
		}

		public void LineTo(int x, int y, bool last = false)
		{
			this.Line(this.currentX, this.currentY, x, y, last);
			this.currentX = x;
			this.currentY = y;
		}

		/// <summary>C++ <c>line</c>: the pixels from (x1, y1) up to, and only if <paramref name="last"/>, including (x2, y2).</summary>
		public void Line(int x1, int y1, int x2, int y2, bool last = false)
		{
			var li = new line_bresenham_interpolator(x1, y1, x2, y2);

			int len = li.len();
			if (len == 0)
			{
				if (last)
				{
					this.BlendPixel(line_bresenham_interpolator.line_lr(x1), line_bresenham_interpolator.line_lr(y1));
				}

				return;
			}

			if (last)
			{
				++len;
			}

			if (li.is_ver())
			{
				do
				{
					this.BlendPixel(li.x2(), li.y1());
					li.vstep();
				}
				while (--len != 0);
			}
			else
			{
				do
				{
					this.BlendPixel(li.x1(), li.y2());
					li.hstep();
				}
				while (--len != 0);
			}
		}

		/// <summary>C++ <c>rectangle</c>: the one-pixel outline of the box from (x1, y1) to (x2, y2), both corners included.</summary>
		public void Rectangle(int x1, int y1, int x2, int y2)
		{
			this.destination.blend_hline(x1, y1, x2 - 1, this.LineColor, 255);
			this.destination.blend_vline(x2, y1, y2 - 1, this.LineColor, 255);
			this.destination.blend_hline(x1 + 1, y2, x2, this.LineColor, 255);
			this.destination.blend_vline(x1, y1 + 1, y2, this.LineColor, 255);
		}

		/// <summary>C++ <c>solid_rectangle</c>: the box from (x1, y1) to (x2, y2), corners in any order, filled.</summary>
		public void SolidRectangle(int x1, int y1, int x2, int y2)
		{
			this.Bar(x1, y1, x2, y2, this.FillColor);
		}

		/// <summary>C++ <c>outlined_rectangle</c>: <see cref="Rectangle"/> with the inside filled.</summary>
		public void OutlinedRectangle(int x1, int y1, int x2, int y2)
		{
			this.Rectangle(x1, y1, x2, y2);
			this.Bar(x1 + 1, y1 + 1, x2 - 1, y2 - 1, this.FillColor);
		}

		/// <summary>C++ <c>ellipse</c>: the one-pixel outline of the ellipse of radii rx, ry centered on the pixel (x, y).</summary>
		public void Ellipse(int x, int y, int rx, int ry)
		{
			var ei = new ellipse_bresenham_interpolator(rx, ry);
			int dx = 0;
			int dy = -ry;
			do
			{
				dx += ei.dx();
				dy += ei.dy();
				this.BlendPixel(x + dx, y + dy, this.LineColor);
				this.BlendPixel(x + dx, y - dy, this.LineColor);
				this.BlendPixel(x - dx, y - dy, this.LineColor);
				this.BlendPixel(x - dx, y + dy, this.LineColor);
				ei.Next();
			}
			while (dy < 0);
		}

		/// <summary>C++ <c>solid_ellipse</c>: the ellipse of radii rx, ry centered on the pixel (x, y), filled.</summary>
		public void SolidEllipse(int x, int y, int rx, int ry)
		{
			var ei = new ellipse_bresenham_interpolator(rx, ry);
			int dx = 0;
			int dy = -ry;
			int dy0 = dy;
			int dx0 = dx;

			do
			{
				dx += ei.dx();
				dy += ei.dy();

				if (dy != dy0)
				{
					this.destination.blend_hline(x - dx0, y + dy0, x + dx0, this.FillColor, 255);
					this.destination.blend_hline(x - dx0, y - dy0, x + dx0, this.FillColor, 255);
				}

				dx0 = dx;
				dy0 = dy;
				ei.Next();
			}
			while (dy < 0);

			this.destination.blend_hline(x - dx0, y + dy0, x + dx0, this.FillColor, 255);
		}

		/// <summary>C++ <c>outlined_ellipse</c>: <see cref="Ellipse"/> with the inside filled.</summary>
		public void OutlinedEllipse(int x, int y, int rx, int ry)
		{
			var ei = new ellipse_bresenham_interpolator(rx, ry);
			int dx = 0;
			int dy = -ry;

			do
			{
				dx += ei.dx();
				dy += ei.dy();

				this.BlendPixel(x + dx, y + dy, this.LineColor);
				this.BlendPixel(x + dx, y - dy, this.LineColor);
				this.BlendPixel(x - dx, y - dy, this.LineColor);
				this.BlendPixel(x - dx, y + dy, this.LineColor);

				if (ei.dy() != 0 && dx != 0)
				{
					this.destination.blend_hline(x - dx + 1, y + dy, x + dx - 1, this.FillColor, 255);
					this.destination.blend_hline(x - dx + 1, y - dy, x + dx - 1, this.FillColor, 255);
				}

				ei.Next();
			}
			while (dy < 0);
		}

		private void BlendPixel(int x, int y, Color color)
		{
			this.destination.blend_hline(x, y, x, color, 255);
		}

		private void BlendPixel(int x, int y)
		{
			this.BlendPixel(x, y, this.LineColor);
		}

		/// <summary>
		/// C++ <c>renderer_base::blend_bar</c>: a filled box, one hline per row, cut to the clip box first. Cut to
		/// a proxy's bounding clip box, not its clip box: an <see cref="ImageMultiClipProxy"/> then clips each
		/// hline to every one of its boxes, as renderer_mclip::blend_bar does box by box. An image that is not a
		/// proxy is cut to its own bounds, so a bar never writes outside it.
		/// </summary>
		private void Bar(int x1, int y1, int x2, int y2, Color color)
		{
			RectangleInt clip = this.destination is ImageClippingProxy proxy
				? proxy.bounding_clip_box()
				: new RectangleInt(0, 0, this.destination.Width - 1, this.destination.Height - 1);
			int left = Math.Max(Math.Min(x1, x2), clip.Left);
			int right = Math.Min(Math.Max(x1, x2), clip.Right);
			int bottom = Math.Max(Math.Min(y1, y2), clip.Bottom);
			int top = Math.Min(Math.Max(y1, y2), clip.Top);
			if (left <= right && bottom <= top)
			{
				for (int y = bottom; y <= top; y++)
				{
					this.destination.blend_hline(left, y, right, color, 255);
				}
			}
		}
	}

	/// <summary>
	/// C++ <c>rasterizer_outline</c>: feeds a path's vertices to a <see cref="RendererPrimitives"/> as aliased lines,
	/// closing a closed polygon of more than two vertices back to its start.
	/// </summary>
	public class RasterizerOutline
	{
		private readonly RendererPrimitives renderer;
		private int startX;
		private int startY;
		private int vertices;

		public RasterizerOutline(RendererPrimitives renderer)
		{
			this.renderer = renderer;
		}

		public void MoveTo(int x, int y)
		{
			this.vertices = 1;
			this.startX = x;
			this.startY = y;
			this.renderer.MoveTo(x, y);
		}

		public void LineTo(int x, int y)
		{
			++this.vertices;
			this.renderer.LineTo(x, y);
		}

		public void Close()
		{
			if (this.vertices > 2)
			{
				this.LineTo(this.startX, this.startY);
			}

			this.vertices = 0;
		}

		public void AddVertex(double x, double y, FlagsAndCommand command)
		{
			if (ShapePath.IsMoveTo(command))
			{
				this.MoveTo(RendererPrimitives.Coord(x), RendererPrimitives.Coord(y));
			}
			else if (ShapePath.is_end_poly(command))
			{
				if (ShapePath.is_closed(command))
				{
					this.Close();
				}
			}
			else
			{
				this.LineTo(RendererPrimitives.Coord(x), RendererPrimitives.Coord(y));
			}
		}

		public void AddPath(IVertexSource vertexSource)
		{
			FlagsAndCommand command;
			vertexSource.Rewind(0);
			while (!ShapePath.IsStop(command = vertexSource.Vertex(out double x, out double y)))
			{
				this.AddVertex(x, y, command);
			}
		}
	}
}
