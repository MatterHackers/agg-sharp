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

using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.RayTracer
{
    [TestFixture, Category("WorkInProgress")]
	public class FrustumTests
	{
		[Test]
		public void RayBundleSameResultAsIndividualRays()
		{
		}

		[Test]
		public void FrustumTransformTests()
		{
			Frustum frustum = new Frustum(
				new Plane(new Vector3(1, 0, 0), 20),
				new Plane(new Vector3(-1, 0, 0), 20),
				new Plane(new Vector3(0, 1, 0), 20),
				new Plane(new Vector3(0, -1, 0), 20),
				new Plane(new Vector3(0, 0, 1), 20),
				new Plane(new Vector3(0, 0, -1), 20));

			// moved right
			{
				Frustum movedRightFrustum = Frustum.Transform(frustum, Matrix4X4.CreateTranslation(10, 0, 0));
				Assert.IsTrue(movedRightFrustum.Planes[0] == new Plane(new Vector3(1, 0, 0), 30));
				Assert.IsTrue(movedRightFrustum.Planes[1] == new Plane(new Vector3(-1, 0, 0), 10));
				Assert.IsTrue(movedRightFrustum.Planes[2] == frustum.Planes[2]);
				Assert.IsTrue(movedRightFrustum.Planes[3] == frustum.Planes[3]);
				Assert.IsTrue(movedRightFrustum.Planes[4] == frustum.Planes[4]);
				Assert.IsTrue(movedRightFrustum.Planes[5] == frustum.Planes[5]);
			}

			// rotated right
			{
				Frustum movedRightFrustum = Frustum.Transform(frustum, Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(45)));
				Matrix4X4 testMatrix = Matrix4X4.CreateRotationY(MathHelper.DegreesToRadians(45));
				Plane control = new Plane(Vector3.TransformNormal(frustum.Planes[0].PlaneNormal, testMatrix), frustum.Planes[0].DistanceToPlaneFromOrigin);
				Assert.IsTrue(movedRightFrustum.Planes[0].Equals(control, .001, .01));
				Assert.IsTrue(movedRightFrustum.Planes[1].Equals(new Plane(Vector3.TransformNormal(frustum.Planes[1].PlaneNormal, testMatrix), frustum.Planes[1].DistanceToPlaneFromOrigin)));
				Assert.IsTrue(movedRightFrustum.Planes[2].Equals(frustum.Planes[2]));
				Assert.IsTrue(movedRightFrustum.Planes[3].Equals(frustum.Planes[3]));
				Assert.IsTrue(movedRightFrustum.Planes[4].Equals(new Plane(Vector3.TransformNormal(frustum.Planes[4].PlaneNormal, testMatrix), frustum.Planes[4].DistanceToPlaneFromOrigin)));
				Assert.IsTrue(movedRightFrustum.Planes[5].Equals(new Plane(Vector3.TransformNormal(frustum.Planes[5].PlaneNormal, testMatrix), frustum.Planes[5].DistanceToPlaneFromOrigin)));
			}
		}

		[Test]
		public void FrustumIntersetAABBTests()
		{
			{
				Frustum frustum = new Frustum(
					new Plane(new Vector3(1, 0, 0), 20),
					new Plane(new Vector3(-1, 0, 0), 20),
					new Plane(new Vector3(0, 1, 0), 20),
					new Plane(new Vector3(0, -1, 0), 20),
					new Plane(new Vector3(0, 0, 1), 20),
					new Plane(new Vector3(0, 0, -1), 20));

				// outside to left
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-30, -10, -10), new Vector3(-25, 10, 10));
					FrustumIntersection intersection = frustum.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Outside);
				}

				// intersect
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-25, 0, -10), new Vector3(-15, 10, 10));
					FrustumIntersection intersection = frustum.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Intersect);
				}

				// not intersect
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-25, 0, 30), new Vector3(-15, 10, 35));
					FrustumIntersection intersection = frustum.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Outside);
				}

				// inside
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-5, -5, -5), new Vector3(5, 5, 5));
					FrustumIntersection intersection = frustum.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Inside);
				}
			}

			{
				Frustum frustum = new Frustum(
					new Plane(new Vector3(-1, -1, 0), 0),
					new Plane(new Vector3(1, -1, 0), 0),
					new Plane(new Vector3(0, -1, -1), 0),
					new Plane(new Vector3(0, -1, 1), 0),
					new Plane(new Vector3(0, -1, 0), 0),
					new Plane(new Vector3(0, 1, 0), 10000));

				// outside to left
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-110, 0, -10), new Vector3(-100, 10, 10));
					FrustumIntersection intersection = frustum.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Outside);
				}

				// intersect with origin (front)
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10));
					FrustumIntersection intersection = frustum.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Intersect);
				}

				// inside
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-5, 100, -5), new Vector3(5, 110, 5));
					FrustumIntersection intersection = frustum.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Inside);
				}
			}

			{
				// looking down -z
				Frustum frustum5PlaneNegZ = new Frustum(
					new Vector3(-1, 0, 1),
					new Vector3(-1, 0, 1),
					new Vector3(0, 1, 1),
					new Vector3(0, -1, 1),
					new Vector3(0, 0, -1), 10000);

				// outside to left
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-110, 0, -10), new Vector3(-100, 10, 10));
					FrustumIntersection intersection = frustum5PlaneNegZ.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Outside);
				}

				// intersect with origin (front)
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-10, -10, -10), new Vector3(10, 10, 10));
					FrustumIntersection intersection = frustum5PlaneNegZ.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Intersect);
				}

				// inside
				{
					AxisAlignedBoundingBox aabb = new AxisAlignedBoundingBox(new Vector3(-5, -5, -110), new Vector3(5, 5, -100));
					FrustumIntersection intersection = frustum5PlaneNegZ.GetIntersect(aabb);
					Assert.IsTrue(intersection == FrustumIntersection.Inside);
				}
			}
		}
	}
}