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
	public class PlaneShape : BaseShape
	{
		private Plane plane;
		public ColorF OddColor;

		public PlaneShape(Vector3 planeNormal, double distanceFromOrigin, MaterialAbstract material)
		{
			plane = new Plane(planeNormal, distanceFromOrigin);
			Material = material;
		}

		public PlaneShape(Vector3 planeNormal, double distanceFromOrigin, ColorF color, ColorF oddcolor, double reflection, double transparency)
		{
			plane.Normal = planeNormal;
			plane.DistanceFromOrigin = distanceFromOrigin;
			//Color = color;
			OddColor = oddcolor;
			//Transparency = transparency;
			//Reflection = reflection;
		}

		public override double GetSurfaceArea()
		{
			return double.PositiveInfinity;
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return new AxisAlignedBoundingBox(Vector3.NegativeInfinity, Vector3.PositiveInfinity);
		}

		public override double GetIntersectCost()
		{
			return 350;
		}

		public override IntersectInfo GetClosestIntersection(Ray ray)
		{
			bool inFront;
			double distanceToHit = plane.GetDistanceToIntersection(ray, out inFront);
			if (distanceToHit > 0)
			{
				return new IntersectInfo
				{
					ClosestHitObject = this,
					HitType = IntersectionType.FrontFace,
					HitPosition = ray.origin + ray.directionNormal * distanceToHit,
					NormalAtHit = plane.Normal,
					DistanceToHit = distanceToHit
				};
			}

			return null;
		}

		public override int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public override IEnumerable IntersectionIterator(Ray ray)
		{
			throw new NotImplementedException();
		}

		public override string ToString()
		{
			return string.Format("Sphere {0}x+{1}y+{2}z+{3}=0)", plane.Normal.X, plane.Normal.Y, plane.Normal.Z, plane.DistanceFromOrigin);
		}

		public override (double u, double v) GetUv(IntersectInfo info)
		{
			Vector3 Position = plane.Normal;
			Vector3 vecU = new Vector3(Position.Y, Position.Z, -Position.X);
			Vector3 vecV = Vector3Ex.Cross(vecU, plane.Normal);

			double u = Vector3Ex.Dot(info.HitPosition, vecU);
			double v = Vector3Ex.Dot(info.HitPosition, vecV);
			return (u, v);
		}
	}
}