using MatterHackers.VectorMath;

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
		}

		public override IEnumerable<VertexData> Vertices()
		{
			VertexData vertexData = new VertexData();
			vertexData.Command = FlagsAndCommand.MoveTo;
			vertexData.Position = new Vector2(originX + radiusX, originY);
			yield return vertexData;

			double anglePerStep = MathHelper.Tau / (double)NumSteps;
			double angle = 0;
			vertexData.Command = FlagsAndCommand.LineTo;
			for (int i = 1; i < NumSteps; i++)
			{
				angle += anglePerStep;

				if (IsCw)
				{
					vertexData.Position = new Vector2(originX + Math.Cos(MathHelper.Tau - angle) * radiusX,
						originY + Math.Sin(MathHelper.Tau - angle) * radiusY);
					yield return vertexData;
				}
				else
				{
					vertexData.Position = new Vector2(originX + Math.Cos(angle) * radiusX, originY + Math.Sin(angle) * radiusY);
					yield return vertexData;
				}
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
			NumSteps = (int)Math.Round(2 * Math.PI / da);
		}
	};
}