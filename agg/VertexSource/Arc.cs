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

using FlagsAndCommand = MatterHackers.Agg.FlagsAndCommand;

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

		private Vector2 origin;

		private Vector2 radius;

		public double ResolutionScale { get; set; } = 1;

		private double startAngle;

		public Arc()
		{
		}

		public Arc(double originX, double originY,
			 double radiusX, double radiusY,
			 double startAngle, double endAngle,
			 Direction direction = Direction.CounterClockWise,
             int numSegments = 0)
        {
			init(originX, originY, radiusX, radiusY, startAngle, endAngle, direction, numSegments);
		}

		public Arc(Vector2 origin,
			 Vector2 radius,
			 double startAngle,
			 double endAngle,
			 Direction direction = Direction.CounterClockWise)
		{
			init(origin, radius, startAngle, endAngle, direction);
		}

		public Arc(Vector2 origin,
			 double radius,
			 double startAngle,
			 double endAngle,
			 Direction direction = Direction.CounterClockWise)
			: this(origin, new Vector2(radius, radius), startAngle, endAngle, direction)
		{
		}

		public enum Direction
		{
			ClockWise,
			CounterClockWise,
		}

		public void init(double originX, double originY,
			double radiusX, double radiusY,
			double startAngle, double endAngle,
			Direction direction = Direction.CounterClockWise,
            int numSegments = 0)
		{
			init(new Vector2(originX, originY), new Vector2(radiusX, radiusY), startAngle, endAngle, direction, numSegments);
		}

		public void init(Vector2 origin,
			Vector2 radius,
			double startAngle, double endAngle,
			Direction direction = Direction.CounterClockWise,
            int numSegments = 0)
		{
            this.NumSegments = numSegments;
            this.origin = origin;
			this.radius = radius;
			this.startAngle = startAngle;
			this.endAngle = endAngle;
			this.direction = direction;
		}

        /// <summary>
		/// This is the number of segments that will be used in each turn. Set to 0 to use the default of an angle approximation.
		/// </summary>
        public int NumSegments { get; set; } = 0;

        public override IEnumerable<VertexData> Vertices()
		{
            if (NumSegments == 0)
			{
				return DeltaVertices();
            }
			else
			{
                return StepVertices();
            }
        }

        private IEnumerable<VertexData> DeltaVertices()
        {
            double averageRadius = (Math.Abs(radius.X) + Math.Abs(radius.Y)) / 2;
			var flattenedDeltaAngle = Math.Acos(averageRadius / (averageRadius + 0.125 / ResolutionScale)) * 2;
			while (endAngle < startAngle)
			{
				endAngle += Math.PI * 2.0;
			}

			VertexData vertexData = new VertexData();
			vertexData.Command = FlagsAndCommand.MoveTo;
			if (direction == Direction.CounterClockWise)
			{
				vertexData.Position = new Vector2(origin.X + Math.Cos(startAngle) * radius.X, origin.Y + Math.Sin(startAngle) * radius.Y);
				yield return vertexData;

				vertexData.Command = FlagsAndCommand.LineTo;
				double angle = startAngle;
				int numSteps = (int)((endAngle - startAngle) / flattenedDeltaAngle);

				for (int i = 0; i <= numSteps; i++)
				{
					if (angle < endAngle)
					{
						vertexData.Position = new Vector2(origin.X + Math.Cos(angle) * radius.X, origin.Y + Math.Sin(angle) * radius.Y);
						yield return vertexData;

						angle += flattenedDeltaAngle;
					}
				}

				vertexData.Position = new Vector2(origin.X + Math.Cos(endAngle) * radius.X, origin.Y + Math.Sin(endAngle) * radius.Y);
				yield return vertexData;
			}
			else
			{
				vertexData.Position = new Vector2(origin.X + Math.Cos(endAngle) * radius.X, origin.Y + Math.Sin(endAngle) * radius.Y);
				yield return vertexData;

				vertexData.Command = FlagsAndCommand.LineTo;
				double angle = endAngle;
				int numSteps = (int)((endAngle - startAngle) / flattenedDeltaAngle);
				for (int i = 0; i <= numSteps; i++)
				{
					vertexData.Position = new Vector2(origin.X + Math.Cos(angle) * radius.X, origin.Y + Math.Sin(angle) * radius.Y);
					yield return vertexData;

					angle -= flattenedDeltaAngle;
				}

				vertexData.Position = new Vector2(origin.X + Math.Cos(startAngle) * radius.X, origin.Y + Math.Sin(startAngle) * radius.Y);
				yield return vertexData;
			}

			vertexData.Command = FlagsAndCommand.Stop;
			yield return vertexData;
		}

        private IEnumerable<VertexData> StepVertices()
        {
            while (endAngle < startAngle)
            {
                endAngle += Math.PI * 2.0;
            }

            var flattenedDeltaAngle = (endAngle - startAngle) / NumSegments;

            VertexData vertexData = new VertexData();
            vertexData.Command = FlagsAndCommand.MoveTo;
            if (direction == Direction.CounterClockWise)
            {
                vertexData.Position = new Vector2(origin.X + Math.Cos(startAngle) * radius.X, origin.Y + Math.Sin(startAngle) * radius.Y);
                yield return vertexData;

                vertexData.Command = FlagsAndCommand.LineTo;
                double angle = startAngle;

                for (int i = 0; i <= NumSegments; i++)
                {
                    if (angle < endAngle)
                    {
                        vertexData.Position = new Vector2(origin.X + Math.Cos(angle) * radius.X, origin.Y + Math.Sin(angle) * radius.Y);
                        yield return vertexData;

                        angle += flattenedDeltaAngle;
                    }
                }

                vertexData.Position = new Vector2(origin.X + Math.Cos(endAngle) * radius.X, origin.Y + Math.Sin(endAngle) * radius.Y);
                yield return vertexData;
            }
            else
            {
                vertexData.Position = new Vector2(origin.X + Math.Cos(endAngle) * radius.X, origin.Y + Math.Sin(endAngle) * radius.Y);
                yield return vertexData;

                vertexData.Command = FlagsAndCommand.LineTo;
                double angle = endAngle;
                int numSteps = (int)((endAngle - startAngle) / flattenedDeltaAngle);
                for (int i = 0; i <= numSteps; i++)
                {
                    vertexData.Position = new Vector2(origin.X + Math.Cos(angle) * radius.X, origin.Y + Math.Sin(angle) * radius.Y);
                    yield return vertexData;

                    angle -= flattenedDeltaAngle;
                }

                vertexData.Position = new Vector2(origin.X + Math.Cos(startAngle) * radius.X, origin.Y + Math.Sin(startAngle) * radius.Y);
                yield return vertexData;
            }

            vertexData.Command = FlagsAndCommand.Stop;
            yield return vertexData;
        }
    }
}