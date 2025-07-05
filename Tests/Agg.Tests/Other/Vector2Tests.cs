/*
Copyright (c) 2025, Lars Brubaker
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

using Agg.Tests.Agg;
using TUnit.Assertions;
using TUnit.Core;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.Tests
{
    public class Vector2Tests
	{
		[Test]
		public async Task ArithmaticOperations()
		{
			var point1 = new Vector2(1, 1);
			var point2 = new Vector2(2, 2);

			Vector2 point3 = point1 + point2;
			await Assert.That(point3 == new Vector2(3, 3)).IsTrue();

			point3 = point1 - point2;
			await Assert.That(point3 == new Vector2(-1, -1)).IsTrue();

			point3 += point1;
			await Assert.That(point3 == new Vector2(0, 0)).IsTrue();

			point3 += point2;
			await Assert.That(point3 == new Vector2(2, 2)).IsTrue();

			point3 *= 6;
			await Assert.That(point3 == new Vector2(12, 12)).IsTrue();

			var inlineOpLeftSide = new Vector2(5, -3);
			var inlineOpRightSide = new Vector2(-5, 4);
			await Assert.That(inlineOpLeftSide + inlineOpRightSide == new Vector2(.0f, 1)).IsTrue();

			await Assert.That(inlineOpLeftSide - inlineOpRightSide == new Vector2(10.0f, -7)).IsTrue();
		}

		[Test]
		public async Task GetLengthAndNormalize()
		{
			var point3 = new Vector2(3, -4);
			await Assert.That(point3.Length > 4.999f && point3.Length < 5.001f).IsTrue();

			point3.Normalize();
			await Assert.That(point3.Length > 0.99f && point3.Length < 1.01f).IsTrue();
		}

		[Test]
		public async Task GetPositionAtTests()
		{
			var line1 = new List<Vector2>()
			{
				new Vector2(10, 3),
				new Vector2(20, 3),
				new Vector2(20, 13),
				new Vector2(10, 13)
			};

			await Assert.That(line1.PolygonLength(false)).IsEqualTo(30);

			// open segments should also give correct values
			await Assert.That(line1.GetPositionAt(3, false)).IsEqualTo(new Vector2(13, 3));
			await Assert.That(line1.GetPositionAt(33, false)).IsEqualTo(new Vector2(10, 13));
            await Assert.That(line1.GetPositionAt(33 + 22 * 10, false)).IsEqualTo(new Vector2(10, 13));
            await Assert.That(line1.GetPositionAt(-2, false)).IsEqualTo(new Vector2(10, 3));
            await Assert.That(line1.GetPositionAt(-2 + -23 * 10, false)).IsEqualTo(new Vector2(10, 3));

            await Assert.That(line1.PolygonLength(true)).IsEqualTo(40);

			// closed loops should wrap correctly
			var error = .000001;
			await Assert.That(line1.GetPositionAt(3)).IsEqualTo(new Vector2(13, 3));
			await Assert.That(new Vector2(13, 3).Equals(line1.GetPositionAt(43), error)).IsTrue();
			await Assert.That(new Vector2(13, 3).Equals(line1.GetPositionAt(43 + 22 * 40), error)).IsTrue();
			await Assert.That(new Vector2(10, 5).Equals(line1.GetPositionAt(-2), error)).IsTrue();
			await Assert.That(new Vector2(10, 5).Equals(line1.GetPositionAt(-2 + 23 * 40), error)).IsTrue();
		}

		[Test]
		public async Task ScalerOperations()
		{
			var scalarMultiplicationArgument = new Vector2(5.0f, 4.0f);
			await Assert.That(scalarMultiplicationArgument * -.5 == new Vector2(-2.5f, -2)).IsTrue();
			await Assert.That(scalarMultiplicationArgument / 2 == new Vector2(2.5, 2)).IsTrue();
			await Assert.That(2 / scalarMultiplicationArgument == new Vector2(.4, .5)).IsTrue();
			await Assert.That(5 * scalarMultiplicationArgument == new Vector2(25, 20)).IsTrue();
		}

		[Test]
		public async Task CrossProduct()
		{
			var rand = new Random();
			var testVector2D1 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			var testVector2D2 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			double cross2D = Vector2.Cross(testVector2D1, testVector2D2);

			var testVector31 = new Vector3(testVector2D1.X, testVector2D1.Y, 0);
			var testVector32 = new Vector3(testVector2D2.X, testVector2D2.Y, 0);
			Vector3 cross3D = Vector3Ex.Cross(testVector31, testVector32);

			await Assert.That(cross3D.Z == cross2D).IsTrue();
		}

		[Test]
		public async Task DotProduct()
		{
			var rand = new Random();
			var testVector2D1 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			var testVector2D2 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			double cross2D = Vector2.Dot(testVector2D1, testVector2D2);

			var testVector31 = new Vector3(testVector2D1.X, testVector2D1.Y, 0);
			var testVector32 = new Vector3(testVector2D2.X, testVector2D2.Y, 0);
			double cross3D = Vector3Ex.Dot(testVector31, testVector32);

			await Assert.That(cross3D == cross2D).IsTrue();
		}

		[Test]
		public async Task LengthAndDistance()
		{
			var rand = new Random();
			var test1 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			var test2 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			Vector2 test3 = test1 + test2;
			double distance1 = test2.Length;
			double distance2 = (test1 - test3).Length;

			await Assert.That(distance1 < distance2 + .001f && distance1 > distance2 - .001f).IsTrue();
		}
	}
}
