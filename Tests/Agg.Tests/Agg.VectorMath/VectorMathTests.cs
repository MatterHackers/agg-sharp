﻿/*
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

using Newtonsoft.Json;
using NUnit.Framework;
using System;

namespace MatterHackers.VectorMath.Tests
{
	[TestFixture, Category("Agg.VectorMath")]
	public class EasingTests
	{
		[Test]
		public void StaticFunctionTests()
		{
			void InverseWorking(Easing.EaseType easeType, Easing.EaseOption easeOption, double k)
			{
				var output = Easing.Calculate(easeType, easeOption, k);

				var input = Easing.CalculateInverse(easeType, easeOption, output);

				Assert.AreEqual(k, input, .001);
			}

			void RangeTest(Easing.EaseType easeType)
			{
				for (double i = 0; i <= 1; i += .05)
				{
					InverseWorking(easeType, Easing.EaseOption.In, i);
					InverseWorking(easeType, Easing.EaseOption.InOut, i);
					InverseWorking(easeType, Easing.EaseOption.Out, i);
				}
			}

			RangeTest(Easing.EaseType.Linear);
			RangeTest(Easing.EaseType.Cubic);
			RangeTest(Easing.EaseType.Quadratic);
		}
	}

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

		[Test]
		public void DistanceToLineTests()
		{
			// outside the line
			Assert.AreEqual(5, Vector2.DistancePointToLine(new Vector2(0, 0), new Vector2(5, 0), new Vector2(10, 0)));
			Assert.AreEqual(5, Vector2.DistancePointToLine(new Vector2(15, 0), new Vector2(5, 0), new Vector2(10, 0)));
			Assert.AreEqual(Math.Sqrt(2), Vector2.DistancePointToLine(new Vector2(-1, -1), new Vector2(0, 0), new Vector2(10, 0)), .0001);
			Assert.AreEqual(Math.Sqrt(2), Vector2.DistancePointToLine(new Vector2(-1, 1), new Vector2(0, 0), new Vector2(10, 0)), .0001);
			Assert.AreEqual(Math.Sqrt(2), Vector2.DistancePointToLine(new Vector2(11, -1), new Vector2(0, 0), new Vector2(10, 0)), .0001);
			Assert.AreEqual(Math.Sqrt(2), Vector2.DistancePointToLine(new Vector2(11, 1), new Vector2(0, 0), new Vector2(10, 0)), .0001);
			// inside the line
			Assert.AreEqual(5, Vector2.DistancePointToLine(new Vector2(7, 5), new Vector2(5, 0), new Vector2(10, 0)));
			Assert.AreEqual(5, Vector2.DistancePointToLine(new Vector2(7, -5), new Vector2(5, 0), new Vector2(10, 0)));
			// line is a point
			Assert.AreEqual(5, Vector2.DistancePointToLine(new Vector2(0, 0), new Vector2(5, 0), new Vector2(5, 0)));
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
		public void Vector3ParseTest()
		{
			// Two segments
			Assert.AreEqual(
				new Vector3(1, 2, 0),
				Vector3.Parse("1, 2"));

			// Two segments, trailing comma
			Assert.AreEqual(
				new Vector3(1, 2, 0),
				Vector3.Parse("1, 2,"));

			// Three segments with whitespace
			Assert.AreEqual(
				new Vector3(1, 2, 3),
				Vector3.Parse("1, 2, 3"));

			// Three segments without whitespace
			Assert.AreEqual(
				new Vector3(1, 2, 3),
				Vector3.Parse("1,2,3"));

			// Doubles
			Assert.AreEqual(
				new Vector3(1.1, 2.2, 3.3),
				Vector3.Parse("1.1, 2.2, 3.3"));

			// Negative values
			Assert.AreEqual(
				new Vector3(-1, -2, -3),
				Vector3.Parse("-1, -2, -3"));

			// Empty
			Assert.AreEqual(
				Vector3.Zero,
				Vector3.Parse(""));
		}

		[Test]
		public void Vector2SerializeTest()
		{
			Assert.AreEqual(
				"{\"X\":1.0,\"Y\":2.0}",
				JsonConvert.SerializeObject(new Vector2(1, 2)),
				"Unexpected properties serialized into json output");
		}

		[Test]
		public void Vector2FloatSerializeTest()
		{
			Assert.AreEqual(
				"{\"X\":1.1,\"Y\":2.2}",
				JsonConvert.SerializeObject(new Vector2Float(1.1, 2.2)),
				"Unexpected properties serialized into json output");
		}

		[Test]
		public void Vector3FloatSerializeTest()
		{
			Assert.AreEqual(
				"{\"X\":1.1,\"Y\":2.2,\"Z\":3.3}",
				JsonConvert.SerializeObject(new Vector3Float(1.1, 2.2, 3.3)),
				"Unexpected properties serialized into json output");
		}

		[Test]
		public void Vector3SerializeTest()
		{
			Assert.AreEqual(
				"{\"X\":1.0,\"Y\":2.0,\"Z\":3.0}",
				JsonConvert.SerializeObject(new Vector3(1, 2, 3)),
				"Unexpected properties serialized into json output");
		}

		[Test]
		public void WorldViewPerspectiveProjectionTests()
		{
			var world = new WorldView(1, 1);
			Assert.IsTrue(world.EyePosition.Equals(Vector3.UnitZ * 7, 1e-3));
			world.CalculatePerspectiveMatrixOffCenter(567, 123, -200, 5, 55);
			Assert.IsTrue(world.GetScreenPosition(new Vector3(0, 0, 0)).Equals(new Vector2((567 - 200) / 2.0, 123 / 2.0), 1e-3));
			Assert.AreEqual(5, world.NearZ, 1e-3);
			Assert.AreEqual(55, world.FarZ, 1e-3);
			Assert.AreEqual(4.14213562373095, world.NearPlaneHeightInViewspace, 1e-3);
			var ray = world.GetRayForLocalBounds(new Vector2((567 - 200) / 2.0, 123)); // top center
			Assert.AreEqual(WorldView.DefaultPerspectiveVFOVDegrees / 2, MathHelper.RadiansToDegrees(Math.Atan2(ray.directionNormal.Y, -ray.directionNormal.Z)), 1e-3);
			Assert.IsTrue((Vector3.UnitZ * 7).Equals(ray.origin, 1e-3));
			Assert.AreEqual(world.NearPlaneHeightInViewspace * 2, world.GetViewspaceHeightAtPosition(new Vector3(1, 1, -10)), 1e-3);
			world.Scale = 3;
			Assert.AreEqual(world.NearPlaneHeightInViewspace * 2 / 3, world.GetWorldUnitsPerScreenPixelAtPosition(new Vector3(1, 1, (7 - 10) / 3.0)) * 123, 1e-3);
		}

		[Test]
		public void WorldViewOrthographicProjectionTests()
		{
			var world = new WorldView(1, 1);
			Assert.IsTrue(world.EyePosition.Equals(Vector3.UnitZ * 7, 1e-3));
			world.CalculateOrthogrphicMatrixOffCenterWithViewspaceHeight(680, 240, -200, 128, 5, 55);
			Assert.IsTrue(world.GetScreenPosition(new Vector3(0, 0, 0)).Equals(new Vector2((680 - 200) / 2.0, 240 / 2.0), 1e-3));
			Assert.IsTrue(world.GetScreenPosition(new Vector3(128, 64, 0)).Equals(new Vector2(680 - 200, 240), 1e-3));
			Assert.IsTrue(world.GetScreenPosition(new Vector3(-128, -64, 0)).Equals(new Vector2(0, 0), 1e-3));
			Assert.AreEqual(5, world.NearZ, 1e-3);
			Assert.AreEqual(55, world.FarZ, 1e-3);
			Assert.AreEqual(128, world.NearPlaneHeightInViewspace, 1e-3);
			var ray = world.GetRayForLocalBounds(new Vector2((680 - 200) / 2.0, 240)); // top center
			Assert.IsTrue(Vector3.UnitZ.Equals(-ray.directionNormal.GetNormal(), 1e-3));
			Assert.IsTrue(new Vector3(0, 64, 2).Equals(ray.origin, 1e-3));
			Assert.AreEqual(world.NearPlaneHeightInViewspace, world.GetViewspaceHeightAtPosition(new Vector3(1, 1, -10)), 1e-3);
			world.Scale = 3;
			Assert.AreEqual(world.NearPlaneHeightInViewspace / 3, world.GetWorldUnitsPerScreenPixelAtPosition(new Vector3(1, 1, (7 - 10) / 3.0)) * 240, 1e-3);
		}

		[Test]
		public void WorldViewEyePositionTests()
		{
			var world = new WorldView(1, 1);
			world.EyePosition = new Vector3(1, 2, 3);
			Assert.IsTrue(new Vector3(1, 2, 3).Equals(world.EyePosition, 1e-3));
		}

		[Test]
		public void FrustumExtractionTests()
		{
			{
				Matrix4X4 perspectiveMatrix = Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.Tau / 4, 1, 3, 507);
				Frustum frustum = Frustum.FrustumFromProjectionMatrix(perspectiveMatrix);

				// left
				Assert.IsTrue(frustum.Planes[0].Normal.Equals(new Vector3(1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[0].DistanceFromOrigin, 0, .0001);
				// right
				Assert.IsTrue(frustum.Planes[1].Normal.Equals(new Vector3(-1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[1].DistanceFromOrigin, 0, .0001);
				// bottom
				Assert.IsTrue(frustum.Planes[2].Normal.Equals(new Vector3(0, 1, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[2].DistanceFromOrigin, 0, .0001);
				// top
				Assert.IsTrue(frustum.Planes[3].Normal.Equals(new Vector3(0, -1, -1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[3].DistanceFromOrigin, 0, .0001);
				// near
				Assert.IsTrue(frustum.Planes[4].Normal.Equals(new Vector3(0, 0, -1), .0001));
				Assert.AreEqual(frustum.Planes[4].DistanceFromOrigin, 3, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].Normal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceFromOrigin, -507, .0001);
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
					Plane control = new Plane(Vector3Ex.TransformNormal(frustum.Planes[0].Normal, testMatrix), frustum.Planes[0].DistanceFromOrigin);
					Assert.IsTrue(movedRightFrustum.Planes[0].Equals(control, .001, .01));
					Assert.IsTrue(movedRightFrustum.Planes[1].Equals(new Plane(Vector3Ex.TransformNormal(frustum.Planes[1].Normal, testMatrix), frustum.Planes[1].DistanceFromOrigin)));
					Assert.IsTrue(movedRightFrustum.Planes[2].Equals(frustum.Planes[2]));
					Assert.IsTrue(movedRightFrustum.Planes[3].Equals(frustum.Planes[3]));
					Assert.IsTrue(movedRightFrustum.Planes[4].Equals(new Plane(Vector3Ex.TransformNormal(frustum.Planes[4].Normal, testMatrix), frustum.Planes[4].DistanceFromOrigin)));
					Assert.IsTrue(movedRightFrustum.Planes[5].Equals(new Plane(Vector3Ex.TransformNormal(frustum.Planes[5].Normal, testMatrix), frustum.Planes[5].DistanceFromOrigin)));
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
				Assert.IsTrue(frustum.Planes[0].Normal.Equals(new Vector3(-1, 0, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[0].DistanceFromOrigin, 0, .0001);
				// right
				Assert.IsTrue(frustum.Planes[1].Normal.Equals(new Vector3(1, 0, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[1].DistanceFromOrigin, 0, .0001);
				// bottom
				Assert.IsTrue(frustum.Planes[2].Normal.Equals(new Vector3(0, 1, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[2].DistanceFromOrigin, 0, .0001);
				// top
				Assert.IsTrue(frustum.Planes[3].Normal.Equals(new Vector3(0, -1, 1).GetNormal(), .0001));
				Assert.AreEqual(frustum.Planes[3].DistanceFromOrigin, 0, .0001);
				// near
				Assert.IsTrue(frustum.Planes[4].Normal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[4].DistanceFromOrigin, 3, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].Normal.Equals(new Vector3(0, 0, -1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceFromOrigin, -507, .0001);
			}

			// translate 10 down z
			{
				double zMove = 10;
				Matrix4X4 perspectiveMatrix = Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.Tau / 4, 1, 3, 507);
				Frustum frustum = Frustum.FrustumFromProjectionMatrix(perspectiveMatrix);
				frustum = Frustum.Transform(frustum, Matrix4X4.CreateTranslation(0, 0, -10));

				double expectedPlaneOffset = Math.Sqrt(2 * (zMove / 2) * (zMove / 2));
				// left
				Assert.IsTrue(frustum.Planes[0].Normal.Equals(new Vector3(1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[0].DistanceFromOrigin, .0001);
				// right
				Assert.IsTrue(frustum.Planes[1].Normal.Equals(new Vector3(-1, 0, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[1].DistanceFromOrigin, .0001);
				// bottom
				Assert.IsTrue(frustum.Planes[2].Normal.Equals(new Vector3(0, 1, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[2].DistanceFromOrigin, .0001);
				// top
				Assert.IsTrue(frustum.Planes[3].Normal.Equals(new Vector3(0, -1, -1).GetNormal(), .0001));
				Assert.AreEqual(expectedPlaneOffset, frustum.Planes[3].DistanceFromOrigin, .0001);
				// near
				Assert.IsTrue(frustum.Planes[4].Normal.Equals(new Vector3(0, 0, -1), .0001));
				Assert.AreEqual(frustum.Planes[4].DistanceFromOrigin, 3 + zMove, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].Normal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceFromOrigin, -507 - zMove, .0001);
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
				Assert.IsTrue(startPoint.Z == 5);
				Assert.IsTrue(endPoint.Z == 8);
			}

			{
				Plane testPlane = new Plane(Vector3.UnitZ, 5);
				Vector3 startPoint = new Vector3(0, 0, 8);
				Vector3 endPoint = new Vector3(0, 0, -2);
				bool exists = testPlane.ClipLine(ref startPoint, ref endPoint);
				Assert.IsTrue(exists);
				Assert.IsTrue(startPoint.Z == 8);
				Assert.IsTrue(endPoint.Z == 5);
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
				Assert.IsTrue(startPoint.Z == 6);
				Assert.IsTrue(endPoint.Z == 12);
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