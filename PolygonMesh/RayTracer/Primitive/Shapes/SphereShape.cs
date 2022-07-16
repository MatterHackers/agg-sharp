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
	/// <summary>
	/// a sphere is one of the most basic shapes you will find in any raytracer application.
	/// why? simply because it is relatively easy and quick to determine an intersection between a
	/// line (ray) and a sphere.
	/// Additionally it is ideal to try out special effects like reflection and refraction on spheres.
	/// </summary>
	public class SphereShape : BaseShape
	{
		public double radius;
		public Vector3 position;

		public SphereShape(Vector3 position, double radius, MaterialAbstract material)
		{
			this.radius = radius;
			this.position = position;
			this.Material = material;
		}

		public override double GetSurfaceArea()
		{
			return 2 * MathHelper.Tau * radius * radius;
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return new AxisAlignedBoundingBox(
				new Vector3(position.X - radius, position.Y - radius, position.Z - radius),
				new Vector3(position.X + radius, position.Y + radius, position.Z + radius));
		}

		public override double GetIntersectCost()
		{
			return 670;
		}

		/// <summary>
		/// This implementation of intersect uses the fastest ray-sphere intersection algorithm I could find
		/// on the internet.
		/// </summary>
		/// <param name="ray"></param>
		/// <returns></returns>
		public override IntersectInfo GetClosestIntersection(Ray ray)
		{
			double radiusSquared = radius * radius;

			Vector3 deltaFromShpereCenterToRayOrigin = ray.origin - this.position;
			double distanceFromSphereCenterToRayOrigin = Vector3Ex.Dot(deltaFromShpereCenterToRayOrigin, ray.directionNormal); // negative means the sphere is in front of the ray.
			double lengthFromRayOrginToSphereCenterSquared = Vector3Ex.Dot(deltaFromShpereCenterToRayOrigin, deltaFromShpereCenterToRayOrigin);
			double lengthFromRayOrigintoNearEdgeOfSphereSquared = lengthFromRayOrginToSphereCenterSquared - radiusSquared;
			double distanceFromSphereCenterToRaySquared = distanceFromSphereCenterToRayOrigin * distanceFromSphereCenterToRayOrigin;
			double amountSphereCenterToRayIsGreaterThanRayOriginToEdgeSquared = distanceFromSphereCenterToRaySquared - lengthFromRayOrigintoNearEdgeOfSphereSquared;

			if (amountSphereCenterToRayIsGreaterThanRayOriginToEdgeSquared > 0
				|| (ray.intersectionType == IntersectionType.BackFace && lengthFromRayOrginToSphereCenterSquared < radiusSquared)) // yes, that's it, we found the intersection!
			{
				IntersectInfo info = new IntersectInfo();
				info.ClosestHitObject = this;
				info.HitType = IntersectionType.FrontFace;
				if (ray.isShadowRay)
				{
					return info;
				}
				double distanceFromRayOriginToSphereCenter = -distanceFromSphereCenterToRayOrigin;

				double amountSphereCenterToRayIsGreaterThanRayOriginToEdge = Math.Sqrt(amountSphereCenterToRayIsGreaterThanRayOriginToEdgeSquared);
				if (ray.intersectionType == IntersectionType.FrontFace)
				{
					double distanceToFrontHit = distanceFromRayOriginToSphereCenter - amountSphereCenterToRayIsGreaterThanRayOriginToEdge;
					if (distanceToFrontHit > ray.maxDistanceToConsider || distanceToFrontHit < ray.minDistanceToConsider)
					{
						return null;
					}
					info.DistanceToHit = distanceToFrontHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.DistanceToHit;
					info.NormalAtHit = (info.HitPosition - position).GetNormal();
				}
				else // check back faces
				{
					double distanceToBackHit = distanceFromRayOriginToSphereCenter + amountSphereCenterToRayIsGreaterThanRayOriginToEdge;
					if (distanceToBackHit > ray.maxDistanceToConsider || distanceToBackHit < ray.minDistanceToConsider)
					{
						return null;
					}
					info.HitType = IntersectionType.BackFace;
					info.DistanceToHit = distanceToBackHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.DistanceToHit;
					info.NormalAtHit = -(info.HitPosition - position).GetNormal();
				}

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
			double radiusSquared = radius * radius;

			Vector3 deltaFromShpereCenterToRayOrigin = ray.origin - this.position;
			double distanceFromSphereCenterToRayOrigin = Vector3Ex.Dot(deltaFromShpereCenterToRayOrigin, ray.directionNormal); // negative means the sphere is in front of the ray.
			double lengthFromRayOrginToSphereCenterSquared = Vector3Ex.Dot(deltaFromShpereCenterToRayOrigin, deltaFromShpereCenterToRayOrigin);
			double lengthFromRayOrigintoNearEdgeOfSphereSquared = lengthFromRayOrginToSphereCenterSquared - radiusSquared;
			double distanceFromSphereCenterToRaySquared = distanceFromSphereCenterToRayOrigin * distanceFromSphereCenterToRayOrigin;
			double amountSphereCenterToRayIsGreaterThanRayOriginToEdgeSquared = distanceFromSphereCenterToRaySquared - lengthFromRayOrigintoNearEdgeOfSphereSquared;

			if (amountSphereCenterToRayIsGreaterThanRayOriginToEdgeSquared > 0)
			{
				double distanceFromRayOriginToSphereCenter = -distanceFromSphereCenterToRayOrigin;
				double amountSphereCenterToRayIsGreaterThanRayOriginToEdge = Math.Sqrt(amountSphereCenterToRayIsGreaterThanRayOriginToEdgeSquared);

				if ((ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace)
				{
					IntersectInfo info = new IntersectInfo();
					info.HitType = IntersectionType.FrontFace;
					info.ClosestHitObject = this;
					double distanceToFrontHit = distanceFromRayOriginToSphereCenter - amountSphereCenterToRayIsGreaterThanRayOriginToEdge;

					info.DistanceToHit = distanceToFrontHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.DistanceToHit;
					info.NormalAtHit = (info.HitPosition - position).GetNormal();

					yield return info;
				}

				if ((ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace)
				{
					IntersectInfo info = new IntersectInfo();
					info.HitType = IntersectionType.BackFace;
					info.ClosestHitObject = this;
					double distanceToBackHit = distanceFromRayOriginToSphereCenter + amountSphereCenterToRayIsGreaterThanRayOriginToEdge;

					info.DistanceToHit = distanceToBackHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.DistanceToHit;
					info.NormalAtHit = -(info.HitPosition - position).GetNormal();

					yield return info;
				}
			}
		}

		public override string ToString()
		{
			return string.Format("Sphere ({0},{1},{2}) Radius: {3}", position.X, position.Y, position.Z, radius);
		}

		public override (double u, double v) GetUv(IntersectInfo info)
		{
			Vector3 vn = new Vector3(0, 1, 0).GetNormal(); // north pole / up
			Vector3 ve = new Vector3(0, 0, 1).GetNormal(); // equator / sphere orientation
			Vector3 vp = (info.HitPosition - position).GetNormal(); //points from center of sphere to intersection

			double phi = Math.Acos(-Vector3Ex.Dot(vp, vn));
			double v = (phi * 2 / Math.PI) - 1;

			double sinphi = Vector3Ex.Dot(ve, vp) / Math.Sin(phi);
			sinphi = sinphi < -1 ? -1 : sinphi > 1 ? 1 : sinphi;
			double theta = Math.Acos(sinphi) * 2 / Math.PI;

			double u;

			if (Vector3Ex.Dot(Vector3Ex.Cross(vn, ve), vp) > 0)
			{
				u = theta;
			}
			else
			{
				u = 1 - theta;
			}

			return (u, v);
		}
	}
}