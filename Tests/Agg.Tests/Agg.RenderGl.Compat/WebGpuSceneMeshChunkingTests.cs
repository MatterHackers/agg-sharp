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
*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.RenderGl.Scene;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Meshes whose vertex data is larger than the device will put in one buffer. An untextured mesh is a
	/// single submesh holding every face, so at 60 bytes a vertex a 5M face mesh asks for nearly a gigabyte
	/// - well past WebGPU's default 256 MiB <c>maxBufferSize</c>, which the device refuses. The scene
	/// renderer splits such a submesh into chunks of whole triangles and draws one per chunk.
	/// </summary>
	/// <remarks>
	/// Driven through a <see cref="RecordingRenderDevice"/> with a tiny configured limit rather than a
	/// half-gigabyte mesh: the behaviour under test is "split at the device's limit", and the limit is a
	/// parameter.
	/// </remarks>
	public class WebGpuSceneMeshChunkingTests
	{
		private const int Width = 80;

		private const int Height = 60;

		/// <summary>Bytes per vertex of the scene's interleaved format: position, normal, uv, edge hints, colour.</summary>
		private const int SceneVertexStride = SceneEdgeShaderDataPlugin.TotalVertexFloatStride * sizeof(float);

		/// <summary>Bytes per triangle of the scene format - the granularity a chunk may end on.</summary>
		private const int SceneTriangleStride = SceneVertexStride * 3;

		/// <summary>Bytes per triangle of the position-only selection mask format.</summary>
		private const int SelectionTriangleStride = 3 * sizeof(float) * 3;

		/// <summary>
		/// Small enough that a 60 face mesh needs several chunks in both formats, and a comfortable multiple
		/// of every uniform buffer the frame also allocates through the same (now limit-enforcing) device.
		/// </summary>
		private const ulong MaxBufferSize = 1000;

		private const int FaceCount = 60;

		/// <summary>
		/// The heart of it: a submesh that does not fit is uploaded as several buffers, every one of them
		/// within the limit and ending on a triangle boundary, and every vertex is still drawn exactly once
		/// per pass.
		/// </summary>
		[Test]
		public async Task AnOversizedSubMeshIsSplitIntoChunksOfWholeTriangles()
		{
			using var harness = new ChunkingHarness(MaxBufferSize);
			var mesh = CreateFaceStrip(FaceCount);

			harness.RenderFrame(mesh, queueSelectionOutline: false);

			var chunks = harness.MeshVertexBuffers();
			int expectedChunkCount = (FaceCount + 4) / 5; // 1000 bytes holds five 180 byte triangles

			await Assert.That(chunks.Count).IsEqualTo(expectedChunkCount)
				.Because("the submesh is larger than the device's maxBufferSize and has to be split");

			foreach (var chunk in chunks)
			{
				await Assert.That(chunk.SizeInBytes).IsLessThanOrEqualTo(MaxBufferSize);
				await Assert.That(chunk.SizeInBytes % SceneTriangleStride).IsEqualTo(0ul)
					.Because("a chunk boundary anywhere but a triangle boundary would drop the triangle that straddles it");
			}

			// Nothing was lost or duplicated: the chunks hold the whole submesh.
			await Assert.That(chunks.Sum(chunk => (long)chunk.SizeInBytes))
				.IsEqualTo((long)FaceCount * SceneTriangleStride);

			// And every pass that draws this mesh draws all of it, one draw per chunk.
			var drawsByPass = harness.MeshDraws().GroupBy(draw => draw.Pass).ToList();
			await Assert.That(drawsByPass.Count).IsGreaterThan(0);
			foreach (var pass in drawsByPass)
			{
				await Assert.That(pass.Count()).IsEqualTo(expectedChunkCount);
				await Assert.That(pass.Sum(draw => draw.VertexCount)).IsEqualTo(FaceCount * 3);
				foreach (var draw in pass)
				{
					await Assert.That((ulong)draw.VertexCount).IsEqualTo(draw.Buffer.SizeInBytes / SceneVertexStride);
				}
			}
		}

		/// <summary>
		/// The selection mask uploads its own position-only copy of the mesh, on the same rule and with its
		/// own (much smaller) stride - so the chunk boundaries are different but still triangle aligned.
		/// </summary>
		[Test]
		public async Task TheSelectionMaskSplitsItsPositionBuffersToo()
		{
			using var harness = new ChunkingHarness(MaxBufferSize);
			var mesh = CreateFaceStrip(FaceCount);

			harness.RenderFrame(mesh, queueSelectionOutline: true);

			// Told apart from the scene chunks by stride: 36 bytes a triangle rather than 180.
			var selectionChunks = harness.MeshVertexBuffers()
				.Where(buffer => buffer.SizeInBytes % SceneTriangleStride != 0
					|| buffer.SizeInBytes < SceneTriangleStride)
				.ToList();

			// 1000 bytes holds twenty-seven 36 byte triangles, so 60 faces need three chunks.
			await Assert.That(selectionChunks.Count).IsEqualTo(3);
			foreach (var chunk in selectionChunks)
			{
				await Assert.That(chunk.SizeInBytes).IsLessThanOrEqualTo(MaxBufferSize);
				await Assert.That(chunk.SizeInBytes % SelectionTriangleStride).IsEqualTo(0ul);
			}

			await Assert.That(selectionChunks.Sum(chunk => (long)chunk.SizeInBytes))
				.IsEqualTo((long)FaceCount * SelectionTriangleStride);
		}

		/// <summary>
		/// The retention model tracks chunks, not submeshes: an edit has to retire every chunk of the
		/// generation it replaced, or a split mesh leaks all but one buffer per edit.
		/// </summary>
		[Test]
		public async Task AMeshEditRetiresEveryChunkOfThePreviousGeneration()
		{
			using var harness = new ChunkingHarness(MaxBufferSize);
			var mesh = CreateFaceStrip(FaceCount);

			harness.RenderFrame(mesh, queueSelectionOutline: false);
			var firstGeneration = harness.MeshVertexBuffers();
			await Assert.That(firstGeneration.Count).IsGreaterThan(1);

			mesh.MarkAsChanged();
			harness.RenderFrame(mesh, queueSelectionOutline: false);

			await Assert.That(firstGeneration.All(buffer => buffer.IsDisposed)).IsTrue()
				.Because("every chunk of the replaced plugin generation is unreachable and must be disposed");

			await Assert.That(harness.MeshVertexBuffers().Count(buffer => !buffer.IsDisposed))
				.IsEqualTo(firstGeneration.Count)
				.Because("the new generation owns exactly as many live chunks as the old one did");
		}

		/// <summary>
		/// Release - the thumbnail path's one-shot cleanup - has to give back every chunk and leave the
		/// submesh caching none of them, so the next frame mints a fresh set rather than binding disposed
		/// buffers.
		/// </summary>
		[Test]
		public async Task ReleaseAllMeshBuffersFreesEveryChunk()
		{
			using var harness = new ChunkingHarness(MaxBufferSize);
			var mesh = CreateFaceStrip(FaceCount);

			harness.RenderFrame(mesh, queueSelectionOutline: true);
			var firstGeneration = harness.MeshVertexBuffers();
			await Assert.That(firstGeneration.Count).IsGreaterThan(1);

			harness.Renderer.ReleaseAllMeshBuffers();
			await Assert.That(firstGeneration.All(buffer => buffer.IsDisposed)).IsTrue();

			harness.RenderFrame(mesh, queueSelectionOutline: true);
			var secondGeneration = harness.MeshVertexBuffers().Skip(firstGeneration.Count).ToList();
			await Assert.That(secondGeneration.Count).IsEqualTo(firstGeneration.Count);
			await Assert.That(secondGeneration.Any(buffer => buffer.IsDisposed)).IsFalse();
		}

		/// <summary>
		/// A mesh that fits is untouched: one buffer, one draw. The goldens are captured on this path, so a
		/// chunking change that quietly added a second draw to every mesh would show up here first.
		/// </summary>
		[Test]
		public async Task AMeshThatFitsIsStillOneBufferAndOneDraw()
		{
			using var harness = new ChunkingHarness(DeviceLimits.DefaultMaxBufferSize);
			var mesh = CreateFaceStrip(FaceCount);

			harness.RenderFrame(mesh, queueSelectionOutline: false);

			await Assert.That(harness.MeshVertexBuffers().Count).IsEqualTo(1);
			foreach (var pass in harness.MeshDraws().GroupBy(draw => draw.Pass))
			{
				await Assert.That(pass.Count()).IsEqualTo(1);
				await Assert.That(pass.Single().VertexCount).IsEqualTo(FaceCount * 3);
			}
		}

		/// <summary>
		/// A strip of unshared triangles - a mesh with an exact, controllable face count and no texture, so
		/// its render data is one submesh of exactly <paramref name="faceCount"/> triangles.
		/// </summary>
		/// <param name="faceCount">How many triangles to build.</param>
		private static Mesh CreateFaceStrip(int faceCount)
		{
			var mesh = new Mesh();
			for (int face = 0; face < faceCount; face++)
			{
				mesh.CreateFace(
					new Vector3(face, 0, 0),
					new Vector3(face + 1, 0, 0),
					new Vector3(face, 1, face % 2 == 0 ? 1 : -1));
			}

			return mesh;
		}

		/// <summary>A scene renderer over a recording device whose buffer limit the test picks.</summary>
		private sealed class ChunkingHarness : System.IDisposable
		{
			public ChunkingHarness(ulong maxBufferSize)
			{
				this.Device = new RecordingRenderDevice { Limits = new DeviceLimits(maxBufferSize) };
				var target = this.Device.CreateTexture(new TextureDescriptor(
					Width,
					Height,
					TextureFormat.Bgra8Unorm,
					TextureUsage.RenderAttachment | TextureUsage.CopySrc,
					1,
					1,
					"colorTarget"));

				this.context = new GlCompatContext(this.Device);
				this.context.SetRenderTarget(target, null);

				this.gl = new GL(this.context);
				this.Renderer = new WebGpuSceneRenderer(this.context) { OwnerGl = this.gl };
				this.context.SceneRenderer = this.Renderer;
			}

			public RecordingRenderDevice Device { get; }

			public WebGpuSceneRenderer Renderer { get; }

			private readonly GlCompatContext context;

			private readonly GL gl;

			/// <summary>Draws one mesh through a whole scene frame, exactly as a widget would.</summary>
			/// <param name="mesh">The mesh to draw.</param>
			/// <param name="queueSelectionOutline">Also queue the mesh as a selection outline, which is what
			/// reaches the position-only mask path.</param>
			public void RenderFrame(Mesh mesh, bool queueSelectionOutline)
			{
				var viewport = new RectangleDouble(0, 0, Width, Height);
				var world = new WorldView(Width, Height);
				world.Reset();

				var lighting = new LightingData();

				RenderHelper.SetGlContext(this.gl, world, viewport, lighting);
				this.Renderer.BeginSceneRendering(new SceneRenderContext(world, viewport, lighting));

				try
				{
					RenderHelper.Render(this.gl, mesh, Color.Red, Matrix4X4.Identity, RenderTypes.Shaded);

					if (queueSelectionOutline)
					{
						this.Renderer.QueueSelectionOutline(mesh, Color.White, Matrix4X4.Identity);
					}
				}
				finally
				{
					this.Renderer.EndSceneRendering();
					RenderHelper.UnsetGlContext(this.gl);
				}
			}

			/// <summary>
			/// The buffers minted for mesh geometry, oldest first. The usage tells them apart from the
			/// per-draw uniform buffers and from the compat layer's pooled immediate-mode vertices, which
			/// declare <see cref="BufferUsage.CopyDst"/> so they can be rewritten.
			/// </summary>
			public IReadOnlyList<StubBuffer> MeshVertexBuffers()
				=> this.Device.CommandsOf<CreateBufferCommand>()
					.Where(command => command.Usage == BufferUsage.Vertex)
					.Select(command => (StubBuffer)command.Buffer)
					.ToList();

			/// <summary>
			/// Every draw of a mesh vertex buffer, with the pass it was recorded into and the buffer bound at
			/// the time. Fullscreen draws bind no vertex buffer and are not reported.
			/// </summary>
			public IReadOnlyList<(RecordingRenderEncoder Pass, StubBuffer Buffer, int VertexCount)> MeshDraws()
			{
				var meshBuffers = new HashSet<IGpuBuffer>(this.MeshVertexBuffers());
				var bound = new Dictionary<RecordingRenderEncoder, IGpuBuffer>();
				var draws = new List<(RecordingRenderEncoder, StubBuffer, int)>();

				foreach (var command in this.Device.Commands)
				{
					switch (command)
					{
						case SetVertexBufferCommand setVertexBuffer:
							bound[setVertexBuffer.Encoder] = setVertexBuffer.Buffer;
							break;

						case DrawCommand draw
							when bound.TryGetValue(draw.Encoder, out var buffer) && meshBuffers.Contains(buffer):
							draws.Add((draw.Encoder, (StubBuffer)buffer, draw.VertexCount));
							break;
					}
				}

				return draws;
			}

			public void Dispose()
			{
				this.Renderer.Dispose();
				this.context.Dispose();
				this.Device.Dispose();
			}
		}
	}
}
