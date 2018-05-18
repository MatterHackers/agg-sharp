﻿/*
The MIT License (MIT)

Copyright (c) 2014 Sebastian Loncar

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

See:
D. H. Laidlaw, W. B. Trumbore, and J. F. Hughes.
"Constructive Solid Geometry for Polyhedral Objects"
SIGGRAPH Proceedings, 1986, p.161.

original author: Danilo Balby Silva Castanheira (danbalby@yahoo.com)

Ported from Java to C# by Sebastian Loncar, Web: http://loncar.de
Optomized and refactored by: Lars Brubaker (larsbrubaker@matterhackers.com)
Project: https://github.com/MatterHackers/agg-sharp (an included library)
*/

using MatterHackers.VectorMath;
using System;

namespace Net3dBool
{
	/// <summary>
	/// Representation of a 3d line or a ray(represented by a direction and a point).
	/// </summary>
	public class Line
	{
		/// <summary>
		/// tolerance value to test equalities
		/// </summary>
		private readonly static double EqualityTolerance = 1e-10f;
		private static Random rnd = new Random();
		private Vector3 startPoint;

		/// <summary>
		/// Constructor for a line.The line created is the intersection between two planes
		/// </summary>
		/// <param name="face1">face representing one of the planes</param>
		/// <param name="face2">face representing one of the planes</param>
		public Line(CsgFace face1, CsgFace face2)
		{
			Vector3 normalFace1 = face1.Normal;
			Vector3 normalFace2 = face2.Normal;

			//direction: cross product of the faces normals
			Direction = Vector3.Cross(normalFace1, normalFace2);

			//if direction lenght is not zero (the planes aren't parallel )...
			if (!(Direction.Length < EqualityTolerance))
			{
				//getting a line point, zero is set to a coordinate whose direction
				//component isn't zero (line intersecting its origin plan)
				startPoint = new Vector3();
				double d1 = -(normalFace1.X * face1.v1.Position.X + normalFace1.Y * face1.v1.Position.Y + normalFace1.Z * face1.v1.Position.Z);
				double d2 = -(normalFace2.X * face2.v1.Position.X + normalFace2.Y * face2.v1.Position.Y + normalFace2.Z * face2.v1.Position.Z);
				if (Math.Abs(Direction.X) > EqualityTolerance)
				{
					startPoint.X = 0;
					startPoint.Y = (d2 * normalFace1.Z - d1 * normalFace2.Z) / Direction.X;
					startPoint.Z = (d1 * normalFace2.Y - d2 * normalFace1.Y) / Direction.X;
				}
				else if (Math.Abs(Direction.Y) > EqualityTolerance)
				{
					startPoint.X = (d1 * normalFace2.Z - d2 * normalFace1.Z) / Direction.Y;
					startPoint.Y = 0;
					startPoint.Z = (d2 * normalFace1.X - d1 * normalFace2.X) / Direction.Y;
				}
				else
				{
					startPoint.X = (d2 * normalFace1.Y - d1 * normalFace2.Y) / Direction.Z;
					startPoint.Y = (d1 * normalFace2.X - d2 * normalFace1.X) / Direction.Z;
					startPoint.Z = 0;
				}
			}

			Direction.Normalize();
		}

		/// <summary>
		/// Constructor for a ray
		/// </summary>
		/// <param name="direction">direction ray</param>
		/// <param name="point">beginning of the ray</param>
		public Line(Vector3 direction, Vector3 point)
		{
			this.Direction = direction;
			this.startPoint = point;
			direction.Normalize();
		}

		private Line()
		{
		}

		/// <summary>
		/// line direction
		/// </summary>
		public Vector3 Direction { get; private set; }

		public Line Clone()
		{
			Line clone = new Line();
			clone.Direction = Direction;
			clone.startPoint = startPoint;
			return clone;
		}

