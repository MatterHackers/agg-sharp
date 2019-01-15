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
using Newtonsoft.Json;

namespace MatterHackers.VectorMath
{
	public class AxisAlignedBoundingBox
	{
		public static AxisAlignedBoundingBox Empty() { return new AxisAlignedBoundingBox(Vector3.PositiveInfinity, Vector3.NegativeInfinity); }
		public static AxisAlignedBoundingBox Zero() { return new AxisAlignedBoundingBox(Vector3.Zero, Vector3.Zero); }

		public Vector3 MinXYZ;
		public Vector3 MaxXYZ;

		public AxisAlignedBoundingBox()
		{
		}

		public AxisAlignedBoundingBox(double minX, double minY, double minZ,
			double maxX, double maxY, double maxZ)
			: this(new Vector3(minX, minY, minZ), new Vector3(maxX, maxY, maxZ))
		{
		}

		public AxisAlignedBoundingBox(Vector3 minXYZ, Vector3 maxXYZ)
		{
			this.MinXYZ = minXYZ;
			this.MaxXYZ = maxXYZ;
		}

		public AxisAlignedBoundingBox(IList<Vector3> verticesPoints)
		{
			if (verticesPoints.Count > 0)
			{
				MinXYZ = verticesPoints[0];
				MaxXYZ = verticesPoints[0];
				for(int i=1; i<verticesPoints.Count; i++)
				{
					ExpandToInclude(verticesPoints[i]);
				}
			}
		}

		[JsonIgnore]
		public Vector3 Center => (MinXYZ + MaxXYZ) / 2;

		public Vector3 GetCenter() => (MinXYZ + MaxXYZ) * .5;

		public double GetCenterX() => (MinXYZ.X + MaxXYZ.X) * .5;

		[JsonIgnore]
		public Vector3 Size => MaxXYZ - MinXYZ;

		[JsonIgnore]
		public double XSize => MaxXYZ.X - MinXYZ.X;

		[JsonIgnore]
		public double YSize => MaxXYZ.Y - MinXYZ.Y;

		[JsonIgnore]
		public double ZSize => MaxXYZ.Z - MinXYZ.Z;

		public bool Equals(AxisAlignedBoundingBox bounds, double equalityTolerance = 0)
		{
			return this.MinXYZ.Equals(bounds.MinXYZ, equalityTolerance) && this.MaxXYZ.Equals(bounds.MaxXYZ, equalityTolerance);
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

			Vector3Ex.Transform(boundsVerts, transform);

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
			MinXYZ.X -= amount;
			MinXYZ.Y -= amount;
			MinXYZ.Z -= amount;

			MaxXYZ.X += amount;
			MaxXYZ.Y += amount;
			MaxXYZ.Z += amount;
		}

		public void ExpandToInclude(Vector3 position)
		{
			MinXYZ.X = Math.Min(MinXYZ.X, position.X);
			MinXYZ.Y = Math.Min(MinXYZ.Y, position.Y);
			MinXYZ.Z = Math.Min(MinXYZ.Z, position.Z);

			MaxXYZ.X = Math.Max(MaxXYZ.X, position.X);
			MaxXYZ.Y = Math.Max(MaxXYZ.Y, position.Y);
			MaxXYZ.Z = Math.Max(MaxXYZ.Z, position.Z);
		}

		public void ExpandToInclude(Vector3Float position)
		{
			MinXYZ.X = Math.Min(MinXYZ.X, position.X);
			MinXYZ.Y = Math.Min(MinXYZ.Y, position.Y);
			MinXYZ.Z = Math.Min(MinXYZ.Z, position.Z);

			MaxXYZ.X = Math.Max(MaxXYZ.X, position.X);
			MaxXYZ.Y = Math.Max(MaxXYZ.Y, position.Y);
			MaxXYZ.Z = Math.Max(MaxXYZ.Z, position.Z);
		}

