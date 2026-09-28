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
using System.Buffers.Binary;
using System.Collections.Generic;
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
	/// <see cref="Graphics2DGpu"/>'s <see cref="IGouraudGraphics"/>: every triangle is one quad of one draw, and
	/// <c>GpuGouraud.wgsl</c> gives each pixel span_gouraud_rgba's colour at the scanline rasterizer's coverage.
	/// </summary>
	/// <remarks>
	/// The coverage is worked out in the shader (the pixel square clipped by the outline) rather than drawn into a
	/// coverage layer as <see cref="GpuGradientFill"/>'s is: a compound fill needs each triangle's own coverage at a
	/// shared pixel, which one layer cannot hold, and a layer per triangle is a pass per triangle - hundreds for a
	/// mesh. The software computes the same area, from outline points rounded to 1/256 pixel as here, so the two agree
	/// to about a cover step. The edge interpolation runs in floats where the software has doubles, so a colour
	/// sitting on a rounding boundary can take its neighbour (one level).
	/// </remarks>
	internal static class GpuGouraudFill
	{
		/// <summary>The WGSL module of the passes (the backend's <c>GpuGouraud.wgsl</c>).</summary>
		internal const string ShaderModuleKey = "GpuGouraud";

		/// <summary>One GpuGouraud.wgsl <c>Triangle</c>: three edges of 48 bytes, info, a 48-byte outline and bounds.</summary>
		private const int TriangleSize = 224;

		private const int UniformSize = 64 + (256 * 4);

		private static readonly ConditionalWeakTable<GlCompatContext, Resources> ResourcesByContext = new ConditionalWeakTable<GlCompatContext, Resources>();

		public static void Fill(Graphics2DGpu graphics, IReadOnlyList<span_gouraud_rgba> triangles, IGammaFunction coverageGamma, bool compound)
		{
			if (triangles == null)
			{
				throw new ArgumentNullException(nameof(triangles));
			}

			// Inside a compositing operator's coverage pass only the cover is wanted, as any other fill gives it.
			if (graphics.CoverageOnly)
			{
				foreach (var triangle in triangles)
				{
					graphics.Render(triangle, Color.White);
				}

				return;
			}

			var context = GpuCoverageFill.ContextOf(graphics, compound ? "FillGouraudCompound" : "FillGouraud");
			context.Passes.RejectLinearLight(compound ? "FillGouraudCompound" : "FillGouraud");
			var descriptor = context.Passes.ColorTarget.Descriptor;
			Affine deviceToScreen = GpuCoverageFill.DeviceToScreen(graphics, context);
			Affine screenToDevice = deviceToScreen;
			screenToDevice.invert();

			var data = new byte[Math.Max(triangles.Count, 1) * TriangleSize];
			Affine transform = graphics.GetTransform();
			var bounds = RectangleDouble.ZeroIntersection;
			for (int i = 0; i < triangles.Count; i++)
			{
				bounds.ExpandToInclude(Pack(data.AsSpan(i * TriangleSize, TriangleSize), triangles[i], transform));
			}

			var clip = GpuCoverageFill.DeviceClip(context);
			double left = bounds.Left, bottom = bounds.Bottom, right = bounds.Right, top = bounds.Top;
			screenToDevice.Transform(ref left, ref bottom);
			screenToDevice.Transform(ref right, ref top);
			int x0 = Math.Max((int)Math.Floor(Math.Min(left, right)), clip.X);
			int y0 = Math.Max((int)Math.Floor(Math.Min(bottom, top)), clip.Y);
			int x1 = Math.Min((int)Math.Ceiling(Math.Max(left, right)), clip.X + clip.Width);
			int y1 = Math.Min((int)Math.Ceiling(Math.Max(bottom, top)), clip.Y + clip.Height);
			if (triangles.Count == 0 || x1 <= x0 || y1 <= y0)
			{
				return;
			}

			// Spend any clear queued on the target and end the compat pass: these passes cannot run inside it.
			context.Passes.EnsurePassOpen();
			context.FlushPass();

			var device = context.Device;
			var resources = ResourcesByContext.GetValue(context, c => c.Own(new Resources()));
			if (resources.Triangles == null || (int)resources.Triangles.SizeInBytes < data.Length)
			{
				// A grown buffer leaves the old one's cached bind group unused; the context releases it.
				resources.Triangles?.Dispose();
				resources.Triangles = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopyDst, (ulong)Math.Max(data.Length * 2, 64 * TriangleSize));
			}

			device.WriteBuffer(resources.Triangles, 0, data);
			resources.Uniform ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			device.WriteBuffer(resources.Uniform, 0, Settings(deviceToScreen, screenToDevice, descriptor, compound ? null : coverageGamma));

			var scissor = (x0, y0, x1 - x0, y1 - y0);
			if (compound)
			{
				resources.Sum = GpuBlur.Layers.Fit(context, resources.Sum, descriptor, "GpuGouraudSum");
				var add = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.One);
				Draw(context, resources, resources.Sum, LoadOp.Clear, add, triangles.Count, scissor);
				Composite(context, resources, scissor);
			}
			else
			{
				var sourceOver = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha);
				Draw(context, resources, context.Passes.ColorTarget, LoadOp.Load, sourceOver, triangles.Count, scissor);
			}

			// The buffers and the sum layer are rewritten by the next fill; submitting keeps those writes behind these passes.
			context.Submit();
		}

		/// <summary>
		/// Writes one <c>Triangle</c>: span_gouraud_rgba::prepare's sorted edges, its outline (in the Graphics2D's
		/// pixels, rounded to the rasterizer's 1/256) and the quad around it; returns that quad.
		/// </summary>
		private static RectangleDouble Pack(Span<byte> destination, span_gouraud_rgba triangle, Affine transform)
		{
			var coord = new span_gouraud.coord_type[3];
			for (int i = 0; i < 3; i++)
			{
				coord[i] = triangle.Corner(i);
				transform.Transform(ref coord[i].x, ref coord[i].y);
			}

			// span_gouraud::arrange_vertices: sorted by y, ties kept as it keeps them.
			if (coord[0].y > coord[2].y)
			{
				(coord[0], coord[2]) = (coord[2], coord[0]);
			}

			if (coord[0].y > coord[1].y)
			{
				(coord[0], coord[1]) = (coord[1], coord[0]);
			}

			if (coord[1].y > coord[2].y)
			{
				(coord[1], coord[2]) = (coord[2], coord[1]);
			}

			WriteEdge(destination, 0, coord[0], coord[2]);
			WriteEdge(destination, 48, coord[0], coord[1]);
			WriteEdge(destination, 96, coord[1], coord[2]);

			bool swap = agg_math.cross_product(coord[0].x, coord[0].y, coord[2].x, coord[2].y, coord[1].x, coord[1].y) < 0.0;

			var bounds = RectangleDouble.ZeroIntersection;
			int count = 0;
			triangle.Rewind(0);
			while (count < 6 && ShapePath.IsVertex(triangle.Vertex(out double x, out double y)))
			{
				transform.Transform(ref x, ref y);
				x = Math.Round(x * 256) / 256;
				y = Math.Round(y * 256) / 256;
				WriteFloat(destination, 160 + (count * 8), x);
				WriteFloat(destination, 164 + (count * 8), y);
				bounds.ExpandToInclude(x, y);
				count++;
			}

			WriteFloat(destination, 144, (int)coord[1].y);
			WriteFloat(destination, 148, swap ? 1 : 0);
			WriteFloat(destination, 152, count);
			if (count < 3)
			{
				// Nothing to cover: an empty quad.
				return RectangleDouble.ZeroIntersection;
			}

			// The quad reaches the pixels the outline's edges only touch.
			bounds = new RectangleDouble(Math.Floor(bounds.Left) - 1, Math.Floor(bounds.Bottom) - 1, Math.Ceiling(bounds.Right) + 1, Math.Ceiling(bounds.Top) + 1);
			WriteFloat(destination, 208, bounds.Left);
			WriteFloat(destination, 212, bounds.Bottom);
			WriteFloat(destination, 216, bounds.Right);
			WriteFloat(destination, 220, bounds.Top);
			return bounds;
		}

		/// <summary>span_gouraud_rgba::rgba_calc::init from <paramref name="c1"/> to <paramref name="c2"/>.</summary>
		private static void WriteEdge(Span<byte> destination, int offset, span_gouraud.coord_type c1, span_gouraud.coord_type c2)
		{
			double dy = c2.y - c1.y;
			WriteFloat(destination, offset, c1.x - 0.5);
			WriteFloat(destination, offset + 4, c1.y - 0.5);
			WriteFloat(destination, offset + 8, c2.x - c1.x);
			WriteFloat(destination, offset + 12, dy < 1e-5 ? 1e5 : 1.0 / dy);
			int[] start = { c1.color.red, c1.color.green, c1.color.blue, c1.color.alpha };
			int[] end = { c2.color.red, c2.color.green, c2.color.blue, c2.color.alpha };
			for (int i = 0; i < 4; i++)
			{
				BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset + 16 + (i * 4)), start[i]);
				BinaryPrimitives.WriteInt32LittleEndian(destination.Slice(offset + 32 + (i * 4)), end[i] - start[i]);
			}
		}

		private static void WriteFloat(Span<byte> destination, int offset, double value)
		{
			BinaryPrimitives.WriteSingleLittleEndian(destination.Slice(offset), (float)value);
		}

		/// <summary>The <c>Settings</c> uniform: the pixel mappings and the rasterizer's gamma table (ScanlineRasterizer.gamma's rounding).</summary>
		private static byte[] Settings(Affine deviceToScreen, Affine screenToDevice, TextureDescriptor target, IGammaFunction gamma)
		{
			var bytes = new byte[UniformSize];
			var span = bytes.AsSpan();
			GlUniformBlock.WriteVector4(span, 0, (float)deviceToScreen.sx, (float)deviceToScreen.shx, (float)deviceToScreen.tx, 0);
			GlUniformBlock.WriteVector4(span, 16, (float)deviceToScreen.shy, (float)deviceToScreen.sy, (float)deviceToScreen.ty, 0);

			// Device pixels to clip space: x from -1 to 1 left to right, y from 1 to -1 top to bottom.
			double kx = 2.0 / target.Width;
			double ky = -2.0 / target.Height;
			GlUniformBlock.WriteVector4(span, 32, (float)(screenToDevice.sx * kx), (float)(screenToDevice.shx * kx), (float)((screenToDevice.tx * kx) - 1), 0);
			GlUniformBlock.WriteVector4(span, 48, (float)(screenToDevice.shy * ky), (float)(screenToDevice.sy * ky), (float)((screenToDevice.ty * ky) + 1), 0);
			for (int i = 0; i < 256; i++)
			{
				int cover = gamma == null ? i : Math.Max(0, Math.Min(255, Util.uround(gamma.GetGamma(i / 255.0) * 255)));
				BinaryPrimitives.WriteSingleLittleEndian(span.Slice(64 + (i * 4)), cover / 255f);
			}

			return bytes;
		}

		private static void Draw(GlCompatContext context, Resources resources, IGpuTexture target, LoadOp load, BlendComponent blend, int triangleCount, (int X, int Y, int Width, int Height) scissor)
		{
			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"fragmentMain",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(target.Descriptor.Format, true, blend, blend) },
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Vertex | ShaderStage.Fragment, BindingType.UniformBuffer),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Vertex | ShaderStage.Fragment, BindingType.ReadOnlyStorageBuffer),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"GpuGouraud"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[] { BindGroupEntry.ForBuffer(0, resources.Uniform), BindGroupEntry.ForBuffer(1, resources.Triangles) },
				"GpuGouraud"));

			using (var encoder = context.Device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(target, load) },
				DepthAttachment.None,
				"GpuGouraud")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(scissor.X, scissor.Y, scissor.Width, scissor.Height);
				encoder.Draw(triangleCount * 6);
			}
		}

		/// <summary>A compound fill's summed layer onto the target, source-over.</summary>
		private static void Composite(GlCompatContext context, Resources resources, (int X, int Y, int Width, int Height) scissor)
		{
			var target = context.Passes.ColorTarget;
			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
			var sourceOver = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha);
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"compositeVertex",
				module,
				"compositeFragment",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(target.Descriptor.Format, true, sourceOver, sourceOver) },
				new[] { new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.Texture) },
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"GpuGouraudComposite"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(pipeline, 0, new[] { BindGroupEntry.ForTexture(2, resources.Sum) }, "GpuGouraudComposite"));
			using (var encoder = context.Device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(target, LoadOp.Load) },
				DepthAttachment.None,
				"GpuGouraudComposite")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(scissor.X, scissor.Y, scissor.Width, scissor.Height);
				encoder.Draw(3);
			}
		}

		/// <summary>One context's buffers and compound sum layer, reused from call to call.</summary>
		private sealed class Resources : IDisposable
		{
			public IGpuBuffer Triangles;

			public IGpuBuffer Uniform;

			public IGpuTexture Sum;

			/// <summary>Releases the buffers and layer with their context (<see cref="GlCompatContext.Own"/>).</summary>
			public void Dispose()
			{
				this.Triangles?.Dispose();
				this.Uniform?.Dispose();
				this.Sum?.Dispose();
			}
		}
	}
}
