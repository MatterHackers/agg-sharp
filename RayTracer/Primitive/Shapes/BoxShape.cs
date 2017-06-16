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
	public class BoxShape : BaseShape
	{
		public Vector3 minXYZ;
		public Vector3 maxXYZ;

		public BoxShape(Vector3 minXYZ, Vector3 maxXYZ, MaterialAbstract material)
		{
			if (maxXYZ.x < minXYZ.x || maxXYZ.y < minXYZ.y || maxXYZ.z < minXYZ.z)
			{
				throw new ArgumentException("All values of min must be less than all values in max.");
			}

			this.minXYZ = minXYZ;
			this.maxXYZ = maxXYZ;
			Material = material;
		}

		public override double GetSurfaceArea()
		{
			double frontAndBack = (maxXYZ.x - minXYZ.x) * (maxXYZ.z - minXYZ.z) * 2;
			double leftAndRight = (maxXYZ.y - minXYZ.y) * (maxXYZ.z - minXYZ.z) * 2;
			double topAndBottom = (maxXYZ.x - minXYZ.x) * (maxXYZ.y - minXYZ.y) * 2;
			return frontAndBack + leftAndRight + topAndBottom;
		}

		public override double GetIntersectCost()
		{
			return 452;
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
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

		/*
bool Box::intersect(const Ray &r, float t0, float t1) const {
  float tmin, tmax, tymin, tymax, tzmin, tzmax;

  tmin = (parameters[r.sign[0]].x() - r.origin.x()) * r.inv_direction.x();
  tmax = (parameters[1-r.sign[0]].x() - r.origin.x()) * r.inv_direction.x();
  tymin = (parameters[r.sign[1]].y() - r.origin.y()) * r.inv_direction.y();
  tymax = (parameters[1-r.sign[1]].y() - r.origin.y()) * r.inv_direction.y();
  if ( (tmin > tymax) || (tymin > tmax) )
	return false;
  if (tymin > tmin)
	tmin = tymin;
  if (tymax < tmax)
	tmax = tymax;
  tzmin = (parameters[r.sign[2]].z() - r.origin.z()) * r.inv_direction.z();
  tzmax = (parameters[1-r.sign[2]].z() - r.origin.z()) * r.inv_direction.z();
  if ( (tmin > tzmax) || (tzmin > tmax) )
	return false;
  if (tzmin > tmin)
	tmin = tzmin;
  if (tzmax < tmax)
	tmax = tzmax;
  return ( (tmin < t1) && (tmax > t0) );
} */

		private bool intersect(Ray ray, out double minDistFound, out double maxDistFound, out int minAxis, out int maxAxis)
		{
			minAxis = 0;
			maxAxis = 0;
			// we calculate distance to the intersection with the x planes of the box
			minDistFound = (this[(int)ray.sign[0]].x - ray.origin.x) * ray.oneOverDirection.x;
			maxDistFound = (this[1 - (int)ray.sign[0]].x - ray.origin.x) * ray.oneOverDirection.x;

			// now find the distance to the y planes of the box
			double minDistToY = (this[(int)ray.sign[1]].y - ray.origin.y) * ray.oneOverDirection.y;
			double maxDistToY = (this[1 - (int)ray.sign[1]].y - ray.origin.y) * ray.oneOverDirection.y;

			if ((minDistFound > maxDistToY) || (minDistToY > maxDistFound))
			{
				return false;
			}

			if (minDistToY > minDistFound)
			{
				minAxis = 1;
				minDistFound = minDistToY;
			}

			if (maxDistToY < maxDistFound)
			{
				maxAxis = 1;
				maxDistFound = maxDistToY;
			}

			// and finaly the z planes
			double minDistToZ = (this[(int)ray.sign[2]].z - ray.origin.z) * ray.oneOverDirection.z;
			double maxDistToZ = (this[1 - (int)ray.sign[2]].z - ray.origin.z) * ray.oneOverDirection.z;

			if ((minDistFound > maxDistToZ) || (minDistToZ > maxDistFound))
			{
				return false;
			}

			if (minDistToZ > minDistFound)
			{
				minAxis = 2;
				minDistFound = minDistToZ;
			}

			if (maxDistToZ < maxDistFound)
			{
				maxAxis = 2;
				maxDistFound = maxDistToZ;
			}

			bool oneHitIsWithinLimits = (minDistFound < ray.maxDistanceToConsider && minDistFound > ray.minDistanceToConsider)
				|| (maxDistFound < ray.maxDistanceToConsider && maxDistFound > ray.minDistanceToConsider);
			return oneHitIsWithinLimits;
		}

		public override RGBA_Floats GetColor(IntersectInfo info)
		{
			if (Material.HasTexture)
			{
				throw new NotImplementedException();
#if false
                //Vector vecU = new Vector(hit.y - Position.y, hit.z - Position.z, Position.x-hit.x);
                Vector3 Position = Transform.Position;
                Vector3 vecU = new Vector3D((P1.y + P2.y) / 2 - Position.y, (P1.z + P2.z) / 2 - Position.z, Position.x - (P1.x + P2.x) / 2).GetNormal();
                Vector3 vecV = vecU.Cross((P1 + P2) / 2 - Position).GetNormal();

                double u = Vector3.Dot(info.hitPosition, vecU);
                double v = Vector3.Dot(info.hitPosition, vecV);
                return Material.GetColor(u, v);
#endif
			}
			else
			{
				return Material.GetColor(0, 0);
			}
		}

		public override IEnumerable IntersectionIterator(Ray ray)
		{
			double minDistFound;
			double maxDistFound;
			int minAxis;
			int maxAxis;

			if (intersect(ray, out minDistFound, out maxDistFound, out minAxis, out maxAxis))
			{
				if ((ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace)
				{
					IntersectInfo info = new IntersectInfo();
					info.hitType = IntersectionType.FrontFace;
					info.closestHitObject = this;
					info.HitPosition = ray.origin + ray.directionNormal * minDistFound;
					info.normalAtHit[minAxis] = ray.sign[minAxis] == Ray.Sign.negative ? 1 : -1; // you hit the side that is oposite your sign
					info.distanceToHit = minDistFound;
					yield return info;
				}

				if ((ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace)
				{
					IntersectInfo info = new IntersectInfo();
					info.hitType = IntersectionType.BackFace;
					info.closestHitObject = this;
					info.HitPosition = ray.origin + ray.directionNormal * maxDistFound;
					info.normalAtHit[maxAxis] = ray.sign[maxAxis] == Ray.Sign.negative ? 1 : -1;
					info.distanceToHit = maxDistFound;
					yield return info;
				}
			}
		}

		public override int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
		{
			throw new NotImplementedException();
		}

		public override IntersectInfo GetClosestIntersection(Ray ray)
		{
			IntersectInfo info = new IntersectInfo();

			double minDistFound;
			double maxDistFound;
			int minAxis;
			int maxAxis;

			if (intersect(ray, out minDistFound, out maxDistFound, out minAxis, out maxAxis))
			{
				if (ray.intersectionType == IntersectionType.FrontFace)
				{
					if (minDistFound > ray.minDistanceToConsider && minDistFound < ray.maxDistanceToConsider)
					{
						info.hitType = IntersectionType.FrontFace;
						if (ray.isShadowRay)
						{
							return info;
						}
						info.closestHitObject = this;
						info.HitPosition = ray.origin + ray.directionNormal * minDistFound;
						info.normalAtHit[minAxis] = ray.sign[minAxis] == Ray.Sign.negative ? 1 : -1; // you hit the side that is oposite your sign
						info.distanceToHit = minDistFound;
					}
				}
				else // check back faces
				{
					if (maxDistFound > ray.minDistanceToConsider && maxDistFound < ray.maxDistanceToConsider)
					{
						info.hitType = IntersectionType.BackFace;
						if (ray.isShadowRay)
						{
							return info;
						}
						info.closestHitObject = this;
						info.HitPosition = ray.origin + ray.directionNormal * maxDistFound;
						info.normalAtHit[maxAxis] = ray.sign[maxAxis] == Ray.Sign.negative ? 1 : -1;
						info.distanceToHit = maxDistFound;
					}
				}
			}

			return info;
		}

		public override string ToString()
		{
			return string.Format("Box ({0},{1},{2})-({3},{4},{5})", minXYZ.x, minXYZ.y, minXYZ.z, maxXYZ.x, maxXYZ.y, maxXYZ.z);
		}
	}
}