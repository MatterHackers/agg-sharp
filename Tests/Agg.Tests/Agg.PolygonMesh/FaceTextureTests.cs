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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// FaceTextures is keyed by face index, so every operation that rebuilds the face list has to
	/// move the entries with the faces. These tests pin that down: a texture entry must always
	/// describe the face it is filed under, or it silently paints the wrong triangle.
	/// </summary>
	public class FaceTextureTests
	{
		private static readonly ImageBuffer TestTexture = new ImageBuffer(4, 4);

		/// <summary>
		/// A triangle in the XZ plane whose UVs are the (x, z) position scaled by 1/size. Any operation
		/// that preserves texturing correctly leaves every face corner's UV equal to that same function
		/// of its position, no matter how the triangle was cut up.
		/// </summary>
		private static Mesh MakeUvIsPositionTriangle(float size)
		{
			var mesh = new Mesh();
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(size, 0, 0));
			mesh.Vertices.Add(new Vector3Float(0, 0, size));
			mesh.Faces.Add(0, 1, 2, mesh.Vertices);

			mesh.FaceTextures[0] = new FaceTextureData(TestTexture,
				new Vector2Float(0, 0),
				new Vector2Float(1, 0),
				new Vector2Float(0, 1));

			return mesh;
		}

		private static async Task AssertUvsMatchPositions(Mesh mesh, float size)
		{
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				await Assert.That(mesh.FaceTextures.ContainsKey(i)).IsTrue();

				var data = mesh.FaceTextures[i];
				var face = mesh.Faces[i];
				var uvs = new[] { data.uv0, data.uv1, data.uv2 };
				var verts = new[] { mesh.Vertices[face.v0], mesh.Vertices[face.v1], mesh.Vertices[face.v2] };

				for (int corner = 0; corner < 3; corner++)
				{
					await Assert.That((double)uvs[corner].X).IsEqualTo(verts[corner].X / size).Within(1e-4);
					await Assert.That((double)uvs[corner].Y).IsEqualTo(verts[corner].Z / size).Within(1e-4);
				}
			}
		}

		[Test]
		public async Task CopyPreservesFaceTextures()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			mesh.FaceTextures[2] = new FaceTextureData(TestTexture,
				new Vector2Float(0, 0),
				new Vector2Float(1, 0),
				new Vector2Float(0, 1));

			var copy = mesh.Copy(CancellationToken.None);

			await Assert.That(copy.FaceTextures.Count).IsEqualTo(1);
			await Assert.That(copy.FaceTextures.ContainsKey(2)).IsTrue();
			await Assert.That(copy.FaceTextures[2].image).IsEqualTo(TestTexture);

			// The dictionary itself has to be a copy, or texturing one mesh textures the other
			copy.FaceTextures.Remove(2);
			await Assert.That(mesh.FaceTextures.ContainsKey(2)).IsTrue();
		}

		[Test]
		public async Task CleanAndMergeMovesFaceTexturesWithTheirFaces()
		{
			// Face 0 is degenerate once the duplicate vertices weld, so face 1 becomes face 0 and its
			// texture has to come with it. Leaving the keys alone paints face 1 with face 0's UVs.
			var mesh = new Mesh();
			mesh.Vertices.Add(new Vector3Float(1, 0, 0));
			mesh.Vertices.Add(new Vector3Float(1, 0, 0)); // exact duplicate of vertex 0
			mesh.Vertices.Add(new Vector3Float(1, 1, 0));
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(5, 0, 0));
			mesh.Vertices.Add(new Vector3Float(0, 5, 0));

			mesh.Faces.Add(0, 1, 2, mesh.Vertices); // degenerate after the weld
			mesh.Faces.Add(3, 4, 5, mesh.Vertices);

			mesh.FaceTextures[0] = new FaceTextureData(TestTexture, new Vector2Float(9, 9), new Vector2Float(9, 9), new Vector2Float(9, 9));
			mesh.FaceTextures[1] = new FaceTextureData(TestTexture, new Vector2Float(0, 0), new Vector2Float(1, 0), new Vector2Float(0, 1));

			mesh.CleanAndMerge();

			await Assert.That(mesh.Faces.Count).IsEqualTo(1);
			await Assert.That(mesh.FaceTextures.Count).IsEqualTo(1);
			await Assert.That(mesh.FaceTextures.ContainsKey(0)).IsTrue();
			await Assert.That(mesh.FaceTextures[0].uv1.X).IsEqualTo(1f);
		}

		[Test]
		public async Task MergeVerticesMovesFaceColorsAndTexturesWithTheirFaces()
		{
			// The first face collapses when its two near vertices weld, so the second face moves to
			// index 0 - both its color and its texture have to move with it.
			var mesh = new Mesh();
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(0.0001f, 0, 0));
			mesh.Vertices.Add(new Vector3Float(0, 1, 0));
			mesh.Vertices.Add(new Vector3Float(10, 0, 0));
			mesh.Vertices.Add(new Vector3Float(20, 0, 0));
			mesh.Vertices.Add(new Vector3Float(10, 10, 0));

			mesh.Faces.Add(0, 1, 2, mesh.Vertices);
			mesh.Faces.Add(3, 4, 5, mesh.Vertices);

			mesh.FaceColors = new[] { Color.Blue, Color.Red };
			mesh.FaceTextures[1] = new FaceTextureData(TestTexture, new Vector2Float(0, 0), new Vector2Float(1, 0), new Vector2Float(0, 1));

			mesh.MergeVertices(0.01, 0);

			await Assert.That(mesh.Faces.Count).IsEqualTo(1);
			await Assert.That(mesh.FaceColors.Length).IsEqualTo(1);
			await Assert.That(mesh.FaceColors[0]).IsEqualTo(Color.Red);
			await Assert.That(mesh.FaceTextures.Count).IsEqualTo(1);
			await Assert.That(mesh.FaceTextures.ContainsKey(0)).IsTrue();
		}

		[Test]
		public async Task ReverseFacesSwapsTextureUvsSoTheyStayWithTheirVertices()
		{
			var mesh = MakeUvIsPositionTriangle(10);

			mesh.ReverseFaces();

			await AssertUvsMatchPositions(mesh, 10);
		}

		[Test]
		public async Task SplitOnPlanesInterpolatesTexturesOntoTheCutPieces()
		{
			var mesh = MakeUvIsPositionTriangle(10);

			mesh.SplitOnPlanes(new Vector3(0, 0, 1), new List<double> { 3, 6 }, 0.01);

			await Assert.That(mesh.Faces.Count).IsGreaterThan(1);
			await AssertUvsMatchPositions(mesh, 10);
		}

		[Test]
		public async Task SplitInterpolatesTexturesOntoTheCutPieces()
		{
			var mesh = MakeUvIsPositionTriangle(10);

			mesh.Split(new Plane(new Vector3(0, 0, 1), 4));

			await Assert.That(mesh.Faces.Count).IsGreaterThan(1);
			await AssertUvsMatchPositions(mesh, 10);
		}

		[Test]
		public async Task CopyAllFacesCarriesTexturesAtTheirNewIndices()
		{
			var destination = PlatonicSolids.CreateCube(10, 10, 10);
			int destinationFaceCount = destination.Faces.Count;

			var source = PlatonicSolids.CreateCube(5, 5, 5);
			source.FaceTextures[1] = new FaceTextureData(TestTexture, new Vector2Float(0, 0), new Vector2Float(1, 0), new Vector2Float(0, 1));

			destination.CopyAllFaces(source, Matrix4X4.Identity);

			await Assert.That(destination.FaceTextures.Count).IsEqualTo(1);
			await Assert.That(destination.FaceTextures.ContainsKey(destinationFaceCount + 1)).IsTrue();
		}

		[Test]
		public async Task CopyAllFacesKeepsTexturesOnTheirTrianglesAcrossADroppedFace()
		{
			// CreateFace drops a source face whose corners are not all distinct, so the textured face
			// arrives one index earlier than a flat fStart + sourceIndex offset would predict - and that
			// offset used to paint the wrong triangle (or nothing at all, at the end of the mesh).
			var source = new Mesh();
			source.Vertices.Add(new Vector3Float(0, 0, 0));
			source.Vertices.Add(new Vector3Float(1, 0, 0));
			source.Vertices.Add(new Vector3Float(1, 0, 0)); // same position as vertex 1, so no face is emitted
			source.Vertices.Add(new Vector3Float(0, 0, 0));
			source.Vertices.Add(new Vector3Float(5, 0, 0));
			source.Vertices.Add(new Vector3Float(0, 0, 5));

			source.Faces.Add(0, 1, 2, new Vector3Float(0, 1, 0));
			source.Faces.Add(3, 4, 5, source.Vertices);

			source.FaceTextures[1] = new FaceTextureData(TestTexture,
				new Vector2Float(0, 0),
				new Vector2Float(1, 0),
				new Vector2Float(0, 1));

			var destination = PlatonicSolids.CreateCube(10, 10, 10);
			int destinationFaceCount = destination.Faces.Count;

			destination.CopyAllFaces(source, Matrix4X4.Identity);

			await Assert.That(destination.Faces.Count).IsEqualTo(destinationFaceCount + 1)
				.Because("the degenerate source face emits no face");
			await Assert.That(destination.FaceTextures.Count).IsEqualTo(1);
			await Assert.That(destination.FaceTextures.ContainsKey(destinationFaceCount)).IsTrue()
				.Because("the texture belongs to the one triangle that was actually emitted");

			// And it really is the triangle the UVs were authored for
			var copiedFace = destination.Faces[destinationFaceCount];
			await Assert.That(destination.Vertices[copiedFace.v1].X).IsEqualTo(5f);
			await Assert.That(destination.Vertices[copiedFace.v2].Z).IsEqualTo(5f);
		}

		[Test]
		public async Task RemoveDegenerateFacesMovesFaceTexturesWithTheirFaces()
		{
			var mesh = new Mesh();
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(1, 0, 0));
			mesh.Vertices.Add(new Vector3Float(2, 0, 0)); // collinear, so face 0 has no area
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(5, 0, 0));
			mesh.Vertices.Add(new Vector3Float(0, 5, 0));

			mesh.Faces.Add(0, 1, 2, mesh.Vertices);
			mesh.Faces.Add(3, 4, 5, mesh.Vertices);

			mesh.FaceTextures[1] = new FaceTextureData(TestTexture, new Vector2Float(0, 0), new Vector2Float(1, 0), new Vector2Float(0, 1));

			mesh.RemoveDegenerateFaces(0.001);

			await Assert.That(mesh.Faces.Count).IsEqualTo(1);
			await Assert.That(mesh.FaceTextures.Count).IsEqualTo(1);
			await Assert.That(mesh.FaceTextures.ContainsKey(0)).IsTrue();
		}
	}
}
