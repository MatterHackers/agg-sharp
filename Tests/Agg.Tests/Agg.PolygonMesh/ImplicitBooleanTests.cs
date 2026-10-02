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

using System;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The implicit-surface processing modes (marching cubes, dual contouring) resample the
	/// operands instead of running the kernel, so their answers are only checked by volume.
	/// </summary>
	public class ImplicitBooleanTests
	{
		// A 20 mm cube with two 10 x 10 columns punched through opposite quarters: 8000 - 2 x 2000.
		private const double CubeSize = 20;
		private const double ExpectedVolume = 4000;

		private static (Mesh, Matrix4X4)[] CubeAndTwoColumns()
		{
			var column = PlatonicSolids.CreateCube(10, 10, 30);
			return new (Mesh, Matrix4X4)[]
			{
				(PlatonicSolids.CreateCube(CubeSize, CubeSize, CubeSize), Matrix4X4.Identity),
				(column, Matrix4X4.CreateTranslation(5, 5, 0)),
				(column, Matrix4X4.CreateTranslation(-5, -5, 0)),
			};
		}

		[Test]
		[Arguments(ProcessingModes.Marching_Cubes)]
		[Arguments(ProcessingModes.Dual_Contouring)]
		public async Task NarySubtractRemovesEveryCutterFromTheFirstOperand(ProcessingModes mode)
		{
			var result = BooleanProcessing.DoArray(CubeAndTwoColumns(),
				CsgModes.Subtract,
				mode,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None);

			await Assert.That(Math.Abs(result.GetVolume() - ExpectedVolume)).IsLessThan(ExpectedVolume * .05);
		}

		[Test]
		public async Task TwoOperandSubtractSamplesAtTheRequestedResolution()
		{
			// A sphere's signed distance is curved, so a grid of a handful of cells (the
			// resolution's exponent read as a cell count) visibly loses volume where 64 cells do not.
			var sphere = MinkowskiProcessing.SphereMesh(10, 64);
			var expected = sphere.GetVolume() - sphere.GetVolume() / 2;

			var result = BooleanProcessing.Do(sphere, Matrix4X4.Identity,
				PlatonicSolids.CreateCube(30, 30, 30), Matrix4X4.CreateTranslation(0, 0, -15),
				CsgModes.Subtract,
				ProcessingModes.Marching_Cubes,
				ProcessingResolution._64,
				ProcessingResolution._64);

			await Assert.That(Math.Abs(result.GetVolume() - expected)).IsLessThan(expected * .03);
		}
	}
}
