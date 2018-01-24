/*
Copyright (c) 2014, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;

namespace MatterHackers.VectorMath
{
	public class AxisAlignedBoundingBox
	{
		public static AxisAlignedBoundingBox Empty { get; } = new AxisAlignedBoundingBox(Vector3.PositiveInfinity, Vector3.NegativeInfinity);
		public static AxisAlignedBoundingBox Zero { get; } = new AxisAlignedBoundingBox(Vector3.Zero, Vector3.Zero);

		public Vector3 minXYZ;
		public Vector3 maxXYZ;

		public AxisAlignedBoundingBox(Vector3 minXYZ, Vector3 maxXYZ)
		{
			this.minXYZ = minXYZ;
			this.maxXYZ = maxXYZ;
		}

		public AxisAlignedBoundingBox(IList<Vector3> verticesPoints)
		{
			if (verticesPoints.Count > 0)
			{
				minXYZ = verticesPoints[0];
				maxXYZ = verticesPoints[0];
				for(int i=1; i<verticesPoints.Count; i++)
				{
					ExpandToInclude(verticesPoints[i]);
				}
			}
		}

		public Vector3 Size => maxXYZ - minXYZ;
			
		public double XSize => maxXYZ.X - minXYZ.X;

		public double YSize => maxXYZ.Y - minXYZ.Y;

		public double ZSize => maxXYZ.Z - minXYZ.Z;

		


		public bool Equals(AxisAlignedBoundingBox bounds, double equalityTolerance = 0)
		{
			return this.minXYZ.Equals(bounds.minXYZ, equalityTolerance) && this.maxXYZ.Equals(bounds.maxXYZ, equalityTolerance);
		}

		public AxisAlignedBoundingBox NewTransformed(Matrix4X4 transform)
		{
			Vector3[] boundsVerts = new Vector3[8];
			boundsVerts[0] = new Vector3(this[0][0], this[0][1], this[0][2]);
			boundsVerts[1] = new Vector3(this[0][0], this[0][1], this[1][2]);
			boundsVerts[2] = new Vector3(this[0][0], this[1][1], this[0][2]);
			boundsVerts[3] = new Vector3(this[0][0], this[1][1], this[1][2]);
			boundsVerts[4] = new Vector3(this[1][0], this[0][1], this[0][2]);
			boundsVerts[5] = new Vector3(this[1][0], this[0][1], this[1][2]);
			boundsVerts[6] = new Vector3(this[1][0], this[1][1], this[0][2]);
			boundsVerts[7] = new Vector3(this[1][0], this[1][1], this[1][2]);

			Vector3.Transform(boundsVerts, transform);

			Vector3 newMin = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
			Vector3 newMax = new Vector3(double.MinValue, double.MinValue, double.MinValue);

			for (int i = 0; i < 8; i++)
			{
				newMin.X = Math.Min(newMin.X, boundsVerts[i].X);
				newMin.Y = Math.Min(newMin.Y, boundsVerts[i].Y);
				newMin.Z = Math.Min(newMin.Z, boundsVerts[i].Z);

				newMax.X = Math.Max(newMax.X, boundsVerts[i].X);
				newMax.Y = Math.Max(newMax.Y, boundsVerts[i].Y);
				newMax.Z = Math.Max(newMax.Z, boundsVerts[i].Z);
			}

			return new AxisAlignedBoundingBox(newMin, newMax);
		}

		public void Expand(int amount)
		{
			minXYZ.X -= amount;
			minXYZ.Y -= amount;
			minXYZ.Z -= amount;

			maxXYZ.X += amount;
			maxXYZ.Y += amount;
			maxXYZ.Z += amount;
		}

		public void ExpandToInclude(Vector3 position)
		{
			minXYZ.X = Math.Min(minXYZ.X, position.X);
			minXYZ.Y = Math.Min(minXYZ.Y, position.Y);
			minXYZ.Z = Math.Min(minXYZ.Z, position.Z);

			maxXYZ.X = Math.Max(maxXYZ.X, position.X);
			maxXYZ.Y = Math.Max(maxXYZ.Y, position.Y);
			maxXYZ.Z = Math.Max(maxXYZ.Z, position.Z);
		}

		/// <summary>
		/// Geth the corners by quadrant of the bottom
		/// </summary>
		/// <param name="quadrantIndex"></param>
		public Vector3 GetBottomCorner(int quadrantIndex)
		{
			switch(quadrantIndex)
			{
				case 0:
					return new Vector3(maxXYZ.X, maxXYZ.Y, minXYZ.Z);

				case 1:
					return new Vector3(minXYZ.X, maxXYZ.Y, minXYZ.Z);

				case 2:
					return new Vector3(minXYZ.X, minXYZ.Y, minXYZ.Z);

				case 3:
					return new Vector3(maxXYZ.X, minXYZ.Y, minXYZ.Z);
			}

			return Vector3.Zero;
		}

		/// <summary>
		/// Get the corners by quadrant of the top
		/// </summary>
		/// <param name="quadrantIndex"></param>
		public Vector3 GetTopCorner(int quadrantIndex)
		{
			switch (quadrantIndex)
			{
				case 0:
					return new Vector3(maxXYZ.X, maxXYZ.Y, maxXYZ.Z);

				case 1:
					return new Vector3(minXYZ.X, maxXYZ.Y, maxXYZ.Z);

				case 2:
					return new Vector3(minXYZ.X, minXYZ.Y, maxXYZ.Z);

				case 3:
					return new Vector3(maxXYZ.X, minXYZ.Y, maxXYZ.Z);
			}

			return Vector3.Zero;
		}

		public Vector3 Center => (minXYZ + maxXYZ) / 2;

		/// <summary>
		/// This is the computation cost of doing an intersection with the given type.
		/// Attempt to give it in average CPU cycles for the intersection.
		/// </summary>
		/// <returns></returns>
		public static double GetIntersectCost()
		{
			// it would be great to try and measure this more accurately.  This is a guess from looking at the intersect function.
			return 132;
		}

		public Vector3 GetCenter() => (minXYZ + maxXYZ) * .5;

		public double GetCenterX() => (minXYZ.X + maxXYZ.X) * .5;
		
		private double volumeCache = 0;

		public double GetVolume()
		{
			if (volumeCache == 0)
			{
				volumeCache = (maxXYZ.X - minXYZ.X) * (maxXYZ.Y - minXYZ.Y) * (maxXYZ.Z - minXYZ.Z);
			}

			return volumeCache;
		}

		private double surfaceAreaCache = 0;

		public double GetSurfaceArea()
		{
			if (surfaceAreaCache == 0)
			{
				double frontAndBack = (maxXYZ.X - minXYZ.X) * (maxXYZ.Z - minXYZ.Z) * 2;
				double leftAndRight = (maxXYZ.Y - minXYZ.Y) * (maxXYZ.Z - minXYZ.Z) * 2;
				double topAndBottom = (maxXYZ.X - minXYZ.X) * (maxXYZ.Y - minXYZ.Y) * 2;
				surfaceAreaCache = frontAndBack + leftAndRight + topAndBottom;
			}

			return surfaceAreaCache;
		}

		public Vector3 this[int index]
		{
			get
			{
				if (index == 0)
				{
					return minXYZ;
				}
				else if (index == 1)
				{
					return maxXYZ;
				}
				else
				{
					throw new IndexOutOfRangeException();
				}
			}
		}

		public static AxisAlignedBoundingBox operator +(AxisAlignedBoundingBox A, AxisAlignedBoundingBox B)
		{
			return Union(A, B);
		}

		public static AxisAlignedBoundingBox Union(AxisAlignedBoundingBox boundsA, AxisAlignedBoundingBox boundsB)
		{
			Vector3 minXYZ = new Vector3(
				Math.Min(boundsA.minXYZ.X, boundsB.minXYZ.X),
				Math.Min(boundsA.minXYZ.Y, boundsB.minXYZ.Y),
				Math.Min(boundsA.minXYZ.Z, boundsB.minXYZ.Z));

			Vector3 maxXYZ = new Vector3(
				Math.Max(boundsA.maxXYZ.X, boundsB.maxXYZ.X),
				Math.Max(boundsA.maxXYZ.Y, boundsB.maxXYZ.Y),
				Math.Max(boundsA.maxXYZ.Z, boundsB.maxXYZ.Z));

			return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
		}

		public bool Intersects(AxisAlignedBoundingBox bounds)
		{
			Vector3 intersectMinXYZ = new Vector3(
				Math.Max(minXYZ.X, bounds.minXYZ.X),
				Math.Max(minXYZ.Y, bounds.minXYZ.Y),
				Math.Max(minXYZ.Z, bounds.minXYZ.Z));

			Vector3 intersectMaxXYZ = new Vector3(
				Math.Max(minXYZ.X, Math.Min(maxXYZ.X, bounds.maxXYZ.X)),
				Math.Max(minXYZ.Y, Math.Min(maxXYZ.Y, bounds.maxXYZ.Y)),
				Math.Max(minXYZ.Z, Math.Min(maxXYZ.Z, bounds.maxXYZ.Z)));

			Vector3 delta = intersectMaxXYZ - intersectMinXYZ;
			if (delta.X >= 0 && delta.Y >= 0 && delta.Z >= 0)
			{
				return true;
			}

			return false;
		}

		public static AxisAlignedBoundingBox Intersection(AxisAlignedBoundingBox boundsA, AxisAlignedBoundingBox boundsB)
		{
			Vector3 minXYZ = new Vector3(
				Math.Max(boundsA.minXYZ.X, boundsB.minXYZ.X),
				Math.Max(boundsA.minXYZ.Y, boundsB.minXYZ.Y),
				Math.Max(boundsA.minXYZ.Z, boundsB.minXYZ.Z));

			Vector3 maxXYZ = new Vector3(
				Math.Max(minXYZ.X, Math.Min(boundsA.maxXYZ.X, boundsB.maxXYZ.X)),
				Math.Max(minXYZ.Y, Math.Min(boundsA.maxXYZ.Y, boundsB.maxXYZ.Y)),
				Math.Max(minXYZ.Z, Math.Min(boundsA.maxXYZ.Z, boundsB.maxXYZ.Z)));

			return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
		}

		public static AxisAlignedBoundingBox Union(AxisAlignedBoundingBox bounds, Vector3 vertex)
		{
			Vector3 minXYZ = Vector3.Zero;
			minXYZ.X = Math.Min(bounds.minXYZ.X, vertex.X);
			minXYZ.Y = Math.Min(bounds.minXYZ.Y, vertex.Y);
			minXYZ.Z = Math.Min(bounds.minXYZ.Z, vertex.Z);

			Vector3 maxXYZ = Vector3.Zero;
			maxXYZ.X = Math.Max(bounds.maxXYZ.X, vertex.X);
			maxXYZ.Y = Math.Max(bounds.maxXYZ.Y, vertex.Y);
			maxXYZ.Z = Math.Max(bounds.maxXYZ.Z, vertex.Z);

			return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
		}

		public void Clamp(ref Vector3 positionToClamp)
		{
			if (positionToClamp.X < minXYZ.X)
			{
				positionToClamp.X = minXYZ.X;
			}
			else if (positionToClamp.X > maxXYZ.X)
			{
				positionToClamp.X = maxXYZ.X;
			}

			if (positionToClamp.Y < minXYZ.Y)
			{
				positionToClamp.Y = minXYZ.Y;
			}
			else if (positionToClamp.Y > maxXYZ.Y)
			{
				positionToClamp.Y = maxXYZ.Y;
			}

			if (positionToClamp.Z < minXYZ.Z)
			{
				positionToClamp.Z = minXYZ.Z;
			}
			else if (positionToClamp.Z > maxXYZ.Z)
			{
				positionToClamp.Z = maxXYZ.Z;
			}
		}

		public bool Contains(Vector3 position, double errorRange = .001)
		{
			if (this.minXYZ.X <= position.X + errorRange
				&& this.maxXYZ.X >= position.X - errorRange
				&& this.minXYZ.Y <= position.Y + errorRange
				&& this.maxXYZ.Y >= position.Y - errorRange
				&& this.minXYZ.Z <= position.Z + errorRange
				&& this.maxXYZ.Z >= position.Z - errorRange)
			{
				return true;
			}

			return false;
		}

		public bool Contains(AxisAlignedBoundingBox bounds)
		{
			if (this.minXYZ.X <= bounds.minXYZ.X
				&& this.maxXYZ.X >= bounds.maxXYZ.X
				&& this.minXYZ.Y <= bounds.minXYZ.Y
				&& this.maxXYZ.Y >= bounds.maxXYZ.Y
				&& this.minXYZ.Z <= bounds.minXYZ.Z
				&& this.maxXYZ.Z >= bounds.maxXYZ.Z)
			{
				return true;
			}

			return false;
		}

		public override string ToString()
		{
			return string.Format("min {0} - max {1}", minXYZ, maxXYZ);
		}
	}
}