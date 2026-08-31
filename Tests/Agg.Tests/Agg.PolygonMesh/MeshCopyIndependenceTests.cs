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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// Face is a reference type, so a "copy" that hands the new mesh the source's own Face objects is
	/// not a copy at all - every in place face edit (ReverseFace, CalculateNormals) reaches through and
	/// rewrites the source. These tests pin down that a copied mesh owns its faces.
	/// </summary>
	public class MeshCopyIndependenceTests
	{
		/// <summary>
		/// Two triangles with distinguishable winding, so a reversal anywhere is visible.
		/// </summary>
		private static Mesh MakeTwoTriangles()
		{
			var mesh = new Mesh();
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(10, 0, 0));
			mesh.Vertices.Add(new Vector3Float(10, 10, 0));
			mesh.Vertices.Add(new Vector3Float(0, 10, 0));
			mesh.Faces.Add(0, 1, 2, mesh.Vertices);
			mesh.Faces.Add(0, 2, 3, mesh.Vertices);

			return mesh;
		}

		private static (int v0, int v1, int v2, Vector3Float normal)[] Snapshot(Mesh mesh)
		{
			var snapshot = new (int, int, int, Vector3Float)[mesh.Faces.Count];
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				var face = mesh.Faces[i];
				snapshot[i] = (face.v0, face.v1, face.v2, face.normal);
			}

			return snapshot;
		}

		private static async Task AssertFacesMatch(Mesh mesh, (int v0, int v1, int v2, Vector3Float normal)[] expected)
		{
			await Assert.That(mesh.Faces.Count).IsEqualTo(expected.Length);
			for (int i = 0; i < expected.Length; i++)
			{
				var face = mesh.Faces[i];
				await Assert.That(face.v0).IsEqualTo(expected[i].v0);
				await Assert.That(face.v1).IsEqualTo(expected[i].v1);
				await Assert.That(face.v2).IsEqualTo(expected[i].v2);
				await Assert.That(face.normal).IsEqualTo(expected[i].normal);
			}
		}

		[Test]
		public async Task ReversingACopyLeavesTheSourceAlone()
		{
			var source = MakeTwoTriangles();
			var before = Snapshot(source);

			var copy = source.Copy(CancellationToken.None);
			copy.ReverseFaces();

			await AssertFacesMatch(source, before);
		}

		[Test]
		public async Task ReversingAMeshBuiltFromAnothersFaceListLeavesTheSourceAlone()
		{
			var source = MakeTwoTriangles();
			var before = Snapshot(source);

			// The same hazard one level down - this is what Copy is built on, and several callers use
			// it directly to get a transformable mesh out of a source they must not disturb.
			var copy = new Mesh(source.Vertices, source.Faces);
			copy.ReverseFaces();

			await AssertFacesMatch(source, before);
		}

		[Test]
		public async Task RecalculatingNormalsOnACopyLeavesTheSourceAlone()
		{
			var source = MakeTwoTriangles();
			var before = Snapshot(source);

			var copy = source.Copy(CancellationToken.None);
			// Move the copy's vertices without going through Transform (which rebuilds faces anyway),
			// then recompute - the new normals must not land on the source's faces.
			for (int i = 0; i < copy.Vertices.Count; i++)
			{
				copy.Vertices[i] = new Vector3Float(copy.Vertices[i].X, copy.Vertices[i].Z, copy.Vertices[i].Y);
			}

			copy.CalculateNormals();

			await AssertFacesMatch(source, before);
		}

		[Test]
		public async Task ACopyStillEqualsItsSource()
		{
			var source = MakeTwoTriangles();

			var copy = source.Copy(CancellationToken.None);

			await Assert.That(copy.Equals(source)).IsTrue();
			await AssertFacesMatch(copy, Snapshot(source));
		}

		[Test]
		public async Task ReversingACopyDoesNotSwapTheSourcesTextureUvs()
		{
			var source = MakeTwoTriangles();
			source.FaceTextures[0] = new FaceTextureData(null,
				new Vector2Float(0, 0),
				new Vector2Float(1, 0),
				new Vector2Float(1, 1));

			var copy = source.Copy(CancellationToken.None);
			copy.ReverseFaces();

			// ReverseFace replaces the entry rather than editing the FaceTextureData in place, which is
			// what lets the copy share the values safely.
			await Assert.That(source.FaceTextures[0].uv0).IsEqualTo(new Vector2Float(0, 0));
			await Assert.That(source.FaceTextures[0].uv2).IsEqualTo(new Vector2Float(1, 1));
			await Assert.That(copy.FaceTextures[0].uv0).IsEqualTo(new Vector2Float(1, 1));
			await Assert.That(copy.FaceTextures[0].uv2).IsEqualTo(new Vector2Float(0, 0));
		}

		[Test]
		public async Task ColoringACopysFacesLeavesTheSourceUncolored()
		{
			var source = MakeTwoTriangles();
			source.FaceColors = new[] { Agg.Color.Red, Agg.Color.Red };

			var copy = source.Copy(CancellationToken.None);
			copy.FaceColors[0] = Agg.Color.Blue;

			await Assert.That(source.FaceColors[0]).IsEqualTo(Agg.Color.Red);
		}
	}
}
