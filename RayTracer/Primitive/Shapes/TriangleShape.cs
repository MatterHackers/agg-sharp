﻿using MatterHackers.Agg;
using MatterHackers.VectorMath;

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

namespace MatterHackers.RayTracer
{
	public class TriangleShapeUv : TriangleShape
	{
		Vector2Float uv0;
		Vector2Float uv1;
		Vector2Float uv2;

		public TriangleShapeUv(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2,
			Vector2 uv0, Vector2 uv1, Vector2 uv2,
			MaterialAbstract material)
			: base(vertex0, vertex1, vertex2, material)
		{
			this.uv0 = new Vector2Float(uv0);
			this.uv1 = new Vector2Float(uv1);
			this.uv2 = new Vector2Float(uv2);
		}

		public override (double u, double v) GetUv(IntersectInfo info)
		{
			Vector3Float normal = Plane.Normal;
			Vector3Float vecU = new Vector3Float(normal.y, normal.z, -normal.x);
			Vector3Float vecV = Vector3Float.Cross(vecU, Plane.Normal);

			var u = Vector3Float.Dot(new Vector3Float(info.HitPosition), vecU);
			var v = Vector3Float.Dot(new Vector3Float(info.HitPosition), vecV);

			return (u, v);
		}
	}

	public class TriangleShape : BaseShape
	{
		private readonly static int[] xMapping = new int[] { 1, 0, 0 };
		private readonly static int[] yMapping = new int[] { 2, 2, 1 };

		Vector3Float aabbMaxXYZ = Vector3Float.NegativeInfinity;
		Vector3Float aabbMinXYZ = Vector3Float.NegativeInfinity;
		private RectangleFloat boundsOnMajorAxis = new RectangleFloat(float.MaxValue, float.MaxValue, float.MinValue, float.MinValue);
		private Vector3Float center;
		public int MajorAxis { get; private set; } = 0;
		public PlaneFloat Plane { get; private set; }
		private Vector3Float[] vertices = new Vector3Float[3];

		public Vector3 GetVertex(int index)
		{
			return new Vector3(vertices[index].x, vertices[index].y, vertices[index].z);
		}

		public override bool Contains(Vector3 position)
		{
			float distanceToPlane = Plane.GetDistanceFromPlane(new Vector3Float(position));

			if(Math.Abs(distanceToPlane) < .001)
			{
				return base.Contains(position);
			}

			return false;
		}

		public TriangleShape(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, MaterialAbstract material)
		{
			Vector3 planeNormal = Vector3.Cross(vertex1 - vertex0, vertex2 - vertex0).GetNormal();
			double distanceFromOrigin = Vector3.Dot(vertex0, planeNormal);
			Plane = new PlaneFloat(new Vector3Float(planeNormal), (float)distanceFromOrigin);
			Material = material;
			vertices[0] = new Vector3Float(vertex0);
			vertices[1] = new Vector3Float(vertex1);
			vertices[2] = new Vector3Float(vertex2);

			center = new Vector3Float((vertex0 + vertex1 + vertex2) / 3);

			var normalLengths = new [] { Math.Abs(planeNormal.X), Math.Abs(planeNormal.Y), Math.Abs(planeNormal.Z)};
			MajorAxis = normalLengths.Select((v, i) => new { Axis = i, Value = Math.Abs(v) }).OrderBy(o => o.Value).Last().Axis;

			for (int i = 0; i < 3; i++)
			{
				boundsOnMajorAxis.Left = Math.Min(vertices[i][xForMajorAxis], boundsOnMajorAxis.Left);
				boundsOnMajorAxis.Right = Math.Max(vertices[i][xForMajorAxis], boundsOnMajorAxis.Right);
				boundsOnMajorAxis.Bottom = Math.Min(vertices[i][yForMajorAxis], boundsOnMajorAxis.Bottom);
				boundsOnMajorAxis.Top = Math.Max(vertices[i][yForMajorAxis], boundsOnMajorAxis.Top);
			}
		}

		private int xForMajorAxis { get { return xMapping[MajorAxis]; } }
		private int yForMajorAxis { get { return yMapping[MajorAxis]; } }
		public override int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
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

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			if (aabbMinXYZ.x == double.NegativeInfinity)
			{
				aabbMinXYZ = Vector3Float.ComponentMin(Vector3Float.ComponentMin(vertices[0], vertices[1]), vertices[2]);
				aabbMaxXYZ = Vector3Float.ComponentMax(Vector3Float.ComponentMax(vertices[0], vertices[1]), vertices[2]);
			}

			return new AxisAlignedBoundingBox(new Vector3(aabbMinXYZ), new Vector3(aabbMaxXYZ));
		}

		public override Vector3 GetCenter()
		{
			return new Vector3(center);
		}

		public override IntersectInfo GetClosestIntersection(Ray ray)
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
						info.closestHitObject = this;
						info.hitType = IntersectionType.FrontFace;
						info.HitPosition = hitPosition;
						info.normalAtHit = new Vector3(Plane.Normal);
						info.distanceToHit = distanceToHit;

						return info;
					}
				}
			}

			return null;
		}

		public override void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			throw new NotImplementedException();
		}

		public override (double u, double v) GetUv(IntersectInfo info)
		{
			Vector3Float normal = Plane.Normal;
			Vector3Float vecU = new Vector3Float(normal.y, normal.z, -normal.x);
			Vector3Float vecV = Vector3Float.Cross(vecU, Plane.Normal);

			var u = Vector3Float.Dot(new Vector3Float(info.HitPosition), vecU);
			var v = Vector3Float.Dot(new Vector3Float(info.HitPosition), vecV);

			return (u, v);
		}

		public override double GetIntersectCost()
		{
			return 350;
		}

		public override double GetSurfaceArea()
		{
			Vector3 accumulation = Vector3.Zero;

			for (int firstIndex = 0; firstIndex < 3; ++firstIndex)
			{
				int secondIndex = (firstIndex + 1) % 3;
				accumulation += new Vector3(Vector3Float.Cross(vertices[firstIndex], vertices[secondIndex]));
			}
			accumulation /= 2;
			return accumulation.Length;
		}

		public override IEnumerable IntersectionIterator(Ray ray)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("Triangle {0} {1} {2}", vertices[0], vertices[1], vertices[2]);
		}

		private bool Check2DHitOnMajorAxis(double x, double y)
		{
			// check the bounding rect
			if (x >= boundsOnMajorAxis.Left && x <= boundsOnMajorAxis.Right &&
				y >= boundsOnMajorAxis.Bottom && y <= boundsOnMajorAxis.Top)
			{
				Vector2 vertex0 = new Vector2(vertices[0][xForMajorAxis], vertices[0][yForMajorAxis]);
				Vector2 vertex1 = new Vector2(vertices[1][xForMajorAxis], vertices[1][yForMajorAxis]);
				Vector2 vertex2 = new Vector2(vertices[2][xForMajorAxis], vertices[2][yForMajorAxis]);
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
	}
}