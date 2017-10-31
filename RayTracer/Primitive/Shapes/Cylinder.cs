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

using MatterHackers.Agg;
using MatterHackers.VectorMath;
using System;
using System.Collections;

namespace MatterHackers.RayTracer
{
	public class CylinderShape : BaseShape
	{
		public double radius;
		public double topRadius;
		public double height;

		private Plane topPlane;
		private Plane bottomPlane;

		public CylinderShape(double bottomRadius, double topRadius, double height, MaterialAbstract material)
		{
			this.radius = bottomRadius;
			this.topRadius = topRadius;
			this.height = height;
			this.Material = material;

			topPlane = new Plane(Vector3.UnitZ, height / 2);
			bottomPlane = new Plane(-Vector3.UnitZ, height / 2);
		}

		public CylinderShape(double radius, double height, MaterialAbstract material)
			: this(radius, radius, height, material)
		{
		}

		public override double GetSurfaceArea()
		{
			double bottomPerimeter = MathHelper.Tau * radius;
			double topPerimeter = MathHelper.Tau * topRadius;

			double areaOfCurvedSurface = (bottomPerimeter + topPerimeter) / 2 * height;

			double areaOfBottom = MathHelper.Tau / 2 * radius * radius;
			double areaOfTop = MathHelper.Tau / 2 * topRadius * topRadius;

			return areaOfCurvedSurface + areaOfBottom + areaOfTop;
		}

		public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			double maxRadius = Math.Max(radius, topRadius);
			return new AxisAlignedBoundingBox(new Vector3(-maxRadius, -maxRadius, -height / 2),
				new Vector3(maxRadius, maxRadius, height / 2));
		}

		public override double GetIntersectCost()
		{
			return 1288;
		}

		public override ColorF GetColor(IntersectInfo info)
		{
			if (Material.HasTexture)
			{
				throw new NotImplementedException();
			}
			else
			{
				// skip uv calculation, just get the color
				return this.Material.GetColor(0, 0);
			}
		}

		private double GetRadiusAtHeight(double height)
		{
			return 0;
		}

