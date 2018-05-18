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
//
// class ClippingPixelFormtProxy
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg.Image
{
	public class ImageClippingProxy : ImageProxy
	{
		private RectangleInt m_ClippingRect;

		public const byte cover_full = 255;

		public ImageClippingProxy(IImageByte ren)
			: base(ren)
		{
			m_ClippingRect = new RectangleInt(0, 0, (int)ren.Width - 1, (int)ren.Height - 1);
		}

		public override void LinkToImage(IImageByte ren)
		{
			base.LinkToImage(ren);
			m_ClippingRect = new RectangleInt(0, 0, (int)ren.Width - 1, (int)ren.Height - 1);
		}

		public bool SetClippingBox(int x1, int y1, int x2, int y2)
		{
			RectangleInt cb = new RectangleInt(x1, y1, x2, y2);
			cb.normalize();
			if (cb.clip(new RectangleInt(0, 0, (int)Width - 1, (int)Height - 1)))
			{
				m_ClippingRect = cb;
				return true;
			}
			m_ClippingRect.Left = 1;
			m_ClippingRect.Bottom = 1;
			m_ClippingRect.Right = 0;
			m_ClippingRect.Top = 0;
			return false;
		}

		public void reset_clipping(bool visibility)
		{
			if (visibility)
			{
				m_ClippingRect.Left = 0;
				m_ClippingRect.Bottom = 0;
				m_ClippingRect.Right = (int)Width - 1;
				m_ClippingRect.Top = (int)Height - 1;
			}
			else
			{
				m_ClippingRect.Left = 1;
				m_ClippingRect.Bottom = 1;
				m_ClippingRect.Right = 0;
				m_ClippingRect.Top = 0;
			}
		}

		public void clip_box_naked(int x1, int y1, int x2, int y2)
		{
			m_ClippingRect.Left = x1;
			m_ClippingRect.Bottom = y1;
			m_ClippingRect.Right = x2;
			m_ClippingRect.Top = y2;
		}

		public bool inbox(int x, int y)
		{
			return x >= m_ClippingRect.Left && y >= m_ClippingRect.Bottom &&
				   x <= m_ClippingRect.Right && y <= m_ClippingRect.Top;
		}

		public RectangleInt clip_box()
		{
			return m_ClippingRect;
		}

		private int xmin()
		{
			return m_ClippingRect.Left;
		}

		private int ymin()
		{
			return m_ClippingRect.Bottom;
		}

		private int xmax()
		{
			return m_ClippingRect.Right;
		}

		private int ymax()
		{
			return m_ClippingRect.Top;
		}

		public RectangleInt bounding_clip_box()
		{
			return m_ClippingRect;
		}

		public int bounding_xmin()
		{
			return m_ClippingRect.Left;
		}

		public int bounding_ymin()
		{
			return m_ClippingRect.Bottom;
		}

		public int bounding_xmax()
		{
			return m_ClippingRect.Right;
		}

		public int bounding_ymax()
		{
			return m_ClippingRect.Top;
		}

		public void clear(IColorType in_c)
		{
			int y;
			Color c = new Color(in_c.Red0To255, in_c.Green0To255, in_c.Blue0To255, in_c.Alpha0To255);
			if (Width != 0)
			{
				for (y = 0; y < Height; y++)
				{
					base.copy_hline(0, (int)y, (int)Width, c);
				}
			}
		}

		public override void copy_pixel(int x, int y, byte[] c, int ByteOffset)
		{
			if (inbox(x, y))
			{
				base.copy_pixel(x, y, c, ByteOffset);
			}
		}

		public override Color GetPixel(int x, int y)
		{
			return inbox(x, y) ? base.GetPixel(x, y) : new Color();
		}

		public override void copy_hline(int x1, int y, int x2, Color c)
		{
			if (x1 > x2) { int t = (int)x2; x2 = (int)x1; x1 = t; }
			if (y > ymax()) return;
			if (y < ymin()) return;
			if (x1 > xmax()) return;
			if (x2 < xmin()) return;

			if (x1 < xmin()) x1 = xmin();
			if (x2 > xmax()) x2 = (int)xmax();

			base.copy_hline(x1, y, (int)(x2 - x1 + 1), c);
		}

		public override void copy_vline(int x, int y1, int y2, Color c)
		{
			if (y1 > y2) { int t = (int)y2; y2 = (int)y1; y1 = t; }
			if (x > xmax()) return;
			if (x < xmin()) return;
			if (y1 > ymax()) return;
			if (y2 < ymin()) return;

			if (y1 < ymin()) y1 = ymin();
			if (y2 > ymax()) y2 = (int)ymax();

			base.copy_vline(x, y1, (int)(y2 - y1 + 1), c);
		}

		public override void blend_hline(int x1, int y, int x2, Color c, byte cover)
		{
			if (x1 > x2)
			{
				int t = (int)x2;
				x2 = x1;
				x1 = t;
			}
			if (y > ymax())
				return;
			if (y < ymin())
				return;
			if (x1 > xmax())
				return;
			if (x2 < xmin())
				return;

			if (x1 < xmin())
				x1 = xmin();
			if (x2 > xmax())
				x2 = xmax();

			base.blend_hline(x1, y, x2, c, cover);
		}

		public override void blend_vline(int x, int y1, int y2, Color c, byte cover)
		{
			if (y1 > y2) { int t = y2; y2 = y1; y1 = t; }
			if (x > xmax()) return;
			if (x < xmin()) return;
			if (y1 > ymax()) return;
			if (y2 < ymin()) return;

			if (y1 < ymin()) y1 = ymin();
			if (y2 > ymax()) y2 = ymax();

			base.blend_vline(x, y1, y2, c, cover);
		}

		public override void blend_solid_hspan(int x, int y, int len, Color c, byte[] covers, int coversIndex)
		{
#if false
            FileStream file = new FileStream("pixels.txt", FileMode.Append, FileAccess.Write);
            StreamWriter sw = new StreamWriter(file);
            sw.Write("h-x=" + x.ToString() + ",y=" + y.ToString() + ",len=" + len.ToString() + "\n");
            sw.Close();
            file.Close();
#endif

			if (y > ymax()) return;
			if (y < ymin()) return;

			if (x < xmin())
			{
				len -= xmin() - x;
				if (len <= 0) return;
				coversIndex += xmin() - x;
				x = xmin();
			}
			if (x + len > xmax())
			{
				len = xmax() - x + 1;
				if (len <= 0) return;
			}
			base.blend_solid_hspan(x, y, len, c, covers, coversIndex);
		}

		public override void blend_solid_vspan(int x, int y, int len, Color c, byte[] covers, int coversIndex)
		{
#if false
            FileStream file = new FileStream("pixels.txt", FileMode.Append, FileAccess.Write);
            StreamWriter sw = new StreamWriter(file);
            sw.Write("v-x=" + x.ToString() + ",y=" + y.ToString() + ",len=" + len.ToString() + "\n");
            sw.Close();
            file.Close();
#endif

			if (x > xmax()) return;
			if (x < xmin()) return;

			if (y < ymin())
			{
				len -= (ymin() - y);
				if (len <= 0) return;
				coversIndex += ymin() - y;
				y = ymin();
			}
			if (y + len > ymax())
			{
				len = (ymax() - y + 1);
				if (len <= 0) return;
			}
			base.blend_solid_vspan(x, y, len, c, covers, coversIndex);
		}

		public override void copy_color_hspan(int x, int y, int len, Color[] colors, int colorsIndex)
		{
			if (y > ymax()) return;
			if (y < ymin()) return;

			if (x < xmin())
			{
				int d = xmin() - x;
				len -= d;
				if (len <= 0) return;
				colorsIndex += d;
				x = xmin();
			}
			if (x + len > xmax())
			{
				len = (xmax() - x + 1);
				if (len <= 0) return;
			}
			base.copy_color_hspan(x, y, len, colors, colorsIndex);
		}

		public override void copy_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex)
		{
			if (x > xmax()) return;
			if (x < xmin()) return;

			if (y < ymin())
			{
				int d = ymin() - y;
				len -= d;
				if (len <= 0) return;
				colorsIndex += d;
				y = ymin();
			}
			if (y + len > ymax())
			{
				len = (ymax() - y + 1);
				if (len <= 0) return;
			}
			base.copy_color_vspan(x, y, len, colors, colorsIndex);
		}

		public override void blend_color_hspan(int x, int y, int in_len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			int len = (int)in_len;
			if (y > ymax())
				return;
			if (y < ymin())
				return;

			if (x < xmin())
			{
				int d = xmin() - x;
				len -= d;
				if (len <= 0) return;
				if (covers != null) coversIndex += d;
				colorsIndex += d;
				x = xmin();
			}
			if (x + len - 1 > xmax())
			{
				len = xmax() - x + 1;
				if (len <= 0) return;
			}

			base.blend_color_hspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}

		public void copy_from(IImageByte src)
		{
			CopyFrom(src, new RectangleInt(0, 0, (int)src.Width, (int)src.Height), 0, 0);
		}

		public override void SetPixel(int x, int y, Color color)
		{
			if ((uint)x < Width && (uint)y < Height)
			{
				base.SetPixel(x, y, color);
			}
		}

		public override void CopyFrom(IImageByte sourceImage,
					   RectangleInt sourceImageRect,
					   int destXOffset,
					   int destYOffset)
		{
			RectangleInt destRect = sourceImageRect;
			destRect.Offset(destXOffset, destYOffset);

			RectangleInt clippedSourceRect = new RectangleInt();
			if (clippedSourceRect.IntersectRectangles(destRect, m_ClippingRect))
			{
				// move it back relative to the source
				clippedSourceRect.Offset(-destXOffset, -destYOffset);

				base.CopyFrom(sourceImage, clippedSourceRect, destXOffset, destYOffset);
			}
		}

		public RectangleInt clip_rect_area(ref RectangleInt destRect, ref RectangleInt sourceRect, int sourceWidth, int sourceHeight)
		{
			RectangleInt rc = new RectangleInt(0, 0, 0, 0);
			RectangleInt cb = clip_box();
			++cb.Right;
			++cb.Top;

			if (sourceRect.Left < 0)
			{
				destRect.Left -= sourceRect.Left;
				sourceRect.Left = 0;
			}
			if (sourceRect.Bottom < 0)
			{
				destRect.Bottom -= sourceRect.Bottom;
				sourceRect.Bottom = 0;
			}

			if (sourceRect.Right > sourceWidth) sourceRect.Right = sourceWidth;
			if (sourceRect.Top > sourceHeight) sourceRect.Top = sourceHeight;

			if (destRect.Left < cb.Left)
			{
				sourceRect.Left += cb.Left - destRect.Left;
				destRect.Left = cb.Left;
			}
			if (destRect.Bottom < cb.Bottom)
			{
				sourceRect.Bottom += cb.Bottom - destRect.Bottom;
				destRect.Bottom = cb.Bottom;
			}

			if (destRect.Right > cb.Right) destRect.Right = cb.Right;
			if (destRect.Top > cb.Top) destRect.Top = cb.Top;

			rc.Right = destRect.Right - destRect.Left;
			rc.Top = destRect.Top - destRect.Bottom;

			if (rc.Right > sourceRect.Right - sourceRect.Left) rc.Right = sourceRect.Right - sourceRect.Left;
			if (rc.Top > sourceRect.Top - sourceRect.Bottom) rc.Top = sourceRect.Top - sourceRect.Bottom;
			return rc;
		}

		public override void blend_color_vspan(int x, int y, int len, Color[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			if (x > xmax()) return;
			if (x < xmin()) return;

			if (y < ymin())
			{
				int d = ymin() - y;
				len -= d;
				if (len <= 0) return;
				if (covers != null) coversIndex += d;
				colorsIndex += d;
				y = ymin();
			}
			if (y + len > ymax())
			{
				len = (ymax() - y + 1);
				if (len <= 0) return;
			}
			base.blend_color_vspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}
	}

	public class ImageClippingProxyFloat : ImageProxyFloat
	{
		private RectangleInt m_ClippingRect;

		public const byte cover_full = 255;

		public ImageClippingProxyFloat(IImageFloat ren)
			: base(ren)
		{
			m_ClippingRect = new RectangleInt(0, 0, (int)ren.Width - 1, (int)ren.Height - 1);
		}

		public override void LinkToImage(IImageFloat ren)
		{
			base.LinkToImage(ren);
			m_ClippingRect = new RectangleInt(0, 0, (int)ren.Width - 1, (int)ren.Height - 1);
		}

		public bool SetClippingBox(int x1, int y1, int x2, int y2)
		{
			RectangleInt cb = new RectangleInt(x1, y1, x2, y2);
			cb.normalize();
			if (cb.clip(new RectangleInt(0, 0, (int)Width - 1, (int)Height - 1)))
			{
				m_ClippingRect = cb;
				return true;
			}
			m_ClippingRect.Left = 1;
			m_ClippingRect.Bottom = 1;
			m_ClippingRect.Right = 0;
			m_ClippingRect.Top = 0;
			return false;
		}

		public void reset_clipping(bool visibility)
		{
			if (visibility)
			{
				m_ClippingRect.Left = 0;
				m_ClippingRect.Bottom = 0;
				m_ClippingRect.Right = (int)Width - 1;
				m_ClippingRect.Top = (int)Height - 1;
			}
			else
			{
				m_ClippingRect.Left = 1;
				m_ClippingRect.Bottom = 1;
				m_ClippingRect.Right = 0;
				m_ClippingRect.Top = 0;
			}
		}

		public void clip_box_naked(int x1, int y1, int x2, int y2)
		{
			m_ClippingRect.Left = x1;
			m_ClippingRect.Bottom = y1;
			m_ClippingRect.Right = x2;
			m_ClippingRect.Top = y2;
		}

		public bool inbox(int x, int y)
		{
			return x >= m_ClippingRect.Left && y >= m_ClippingRect.Bottom &&
				   x <= m_ClippingRect.Right && y <= m_ClippingRect.Top;
		}

		public RectangleInt clip_box()
		{
			return m_ClippingRect;
		}

		private int xmin()
		{
			return m_ClippingRect.Left;
		}

		private int ymin()
		{
			return m_ClippingRect.Bottom;
		}

		private int xmax()
		{
			return m_ClippingRect.Right;
		}

		private int ymax()
		{
			return m_ClippingRect.Top;
		}

		public RectangleInt bounding_clip_box()
		{
			return m_ClippingRect;
		}

		public int bounding_xmin()
		{
			return m_ClippingRect.Left;
		}

		public int bounding_ymin()
		{
			return m_ClippingRect.Bottom;
		}

		public int bounding_xmax()
		{
			return m_ClippingRect.Right;
		}

		public int bounding_ymax()
		{
			return m_ClippingRect.Top;
		}

		public void clear(IColorType in_c)
		{
			int y;
			ColorF colorFloat = in_c.ToColorF();
			if (Width != 0)
			{
				for (y = 0; y < Height; y++)
				{
					base.copy_hline(0, (int)y, (int)Width, colorFloat);
				}
			}
		}

		public override void copy_pixel(int x, int y, float[] c, int ByteOffset)
		{
			if (inbox(x, y))
			{
				base.copy_pixel(x, y, c, ByteOffset);
			}
		}

		public override ColorF GetPixel(int x, int y)
		{
			return inbox(x, y) ? base.GetPixel(x, y) : new ColorF();
		}

		public override void copy_hline(int x1, int y, int x2, ColorF c)
		{
			if (x1 > x2) { int t = (int)x2; x2 = (int)x1; x1 = t; }
			if (y > ymax()) return;
			if (y < ymin()) return;
			if (x1 > xmax()) return;
			if (x2 < xmin()) return;

			if (x1 < xmin()) x1 = xmin();
			if (x2 > xmax()) x2 = (int)xmax();

			base.copy_hline(x1, y, (int)(x2 - x1 + 1), c);
		}

		public override void copy_vline(int x, int y1, int y2, ColorF c)
		{
			if (y1 > y2) { int t = (int)y2; y2 = (int)y1; y1 = t; }
			if (x > xmax()) return;
			if (x < xmin()) return;
			if (y1 > ymax()) return;
			if (y2 < ymin()) return;

			if (y1 < ymin()) y1 = ymin();
			if (y2 > ymax()) y2 = (int)ymax();

			base.copy_vline(x, y1, (int)(y2 - y1 + 1), c);
		}

		public override void blend_hline(int x1, int y, int x2, ColorF c, byte cover)
		{
			if (x1 > x2)
			{
				int t = (int)x2;
				x2 = x1;
				x1 = t;
			}
			if (y > ymax())
				return;
			if (y < ymin())
				return;
			if (x1 > xmax())
				return;
			if (x2 < xmin())
				return;

			if (x1 < xmin())
				x1 = xmin();
			if (x2 > xmax())
				x2 = xmax();

			base.blend_hline(x1, y, x2, c, cover);
		}

		public override void blend_vline(int x, int y1, int y2, ColorF c, byte cover)
		{
			if (y1 > y2) { int t = y2; y2 = y1; y1 = t; }
			if (x > xmax()) return;
			if (x < xmin()) return;
			if (y1 > ymax()) return;
			if (y2 < ymin()) return;

			if (y1 < ymin()) y1 = ymin();
			if (y2 > ymax()) y2 = ymax();

			base.blend_vline(x, y1, y2, c, cover);
		}

		public override void blend_solid_hspan(int x, int y, int in_len, ColorF c, byte[] covers, int coversIndex)
		{
			int len = (int)in_len;
			if (y > ymax()) return;
			if (y < ymin()) return;

			if (x < xmin())
			{
				len -= xmin() - x;
				if (len <= 0) return;
				coversIndex += xmin() - x;
				x = xmin();
			}
			if (x + len > xmax())
			{
				len = xmax() - x + 1;
				if (len <= 0) return;
			}
			base.blend_solid_hspan(x, y, len, c, covers, coversIndex);
		}

		public override void blend_solid_vspan(int x, int y, int len, ColorF c, byte[] covers, int coversIndex)
		{
			if (x > xmax()) return;
			if (x < xmin()) return;

			if (y < ymin())
			{
				len -= (ymin() - y);
				if (len <= 0) return;
				coversIndex += ymin() - y;
				y = ymin();
			}
			if (y + len > ymax())
			{
				len = (ymax() - y + 1);
				if (len <= 0) return;
			}
			base.blend_solid_vspan(x, y, len, c, covers, coversIndex);
		}

		public override void copy_color_hspan(int x, int y, int len, ColorF[] colors, int colorsIndex)
		{
			if (y > ymax()) return;
			if (y < ymin()) return;

			if (x < xmin())
			{
				int d = xmin() - x;
				len -= d;
				if (len <= 0) return;
				colorsIndex += d;
				x = xmin();
			}
			if (x + len > xmax())
			{
				len = (xmax() - x + 1);
				if (len <= 0) return;
			}
			base.copy_color_hspan(x, y, len, colors, colorsIndex);
		}

		public override void copy_color_vspan(int x, int y, int len, ColorF[] colors, int colorsIndex)
		{
			if (x > xmax()) return;
			if (x < xmin()) return;

			if (y < ymin())
			{
				int d = ymin() - y;
				len -= d;
				if (len <= 0) return;
				colorsIndex += d;
				y = ymin();
			}
			if (y + len > ymax())
			{
				len = (ymax() - y + 1);
				if (len <= 0) return;
			}
			base.copy_color_vspan(x, y, len, colors, colorsIndex);
		}

		public override void blend_color_hspan(int x, int y, int in_len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			int len = (int)in_len;
			if (y > ymax())
				return;
			if (y < ymin())
				return;

			if (x < xmin())
			{
				int d = xmin() - x;
				len -= d;
				if (len <= 0) return;
				if (covers != null) coversIndex += d;
				colorsIndex += d;
				x = xmin();
			}
			if (x + len - 1 > xmax())
			{
				len = xmax() - x + 1;
				if (len <= 0) return;
			}

			base.blend_color_hspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}

		public void copy_from(IImageFloat src)
		{
			CopyFrom(src, new RectangleInt(0, 0, (int)src.Width, (int)src.Height), 0, 0);
		}

		public override void SetPixel(int x, int y, ColorF color)
		{
			if ((uint)x < Width && (uint)y < Height)
			{
				base.SetPixel(x, y, color);
			}
		}

		public override void CopyFrom(IImageFloat sourceImage,
					   RectangleInt sourceImageRect,
					   int destXOffset,
					   int destYOffset)
		{
			RectangleInt destRect = sourceImageRect;
			destRect.Offset(destXOffset, destYOffset);

			RectangleInt clippedSourceRect = new RectangleInt();
			if (clippedSourceRect.IntersectRectangles(destRect, m_ClippingRect))
			{
				// move it back relative to the source
				clippedSourceRect.Offset(-destXOffset, -destYOffset);

				base.CopyFrom(sourceImage, clippedSourceRect, destXOffset, destYOffset);
			}
		}

		public RectangleInt clip_rect_area(ref RectangleInt destRect, ref RectangleInt sourceRect, int sourceWidth, int sourceHeight)
		{
			RectangleInt rc = new RectangleInt(0, 0, 0, 0);
			RectangleInt cb = clip_box();
			++cb.Right;
			++cb.Top;

			if (sourceRect.Left < 0)
			{
				destRect.Left -= sourceRect.Left;
				sourceRect.Left = 0;
			}
			if (sourceRect.Bottom < 0)
			{
				destRect.Bottom -= sourceRect.Bottom;
				sourceRect.Bottom = 0;
			}

			if (sourceRect.Right > sourceWidth) sourceRect.Right = sourceWidth;
			if (sourceRect.Top > sourceHeight) sourceRect.Top = sourceHeight;

			if (destRect.Left < cb.Left)
			{
				sourceRect.Left += cb.Left - destRect.Left;
				destRect.Left = cb.Left;
			}
			if (destRect.Bottom < cb.Bottom)
			{
				sourceRect.Bottom += cb.Bottom - destRect.Bottom;
				destRect.Bottom = cb.Bottom;
			}

			if (destRect.Right > cb.Right) destRect.Right = cb.Right;
			if (destRect.Top > cb.Top) destRect.Top = cb.Top;

			rc.Right = destRect.Right - destRect.Left;
			rc.Top = destRect.Top - destRect.Bottom;

			if (rc.Right > sourceRect.Right - sourceRect.Left) rc.Right = sourceRect.Right - sourceRect.Left;
			if (rc.Top > sourceRect.Top - sourceRect.Bottom) rc.Top = sourceRect.Top - sourceRect.Bottom;
			return rc;
		}

		public override void blend_color_vspan(int x, int y, int len, ColorF[] colors, int colorsIndex, byte[] covers, int coversIndex, bool firstCoverForAll)
		{
			if (x > xmax()) return;
			if (x < xmin()) return;

			if (y < ymin())
			{
				int d = ymin() - y;
				len -= d;
				if (len <= 0) return;
				if (covers != null) coversIndex += d;
				colorsIndex += d;
				y = ymin();
			}
			if (y + len > ymax())
			{
				len = (ymax() - y + 1);
				if (len <= 0) return;
			}
			base.blend_color_vspan(x, y, len, colors, colorsIndex, covers, coversIndex, firstCoverForAll);
		}
	}
}