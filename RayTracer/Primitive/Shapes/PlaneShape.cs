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
			plane.PlaneNormal = planeNormal;
			plane.DistanceToPlaneFromOrigin = distanceFromOrigin;
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

		public override ColorF GetColor(IntersectInfo info)
		{
			if (Material.HasTexture)
			{
				Vector3 Position = plane.PlaneNormal;
				Vector3 vecU = new Vector3(Position.Y, Position.Z, -Position.X);
				Vector3 vecV = Vector3.Cross(vecU, plane.PlaneNormal);

				double u = Vector3.Dot(info.HitPosition, vecU);
				double v = Vector3.Dot(info.HitPosition, vecV);
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

		public override IntersectInfo GetClosestIntersection(Ray ray)
		{
			bool inFront;
			double distanceToHit = plane.GetDistanceToIntersection(ray, out inFront);
			if (distanceToHit > 0)
			{
				IntersectInfo info = new IntersectInfo();
				info.closestHitObject = this;
				info.hitType = IntersectionType.FrontFace;
				info.HitPosition = ray.origin + ray.directionNormal * distanceToHit;
				info.normalAtHit = plane.PlaneNormal;
				info.distanceToHit = distanceToHit;

				return info;
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
			return string.Format("Sphere {0}x+{1}y+{2}z+{3}=0)", plane.PlaneNormal.X, plane.PlaneNormal.Y, plane.PlaneNormal.Z, plane.DistanceToPlaneFromOrigin);
		}
	}
}