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
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------
//
// trans_warp_magnifier
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg.Transform
{
	/// <summary>
	/// C++ AGG's trans_warp_magnifier: a lens. Points within <see cref="Radius"/> of the center are pushed out
	/// from it by <see cref="Magnification"/>; points outside move out by the fixed amount the rim moved,
	/// radius * (magnification - 1), so the plane stays continuous and far points barely move. It only moves
	/// vertices - put a <see cref="VertexSource.Segmentator"/> in front so long edges bend.
	/// </summary>
	public class TransWarpMagnifier : ITransform
	{
		/// <summary>X of the lens center (C++ <c>xc()</c>).</summary>
		public double CenterX { get; set; }

		/// <summary>Y of the lens center (C++ <c>yc()</c>).</summary>
		public double CenterY { get; set; }

		/// <summary>How much the lens enlarges what is inside it (default 1, no change).</summary>
		public double Magnification { get; set; } = 1.0;

		/// <summary>
		/// The lens radius in source space (default 1). On screen the magnified disc is Radius * Magnification
		/// in radius.
		/// </summary>
		public double Radius { get; set; } = 1.0;

		/// <summary>Moves the lens center, as C++ <c>center(x, y)</c>.</summary>
		public void SetCenter(double x, double y)
		{
			CenterX = x;
			CenterY = y;
		}

		/// <summary>Maps a source point to where the lens shows it.</summary>
		/// <remarks>
		/// A zero-radius lens, or the exact center of one, leaves the point alone. C++ AGG divides 0 by 0 at
		/// the center of a zero-radius lens and returns NaN; the reference renderer's patched
		/// agg_trans_warp_magnifier.cpp does what this does (TransWarpMagnifierTests).
		/// </remarks>
		public void Transform(ref double x, ref double y)
		{
			double dx = x - CenterX;
			double dy = y - CenterY;
			double r = Math.Sqrt(dx * dx + dy * dy);
			if (r == 0 || Radius == 0)
			{
				return;
			}

			if (r < Radius)
			{
				x = CenterX + dx * Magnification;
				y = CenterY + dy * Magnification;
				return;
			}

			double m = (r + Radius * (Magnification - 1.0)) / r;
			x = CenterX + dx * m;
			y = CenterY + dy * m;
		}

		/// <summary>
		/// Maps a point on screen back to the source point the lens shows there - the inverse of
		/// <see cref="Transform"/> (C++ AGG 2.4's closed form by Andrew Skalkin). Like <see cref="Transform"/>,
		/// a zero radius or the exact center leaves the point alone where C++ can return NaN.
		/// </summary>
		public void InverseTransform(ref double x, ref double y)
		{
			double dx = x - CenterX;
			double dy = y - CenterY;
			double r = Math.Sqrt(dx * dx + dy * dy);
			if (r == 0 || Radius == 0)
			{
				return;
			}

			if (r < Radius * Magnification)
			{
				x = CenterX + dx / Magnification;
				y = CenterY + dy / Magnification;
			}
			else
			{
				double rnew = r - Radius * (Magnification - 1.0);
				x = CenterX + rnew * dx / r;
				y = CenterY + rnew * dy / r;
			}
		}
	}
}
