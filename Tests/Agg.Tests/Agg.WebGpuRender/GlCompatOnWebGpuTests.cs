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

using System;
using System.Threading.Tasks;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The Phase 2 integration milestone: the whole Phase 1 stack - <see cref="GlCompatContext"/>, its
	/// accumulators, pipeline cache, pass scope and texture store - running on a real
	/// <c>WebGpuRenderDevice</c> and producing pixels. Everything below has been asserted headlessly
	/// against <c>RecordingRenderDevice</c>; these tests are what prove the command stream those tests
	/// describe is also a command stream a GPU accepts.
	/// <para>
	/// <b>Coordinates.</b> GL measures y up from the bottom and the readback rows run top down, so a quad
	/// drawn at GL y 16..48 of a 64 pixel target is read back at rows 16..48 counted from the top
	/// (64 - 48 to 64 - 16). Getting that backwards is exactly the mistake the removed D3D11 y-flip could
	/// have introduced, so the assertions are deliberately asymmetric - a vertically mirrored render
	/// fails them.
	/// </para>
	/// </summary>
	[NotInParallel]
	public class GlCompatOnWebGpuTests
	{
		private const uint Size = 64;

		private const int ColorBufferBit = 0x00004000;

		private const int GlNearest = 9728;

		private const int GlRgba = 0x1908;

		[Test]
		public async Task AnImmediateModeColoredQuadReachesTheGpu()
		{
			using (var harness = WebGpuRenderTestHarness.Create(Size, Size))
			using (var context = new GlCompatContext(harness.Device))
			{
				SetUpTarget(harness, context);

				context.ClearColor(0, 0, 1, 1);
				context.Clear(ColorBufferBit);

				context.Color4(255, 0, 0, 255);
				context.Begin(BeginMode.TriangleStrip);
				context.Vertex2(16, 16);
				context.Vertex2(48, 16);
				context.Vertex2(16, 48);
				context.Vertex2(48, 48);
				context.End();

				context.Submit();

				var image = await harness.ReadAsync();

				await Assert.That(image.PixelAt(32, 32)).IsEqualTo(ReadbackImage.Rgba(255, 0, 0, 255));
				await Assert.That(image.PixelAt(2, 2)).IsEqualTo(ReadbackImage.Rgba(0, 0, 255, 255));
				await Assert.That(image.PixelAt(32, 60)).IsEqualTo(ReadbackImage.Rgba(0, 0, 255, 255));
				await Assert.That(harness.Device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		public async Task ATexturedQuadSamplesTheUploadedTexelsInTheRightPlaces()
		{
			// A 2x2 texture with four distinguishable texels, uploaded first row first. Mapping it over a
			// quad with v = 0 at the bottom (GL's convention) puts the first uploaded row at the bottom of
			// the screen, which is what the classic D3D11 path does too - so a flipped or swizzled sample
			// changes which corner is which and the test says which.
			var texels = new byte[]
			{
				255, 0, 0, 255,     0, 255, 0, 255,       // row 0: red, green
				0, 0, 255, 255,     255, 255, 255, 255,   // row 1: blue, white
			};

			using (var harness = WebGpuRenderTestHarness.Create(Size, Size))
			using (var context = new GlCompatContext(harness.Device))
			{
				SetUpTarget(harness, context);

				context.ClearColor(0, 0, 0, 1);
				context.Clear(ColorBufferBit);

				int texture = context.GenTexture();
				context.BindTexture(0, texture);
				context.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, GlNearest);
				context.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, GlNearest);
				context.TexImage2D(0, 0, 0, 2, 2, 0, GlRgba, 0, texels);
				context.Enable((int)EnableCap.Texture2D);

				// White vertices so GL_MODULATE leaves the texel alone; the replace path is a flag in the
				// same shader and is not what this test is about.
				context.Color4(255, 255, 255, 255);
				context.Begin(BeginMode.TriangleStrip);
				context.TexCoord2(0, 0);
				context.Vertex2(16, 16);
				context.TexCoord2(1, 0);
				context.Vertex2(48, 16);
				context.TexCoord2(0, 1);
				context.Vertex2(16, 48);
				context.TexCoord2(1, 1);
				context.Vertex2(48, 48);
				context.End();

				context.Submit();

				var image = await harness.ReadAsync();

				// Device rows 32..48 are GL y 16..32, the bottom half of the quad, where v is near 0.
				await Assert.That(image.PixelAt(24, 40)).IsEqualTo(ReadbackImage.Rgba(255, 0, 0, 255));
				await Assert.That(image.PixelAt(40, 40)).IsEqualTo(ReadbackImage.Rgba(0, 255, 0, 255));
				await Assert.That(image.PixelAt(24, 24)).IsEqualTo(ReadbackImage.Rgba(0, 0, 255, 255));
				await Assert.That(image.PixelAt(40, 24)).IsEqualTo(ReadbackImage.Rgba(255, 255, 255, 255));
				await Assert.That(image.PixelAt(2, 2)).IsEqualTo(ReadbackImage.Rgba(0, 0, 0, 255));
				await Assert.That(harness.Device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		public async Task TheScissorClipsDrawingButNotTheClear()
		{
			using (var harness = WebGpuRenderTestHarness.Create(Size, Size))
			using (var context = new GlCompatContext(harness.Device))
			{
				SetUpTarget(harness, context);

				context.ClearColor(0, 0, 1, 1);
				context.Clear(ColorBufferBit);

				// GL's left half. The clear runs as the pass load op, which no scissor applies to, so the
				// right half must still come back blue rather than untouched garbage.
				context.Enable((int)EnableCap.ScissorTest);
				context.Scissor(0, 0, 32, (int)Size);

				context.Color4(255, 0, 0, 255);
				context.Begin(BeginMode.TriangleStrip);
				context.Vertex2(0, 0);
				context.Vertex2(Size, 0);
				context.Vertex2(0, Size);
				context.Vertex2(Size, Size);
				context.End();

				context.Submit();

				var image = await harness.ReadAsync();

				await Assert.That(image.PixelAt(8, 32)).IsEqualTo(ReadbackImage.Rgba(255, 0, 0, 255));
				await Assert.That(image.PixelAt(56, 32)).IsEqualTo(ReadbackImage.Rgba(0, 0, 255, 255));
				await Assert.That(harness.Device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		public async Task ADisplayListReplaysItsBakedGeometry()
		{
			using (var harness = WebGpuRenderTestHarness.Create(Size, Size))
			using (var context = new GlCompatContext(harness.Device))
			{
				SetUpTarget(harness, context);

				int list = context.GenLists(1);
				context.NewList(list, null);
				context.Color4(0, 255, 0, 255);
				context.Begin(BeginMode.TriangleStrip);
				context.Vertex2(8, 8);
				context.Vertex2(24, 8);
				context.Vertex2(8, 24);
				context.Vertex2(24, 24);
				context.End();
				context.EndList();

				context.ClearColor(0, 0, 1, 1);
				context.Clear(ColorBufferBit);

				// Nothing was drawn while the list was recording, so a blue frame here would mean the
				// replay did nothing - and the baked buffer takes the create-with-initial-data path, which
				// no other test covers.
				context.CallList(list);
				context.Submit();

				var image = await harness.ReadAsync();

				await Assert.That(image.PixelAt(16, (int)Size - 16)).IsEqualTo(ReadbackImage.Rgba(0, 255, 0, 255));
				await Assert.That(image.PixelAt(48, 16)).IsEqualTo(ReadbackImage.Rgba(0, 0, 255, 255));
				await Assert.That(harness.Device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		public async Task TwoDrawsInOnePassKeepTheirOwnUniformsAndPipelines()
		{
			// The finding that shapes the whole buffer model: queue writes are ordered against submits,
			// not against the draws in an open pass, so two draws in one pass that shared a uniform buffer
			// would both read the second write. The compat layer pools one buffer per draw; if that ever
			// regressed, both quads would land in the same place and the first assertion would fail.
			using (var harness = WebGpuRenderTestHarness.Create(Size, Size))
			using (var context = new GlCompatContext(harness.Device))
			{
				SetUpTarget(harness, context);

				context.ClearColor(0, 0, 0, 1);
				context.Clear(ColorBufferBit);

				context.Color4(255, 0, 0, 255);
				DrawUnitQuad(context);

				context.Translate(32, 32, 0);
				context.Color4(0, 255, 0, 255);
				DrawUnitQuad(context);

				context.Submit();

				var image = await harness.ReadAsync();

				// First quad at GL 4..20, second translated to GL 36..52.
				await Assert.That(image.PixelAt(12, (int)Size - 12)).IsEqualTo(ReadbackImage.Rgba(255, 0, 0, 255));
				await Assert.That(image.PixelAt(44, (int)Size - 44)).IsEqualTo(ReadbackImage.Rgba(0, 255, 0, 255));
				await Assert.That(context.Passes.PassOpenCount).IsEqualTo(1);
				await Assert.That(harness.Device.LastUncapturedError).IsNull();
			}
		}

		private static void DrawUnitQuad(GlCompatContext context)
		{
			context.Begin(BeginMode.TriangleStrip);
			context.Vertex2(4, 4);
			context.Vertex2(20, 4);
			context.Vertex2(4, 20);
			context.Vertex2(20, 20);
			context.End();
		}

		/// <summary>
		/// Points the context at the harness target and sets up a pixel-for-pixel orthographic projection,
		/// the same shape <c>Graphics2DGpu</c> uses for the 2D UI.
		/// </summary>
		/// <param name="harness">The device and target.</param>
		/// <param name="context">The context to set up.</param>
		private static void SetUpTarget(WebGpuRenderTestHarness harness, GlCompatContext context)
		{
			context.SetRenderTarget(harness.Target);
			context.Viewport(0, 0, (int)harness.Width, (int)harness.Height);
			context.MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode.Projection);
			context.LoadIdentity();
			context.Ortho(0, harness.Width, 0, harness.Height, -1, 1);
			context.MatrixMode(MatterHackers.RenderGl.OpenGl.MatrixMode.Modelview);
			context.LoadIdentity();
		}
	}
}
