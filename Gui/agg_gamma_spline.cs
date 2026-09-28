using System.Collections.Generic;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

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
//
// class gamma_spline
//
//----------------------------------------------------------------------------

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The GuiWidget face of C++ AGG's <c>gamma_spline</c>: a thin adapter over <see cref="GammaSpline"/>, which
	/// holds the curve and the gamma array. As a vertex source it walks the curve across the box set by
	/// <see cref="box"/>.
	/// </summary>
	/// <remarks>
	/// Values in [0, 2] per point, 1.0 being a quarter of the box: kx1/ky1 place the lower-left control point
	/// from (0, 0), kx2/ky2 the upper-right one from (1, 1). <see cref="gamma"/> is the 256-entry array.
	/// </remarks>
	public class gamma_spline : SimpleVertexSourceWidget
	{
		private IEnumerator<VertexData> curve;

		public gamma_spline()
			: this(new GammaSpline())
		{
		}

		/// <summary>An adapter over <paramref name="spline"/>, so a control and its widget share one curve.</summary>
		public gamma_spline(GammaSpline spline)
			: base(new Vector2())
		{
			this.Spline = spline;
		}

		/// <summary>The curve this widget adapts.</summary>
		public GammaSpline Spline { get; }

		/// <summary>One path: the curve.</summary>
		public override int num_paths() => 1;

		public void values(double kx1, double ky1, double kx2, double ky2) => this.Spline.SetValues(kx1, ky1, kx2, ky2);

		public byte[] gamma() => this.Spline.Gamma;

		public double y(double x) => this.Spline.Y(x);

		public void values(out double kx1, out double ky1, out double kx2, out double ky2) => this.Spline.GetValues(out kx1, out ky1, out kx2, out ky2);

		/// <summary>C++ <c>box</c>: where the curve is drawn.</summary>
		public void box(double x1, double y1, double x2, double y2) => this.Spline.SetBox(x1, y1, x2, y2);

		public override void Rewind(int idx)
		{
			this.curve = this.Spline.CurvePath().Vertices().GetEnumerator();
		}

		public override FlagsAndCommand Vertex(out double ox, out double oy)
		{
			ox = 0;
			oy = 0;
			if (this.curve == null)
			{
				this.Rewind(0);
			}

			if (!this.curve.MoveNext() || this.curve.Current.IsStop)
			{
				return FlagsAndCommand.Stop;
			}

			ox = this.curve.Current.Position.X;
			oy = this.curve.Current.Position.Y;
			return this.curve.Current.Command;
		}
	}
}
