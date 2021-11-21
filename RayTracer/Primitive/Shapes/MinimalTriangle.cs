// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
	public class MinimalTriangle : IPrimitive, ITriangle
	{
		private readonly static int[] xMapping = new int[] { 1, 0, 0 };
		private readonly static int[] yMapping = new int[] { 2, 2, 1 };

		private Vector3Float aabbMaxXYZ = Vector3Float.NegativeInfinity;
		private Vector3Float aabbMinXYZ = Vector3Float.NegativeInfinity;
		private RectangleFloat boundsOnMajorAxis = new RectangleFloat(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
		private Vector3Float center;
		private int faceIndex;
		private byte MajorAxis = 0;
		private Func<int, int, Vector3Float> vertexFunc;

		public MinimalTriangle(Func<int, int, Vector3Float> vertexFunc, int faceIndex)
		{
			this.faceIndex = faceIndex;
			this.vertexFunc = vertexFunc;
			var planeNormal = (vertex(1) - vertex(0)).Cross(vertex(2) - vertex(0)).GetNormal();
			double distanceFromOrigin = vertex(0).Dot(planeNormal);
			Plane = new PlaneFloat(new Vector3Float(planeNormal), (float)distanceFromOrigin);

			center = new Vector3Float((vertex(0) + vertex(1) + vertex(2)) / 3);

			var normalLengths = new[] { Math.Abs(planeNormal.X), Math.Abs(planeNormal.Y), Math.Abs(planeNormal.Z) };
			MajorAxis = (byte)normalLengths.Select((v, i) => new { Axis = i, Value = Math.Abs(v) }).OrderBy(o => o.Value).Last().Axis;

			aabbMinXYZ = vertex(0).ComponentMin(vertex(1)).ComponentMin(vertex(2));
			aabbMaxXYZ = vertex(0).ComponentMax(vertex(1)).ComponentMax(vertex(2));

			for (int i = 0; i < 3; i++)
			{
				boundsOnMajorAxis.Left = Math.Min(vertex(i)[xForMajorAxis], boundsOnMajorAxis.Left);
				boundsOnMajorAxis.Right = Math.Max(vertex(i)[xForMajorAxis], boundsOnMajorAxis.Right);
				boundsOnMajorAxis.Bottom = Math.Min(vertex(i)[yForMajorAxis], boundsOnMajorAxis.Bottom);
				boundsOnMajorAxis.Top = Math.Max(vertex(i)[yForMajorAxis], boundsOnMajorAxis.Top);
			}
		}

		public MaterialAbstract Material { get => throw new NotImplementedException(); set => throw new NotImplementedException(); }

		public PlaneFloat Plane { get; private set; }

        public IEnumerable<IBvhItem> Children => throw new NotImplementedException();

        public Matrix4X4 AxisToWorld => throw new NotImplementedException();

        private int xForMajorAxis => xMapping[MajorAxis];

		private int yForMajorAxis => yMapping[MajorAxis];

		public bool Contains(Vector3 position)
		{
			float distanceToPlane = Plane.GetDistanceFromPlane(new Vector3Float(position));

			if (Math.Abs(distanceToPlane) < .001)
			{
				if (this.GetAxisAlignedBoundingBox().Contains(position))
				{
					return true;
				}
			}

			return false;
		}

		public int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public int FindSideOfLine(Vector2 sidePoint0, Vector2 sidePoint1, Vector2 testPosition)
		{
			if (Vector2.Cross(testPosition - sidePoint0, sidePoint1 - sidePoint0) < 0)
			{
				return 1;
			}

			return -1;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return new AxisAlignedBoundingBox(new Vector3(aabbMinXYZ), new Vector3(aabbMaxXYZ));
		}

		public double GetAxisCenter(int axis)
		{
			if (axis == 0)
			{
				return center.X;
			}
			if (axis == 1)
			{
				return center.Y;
			}

			return center.Z;
		}

		public Vector3 GetCenter()
		{
			return new Vector3(center);
		}

		public IntersectInfo GetClosestIntersection(Ray ray)
		{
			bool inFront;
			float distanceToHit;
			if (Plane.RayHitPlane(ray, out distanceToHit, out inFront))
			{
				bool wantFrontAndInFront = (ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace && inFront;
				bool wantBackAndInBack = (ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace && !inFront;
				if (wantFrontAndInFront || wantBackAndInBack)
				{
					Vector3 hitPosition = ray.origin + ray.directionNormal * distanceToHit;

					bool haveHitIn2D = false;
					if (MajorAxis == 0)
					{
						haveHitIn2D = Check2DHitOnMajorAxis(hitPosition.Y, hitPosition.Z);
					}
					else if (MajorAxis == 1)
					{
						haveHitIn2D = Check2DHitOnMajorAxis(hitPosition.X, hitPosition.Z);
					}
					else
					{
						haveHitIn2D = Check2DHitOnMajorAxis(hitPosition.X, hitPosition.Y);
					}
					if (haveHitIn2D)
					{
						IntersectInfo info = new IntersectInfo();
						info.ClosestHitObject = this;
						info.HitType = IntersectionType.FrontFace;
						info.HitPosition = hitPosition;
						info.NormalAtHit = new Vector3(Plane.Normal);
						info.DistanceToHit = distanceToHit;

						return info;
					}
				}
			}

			return null;
		}

		public void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			throw new NotImplementedException();
		}

		public ColorF GetColor(IntersectInfo info)
		{
			throw new NotImplementedException();
		}

		public bool GetContained(List<IBvhItem> results, AxisAlignedBoundingBox subRegion)
		{
			AxisAlignedBoundingBox bounds = GetAxisAlignedBoundingBox();
			if (bounds.Contains(subRegion))
			{
				results.Add(this);
				return true;
			}

			return false;
		}

        public IEnumerable<IBvhItem> GetCrossing(Plane plane)
        {
            throw new NotImplementedException();
        }

        public double GetIntersectCost()
		{
			return 350;
		}

		public double GetSurfaceArea()
		{
			Vector3 accumulation = Vector3.Zero;

			for (int firstIndex = 0; firstIndex < 3; ++firstIndex)
			{
				int secondIndex = (firstIndex + 1) % 3;
				accumulation += new Vector3(vertex(firstIndex).Cross(vertex(secondIndex)));
			}
			accumulation /= 2;
			return accumulation.Length;
		}

		public (double u, double v) GetUv(IntersectInfo info)
		{
			Vector3Float normal = Plane.Normal;
			Vector3Float vecU = new Vector3Float(normal.Y, normal.Z, -normal.X);
			Vector3Float vecV = vecU.Cross(Plane.Normal);

			var u = new Vector3Float(info.HitPosition).Dot(vecU);
			var v = new Vector3Float(info.HitPosition).Dot(vecV);

			return (u, v);
		}

		public Vector3 GetVertex(int index)
		{
			return new Vector3(vertex(index).X, vertex(index).Y, vertex(index).Z);
		}

		public IEnumerable IntersectionIterator(Ray ray)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("Triangle {0} {1} {2}", vertex(0), vertex(1), vertex(2));
		}

		private bool Check2DHitOnMajorAxis(double x, double y)
		{
			// check the bounding rect
			if (x >= boundsOnMajorAxis.Left && x <= boundsOnMajorAxis.Right &&
				y >= boundsOnMajorAxis.Bottom && y <= boundsOnMajorAxis.Top)
			{
				Vector2 vertex0 = new Vector2(vertex(0)[xForMajorAxis], vertex(0)[yForMajorAxis]);
				Vector2 vertex1 = new Vector2(vertex(1)[xForMajorAxis], vertex(1)[yForMajorAxis]);
				Vector2 vertex2 = new Vector2(vertex(2)[xForMajorAxis], vertex(2)[yForMajorAxis]);
				Vector2 hitPosition = new Vector2(x, y);
				int sumOfLineSides = FindSideOfLine(vertex0, vertex1, hitPosition);
				sumOfLineSides += FindSideOfLine(vertex1, vertex2, hitPosition);
				sumOfLineSides += FindSideOfLine(vertex2, vertex0, hitPosition);
				if (sumOfLineSides == -3 || sumOfLineSides == 3)
				{
					return true;
				}
			}

			return false;
		}

		private Vector3Float vertex(int i)
		{
			return vertexFunc(faceIndex, i);
		}
	}
}