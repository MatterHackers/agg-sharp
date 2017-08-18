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

using NUnit.Framework;
using System;

namespace MatterHackers.VectorMath.Tests
{
	[TestFixture, Category("Agg.VectorMath")]
	public class Vector2Tests
	{
		[Test]
		public void GetDeltaAngleTests()
		{
			// angles around 0
			{
				var center = new Vector2(0, 0);
				var start = new Vector2(1, 0);
				var end = new Vector2(0, 1);
				Assert.IsTrue(Math.Abs(MathHelper.Tau / 4 - center.GetDeltaAngle(start, end)) < .0001);

				start = new Vector2(-1, 0);
				end = new Vector2(0, 1);
				Assert.IsTrue(Math.Abs(-MathHelper.Tau / 4 - center.GetDeltaAngle(start, end)) < .0001);
			}

			// angles around 1,2
			{
				var center = new Vector2(1, 2);
				var start = new Vector2(2, 2);
				var end = new Vector2(1, 3);
				Assert.IsTrue(Math.Abs(MathHelper.Tau / 4 - center.GetDeltaAngle(start, end)) < .0001);

				start = new Vector2(0, 2);
				end = new Vector2(1, 3);
				Assert.IsTrue(Math.Abs(-MathHelper.Tau / 4 - center.GetDeltaAngle(start, end)) < .0001);
			}
		}
	}

	[TestFixture, Category("Agg.VectorMath")]
	public class Vector3Tests
	{
		[Test]
		public void StaticFunctionTests()
		{
			Assert.IsTrue(Vector3.Collinear(new Vector3(0, 0, 1), new Vector3(0, 0, 2), new Vector3(0, 0, 3)));
			Assert.IsTrue(!Vector3.Collinear(new Vector3(0, 0, 1), new Vector3(0, 0, 2), new Vector3(0, 1, 3)));
		}

