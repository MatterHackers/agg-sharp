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
	public struct Plane
	{
		public double DistanceFromOrigin { get; set; }
		public Vector3 Normal { get; set; }

		private const double TreatAsZero = .000000001;

		public Plane(Vector3 planeNormal, double distanceFromOrigin)
		{
			this.Normal = planeNormal.GetNormal();
			this.DistanceFromOrigin = distanceFromOrigin;
		}

		public Plane(Vector3 point0, Vector3 point1, Vector3 point2)
		{
			this.Normal = Vector3Ex.Cross((point1 - point0), (point2 - point0)).GetNormal();
			this.DistanceFromOrigin = Vector3Ex.Dot(Normal, point0);
		}

		public Plane(Vector3Float point0, Vector3Float point1, Vector3Float point2)
		{
			this.Normal = new Vector3((point1 - point0).Cross(point2 - point0).GetNormal());
			this.DistanceFromOrigin = Normal.Dot(new Vector3(point0));
		}

		public override string ToString()
		{
			return $"D:{DistanceFromOrigin:0.###} V:x{Normal.X:0.###}, y{Normal.Y:0.###}, z{Normal.Z:0.###}";
		}

		public Plane(Vector3 planeNormal, Vector3 pointOnPlane)
		{
			this.Normal = planeNormal.GetNormal();
			this.DistanceFromOrigin = Vector3Ex.Dot(planeNormal, pointOnPlane);
		}

		public double this[int index]
		{
			get
			{
				switch (index)
				{
					case 0:
					case 1:
					case 2:
						return Normal[index];

					default:
						return DistanceFromOrigin;
				}
			}

			set
			{
				switch (index)
				{
					case 0:
					case 1:
					case 2:
						Vector3 normal = Normal;
						normal[index] = value;
						Normal = normal;
						break;

					default:
						DistanceFromOrigin = value;
						break;
				}
			}
		}

		public override bool Equals(object obj)
		{
			throw new NotImplementedException();
		}

		public override int GetHashCode()
		{
			throw new NotImplementedException();
		}

		public static bool operator ==(Plane left, Plane right)
		{
			return left.Equals(right);
		}

		public static bool operator !=(Plane left, Plane right)
		{
			return !left.Equals(right);
		}

		public bool Equals(Plane other)
		{
			return
				Normal == other.Normal
				&& DistanceFromOrigin == other.DistanceFromOrigin;
		}

		public double GetDistanceFromPlane(Vector3Float positionToCheck)
		{
			return GetDistanceFromPlane(new Vector3(positionToCheck));
		}
		public double GetDistanceFromPlane(Vector3 positionToCheck)
		{
			double distanceToPosition = Vector3Ex.Dot(Normal, positionToCheck);
			return distanceToPosition - DistanceFromOrigin;
		}

		/// <summary>
		/// Modify the start and end points so they fall on the normal side of the plane.
		/// </summary>
		/// <param name="startPoint"></param>
		/// <param name="endPoint"></param>
		/// <returns>Returns true if any part of the line is on the normal side of the plane else false.</returns>
		public bool ClipLine(ref Vector3 startPoint, ref Vector3 endPoint)
		{
			double startDistance = GetDistanceFromPlane(startPoint);
			double endDistance = GetDistanceFromPlane(endPoint);

			if(startDistance < 0)
			{
				if(endDistance < 0)
				{
					// both points are behind the plane
					return false;
				}

				// the start point must be clipped
				// get the normal in the direction of the start point
				Vector3 lineDirection = startPoint - endPoint;
				double lineLength = lineDirection.Length;
				Vector3 lineNormal = lineDirection / lineLength;
				double startClipRatio = startDistance / (endDistance - startDistance);
				startPoint = startPoint + lineNormal * (lineLength * startClipRatio);
			}
			else if(endDistance < 0)
			{
				// the end point must be clipped
				// get the normal in the direction of the start point
				Vector3 lineDirection = endPoint - startPoint;
				double lineLength = lineDirection.Length;
				Vector3 lineNormal = lineDirection / lineLength;
				double endClipRatio = endDistance / (startDistance - endDistance);
				endPoint = endPoint + lineNormal * (lineLength * endClipRatio);
			}

			// both points in front of the plane
			return true;
		}

		public bool Equals(Plane control, double normalError, double lengthError)
		{
			if(Normal.Equals(control.Normal, normalError)
				&& Math.Abs(DistanceFromOrigin - control.DistanceFromOrigin) < lengthError)
			{
				return true;
			}

			return false;
		}

		public static Plane Transform(Plane inputPlane, Matrix4X4 matrix)
		{
			Vector3 planeNormal = inputPlane.Normal;
			double distanceToPlane = inputPlane.DistanceFromOrigin;

			Plane outputPlane = new Plane();
			outputPlane.Normal = Vector3Ex.TransformVector(planeNormal, matrix).GetNormal();
			Vector3 pointOnPlane = planeNormal * distanceToPlane;
			Vector3 pointOnTransformedPlane = Vector3Ex.Transform(pointOnPlane, matrix);
			outputPlane.DistanceFromOrigin = Vector3Ex.Dot(outputPlane.Normal, pointOnTransformedPlane);

			return outputPlane;
		}

		public double GetDistanceToIntersection(Ray ray, out bool inFront)
		{
			inFront = false;
			double normalDotRayDirection = Vector3Ex.Dot(Normal, ray.directionNormal);
			if (normalDotRayDirection < TreatAsZero && normalDotRayDirection > -TreatAsZero) // the ray is parallel to the plane
			{
				return double.PositiveInfinity;
			}

			if (normalDotRayDirection < 0)
			{
				inFront = true;
			}

			return (DistanceFromOrigin - Vector3Ex.Dot(Normal, ray.origin)) / normalDotRayDirection;
		}

		public double GetDistanceToIntersection(Vector3 pointOnLine, Vector3 lineDirection)
		{
			double normalDotRayDirection = Vector3Ex.Dot(Normal, lineDirection);
			if (normalDotRayDirection < TreatAsZero && normalDotRayDirection > -TreatAsZero) // the ray is parallel to the plane
			{
				return double.PositiveInfinity;
			}

			double planeNormalDotPointOnLine = Vector3Ex.Dot(Normal, pointOnLine);
			return (DistanceFromOrigin - planeNormalDotPointOnLine) / normalDotRayDirection;
		}

		public bool RayHitPlane(Ray ray, out double distanceToHit, out bool hitFrontOfPlane)
		{
			distanceToHit = double.PositiveInfinity;
			hitFrontOfPlane = false;

			double normalDotRayDirection = Vector3Ex.Dot(Normal, ray.directionNormal);
			if (normalDotRayDirection < TreatAsZero && normalDotRayDirection > -TreatAsZero) // the ray is parallel to the plane
			{
				return false;
			}

			if (normalDotRayDirection < 0)
			{
				hitFrontOfPlane = true;
			}

			double distanceToRayOriginFromOrigin = Vector3Ex.Dot(Normal, ray.origin);

			double distanceToPlaneFromRayOrigin = DistanceFromOrigin - distanceToRayOriginFromOrigin;

			bool originInFrontOfPlane = distanceToPlaneFromRayOrigin < 0;

			bool originAndHitAreOnSameSide = originInFrontOfPlane == hitFrontOfPlane;
			if (!originAndHitAreOnSameSide)
			{
				return false;
			}

			distanceToHit = distanceToPlaneFromRayOrigin / normalDotRayDirection;
			return true;
		}

		public void Normalize()
		{
			Normalize(ref this);
		}

		public static void Normalize(ref Plane plane)
		{
			double length = plane.Normal.Length;
			plane.Normal = plane.Normal / length;
			plane.DistanceFromOrigin /= length;
		}

		public bool LineHitPlane(Vector3 start, Vector3 end, out Vector3 intersectionPosition)
		{
			double distanceToStartFromOrigin = Vector3Ex.Dot(Normal, start);
			if (distanceToStartFromOrigin == 0)
			{
				intersectionPosition = start;
				return true;
			}

			double distanceToEndFromOrigin = Vector3Ex.Dot(Normal, end);
			if (distanceToEndFromOrigin == 0)
			{
				intersectionPosition = end;
				return true;
			}

			if((distanceToStartFromOrigin < 0 && distanceToEndFromOrigin > 0)
				|| (distanceToStartFromOrigin > 0 && distanceToEndFromOrigin < 0))
			{
				Vector3 direction = (end - start).GetNormal();

				double startDistanceFromPlane = distanceToStartFromOrigin - DistanceFromOrigin;
				double endDistanceFromPlane = distanceToEndFromOrigin - DistanceFromOrigin;
				double lengthAlongPlanNormal = endDistanceFromPlane - startDistanceFromPlane;

				double ratioToPlanFromStart = startDistanceFromPlane / lengthAlongPlanNormal;
				intersectionPosition = start + direction * ratioToPlanFromStart;

				return true;
			}

			intersectionPosition = Vector3.PositiveInfinity;
			return false;
		}
	}
}