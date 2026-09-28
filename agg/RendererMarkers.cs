//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>C++ AGG's marker_e: the shapes <see cref="RendererMarkers"/> draws.</summary>
	public enum MarkerType
	{
		Square,
		Diamond,
		Circle,
		CrossedCircle,
		SemiellipseLeft,
		SemiellipseRight,
		SemiellipseUp,
		SemiellipseDown,
		TriangleLeft,
		TriangleRight,
		TriangleUp,
		TriangleDown,
		FourRays,
		Cross,
		X,
		Dash,
		Dot,
		Pixel,
		EndOfMarkers,
	}

	/// <summary>
	/// C++ AGG's renderer_markers, with the parts of renderer_primitives it uses: aliased Bresenham lines and
	/// small pixel-aligned markers, outlined in <see cref="LineColor"/> and filled with <see cref="FillColor"/>,
	/// drawn straight into a clipped image one pixel or span at a time.
	/// </summary>
	public class RendererMarkers
	{
		private readonly ImageClippingProxy ren;

		// C++ renderer_markers derives from renderer_primitives; the lines, the square's box and the ellipses
		// are its shapes.
		private readonly RendererPrimitives primitives;

		public RendererMarkers(ImageClippingProxy ren)
		{
			this.ren = ren;
			this.primitives = new RendererPrimitives(ren);
		}

		public Color FillColor
		{
			get => primitives.FillColor;
			set => primitives.FillColor = value;
		}

		public Color LineColor
		{
			get => primitives.LineColor;
			set => primitives.LineColor = value;
		}

		/// <summary>renderer_primitives::coord: a pixel coordinate in the 24.8 subpixels <see cref="Line"/> takes.</summary>
		public static int Coord(double c) => RendererPrimitives.Coord(c);

		/// <summary>renderer_primitives::line: a Bresenham line between subpixel points, the last pixel only if asked.</summary>
		public void Line(int x1, int y1, int x2, int y2, bool last = false) => primitives.Line(x1, y1, x2, y2, last);

		/// <summary>renderer_markers::marker: one marker of radius r centered on the pixel (x, y).</summary>
		public void Marker(int x, int y, int r, MarkerType type)
		{
			switch (type)
			{
				case MarkerType.Square: Square(x, y, r); break;
				case MarkerType.Diamond: Diamond(x, y, r); break;
				case MarkerType.Circle: Circle(x, y, r); break;
				case MarkerType.CrossedCircle: CrossedCircle(x, y, r); break;
				case MarkerType.SemiellipseLeft: SemiellipseHorizontal(x, y, r, 1); break;
				case MarkerType.SemiellipseRight: SemiellipseHorizontal(x, y, r, -1); break;
				case MarkerType.SemiellipseUp: SemiellipseVertical(x, y, r, -1); break;
				case MarkerType.SemiellipseDown: SemiellipseVertical(x, y, r, 1); break;
				case MarkerType.TriangleLeft: TriangleHorizontal(x, y, r, 1); break;
				case MarkerType.TriangleRight: TriangleHorizontal(x, y, r, -1); break;
				case MarkerType.TriangleUp: TriangleVertical(x, y, r, -1); break;
				case MarkerType.TriangleDown: TriangleVertical(x, y, r, 1); break;
				case MarkerType.FourRays: FourRays(x, y, r); break;
				case MarkerType.Cross: Cross(x, y, r); break;
				case MarkerType.X: Xing(x, y, r); break;
				case MarkerType.Dash: Dash(x, y, r); break;
				case MarkerType.Dot: Dot(x, y, r); break;
				case MarkerType.Pixel: Pixel(x, y, FillColor); break;
			}
		}

		/// <summary>
		/// Whether the marker's box reaches the clip box. C++ builds the box's right edge as x+y where x+r is
		/// meant, which drops a marker left of the clip box whose right half reaches into it when y is small;
		/// this uses x+r (tools/cpp-renderer/patches/agg_renderer_markers.h fixes the reference the same way).
		/// </summary>
		private bool Visible(int x, int y, int r)
		{
			RectangleInt clip = ren.bounding_clip_box();
			return Math.Max(x - r, clip.Left) <= Math.Min(x + r, clip.Right)
				&& Math.Max(y - r, clip.Bottom) <= Math.Min(y + r, clip.Top);
		}

		private void Pixel(int x, int y, Color color) => ren.BlendPixel(x, y, color, 255);

		private void Hline(int x1, int y, int x2, Color color) => ren.blend_hline(x1, y, x2, color, 255);

		private void Vline(int x, int y1, int y2, Color color) => ren.blend_vline(x, y1, y2, color, 255);

		private void Square(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					primitives.OutlinedRectangle(x - r, y - r, x + r, y + r);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		private void Diamond(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					int dy = -r;
					int dx = 0;
					do
					{
						Pixel(x - dx, y + dy, LineColor);
						Pixel(x + dx, y + dy, LineColor);
						Pixel(x - dx, y - dy, LineColor);
						Pixel(x + dx, y - dy, LineColor);

						if (dx != 0)
						{
							Hline(x - dx + 1, y + dy, x + dx - 1, FillColor);
							Hline(x - dx + 1, y - dy, x + dx - 1, FillColor);
						}

						++dy;
						++dx;
					}
					while (dy <= 0);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		private void Circle(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					primitives.OutlinedEllipse(x, y, r, r);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		private void CrossedCircle(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					primitives.OutlinedEllipse(x, y, r, r);
					int r6 = r + (r >> 1);
					if (r <= 2)
					{
						r6++;
					}

					r >>= 1;
					Hline(x - r6, y, x - r, LineColor);
					Hline(x + r, y, x + r6, LineColor);
					Vline(x, y - r6, y - r, LineColor);
					Vline(x, y + r, y + r6, LineColor);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		/// <summary>semiellipse_left (side 1) and semiellipse_right (side -1), which C++ writes out as mirrored copies.</summary>
		private void SemiellipseHorizontal(int x, int y, int r, int side)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					int r8 = r * 4 / 5;
					int dy = -r;
					int dx = 0;
					var ei = new ellipse_bresenham_interpolator(r * 3 / 5, r + r8);
					do
					{
						dx += ei.dx();
						dy += ei.dy();

						Pixel(x + (side * dy), y + dx, LineColor);
						Pixel(x + (side * dy), y - dx, LineColor);

						if (ei.dy() != 0 && dx != 0)
						{
							Vline(x + (side * dy), y - dx + 1, y + dx - 1, FillColor);
						}

						ei.Next();
					}
					while (dy < r8);
					Vline(x + (side * dy), y - dx, y + dx, LineColor);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		/// <summary>semiellipse_up (side -1) and semiellipse_down (side 1).</summary>
		private void SemiellipseVertical(int x, int y, int r, int side)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					int r8 = r * 4 / 5;
					int dy = -r;
					int dx = 0;
					var ei = new ellipse_bresenham_interpolator(r * 3 / 5, r + r8);
					do
					{
						dx += ei.dx();
						dy += ei.dy();

						Pixel(x + dx, y + (side * dy), LineColor);
						Pixel(x - dx, y + (side * dy), LineColor);

						if (ei.dy() != 0 && dx != 0)
						{
							Hline(x - dx + 1, y + (side * dy), x + dx - 1, FillColor);
						}

						ei.Next();
					}
					while (dy < r8);

					// Unlike left and right, C++ closes up and down one row further out: y-dy-1 and y+dy+1.
					Hline(x - dx, y + (side * (dy + 1)), x + dx, LineColor);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		/// <summary>triangle_left (side 1) and triangle_right (side -1).</summary>
		private void TriangleHorizontal(int x, int y, int r, int side)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					int dy = -r;
					int dx = 0;
					int flip = 0;
					int r6 = r * 3 / 5;
					do
					{
						Pixel(x + (side * dy), y - dx, LineColor);
						Pixel(x + (side * dy), y + dx, LineColor);

						if (dx != 0)
						{
							Vline(x + (side * dy), y - dx + 1, y + dx - 1, FillColor);
						}

						++dy;
						dx += flip;
						flip ^= 1;
					}
					while (dy < r6);
					Vline(x + (side * dy), y - dx, y + dx, LineColor);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		/// <summary>triangle_up (side -1) and triangle_down (side 1).</summary>
		private void TriangleVertical(int x, int y, int r, int side)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					int dy = -r;
					int dx = 0;
					int flip = 0;
					int r6 = r * 3 / 5;
					do
					{
						Pixel(x - dx, y + (side * dy), LineColor);
						Pixel(x + dx, y + (side * dy), LineColor);

						if (dx != 0)
						{
							Hline(x - dx + 1, y + (side * dy), x + dx - 1, FillColor);
						}

						++dy;
						dx += flip;
						flip ^= 1;
					}
					while (dy < r6);
					Hline(x - dx, y + (side * dy), x + dx, LineColor);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		private void FourRays(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					int dy = -r;
					int dx = 0;
					int flip = 0;
					int r3 = -(r / 3);
					do
					{
						Pixel(x - dx, y + dy, LineColor);
						Pixel(x + dx, y + dy, LineColor);
						Pixel(x - dx, y - dy, LineColor);
						Pixel(x + dx, y - dy, LineColor);
						Pixel(x + dy, y - dx, LineColor);
						Pixel(x + dy, y + dx, LineColor);
						Pixel(x - dy, y - dx, LineColor);
						Pixel(x - dy, y + dx, LineColor);

						if (dx != 0)
						{
							Hline(x - dx + 1, y + dy, x + dx - 1, FillColor);
							Hline(x - dx + 1, y - dy, x + dx - 1, FillColor);
							Vline(x + dy, y - dx + 1, y + dx - 1, FillColor);
							Vline(x - dy, y - dx + 1, y + dx - 1, FillColor);
						}

						++dy;
						dx += flip;
						flip ^= 1;
					}
					while (dy <= r3);

					primitives.SolidRectangle(x + r3 + 1, y + r3 + 1, x - r3 - 1, y - r3 - 1);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		private void Cross(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					Vline(x, y - r, y + r, LineColor);
					Hline(x - r, y, x + r, LineColor);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		private void Xing(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					int dy = -r * 7 / 10;
					do
					{
						Pixel(x + dy, y + dy, LineColor);
						Pixel(x - dy, y + dy, LineColor);
						Pixel(x + dy, y - dy, LineColor);
						Pixel(x - dy, y - dy, LineColor);
						++dy;
					}
					while (dy < 0);
				}

				Pixel(x, y, FillColor);
			}
		}

		private void Dash(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					Hline(x - r, y, x + r, LineColor);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}

		private void Dot(int x, int y, int r)
		{
			if (Visible(x, y, r))
			{
				if (r != 0)
				{
					primitives.SolidEllipse(x, y, r, r);
				}
				else
				{
					Pixel(x, y, FillColor);
				}
			}
		}
	}
}
