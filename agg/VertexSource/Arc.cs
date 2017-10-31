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
		}

		override public IEnumerable<VertexData> Vertices()
		{
			double averageRadius = (Math.Abs(radius.X) + Math.Abs(radius.Y)) / 2;
			flatenDeltaAngle = Math.Acos(averageRadius / (averageRadius + 0.125 / scale)) * 2;
			while (endAngle < startAngle)
			{
				endAngle += Math.PI * 2.0;
			}

			VertexData vertexData = new VertexData();
			vertexData.command = FlagsAndCommand.CommandMoveTo;
			if (direction == Direction.CounterClockWise)
			{
				vertexData.position.X = origin.X + Math.Cos(startAngle) * radius.X;
				vertexData.position.Y = origin.Y + Math.Sin(startAngle) * radius.Y;
				yield return vertexData;

				vertexData.command = FlagsAndCommand.CommandLineTo;
				double angle = startAngle;
				int numSteps = (int)((endAngle - startAngle) / flatenDeltaAngle);
                for (int i=0; i<=numSteps; i++)
				{
					vertexData.position.X = origin.X + Math.Cos(angle) * radius.X;
					vertexData.position.Y = origin.Y + Math.Sin(angle) * radius.Y;
					yield return vertexData;

					angle += flatenDeltaAngle;
				}

				vertexData.position.X = origin.X + Math.Cos(endAngle) * radius.X;
				vertexData.position.Y = origin.Y + Math.Sin(endAngle) * radius.Y;
				yield return vertexData;
			}
			else
			{
				vertexData.position.X = origin.X + Math.Cos(endAngle) * radius.X;
				vertexData.position.Y = origin.Y + Math.Sin(endAngle) * radius.Y;
				yield return vertexData;

				vertexData.command = FlagsAndCommand.CommandLineTo;
				double angle = endAngle;
				int numSteps = (int)((endAngle - startAngle) / flatenDeltaAngle);
				for (int i = 0; i <= numSteps; i++)
				{
					vertexData.position.X = origin.X + Math.Cos(angle) * radius.X;
					vertexData.position.Y = origin.Y + Math.Sin(angle) * radius.Y;
					yield return vertexData;

					angle -= flatenDeltaAngle;
				}

				vertexData.position.X = origin.X + Math.Cos(startAngle) * radius.X;
				vertexData.position.Y = origin.Y + Math.Sin(startAngle) * radius.Y;
				yield return vertexData;
			}

			vertexData.command = FlagsAndCommand.CommandStop;
			yield return vertexData;
		}
	}
}