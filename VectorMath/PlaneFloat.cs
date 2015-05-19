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

namespace MatterHackers.VectorMath
{
	public class PlaneFloat
	{
		public float distanceToPlaneFromOrigin;
		public Vector3Float planeNormal;
		private const float TreatAsZero = .000001f;
		public PlaneFloat(Vector3Float planeNormal, float distanceFromOrigin)
		{
			this.planeNormal = planeNormal.GetNormal();
			this.distanceToPlaneFromOrigin = distanceFromOrigin;
		}

		public PlaneFloat(Vector3Float point0, Vector3Float point1, Vector3Float point2)
		{
			this.planeNormal = Vector3Float.Cross((point1 - point0), (point2 - point0)).GetNormal();
			this.distanceToPlaneFromOrigin = Vector3Float.Dot(planeNormal, point0);
		}

		public float GetDistanceFromPlane(Vector3Float positionToCheck)
		{
			float distanceToPointFromOrigin = Vector3Float.Dot(positionToCheck, planeNormal);
			return distanceToPointFromOrigin - distanceToPlaneFromOrigin;
		}

		public float GetDistanceToIntersection(Ray ray, out bool inFront)
		{
			inFront = false;
			float normalDotRayDirection = Vector3Float.Dot(planeNormal, new Vector3Float(ray.directionNormal));
			if (normalDotRayDirection < TreatAsZero && normalDotRayDirection > -TreatAsZero) // the ray is parallel to the plane
			{
				return float.PositiveInfinity;
			}

			if (normalDotRayDirection < 0)
			{
				inFront = true;
			}

			return (distanceToPlaneFromOrigin - Vector3Float.Dot(planeNormal, new Vector3Float(ray.origin))) / normalDotRayDirection;
		}

		public float GetDistanceToIntersection(Vector3Float pointOnLine, Vector3Float lineDirection)
		{
			float normalDotRayDirection = Vector3Float.Dot(planeNormal, lineDirection);
			if (normalDotRayDirection < TreatAsZero && normalDotRayDirection > -TreatAsZero) // the ray is parallel to the plane
			{
				return float.PositiveInfinity;
			}

			float planeNormalDotPointOnLine = Vector3Float.Dot(planeNormal, pointOnLine);
			return (distanceToPlaneFromOrigin - planeNormalDotPointOnLine) / normalDotRayDirection;
		}

		public bool RayHitPlane(Ray ray, out float distanceToHit, out bool hitFrontOfPlane)
		{
			distanceToHit = float.PositiveInfinity;
			hitFrontOfPlane = false;

			float normalDotRayDirection = Vector3Float.Dot(planeNormal, new Vector3Float(ray.directionNormal));
			if (normalDotRayDirection < TreatAsZero && normalDotRayDirection > -TreatAsZero) // the ray is parallel to the plane
			{
				return false;
			}

			if (normalDotRayDirection < 0)
			{
				hitFrontOfPlane = true;
			}

			float distanceToRayOriginFromOrigin = Vector3Float.Dot(planeNormal, new Vector3Float(ray.origin));

			float distanceToPlaneFromRayOrigin = distanceToPlaneFromOrigin - distanceToRayOriginFromOrigin;

			bool originInFrontOfPlane = distanceToPlaneFromRayOrigin < 0;

			bool originAndHitAreOnSameSide = originInFrontOfPlane == hitFrontOfPlane;
			if (!originAndHitAreOnSameSide)
			{
				return false;
			}

			distanceToHit = distanceToPlaneFromRayOrigin / normalDotRayDirection;
			return true;
		}
	}
}