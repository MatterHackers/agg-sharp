using MatterHackers.VectorMath;

//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026 Lars Brubaker
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
// class ellipse
//
//----------------------------------------------------------------------------
using System;
using System.Collections.Generic;
using FlagsAndCommand = MatterHackers.Agg.FlagsAndCommand;

namespace MatterHackers.Agg.VertexSource
{
	public class Ellipse : VertexSourceLegacySupport
	{
		public double originX;
		public double originY;
		public double radiusX;
		public double radiusY;

		#region resolution
		private double _resolutionScale = 1;
		public double ResolutionScale
		{
			get { return _resolutionScale; }
			set
			{
				_resolutionScale = value;
				calc_num_steps();
				InvalidateVertices();
			}
		}
		#endregion

		public int NumSteps { get; private set; }
		//private int m_step;
		public bool IsCw;

		public Ellipse()
		{
			originX = 0.0;
			originY = 0.0;
			radiusX = 1.0;
			radiusY = 1.0;
			NumSteps = 4;
			//m_step = 0;
			IsCw = false;
		}

		public Ellipse(Vector2 origin, double Radius)
			: this(origin.X, origin.Y, Radius, Radius, 0, false)
		{
		}

		public Ellipse(Vector2 origin, double RadiusX, double RadiusY, int num_steps = 0, bool cw = false)
			: this(origin.X, origin.Y, RadiusX, RadiusY, num_steps, cw)
		{
		}

		public Ellipse(double OriginX, double OriginY, double RadiusX, double RadiusY, int num_steps = 0, bool cw = false)
		{
			this.originX = OriginX;
			this.originY = OriginY;
			this.radiusX = RadiusX;
			this.radiusY = RadiusY;
			NumSteps = num_steps;
			//m_step = 0;
			IsCw = cw;
			if (NumSteps == 0)
			{
				calc_num_steps();
			}
		}

		public void init(double OriginX, double OriginY, double RadiusX, double RadiusY)
		{
			init(OriginX, OriginY, RadiusX, RadiusY, 0, false);
		}

		public void init(double OriginX, double OriginY, double RadiusX, double RadiusY, int num_steps)
		{
			init(OriginX, OriginY, RadiusX, RadiusY, num_steps, false);
		}

		public void init(double OriginX, double OriginY, double RadiusX, double RadiusY,
				  int num_steps, bool cw)
		{
			originX = OriginX;
			originY = OriginY;
			radiusX = RadiusX;
			radiusY = RadiusY;
			NumSteps = num_steps;
			//m_step = 0;
			IsCw = cw;
			if (NumSteps == 0)
			{
				calc_num_steps();
			}

			// The other init overloads funnel through here, so this one call covers them all. Note that
			// assigning the public originX/originY/radiusX/radiusY/IsCw fields directly cannot invalidate;
			// callers that reshape that way still have to Rewind by hand.
			InvalidateVertices();
		}

		/// <summary>
		/// C++ agg_ellipse.h <c>vertex</c>: step i sits at angle i / NumSteps * 2pi (2pi minus that when
		/// <see cref="IsCw"/>), each computed on its own. Summing a per-step angle drifts in the last bits and
		/// moves pixels against every C++ golden that draws a circle.
		/// </summary>
		public override IEnumerable<VertexData> Vertices()
		{
			VertexData vertexData = new VertexData();
			for (int step = 0; step < NumSteps; step++)
			{
				double angle = (double)step / (double)NumSteps * 2.0 * Math.PI;
				if (IsCw)
				{
					angle = 2.0 * Math.PI - angle;
				}

				vertexData.Command = step == 0 ? FlagsAndCommand.MoveTo : FlagsAndCommand.LineTo;
				vertexData.Position = new Vector2(originX + Math.Cos(angle) * radiusX, originY + Math.Sin(angle) * radiusY);
				yield return vertexData;
			}

			vertexData.Position = new Vector2();
			vertexData.Command = FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW;
			yield return vertexData;
			vertexData.Command = FlagsAndCommand.Stop;
			yield return vertexData;
		}

		private void calc_num_steps()
		{
			double ra = (Math.Abs(radiusX) + Math.Abs(radiusY)) / 2;
			double da = Math.Acos(ra / (ra + 0.125 / ResolutionScale)) * 2;
			// C++ uround (half up), not Math.Round's half to even.
			NumSteps = Util.uround(2 * Math.PI / da);
		}
	};
}