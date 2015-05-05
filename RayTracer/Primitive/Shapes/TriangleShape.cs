using MatterHackers.Agg;
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

namespace MatterHackers.RayTracer
{
	public class TriangleShape : BaseShape
	{
		private Plane plane;
		private int majorAxis = 0;
		private RectangleDouble boundsOnMajorAxis = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
		private Vector3Float[] vertices = new Vector3Float[3];
		private Vector3Float center;

		int[] xMapping = new int[] { 1, 0, 0 };
		int xForMajorAxis { get { return xMapping[majorAxis]; } }

		int[] yMapping = new int[] { 2, 2, 1 };
		int yForMajorAxis { get { return yMapping[majorAxis]; } }

		public TriangleShape(Vector3 vertex0, Vector3 vertex1, Vector3 vertex2, MaterialAbstract material)
		{
			Vector3 planeNormal = Vector3.Cross(vertex1 - vertex0, vertex2 - vertex0).GetNormal();
			double distanceFromOrigin = Vector3.Dot(vertex0, planeNormal);
			plane = new Plane(planeNormal, distanceFromOrigin);
			Material = material;
			vertices[0] = new Vector3Float(vertex0);
			vertices[1] = new Vector3Float(vertex1);
			vertices[2] = new Vector3Float(vertex2);
			center = new Vector3Float((vertex0 + vertex1 + vertex2) / 3);
			if (Math.Abs(planeNormal.x) > Math.Abs(planeNormal.y))
			{
				if (Math.Abs(planeNormal.x) > Math.Abs(planeNormal.z))
				{
					// mostly facing x axis
					majorAxis = 0;
				}
				else if (Math.Abs(planeNormal.y) > Math.Abs(planeNormal.z))
				{
					// mostly facing z
					majorAxis = 2;
				}
			}
			else if (Math.Abs(planeNormal.y) > Math.Abs(planeNormal.z))
			{
				// mostly facing y
				majorAxis = 1;
			}
			else
			{
				// mostly facing z
				majorAxis = 2;
			}
			for (int i = 0; i < 3; i++)
			{
				boundsOnMajorAxis.Left = Math.Min(vertices[i][xForMajorAxis], boundsOnMajorAxis.Left);
				boundsOnMajorAxis.Right = Math.Max(vertices[i][xForMajorAxis], boundsOnMajorAxis.Right);
				boundsOnMajorAxis.Bottom = Math.Min(vertices[i][yForMajorAxis], boundsOnMajorAxis.Bottom);
				boundsOnMajorAxis.Top = Math.Max(vertices[i][yForMajorAxis], boundsOnMajorAxis.Top);
			}
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

		public override Vector3 GetCenter()
		{
			return new Vector3(center);
		}

		private AxisAlignedBoundingBox cachedAABB = new AxisAlignedBoundingBox(Vector3.NegativeInfinity, Vector3.NegativeInfinity);

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			if (cachedAABB.minXYZ.x == double.NegativeInfinity)
			{
				Vector3Float minXYZ = Vector3Float.ComponentMin(Vector3Float.ComponentMin(vertices[0], vertices[1]), vertices[2]);
				Vector3Float maxXYZ = Vector3Float.ComponentMax(Vector3Float.ComponentMax(vertices[0], vertices[1]), vertices[2]);
				cachedAABB = new AxisAlignedBoundingBox(new Vector3(minXYZ), new Vector3(maxXYZ));
			}

			return cachedAABB;
		}

		public override RGBA_Floats GetColor(IntersectInfo info)
		{
			if (Material.HasTexture)
			{
				Vector3 Position = plane.planeNormal;
				Vector3 vecU = new Vector3(Position.y, Position.z, -Position.x);
				Vector3 vecV = Vector3.Cross(vecU, plane.planeNormal);

				double u = Vector3.Dot(info.hitPosition, vecU);
				double v = Vector3.Dot(info.hitPosition, vecV);
				return Material.GetColor(u, v);
			}
			else
			{
				return Material.GetColor(0, 0);
			}
		}

		public override double GetIntersectCost()
		{
			return 350;
		}

		public int FindSideOfLine(Vector2 sidePoint0, Vector2 sidePoint1, Vector2 testPosition)
		{
			if (Vector2.Cross(testPosition - sidePoint0, sidePoint1 - sidePoint0) < 0)
			{
				return 1;
			}

			return -1;
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

		public override IntersectInfo GetClosestIntersection(Ray ray)
		{
			bool inFront;
			double distanceToHit;
			if (plane.RayHitPlane(ray, out distanceToHit, out inFront))
			{
				bool wantFrontAndInFront = (ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace && inFront;
				bool wantBackAndInBack = (ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace && !inFront;
				if (wantFrontAndInFront || wantBackAndInBack)
				{
					Vector3 hitPosition = ray.origin + ray.directionNormal * distanceToHit;

					bool haveHitIn2D = false;
					if (majorAxis == 0)
					{
						haveHitIn2D = Check2DHitOnMajorAxis(hitPosition.y, hitPosition.z);
					}
					else if (majorAxis == 1)
					{
						haveHitIn2D = Check2DHitOnMajorAxis(hitPosition.x, hitPosition.z);
					}
					else
					{
						haveHitIn2D = Check2DHitOnMajorAxis(hitPosition.x, hitPosition.y);
					}
					if (haveHitIn2D)
					{
						IntersectInfo info = new IntersectInfo();
						info.closestHitObject = this;
						info.hitType = IntersectionType.FrontFace;
						info.hitPosition = hitPosition;
						info.normalAtHit = plane.planeNormal;
						info.distanceToHit = distanceToHit;

						return info;
					}
				}
			}

			return null;
		}

		public override int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public override void GetClosestIntersections(RayBundle rayBundle, int rayIndexToStartCheckingFrom, IntersectInfo[] intersectionsForBundle)
		{
			throw new NotImplementedException();
		}

		public override IEnumerable IntersectionIterator(Ray ray)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("Triangle {0} {1} {2}", vertices[0], vertices[1], vertices[2]);
		}
	}
}