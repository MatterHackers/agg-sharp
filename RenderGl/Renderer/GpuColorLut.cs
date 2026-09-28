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
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="IGammaGraphics"/>. Coverage gamma is <see cref="GpuBlur"/>'s layer
	/// at radius 0 coloured by a table of the colour at each mapped coverage; <see cref="MapChannels"/> copies the
	/// frame and runs <c>GpuColorLut.wgsl</c> over the region, writing back without blending.
	/// </summary>
	/// <remarks>
	/// The tables work on the 8-bit values the target holds, as software's do on its bytes. The target holds
	/// premultiplied colour, which is the same wherever the frame is opaque - the case pixfmt apply_gamma_inv and a
	/// packed format are used for. <see cref="MapChannels"/> reads the target back, so it needs a target that
	/// allows copies (<see cref="TextureUsage.CopySrc"/>), as <see cref="GpuCompOp"/> does.
	/// </remarks>
	internal static class GpuColorLut
	{
		/// <summary>The WGSL module of the pass (the backend's <c>GpuColorLut.wgsl</c>).</summary>
		internal const string ShaderModuleKey = "GpuColorLut";

		private static readonly ConditionalWeakTable<GlCompatContext, Resources> ResourcesByContext = new ConditionalWeakTable<GlCompatContext, Resources>();

		public static void DrawWithCoverageGamma(Graphics2DGpu graphics, IGammaFunction gamma, Color color, Action drawCoverage)
		{
			if (gamma == null)
			{
				throw new ArgumentNullException(nameof(gamma));
			}

			// ScanlineRasterizer's gamma table: each 8-bit cover mapped and rounded, then scaled by the colour's alpha.
			var table = new Color[256];
			for (int i = 0; i < 256; i++)
			{
				int cover = (int)Math.Round(gamma.GetGamma(i / 255.0) * 255.0);
				cover = Math.Max(0, Math.Min(255, cover));
				table[i] = new Color(color, (color.alpha * cover) / 255);
			}

			GpuBlur.Draw(graphics.gl, 0, drawCoverage, table);
		}

		public static void MapChannels(Graphics2DGpu graphics, RectangleDouble region, byte[] red, byte[] green, byte[] blue)
		{
			CheckTable(red, nameof(red));
			CheckTable(green, nameof(green));
			CheckTable(blue, nameof(blue));

			var gl = graphics.gl;
			if (!(gl?.GpuContext is GlCompatContext context) || context.Passes.ColorTarget == null)
			{
				throw new NotSupportedException("MapChannels needs a GPU render target.");
			}

			context.Passes.RejectLinearLight("MapChannels");

			var target = context.Passes.ColorTarget;
			var descriptor = target.Descriptor;
			if ((descriptor.Usage & TextureUsage.CopySrc) == 0)
			{
				throw new NotSupportedException("MapChannels needs a render target that allows copies (CopySrc).");
			}

			var area = DeviceArea(graphics, context, region);
			if (area.Width <= 0 || area.Height <= 0)
			{
				return;
			}

			// Spend any clear queued on the target and end the compat pass: the copy and the pass cannot run inside it.
			context.Passes.EnsurePassOpen();
			context.FlushPass();

			var device = context.Device;
			var resources = ResourcesByContext.GetValue(context, c => c.Own(new Resources()));
			resources.Copy = GpuBlur.Layers.Fit(context, resources.Copy, descriptor, "GpuColorLutCopy", TextureUsage.CopyDst);
			device.CopyTextureToTexture(target, resources.Copy, area.X, area.Y, area.Width, area.Height);

			resources.Table ??= device.CreateTexture(new TextureDescriptor(256, 1, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding | TextureUsage.CopyDst, 1, 1, "GpuColorLutTable"));
			var bytes = new byte[256 * 4];
			for (int i = 0; i < 256; i++)
			{
				bytes[(i * 4) + 0] = red[i];
				bytes[(i * 4) + 1] = green[i];
				bytes[(i * 4) + 2] = blue[i];
				bytes[(i * 4) + 3] = 255;
			}

			device.WriteTexture(resources.Table, bytes, 256 * 4);
			RunPass(context, resources, target, area);

			// The table and the copy are rewritten by the next call; submitting keeps those writes behind this pass.
			context.Submit();
		}

		private static void CheckTable(byte[] table, string name)
		{
			if (table == null || table.Length < 256)
			{
				throw new ArgumentException("A channel table needs an entry for every value, 256.", name);
			}
		}

		/// <summary>The region's device rectangle, through the viewport the ortho projection maps the Graphics2D's pixels onto, inside the scissor.</summary>
		private static (int X, int Y, int Width, int Height) DeviceArea(Graphics2DGpu graphics, GlCompatContext context, RectangleDouble region)
		{
			var descriptor = context.Passes.ColorTarget.Descriptor;
			int scale = context.CoordinateScale;
			var state = context.State;
			GlViewportRect view = state.ViewportSet ? state.Viewport : new GlViewportRect(0, 0, (int)descriptor.Width / scale, (int)descriptor.Height / scale);

			var transform = graphics.GetTransform();
			double x0 = region.Left, y0 = region.Bottom, x1 = region.Right, y1 = region.Top;
			transform.Transform(ref x0, ref y0);
			transform.Transform(ref x1, ref y1);
			double toDeviceX = view.Width * (double)scale / graphics.Width;
			double toDeviceY = view.Height * (double)scale / graphics.Height;
			int left = (int)Math.Round((view.X * scale) + (Math.Min(x0, x1) * toDeviceX));
			int right = (int)Math.Round((view.X * scale) + (Math.Max(x0, x1) * toDeviceX));
			int bottomUp = (int)Math.Round((view.Y * scale) + (Math.Min(y0, y1) * toDeviceY));
			int topUp = (int)Math.Round((view.Y * scale) + (Math.Max(y0, y1) * toDeviceY));
			(int X, int Y, int Width, int Height) area = (left, (int)descriptor.Height - topUp, right - left, topUp - bottomUp);
			if (state.ScissorEnabled)
			{
				area = Intersect(area, context.ToDeviceRect(state.Scissor));
			}

			return Intersect(area, (0, 0, (int)descriptor.Width, (int)descriptor.Height));
		}

		private static (int X, int Y, int Width, int Height) Intersect((int X, int Y, int Width, int Height) a, (int X, int Y, int Width, int Height) b)
		{
			int x0 = Math.Max(a.X, b.X);
			int y0 = Math.Max(a.Y, b.Y);
			int x1 = Math.Min(a.X + a.Width, b.X + b.Width);
			int y1 = Math.Min(a.Y + a.Height, b.Y + b.Height);
			return (x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
		}

		private static void RunPass(GlCompatContext context, Resources resources, IGpuTexture target, (int X, int Y, int Width, int Height) area)
		{
			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
			var replace = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.Zero);
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"fragmentMain",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(target.Descriptor.Format, false, replace, replace) },
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"GpuColorLut"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, resources.Copy),
					BindGroupEntry.ForTexture(1, resources.Table),
				},
				"GpuColorLut"));

			using (var encoder = context.Device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(target, LoadOp.Load) },
				DepthAttachment.None,
				"GpuColorLut")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(area.X, area.Y, area.Width, area.Height);
				encoder.Draw(3);
			}
		}

		/// <summary>One context's frame copy and table, reused from call to call and released with the context.</summary>
		private sealed class Resources : IDisposable
		{
			public IGpuTexture Copy;

			public IGpuTexture Table;

			public void Dispose()
			{
				this.Copy?.Dispose();
				this.Table?.Dispose();
				this.Copy = null;
				this.Table = null;
			}
		}
	}
}
