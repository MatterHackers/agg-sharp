// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
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

namespace MatterHackers.VectorMath
{
	[Flags]
	public enum IntersectionType { None = 0, FrontFace = 1, BackFace = 2, Both = FrontFace | BackFace };

	/// <summary>
	/// a virtual ray that is casted from a begin Position in a certain Direction.
	/// </summary>
	public class Ray
	{
		public static double sameSurfaceOffset = .00001;

		public Vector3 origin;
		public Vector3 directionNormal;
		public double minDistanceToConsider;
		public double maxDistanceToConsider;
		public Vector3 oneOverDirection;
		public bool isShadowRay;
		public IntersectionType intersectionType;

		public enum Sign { negative = 1, positive = 0 };

		public Sign[] sign = new Sign[3];

		public Ray(Vector3 origin, Vector3 directionNormal, double minDistanceToConsider = 0, double maxDistanceToConsider = double.PositiveInfinity, IntersectionType intersectionType = IntersectionType.FrontFace)
		{
			this.origin = origin;
			this.directionNormal = directionNormal;
			this.minDistanceToConsider = minDistanceToConsider;
			this.maxDistanceToConsider = maxDistanceToConsider;
			this.intersectionType = intersectionType;
			oneOverDirection = 1 / directionNormal;

			sign[0] = (oneOverDirection.X < 0) ? Sign.negative : Sign.positive;
			sign[1] = (oneOverDirection.Y < 0) ? Sign.negative : Sign.positive;
			sign[2] = (oneOverDirection.Z < 0) ? Sign.negative : Sign.positive;
		}

		public Ray(Ray rayToCopy)
		{
			origin = rayToCopy.origin;
			directionNormal = rayToCopy.directionNormal;
			minDistanceToConsider = rayToCopy.minDistanceToConsider;
			maxDistanceToConsider = rayToCopy.maxDistanceToConsider;
			oneOverDirection = rayToCopy.oneOverDirection;
			isShadowRay = rayToCopy.isShadowRay;
			intersectionType = rayToCopy.intersectionType;
			sign[0] = rayToCopy.sign[0];
			sign[1] = rayToCopy.sign[1];
			sign[2] = rayToCopy.sign[2];
		}

		public static Ray Transform(Ray ray, Matrix4X4 matrix)
		{
			Vector3 transformedOrigin = Vector3Ex.TransformPosition(ray.origin, matrix);
			Vector3 transformedDirecton = Vector3Ex.TransformVector(ray.directionNormal, matrix);
			return new Ray(transformedOrigin, transformedDirecton, ray.minDistanceToConsider, ray.maxDistanceToConsider, ray.intersectionType);
		}

		public bool Intersect(AxisAlignedBoundingBox bounds, out double minDistFound, out double maxDistFound, out int minAxis, out int maxAxis)
		{
			minAxis = 0;
			maxAxis = 0;
			// we calculate distance to the intersection with the x planes of the box
			minDistFound = (bounds[(int)this.sign[0]].X - this.origin.X) * this.oneOverDirection.X;
			maxDistFound = (bounds[1 - (int)this.sign[0]].X - this.origin.X) * this.oneOverDirection.X;

			// now find the distance to the y planes of the box
			double minDistToY = (bounds[(int)this.sign[1]].Y - this.origin.Y) * this.oneOverDirection.Y;
			double maxDistToY = (bounds[1 - (int)this.sign[1]].Y - this.origin.Y) * this.oneOverDirection.Y;

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

			// and finally the z planes
			double minDistToZ = (bounds[(int)this.sign[2]].Z - this.origin.Z) * this.oneOverDirection.Z;
			double maxDistToZ = (bounds[1 - (int)this.sign[2]].Z - this.origin.Z) * this.oneOverDirection.Z;

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

			bool oneHitIsWithinLimits = (minDistFound < this.maxDistanceToConsider && minDistFound > this.minDistanceToConsider)
				|| (maxDistFound < this.maxDistanceToConsider && maxDistFound > this.minDistanceToConsider);

			return oneHitIsWithinLimits;
		}

