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
	/// Per-draw uniform data is staged in a CPU array and flushed to one big GPU buffer with a single
	/// queue write per submit. The reason is measured, not aesthetic: wgpuQueueWriteBuffer costs ~13 us
	/// per call in wgpu-native, and a busy MatterCAD frame made ~2,650 of them - 35 ms of a 64 ms frame.
	/// These tests pin the property that pays for that: the number of queue writes must be a function of
	/// the number of submits, not of the number of draws.
	/// </summary>
	public class UniformWriteBatchingTests
	{
		private const int Width = 80;

		private const int Height = 60;

		[Test]
		public async Task TwoDDrawsShareOneUniformWritePerSubmit()
		{
			var harness = GlCompatTestHarness.Create();

			for (int i = 0; i < 8; i++)
			{
				harness.Context.Translate(1, 0, 0);
				harness.DrawTriangle();
			}

			harness.Context.Submit();

			await Assert.That(harness.UniformWrites().Count).IsEqualTo(1)
				.Because("eight draws must cost one queue write, not eight");

			// And the blocks are still per draw: the eighth draw's model view carries all eight translates.
			await Assert.That(harness.UniformModelView(7).Row3.X).IsEqualTo(8.0).Within(1e-5);
			await Assert.That(harness.UniformModelView(0).Row3.X).IsEqualTo(1.0).Within(1e-5);
		}

		[Test]
		public async Task TwoDDrawsShareOneVertexWritePerSubmit()
		{
			var harness = GlCompatTestHarness.Create();

			const int drawCount = 8;
			for (int i = 0; i < drawCount; i++)
			{
				// A different red per draw, so the assertion below can prove each batch's bytes really
				// landed at the offset that draw binds rather than merely being present somewhere.
				harness.Context.Color4((byte)(10 * (i + 1)), 0, 0, 255);
				harness.DrawTriangle();
			}

			harness.Context.Submit();

			await Assert.That(harness.VertexWrites().Count).IsEqualTo(1)
				.Because("eight batches must cost one queue write, not eight");

			// Sharing a write does not mean sharing a range: a queue write is ordered against the submit,
			// not against the draws in an open pass, so two draws in one window must never overlap.
			var offsets = harness.Device.CommandsOf<SetVertexBufferCommand>()
				.Select(command => command.Offset)
				.ToList();
			await Assert.That(offsets.Distinct().Count()).IsEqualTo(drawCount);

			int batchBytes = 3 * (int)GlShaderKeys.ColoredVertexLayout.ArrayStride;
			for (int i = 0; i < drawCount; i++)
			{
				byte[] bytes = harness.VertexBytesForDraw(i, batchBytes);
				await Assert.That(GlCompatTestHarness.ColoredVertexRed(bytes, 0))
					.IsEqualTo(10 * (i + 1) / 255f).Within(1e-6f);
			}
		}

		[Test]
		public async Task SceneUniformWritesDoNotGrowWithTheNumberOfDraws()
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

			var meshes = Enumerable.Range(0, 12)
				.Select(index => PlatonicSolids.CreateCube(10 + index, 10, 10))
				.ToList();

			// The first frames also mint resources and warm caches, so measure a steady state frame of each
			// size: what is under test is the slope against draw count, not the intercept.
			RenderFrame(gl, renderer, meshes.Take(2).ToList());
			RenderFrame(gl, renderer, meshes.Take(2).ToList());
			device.ClearRecording();
			RenderFrame(gl, renderer, meshes.Take(2).ToList());
			int writesForTwo = UniformWrites(device);

			RenderFrame(gl, renderer, meshes);
			device.ClearRecording();
			RenderFrame(gl, renderer, meshes);
			int writesForTwelve = UniformWrites(device);

			await Assert.That(writesForTwelve).IsEqualTo(writesForTwo)
				.Because("uniform queue writes are per submit, so six times the draws must cost the same");
		}

		private static int UniformWrites(RecordingRenderDevice device)
			=> device.CommandsOf<WriteBufferCommand>()
				.Count(command => (command.Buffer.Usage & BufferUsage.Uniform) != 0);

		/// <summary>Draws meshes through a whole scene frame, exactly as a widget would.</summary>
		/// <param name="gl">The facade the scene renderer is keyed on.</param>
		/// <param name="renderer">The renderer under test.</param>
		/// <param name="meshes">The meshes to draw, one command each.</param>
		private static void RenderFrame(GL gl, WebGpuSceneRenderer renderer, IReadOnlyList<Mesh> meshes)
		{
			var viewport = new RectangleDouble(0, 0, Width, Height);
			var world = new WorldView(Width, Height);
			world.Reset();

			var lighting = new LightingData();

			RenderHelper.SetGlContext(gl, world, viewport, lighting);
			renderer.BeginSceneRendering(new SceneRenderContext(world, viewport, lighting));

			try
			{
				foreach (var mesh in meshes)
				{
					RenderHelper.Render(gl, mesh, Color.Red, Matrix4X4.Identity, RenderTypes.Shaded);
				}
			}
			finally
			{
				renderer.EndSceneRendering();
				RenderHelper.UnsetGlContext(gl);
			}
		}
	}
}
