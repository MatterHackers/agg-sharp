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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="IImageFilterGraphics"/>: the path is drawn in white into a transparent
	/// coverage layer (the ordinary halo-anti-aliased fill), then <c>GpuImageFilter.wgsl</c> runs the chosen AGG image
	/// span generator at every pixel of the path's bounds - the image and the filter's weight table uploaded as
	/// textures - scales the colour by the coverage and draws it source-over.
	/// </summary>
	/// <remarks>
	/// Not byte-exact against software. Where software runs span_interpolator_linear over an affine and the device
	/// is the Graphics2D's pixels one for one, the shader steps its integer subpixel dda along software's own spans
	/// (<see cref="GpuImageFilterSpans"/>), so only the coverage (the GPU's anti-aliasing) differs; elsewhere it maps
	/// each device pixel centre through the homography in floats where software steps integer subpixels (and
	/// persp_lerp / subdiv interpolate), so a subpixel coordinate can differ by one. It samples per device pixel, so on a scaled (HiDPI) target the image
	/// is filtered at the device's resolution.
	/// </remarks>
	internal static class GpuImageFilter
	{
		/// <summary>The WGSL module of the pass (the backend's <c>GpuImageFilter.wgsl</c>).</summary>
		internal const string ShaderModuleKey = "GpuImageFilter";

		private const int UniformSize = 14 * 16;

		private static readonly ConditionalWeakTable<GlCompatContext, Resources> ResourcesByContext = new ConditionalWeakTable<GlCompatContext, Resources>();

		public static void Fill(Graphics2DGpu graphics, IVertexSource path, IImageByte image, ImageFilterFill fill)
		{
			if (image == null)
			{
				throw new ArgumentNullException(nameof(image));
			}

			if (image.BitDepth != 32)
			{
				throw new NotSupportedException("A filtered image fill's image is expected to be 32 bit.");
			}

			Fill(graphics, path, fill, (context, resources) =>
			{
				resources.UploadImage(context, image);
				return new Source(resources.Image, image.Width, image.Height, 1, false);
			});
		}

		/// <summary>
		/// <see cref="Fill(Graphics2DGpu, IVertexSource, IImageByte, ImageFilterFill)"/> reading the picture a
		/// <see cref="GpuRetainedLayer"/> holds straight from its texture, with no trip through the CPU. The layer's
		/// logical pixels are the image's pixels (one painted at a coordinate scale above 1 is filtered at its
		/// texture's resolution), and its colour is premultiplied, which the generators run on as C++ AGG's rgba
		/// generators do over a premultiplied pixel format.
		/// </summary>
		public static void Fill(Graphics2DGpu graphics, IVertexSource path, IRetainedLayer layer, ImageFilterFill fill)
		{
			if (layer == null)
			{
				throw new ArgumentNullException(nameof(layer));
			}

			if (layer is not GpuRetainedLayer gpuLayer || !gpuLayer.BelongsTo(graphics))
			{
				throw new ArgumentException("The layer is not one this surface's device made.", nameof(layer));
			}

			var target = gpuLayer.Target;
			if (target.IsDrawing)
			{
				// Its texture is the render attachment then; WebGPU cannot sample it in the same pass.
				throw new InvalidOperationException("A retained layer cannot be read while it is being painted.");
			}

			if (target.Texture == null)
			{
				throw new InvalidOperationException("The retained layer has not been painted yet.");
			}

			if (target.LinearLight)
			{
				// Its texels are linear light in floats, which the generators would read as sRGB bytes.
				throw new NotSupportedException("A linear-light layer (CreateRetainedLayer(linearLight: true)) cannot be filled from; paint the picture in an sRGB layer.");
			}

			if (fill?.BrightnessToAlpha != null)
			{
				// The table reads straight colour, which a premultiplied layer no longer has.
				throw new NotSupportedException("BrightnessToAlpha needs a straight-alpha image, not a retained layer.");
			}

			Fill(graphics, path, fill, (context, resources) =>
				new Source(target.Texture, target.Width, target.Height, gpuLayer.PaintedCoordinateScale, true));
		}

		private static void Fill(Graphics2DGpu graphics, IVertexSource path, ImageFilterFill fill, Func<GlCompatContext, Resources, Source> source)
		{
			if (path == null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			if (fill == null)
			{
				throw new ArgumentNullException(nameof(fill));
			}

			if ((fill.Kind == ImageFilterKind.Filter || fill.Kind == ImageFilterKind.Resample) && fill.Filter == null)
			{
				throw new ArgumentException("Filter and Resample need the filter's weight table.", nameof(fill));
			}

			if (fill.BilinearScreenToImage != null && fill.Kind == ImageFilterKind.Resample)
			{
				throw new NotSupportedException("A bilinear mapping has no resample generator.");
			}

			if (graphics.CoverageOnly)
			{
				graphics.Render(path, Color.White);
				return;
			}

			var context = GpuCoverageFill.ContextOf(graphics, "FillPathWithFilteredImage");
			context.Passes.RejectLinearLight("FillPathWithFilteredImage");
			var resources = ResourcesByContext.GetValue(context, c => c.Own(new Resources()));
			if (GpuCoverageFill.DrawCoverage(graphics, context, path, ref resources.Coverage, ref resources.InUse, "GpuImageFilterCoverage", "FillPathWithFilteredImage", out var pass))
			{
				var image = source(context, resources);
				int[] spans = GpuImageFilterSpans.Build(path, graphics.GetTransform(), pass.DeviceToScreen, fill, image.Scale);
				Shade(context, resources, pass, image, fill, spans);
			}
		}

		private static void Shade(GlCompatContext context, Resources resources, GpuCoverageFill.CoveragePass pass, Source source, ImageFilterFill fill, int[] spans)
		{
			var descriptor = pass.Target.Descriptor;
			var device = context.Device;
			resources.Weights = UploadWeights(context, resources.Weights, fill.Filter);
			resources.AlphaTable = UploadAlphaTable(context, resources.AlphaTable, fill.BrightnessToAlpha);

			resources.Uniform ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			device.WriteBuffer(resources.Uniform, 0, Settings(pass.DeviceToScreen, source, fill));

			byte[] spanBytes = new byte[spans.Length * sizeof(int)];
			Buffer.BlockCopy(spans, 0, spanBytes, 0, spanBytes.Length);
			if (resources.Spans == null || (int)resources.Spans.SizeInBytes < spanBytes.Length)
			{
				// A grown buffer leaves the old one's cached bind group unused; the context releases it.
				resources.Spans?.Dispose();
				resources.Spans = device.CreateBuffer(BufferUsage.Storage | BufferUsage.CopyDst, (ulong)Math.Max(spanBytes.Length * 2, 4096));
			}

			device.WriteBuffer(resources.Spans, 0, spanBytes);

			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
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
					new BindGroupLayoutEntry(0, 4, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 5, ShaderStage.Fragment, BindingType.ReadOnlyStorageBuffer),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"GpuImageFilter"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, resources.Coverage),
					BindGroupEntry.ForTexture(1, source.Texture),
					BindGroupEntry.ForTexture(2, resources.Weights),
					BindGroupEntry.ForBuffer(3, resources.Uniform),
					BindGroupEntry.ForTexture(4, resources.AlphaTable),
					BindGroupEntry.ForBuffer(5, resources.Spans),
				},
				"GpuImageFilter"));

			using (var encoder = device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(pass.Target, LoadOp.Load) },
				DepthAttachment.None,
				"GpuImageFilter")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(pass.X, pass.Y, pass.Width, pass.Height);
				encoder.Draw(3);
			}

			// The uniform, image, weights and coverage layer are rewritten by the next fill; submitting now keeps
			// those writes from overtaking this draw in the queue.
			context.Submit();
		}

		/// <summary>GpuImageFilter.wgsl's <c>Settings</c>.</summary>
		private static byte[] Settings(Affine deviceToScreen, Source source, ImageFilterFill fill)
		{
			// Device pixel to image: device to screen first, then the fill's screen to image.
			// A bilinear mapping is not a homography: the rows stop at the screen and the shader applies it after.
			var bilinear = fill.BilinearScreenToImage;
			var deviceToImage = bilinear != null ? new Perspective(Affine.NewIdentity()) : fill.ScreenToImage();
			deviceToImage.premultiply(deviceToScreen);
			if (bilinear == null)
			{
				// Image pixels to the source texture's (a layer painted at a coordinate scale has more).
				deviceToImage.multiply(Affine.NewScaling(source.Scale));
			}

			var imageToDevice = new Perspective(deviceToImage);
			imageToDevice.invert();

			var bytes = new byte[UniformSize];
			var span = bytes.AsSpan();
			WriteRows(span, 0, deviceToImage);
			WriteRows(span, 48, imageToDevice);

			int kind = (int)fill.Kind;
			WriteInts(span, 96, kind, (int)fill.Edge, source.Width, source.Height);

			var filter = fill.Filter;
			bool isResample = fill.Kind == ImageFilterKind.Resample;
			bool twoByTwo = fill.Kind == ImageFilterKind.Filter && filter.diameter() == 2;
			bool affineResample = isResample && fill.IsAffine;
			WriteInts(span, 112, filter?.diameter() ?? 0, filter?.start() ?? 0, twoByTwo ? 1 : 0, affineResample ? 1 : 0);

			if (affineResample)
			{
				// span_image_resample_affine.prepare over the device-to-image affine.
				var affine = new Affine(deviceToImage.sx, deviceToImage.shy, deviceToImage.shx, deviceToImage.sy, deviceToImage.tx, deviceToImage.ty);
				affine.scaling_abs(out double scaleX, out double scaleY);
				const double scaleLimit = 200;
				double scaleXY = scaleX * scaleY;
				if (scaleXY > scaleLimit)
				{
					scaleX = scaleX * scaleLimit / scaleXY;
					scaleY = scaleY * scaleLimit / scaleXY;
				}

				scaleX = Math.Max(1, Math.Min(scaleLimit, Math.Max(1, scaleX)) * fill.Blur);
				scaleY = Math.Max(1, Math.Min(scaleLimit, Math.Max(1, scaleY)) * fill.Blur);
				WriteInts(span, 128, Util.uround(scaleX * 256), Util.uround(scaleY * 256), Util.uround(256 / scaleX), Util.uround(256 / scaleY));
			}

			// span_image_resample's scale limit, 20, and its blur in subpixels.
			int blur = Util.uround(fill.Blur * 256);
			WriteInts(span, 144, 20, blur, blur, bilinear != null ? 1 : 0);

			// A layer's texels are premultiplied, so what the clip accessor reads past its edge must be too.
			var background = fill.Background;
			int scale = source.IsLayer ? background.alpha : 255;
			WriteInts(span, 160, (background.red * scale) / 255, (background.green * scale) / 255, (background.blue * scale) / 255, background.alpha);

			if (bilinear != null)
			{
				// trans_bilinear's x' = a + b xy + c x + d y (and y' likewise), recovered from four points.
				double[] x = { 0, 1, 0, 1 };
				double[] y = { 0, 0, 1, 1 };
				for (int i = 0; i < 4; i++)
				{
					bilinear.Transform(ref x[i], ref y[i]);
					x[i] *= source.Scale;
					y[i] *= source.Scale;
				}

				GlUniformBlock.WriteVector4(span, 176, (float)x[0], (float)(x[3] - x[1] - x[2] + x[0]), (float)(x[1] - x[0]), (float)(x[2] - x[0]));
				GlUniformBlock.WriteVector4(span, 192, (float)y[0], (float)(y[3] - y[1] - y[2] + y[0]), (float)(y[1] - y[0]), (float)(y[2] - y[0]));
			}

			var alphaTable = fill.BrightnessToAlpha;
			WriteInts(span, 208, alphaTable != null ? 1 : 0, alphaTable?.Length ?? 0, source.IsLayer ? 1 : 0, fill.Opaque ? 1 : 0);
			return bytes;
		}

		private static void WriteRows(Span<byte> destination, int offset, Perspective m)
		{
			// Perspective.Transform: x' = sx x + shx y + tx, y' = shy x + sy y + ty, w = w0 x + w1 y + w2.
			GlUniformBlock.WriteVector4(destination, offset, (float)m.sx, (float)m.shx, (float)m.tx, 0);
			GlUniformBlock.WriteVector4(destination, offset + 16, (float)m.shy, (float)m.sy, (float)m.ty, 0);
			GlUniformBlock.WriteVector4(destination, offset + 32, (float)m.w0, (float)m.w1, (float)m.w2, 0);
		}

		private static void WriteInts(Span<byte> destination, int offset, int x, int y, int z, int w)
		{
			BitConverter.TryWriteBytes(destination.Slice(offset, 4), x);
			BitConverter.TryWriteBytes(destination.Slice(offset + 4, 4), y);
			BitConverter.TryWriteBytes(destination.Slice(offset + 8, 4), z);
			BitConverter.TryWriteBytes(destination.Slice(offset + 12, 4), w);
		}

		/// <summary>The image's straight-alpha bytes in RGBA order, image row 0 in texture row 0.</summary>
		private static IGpuTexture Upload(GlCompatContext context, IGpuTexture texture, IImageByte image)
		{
			texture = Fit(context, texture, (uint)image.Width, (uint)image.Height, "GpuImageFilterImage");
			byte[] source = image.GetBuffer();
			int step = image.GetBytesBetweenPixelsInclusive();
			var bytes = new byte[image.Width * image.Height * 4];
			for (int y = 0; y < image.Height; y++)
			{
				int from = image.GetBufferOffsetY(y);
				int to = y * image.Width * 4;
				for (int x = 0; x < image.Width; x++, from += step, to += 4)
				{
					bytes[to + 0] = source[from + ImageBuffer.OrderR];
					bytes[to + 1] = source[from + ImageBuffer.OrderG];
					bytes[to + 2] = source[from + ImageBuffer.OrderB];
					bytes[to + 3] = source[from + ImageBuffer.OrderA];
				}
			}

			context.Device.WriteTexture(texture, bytes, (uint)(image.Width * 4));
			return texture;
		}

		/// <summary>The weight table, 256 entries a row, each weight + 32768 in red (low byte) and green (high byte).</summary>
		private static IGpuTexture UploadWeights(GlCompatContext context, IGpuTexture texture, ImageFilterLookUpTable filter)
		{
			int rows = Math.Max(1, filter?.diameter() ?? 1);
			texture = Fit(context, texture, 256, (uint)rows, "GpuImageFilterWeights");
			var bytes = new byte[256 * rows * 4];
			if (filter != null)
			{
				int[] weights = filter.weight_array();
				for (int i = 0; i < 256 * rows; i++)
				{
					int stored = weights[i] + 32768;
					bytes[(i * 4) + 0] = (byte)(stored & 255);
					bytes[(i * 4) + 1] = (byte)(stored >> 8);
				}
			}

			context.Device.WriteTexture(texture, bytes, 256 * 4);
			return texture;
		}

		/// <summary>ImageFilterFill.BrightnessToAlpha, one entry a texel in red (a 1x1 stand-in when there is none).</summary>
		private static IGpuTexture UploadAlphaTable(GlCompatContext context, IGpuTexture texture, byte[] table)
		{
			int length = Math.Max(1, table?.Length ?? 1);
			texture = Fit(context, texture, (uint)length, 1, "GpuImageFilterAlphaTable");
			if (table != null)
			{
				var bytes = new byte[length * 4];
				for (int i = 0; i < table.Length; i++)
				{
					bytes[i * 4] = table[i];
				}

				context.Device.WriteTexture(texture, bytes, (uint)(length * 4));
			}

			return texture;
		}

		private static IGpuTexture Fit(GlCompatContext context, IGpuTexture texture, uint width, uint height, string label)
		{
			if (texture != null && texture.Descriptor.Width == width && texture.Descriptor.Height == height)
			{
				return texture;
			}

			if (texture != null)
			{
				// Bind groups are cached on the texture object; without this every resize strands one.
				context.Pipelines.InvalidateBindGroupsUsing(texture);
				texture.Dispose();
			}

			return context.Device.CreateTexture(new TextureDescriptor(width, height, TextureFormat.Rgba8Unorm, TextureUsage.TextureBinding | TextureUsage.CopyDst, 1, 1, label));
		}

		/// <summary>The texture a fill reads as its image.</summary>
		/// <param name="Texture">The image's texels.</param>
		/// <param name="Width">Its width in texels.</param>
		/// <param name="Height">Its height in texels.</param>
		/// <param name="Scale">Texels per image pixel.</param>
		/// <param name="IsLayer">A retained layer's texture: premultiplied, and row 0 is the picture's top (see
		/// <see cref="GpuRenderTarget"/>), where an uploaded image is straight alpha with row 0 at the bottom.</param>
		private readonly record struct Source(IGpuTexture Texture, int Width, int Height, int Scale, bool IsLayer);

		/// <summary>One context's coverage layer, image, weight table and uniform, reused from call to call and released with the context.</summary>
		private sealed class Resources : IDisposable
		{
			public bool InUse;

			public IGpuTexture Coverage;

			public IGpuTexture Image;

			public IGpuTexture Weights;

			public IGpuBuffer Uniform;

			public IGpuTexture AlphaTable;

			// GpuImageFilterSpans' table, for the shader's span_interpolator_linear.
			public IGpuBuffer Spans;

			// The ImageBuffer Image holds and its ChangedCount then, so drawing an unchanged image again skips the
			// upload. Weak, so the cache does not keep a caller's picture alive with the context.
			private readonly WeakReference<ImageBuffer> uploaded = new WeakReference<ImageBuffer>(null);

			private int uploadedChangedCount;

			/// <summary>Puts <paramref name="image"/> in <see cref="Image"/>, unless it is already there unchanged.</summary>
			public void UploadImage(GlCompatContext context, IImageByte image)
			{
				var buffer = image as ImageBuffer;
				if (buffer != null && this.Image != null
					&& this.uploaded.TryGetTarget(out var last) && ReferenceEquals(last, buffer)
					&& this.uploadedChangedCount == buffer.ChangedCount)
				{
					return;
				}

				this.Image = Upload(context, this.Image, image);
				this.uploaded.SetTarget(buffer);
				this.uploadedChangedCount = buffer?.ChangedCount ?? 0;
			}

			public void Dispose()
			{
				this.AlphaTable?.Dispose();
				this.AlphaTable = null;
				this.Spans?.Dispose();
				this.Spans = null;
				this.uploaded.SetTarget(null);
				this.Coverage?.Dispose();
				this.Image?.Dispose();
				this.Weights?.Dispose();
				this.Uniform?.Dispose();
				this.Coverage = null;
				this.Image = null;
				this.Weights = null;
				this.Uniform = null;
			}
		}
	}
}