		public override IntersectInfo GetClosestIntersection(Ray ray)
		{
			double radiusSquared = radius * radius;

			Vector2 rayOrigin = new Vector2(ray.origin);
			Vector2 rayDirectionXY = new Vector2(ray.directionNormal);
			Vector2 rayDirection = rayDirectionXY.GetNormal();
			Vector2 thisPosition = Vector2.Zero;
			Vector2 deltaFromShpereCenterToRayOrigin = rayOrigin - thisPosition;
			double distanceFromCircleCenterToRayOrigin = Vector2.Dot(deltaFromShpereCenterToRayOrigin, rayDirection); // negative means the Circle is in front of the ray.
			double lengthFromRayOrginToCircleCenterSquared = Vector2.Dot(deltaFromShpereCenterToRayOrigin, deltaFromShpereCenterToRayOrigin);
			double lengthFromRayOrigintoNearEdgeOfCircleSquared = lengthFromRayOrginToCircleCenterSquared - radiusSquared;
			double distanceFromCircleCenterToRaySquared = distanceFromCircleCenterToRayOrigin * distanceFromCircleCenterToRayOrigin;
			double amountCircleCenterToRayIsGreaterThanRayOriginToEdgeSquared = distanceFromCircleCenterToRaySquared - lengthFromRayOrigintoNearEdgeOfCircleSquared;

			if (amountCircleCenterToRayIsGreaterThanRayOriginToEdgeSquared > 0)
			{
				{
					bool inFrontOfTop;
					double testDistanceToHit = topPlane.GetDistanceToIntersection(ray, out inFrontOfTop);
					bool wantFrontAndInFront = (ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace && inFrontOfTop;
					bool wantBackAndInBack = (ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace && !inFrontOfTop;
					if (wantFrontAndInFront || wantBackAndInBack)
					{
						Vector3 topHitPosition = ray.origin + ray.directionNormal * testDistanceToHit;

						if (topHitPosition.X * topHitPosition.X + topHitPosition.Y * topHitPosition.Y < topRadius * topRadius)
						{
							IntersectInfo topHitInfo = new IntersectInfo();
							topHitInfo.HitPosition = topHitPosition;
							topHitInfo.closestHitObject = this;
							if (ray.intersectionType == IntersectionType.FrontFace)
							{
								topHitInfo.hitType = IntersectionType.FrontFace;
								topHitInfo.normalAtHit = topPlane.PlaneNormal;
							}
							else
							{
								topHitInfo.hitType = IntersectionType.BackFace;
								topHitInfo.normalAtHit = -topPlane.PlaneNormal;
							}
							topHitInfo.distanceToHit = testDistanceToHit;

							return topHitInfo;
						}
					}
				}

				{
					bool inFrontOfBottom;
					double testDistanceToHit = bottomPlane.GetDistanceToIntersection(ray, out inFrontOfBottom);
					if (ray.intersectionType == IntersectionType.FrontFace && inFrontOfBottom
						|| ray.intersectionType == IntersectionType.BackFace && !inFrontOfBottom)
					{
						Vector3 bottomHitPosition = ray.origin + ray.directionNormal * testDistanceToHit;

						if (bottomHitPosition.X * bottomHitPosition.X + bottomHitPosition.Y * bottomHitPosition.Y < radius * radius)
						{
							IntersectInfo bottomHitInfo = new IntersectInfo();
							bottomHitInfo.HitPosition = bottomHitPosition;
							bottomHitInfo.closestHitObject = this;
							if (ray.intersectionType == IntersectionType.FrontFace)
							{
								bottomHitInfo.hitType = IntersectionType.FrontFace;
								bottomHitInfo.normalAtHit = bottomPlane.PlaneNormal;
							}
							else
							{
								bottomHitInfo.hitType = IntersectionType.BackFace;
								bottomHitInfo.normalAtHit = -bottomPlane.PlaneNormal;
							}
							bottomHitInfo.distanceToHit = testDistanceToHit;

							return bottomHitInfo;
						}
					}
				}

				IntersectInfo info = new IntersectInfo();
				info.closestHitObject = this;
				info.hitType = IntersectionType.FrontFace;
				if (ray.isShadowRay)
				{
					return info;
				}
				double distanceFromRayOriginToCircleCenter = -distanceFromCircleCenterToRayOrigin;

				double amountCircleCenterToRayIsGreaterThanRayOriginToEdge = Math.Sqrt(amountCircleCenterToRayIsGreaterThanRayOriginToEdgeSquared);

				double scaleRatio = ray.directionNormal.Length / rayDirectionXY.Length;

				if (ray.intersectionType == IntersectionType.FrontFace)
				{
					double distanceToFrontHit = (distanceFromRayOriginToCircleCenter - amountCircleCenterToRayIsGreaterThanRayOriginToEdge) * scaleRatio;
					if (distanceToFrontHit > ray.maxDistanceToConsider || distanceToFrontHit < ray.minDistanceToConsider)
					{
						return null;
					}
					info.distanceToHit = distanceToFrontHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.distanceToHit;
					if (info.HitPosition.Z < -height / 2 || info.HitPosition.Z > height / 2)
					{
						return null;
					}
					info.normalAtHit = new Vector3(info.HitPosition.X, info.HitPosition.Y, 0).GetNormal();
				}
				else if (ray.intersectionType == IntersectionType.BackFace)// check back faces
				{
					double distanceToBackHit = (distanceFromRayOriginToCircleCenter + amountCircleCenterToRayIsGreaterThanRayOriginToEdge) * scaleRatio;
					if (distanceToBackHit > ray.maxDistanceToConsider || distanceToBackHit < ray.minDistanceToConsider)
					{
						return null;
					}
					info.hitType = IntersectionType.BackFace;
					info.distanceToHit = distanceToBackHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.distanceToHit;
					if (info.HitPosition.Z < height / 2 || info.HitPosition.Z > height / 2)
					{
						return null;
					}
					info.normalAtHit = -(new Vector3(info.HitPosition.X, info.HitPosition.Y, 0).GetNormal());
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

			Vector2 rayOrigin = new Vector2(ray.origin);
			Vector2 rayDirectionXY = new Vector2(ray.directionNormal);
			Vector2 rayDirection = rayDirectionXY.GetNormal();
			Vector2 thisPosition = Vector2.Zero;
			Vector2 deltaFromShpereCenterToRayOrigin = rayOrigin - thisPosition;
			double distanceFromCircleCenterToRayOrigin = Vector2.Dot(deltaFromShpereCenterToRayOrigin, rayDirection);
			double lengthFromRayOrginToCircleCenterSquared = Vector2.Dot(deltaFromShpereCenterToRayOrigin, deltaFromShpereCenterToRayOrigin);
			double lengthFromRayOrigintoNearEdgeOfCircleSquared = lengthFromRayOrginToCircleCenterSquared - radiusSquared;
			double distanceFromCircleCenterToRaySquared = distanceFromCircleCenterToRayOrigin * distanceFromCircleCenterToRayOrigin;
			double amountCircleCenterToRayIsGreaterThanRayOriginToEdgeSquared = distanceFromCircleCenterToRaySquared - lengthFromRayOrigintoNearEdgeOfCircleSquared;

			if (amountCircleCenterToRayIsGreaterThanRayOriginToEdgeSquared > 0)
			{
				double distanceFromRayOriginToCircleCenter = -distanceFromCircleCenterToRayOrigin;
				double amountCircleCenterToRayIsGreaterThanRayOriginToEdge = Math.Sqrt(amountCircleCenterToRayIsGreaterThanRayOriginToEdgeSquared);
				double scaleRatio = ray.directionNormal.Length / rayDirectionXY.Length;

				if ((ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace)
				{
					IntersectInfo info = new IntersectInfo();
					info.hitType = IntersectionType.FrontFace;
					info.closestHitObject = this;
					double distanceToFrontHit = (distanceFromRayOriginToCircleCenter - amountCircleCenterToRayIsGreaterThanRayOriginToEdge) * scaleRatio;
					info.distanceToHit = distanceToFrontHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.distanceToHit;
					if (info.HitPosition.Z > -height / 2 && info.HitPosition.Z < height / 2)
					{
						info.normalAtHit = new Vector3(info.HitPosition.X, info.HitPosition.Y, 0).GetNormal();
						yield return info;
					}
				}

				if ((ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace)
				{
					IntersectInfo info = new IntersectInfo();
					info.hitType = IntersectionType.BackFace;
					info.closestHitObject = this;
					double distanceToBackHit = (distanceFromRayOriginToCircleCenter + amountCircleCenterToRayIsGreaterThanRayOriginToEdge) * scaleRatio;
					info.distanceToHit = distanceToBackHit;
					info.HitPosition = ray.origin + ray.directionNormal * info.distanceToHit;
					if (info.HitPosition.Z > -height / 2 && info.HitPosition.Z < height / 2)
					{
						info.normalAtHit = -(new Vector3(info.HitPosition.X, info.HitPosition.Y, 0).GetNormal());
						yield return info;
					}
				}

				{
					bool inFrontOfTopFace;
					double testDistanceToHit = topPlane.GetDistanceToIntersection(ray, out inFrontOfTopFace);
					Vector3 topHitPosition = ray.origin + ray.directionNormal * testDistanceToHit;

					if (topHitPosition.X * topHitPosition.X + topHitPosition.Y * topHitPosition.Y < topRadius * topRadius)
					{
						if ((ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace && inFrontOfTopFace)
						{
							IntersectInfo topHitInfo = new IntersectInfo();
							topHitInfo.HitPosition = topHitPosition;
							topHitInfo.closestHitObject = this;
							topHitInfo.hitType = IntersectionType.FrontFace;
							topHitInfo.normalAtHit = topPlane.PlaneNormal;
							topHitInfo.distanceToHit = testDistanceToHit;

							yield return topHitInfo;
						}

						if ((ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace && !inFrontOfTopFace)
						{
							IntersectInfo topHitInfo = new IntersectInfo();
							topHitInfo.HitPosition = topHitPosition;
							topHitInfo.closestHitObject = this;
							topHitInfo.hitType = IntersectionType.BackFace;
							topHitInfo.normalAtHit = -topPlane.PlaneNormal;
							topHitInfo.distanceToHit = testDistanceToHit;

							yield return topHitInfo;
						}
					}
				}

				{
					bool inFrontOfBottomFace;
					double testDistanceToHit = bottomPlane.GetDistanceToIntersection(ray, out inFrontOfBottomFace);
					Vector3 bottomHitPosition = ray.origin + ray.directionNormal * testDistanceToHit;

					if (bottomHitPosition.X * bottomHitPosition.X + bottomHitPosition.Y * bottomHitPosition.Y < radius * radius)
					{
						if ((ray.intersectionType & IntersectionType.FrontFace) == IntersectionType.FrontFace && inFrontOfBottomFace)
						{
							IntersectInfo bottomHitInfo = new IntersectInfo();
							bottomHitInfo.HitPosition = bottomHitPosition;
							bottomHitInfo.closestHitObject = this;
							bottomHitInfo.hitType = IntersectionType.FrontFace;
							bottomHitInfo.normalAtHit = bottomPlane.PlaneNormal;
							bottomHitInfo.distanceToHit = testDistanceToHit;

							yield return bottomHitInfo;
						}

						if ((ray.intersectionType & IntersectionType.BackFace) == IntersectionType.BackFace && !inFrontOfBottomFace)
						{
							IntersectInfo bottomHitInfo = new IntersectInfo();
							bottomHitInfo.HitPosition = bottomHitPosition;
							bottomHitInfo.closestHitObject = this;
							bottomHitInfo.hitType = IntersectionType.BackFace;
							bottomHitInfo.normalAtHit = -bottomPlane.PlaneNormal;
							bottomHitInfo.distanceToHit = testDistanceToHit;

							yield return bottomHitInfo;
						}
					}
				}
			}
		}

		public override string ToString()
		{
			return string.Format("Cylinder");
		}
	}

#if false
/*
 *
 * RayTrace Software Package, release 3.0.  May 3, 2006.
 *
 * Author: Samuel R. Buss
 *
 * Software accompanying the book
 *		3D Computer Graphics: A Mathematical Introduction with OpenGL,
 *		by S. Buss, Cambridge University Press, 2003.
 *
 * Software is "as-is" and carries no warranty.  It may be used without
 *   restriction, but if you modify it, please change the filenames to
 *   prevent confusion between different versions.  Please acknowledge
 *   all use of the software in any publications or products based on it.
 *
 * Bug reports: Sam Buss, sbuss@ucsd.edu.
 * Web page: http://math.ucsd.edu/~sbuss/MathCG
 *
 */

public class ViewableCylinder
{
	Vector3 CenterAxis;		// Unit vector up the center
	Vector3 Center;			// Point on the central axis
	Vector3 AxisA;				// Radial axis A - length equals 1/RadiusA
	Vector3 AxisB;				// Radial axis B - length equals 1/RadiusB
	double RadiusA;
	double RadiusB;

	double Height;				// Height of right cylinder
	double HalfHeight;			// Height/2
	double CenterDotAxis;		// Center^CenterAxis

	// Used for non-right cylinders:
	Vector3 TopNormal;				// Unit vector - normal outward from top
	double TopPlaneCoef;
	Vector3 BottomNormal;
	double BottomPlaneCoef;			// Unit vector - normal outward from bottom

	bool IsRightCylinderFlag;	// True for right cylinders
								// False for general bounding planes

	BaseMaterial SideOuterMat;
	BaseMaterial SideInnerMat;
	BaseMaterial TopOuterMat;
	BaseMaterial TopInnerMat;
	BaseMaterial BottomOuterMat;
	BaseMaterial BottomInnerMat;

    // Constructors
	public ViewableCylinder()
    {
        Reset();
    }

	public void Reset()
    {
        CenterAxis = Vector3.UnitY;
        AxisA = Vector3.UnitZ;
        AxisB = Vector3.UnitX;
        SetCenter(Vector3.Zero);
        SetHeight(1.0);
        RadiusA = 1.0;
        RadiusB = 1.0;
        SetMaterial(new SolidMaterial(RGBA_Floats.Cyan, 0, 0, 0));
    }

	// Returns an intersection if found with distance maxDistance
	// viewDir must be a unit vector.
	// intersectDistance and visPoint are returned values.
    bool FindIntersectionNT (Vector3 viewPos, Vector3 viewDir, double maxDistance,
		out double intersectDistance, out Vector2 returnedPoint )
{
	double maxFrontDist = double.NegativeInfinity;
	double minBackDist = double.PositiveInfinity;
	int frontType, backType;		// 0, 1, 2 = top, bottom, side

	// Start with the bounding planes
	if ( IsRightCylinder() ) {
		double pdotn = Vector3.Dot(viewPos, CenterAxis) - CenterDotAxis;
		double udotn = Vector3.Dot(viewDir, CenterAxis);
		if ( pdotn > HalfHeight )
        {
			if ( udotn>=0.0 )
            {
				return false;		// Above top plane pointing up
			}
			// Hits top from above
			maxFrontDist = (HalfHeight-pdotn)/udotn;
			frontType = 0;
			minBackDist = -(HalfHeight+pdotn)/udotn;
			backType = 1;
		}
		else if ( pdotn < -HalfHeight )
        {
			if ( udotn<=0.0 )
            {
				return false;		// Below bottom, pointing down
			}
			// Hits bottom plane from below
			maxFrontDist = -(HalfHeight+pdotn)/udotn;
			frontType = 1;
			minBackDist = (HalfHeight-pdotn)/udotn;
			backType = 0;
		}
		else if ( udotn<0.0 )
        {  // Inside, pointing down
			minBackDist = -(HalfHeight+pdotn)/udotn;
			backType = 1;
		}
		else if ( udotn>0.0 )
        {		// Inside, pointing up
			minBackDist = (HalfHeight-pdotn)/udotn;
			backType = 0;
		}
	}
	else
    {
		// Has two bounding planes (not right cylinder)
		// First handle the top plane
		double pdotnCap = Vector3.Dot(TopNormal, viewPos);
		double udotnCap = Vector3.Dot(TopNormal, viewDir);
		if ( pdotnCap>TopPlaneCoef ) {
			if ( udotnCap>=0.0 ) {
				return false;		// Above top plane, pointing up
			}
			maxFrontDist = (TopPlaneCoef-pdotnCap)/udotnCap;
			frontType = 0;
		}
		else if ( pdotnCap<TopPlaneCoef ) {
			if ( udotnCap>0.0 ) {
				// Below top plane, pointing up
				minBackDist = (TopPlaneCoef-pdotnCap)/udotnCap;
				backType = 0;
			}
		}
		// Second, handle the bottom plane
		pdotnCap = Vector3.Dot(BottomNormal, viewPos);
		udotnCap = Vector3.Dot(BottomNormal^viewDir);
		if ( pdotnCap<BottomPlaneCoef )
        {
			if ( udotnCap>0.0 )
            {
				double newBackDist = (BottomPlaneCoef-pdotnCap)/udotnCap;
				if ( newBackDist<maxFrontDist )
                {
					return false;
				}
				if ( newBackDist<minBackDist )
                {
					minBackDist = newBackDist;
					backType = 1;
				}
			}
		}
		else if ( pdotnCap>BottomPlaneCoef )
        {
			if ( udotnCap>=0.0 )
            {
				return false;		// Above bottom plane, pointing up (away)
			}
			// Above bottom plane, pointing down
			double newFrontDist = (BottomPlaneCoef-pdotnCap)/udotnCap;
			if ( newFrontDist>minBackDist )
            {
				return false;
			}
			if ( newFrontDist>maxFrontDist )
            {
				maxFrontDist = newFrontDist;
				frontType = 1;
			}
		}
	}
	if ( maxFrontDist>maxDistance )
    {
		return false;
	}

	// Now handle the cylinder sides
	Vector3 v = viewPos;
	v -= Center;
	double pdotuA = Vector3.Dot(v, AxisA);
	double pdotuB = Vector3.Dot(v, AxisB);
	double udotuA = Vector3.Dot(viewDir, AxisA);
	double udotuB = Vector3.Dot(viewDir, AxisB);

	double C = pdotuA*pdotuA + pdotuB*pdotuB - 1.0;
	double B = (pdotuA*udotuA + pdotuB*udotuB);

	if ( C>=0.0 && B>0.0 )
    {
		return false;			// Pointing away from the cylinder
	}

	B += B;		// Double B for final 2.0 factor

	double A = udotuA*udotuA+udotuB*udotuB;

	double alpha1, alpha2;	// The roots, in order
	int numRoots = QuadraticSolveRealSafe(A, B, C, out alpha1, out alpha2);
	if ( numRoots==0 )
    {
		return false;		// No intersection
	}
	if ( alpha1>maxFrontDist )
    {
		if ( alpha1>minBackDist )
        {
			return false;
		}
		maxFrontDist = alpha1;
		frontType = 2;
	}
	if ( numRoots==2 && alpha2<minBackDist )
    {
		if ( alpha2<maxFrontDist )
        {
			return false;
		}
		minBackDist = alpha2;
		backType = 2;
	}

	// Put it all together:

	double alpha;
	int hitSurface;
	if ( maxFrontDist>0.0 )
    {
		returnedPoint.SetFrontFace();	// Hit from outside
		alpha = maxFrontDist;
		hitSurface = frontType;
	}
	else
    {
		returnedPoint.SetBackFace();	// Hit from inside
		alpha = minBackDist;
		hitSurface = backType;
	}

	if ( alpha >= maxDistance )
    {
		return false;
	}

	intersectDistance = alpha;
	// Set v to the intersection point
	v = viewDir;
	v *= alpha;
	v += viewPos;
	returnedPoint.SetPosition( v );		// Intersection point

	// Now set v equal to returned position relative to the center
	v -= Center;
	double vdotuA = Vector3(v, AxisA);
	double vdotuB = Vector3(v, AxisB);

	switch ( hitSurface )
    {
	case 0:		// Top surface
		returnedPoint.SetNormal( TopNormal );
		if ( returnedPoint.IsFrontFacing() )
        {
			returnedPoint.SetMaterial(TopOuterMat );
		}
		else
        {
			returnedPoint.SetMaterial(TopInnerMat );
		}

		// Calculate U-V values for texture coordinates
		vdotuA = 0.5*(1.0-vdotuA);
		vdotuB = 0.5*(1.0+vdotuB);
		returnedPoint.SetUV( vdotuA, vdotuB );
		returnedPoint.SetFaceNumber( TopFaceNum );
		break;

	case 1:		// Bottom face
		returnedPoint.SetNormal( BottomNormal );
		if ( returnedPoint.IsFrontFacing() )
        {
			returnedPoint.SetMaterial( *BottomOuterMat );
		}
		else
        {
			returnedPoint.SetMaterial( *BottomInnerMat );
		}

		// Calculate U-V values for texture coordinates
		vdotuA = 0.5*(1.0+vdotuA);
		vdotuB = 0.5*(1.0+vdotuB);
		returnedPoint.SetUV( vdotuA, vdotuB );
		returnedPoint.SetFaceNumber( BaseFaceNum );
		break;

	case 2:		// Cylinder's side
		Vector3 normal;
		normal = vdotuA*AxisA;
		normal += vdotuB*AxisB;
		normal.Normalize();
		returnedPoint.SetNormal( normal );
		if ( returnedPoint.IsFrontFacing() )
        {
			returnedPoint.SetMaterial( SideOuterMat );
		}
		else
        {
			returnedPoint.SetMaterial( SideInnerMat );
		}

		// Calculate u-v coordinates for texture mapping (in range[0,1]x[0,1])
		double uCoord = Math.Atan2( vdotuB, vdotuA )/ MathHelper.Tau + 0.5;
		double vCoord;
		if ( IsRightCylinder() )
        {
			vCoord = (Vector3.Dot(v, CenterAxis)+HalfHeight)/Height;
		}
		else
        {
			Vector3 hitPos=returnedPoint.GetPosition();
			double distUp = (TopPlaneCoef - Vector3.Dot(hitPos, TopNormal))/ Vector3.Dot(CenterAxis, TopNormal);
			double distDown = -(BottomPlaneCoef-Vector3.Dot(hitPos, BottomNormal))/Vector3.Dot(CenterAxis, BottomNormal);
			if ( distDown+distUp > 0.0 ) {
				vCoord = distDown/(distDown+distUp);
			}
			else {
				vCoord = 0.5;	// At corner
			}
		}
		returnedPoint.SetUV( uCoord, vCoord );
		returnedPoint.SetFaceNumber( SideFaceNum );
	}
	return true;
}

void CalcBoundingPlanes(Vector3 u, out double minDot, out double maxDot )
{
	double centerDot = Vector3.Dot(u, Center);
	double AxisCdotU = Vector3.Dot(CenterAxis, u);
	if ( IsRightCylinderFlag )
    {
		double deltaDot = HalfHeight*Math.Abs(AxisCdotU)
							+ Math.Sqrt(Square(RadiusA*RadiusA*Vector3.Dot(AxisA,u))+Square(RadiusB*RadiusB*Vector3.Dot(AxisB,u)));
		minDot = centerDot - deltaDot;
		maxDot = centerDot + deltaDot;
		return;
	}

	double maxD, minD;
	// Handle top face
	Vector3 perp = TopNormal;
	perp *= u;
	double alpha = Vector3.Dot(perp, AxisA)*RadiusA;
	double beta = Vector3.Dot(perp, AxisB)*RadiusB;
	if ( alpha==0.0 && beta==0.0 )
    {	// If u perpindicular to top face
		maxD = minD = TopPlaneCoef*Vector3.Dot(u,TopNormal);
	}
	else
    {
		double solnX = -beta*RadiusA*RadiusA;
		double solnY = alpha*RadiusB*RadiusB;
		double ratio = Math.Sqrt( Square(alpha*RadiusB) + Square(beta*RadiusA) );
		solnX /= ratio;									// Now solnX and solnY give point on cylinder to check
		solnY /= ratio;
		Vector3 trial = perp;					// Be careful: reuse of Vector3 to avoid constructor overhead
		trial = Center;
		trial += AxisA * (solnX*RadiusA);
		trial += AxisB * (solnY*RadiusB);  // This is what it was. LBB - trial.AddScaled( AxisB, solnY*RadiusB );
		maxD = minD = Vector3.Dot(trial,u) + (TopPlaneCoef-Vector3.Dot(trial, TopNormal))*AxisCdotU/ Vector3.Dot(CenterAxis, TopNormal);
		trial = Center;
		trial += AxisA * (-solnX*RadiusA );
		trial += AxisB * (-solnY*RadiusB );
		double newDot = Vector3.Dot(trial, u) + (TopPlaneCoef-Vector3.Dot(trial, TopNormal))*AxisCdotU/Vector3.Dot(CenterAxis, TopNormal);
		UpdateMinMax( newDot, minD, maxD );
	}

	// Handle bottom face
	perp = BottomNormal;
	perp *= u;
	alpha = Vector3.Dot(perp, AxisA)*RadiusA;
	beta = Vector3.Dot(perp, AxisB)*RadiusB;
	if ( alpha==0.0 && beta==0.0 )
    {			// If u perpindicular to bottom face
		UpdateMinMax( BottomPlaneCoef*Vector3.Dot(u, BottomNormal), minD, maxD );
	}
	else
    {
		double solnX = -beta*RadiusA*RadiusA;
		double solnY = alpha*RadiusB*RadiusB;
		double ratio = Math.Sqrt( Square(alpha*RadiusB) + Square(beta*RadiusA) );
		solnX /= ratio;									// Now solnX and solnY give point on cylinder to check
		solnY /= ratio;
		Vector3 trial = Center;
		trial += AxisA * ( solnX*RadiusA );
		trial += AxisB * ( solnY*RadiusB );
		double newDot = Vector3.Dot(trial, u) + (BottomPlaneCoef-Vector3.Dot(trial, BottomNormal))*AxisCdotU/Vector3.Dot(CenterAxis, BottomNormal);
		UpdateMinMax( newDot, minD, maxD );
		trial = Center;
		trial += AxisA * ( -solnX*RadiusA );
		trial += AxisB * ( -solnY*RadiusB );
		newDot = Vector3.Dot(trial, u) + (BottomPlaneCoef-Vector3.Dot(trial, BottomNormal))*AxisCdotU/Vector3.Dot(CenterAxis, BottomNormal);
		UpdateMinMax( newDot, minD, maxD );
	}

	minDot = minD;
	maxDot = maxD;
}

private double Square(double p)
{
 	return p * p;
}

	// SetCenterAxis should be called before the other set routines, otherwise
	//		strange effects can occur.  SetCenterAxis() chooses radial axes
	//		automatically -- this can be overridden by  calling SetRadialAxes AFTERWARDS.
	void SetCenterAxis( double x, double y, double z );
	void SetCenterAxis( const double* axisC );
	void SetCenterAxis( const float* axisC );
	void SetCenterAxis( const Vector3& axisC );

	// You should call SetRadius() for circular cylinders;
	//		or call both SetRadii and SetRadialAxes for
	//		elliptical cross-section cylinders.
	void SetRadius( double radius ) { SetRadii(radius,radius);}	// Makes it a circular cylinder with that radius
	void SetRadii(double radiusA, double radiusB);  // Radii for the two radial axes

	// SetRadialAxes should be called *after* SetCenterAxis
	// The radial axes are also used for calculating u, v coordinates.
	//    the u=0.5 line is in the direction of Axis A.
	void SetRadialAxes( const Vector3& axisA, const Vector3& axisB );

	// For right cylinders, SetCenter is the center point.  For non-right
	//	cylinders, the center is just any point on the central axis.
void SetCenter( Vector3 center )
{
	this.Center = center;

	CenterDotAxis = Vector3.Dot(Center, CenterAxis);
	if ( IsRightCylinder() )
{
		TopPlaneCoef = CenterDotAxis+HalfHeight;
		BottomPlaneCoef = -(CenterDotAxis-HalfHeight);
	}
}

	// A "right cylinder" is a cylinder with the base and top faces
	//  perpindicular to the center axis.
	// Calling either SetHeight makes the cylinder
	//	into a right cylinder.  For right cylinders you should call SetHeight and
	//	SetCenter().
	void SetHeight( double height );

	// Here is an alternative to right cylinders.
	// Cylinders can also be defined with arbitrary planes bounding the top and
	//	and bottom of the cylinder.
	// The planes are specified by (a) outward normals (unit vectors!)
	//	and by (b) the plane coefficient.  The plane is {x : x^(planenormal)=planeCoef }
	void SetBottomFace( const Vector3& planenormal, double planecoef );
	void SetTopFace( const Vector3& planenormal, double planeCoef );

	bool IsRightCylinder() const { return IsRightCylinderFlag; }

	// SetMaterial() sets all the materials at once.
	// SetMaterialInner() - sets all the inner materials at once.
	// SetMaterialOuter() - sets all the outer materials at once.
	void SetMaterial(const MaterialBase *material);
	void SetMaterialInner(const MaterialBase *material);
	void SetMaterialOuter(const MaterialBase *material);
	void SetMaterialSideInner(const MaterialBase *material);
	void SetMaterialSideOuter(const MaterialBase *material);
	void SetMaterialTopInner(const MaterialBase *material);
	void SetMaterialTopOuter(const MaterialBase *material);
	void SetMaterialBottomInner(const MaterialBase *material);
	void SetMaterialBottomOuter(const MaterialBase *material);

	// U-V coordinates are returned with the sides in [0,1]x[0,1], the bottom
	//		in [-1,0]x[-1,0], and the top in [1,2]x[1,2].

	// Get routines
	void GetCenter( double* center ) const { Center.Dump( center ); }
	void GetCenter( float* center ) const { Center.Dump( center ) ; }
	const Vector3& GetCenter() const { return Center; }
	void GetCenterAxis( double* center ) const { CenterAxis.Dump( center ); }
	void GetCenterAxis( float* center ) const { CenterAxis.Dump( center ); }
	const Vector3& GetCenterAxis() const { return CenterAxis; }
	const Vector3 GetAxisA() const { return RadiusA*AxisA; }
	const Vector3 GetAxisB() const { return RadiusB*AxisB; }
	// InvScaled Axis A values are scaled by (1/RadiusA).
	void GetInvScaledAxisA( double* center ) const { AxisA.Dump( center ); }
	void GetInvScaledAxisA( float* center ) const { AxisA.Dump( center ); }
	const Vector3& GetInvScaledAxisA() const { return AxisA; }
	// InvScaled Axis B values are scaled by (1/RadiusB).
	void GetInvScaledAxisB( double* center ) const { AxisB.Dump( center ); }
	void GetInvScaledAxisB( float* center ) const { AxisB.Dump( center ); }
	const Vector3& GetInvScaledAxisB() const { return AxisB; }
	double GetRadiusA( ) const { return RadiusA; }
	double GetRadiusB( ) const { return RadiusB; }
	double GetHeight( ) const { return Height; }
	void GetBottomFace( Vector3* planenormal, double* planeCoef ) const
		{ *planenormal = BottomNormal; *planeCoef = BottomPlaneCoef; }
	void GetTopFace( Vector3* planenormal, double* planeCoef ) const
		{ *planenormal = TopNormal; *planeCoef = TopPlaneCoef; }
	const MaterialBase* GetMaterialSideOuter() const { return SideOuterMat; }
	const MaterialBase* GetMaterialSideInner() const { return SideInnerMat; }
	const MaterialBase* GetMaterialTopOuter() const { return TopOuterMat; }
	const MaterialBase* GetMaterialTopInner() const { return TopInnerMat; }
	const MaterialBase* GetMaterialBottomOuter() const { return BottomOuterMat; }
	const MaterialBase* GetMaterialBottomInner() const { return BottomInnerMat; }

	enum {
		SideFaceNum = 0,
		BaseFaceNum = 1,
		TopFaceNum = 2
	};
}

inline void ViewableCylinder::SetCenterAxis( const Vector3& axisC )
{
	CenterAxis = axisC;
	CenterAxis.Normalize();

	// Fix up the radial axes
	GetOrtho(CenterAxis, AxisA, AxisB);
	AxisA /= RadiusA;
	AxisB /= RadiusB;

	CenterDotAxis = Center^CenterAxis;

	// Re - set the height (to fix top & bottom normals)
	if ( IsRightCylinder() ) {
		TopNormal = CenterAxis;
		BottomNormal = CenterAxis;
		BottomNormal.Negate();
		TopPlaneCoef = CenterDotAxis+HalfHeight;
		BottomPlaneCoef = -(CenterDotAxis-HalfHeight);
	}
}

inline void ViewableCylinder::SetRadii( double radiusA, double radiusB )
{
	RadiusA = radiusA;
	RadiusB = radiusB;
	assert ( RadiusA>0.0 && RadiusB>0.0 );
	AxisA *= 1.0/(RadiusA*AxisA.Norm());
	AxisB *= 1.0/(RadiusB*AxisB.Norm());
}

inline void ViewableCylinder::SetRadialAxes( const Vector3& axisA,
											const Vector3& axisB )
{
	AxisA = axisA;
	AxisA -= (AxisA^CenterAxis)*CenterAxis;	// Make perpindicular to CenterAxis
	assert( AxisA.Norm()!=0.0 );			// Must not be parallel to CenterAxis
	AxisA /= RadiusA*AxisA.Norm();
	AxisB = axisB;
	AxisB -= (AxisB^CenterAxis)*CenterAxis;	// Make perpindicular to CenterAxis
	assert( AxisB.Norm()!=0.0 );			// Must not be parallel to CenterAxis
	AxisB /= RadiusB*AxisB.Norm();
}

inline void ViewableCylinder::SetHeight( double height )
{
	IsRightCylinderFlag = true;
	Height = height;
	HalfHeight = Height*0.5f;
	TopNormal = CenterAxis;
	BottomNormal = CenterAxis;
	BottomNormal.Negate();
	TopPlaneCoef = CenterDotAxis+HalfHeight;
	BottomPlaneCoef = -(CenterDotAxis-HalfHeight);
}

inline void ViewableCylinder::SetTopFace( const Vector3& planenormal,
											 double planeCoef )
{
	assert ( (CenterAxis^planenormal) > 0.0 );
	double norm = planenormal.Norm();
	TopNormal = planenormal/norm;
	TopPlaneCoef = planeCoef/norm;
	IsRightCylinderFlag = false;
}

inline void ViewableCylinder::SetBottomFace( const Vector3& planenormal,
											 double planeCoef )
{
	assert ( (CenterAxis^planenormal) < 0.0 );
	double norm = planenormal.Norm();
	BottomNormal = planenormal/norm;
	BottomPlaneCoef = planeCoef/norm;
	IsRightCylinderFlag = false;
}

inline void ViewableCylinder::SetMaterial(const MaterialBase *material)
{
	SetMaterialInner( material );
	SetMaterialOuter( material );
}

inline void ViewableCylinder::SetMaterialInner(const MaterialBase *material)
{
	SetMaterialSideInner( material );
	SetMaterialTopInner( material );
	SetMaterialBottomInner( material );
}

inline void ViewableCylinder::SetMaterialOuter(const MaterialBase *material)
{
	SetMaterialSideOuter( material );
	SetMaterialTopOuter( material );
	SetMaterialBottomOuter( material );
}

inline void ViewableCylinder::SetMaterialSideInner(const MaterialBase *material)
{
	SideInnerMat = material;
}

inline void ViewableCylinder::SetMaterialSideOuter(const MaterialBase *material)
{
	SideOuterMat = material;
}

inline void ViewableCylinder::SetMaterialTopInner(const MaterialBase *material)
{
	TopInnerMat = material;
}

inline void ViewableCylinder::SetMaterialTopOuter(const MaterialBase *material)
{
	TopOuterMat = material;
}

inline void ViewableCylinder::SetMaterialBottomInner(const MaterialBase *material)
{
	BottomInnerMat = material;
}

inline void ViewableCylinder::SetMaterialBottomOuter(const MaterialBase *material)
{
	BottomOuterMat = material;
}

#endif
}