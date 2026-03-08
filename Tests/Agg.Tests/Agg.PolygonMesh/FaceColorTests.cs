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
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	public class FaceColorTests
	{
		[Test]
		public async Task CopyPreservesFaceColors()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var colors = new Color[mesh.Faces.Count];
			for (int i = 0; i < colors.Length; i++)
			{
				colors[i] = i < colors.Length / 2 ? Color.Red : Color.Blue;
			}

			mesh.FaceColors = colors;

			var copy = mesh.Copy(CancellationToken.None);

			await Assert.That(copy.FaceColors).IsNotNull();
			await Assert.That(copy.FaceColors.Length).IsEqualTo(mesh.FaceColors.Length);

			for (int i = 0; i < colors.Length; i++)
			{
				await Assert.That(copy.FaceColors[i]).IsEqualTo(mesh.FaceColors[i]);
			}

			// Verify it's a deep copy — modifying original doesn't affect copy
			mesh.FaceColors[0] = Color.Green;
			await Assert.That(copy.FaceColors[0]).IsEqualTo(Color.Red);
		}

		[Test]
		public async Task CopyWithoutFaceColorsReturnsNull()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var copy = mesh.Copy(CancellationToken.None);
			await Assert.That(copy.FaceColors).IsNull();
		}

		[Test]
		public async Task BooleanUnionPreservesFaceColors()
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
			bool hasRed = result.FaceColors.Any(c => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0);
			bool hasBlue = result.FaceColors.Any(c => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255);
			await Assert.That(hasRed).IsTrue();
			await Assert.That(hasBlue).IsTrue();
		}

		[Test]
		public async Task BooleanSubtractPreservesFaceColors()
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
			bool hasRed = result.FaceColors.Any(c => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0);
			bool hasBlue = result.FaceColors.Any(c => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255);
			await Assert.That(hasRed).IsTrue();
			await Assert.That(hasBlue).IsTrue();
		}

		[Test]
		public async Task DoArrayWithColorsPreservesFaceColors()
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
			bool hasRed = result.FaceColors.Any(c => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0);
			bool hasGreen = result.FaceColors.Any(c => c.Red0To255 == 0 && c.Green0To255 == 255 && c.Blue0To255 == 0);
			bool hasBlue = result.FaceColors.Any(c => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255);
			await Assert.That(hasRed).IsTrue();
			await Assert.That(hasGreen).IsTrue();
			await Assert.That(hasBlue).IsTrue();
		}

		[Test]
		public async Task BooleanWithoutColorsReturnsNullFaceColors()
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

		[Test]
		public async Task CleanAndMergePreservesFaceColors()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			// Assign distinct colors to each face
			mesh.FaceColors = new Color[mesh.Faces.Count];
			for (int i = 0; i < mesh.FaceColors.Length; i++)
			{
				mesh.FaceColors[i] = i < mesh.FaceColors.Length / 2 ? Color.Red : Color.Blue;
			}

			int originalFaceCount = mesh.Faces.Count;
			mesh.CleanAndMerge();

			// Face count should be unchanged for a valid cube
			await Assert.That(mesh.Faces.Count).IsEqualTo(originalFaceCount);
			await Assert.That(mesh.FaceColors).IsNotNull();
			await Assert.That(mesh.FaceColors.Length).IsEqualTo(mesh.Faces.Count);

			// Colors should be preserved
			bool hasRed = mesh.FaceColors.Any(c => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0);
			bool hasBlue = mesh.FaceColors.Any(c => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255);
			await Assert.That(hasRed).IsTrue();
			await Assert.That(hasBlue).IsTrue();
		}

		[Test]
		public async Task CleanAndMergeWithDegenerateFacesPreservesFaceColors()
		{
			// Create a mesh with a degenerate face that CleanAndMerge will remove
			var mesh = new Mesh();
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(1, 0, 0));
			mesh.Vertices.Add(new Vector3Float(0, 1, 0));
			mesh.Vertices.Add(new Vector3Float(1, 1, 0)); // duplicate position for degenerate
			mesh.Vertices.Add(new Vector3Float(1, 0, 0)); // exact duplicate of vertex 1

			// Normal triangle
			mesh.Faces.Add(0, 1, 2, mesh.Vertices);
			// Degenerate triangle (vertex 1 and 4 share the same position, so after merge iv1 == iv4)
			mesh.Faces.Add(1, 4, 3, mesh.Vertices);

			mesh.FaceColors = new Color[] { Color.Red, Color.Blue };

			mesh.CleanAndMerge();

			// The degenerate face (vertices 1 and 4 merge to same index) should be removed
			// but the surviving face's color should be preserved
			await Assert.That(mesh.FaceColors).IsNotNull();
			await Assert.That(mesh.FaceColors.Length).IsEqualTo(mesh.Faces.Count);

			// First face (Red) should survive
			await Assert.That(mesh.FaceColors[0].Red0To255).IsEqualTo(255);
		}

		[Test]
		public async Task ManifoldRunDataExtractsFaceColorsCorrectly()
		{
			// Two non-overlapping cubes — every face must come from exactly one source mesh
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var colorA = Color.Red;
			var colorB = Color.Blue;

			// Use DoArray directly (what CombineParticipants calls)
			var result = BooleanProcessing.DoArray(
				new[] {
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
			int redCount = result.FaceColors.Count(c => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0);
			int blueCount = result.FaceColors.Count(c => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255);

			await Assert.That(redCount).IsEqualTo(12);
			await Assert.That(blueCount).IsEqualTo(12);
			await Assert.That(redCount + blueCount).IsEqualTo(result.Faces.Count);
		}

		[Test]
		public async Task FaceColorsSurviveFullCleanupPipeline()
		{
			// Simulate the full pipeline that CombineParticipants does:
			// BooleanProcessing.DoArray -> CleanAndMerge -> RemoveUnusedVertices
			var meshA = PlatonicSolids.CreateCube(10, 10, 10);
			var meshB = PlatonicSolids.CreateCube(10, 10, 10);

			var result = BooleanProcessing.DoArray(
				new[] {
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

			bool hasRed = result.FaceColors.Any(c => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0);
			bool hasBlue = result.FaceColors.Any(c => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255);
			await Assert.That(hasRed).IsTrue();
			await Assert.That(hasBlue).IsTrue();
		}

		[Test]
		public async Task IntersectPreservesBothFaceColors()
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
			bool hasRed = result.FaceColors.Any(c => c.Red0To255 == 255 && c.Green0To255 == 0 && c.Blue0To255 == 0);
			bool hasBlue = result.FaceColors.Any(c => c.Red0To255 == 0 && c.Green0To255 == 0 && c.Blue0To255 == 255);
			await Assert.That(hasRed).IsTrue();
			await Assert.That(hasBlue).IsTrue();
		}

		[Test]
		public async Task RemoveUnusedVerticesPreservesFaceColors()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			mesh.FaceColors = new Color[mesh.Faces.Count];
			for (int i = 0; i < mesh.FaceColors.Length; i++)
			{
				mesh.FaceColors[i] = i < mesh.FaceColors.Length / 2 ? Color.Red : Color.Blue;
			}

			mesh.RemoveUnusedVertices();

			await Assert.That(mesh.FaceColors).IsNotNull();
			await Assert.That(mesh.FaceColors.Length).IsEqualTo(mesh.Faces.Count);
		}
	}
}
