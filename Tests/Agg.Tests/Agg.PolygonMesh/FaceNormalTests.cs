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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// A zero area face is topologically real - the seams where a solid touches itself are made of them,
	/// and they carry the edges that keep such a mesh manifold - so the mesh has to be able to hold one
	/// without its normal turning into NaN.
	/// </summary>
	public class FaceNormalTests
	{
		[Test]
		public async Task ZeroAreaFaceGetsAFiniteNormal()
		{
			var vertices = new List<Vector3Float>
			{
				new Vector3Float(0, 0, 0),
				new Vector3Float(10, 0, 0),
				// A third corner on the line through the first two: three distinct vertices, no area.
				new Vector3Float(5, 0, 0),
				// And a fourth at a position already used, which is what a self-touch seam looks like.
				new Vector3Float(10, 0, 0),
			};

			foreach (var face in new[]
			{
				new Face(0, 1, 2, vertices),
				new Face(0, 1, 3, vertices),
			})
			{
				await Assert.That(float.IsNaN(face.normal.X) || float.IsNaN(face.normal.Y) || float.IsNaN(face.normal.Z)).IsFalse()
					.Because("a NaN normal compares false against every tolerance, so it silently breaks vertex merging and coplanar walks");

				await Assert.That(face.normal).IsEqualTo(Vector3Float.Zero)
					.Because("a face with no area has no direction to point in");
			}
		}

		[Test]
		public async Task FaceWithAreaStillGetsAUnitNormal()
		{
			var vertices = new List<Vector3Float>
			{
				new Vector3Float(0, 0, 0),
				new Vector3Float(10, 0, 0),
				new Vector3Float(0, 10, 0),
			};

			var face = new Face(0, 1, 2, vertices);

			await Assert.That(face.normal).IsEqualTo(new Vector3Float(0, 0, 1))
				.Because("guarding the degenerate case must not change the normal of a real face");
		}
	}
}
