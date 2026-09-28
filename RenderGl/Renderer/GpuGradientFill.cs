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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="IGradientFillGraphics"/>: the path is drawn in white into a transparent
	/// coverage layer (the ordinary halo-anti-aliased fill), then <c>GpuGradient.wgsl</c> evaluates span_gradient at
	/// every pixel of the path's bounds, scales the looked-up colour by the coverage and draws it source-over.
	/// </summary>
	/// <remarks>
	/// Not byte-exact against software span_gradient: that one steps span_interpolator_linear's integer subpixels
	/// along each span, the shader transforms each pixel centre in floats, so a pixel on the boundary between two
	/// table entries can take its neighbour.
	/// </remarks>
	internal static class GpuGradientFill
	{
		/// <summary>The WGSL module of the gradient pass (the backend's <c>GpuGradient.wgsl</c>).</summary>
		internal const string ShaderModuleKey = "GpuGradient";

		private const int GradientSize = 80;

		private const int UniformSize = GradientSize * 2;

		private static readonly ConditionalWeakTable<GlCompatContext, Layers> LayersByContext = new ConditionalWeakTable<GlCompatContext, Layers>();

		public static void Fill(Graphics2DGpu graphics, IVertexSource path, GradientFill gradient, GradientFill alphaGradient)
		{
			if (path == null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			if (gradient?.Colors == null || (alphaGradient != null && alphaGradient.Colors == null))
			{
				throw new ArgumentException("A gradient fill needs its colour function.", nameof(gradient));
			}

			// Inside a compositing operator's coverage pass only the cover is wanted, as any other fill gives it.
			if (graphics.CoverageOnly)
			{
				graphics.Render(path, Color.White);
				return;
			}

			var context = GpuCoverageFill.ContextOf(graphics, "FillPathWithGradient");
			var layers = LayersByContext.GetValue(context, c => c.Own(new Layers()));
			if (GpuCoverageFill.DrawCoverage(graphics, context, path, ref layers.Coverage, ref layers.InUse, "GpuGradientCoverage", "FillPathWithGradient", out var pass))
			{
				Shade(context, layers, pass, gradient, alphaGradient);
			}
		}

		private static void Shade(GlCompatContext context, Layers layers, GpuCoverageFill.CoveragePass pass, GradientFill gradient, GradientFill alphaGradient)
		{
			var descriptor = pass.Target.Descriptor;
			var device = context.Device;
			layers.ColorTable = WriteTable(context, layers.ColorTable, gradient.Colors, "GpuGradientColors");
			layers.AlphaTable = alphaGradient == null
				? layers.AlphaTable ?? WriteTable(context, null, gradient.Colors, "GpuGradientAlphas")
				: WriteTable(context, layers.AlphaTable, alphaGradient.Colors, "GpuGradientAlphas");

			var scratch = new byte[UniformSize];
			WriteGradient(scratch.AsSpan(0, GradientSize), gradient, pass.DeviceToScreen);
			if (alphaGradient != null)
			{
				WriteGradient(scratch.AsSpan(GradientSize, GradientSize), alphaGradient, pass.DeviceToScreen);
			}

			layers.Uniform ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			device.WriteBuffer(layers.Uniform, 0, scratch);

			var cache = context.Pipelines;
			var module = cache.GetShaderModule(GlShaderKeys.ForTarget(ShaderModuleKey, context.Passes.LinearLight, true));
			var sourceOver = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha);
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"fragmentMain",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(descriptor.Format, true, sourceOver, sourceOver) },
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 3, ShaderStage.Fragment, BindingType.UniformBuffer),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"GpuGradient"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, layers.Coverage),
					BindGroupEntry.ForTexture(1, layers.ColorTable),
					BindGroupEntry.ForTexture(2, layers.AlphaTable),
					BindGroupEntry.ForBuffer(3, layers.Uniform),
				},
				"GpuGradient"));

			using (var encoder = device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(pass.Target, LoadOp.Load) },
				DepthAttachment.None,
				"GpuGradient")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(pass.X, pass.Y, pass.Width, pass.Height);
				encoder.Draw(3);
			}

			// The uniform, the tables and the coverage layer are rewritten by the next fill; submitting now keeps
			// those writes from overtaking this draw in the queue.
			context.Submit();
		}

		/// <summary>Writes one GpuGradient.wgsl <c>Gradient</c>: device pixel to gradient matrix, range, focus and table size.</summary>
		private static void WriteGradient(Span<byte> destination, GradientFill gradient, Affine deviceToScreen)
		{
			Affine deviceToGradient = deviceToScreen * gradient.ScreenToGradient;
			GlUniformBlock.WriteVector4(destination, 0, (float)deviceToGradient.sx, (float)deviceToGradient.shx, (float)deviceToGradient.tx, 0);
			GlUniformBlock.WriteVector4(destination, 16, (float)deviceToGradient.shy, (float)deviceToGradient.sy, (float)deviceToGradient.ty, 0);

			// span_gradient keeps d1 and d2 as rounded subpixels.
			int d1 = Util.iround(gradient.D1 * 16);
			int d2 = Util.iround(gradient.D2 * 16);
			GlUniformBlock.WriteVector4(destination, 32, (int)gradient.Shape, (int)gradient.Spread, d1, d2);

			// gradient_radial_focus's invariants, its degenerate focus on the circle nudged as update_values does.
			int r = Util.iround(gradient.FocusRadius * 16);
			int fx = Util.iround(gradient.FocusX * 16);
			int fy = Util.iround(gradient.FocusY * 16);
			double r2 = (double)r * r;
			double d = r2 - (((double)fx * fx) + ((double)fy * fy));
			if (d == 0)
			{
				fx += fx == 0 ? 0 : (fx < 0 ? 1 : -1);
				fy += fy == 0 ? 0 : (fy < 0 ? 1 : -1);
				d = r2 - (((double)fx * fx) + ((double)fy * fy));
			}

			GlUniformBlock.WriteVector4(destination, 48, r, fx, fy, (float)(r / d));
			GlUniformBlock.WriteVector4(destination, 64, gradient.Colors.size(), 1, 0, 0);
		}

		private static IGpuTexture WriteTable(GlCompatContext context, IGpuTexture table, IColorFunction colors, string label)
		{
			int size = colors.size();
			if (size < 1)
			{
				throw new ArgumentException("A gradient's colour function needs at least one entry.");
			}

			if (table == null || table.Descriptor.Width != size)
			{
				if (table != null)
				{
					context.Pipelines.InvalidateBindGroupsUsing(table);
					table.Dispose();
				}

				table = context.Device.CreateTexture(new TextureDescriptor((uint)size, 1, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding | TextureUsage.CopyDst, 1, 1, label));
			}

			var bytes = new byte[size * 4];
			for (int i = 0; i < size; i++)
			{
				Color color = colors[i];
				bytes[(i * 4) + 0] = color.red;
				bytes[(i * 4) + 1] = color.green;
				bytes[(i * 4) + 2] = color.blue;
				bytes[(i * 4) + 3] = color.alpha;
			}

			context.Device.WriteTexture(table, bytes, (uint)(size * 4));
			return table;
		}

		/// <summary>One context's coverage layer, tables and uniform, reused from call to call.</summary>
		private sealed class Layers : IDisposable
		{
			public bool InUse;

			public IGpuTexture Coverage;

			public IGpuTexture ColorTable;

			public IGpuTexture AlphaTable;

			public IGpuBuffer Uniform;

			/// <summary>Releases the layer, tables and uniform with their context (<see cref="GlCompatContext.Own"/>).</summary>
			public void Dispose()
			{
				this.Coverage?.Dispose();
				this.ColorTable?.Dispose();
				this.AlphaTable?.Dispose();
				this.Uniform?.Dispose();
			}
		}
	}
}
