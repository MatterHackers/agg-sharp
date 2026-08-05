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

using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The face-colour behaviour a CSG backend has to deliver, written once so both
	/// engines are held to the same assertions rather than to two drifting copies.
	/// </summary>
	/// <remarks>
	/// <see cref="FaceColorTests"/> runs these against the default (ManifoldNET) engine
	/// and <see cref="ManifoldRustBackendTests"/> runs them again with
	/// <see cref="BooleanProcessing.UseManifoldRust"/> set. Nothing here mentions a
	/// backend: the scenarios go through the public
	/// <see cref="BooleanProcessing"/> entry points, which is exactly the routing being
	/// tested.
	/// </remarks>
	internal static class FaceColorBooleanScenarios
	{
		public static async Task BooleanUnionPreservesFaceColors()
		{
			// Create two cubes at different positions with different colors
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var matrixA = Matrix4X4.CreateTranslation(-3, 0, 0);
			var matrixB = Matrix4X4.CreateTranslation(3, 0, 0);

			var colorA = Color.Red;
			var colorB = Color.Blue;

			var result = BooleanProcessing.Do(
				meshA, matrixA,
				meshB, matrixB,
				CsgModes.Union,
				ProcessingModes.Polygons,
				meshColors: new[] { colorA, colorB });

			await Assert.That(result).IsNotNull();
			await Assert.That(result.FaceColors).IsNotNull();
			await Assert.That(result.FaceColors.Length).IsEqualTo(result.Faces.Count);

			// Verify we have both colors in the result
			await Assert.That(HasRed(result)).IsTrue();
			await Assert.That(HasBlue(result)).IsTrue();
		}

		public static async Task BooleanSubtractPreservesFaceColors()
		{
			var meshA = PlatonicSolids.CreateCube(20, 20, 20);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var matrixA = Matrix4X4.Identity;
			var matrixB = Matrix4X4.CreateTranslation(5, 5, 5);

			var colorA = Color.Red;
			var colorB = Color.Blue;

			var result = BooleanProcessing.Do(
				meshA, matrixA,
				meshB, matrixB,
				CsgModes.Subtract,
				ProcessingModes.Polygons,
				meshColors: new[] { colorA, colorB });

			await Assert.That(result).IsNotNull();
			await Assert.That(result.FaceColors).IsNotNull();
			await Assert.That(result.FaceColors.Length).IsEqualTo(result.Faces.Count);

			// Subtract result should have faces from both meshes
			await Assert.That(HasRed(result)).IsTrue();
			await Assert.That(HasBlue(result)).IsTrue();
		}

		public static async Task DoArrayWithColorsPreservesFaceColors()
		{
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);
			var meshC = PlatonicSolids.CreateCube(10, 10, 10);

			var items = new[]
			{
				(meshA, Matrix4X4.CreateTranslation(-8, 0, 0)),
				(meshB, Matrix4X4.CreateTranslation(0, 0, 0)),
				(meshC, Matrix4X4.CreateTranslation(8, 0, 0)),
			};

			var colors = new[] { Color.Red, Color.Green, Color.Blue };

			var result = BooleanProcessing.DoArray(
				items,
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None,
				meshColors: colors);

			await Assert.That(result).IsNotNull();
			await Assert.That(result.FaceColors).IsNotNull();
			await Assert.That(result.FaceColors.Length).IsEqualTo(result.Faces.Count);

			// Verify all three colors are present
			await Assert.That(HasRed(result)).IsTrue();
			await Assert.That(HasGreen(result)).IsTrue();
			await Assert.That(HasBlue(result)).IsTrue();
		}

		public static async Task BooleanWithoutColorsReturnsNullFaceColors()
		{
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var result = BooleanProcessing.Do(
				meshA, Matrix4X4.CreateTranslation(-3, 0, 0),
				meshB, Matrix4X4.CreateTranslation(3, 0, 0),
				CsgModes.Union,
				ProcessingModes.Polygons);

			await Assert.That(result).IsNotNull();
			await Assert.That(result.FaceColors).IsNull();
		}

		public static async Task ManifoldRunDataExtractsFaceColorsCorrectly()
		{
			// Two non-overlapping cubes — every face must come from exactly one source mesh
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var colorA = Color.Red;
			var colorB = Color.Blue;

			// Use DoArray directly (what CombineParticipants calls)
			var result = BooleanProcessing.DoArray(
				new[]
				{
					(meshA, Matrix4X4.CreateTranslation(-20, 0, 0)),
					(meshB, Matrix4X4.CreateTranslation(20, 0, 0))
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None,
				meshColors: new[] { colorA, colorB });

			await Assert.That(result).IsNotNull();
			await Assert.That(result.FaceColors).IsNotNull();
			await Assert.That(result.FaceColors.Length).IsEqualTo(result.Faces.Count);

			// Count red and blue faces — should be 12 each (cube = 12 triangles)
			int redCount = result.FaceColors.Count(IsRed);
			int blueCount = result.FaceColors.Count(IsBlue);

			await Assert.That(redCount).IsEqualTo(12);
			await Assert.That(blueCount).IsEqualTo(12);
			await Assert.That(redCount + blueCount).IsEqualTo(result.Faces.Count);
		}

		public static async Task FaceColorsSurviveFullCleanupPipeline()
		{
			// Simulate the full pipeline that CombineParticipants does:
			// BooleanProcessing.DoArray -> CleanAndMerge -> RemoveUnusedVertices
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var result = BooleanProcessing.DoArray(
				new[]
				{
					(meshA, Matrix4X4.CreateTranslation(-3, 0, 0)),
					(meshB, Matrix4X4.CreateTranslation(3, 0, 0))
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None,
				meshColors: new[] { Color.Red, Color.Blue });

			await Assert.That(result).IsNotNull();
			await Assert.That(result.FaceColors).IsNotNull();

			// Now run the same cleanup pipeline that CombineParticipants + Combine do
			result.CleanAndMerge();
			result.RemoveUnusedVertices();
			// Combine() also calls CleanAndMerge a second time
			result.CleanAndMerge();

			await Assert.That(result.FaceColors).IsNotNull();
			await Assert.That(result.FaceColors.Length).IsEqualTo(result.Faces.Count);

			await Assert.That(HasRed(result)).IsTrue();
			await Assert.That(HasBlue(result)).IsTrue();
		}

		public static async Task IntersectPreservesBothFaceColors()
		{
			// Two overlapping cubes with Intersect — result should have faces from both sources
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var result = BooleanProcessing.Do(
				meshA, Matrix4X4.CreateTranslation(-3, 0, 0),
				meshB, Matrix4X4.CreateTranslation(3, 0, 0),
				CsgModes.Intersect,
				ProcessingModes.Polygons,
				meshColors: new[] { Color.Red, Color.Blue });

			await Assert.That(result).IsNotNull();
			await Assert.That(result.FaceColors).IsNotNull();
			await Assert.That(result.FaceColors.Length).IsEqualTo(result.Faces.Count);

			// Intersection should have faces from both meshes
			await Assert.That(HasRed(result)).IsTrue();
			await Assert.That(HasBlue(result)).IsTrue();
		}

		public static async Task SubtractFromMeshWithFaceColorsPreservesColors()
		{
			// First, create a combined mesh with FaceColors via boolean union
			var cubeA = PlatonicSolids.CreateCube(15, 15, 15);
			var cubeB = PlatonicSolids.CreateCube(15, 15, 15);

			// Union two overlapping cubes with different colors
			var combinedMesh = BooleanProcessing.Do(
				cubeA, Matrix4X4.CreateTranslation(3, 0, 0),
				cubeB, Matrix4X4.CreateTranslation(-3, 0, 0),
				CsgModes.Union,
				meshColors: new[] { Color.Blue, Color.Green });

			await Assert.That(combinedMesh.FaceColors).IsNotNull()
				.Because("Union with meshColors should produce FaceColors");

			bool hasBlue = combinedMesh.FaceColors.Any(c => c.Blue0To255 == 255 && c.Red0To255 == 0);
			bool hasGreen = combinedMesh.FaceColors.Any(c => c.Green0To255 == 255 && c.Red0To255 == 0);
			await Assert.That(hasBlue).IsTrue().Because("Combined mesh should have blue faces");
			await Assert.That(hasGreen).IsTrue().Because("Combined mesh should have green faces");

			// Now subtract this combined mesh (which has FaceColors) from a larger cube
			var keepCube = PlatonicSolids.CreateCube(40, 40, 40);
			var resultMesh = BooleanProcessing.Do(
				keepCube, Matrix4X4.Identity,
				combinedMesh, Matrix4X4.Identity,
				CsgModes.Subtract,
				meshColors: new[] { Color.Red, Color.White }); // White is placeholder for remove

			await Assert.That(resultMesh).IsNotNull();
			await Assert.That(resultMesh.Faces.Count).IsGreaterThan(12);
			await Assert.That(resultMesh.FaceColors).IsNotNull()
				.Because("Subtract result should have FaceColors");

			// The result should have red faces (from keep cube) and blue+green faces (from cavity)
			bool resultHasRed = resultMesh.FaceColors.Any(IsRed);
			bool resultHasBlue = resultMesh.FaceColors.Any(c => c.Blue0To255 == 255 && c.Red0To255 == 0);
			bool resultHasGreen = resultMesh.FaceColors.Any(c => c.Green0To255 == 255 && c.Red0To255 == 0);
			await Assert.That(resultHasRed).IsTrue().Because("Keep surfaces should be red");

			// Log what colors we actually got for debugging
			var distinctColors = resultMesh.FaceColors.Distinct().ToList();
			System.Diagnostics.Debug.WriteLine($"Result has {resultMesh.Faces.Count} faces, {distinctColors.Count} distinct colors:");
			foreach (var c in distinctColors)
			{
				var count = resultMesh.FaceColors.Count(fc => fc == c);
				System.Diagnostics.Debug.WriteLine($"  R={c.Red0To255} G={c.Green0To255} B={c.Blue0To255} A={c.Alpha0To255}: {count} faces");
			}

			await Assert.That(resultHasBlue).IsTrue().Because("Cavity should have blue faces from combined mesh");
			await Assert.That(resultHasGreen).IsTrue().Because("Cavity should have green faces from combined mesh");
		}

		private static bool IsRed(Color c) => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0;

		private static bool IsGreen(Color c) => c.Red0To255 == 0 && c.Green0To255 == 255 && c.Blue0To255 == 0;

		private static bool IsBlue(Color c) => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255;

		private static bool HasRed(Mesh mesh) => mesh.FaceColors.Any(IsRed);

		private static bool HasGreen(Mesh mesh) => mesh.FaceColors.Any(IsGreen);

		private static bool HasBlue(Mesh mesh) => mesh.FaceColors.Any(IsBlue);
	}
}
