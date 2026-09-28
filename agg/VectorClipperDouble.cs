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
//----------------------------------------------------------------------------

using poly_subpixel_scale_e = MatterHackers.Agg.Util.poly_subpixel_scale_e;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's <c>rasterizer_sl_clip_dbl</c>: the clipper working in pixel doubles, rounding to 24.8
	/// subpixels only as each clipped line reaches the cells. The integer <see cref="VectorClipper"/> rounds
	/// the path's points first and finds where an edge crosses the clip box in rounded coordinates, so where a
	/// path crosses the box the two cut it a subpixel apart. Give it to a rasterizer to match C++ code that
	/// templates the rasterizer on <c>rasterizer_sl_clip_dbl</c>.
	/// </summary>
	public class VectorClipperDouble : VectorClipper
	{
		private RectangleDouble box;
		private double m_x1;
		private double m_y1;
		private int m_f1;

		private const double SubpixelScale = (int)poly_subpixel_scale_e.poly_subpixel_scale;

		public override double UpscaleD(double v) => v;

		public override double DownscaleD(int v) => v / SubpixelScale;

		public override RectangleDouble ClipBoxPixels => box;

		public override void ClipBoxD(double x1, double y1, double x2, double y2)
		{
			box = new RectangleDouble(x1, y1, x2, y2);
			box.normalize();
			m_clipping = true;
		}

		public override void MoveToD(double x1, double y1)
		{
			m_x1 = x1;
			m_y1 = y1;
			if (m_clipping)
			{
				m_f1 = ClippingFlags(x1, y1);
			}
		}

		// C++ ras_conv_dbl::xi / yi.
		private static int Xi(double v) => Util.iround(v * SubpixelScale);

		private static double MulDiv(double a, double b, double c) => a * b / c;

		private int ClippingFlags(double x, double y)
		{
			return (x > box.Right ? 1 : 0)
				| (y > box.Top ? 1 << 1 : 0)
				| (x < box.Left ? 1 << 2 : 0)
				| (y < box.Bottom ? 1 << 3 : 0);
		}

		private int ClippingFlagsY(double y)
		{
			return ((y > box.Top ? 1 : 0) << 1) | ((y < box.Bottom ? 1 : 0) << 3);
		}

		//------------------------------------------------------------------------
		private void LineClipY(RasterizerCellsAa ras,
									double x1, double y1,
									double x2, double y2,
									int f1, int f2)
		{
			f1 &= 10;
			f2 &= 10;
			if ((f1 | f2) == 0)
			{
				// Fully visible
				ras.line(Xi(x1), Xi(y1), Xi(x2), Xi(y2));
			}
			else
			{
				if (f1 == f2)
				{
					// Invisible by Y
					return;
				}

				double tx1 = x1;
				double ty1 = y1;
				double tx2 = x2;
				double ty2 = y2;

				if ((f1 & 8) != 0) // y1 < clip.y1
				{
					tx1 = x1 + MulDiv(box.Bottom - y1, x2 - x1, y2 - y1);
					ty1 = box.Bottom;
				}

				if ((f1 & 2) != 0) // y1 > clip.y2
				{
					tx1 = x1 + MulDiv(box.Top - y1, x2 - x1, y2 - y1);
					ty1 = box.Top;
				}

				if ((f2 & 8) != 0) // y2 < clip.y1
				{
					tx2 = x1 + MulDiv(box.Bottom - y1, x2 - x1, y2 - y1);
					ty2 = box.Bottom;
				}

				if ((f2 & 2) != 0) // y2 > clip.y2
				{
					tx2 = x1 + MulDiv(box.Top - y1, x2 - x1, y2 - y1);
					ty2 = box.Top;
				}

				ras.line(Xi(tx1), Xi(ty1), Xi(tx2), Xi(ty2));
			}
		}

		//--------------------------------------------------------------------
		public override void LineToD(RasterizerCellsAa ras, double x2, double y2)
		{
			if (m_clipping)
			{
				int f2 = ClippingFlags(x2, y2);

				if ((m_f1 & 10) == (f2 & 10) && (m_f1 & 10) != 0)
				{
					// Invisible by Y
					m_x1 = x2;
					m_y1 = y2;
					m_f1 = f2;
					return;
				}

				double x1 = m_x1;
				double y1 = m_y1;
				int f1 = m_f1;
				double y3, y4;
				int f3, f4;

				switch (((f1 & 5) << 1) | (f2 & 5))
				{
					case 0: // Visible by X
						LineClipY(ras, x1, y1, x2, y2, f1, f2);
						break;

					case 1: // x2 > clip.x2
						y3 = y1 + MulDiv(box.Right - x1, y2 - y1, x2 - x1);
						f3 = ClippingFlagsY(y3);
						LineClipY(ras, x1, y1, box.Right, y3, f1, f3);
						LineClipY(ras, box.Right, y3, box.Right, y2, f3, f2);
						break;

					case 2: // x1 > clip.x2
						y3 = y1 + MulDiv(box.Right - x1, y2 - y1, x2 - x1);
						f3 = ClippingFlagsY(y3);
						LineClipY(ras, box.Right, y1, box.Right, y3, f1, f3);
						LineClipY(ras, box.Right, y3, x2, y2, f3, f2);
						break;

					case 3: // x1 > clip.x2 && x2 > clip.x2
						LineClipY(ras, box.Right, y1, box.Right, y2, f1, f2);
						break;

					case 4: // x2 < clip.x1
						y3 = y1 + MulDiv(box.Left - x1, y2 - y1, x2 - x1);
						f3 = ClippingFlagsY(y3);
						LineClipY(ras, x1, y1, box.Left, y3, f1, f3);
						LineClipY(ras, box.Left, y3, box.Left, y2, f3, f2);
						break;

					case 6: // x1 > clip.x2 && x2 < clip.x1
						y3 = y1 + MulDiv(box.Right - x1, y2 - y1, x2 - x1);
						y4 = y1 + MulDiv(box.Left - x1, y2 - y1, x2 - x1);
						f3 = ClippingFlagsY(y3);
						f4 = ClippingFlagsY(y4);
						LineClipY(ras, box.Right, y1, box.Right, y3, f1, f3);
						LineClipY(ras, box.Right, y3, box.Left, y4, f3, f4);
						LineClipY(ras, box.Left, y4, box.Left, y2, f4, f2);
						break;

					case 8: // x1 < clip.x1
						y3 = y1 + MulDiv(box.Left - x1, y2 - y1, x2 - x1);
						f3 = ClippingFlagsY(y3);
						LineClipY(ras, box.Left, y1, box.Left, y3, f1, f3);
						LineClipY(ras, box.Left, y3, x2, y2, f3, f2);
						break;

					case 9:  // x1 < clip.x1 && x2 > clip.x2
						y3 = y1 + MulDiv(box.Left - x1, y2 - y1, x2 - x1);
						y4 = y1 + MulDiv(box.Right - x1, y2 - y1, x2 - x1);
						f3 = ClippingFlagsY(y3);
						f4 = ClippingFlagsY(y4);
						LineClipY(ras, box.Left, y1, box.Left, y3, f1, f3);
						LineClipY(ras, box.Left, y3, box.Right, y4, f3, f4);
						LineClipY(ras, box.Right, y4, box.Right, y2, f4, f2);
						break;

					case 12: // x1 < clip.x1 && x2 < clip.x1
						LineClipY(ras, box.Left, y1, box.Left, y2, f1, f2);
						break;
				}
				m_f1 = f2;
			}
			else
			{
				ras.line(Xi(m_x1), Xi(m_y1), Xi(x2), Xi(y2));
			}
			m_x1 = x2;
			m_y1 = y2;
		}
	}
}
