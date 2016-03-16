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
		private Direction direction;

		private double endAngle;

		private double flatenDeltaAngle;

		private bool isInitialized = false;

		private Vector2 origin;

		private Vector2 radius;

		private double scale = 1.0;

		private double startAngle;

		public Arc()
		{
		}

		public Arc(double originX, double originY,
			 double radiusX, double radiusY,
			 double startAngle, double endAngle,
			 Direction direction = Direction.CounterClockWise,
			 double scale = 1.0)
		{
			init(originX, originY, radiusX, radiusY, startAngle, endAngle, direction, scale);
		}

		public Arc(Vector2 origin,
			 Vector2 radius,
			 double startAngle, double endAngle,
			 Direction direction = Direction.CounterClockWise,
			 double scale = 1.0)
		{
			init(origin, radius, startAngle, endAngle, direction, scale);
		}

		public enum Direction
		{
			ClockWise,
			CounterClockWise,
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

		public void init(double originX, double originY,
								   double radiusX, double radiusY,
				   double startAngle, double endAngle,
			 Direction direction = Direction.CounterClockWise,
				   double scale = 1.0)
		{
			init(new Vector2(originX, originY), new Vector2(radiusX, radiusY), startAngle, endAngle, direction, scale);
		}

		public void init(Vector2 origin,
				   Vector2 radius,
				   double startAngle, double endAngle,
			 Direction direction = Direction.CounterClockWise,
				   double scale = 1.0)
		{
			this.origin = origin;
			this.radius = radius;
			this.startAngle = startAngle;
			this.endAngle = endAngle;
			this.direction = direction;
			this.scale = scale;
			normalize(startAngle, endAngle);
		}

		override public IEnumerable<VertexData> Vertices()
		{
			if (!isInitialized)
			{
				normalize(startAngle, endAngle);
			}

			VertexData vertexData = new VertexData();
			vertexData.command = FlagsAndCommand.CommandMoveTo;
			if (direction == Direction.CounterClockWise)
			{
				vertexData.position.x = origin.x + Math.Cos(startAngle) * radius.x;
				vertexData.position.y = origin.y + Math.Sin(startAngle) * radius.y;
				yield return vertexData;

				vertexData.command = FlagsAndCommand.CommandLineTo;
				double angle = startAngle;
				while (angle < endAngle - flatenDeltaAngle / 4)
				{
					vertexData.position.x = origin.x + Math.Cos(angle) * radius.x;
					vertexData.position.y = origin.y + Math.Sin(angle) * radius.y;
					yield return vertexData;

					angle += flatenDeltaAngle;
				}

				vertexData.position.x = origin.x + Math.Cos(endAngle) * radius.x;
				vertexData.position.y = origin.y + Math.Sin(endAngle) * radius.y;
				yield return vertexData;
			}
			else
			{
				vertexData.position.x = origin.x + Math.Cos(endAngle) * radius.x;
				vertexData.position.y = origin.y + Math.Sin(endAngle) * radius.y;
				yield return vertexData;

				vertexData.command = FlagsAndCommand.CommandLineTo;
				double angle = endAngle;
				while (angle > startAngle + flatenDeltaAngle / 4)
				{
					vertexData.position.x = origin.x + Math.Cos(angle) * radius.x;
					vertexData.position.y = origin.y + Math.Sin(angle) * radius.y;
					yield return vertexData;

					angle -= flatenDeltaAngle;
				}

				vertexData.position.x = origin.x + Math.Cos(startAngle) * radius.x;
				vertexData.position.y = origin.y + Math.Sin(startAngle) * radius.y;
				yield return vertexData;
			}

			vertexData.command = FlagsAndCommand.CommandStop;
			yield return vertexData;
		}

		private void normalize(double startAngle, double endAngle)
		{
			double averageRadius = (Math.Abs(radius.x) + Math.Abs(radius.y)) / 2;
			flatenDeltaAngle = Math.Acos(averageRadius / (averageRadius + 0.125 / scale)) * 2;
			while (endAngle < startAngle)
			{
				endAngle += Math.PI * 2.0;
			}

			this.startAngle = startAngle;
			this.endAngle = endAngle;
			isInitialized = true;
		}
	}
}