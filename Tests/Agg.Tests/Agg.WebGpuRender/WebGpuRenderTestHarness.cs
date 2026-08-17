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
using MatterHackers.Agg.Tests.TestingInfrastructure;
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// A real <see cref="WebGpuRenderDevice"/> with an offscreen color target, plus the readback the
	/// pixel assertions need. Every wgpu test in this folder is "draw something, read it back, look at
	/// exact pixels", so the boilerplate lives here once.
	/// <para>
	/// The target is <see cref="TextureFormat.Rgba8Unorm"/> deliberately: with an Rgba8 target a channel
	/// read back as 0 or 255 cannot be a swizzle or a rounding artifact, so a wrong color says which
	/// channel is wrong instead of just "different".
	/// </para>
	/// </summary>
	public sealed class WebGpuRenderTestHarness : IDisposable
	{
		private WebGpuRenderTestHarness(WebGpuRenderDevice device, IGpuTexture target, IGpuTexture depth)
		{
			this.Device = device;
			this.Target = target;
			this.Depth = depth;
		}

		/// <summary>The device under test.</summary>
		public WebGpuRenderDevice Device { get; }

		/// <summary>The color attachment drawing goes to.</summary>
		public IGpuTexture Target { get; }

		/// <summary>The depth attachment, or null.</summary>
		public IGpuTexture Depth { get; }

		/// <summary>Target width in pixels.</summary>
		public uint Width => this.Target.Descriptor.Width;

		/// <summary>Target height in pixels.</summary>
		public uint Height => this.Target.Descriptor.Height;

		/// <summary>
		/// Creates a device on this OS's native backend and an offscreen target.
		/// <para>
		/// The backend is named explicitly (<see cref="TestRenderBackend.Native"/>) rather than left to
		/// wgpu's choice, exactly as the Phase 0 spike does: a machine that silently landed on another
		/// backend would turn a hard failure into a mysterious pixel diff.
		/// </para>
		/// </summary>
		/// <param name="width">Target width in pixels.</param>
		/// <param name="height">Target height in pixels.</param>
		/// <param name="withDepth">Whether to attach a depth buffer.</param>
		public static WebGpuRenderTestHarness Create(uint width = 64, uint height = 64, bool withDepth = false)
		{
			var device = new WebGpuRenderDevice(false, TestRenderBackend.Native, "WebGpuRenderTests");
			try
			{
				var target = device.CreateTexture(new TextureDescriptor(
					width,
					height,
					TextureFormat.Rgba8Unorm,
					TextureUsage.RenderAttachment | TextureUsage.CopySrc,
					1,
					1,
					"colorTarget"));

				IGpuTexture depth = null;
				if (withDepth)
				{
					depth = device.CreateTexture(new TextureDescriptor(
						width,
						height,
						TextureFormat.Depth32Float,
						TextureUsage.RenderAttachment,
						1,
						1,
						"depthTarget"));
				}

				return new WebGpuRenderTestHarness(device, target, depth);
			}
			catch
			{
				device.Dispose();
				throw;
			}
		}

		/// <summary>Reads the color target back, honoring the padded row stride wgpu reports.</summary>
		public async Task<ReadbackImage> ReadAsync()
		{
			uint stride = TextureFormatInfo.AlignedRowStride(this.Target.Descriptor.Format, this.Width);
			var bytes = new byte[stride * this.Height];
			var result = await this.Device.ReadTextureAsync(this.Target, bytes);
			return new ReadbackImage(bytes, result);
		}

		/// <summary>Disposes the textures and the device.</summary>
		public void Dispose()
		{
			this.Depth?.Dispose();
			this.Target?.Dispose();
			this.Device?.Dispose();
		}
	}

	/// <summary>
	/// The bytes a readback produced, addressed by pixel. Rows run top down, which is wgpu's order and
	/// therefore also the order the compat layer's y flip targets - a pixel here is at the same
	/// coordinates a screenshot would show it, not at GL's y-up ones.
	/// </summary>
	public readonly struct ReadbackImage
	{
		private readonly byte[] bytes;
		private readonly TextureReadResult layout;

		/// <summary>Wraps the destination buffer and the layout the read reported.</summary>
		/// <param name="bytes">The bytes filled by the read.</param>
		/// <param name="layout">The size and row stride the read reported.</param>
		public ReadbackImage(byte[] bytes, TextureReadResult layout)
		{
			this.bytes = bytes;
			this.layout = layout;
		}

		/// <summary>Width in pixels.</summary>
		public uint Width => this.layout.Width;

		/// <summary>Height in pixels.</summary>
		public uint Height => this.layout.Height;

		/// <summary>Bytes from one row to the next, padding included.</summary>
		public uint RowStride => this.layout.RowStride;

		/// <summary>
		/// One pixel as "R,G,B,A". A string rather than an array so an assertion failure prints both
		/// colors instead of "arrays differ".
		/// </summary>
		/// <param name="x">Column, from the left.</param>
		/// <param name="y">Row, from the top.</param>
		public string PixelAt(int x, int y)
		{
			int offset = (int)(y * this.layout.RowStride) + (x * 4);
			return $"{this.bytes[offset]},{this.bytes[offset + 1]},{this.bytes[offset + 2]},{this.bytes[offset + 3]}";
		}

		/// <summary>Describes an expected color the same way <see cref="PixelAt"/> describes an actual one.</summary>
		/// <param name="red">Red channel.</param>
		/// <param name="green">Green channel.</param>
		/// <param name="blue">Blue channel.</param>
		/// <param name="alpha">Alpha channel.</param>
		public static string Rgba(byte red, byte green, byte blue, byte alpha) => $"{red},{green},{blue},{alpha}";
	}
}
