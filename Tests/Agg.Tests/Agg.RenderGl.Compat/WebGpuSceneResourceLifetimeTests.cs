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
	/// What the scene renderer and the pipeline cache do with device objects that outlive a frame: the
	/// vertex buffers a mesh's render-data plugin minted, and the bind groups built over textures that
	/// are then destroyed. Both are caches that nothing else evicts, so both are places a long-running
	/// session leaks GPU memory one edit or one resize at a time.
	/// </summary>
	public class WebGpuSceneResourceLifetimeTests
	{
		private const int Width = 80;

		private const int Height = 60;

		/// <summary>
		/// Editing a mesh replaces its <see cref="SceneEdgeShaderDataPlugin"/>, and the vertex buffer the
		/// old one cached can never be reached again. The renderer has to retire it rather than hold it
		/// until the context closes - which is what it used to do, one buffer per submesh per edit.
		/// </summary>
		[Test]
		public async Task AMeshEditRetiresThePreviousGenerationsVertexBuffer()
		{
			var device = new RecordingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(
				Width,
				Height,
				TextureFormat.Bgra8Unorm,
				TextureUsage.RenderAttachment | TextureUsage.CopySrc,
				1,
				1,
				"colorTarget"));

			using var context = new GlCompatContext(device);
			context.SetRenderTarget(target, null);

			var gl = new GL(context);
			using var renderer = new WebGpuSceneRenderer(context) { OwnerGl = gl };
			context.SceneRenderer = renderer;

			// One submesh, because it carries no face textures - so "one live buffer per mesh" is
			// literally one buffer.
			var mesh = PlatonicSolids.CreateCube(20, 20, 20);

			RenderFrame(gl, renderer, mesh);
			var firstGeneration = MeshVertexBuffers(device);
			await Assert.That(firstGeneration.Count).IsEqualTo(1);
			await Assert.That(firstGeneration[0].IsDisposed).IsFalse();

			mesh.MarkAsChanged();

			RenderFrame(gl, renderer, mesh);
			var allBuffers = MeshVertexBuffers(device);

			// The edit rebuilt the plugin, so a second buffer was minted...
			await Assert.That(allBuffers.Count).IsEqualTo(2);

			// ... the first one was retired with the plugin that owned it, and released once the frame
			// that could still have been referencing it had been submitted ...
			await Assert.That(allBuffers[0].IsDisposed).IsTrue()
				.Because("the buffer of the plugin generation the edit replaced is unreachable and must be disposed");

			// ... and exactly one buffer for this mesh is still alive.
			await Assert.That(allBuffers.Count(buffer => !buffer.IsDisposed)).IsEqualTo(1);
		}

		/// <summary>
		/// A bind group holds the textures it binds, so the cache has to drop the groups built over a
		/// texture that is being destroyed - on a resize, that is every group of the previous generation.
		/// </summary>
		[Test]
		public async Task InvalidatingATextureDisposesTheBindGroupsThatBindIt()
		{
			var device = new RecordingRenderDevice();
			using var cache = new GlPipelineCache(device);

			var module = cache.GetShaderModule(GlShaderKeys.ModuleKey(true, false));
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				GlShaderKeys.VertexEntryPoint,
				module,
				GlShaderKeys.FragmentEntryPoint(false),
				new[] { GlShaderKeys.VertexLayout(true, false) },
				new[] { new ColorTargetState(TextureFormat.Bgra8Unorm) },
				GlShaderKeys.BindGroupLayout(true)));

			var doomedTexture = device.CreateTexture(new TextureDescriptor(
				16, 16, TextureFormat.Bgra8Unorm, TextureUsage.TextureBinding, 1, 1, "doomed"));
			var survivingTexture = device.CreateTexture(new TextureDescriptor(
				16, 16, TextureFormat.Bgra8Unorm, TextureUsage.TextureBinding, 1, 1, "surviving"));
			var sampler = device.CreateSampler(new SamplerDescriptor());

			BindGroupDescriptor Descriptor(IGpuTexture texture)
				=> new BindGroupDescriptor(
					pipeline,
					0,
					new[] { BindGroupEntry.ForSampler(0, sampler), BindGroupEntry.ForTexture(1, texture) },
					"test");

			var doomedGroup = cache.GetBindGroup(Descriptor(doomedTexture));
			var survivingGroup = cache.GetBindGroup(Descriptor(survivingTexture));
			await Assert.That(cache.BindGroupCount).IsEqualTo(2);

			await Assert.That(cache.InvalidateBindGroupsUsing(doomedTexture)).IsEqualTo(1);

			await Assert.That(((StubBindGroup)doomedGroup).IsDisposed).IsTrue();
			await Assert.That(((StubBindGroup)survivingGroup).IsDisposed).IsFalse()
				.Because("only the groups that bind the destroyed texture may be evicted");

			// The evicted key is gone from the cache rather than left pointing at a disposed object, so an
			// identical request builds a new group.
			var rebuiltGroup = cache.GetBindGroup(Descriptor(doomedTexture));
			await Assert.That(rebuiltGroup).IsNotSameReferenceAs(doomedGroup);
			await Assert.That(cache.GetBindGroup(Descriptor(survivingTexture))).IsSameReferenceAs(survivingGroup);
		}

		/// <summary>Draws one mesh through a whole scene frame, exactly as a widget would.</summary>
		/// <param name="gl">The facade the scene renderer is keyed on.</param>
		/// <param name="renderer">The renderer under test.</param>
		/// <param name="mesh">The mesh to draw.</param>
		private static void RenderFrame(GL gl, WebGpuSceneRenderer renderer, Mesh mesh)
		{
			var viewport = new RectangleDouble(0, 0, Width, Height);
			var world = new WorldView(Width, Height);
			world.Reset();

			// A fresh LightingData per frame: SetGlContext normalises LightDirection0 in place.
			var lighting = new LightingData();

			RenderHelper.SetGlContext(gl, world, viewport, lighting);
			renderer.BeginSceneRendering(new SceneRenderContext(world, viewport, lighting));

			try
			{
				RenderHelper.Render(gl, mesh, Color.Red, Matrix4X4.Identity, RenderTypes.Shaded);
			}
			finally
			{
				renderer.EndSceneRendering();
				RenderHelper.UnsetGlContext(gl);
			}
		}

		/// <summary>
		/// The buffers minted for mesh geometry, oldest first. The scene vertex stride tells them apart
		/// from the per-draw uniform buffers and from the compat layer's pooled immediate-mode vertices,
		/// which are created with <see cref="BufferUsage.CopyDst"/> so they can be rewritten.
		/// </summary>
		/// <param name="device">The device every creation was recorded on.</param>
		private static IReadOnlyList<StubBuffer> MeshVertexBuffers(RecordingRenderDevice device)
			=> device.CommandsOf<CreateBufferCommand>()
				.Where(command => command.Usage == BufferUsage.Vertex)
				.Select(command => (StubBuffer)command.Buffer)
				.ToList();
	}
}
