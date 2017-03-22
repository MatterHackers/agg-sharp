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
				Assert.AreEqual(frustum.Planes[4].DistanceToPlaneFromOrigin, -3, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].PlaneNormal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceToPlaneFromOrigin, 507, .0001);
			}
		}

		[Test]
		public void FrustumTransformTests()
		{
			// rotate about y 180 degrees
			{
				Matrix4X4 perspectiveMatrix = Matrix4X4.CreatePerspectiveFieldOfView(MathHelper.Tau / 4, 1, 3, 507);
				Frustum frustum = Frustum.FrustumFromProjectionMatrix(perspectiveMatrix);
				frustum = Frustum.Transform(frustum, Matrix4X4.CreateRotationY(MathHelper.Tau / 2));

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
				Assert.AreEqual(frustum.Planes[4].DistanceToPlaneFromOrigin, -3, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].PlaneNormal.Equals(new Vector3(0, 0, -1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceToPlaneFromOrigin, 507, .0001);
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
				Assert.AreEqual(frustum.Planes[4].DistanceToPlaneFromOrigin, -3 + zMove, .0001);
				// far
				Assert.IsTrue(frustum.Planes[5].PlaneNormal.Equals(new Vector3(0, 0, 1), .0001));
				Assert.AreEqual(frustum.Planes[5].DistanceToPlaneFromOrigin, 507 - zMove, .0001);
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