		/// <summary>
		/// Gets the corners by quadrant of the bottom
		/// </summary>
		/// <param name="quadrantIndex"></param>
		public Vector3 GetBottomCorner(int quadrantIndex)
		{
			switch(quadrantIndex)
			{
				case 0:
					return new Vector3(MaxXYZ.X, MaxXYZ.Y, MinXYZ.Z);

				case 1:
					return new Vector3(MinXYZ.X, MaxXYZ.Y, MinXYZ.Z);

				case 2:
					return new Vector3(MinXYZ.X, MinXYZ.Y, MinXYZ.Z);

				case 3:
					return new Vector3(MaxXYZ.X, MinXYZ.Y, MinXYZ.Z);
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
					return new Vector3(MaxXYZ.X, MaxXYZ.Y, MaxXYZ.Z);

				case 1:
					return new Vector3(MinXYZ.X, MaxXYZ.Y, MaxXYZ.Z);

				case 2:
					return new Vector3(MinXYZ.X, MinXYZ.Y, MaxXYZ.Z);

				case 3:
					return new Vector3(MaxXYZ.X, MinXYZ.Y, MaxXYZ.Z);
			}

			return Vector3.Zero;
		}

		/// <summary>
		/// AABB defines our unit value for Intersection cost at 1. All other types
		/// should attempt to calculate their cost in CPU relative to this.
		/// </summary>
		/// <returns></returns>
		public static double GetIntersectCost()
		{
			return 1;
		}
	
		private double volumeCache = 0;

		public double GetVolume()
		{
			if (volumeCache == 0)
			{
				volumeCache = (MaxXYZ.X - MinXYZ.X) * (MaxXYZ.Y - MinXYZ.Y) * (MaxXYZ.Z - MinXYZ.Z);
			}

			return volumeCache;
		}

		private double surfaceAreaCache = 0;

		public double GetSurfaceArea()
		{
			if (surfaceAreaCache == 0)
			{
				double frontAndBack = (MaxXYZ.X - MinXYZ.X) * (MaxXYZ.Z - MinXYZ.Z) * 2;
				double leftAndRight = (MaxXYZ.Y - MinXYZ.Y) * (MaxXYZ.Z - MinXYZ.Z) * 2;
				double topAndBottom = (MaxXYZ.X - MinXYZ.X) * (MaxXYZ.Y - MinXYZ.Y) * 2;
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
					return MinXYZ;
				}
				else if (index == 1)
				{
					return MaxXYZ;
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
				Math.Min(boundsA.MinXYZ.X, boundsB.MinXYZ.X),
				Math.Min(boundsA.MinXYZ.Y, boundsB.MinXYZ.Y),
				Math.Min(boundsA.MinXYZ.Z, boundsB.MinXYZ.Z));

			Vector3 maxXYZ = new Vector3(
				Math.Max(boundsA.MaxXYZ.X, boundsB.MaxXYZ.X),
				Math.Max(boundsA.MaxXYZ.Y, boundsB.MaxXYZ.Y),
				Math.Max(boundsA.MaxXYZ.Z, boundsB.MaxXYZ.Z));

			return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
		}

		public bool Intersects(AxisAlignedBoundingBox bounds)
		{
			Vector3 intersectMinXYZ = new Vector3(
				Math.Max(MinXYZ.X, bounds.MinXYZ.X),
				Math.Max(MinXYZ.Y, bounds.MinXYZ.Y),
				Math.Max(MinXYZ.Z, bounds.MinXYZ.Z));

			Vector3 intersectMaxXYZ = new Vector3(
				Math.Max(MinXYZ.X, Math.Min(MaxXYZ.X, bounds.MaxXYZ.X)),
				Math.Max(MinXYZ.Y, Math.Min(MaxXYZ.Y, bounds.MaxXYZ.Y)),
				Math.Max(MinXYZ.Z, Math.Min(MaxXYZ.Z, bounds.MaxXYZ.Z)));

			Vector3 delta = intersectMaxXYZ - intersectMinXYZ;
			if (delta.X >= 0 && delta.Y >= 0 && delta.Z >= 0)
			{
				return true;
			}

			return false;
		}

		public void Set(double minX, double minY, double minZ, double maxX, double maxY, double maxZ)
		{
			MinXYZ = new Vector3(minX, minY, minZ);
			MaxXYZ = new Vector3(maxX, maxY, maxZ);
		}

