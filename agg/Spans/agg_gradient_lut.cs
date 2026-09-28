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
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------

using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg
{
	/// <summary>
	/// C++ AGG's <c>gradient_lut</c>: a color table built from SVG-style color stops, usable directly as the
	/// color function of <see cref="span_gradient"/>. Call <see cref="remove_all"/>, <see cref="add_color"/> at
	/// least twice (offsets 0 to 1), then <see cref="build_lut"/>.
	/// </summary>
	/// <remarks>
	/// The colors are interpolated byte for byte in whatever space they are given in; C++ gradient_lut over
	/// <c>color_interpolator&lt;srgba8&gt;</c> is this table fed sRGB bytes, its entries converted back to linear
	/// when drawn. Unlike C++, the last entry of each segment is its stop's color (see <see cref="build_lut"/>).
	/// </remarks>
	public class gradient_lut : IColorFunction
	{
		private readonly List<(double Offset, Color Color)> colorProfile = new List<(double, Color)>();
		private readonly Color[] colorLut;
		private readonly bool fastInterpolator;

		/// <param name="size">C++ <c>ColorLutSize</c>, 256 by default.</param>
		/// <param name="fastInterpolator">True for C++ <c>color_interpolator&lt;rgba8&gt;</c> (a 14-bit DDA per
		/// channel); false for the generic <c>color_interpolator</c>, <c>rgba8::gradient</c> at each step, which
		/// is what every other 8-bit color type (srgba8 included) gets.</param>
		public gradient_lut(int size = 256, bool fastInterpolator = true)
		{
			this.colorLut = new Color[size];
			this.fastInterpolator = fastInterpolator;
		}

		public Color this[int i] => this.colorLut[i];

		public int size() => this.colorLut.Length;

		public void remove_all() => this.colorProfile.Clear();

		/// <summary>A stop at <paramref name="offset"/>, clamped to 0 to 1.</summary>
		public void add_color(double offset, Color color)
		{
			offset = offset < 0.0 ? 0.0 : offset > 1.0 ? 1.0 : offset;
			this.colorProfile.Add((offset, color));
		}

		/// <summary>
		/// Sorts the stops, drops all but the first of equal offsets and fills the table. Entry i is at offset
		/// i / size; each segment is interpolated from its start stop's color to its end stop's color on its last
		/// entry. (C++ sizes the interpolator end - start + 1, so every segment stopped two steps short of its end
		/// color; the reference renderer carries the same fix.)
		/// </summary>
		public void build_lut()
		{
			var profile = this.colorProfile.OrderBy(p => p.Offset).ToList();
			for (int i = profile.Count - 1; i > 0; i--)
			{
				if (profile[i].Offset == profile[i - 1].Offset)
				{
					profile.RemoveAt(i);
				}
			}

			if (profile.Count < 2)
			{
				return;
			}

			int lutSize = this.colorLut.Length;
			int start = (int)Util.uround(profile[0].Offset * lutSize);
			int end = start;
			for (int i = 0; i < start; i++)
			{
				this.colorLut[i] = profile[0].Color;
			}

			for (int i = 1; i < profile.Count; i++)
			{
				end = (int)Util.uround(profile[i].Offset * lutSize);
				int segmentLength = end > start + 1 ? end - start - 1 : 1;
				Color c1 = profile[i - 1].Color;
				Color c2 = profile[i].Color;
				if (this.fastInterpolator)
				{
					var r = new dda_line_interpolator(c1.red, c2.red, segmentLength, 14);
					var g = new dda_line_interpolator(c1.green, c2.green, segmentLength, 14);
					var b = new dda_line_interpolator(c1.blue, c2.blue, segmentLength, 14);
					var a = new dda_line_interpolator(c1.alpha, c2.alpha, segmentLength, 14);
					for (; start < end; start++)
					{
						this.colorLut[start] = new Color(r.y(), g.y(), b.y(), a.y());
						r.Next();
						g.Next();
						b.Next();
						a.Next();
					}
				}
				else
				{
					for (int count = 0; start < end; start++, count++)
					{
						this.colorLut[start] = Rgba8.Gradient(c1, c2, (double)count / segmentLength);
					}
				}
			}

			Color last = profile[profile.Count - 1].Color;
			for (; end < lutSize; end++)
			{
				this.colorLut[end] = last;
			}
		}
	}
}
