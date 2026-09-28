//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

using System;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg
{
	/// <summary>A C++ <c>rgba</c>: a color with double channels, what line image sources hand the pattern.</summary>
	public struct LineImageColor
	{
		public double R;
		public double G;
		public double B;
		public double A;

		public LineImageColor(double r, double g, double b, double a)
		{
			this.R = r;
			this.G = g;
			this.B = b;
			this.A = a;
		}

		/// <summary>C++ <c>rgba::premultiply</c>.</summary>
		public LineImageColor Premultiply() => new LineImageColor(this.R * this.A, this.G * this.A, this.B * this.A, this.A);

		/// <summary>C++ <c>rgba::gradient</c>: the color <paramref name="k"/> of the way to <paramref name="c"/>.</summary>
		public LineImageColor Gradient(LineImageColor c, double k) => new LineImageColor(
			this.R + ((c.R - this.R) * k),
			this.G + ((c.G - this.G) * k),
			this.B + ((c.B - this.B) * k),
			this.A + ((c.A - this.A) * k));

		public LineImageColor Scale(double k) => new LineImageColor(this.R * k, this.G * k, this.B * k, this.A * k);

		public LineImageColor Add(LineImageColor c) => new LineImageColor(this.R + c.R, this.G + c.G, this.B + c.B, this.A + c.A);

		/// <summary>C++ <c>rgba8(rgba)</c>: each channel uround(v * 255), kept to its low byte as C++'s cast does.</summary>
		public Color ToRgba8() => new Color(ToByte(this.R), ToByte(this.G), ToByte(this.B), ToByte(this.A));

		private static int ToByte(double v) => (byte)Util.uround(v * 255);
	}

	/// <summary>What <see cref="line_image_pattern.create(ILineImageSource)"/> reads: C++'s template Source.</summary>
	public interface ILineImageSource
	{
		double Width { get; }

		double Height { get; }

		LineImageColor Pixel(int x, int y);
	}

	/// <summary>
	/// C++ <c>line_image_scale</c>: a source resampled to <c>height</c> rows, so a pattern can be as tall as the line
	/// is wide. Shrinking averages the source rows each output row covers; growing interpolates between the nearest.
	/// </summary>
	public class LineImageScale : ILineImageSource
	{
		private readonly ILineImageSource source;
		private readonly double scale;
		private readonly double scaleInverse;

		public LineImageScale(ILineImageSource source, double height)
		{
			this.source = source;
			this.Height = height;
			this.scale = source.Height / height;
			this.scaleInverse = height / source.Height;
		}

		public double Width => this.source.Width;

		public double Height { get; }

		public LineImageColor Pixel(int x, int y)
		{
			// C++ takes the source's height as an int here.
			int h = (int)this.source.Height - 1;
			if (this.scale < 1.0)
			{
				double sourceY = ((y + 0.5) * this.scale) - 0.5;
				int y1 = (int)Math.Floor(sourceY);
				int y2 = y1 + 1;
				LineImageColor pix1 = y1 < 0 ? default : this.source.Pixel(x, y1);
				LineImageColor pix2 = y2 > h ? default : this.source.Pixel(x, y2);
				return pix1.Gradient(pix2, sourceY - y1);
			}
			else
			{
				double sourceY1 = ((y + 0.5) * this.scale) - 0.5;
				double sourceY2 = sourceY1 + this.scale;
				int y1 = (int)Math.Floor(sourceY1);
				int y2 = (int)Math.Floor(sourceY2);
				LineImageColor c = default;
				if (y1 >= 0)
				{
					c = c.Add(this.source.Pixel(x, y1).Scale(y1 + 1 - sourceY1));
				}

				while (++y1 < y2)
				{
					if (y1 <= h)
					{
						c = c.Add(this.source.Pixel(x, y1));
					}
				}

				if (y2 <= h)
				{
					c = c.Add(this.source.Pixel(x, y2).Scale(sourceY2 - y2));
				}

				return c.Scale(this.scaleInverse);
			}
		}
	}

	//======================================================line_image_pattern
	/// <summary>
	/// C++ <c>line_image_pattern</c>: the image an <see cref="ImageLineRenderer"/> draws along a line, stored with
	/// a border of <c>dilation</c> pixels so the filter can sample past its edges - transparent above and below, and
	/// wrapped round left and right, so the pattern repeats seamlessly along the line.
	/// </summary>
	public class line_image_pattern
	{
		protected readonly IPatternFilter m_filter;
		protected readonly int m_dilation;
		protected readonly int m_dilation_hr;
		protected ImageBuffer m_buf;
		protected int m_width;
		protected int m_height;
		protected int m_width_hr;
		protected int m_half_height_hr;
		protected int m_offset_y_hr;

		public line_image_pattern(IPatternFilter filter)
		{
			m_filter = filter;
			m_dilation = filter.dilation() + 1;
			m_dilation_hr = m_dilation << LineAABasics.line_subpixel_shift;
		}

		public line_image_pattern(IPatternFilter filter, ILineImageSource src)
			: this(filter)
		{
			create(src);
		}

		/// <summary>C++ <c>create</c>: fills the dilated pattern from <paramref name="src"/>.</summary>
		public virtual void create(ILineImageSource src)
		{
			m_height = Util.uceil(src.Height);
			m_width = Util.uceil(src.Width);
			m_width_hr = Util.uround(src.Width * LineAABasics.line_subpixel_scale);
			m_half_height_hr = Util.uround(src.Height * LineAABasics.line_subpixel_scale / 2);
			m_offset_y_hr = m_dilation_hr + m_half_height_hr - (LineAABasics.line_subpixel_scale / 2);
			m_half_height_hr += LineAABasics.line_subpixel_scale / 2;

			// The filter reads raw BGRA bytes: this buffer is only ever written directly, never blended into.
			m_buf = new ImageBuffer(m_width + (m_dilation * 2), m_height + (m_dilation * 2));

			for (int y = 0; y < m_height; y++)
			{
				for (int x = 0; x < m_width; x++)
				{
					SetPixel(x + m_dilation, y + m_dilation, src.Pixel(x, y).ToRgba8());
				}
			}

			// The rows above and below stay transparent (a new buffer is all zero), then every row wraps round.
			int h = m_height + (m_dilation * 2);
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < m_dilation; x++)
				{
					SetPixel(m_dilation + m_width + x, y, GetPixel(m_dilation + x, y));
					SetPixel(m_dilation - 1 - x, y, GetPixel(m_dilation + m_width - 1 - x, y));
				}
			}
		}

		public int pattern_width()
		{
			return m_width_hr;
		}

		public int line_width()
		{
			return m_half_height_hr;
		}

		public double width()
		{
			return m_height;
		}

		/// <summary>C++ <c>pixel</c>: the filtered pattern color at subpixel (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public virtual void pixel(Color[] destBuffer, int destBufferOffset, int x, int y)
		{
			m_filter.pixel_high_res(m_buf, destBuffer, destBufferOffset, (x % m_width_hr) + m_dilation_hr, y + m_offset_y_hr);
		}

		public IPatternFilter filter()
		{
			return m_filter;
		}

		private void SetPixel(int x, int y, Color c)
		{
			byte[] buffer = m_buf.GetBuffer();
			int offset = m_buf.GetBufferOffsetXY(x, y);
			buffer[offset + ImageBuffer.OrderR] = c.red;
			buffer[offset + ImageBuffer.OrderG] = c.green;
			buffer[offset + ImageBuffer.OrderB] = c.blue;
			buffer[offset + ImageBuffer.OrderA] = c.alpha;
		}

		private Color GetPixel(int x, int y)
		{
			byte[] buffer = m_buf.GetBuffer();
			int offset = m_buf.GetBufferOffsetXY(x, y);
			return new Color(buffer[offset + ImageBuffer.OrderR], buffer[offset + ImageBuffer.OrderG], buffer[offset + ImageBuffer.OrderB], buffer[offset + ImageBuffer.OrderA]);
		}
	}

	//=================================================line_image_pattern_pow2
	/// <summary>
	/// C++ <c>line_image_pattern_pow2</c>: repeats the pattern every power of two pixels, a mask instead of a modulo;
	/// a pattern not a power of two wide is padded out with its wrap.
	/// </summary>
	public class line_image_pattern_pow2 : line_image_pattern
	{
		private int m_mask = LineAABasics.line_subpixel_mask;

		public line_image_pattern_pow2(IPatternFilter filter)
			: base(filter)
		{
		}

		public line_image_pattern_pow2(IPatternFilter filter, ILineImageSource src)
			: base(filter)
		{
			create(src);
		}

		public override void create(ILineImageSource src)
		{
			base.create(src);
			m_mask = 1;
			while (m_mask < m_width)
			{
				m_mask <<= 1;
				m_mask |= 1;
			}

			m_mask <<= LineAABasics.line_subpixel_shift - 1;
			m_mask |= LineAABasics.line_subpixel_mask;
			m_width_hr = m_mask + 1;
		}

		public override void pixel(Color[] destBuffer, int destBufferOffset, int x, int y)
		{
			m_filter.pixel_high_res(m_buf, destBuffer, destBufferOffset, (x & m_mask) + m_dilation_hr, y + m_offset_y_hr);
		}
	}
}
