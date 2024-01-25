/*
Copyright (c) 2023, Lars Brubaker
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
using Xunit;

namespace MatterHackers.Agg.Tests
{
	//[TestFixture]
	public class Vector3Tests
	{
		[Fact]
		public void VectorAdditionAndSubtraction()
		{
			var point1 = default(Vector3);
			point1 = new Vector3(1, 1, 1);

			var point2 = default(Vector3);
			point2 = new Vector3(2, 2, 2);

			var point3 = default(Vector3);
			point3 = Vector3.Add(point1, point2);
			Assert.True(point3 == new Vector3(3, 3, 3));

			point3 = point1 - point2;
			Assert.True(point3 == new Vector3(-1, -1, -1));

			point3 += point1;
			Assert.True(point3 == new Vector3(0, 0, 0));

			point3 += point2;
			Assert.True(point3 == new Vector3(2, 2, 2));

			point3 = new Vector3(3, -4, 5);
			Assert.True(point3.Length > 7.07 && point3.Length < 7.08);

			var inlineOpLeftSide = new Vector3(5.0f, -3.0f, .0f);
			var inlineOpRightSide = new Vector3(-5.0f, 4.0f, 1.0f);
			Assert.True(inlineOpLeftSide + inlineOpRightSide == new Vector3(.0f, 1.0f, 1.0f));

			Assert.True(inlineOpLeftSide - inlineOpRightSide == new Vector3(10.0f, -7.0f, -1.0f));
		}

		[Fact]
		public void ScalarMultiplication()
		{
			var scalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			Assert.True(scalarMultiplicationArgument * -.5 == -new Vector3(2.5f, 2.0f, 1.5f));
			Assert.True(-.5 * scalarMultiplicationArgument == -new Vector3(2.5f, 2.0f, 1.5f));
			Assert.True(5 * scalarMultiplicationArgument == new Vector3(25.0f, 20.0f, 15.0f));

			var point3 = new Vector3(2, 3, 4);
			point3 *= 6;
			Assert.True(point3.Equals(new Vector3(12, 18, 24), .01f));
		}

		[Fact]
		public void ScalarDivision()
		{
			var scalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			Assert.True(scalarMultiplicationArgument / 2 == new Vector3(2.5f, 2.0f, 1.5f));

			var point3 = new Vector3(12, 18, 24);
			point3 /= 6;
			Assert.True(point3.Equals(new Vector3(2, 3, 4), .01f));
		}

		[Fact]
		public void DotProduct()
		{
			var test1 = new Vector3(10, 1, 2);
			var test2 = new Vector3(1, 0, 0);
			double dotResult = Vector3Ex.Dot(test2, test1);
			Assert.True(dotResult == 10);
		}

		[Fact]
		public void CrossProduct()
		{
			var test1 = new Vector3(10, 0, 0);
			var test2 = new Vector3(1, 1, 0);
			Vector3 crossResult = Vector3Ex.Cross(test2, test1);
			Assert.True(crossResult.X == 0);
			Assert.True(crossResult.Y == 0);
			Assert.True(crossResult.Z < 0);
		}

		[Fact]
		public void Normalize()
		{
			var point3 = new Vector3(3, -4, 5);
			point3.Normalize();
			Assert.True(point3.Length > 0.99 && point3.Length < 1.01);
		}
	}
}