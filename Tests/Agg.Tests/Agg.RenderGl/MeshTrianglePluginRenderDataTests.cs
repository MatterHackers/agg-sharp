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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// MeshTrianglePlugin.CreateRenderData is the single biggest allocator in the thumbnail pipeline
	/// (a 5.1M face mesh measured 8,348 MB allocated / 1,958 MB retained), so its vertex buffers are
	/// sized and filled as tightly as the draw paths allow. These tests pin the layout the renderers
	/// read and the conditions under which a buffer is left empty.
	/// </summary>
	[NotInParallel]
	public class MeshTrianglePluginRenderDataTests
	{
		[Test]
		public async Task InterleavedDataMatchesTheVectorPodContents()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var gl = new GL(new RecordingGpuContext(idBase: 40000));

			var plugin = MeshTrianglePlugin.Get(gl, mesh, normal => Color.Red);

			await Assert.That(plugin.subMeshs.Count).IsEqualTo(1);
			var subMesh = plugin.subMeshs[0];

			int vertexCount = mesh.Faces.Count * 3;
			await Assert.That(subMesh.positionData.Count).IsEqualTo(vertexCount);
			await Assert.That(subMesh.normalData.Count).IsEqualTo(vertexCount);
			await Assert.That(subMesh.interleavedData.Length)
				.IsEqualTo(vertexCount * SubTriangleMesh.InterleavedStride);

			for (int faceIndex = 0; faceIndex < mesh.Faces.Count; faceIndex++)
			{
				var face = mesh.Faces[faceIndex];
				var faceVertexIndices = new[] { face.v0, face.v1, face.v2 };
				for (int cornerIndex = 0; cornerIndex < 3; cornerIndex++)
				{
					int vertexIndex = faceIndex * 3 + cornerIndex;
					var expectedPosition = mesh.Vertices[faceVertexIndices[cornerIndex]];

					var position = subMesh.positionData.Array[vertexIndex];
					var normal = subMesh.normalData.Array[vertexIndex];

					await Assert.That(position.positionX).IsEqualTo((float)expectedPosition.X);
					await Assert.That(position.positionY).IsEqualTo((float)expectedPosition.Y);
					await Assert.That(position.positionZ).IsEqualTo((float)expectedPosition.Z);
					await Assert.That(normal.normalX).IsEqualTo(face.normal.X);

					// The interleaved copy the D3D path uploads has to agree with the arrays the
					// legacy GL path binds - they are drawn as the same triangles.
					int offset = vertexIndex * SubTriangleMesh.InterleavedStride;
					await Assert.That(subMesh.interleavedData[offset + 0]).IsEqualTo(position.positionX);
					await Assert.That(subMesh.interleavedData[offset + 1]).IsEqualTo(position.positionY);
					await Assert.That(subMesh.interleavedData[offset + 2]).IsEqualTo(position.positionZ);
					await Assert.That(subMesh.interleavedData[offset + 3]).IsEqualTo(normal.normalX);
					await Assert.That(subMesh.interleavedData[offset + 4]).IsEqualTo(normal.normalY);
					await Assert.That(subMesh.interleavedData[offset + 5]).IsEqualTo(normal.normalZ);
					await Assert.That(subMesh.interleavedData[offset + 6]).IsEqualTo(0f);
					await Assert.That(subMesh.interleavedData[offset + 7]).IsEqualTo(0f);
				}
			}
		}

		[Test]
		public async Task VertexBuffersAreSizedToTheMeshWithNoGrowthOvershoot()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var gl = new GL(new RecordingGpuContext(idBase: 41000));

			var plugin = MeshTrianglePlugin.Get(gl, mesh, normal => Color.Red);
			var subMesh = plugin.subMeshs[0];

			// A mesh with no face textures is always one submesh, so the exact vertex count is known
			// before the fill. Letting VectorPOD double its way there re-copies the whole buffer at
			// every step and leaves up to 25% of the final allocation unused.
			await Assert.That(subMesh.positionData.AllocatedSize).IsEqualTo(subMesh.positionData.Count);
			await Assert.That(subMesh.normalData.AllocatedSize).IsEqualTo(subMesh.normalData.Count);
			await Assert.That(subMesh.colorData.AllocatedSize).IsEqualTo(subMesh.colorData.Count);
		}

		[Test]
		public async Task SharedTextureOnEveryFaceIsOneFullySizedSubMesh()
		{
			// More faces than the old reservation floor, so an undersized guess shows up as growth.
			var mesh = new Mesh();
			for (int faceIndex = 0; faceIndex < 5000; faceIndex++)
			{
				mesh.CreateFace(
					new Vector3(faceIndex, 0, 0),
					new Vector3(faceIndex + 1, 0, 0),
					new Vector3(faceIndex, 1, 0));
			}

			// The image texture cache is keyed off the pixel buffer, so every test needs its own image.
			var texture = new ImageBuffer(16, 16, 32, new BlenderBGRA());
			texture.SetPixel(1, 1, Color.Green);
			mesh.PlaceTextureOnFaces(Enumerable.Range(0, mesh.Faces.Count), texture, Matrix4X4.Identity);

			var gl = new GL(new RecordingGpuContext(idBase: 46000));

			var plugin = MeshTrianglePlugin.Get(gl, mesh, normal => Color.Red);

			// FaceTextures holds an entry per textured face, not per image, so sizing off its count
			// used to reserve almost nothing here and hand the whole fill back to VectorPOD's growth.
			await Assert.That(plugin.subMeshs.Count).IsEqualTo(1);
			var subMesh = plugin.subMeshs[0];

			await Assert.That(subMesh.positionData.Count).IsEqualTo(mesh.Faces.Count * 3);
			await Assert.That(subMesh.positionData.AllocatedSize).IsEqualTo(subMesh.positionData.Count);
			await Assert.That(subMesh.normalData.AllocatedSize).IsEqualTo(subMesh.normalData.Count);
			await Assert.That(subMesh.textureData.AllocatedSize).IsEqualTo(subMesh.textureData.Count);
			await Assert.That(subMesh.colorData.AllocatedSize).IsEqualTo(subMesh.colorData.Count);
			await Assert.That(subMesh.interleavedData.Length)
				.IsEqualTo(subMesh.positionData.Count * SubTriangleMesh.InterleavedStride);
		}

		[Test]
		public async Task UntexturedSubMeshLeavesTextureDataEmpty()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var gl = new GL(new RecordingGpuContext(idBase: 42000));

			var subMesh = MeshTrianglePlugin.Get(gl, mesh).subMeshs[0];

			// Every consumer only binds the texture coordinate array when subMesh.texture is set, so
			// filling it for an untextured mesh is 8 bytes a vertex nothing ever reads.
			await Assert.That(subMesh.texture).IsNull();
			await Assert.That(subMesh.textureData.Count).IsEqualTo(0);
		}

		[Test]
		public async Task SubMeshWithoutVertexColorsLeavesColorDataEmpty()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var gl = new GL(new RecordingGpuContext(idBase: 43000));

			// No color func and no face colors, so nothing turns the color array on at draw time.
			var subMesh = MeshTrianglePlugin.Get(gl, mesh).subMeshs[0];

			await Assert.That(subMesh.UseVertexColors).IsFalse();
			await Assert.That(subMesh.colorData.Count).IsEqualTo(0);
		}

		[Test]
		public async Task ColorFuncFillsTheColorArray()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var gl = new GL(new RecordingGpuContext(idBase: 44000));

			var subMesh = MeshTrianglePlugin.Get(gl, mesh, normal => Color.Red).subMeshs[0];

			await Assert.That(subMesh.UseVertexColors).IsTrue();
			await Assert.That(subMesh.colorData.Count).IsEqualTo(mesh.Faces.Count * 3);
			await Assert.That(subMesh.colorData.Array[0].red).IsEqualTo(Color.Red.red);
			await Assert.That(subMesh.colorData.Array[0].green).IsEqualTo(Color.Red.green);
			await Assert.That(subMesh.colorData.Array[0].alpha).IsEqualTo(Color.Red.alpha);
		}

		[Test]
		public async Task TexturedSubMeshKeepsItsTextureCoordinates()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			// The image texture cache is keyed off the pixel buffer, so every test needs its own image.
			var texture = new ImageBuffer(16, 16, 32, new BlenderBGRA());
			texture.SetPixel(1, 1, Color.Yellow);
			mesh.PlaceTextureOnFace(0, texture);

			var gl = new GL(new RecordingGpuContext(idBase: 45000));

			var plugin = MeshTrianglePlugin.Get(gl, mesh);
			var texturedSubMesh = plugin.subMeshs.First(subMesh => ReferenceEquals(subMesh.texture, texture));

			int vertexCount = texturedSubMesh.positionData.Count;
			await Assert.That(texturedSubMesh.textureData.Count).IsEqualTo(vertexCount);

			// The uv the GL path binds and the uv baked into the interleaved upload must be the same.
			for (int vertexIndex = 0; vertexIndex < vertexCount; vertexIndex++)
			{
				int offset = vertexIndex * SubTriangleMesh.InterleavedStride;
				await Assert.That(texturedSubMesh.interleavedData[offset + 6])
					.IsEqualTo(texturedSubMesh.textureData.Array[vertexIndex].textureU);
				await Assert.That(texturedSubMesh.interleavedData[offset + 7])
					.IsEqualTo(texturedSubMesh.textureData.Array[vertexIndex].textureV);
			}

			// A textured mesh splits into several submeshes, so each one's buffers are still exactly
			// as long as they claim to be even though the split is not known up front.
			foreach (var subMesh in plugin.subMeshs)
			{
				await Assert.That(subMesh.interleavedData.Length)
					.IsEqualTo(subMesh.positionData.Count * SubTriangleMesh.InterleavedStride);
			}
		}
	}
}
