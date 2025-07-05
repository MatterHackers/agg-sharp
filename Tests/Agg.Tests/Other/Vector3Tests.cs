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

namespace MatterHackers.Agg.Tests
{
    
    public class Vector3Tests
	{
		[Test]
		public async Task VectorAdditionAndSubtraction()
		{
			var point1 = default(Vector3);
			point1 = new Vector3(1, 1, 1);

			var point2 = default(Vector3);
			point2 = new Vector3(2, 2, 2);

			var point3 = default(Vector3);
			point3 = Vector3.Add(point1, point2);
			await Assert.That(point3 == new Vector3(3, 3, 3)).IsTrue();

			point3 = point1 - point2;
			await Assert.That(point3 == new Vector3(-1, -1, -1)).IsTrue();

			point3 += point1;
			await Assert.That(point3 == new Vector3(0, 0, 0)).IsTrue();

			point3 += point2;
			await Assert.That(point3 == new Vector3(2, 2, 2)).IsTrue();

			point3 = new Vector3(3, -4, 5);
			await Assert.That(point3.Length > 7.07 && point3.Length < 7.08).IsTrue();

			var inlineOpLeftSide = new Vector3(5.0f, -3.0f, .0f);
			var inlineOpRightSide = new Vector3(-5.0f, 4.0f, 1.0f);
			await Assert.That(inlineOpLeftSide + inlineOpRightSide == new Vector3(.0f, 1.0f, 1.0f)).IsTrue();

			await Assert.That(inlineOpLeftSide - inlineOpRightSide == new Vector3(10.0f, -7.0f, -1.0f)).IsTrue();
		}

		[Test]
		public async Task ScalarMultiplication()
		{
			var scalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			await Assert.That(scalarMultiplicationArgument * -.5 == -new Vector3(2.5f, 2.0f, 1.5f)).IsTrue();
			await Assert.That(-.5 * scalarMultiplicationArgument == -new Vector3(2.5f, 2.0f, 1.5f)).IsTrue();
			await Assert.That(5 * scalarMultiplicationArgument == new Vector3(25.0f, 20.0f, 15.0f)).IsTrue();

			var point3 = new Vector3(2, 3, 4);
			point3 *= 6;
			await Assert.That(point3.Equals(new Vector3(12, 18, 24), .01f)).IsTrue();
		}

		[Test]
		public async Task ScalarDivision()
		{
			var scalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			await Assert.That(scalarMultiplicationArgument / 2 == new Vector3(2.5f, 2.0f, 1.5f)).IsTrue();

			var point3 = new Vector3(12, 18, 24);
			point3 /= 6;
			await Assert.That(point3.Equals(new Vector3(2, 3, 4), .01f)).IsTrue();
		}

		[Test]
		public async Task DotProduct()
		{
			var test1 = new Vector3(10, 1, 2);
			var test2 = new Vector3(1, 0, 0);
			double dotResult = Vector3Ex.Dot(test2, test1);
			await Assert.That(dotResult == 10).IsTrue();
		}

		[Test]
		public async Task CrossProduct()
		{
			var test1 = new Vector3(10, 0, 0);
			var test2 = new Vector3(1, 1, 0);
			Vector3 crossResult = Vector3Ex.Cross(test2, test1);
			await Assert.That(crossResult.X == 0).IsTrue();
			await Assert.That(crossResult.Y == 0).IsTrue();
			await Assert.That(crossResult.Z < 0).IsTrue();
		}

		[Test]
		public async Task Normalize()
		{
			var point3 = new Vector3(3, -4, 5);
			point3.Normalize();
			await Assert.That(point3.Length > 0.99 && point3.Length < 1.01).IsTrue();
		}
	}
}
