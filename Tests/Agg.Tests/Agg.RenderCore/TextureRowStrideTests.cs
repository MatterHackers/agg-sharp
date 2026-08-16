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
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// WebGPU pads every texture-copy row out to 256 bytes. Getting that arithmetic wrong does not
	/// throw - it shears the readback image by a few pixels per row, which is exactly the kind of bug
	/// that survives a casual look at a screenshot. These cases cover widths that land on the
	/// alignment and widths that do not.
	/// </summary>
	public class TextureRowStrideTests
	{
		[Test]
		[Arguments(TextureFormat.Rgba8Unorm, 64u, 256u)] // exactly one alignment unit: no padding
		[Arguments(TextureFormat.Rgba8Unorm, 65u, 512u)] // 260 tight bytes round up to two units
		[Arguments(TextureFormat.Rgba8Unorm, 1u, 256u)] // a single pixel still costs a full row
		[Arguments(TextureFormat.Rgba8Unorm, 128u, 512u)] // exactly two units
		[Arguments(TextureFormat.Rgba8Unorm, 100u, 512u)] // 400 tight bytes
		[Arguments(TextureFormat.Bgra8Unorm, 1920u, 7680u)] // a real window width happens to align
		[Arguments(TextureFormat.R8Unorm, 256u, 256u)] // one byte per pixel, aligned
		[Arguments(TextureFormat.R8Unorm, 300u, 512u)] // one byte per pixel, not aligned
		[Arguments(TextureFormat.Rg32Float, 32u, 256u)] // depth peeling target, aligned
		[Arguments(TextureFormat.Rg32Float, 33u, 512u)] // depth peeling target, padded
		public async Task AlignedRowStrideRoundsUpToTheCopyAlignment(TextureFormat format, uint width, uint expectedStride)
		{
			await Assert.That(TextureFormatInfo.AlignedRowStride(format, width)).IsEqualTo(expectedStride);
		}

		[Test]
		public async Task TightRowStrideIsUnpadded()
		{
			// The value a naive caller would assume - kept separate so the padding is visible as a diff.
			await Assert.That(TextureFormatInfo.TightRowStride(TextureFormat.Rgba8Unorm, 65)).IsEqualTo(260u);
			await Assert.That(TextureFormatInfo.TightRowStride(TextureFormat.R8Unorm, 300)).IsEqualTo(300u);
		}

		[Test]
		public async Task ReadTextureReportsThePaddedStrideAndZeroFillsTheDestination()
		{
			var device = new RecordingRenderDevice();
			var texture = device.CreateTexture(new TextureDescriptor(65, 4, TextureFormat.Rgba8Unorm, TextureUsage.CopySrc | TextureUsage.RenderAttachment));

			var destination = new byte[512 * 4];
			for (int i = 0; i < destination.Length; i++)
			{
				destination[i] = 0xCD;
			}

			var result = await device.ReadTextureAsync(texture, destination);

			await Assert.That(result.RowStride).IsEqualTo(512u);
			await Assert.That(result.Width).IsEqualTo(65u);
			await Assert.That(result.Height).IsEqualTo(4u);
			await Assert.That(result.TotalBytes).IsEqualTo(2048ul);
			await Assert.That(destination[0]).IsEqualTo((byte)0);
			await Assert.That(destination[destination.Length - 1]).IsEqualTo((byte)0);
		}

		[Test]
		public async Task ReadTextureRejectsADestinationSizedForTightlyPackedRows()
		{
			// The mistake this guards against: sizing the buffer width * height * bpp and assuming the
			// rows come back tightly packed.
			var device = new RecordingRenderDevice();
			var texture = device.CreateTexture(new TextureDescriptor(65, 4, TextureFormat.Rgba8Unorm, TextureUsage.CopySrc));
			var tightlyPacked = new byte[65 * 4 * 4];

			await Assert.That(async () => await device.ReadTextureAsync(texture, tightlyPacked))
				.Throws<ArgumentException>();
		}

		[Test]
		public async Task ReadTextureIsRefusedWhileAPassIsOpen()
		{
			// WebGPU forbids readback inside a pass; this is what forces the FlushPass pattern above
			// the seam, so the test double has to enforce it or the pattern never gets exercised.
			var device = new RecordingRenderDevice();
			var target = device.CreateTexture(new TextureDescriptor(64, 64, TextureFormat.Bgra8Unorm, TextureUsage.RenderAttachment | TextureUsage.CopySrc));

			using (device.BeginRenderPass(new RenderPassDescriptor(target, LoadOp.Clear, ClearColor.Black)))
			{
				await Assert.That(async () => await device.ReadTextureAsync(target, new byte[256 * 64]))
					.Throws<InvalidOperationException>();
			}

			// Once the pass has ended the same read is fine.
			var result = await device.ReadTextureAsync(target, new byte[256 * 64]);
			await Assert.That(result.RowStride).IsEqualTo(256u);
		}
	}
}
