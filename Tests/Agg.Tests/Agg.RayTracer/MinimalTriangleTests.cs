/*
Copyright (c) 2026, Lars Brubaker
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

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.RayTracer
{
	public class MinimalTriangleTests
	{
		/// <summary>
		/// A triangle's box has to hold its own corners. It used to be kept as a float centre and half size,
		/// and centre - halfSize rounds: a cylinder cap triangle with a corner at x = -3.8e-15 and another at
		/// 0.98 got a box starting at exactly 0, so a ray straight down x = 0 - inside the triangle - missed
		/// the box and was never tested against the triangle.
		/// </summary>
		[Test]
		public async Task TheBoxHoldsEveryCorner()
		{
			var corners = new[]
			{
				new Vector3Float(0, 0, 5),
				new Vector3Float(0.9801714f, 9.951847f, 5),
				new Vector3Float(-3.8285686E-15f, 10, 5),
			};

			var triangle = new MinimalTriangle((face, vertex) => corners[vertex], 0);
			var box = triangle.GetAxisAlignedBoundingBox();

			foreach (var corner in corners)
			{
				await Assert.That(box.MinXYZ.X <= corner.X && box.MinXYZ.Y <= corner.Y && box.MinXYZ.Z <= corner.Z).IsTrue();
				await Assert.That(box.MaxXYZ.X >= corner.X && box.MaxXYZ.Y >= corner.Y && box.MaxXYZ.Z >= corner.Z).IsTrue();
			}

			var hit = triangle.GetClosestIntersection(new Ray(new Vector3(0, 9.9, 15), -Vector3.UnitZ));
			await Assert.That(hit).IsNotNull();
		}
	}
}
