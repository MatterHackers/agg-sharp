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
// Arc vertex generator
//
//----------------------------------------------------------------------------
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

using FlagsAndCommand = MatterHackers.Agg.ShapePath.FlagsAndCommand;

namespace MatterHackers.Agg.VertexSource
{
	//=====================================================================arc
	//
	// See Implementation agg_arc.cpp
	//
	public class Arc : VertexSourceLegacySupport
	{
		private Vector2 origin;

		private double radiusX;
		private double radiusY;

		private double startAngle;
		private double endAngle;
		private double scale = 1.0;
		private bool moveToStart = true;

		private double flatenDeltaAngle;

		private bool isInitialized = false;

		public Arc()
		{
		}

		public Arc(double OriginX, double OriginY,
			 double RadiusX, double RadiusY,
			 double Angle1, double Angle2,
			 double Scale = 1.0,
			 bool moveToStart = true)
		{
			init(OriginX, OriginY, RadiusX, RadiusY, Angle1, Angle2, Scale: Scale, moveToStart: moveToStart);
		}

		public void init(double OriginX, double OriginY,
				   double RadiusX, double RadiusY,
				   double Angle1, double Angle2,
				   double Scale = 1.0,
				   bool moveToStart = true)
		{
			origin.x = OriginX;
			origin.y = OriginY;
			radiusX = RadiusX;
			radiusY = RadiusY;
			scale = Scale;
			this.moveToStart = moveToStart;
			normalize(Angle1, Angle2);
		}

		public void approximation_scale(double s)
		{
			scale = s;
			isInitialized = false; // force recalc
		}

		public double approximation_scale()
		{
			return scale;
		}

		override public IEnumerable<VertexData> Vertices()
		{
			if (!isInitialized)
			{
				normalize(startAngle, endAngle);
			}

			// go to the start
			VertexData vertexData = new VertexData();
			vertexData.command = moveToStart ? FlagsAndCommand.CommandMoveTo : FlagsAndCommand.CommandLineTo;
			vertexData.position.x = origin.x + Math.Cos(startAngle) * radiusX;
			vertexData.position.y = origin.y + Math.Sin(startAngle) * radiusY;
			yield return vertexData;

			vertexData.command = FlagsAndCommand.CommandLineTo;
			double angle = startAngle;
			while (angle < endAngle - flatenDeltaAngle / 4)
			{
				vertexData.position.x = origin.x + Math.Cos(angle) * radiusX;
				vertexData.position.y = origin.y + Math.Sin(angle) * radiusY;
				yield return vertexData;

				angle += flatenDeltaAngle;
			}

			vertexData.position.x = origin.x + Math.Cos(endAngle) * radiusX;
			vertexData.position.y = origin.y + Math.Sin(endAngle) * radiusY;
			yield return vertexData;

			vertexData.command = FlagsAndCommand.CommandStop;
			yield return vertexData;
		}

		private void normalize(double Angle1, double Angle2)
		{
			double ra = (Math.Abs(radiusX) + Math.Abs(radiusY)) / 2;
			flatenDeltaAngle = Math.Acos(ra / (ra + 0.125 / scale)) * 2;
			while (Angle2 < Angle1)
			{
				Angle2 += Math.PI * 2.0;
			}

			startAngle = Angle1;
			endAngle = Angle2;
			isInitialized = true;
		}
	}
}