		/// <summary>
		/// Computes the point resulting from the intersection with another line
		/// </summary>
		/// <param name="otherLine">the other line to apply the intersection. The lines are supposed to intersect</param>
		/// <returns>point resulting from the intersection. If the point coundn't be obtained, return null</returns>
		public Vector3 ComputeLineIntersection(Line otherLine)
		{
			//x = x1 + a1*t = x2 + b1*s
			//y = y1 + a2*t = y2 + b2*s
			//z = z1 + a3*t = z2 + b3*s

			Vector3 linePoint = otherLine.GetPoint();
			Vector3 lineDirection = otherLine.Direction;

			double t;
			if (Math.Abs(Direction.Y * lineDirection.X - Direction.X * lineDirection.Y) > EqualityTolerance)
			{
				t = (-startPoint.Y * lineDirection.X + linePoint.Y * lineDirection.X + lineDirection.Y * startPoint.X - lineDirection.Y * linePoint.X) / (Direction.Y * lineDirection.X - Direction.X * lineDirection.Y);
			}
			else if (Math.Abs(-Direction.X * lineDirection.Z + Direction.Z * lineDirection.X) > EqualityTolerance)
			{
				t = -(-lineDirection.Z * startPoint.X + lineDirection.Z * linePoint.X + lineDirection.X * startPoint.Z - lineDirection.X * linePoint.Z) / (-Direction.X * lineDirection.Z + Direction.Z * lineDirection.X);
			}
			else if (Math.Abs(-Direction.Z * lineDirection.Y + Direction.Y * lineDirection.Z) > EqualityTolerance)
			{
				t = (startPoint.Z * lineDirection.Y - linePoint.Z * lineDirection.Y - lineDirection.Z * startPoint.Y + lineDirection.Z * linePoint.Y) / (-Direction.Z * lineDirection.Y + Direction.Y * lineDirection.Z);
			}
			else
			{
#if DEBUG
				throw new InvalidOperationException();
#else
				return Vector3.Zero;
#endif
			}

			double x = startPoint.X + Direction.X * t;
			double y = startPoint.Y + Direction.Y * t;
			double z = startPoint.Z + Direction.Z * t;

			return new Vector3(x, y, z);
		}

		/// <summary>
		/// Compute the point resulting from the intersection with a plane
		/// </summary>
		/// <param name="normal">the plane normal</param>
		/// <param name="planePoint">a plane point.</param>
		/// <returns>intersection point.If they don't intersect, return null</returns>
		public Vector3 ComputePlaneIntersection(Plane plane)
		{
			double distanceToStartFromOrigin = Vector3.Dot(plane.PlaneNormal, startPoint);

			double distanceFromPlane = distanceToStartFromOrigin - plane.DistanceToPlaneFromOrigin;
			double denominator = Vector3.Dot(plane.PlaneNormal, Direction);

			if (Math.Abs(denominator) < EqualityTolerance)
			{
				//if line is paralel to the plane...
				if (Math.Abs(distanceFromPlane) < EqualityTolerance)
				{
					//if line is contained in the plane...
					return startPoint;
				}
				else
				{
					return Vector3.PositiveInfinity;
				}
			}
			else // line intercepts the plane...
			{
				double t = -distanceFromPlane / denominator;
				Vector3 resultPoint = new Vector3();
				resultPoint.X = startPoint.X + t * Direction.X;
				resultPoint.Y = startPoint.Y + t * Direction.Y;
				resultPoint.Z = startPoint.Z + t * Direction.Z;

				return resultPoint;
			}
		}

		/// <summary>
		/// Computes the distance from the line point to another point
		/// </summary>
		/// <param name="otherPoint">the point to compute the distance from the line point. The point is supposed to be on the same line.</param>
		/// <returns>points distance. If the point submitted is behind the direction, the distance is negative</returns>
		public double ComputePointToPointDistance(Vector3 otherPoint)
		{
			double distance = (otherPoint - startPoint).Length;
			Vector3 vec = new Vector3(otherPoint.X - startPoint.X, otherPoint.Y - startPoint.Y, otherPoint.Z - startPoint.Z);
			vec.Normalize();
			if (Vector3.Dot(vec, Direction) < 0)
			{
				return -distance;
			}
			else
			{
				return distance;
			}
		}

		public Vector3 GetPoint()
		{
			return startPoint;
		}

		/// <summary>
		/// Changes slightly the line direction
		/// </summary>
		public void PerturbDirection()
		{
			Vector3 perturbedDirection = Direction;
			perturbedDirection.X += 1e-5 * Random();
			perturbedDirection.Y += 1e-5 * Random();
			perturbedDirection.Z += 1e-5 * Random();

			Direction = perturbedDirection;
		}

		public void SetDirection(Vector3 direction)
		{
			this.Direction = direction;
		}

		public void SetPoint(Vector3 point)
		{
			this.startPoint = point;
		}

		public String toString()
		{
			return "Direction: " + Direction.ToString() + "\nPoint: " + startPoint.ToString();
		}

		private static double Random()
		{
			return rnd.NextDouble();
		}
	}
}