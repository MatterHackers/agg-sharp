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
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="IBlurGraphics"/>: the draws go source-over into a transparent
	/// premultiplied layer the size of the target, <c>GpuBlur.wgsl</c> stack-blurs it across into a second layer and
	/// then down, drawing that pass source-over onto the target (inside the current scissor).
	/// </summary>
	/// <remarks>
	/// Not byte-exact against software stack_blur: that one sums in integers and divides with its mul/shr tables,
	/// the shader in floats with the one rounding the 8-bit middle layer makes, so pixels differ by a unit or two.
	/// No destination read is needed, so any target works - except for <see cref="BlurUnder"/>, which copies the
	/// target and so needs it to allow copies (<see cref="TextureUsage.CopySrc"/>), as <see cref="GpuCompOp"/> does.
	/// </remarks>
	internal static class GpuBlur
	{
		/// <summary>The WGSL module of the blur passes (the backend's <c>GpuBlur.wgsl</c>).</summary>
		internal const string ShaderModuleKey = "GpuBlur";

		private const int UniformSize = 16;

		private static readonly ConditionalWeakTable<GlCompatContext, Layers> LayersByContext = new ConditionalWeakTable<GlCompatContext, Layers>();

		public static void Draw(GL gl, double radius, Action draw, Color[] alphaToColor)
		{
			if (draw == null)
			{
				throw new ArgumentNullException(nameof(draw));
			}

			if (alphaToColor != null && alphaToColor.Length < 256)
			{
				throw new ArgumentException("A colour table needs an entry for every alpha value, 256.", nameof(alphaToColor));
			}

			if (!(gl?.GpuContext is GlCompatContext context) || context.Passes.ColorTarget == null)
			{
				throw new NotSupportedException("DrawBlurred needs a GPU render target.");
			}

			context.Passes.RejectLinearLight(alphaToColor != null ? "DrawWithCoverageGamma" : "DrawBlurred");

			var layers = LayersByContext.GetValue(context, c => c.Own(new Layers()));
			if (layers.InUse)
			{
				throw new NotSupportedException("DrawBlurred cannot be nested inside another DrawBlurred.");
			}

			var target = context.Passes.ColorTarget;
			var descriptor = target.Descriptor;
			int scale = context.CoordinateScale;
			var state = context.State;
			GlViewportRect? viewport = state.ViewportSet ? state.Viewport : (GlViewportRect?)null;

			layers.InUse = true;
			var redirect = GpuTargetRedirect.SaveCurrent(gl, context);
			bool drawn = false;
			try
			{
				layers.Ensure(context, descriptor);
				redirect.RedirectTo(layers.Drawn, null, scale, (int)descriptor.Width / scale, (int)descriptor.Height / scale);

				// The layer lines up with the target texel for texel, so the draws keep the target's viewport.
				if (viewport.HasValue)
				{
					gl.Viewport(viewport.Value.X, viewport.Value.Y, viewport.Value.Width, viewport.Value.Height);
				}

				draw();
				drawn = true;
			}
			finally
			{
				redirect.Restore();
				layers.InUse = false;
			}

			if (drawn)
			{
				// stack_blur takes a whole radius; a scaled target blurs as far in its own pixels.
				int deviceRadius = Math.Max(0, (int)Math.Round(Math.Round(radius) * scale));
				Blur(context, target, layers, deviceRadius, alphaToColor);
			}
		}

		/// <summary>
		/// <see cref="IBlurGraphics.BlurUnder"/>: the region drawn in white into a coverage layer, the target copied
		/// into the drawn layer around it, stack-blurred across into the middle layer, then down and mixed over the
		/// copy by the coverage, written back without blending.
		/// </summary>
		public static void BlurUnder(Graphics2DGpu graphics, IVertexSource region, double radius)
		{
			if (region == null)
			{
				throw new ArgumentNullException(nameof(region));
			}

			var gl = graphics.gl;
			if (!(gl?.GpuContext is GlCompatContext context) || context.Passes.ColorTarget == null)
			{
				throw new NotSupportedException("BlurUnder needs a GPU render target.");
			}

			context.Passes.RejectLinearLight("BlurUnder");

			var target = context.Passes.ColorTarget;
			var descriptor = target.Descriptor;
			if ((descriptor.Usage & TextureUsage.CopySrc) == 0)
			{
				throw new NotSupportedException("BlurUnder needs a render target that allows copies (CopySrc).");
			}

			var layers = LayersByContext.GetValue(context, c => c.Own(new Layers()));
			if (layers.InUse)
			{
				throw new NotSupportedException("BlurUnder cannot be nested inside a DrawBlurred.");
			}

			int scale = context.CoordinateScale;
			var state = context.State;
			GlViewportRect? viewport = state.ViewportSet ? state.Viewport : (GlViewportRect?)null;
			var bounds = new VertexSourceApplyTransform(region, graphics.GetTransform()).GetBounds();
			if (bounds.Width <= 0 || bounds.Height <= 0)
			{
				return;
			}

			layers.InUse = true;
			var redirect = GpuTargetRedirect.SaveCurrent(gl, context);
			try
			{
				layers.Ensure(context, descriptor);
				layers.Coverage = Layers.Fit(context, layers.Coverage, descriptor, "GpuBlurCoverage");
				redirect.RedirectTo(layers.Coverage, null, scale, (int)descriptor.Width / scale, (int)descriptor.Height / scale);
				if (viewport.HasValue)
				{
					gl.Viewport(viewport.Value.X, viewport.Value.Y, viewport.Value.Width, viewport.Value.Height);
				}

				graphics.Render(region, Color.White);
			}
			finally
			{
				redirect.Restore();
				layers.InUse = false;
			}

			int deviceRadius = Math.Max(0, (int)Math.Round(Math.Round(radius) * scale));

			// The region's device rectangle (the halo reaches a pixel past it), found through the viewport the
			// ortho projection maps the Graphics2D's pixels onto.
			GlViewportRect view = viewport ?? new GlViewportRect(0, 0, (int)descriptor.Width / scale, (int)descriptor.Height / scale);
			double toDeviceX = view.Width * (double)scale / graphics.Width;
			double toDeviceY = view.Height * (double)scale / graphics.Height;
			int left = (int)Math.Floor((view.X * scale) + (bounds.Left * toDeviceX)) - 2;
			int right = (int)Math.Ceiling((view.X * scale) + (bounds.Right * toDeviceX)) + 2;
			int bottomUp = (int)Math.Floor((view.Y * scale) + (bounds.Bottom * toDeviceY)) - 2;
			int topUp = (int)Math.Ceiling((view.Y * scale) + (bounds.Top * toDeviceY)) + 2;
			(int X, int Y, int Width, int Height) area = (left, (int)descriptor.Height - topUp, right - left, topUp - bottomUp);
			if (context.State.ScissorEnabled)
			{
				area = Intersect(area, context.ToDeviceRect(context.State.Scissor));
			}

			area = Intersect(area, (0, 0, (int)descriptor.Width, (int)descriptor.Height));
			if (area.Width <= 0 || area.Height <= 0)
			{
				return;
			}

			// The copy reaches the radius past the area, so every tap the area's pixels take is the frame's.
			var copy = Intersect((area.X - deviceRadius, area.Y - deviceRadius, area.Width + (2 * deviceRadius), area.Height + (2 * deviceRadius)), (0, 0, (int)descriptor.Width, (int)descriptor.Height));

			context.Passes.EnsurePassOpen();
			context.FlushPass();

			var device = context.Device;
			device.CopyTextureToTexture(target, layers.Drawn, copy.X, copy.Y, copy.Width, copy.Height);

			layers.Table ??= device.CreateTexture(new TextureDescriptor(256, 1, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding | TextureUsage.CopyDst, 1, 1, "GpuBlurTable"));
			layers.Across ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			layers.Down ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			var scratch = new byte[UniformSize];
			GlUniformBlock.WriteVector4(scratch.AsSpan(), 0, deviceRadius, 0, 0, 0);
			device.WriteBuffer(layers.Across, 0, scratch);
			GlUniformBlock.WriteVector4(scratch.AsSpan(), 0, deviceRadius, 1, 0, 0);
			device.WriteBuffer(layers.Down, 0, scratch);

			RunPass(context, layers.Drawn, layers.Table, layers.Across, layers.Middle, LoadOp.Clear, false, area, "GpuBlurAcross");
			RunUnderPass(context, layers, target, area);

			// As Blur: the next call rewrites the uniforms and layers.
			context.Submit();
		}

		private static (int X, int Y, int Width, int Height) Intersect((int X, int Y, int Width, int Height) a, (int X, int Y, int Width, int Height) b)
		{
			int x0 = Math.Max(a.X, b.X);
			int y0 = Math.Max(a.Y, b.Y);
			int x1 = Math.Min(a.X + a.Width, b.X + b.Width);
			int y1 = Math.Min(a.Y + a.Height, b.Y + b.Height);
			return (x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
		}

		private static void RunUnderPass(GlCompatContext context, Layers layers, IGpuTexture target, (int X, int Y, int Width, int Height) area)
		{
			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
			var replace = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.Zero);
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"underMain",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(target.Descriptor.Format, false, replace, replace) },
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.UniformBuffer),
					new BindGroupLayoutEntry(0, 3, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 4, ShaderStage.Fragment, BindingType.Texture),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"GpuBlurUnder"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, layers.Middle),
					BindGroupEntry.ForBuffer(2, layers.Down),
					BindGroupEntry.ForTexture(3, layers.Coverage),
					BindGroupEntry.ForTexture(4, layers.Drawn),
				},
				"GpuBlurUnder"));

			using (var encoder = context.Device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(target, LoadOp.Load) },
				DepthAttachment.None,
				"GpuBlurUnder")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(area.X, area.Y, area.Width, area.Height);
				encoder.Draw(3);
			}
		}

		private static void Blur(GlCompatContext context, IGpuTexture target, Layers layers, int radius, Color[] alphaToColor)
		{
			var descriptor = target.Descriptor;
			(int X, int Y, int Width, int Height) scissor = (0, 0, (int)descriptor.Width, (int)descriptor.Height);
			if (context.State.ScissorEnabled)
			{
				scissor = context.ToDeviceRect(context.State.Scissor);
				if (scissor.Width <= 0 || scissor.Height <= 0)
				{
					return;
				}
			}

			// Spend any clear queued on the target and end the compat pass: these passes cannot run inside it.
			context.Passes.EnsurePassOpen();
			context.FlushPass();

			var device = context.Device;
			bool useTable = alphaToColor != null;
			layers.Table ??= device.CreateTexture(new TextureDescriptor(256, 1, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding | TextureUsage.CopyDst, 1, 1, "GpuBlurTable"));
			if (useTable)
			{
				var bytes = new byte[256 * 4];
				for (int i = 0; i < 256; i++)
				{
					bytes[(i * 4) + 0] = alphaToColor[i].red;
					bytes[(i * 4) + 1] = alphaToColor[i].green;
					bytes[(i * 4) + 2] = alphaToColor[i].blue;
					bytes[(i * 4) + 3] = alphaToColor[i].alpha;
				}

				device.WriteTexture(layers.Table, bytes, 256 * 4);
			}

			layers.Across ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			layers.Down ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			var scratch = new byte[UniformSize];
			GlUniformBlock.WriteVector4(scratch.AsSpan(), 0, radius, 0, 0, 0);
			device.WriteBuffer(layers.Across, 0, scratch);
			GlUniformBlock.WriteVector4(scratch.AsSpan(), 0, radius, 1, useTable ? 1 : 0, 0);
			device.WriteBuffer(layers.Down, 0, scratch);

			// Across: replaces the middle layer. Down: premultiplied source-over onto the target.
			RunPass(context, layers.Drawn, layers.Table, layers.Across, layers.Middle, LoadOp.Clear, false, scissor, "GpuBlurAcross");
			RunPass(context, layers.Middle, layers.Table, layers.Down, target, LoadOp.Load, true, scissor, "GpuBlurDown");

			// The uniforms, the table and the layers are rewritten by the next blur; submitting now keeps those
			// writes from overtaking these draws in the queue.
			context.Submit();
		}

		private static void RunPass(GlCompatContext context, IGpuTexture source, IGpuTexture table, IGpuBuffer uniform, IGpuTexture destination, LoadOp load, bool blend, (int X, int Y, int Width, int Height) scissor, string label)
		{
			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
			var sourceOver = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha);
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"fragmentMain",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(destination.Descriptor.Format, blend, sourceOver, sourceOver) },
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.UniformBuffer),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				label));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, source),
					BindGroupEntry.ForTexture(1, table),
					BindGroupEntry.ForBuffer(2, uniform),
				},
				label));

			using (var encoder = context.Device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(destination, load, ClearColor.Transparent) },
				DepthAttachment.None,
				label)))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				if (blend)
				{
					encoder.SetScissor(scissor.X, scissor.Y, scissor.Width, scissor.Height);
				}

				encoder.Draw(3);
			}
		}

		/// <summary>One context's layer textures, colour table and uniforms, reused from call to call.</summary>
		internal sealed class Layers : IDisposable
		{
			public bool InUse;

			public IGpuTexture Drawn;

			public IGpuTexture Middle;

			public IGpuTexture Table;

			public IGpuTexture Coverage;

			public IGpuBuffer Across;

			public IGpuBuffer Down;

			public void Ensure(GlCompatContext context, TextureDescriptor target)
			{
				// CopyDst: BlurUnder copies the frame into it.
				this.Drawn = Fit(context, this.Drawn, target, "GpuBlurDrawn", TextureUsage.CopyDst);
				this.Middle = Fit(context, this.Middle, target, "GpuBlurMiddle");
			}

			/// <summary>Releases the layers with their context (<see cref="GlCompatContext.Own"/>).</summary>
			public void Dispose()
			{
				this.Drawn?.Dispose();
				this.Middle?.Dispose();
				this.Table?.Dispose();
				this.Coverage?.Dispose();
				this.Across?.Dispose();
				this.Down?.Dispose();
			}

			public static IGpuTexture Fit(GlCompatContext context, IGpuTexture texture, TextureDescriptor target, string label, TextureUsage extraUsage = 0)
			{
				var current = texture?.Descriptor;
				if (current != null
					&& current.Value.Width == target.Width
					&& current.Value.Height == target.Height
					&& current.Value.Format == target.Format)
				{
					return texture;
				}

				if (texture != null)
				{
					// Bind groups are cached on the texture object; without this every resize strands one.
					context.Pipelines.InvalidateBindGroupsUsing(texture);
					texture.Dispose();
				}

				var usage = TextureUsage.RenderAttachment | TextureUsage.TextureBinding | extraUsage;
				return context.Device.CreateTexture(new TextureDescriptor(target.Width, target.Height, target.Format, usage, 1, 1, label));
			}
		}
	}
}
