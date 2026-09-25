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

namespace MatterHackers.PolygonMesh.UnitTests
{
	public class MeshMeasureTests
	{
		private const double Epsilon = 1e-9;

		[Test]
		public async Task ScaledCubeHasBoxVolumeAndArea()
		{
			var cube = PlatonicSolids.CreateCube(2, 3, 4);

			await Assert.That(cube.GetVolume()).IsEqualTo(24).Within(Epsilon);
			await Assert.That(cube.GetSurfaceArea()).IsEqualTo(52).Within(Epsilon);
		}

		[Test]
		public async Task FlippedCubeHasNegativeVolumeAndTheSameArea()
		{
			var cube = PlatonicSolids.CreateCube(2, 3, 4);
			cube.ReverseFaces();

			await Assert.That(cube.GetVolume()).IsEqualTo(-24).Within(Epsilon)
				.Because("an inward-wound closed mesh encloses its volume with the opposite sign");
			await Assert.That(cube.GetSurfaceArea()).IsEqualTo(52).Within(Epsilon)
				.Because("area does not depend on winding");
		}

		[Test]
		public async Task OpenBoxVolumeDependsOnWhereTheOriginIs()
		{
			// Remove the two z = +0.5 triangles (CreateCube's "front", its first two faces) of a unit cube
			// centered on the origin.
			var centered = PlatonicSolids.CreateCube(1, 1, 1);
			centered.Faces.RemoveRange(0, 2);

			// The missing z = +0.5 face would have added area * distance-from-origin / 3 = 1 * 0.5 / 3.
			await Assert.That(centered.GetVolume()).IsEqualTo(1 - 1.0 / 6).Within(Epsilon);
			await Assert.That(centered.GetSurfaceArea()).IsEqualTo(5).Within(Epsilon);

			// Moving the same open box up by 0.5 puts its missing face at z = 1 and changes the sum,
			// which is why the sum is not a volume on an open mesh.
			var raised = centered.GetVolume(Matrix4X4.CreateTranslation(0, 0, 0.5));
			await Assert.That(raised).IsEqualTo(1 - 1.0 / 3).Within(Epsilon);
		}

		[Test]
		public async Task TransformOverloadMatchesManualScaling()
		{
			var unitCube = PlatonicSolids.CreateCube(1, 1, 1);
			var scaledCube = PlatonicSolids.CreateCube(2, 3, 4);
			var transform = Matrix4X4.CreateScale(2, 3, 4) * Matrix4X4.CreateRotationZ(0.7) * Matrix4X4.CreateTranslation(5, -6, 7);

			await Assert.That(unitCube.GetVolume(transform)).IsEqualTo(scaledCube.GetVolume()).Within(Epsilon);
			await Assert.That(unitCube.GetSurfaceArea(transform)).IsEqualTo(scaledCube.GetSurfaceArea()).Within(Epsilon);

			// A mirror turns the mesh inside out, so its volume changes sign with the determinant.
			var mirror = Matrix4X4.CreateScale(-2, 3, 4);
			await Assert.That(unitCube.GetVolume(mirror)).IsEqualTo(-24).Within(Epsilon);
			await Assert.That(unitCube.GetSurfaceArea(mirror)).IsEqualTo(52).Within(Epsilon);
		}
	}
}