		[Test]
		public void FrustumExtractionTests()
		{
			{
				Matrix4X4 perspectiveMatrix = Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.Tau / 4, 1, 3, 507);
				Frustum frustum = Frustum.FrustumFromProjectionMatrix(perspectiveMatrix);

				// left
				Assert.IsTrue(frustum.Planes[0].PlaneNormal.Equals(new Vector3(1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[0].DistanceToPlaneFromOrigin, 0, .0001);
				// right
				Assert.IsTrue(frustum.Planes[1].PlaneNormal.Equals(new Vector3(-1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[1].DistanceToPlaneFromOrigin, 0, .0001);
				// bottom
				Assert.IsTrue(frustum.Planes[2].PlaneNormal.Equals(new Vector3(0, 1, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[2].DistanceToPlaneFromOrigin, 0, .0001);
				// top
				Assert.IsTrue(frustum.Planes[3].PlaneNormal.Equals(new Vector3(0, -1, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[3].DistanceToPlaneFromOrigin, 0, .0001);
				// near
				Assert.IsTrue(frustum.Planes[4].PlaneNormal.Equals(new Vector3(0, 0, -1), .0001));
				Assert.AreEqual(frustum.Planes[4].DistanceToPlaneFromOrigin, 3, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].PlaneNormal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceToPlaneFromOrigin, -507, .0001);
			}
		}

		public static void TestFrustumClipLine(Frustum frustum, Vector3 lineStart, Vector3 lineEnd, bool expectInFrustum, Vector3? startClipped, Vector3? endClipped)
		{
			var inFrustum = frustum.ClipLine(ref lineStart, ref lineEnd);
			Assert.AreEqual(expectInFrustum, inFrustum);
			if (expectInFrustum)
			{
				Assert.IsTrue(lineStart.Equals((Vector3)startClipped, .0001));
				Assert.IsTrue(lineEnd.Equals((Vector3)endClipped, .001));
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

		[Test]
		public void FrustumTransformTests()
		{
			{
				Frustum frustum = new Frustum(
					new Plane(new Vector3(1, 0, 0), -20),
					new Plane(new Vector3(-1, 0, 0), -20),
					new Plane(new Vector3(0, 1, 0), -20),
					new Plane(new Vector3(0, -1, 0), -20),
					new Plane(new Vector3(0, 0, 1), -20),
					new Plane(new Vector3(0, 0, -1), -20));

				Vector3Tests.TestFrustumClipLine(frustum, new Vector3(-5, 0, 0), new Vector3(5, 0, 0), true, new Vector3(-5, 0, 0), new Vector3(5, 0, 0));
				Vector3Tests.TestFrustumClipLine(frustum, new Vector3(-50, 0, 0), new Vector3(-21, 0, 0), false, null, null);
				Vector3Tests.TestFrustumClipLine(frustum, new Vector3(-50, 0, 0), new Vector3(-19, 0, 0), true, new Vector3(-20, 0, 0), new Vector3(-19, 0, 0));

				// moved right
				{
					Frustum movedRightFrustum = Frustum.Transform(frustum, Matrix4X4.CreateTranslation(10, 0, 0));
					Assert.IsTrue(movedRightFrustum.Planes[0] == new Plane(new Vector3(1, 0, 0), -10));
					Assert.IsTrue(movedRightFrustum.Planes[1] == new Plane(new Vector3(-1, 0, 0), -30));
					Assert.IsTrue(movedRightFrustum.Planes[2] == frustum.Planes[2]);
					Assert.IsTrue(movedRightFrustum.Planes[3] == frustum.Planes[3]);
					Assert.IsTrue(movedRightFrustum.Planes[4] == frustum.Planes[4]);
					Assert.IsTrue(movedRightFrustum.Planes[5] == frustum.Planes[5]);

					Vector3Tests.TestFrustumClipLine(movedRightFrustum, new Vector3(-5, 0, 0), new Vector3(5, 0, 0), true, new Vector3(-5, 0, 0), new Vector3(5, 0, 0));
					Vector3Tests.TestFrustumClipLine(movedRightFrustum, new Vector3(-50, 0, 0), new Vector3(-11, 0, 0), false, null, null);
					Vector3Tests.TestFrustumClipLine(movedRightFrustum, new Vector3(-50, 0, 0), new Vector3(-9, 0, 0), true, new Vector3(-10, 0, 0), new Vector3(-9, 0, 0));
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

			// rotate about y 180 degrees
			{
				Matrix4X4 perspectiveMatrix = Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.Tau / 4, 1, 3, 507);
				Frustum perspectiveFrustum = Frustum.FrustumFromProjectionMatrix(perspectiveMatrix);

				Vector3Tests.TestFrustumClipLine(perspectiveFrustum, new Vector3(-10, 0, -5), new Vector3(0, 0, -5), true, new Vector3(-5, 0, -5), new Vector3(0, 0, -5));
				Vector3Tests.TestFrustumClipLine(perspectiveFrustum, new Vector3(-50, 0, -5), new Vector3(-21, 0, -5), false, null, null);
				Vector3Tests.TestFrustumClipLine(perspectiveFrustum, new Vector3(-50, 0, -20), new Vector3(-19, 0, -20), true, new Vector3(-20, 0, -20), new Vector3(-19, 0, -20));

				var frustum = Frustum.Transform(perspectiveFrustum, Matrix4X4.CreateRotationY(MathHelper.Tau / 2));

				// left
				Assert.IsTrue(frustum.Planes[0].PlaneNormal.Equals(new Vector3(-1, 0, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[0].DistanceToPlaneFromOrigin, 0, .0001);
				// right
				Assert.IsTrue(frustum.Planes[1].PlaneNormal.Equals(new Vector3(1, 0, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[1].DistanceToPlaneFromOrigin, 0, .0001);
				// bottom
				Assert.IsTrue(frustum.Planes[2].PlaneNormal.Equals(new Vector3(0, 1, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[2].DistanceToPlaneFromOrigin, 0, .0001);
				// top
				Assert.IsTrue(frustum.Planes[3].PlaneNormal.Equals(new Vector3(0, -1, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[3].DistanceToPlaneFromOrigin, 0, .0001);
				// near
				Assert.IsTrue(frustum.Planes[4].PlaneNormal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[4].DistanceToPlaneFromOrigin, 3, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].PlaneNormal.Equals(new Vector3(0, 0, -1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceToPlaneFromOrigin, -507, .0001);
			}

			// translate 10 down z
			{
				double zMove = 10;
				Matrix4X4 perspectiveMatrix = Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.Tau / 4, 1, 3, 507);
				Frustum frustum = Frustum.FrustumFromProjectionMatrix(perspectiveMatrix);
				frustum = Frustum.Transform(frustum, Matrix4X4.CreateTranslation(0, 0, -10));

				double expectedPlaneOffset = Math.Sqrt(2 * (zMove / 2) * (zMove / 2));
				// left
				Assert.IsTrue(frustum.Planes[0].PlaneNormal.Equals(new Vector3(1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[0].DistanceToPlaneFromOrigin, .0001);
				// right
				Assert.IsTrue(frustum.Planes[1].PlaneNormal.Equals(new Vector3(-1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[1].DistanceToPlaneFromOrigin, .0001);
				// bottom
				Assert.IsTrue(frustum.Planes[2].PlaneNormal.Equals(new Vector3(0, 1, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[2].DistanceToPlaneFromOrigin, .0001);
				// top
				Assert.IsTrue(frustum.Planes[3].PlaneNormal.Equals(new Vector3(0, -1, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[3].DistanceToPlaneFromOrigin, .0001);
				// near
				Assert.IsTrue(frustum.Planes[4].PlaneNormal.Equals(new Vector3(0, 0, -1), .0001));
				Assert.AreEqual(frustum.Planes[4].DistanceToPlaneFromOrigin, 3 + zMove, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].PlaneNormal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceToPlaneFromOrigin, -507 - zMove, .0001);
			}
		}

		[Test]
		public void PlaneClipLineTests()
		{
			{
				Plane testPlane = new Plane(Vector3.UnitZ, 5);
				Vector3 startPoint = new Vector3(0, 0, -2);
				Vector3 endPoint = new Vector3(0, 0, 8);
				bool exists = testPlane.ClipLine(ref startPoint, ref endPoint);
				Assert.IsTrue(exists);
				Assert.IsTrue(startPoint.z == 5);
				Assert.IsTrue(endPoint.z == 8);
			}

			{
				Plane testPlane = new Plane(Vector3.UnitZ, 5);
				Vector3 startPoint = new Vector3(0, 0, 8);
				Vector3 endPoint = new Vector3(0, 0, -2);
				bool exists = testPlane.ClipLine(ref startPoint, ref endPoint);
				Assert.IsTrue(exists);
				Assert.IsTrue(startPoint.z == 8);
				Assert.IsTrue(endPoint.z == 5);
			}

			{
				Plane testPlane = new Plane(Vector3.UnitZ, 5);
				Vector3 startPoint = new Vector3(0, 0, 4);
				Vector3 endPoint = new Vector3(0, 0, -2);
				bool exists = testPlane.ClipLine(ref startPoint, ref endPoint);
				Assert.IsFalse(exists);
			}

			{
				Plane testPlane = new Plane(Vector3.UnitZ, 5);
				Vector3 startPoint = new Vector3(0, 0, 6);
				Vector3 endPoint = new Vector3(0, 0, 12);
				bool exists = testPlane.ClipLine(ref startPoint, ref endPoint);
				Assert.IsTrue(exists);
				Assert.IsTrue(startPoint.z == 6);
				Assert.IsTrue(endPoint.z == 12);
			}
		}

		[Test]
		public void TestGetHashCode()
		{
			{
				Vector2 a = new Vector2(10, 11);
				Vector2 b = new Vector2(10, 11);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				Vector3 a = new Vector3(10, 11, 12);
				Vector3 b = new Vector3(10, 11, 12);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				Vector4 a = new Vector4(10, 11, 12, 13);
				Vector4 b = new Vector4(10, 11, 12, 13);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				Quaternion a = new Quaternion(10, 11, 12, 13);
				Quaternion b = new Quaternion(10, 11, 12, 13);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				Matrix4X4 a = Matrix4X4.CreateRotationX(3);
				Matrix4X4 b = Matrix4X4.CreateRotationX(3);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
		}
	}
}