		public static AxisAlignedBoundingBox Intersection(AxisAlignedBoundingBox boundsA, AxisAlignedBoundingBox boundsB)
		{
			Vector3 minXYZ = new Vector3(
				Math.Max(boundsA.MinXYZ.X, boundsB.MinXYZ.X),
				Math.Max(boundsA.MinXYZ.Y, boundsB.MinXYZ.Y),
				Math.Max(boundsA.MinXYZ.Z, boundsB.MinXYZ.Z));

			Vector3 maxXYZ = new Vector3(
				Math.Max(minXYZ.X, Math.Min(boundsA.MaxXYZ.X, boundsB.MaxXYZ.X)),
				Math.Max(minXYZ.Y, Math.Min(boundsA.MaxXYZ.Y, boundsB.MaxXYZ.Y)),
				Math.Max(minXYZ.Z, Math.Min(boundsA.MaxXYZ.Z, boundsB.MaxXYZ.Z)));

			return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
		}

		public static AxisAlignedBoundingBox Union(AxisAlignedBoundingBox bounds, Vector3 vertex)
		{
			Vector3 minXYZ = Vector3.Zero;
			minXYZ.X = Math.Min(bounds.MinXYZ.X, vertex.X);
			minXYZ.Y = Math.Min(bounds.MinXYZ.Y, vertex.Y);
			minXYZ.Z = Math.Min(bounds.MinXYZ.Z, vertex.Z);

			Vector3 maxXYZ = Vector3.Zero;
			maxXYZ.X = Math.Max(bounds.MaxXYZ.X, vertex.X);
			maxXYZ.Y = Math.Max(bounds.MaxXYZ.Y, vertex.Y);
			maxXYZ.Z = Math.Max(bounds.MaxXYZ.Z, vertex.Z);

			return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
		}

		public void Clamp(ref Vector3 positionToClamp)
		{
			if (positionToClamp.X < MinXYZ.X)
			{
				positionToClamp.X = MinXYZ.X;
			}
			else if (positionToClamp.X > MaxXYZ.X)
			{
				positionToClamp.X = MaxXYZ.X;
			}

			if (positionToClamp.Y < MinXYZ.Y)
			{
				positionToClamp.Y = MinXYZ.Y;
			}
			else if (positionToClamp.Y > MaxXYZ.Y)
			{
				positionToClamp.Y = MaxXYZ.Y;
			}

			if (positionToClamp.Z < MinXYZ.Z)
			{
				positionToClamp.Z = MinXYZ.Z;
			}
			else if (positionToClamp.Z > MaxXYZ.Z)
			{
				positionToClamp.Z = MaxXYZ.Z;
			}
		}

		public bool Contains(double x, double y, double z, double errorRange = .001)
		{
			return Contains(new Vector3(x, y, z), errorRange);
		}

		public bool Contains(Vector3 position, double errorRange = .001)
		{
			if (this.MinXYZ.X <= position.X + errorRange
				&& this.MaxXYZ.X >= position.X - errorRange
				&& this.MinXYZ.Y <= position.Y + errorRange
				&& this.MaxXYZ.Y >= position.Y - errorRange
				&& this.MinXYZ.Z <= position.Z + errorRange
				&& this.MaxXYZ.Z >= position.Z - errorRange)
			{
				return true;
			}

			return false;
		}

		public bool Contains(AxisAlignedBoundingBox bounds)
		{
			if (this.MinXYZ.X <= bounds.MinXYZ.X
				&& this.MaxXYZ.X >= bounds.MaxXYZ.X
				&& this.MinXYZ.Y <= bounds.MinXYZ.Y
				&& this.MaxXYZ.Y >= bounds.MaxXYZ.Y
				&& this.MinXYZ.Z <= bounds.MinXYZ.Z
				&& this.MaxXYZ.Z >= bounds.MaxXYZ.Z)
			{
				return true;
			}

			return false;
		}

		public override string ToString()
		{
			return string.Format("min {0} - max {1}", MinXYZ, MaxXYZ);
		}
	}
}