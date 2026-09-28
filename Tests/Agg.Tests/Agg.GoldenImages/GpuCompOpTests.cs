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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// <see cref="ICompOpGraphics"/> on <c>Graphics2DGpu</c> against a real device: on a transparent target, a
	/// source composited through each operator over a translucent destination comes out as
	/// <see cref="BlenderCompOpBGRA"/> computes it at the same cover - inside, at anti-aliased edges, over the
	/// destination and over nothing.
	/// </summary>
	/// <remarks>
	/// The GPU's anti-aliasing is not AGG's, so the cover of each pixel is measured on the device (the same
	/// geometry drawn opaque white) and handed to the software blender; what is compared is the operator and
	/// its cover handling, within 2 per channel.
	/// </remarks>
	[NotInParallel]
	public class GpuCompOpTests
	{
		private const int Width = 200;
		private const int Height = 100;

		private static readonly Color Destination = new Color(200, 100, 50, 160);
		private static readonly Color Source = new Color(40, 180, 220, 120);

		/// <summary>The destination: a translucent rectangle on whole pixels, covering x 0-120.</summary>
		private static readonly IVertexSource DestinationShape = new RoundedRect(0, 0, 120, Height, 0);

		public static CompOp[] AllOperators() => (CompOp[])Enum.GetValues(typeof(CompOp));

		[Test]
		[MethodDataSource(nameof(AllOperators))]
		public async Task OperatorMatchesSoftwareBlenderAtEveryCover(CompOp op)
		{
			// Crosses the destination's right edge, so its anti-aliased rim lies over the destination and over nothing.
			var shape = new Ellipse(120, 50, 40.3, 40.3, 100);
			await AssertMatchesSoftware(op, Source, g => g.Render(shape, Source), g => g.Render(shape, Color.White), new[] { 50, 70, 89 });
		}

		[Test]
		[Arguments(CompOp.Xor)]
		[Arguments(CompOp.Difference)]
		[Arguments(CompOp.SrcIn)]
		public async Task RingsMeetTheDestinationOnce(CompOp op)
		{
			// Concentric rings, as compositing2 draws its gradients: their anti-aliased seams overlap, and an
			// operator applied ring by ring would go through the seams twice. Opaque, so the layer's coverage at a
			// seam is what the white measurement sees.
			var opaque = new Color(40, 180, 220, 255);
			await AssertMatchesSoftware(op, opaque, g => DrawRings(g, opaque), g => DrawRings(g, Color.White), new[] { 50, 57 });
		}

		[Test]
		public async Task DrawingAfterwardsIsSourceOver()
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			var graphics = capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0));
			graphics.Render(DestinationShape, Destination);
			((ICompOpGraphics)graphics).DrawWithCompOp(CompOp.Xor, () => graphics.Render(new RoundedRect(80, 20, 110, 80, 0), Source));
			graphics.Render(new RoundedRect(185, 20, 195, 80, 0), Source);

			var image = await capture.CaptureAsync();
			await AssertNear(image, 190, 50, Expected(CompOp.SrcOver, null, Source, 255), "source-over afterwards");
			await AssertNear(image, 50, 50, Expected(CompOp.SrcOver, Destination, null, 255), "destination untouched");
		}

		private static void DrawRings(Graphics2D graphics, Color color)
		{
			const int Rings = 12;
			for (int i = 0; i < Rings; i++)
			{
				var ring = new VertexStorage();
				ring.ConcatPath(new Ellipse(100, 50, 40.0 * (i + 1) / Rings, 40.0 * (i + 1) / Rings, 100));
				if (i > 0)
				{
					ring.ConcatPath(new Ellipse(100, 50, 40.0 * i / Rings, 40.0 * i / Rings, 100, cw: true));
				}

				graphics.Render(ring, color);
			}
		}

		/// <summary>
		/// Draws the destination, then <paramref name="drawSource"/> through <paramref name="op"/>, and checks every
		/// pixel of <paramref name="rows"/> against the software blender at the cover <paramref name="drawCover"/>
		/// leaves in an opaque-white draw of the same geometry.
		/// </summary>
		private static async Task AssertMatchesSoftware(CompOp op, Color source, Action<Graphics2D> drawSource, Action<Graphics2D> drawCover, int[] rows)
		{
			ImageBuffer cover = await Capture(drawCover);

			// The destination as the device draws it: its anti-aliased rim is the device's, not AGG's.
			ImageBuffer destination = await Capture(g => g.Render(DestinationShape, Destination));

			ImageBuffer image = await Capture(graphics =>
			{
				var compositing = (ICompOpGraphics)graphics;
				if (!compositing.SupportsCompOp(op))
				{
					throw new InvalidOperationException($"{op} is not supported.");
				}

				graphics.Render(DestinationShape, Destination);
				compositing.DrawWithCompOp(op, () => drawSource(graphics));
			});

			foreach (int y in rows)
			{
				for (int x = 0; x < Width; x++)
				{
					int c = cover.GetBuffer()[cover.GetBufferOffsetXY(x, y) + ImageBuffer.OrderA];
					var expected = new byte[4];
					Array.Copy(destination.GetBuffer(), destination.GetBufferOffsetXY(x, y), expected, 0, 4);
					BlenderCompOpBGRA.BlendPix(op, expected, 0, source, c);
					await AssertNear(image, x, y, expected, $"{op} at ({x}, {y}), cover {c}");
				}
			}
		}

		private static async Task<ImageBuffer> Capture(Action<Graphics2D> draw)
		{
			using var capture = WebGpuOffscreenCapture.Create(Width, Height);
			draw(capture.BeginWidgetFrame(new ColorF(0, 0, 0, 0)));
			return await capture.CaptureAsync();
		}

		/// <summary>
		/// The premultiplied BGRA pixel software gets: a transparent pixel, <paramref name="destination"/> drawn
		/// source-over, then <paramref name="source"/> through <paramref name="op"/> at <paramref name="cover"/>.
		/// </summary>
		private static byte[] Expected(CompOp op, Color? destination, Color? source, int cover)
		{
			var pixel = new byte[4];
			if (destination.HasValue)
			{
				BlenderCompOpBGRA.BlendPix(CompOp.SrcOver, pixel, 0, destination.Value, 255);
			}

			if (source.HasValue)
			{
				BlenderCompOpBGRA.BlendPix(op, pixel, 0, source.Value, cover);
			}

			return pixel;
		}

		private static async Task AssertNear(ImageBuffer image, int x, int y, byte[] expected, string where)
		{
			byte[] buffer = image.GetBuffer();
			int offset = image.GetBufferOffsetXY(x, y);
			for (int i = 0; i < 4; i++)
			{
				if (Math.Abs(buffer[offset + i] - expected[i]) > 2)
				{
					string actual = $"{buffer[offset]},{buffer[offset + 1]},{buffer[offset + 2]},{buffer[offset + 3]}";
					string wanted = $"{expected[0]},{expected[1]},{expected[2]},{expected[3]}";
					await Assert.That(Math.Abs(buffer[offset + i] - expected[i])).IsLessThanOrEqualTo(2)
						.Because($"{where}: GPU {actual} vs software {wanted} (BGRA)");
				}
			}
		}
	}
}