		public RayHitInfo GetClosestIntersection(AxisAlignedBoundingBox bounds)
		{
			RayHitInfo info = new RayHitInfo();

			double minDistFound;
			double maxDistFound;
			int minAxis;
			int maxAxis;

			if (Intersect(bounds, out minDistFound, out maxDistFound, out minAxis, out maxAxis))
			{
				if (this.intersectionType == IntersectionType.FrontFace)
				{
					if (minDistFound > this.minDistanceToConsider && minDistFound < this.maxDistanceToConsider)
					{
						info.HitType = IntersectionType.FrontFace;
						if (this.isShadowRay)
						{
							return info;
						}
						info.ClosestHitObject = bounds;
						info.HitPosition = this.origin + this.directionNormal * minDistFound;
						Vector3 normalAtHit = default(Vector3);
						normalAtHit[minAxis] = this.sign[minAxis] == Ray.Sign.negative ? 1 : -1; // you hit the side that is opposite your sign
						info.NormalAtHit = normalAtHit;
						info.DistanceToHit = minDistFound;
					}
				}
				else // check back faces
				{
					if (maxDistFound > this.minDistanceToConsider && maxDistFound < this.maxDistanceToConsider)
					{
						info.HitType = IntersectionType.BackFace;
						if (this.isShadowRay)
						{
							return info;
						}
						info.ClosestHitObject = bounds;
						info.HitPosition = this.origin + this.directionNormal * maxDistFound;
						Vector3 normalAtHit = default(Vector3);
						normalAtHit[minAxis] = this.sign[minAxis] == Ray.Sign.negative ? 1 : -1; // you hit the side that is opposite your sign
						info.NormalAtHit = normalAtHit;
						info.DistanceToHit = maxDistFound;
					}
				}
			}

			return info;
		}

		public bool Intersection(AxisAlignedBoundingBox bounds)
		{
			Ray ray = this;
			// we calculate distance to the intersection with the x planes of the box
			double minDistFound = (bounds[(int)ray.sign[0]].X - ray.origin.X) * ray.oneOverDirection.X;
			double maxDistFound = (bounds[1 - (int)ray.sign[0]].X - ray.origin.X) * ray.oneOverDirection.X;

			// now find the distance to the y planes of the box
			double minDistToY = (bounds[(int)ray.sign[1]].Y - ray.origin.Y) * ray.oneOverDirection.Y;
			double maxDistToY = (bounds[1 - (int)ray.sign[1]].Y - ray.origin.Y) * ray.oneOverDirection.Y;

			if ((minDistFound > maxDistToY) || (minDistToY > maxDistFound))
			{
				return false;
			}

			if (minDistToY > minDistFound)
			{
				minDistFound = minDistToY;
			}

			if (maxDistToY < maxDistFound)
			{
				maxDistFound = maxDistToY;
			}

			// and finally the z planes
			double minDistToZ = (bounds[(int)ray.sign[2]].Z - ray.origin.Z) * ray.oneOverDirection.Z;
			double maxDistToZ = (bounds[1 - (int)ray.sign[2]].Z - ray.origin.Z) * ray.oneOverDirection.Z;

			if ((minDistFound > maxDistToZ) || (minDistToZ > maxDistFound))
			{
				return false;
			}

			if (minDistToZ > minDistFound)
			{
				minDistFound = minDistToZ;
			}

			if (maxDistToZ < maxDistFound)
			{
				maxDistFound = maxDistToZ;
			}

			bool withinDistanceToConsider = (minDistFound < ray.maxDistanceToConsider) && (maxDistFound > ray.minDistanceToConsider);
			return withinDistanceToConsider;
		}

		public void PerturbDirection(Random random)
		{
			directionNormal.X += 1e-5 * random.NextDouble();
			directionNormal.Y += 1e-5 * random.NextDouble();
			directionNormal.Z += 1e-5 * random.NextDouble();
		}
	}
}