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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="IBlurGraphics.BlurBox"/>: the region drawn in white into a coverage
	/// layer, the box under it copied out of the target, blurred by <c>GpuRegionBlur.wgsl</c> in the software blur's
	/// own arithmetic (compute passes along the rows and, for the recursive blur, the columns, into a storage buffer
	/// of packed bytes), and shown by a pass that finishes the columns and mixes the result over the copy by the coverage.
	/// </summary>
	/// <remarks>
	/// The integer blurs (stack, 3x3 box) match software byte for byte; the float ones (recursive, slight) sum in
	/// f32 where software uses doubles, so a sum landing within a hair of a half can round the other way. The
	/// recursive blur runs one row or column per invocation, the IIR being serial along its line, in batches of lines
	/// whose unrounded causal pass fits the device's storage binding limit. The target must
	/// allow copies (<see cref="TextureUsage.CopySrc"/>), as for <see cref="GpuBlur.BlurUnder"/>.
	/// </remarks>
	internal static class GpuRegionBlur
	{
		/// <summary>The WGSL module of the blur passes (the backend's <c>GpuRegionBlur.wgsl</c>).</summary>
		internal const string ShaderModuleKey = "GpuRegionBlur";

		private const int UniformSize = 80;

		// Packed rgb bytes: every value kept between passes is a rounded byte, so 4 bytes lose nothing.
		private const int PixelSize = 4;

		// The recursive blur's causal pass, a vec4<f32> a pixel, before its anti-causal pass rounds it.
		private const int ScratchPixelSize = 16;

		private static readonly ConditionalWeakTable<GlCompatContext, Resources> ResourcesByContext = new ConditionalWeakTable<GlCompatContext, Resources>();

		public static void Blur(Graphics2DGpu graphics, IVertexSource region, double radius, BlurKind kind, ColorChannels channels)
		{
			if (region == null)
			{
				throw new ArgumentNullException(nameof(region));
			}

			var gl = graphics.gl;
			if (!(gl?.GpuContext is GlCompatContext context) || context.Passes.ColorTarget == null)
			{
				throw new NotSupportedException("BlurBox needs a GPU render target.");
			}

			context.Passes.RejectLinearLight("BlurBox");

			var target = context.Passes.ColorTarget;
			var descriptor = target.Descriptor;
			if ((descriptor.Usage & TextureUsage.CopySrc) == 0)
			{
				throw new NotSupportedException("BlurBox needs a render target that allows copies (CopySrc).");
			}

			int scale = context.CoordinateScale;
			double deviceRadius = radius * scale;
			var slight = new SlightBlur(deviceRadius);
			bool nothingToDo = (channels & ColorChannels.All & ~ColorChannels.Alpha) == 0
				|| (kind == BlurKind.Stack && Util.uround(deviceRadius) < 1)
				|| (kind == BlurKind.Recursive && deviceRadius < 0.62)
				|| (kind == BlurKind.Slight && deviceRadius <= 0);
			var bounds = new VertexSourceApplyTransform(region, graphics.GetTransform()).GetBounds();
			if (nothingToDo || bounds.Width <= 0 || bounds.Height <= 0)
			{
				return;
			}

			var resources = ResourcesByContext.GetValue(context, c => c.Own(new Resources()));
			if (resources.InUse)
			{
				throw new NotSupportedException("BlurBox cannot be nested.");
			}

			var state = context.State;
			GlViewportRect? viewport = state.ViewportSet ? state.Viewport : (GlViewportRect?)null;
			resources.InUse = true;
			var redirect = GpuTargetRedirect.SaveCurrent(gl, context);
			try
			{
				resources.Coverage = GpuBlur.Layers.Fit(context, resources.Coverage, descriptor, "GpuRegionBlurCoverage");
				redirect.RedirectTo(resources.Coverage, null, scale, (int)descriptor.Width / scale, (int)descriptor.Height / scale);
				if (viewport.HasValue)
				{
					gl.Viewport(viewport.Value.X, viewport.Value.Y, viewport.Value.Width, viewport.Value.Height);
				}

				graphics.Render(region, Color.White);
			}
			finally
			{
				redirect.Restore();
				resources.InUse = false;
			}

			// The box: the device pixels the region's bounds touch, found through the viewport the ortho projection
			// maps the Graphics2D's pixels onto (the 3x3 box reads a pixel ring past them), clipped to the target.
			GlViewportRect view = viewport ?? new GlViewportRect(0, 0, (int)descriptor.Width / scale, (int)descriptor.Height / scale);
			double toDeviceX = view.Width * (double)scale / graphics.Width;
			double toDeviceY = view.Height * (double)scale / graphics.Height;
			int reach = kind == BlurKind.Box3x3 ? 1 : 0;
			int left = (int)Math.Floor((view.X * scale) + (bounds.Left * toDeviceX)) - reach;
			int right = (int)Math.Ceiling((view.X * scale) + (bounds.Right * toDeviceX)) + reach;
			int bottomUp = (int)Math.Floor((view.Y * scale) + (bounds.Bottom * toDeviceY)) - reach;
			int topUp = (int)Math.Ceiling((view.Y * scale) + (bounds.Top * toDeviceY)) + reach;
			var box = Intersect((left, (int)descriptor.Height - topUp, right - left, topUp - bottomUp), (0, 0, (int)descriptor.Width, (int)descriptor.Height));
			var show = box;
			if (context.State.ScissorEnabled)
			{
				show = Intersect(show, context.ToDeviceRect(context.State.Scissor));
			}

			if (show.Width <= 0 || show.Height <= 0
				|| (kind == BlurKind.Slight && (box.Width < 3 || box.Height < 3)))
			{
				return;
			}

			context.Passes.EnsurePassOpen();
			context.FlushPass();

			var device = context.Device;
			resources.Frame = GpuBlur.Layers.Fit(context, resources.Frame, descriptor, "GpuRegionBlurFrame", TextureUsage.CopyDst);
			device.CopyTextureToTexture(target, resources.Frame, box.X, box.Y, box.Width, box.Height);

			// What one storage binding may see, asked of the device: the WebGPU default is 128 MiB, which a 4K box of
			// floats would pass.
			ulong bindingLimit = Math.Min(device.Limits.MaxStorageBufferBindingSize, device.Limits.MaxBufferSize);
			ulong pixelBytes = (ulong)Math.Max(1, box.Width * box.Height) * PixelSize;
			if (pixelBytes > bindingLimit)
			{
				throw new NotSupportedException($"BlurBox's {box.Width}x{box.Height} box needs {pixelBytes:N0} bytes, past this device's {bindingLimit:N0} byte storage binding limit.");
			}

			resources.Pixels = Fit(device, resources.Pixels, ref resources.PixelCapacity, pixelBytes);

			// Recursive: as many rows (then columns) a batch as the binding limit allows room for.
			int rowsPerBatch = 0, columnsPerBatch = 0;
			ulong scratchBytes = 0;
			if (kind == BlurKind.Recursive)
			{
				rowsPerBatch = LinesPerBatch(box.Width, box.Height, bindingLimit);
				columnsPerBatch = LinesPerBatch(box.Height, box.Width, bindingLimit);
				scratchBytes = Math.Max((ulong)rowsPerBatch * (ulong)box.Width, (ulong)columnsPerBatch * (ulong)box.Height) * ScratchPixelSize;
				resources.Scratch = Fit(device, resources.Scratch, ref resources.ScratchCapacity, scratchBytes);
			}

			resources.Uniform ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			device.WriteBuffer(resources.Uniform, 0, Settings(box, kind, deviceRadius, channels, slight, (0, 0)));

			resources.EnsurePipelines(context);
			var computeEntries = new[]
			{
				BindGroupEntry.ForTexture(0, resources.Frame),
				BindGroupEntry.ForBuffer(2, resources.Uniform),
				BindGroupEntry.ForBuffer(3, resources.Pixels, 0, pixelBytes),
			};

			var showPipeline = ShowPipeline(context, descriptor.Format);
			// The recursive entry points share one layout (with the scratch), so one group serves them both.
			IBindGroup computeGroup = null;
			if (kind == BlurKind.Recursive)
			{
				var recursiveEntries = new[] { computeEntries[0], computeEntries[1], computeEntries[2], BindGroupEntry.ForBuffer(5, resources.Scratch, 0, scratchBytes) };
				computeGroup = device.CreateBindGroup(new BindGroupDescriptor(resources.AcrossRows, 0, recursiveEntries, "GpuRegionBlurRecursive"));
			}
			else if (kind != BlurKind.Box3x3)
			{
				computeGroup = device.CreateBindGroup(new BindGroupDescriptor(resources.AcrossPixels, 0, computeEntries, "GpuRegionBlurCompute"));
			}
			var showGroup = device.CreateBindGroup(new BindGroupDescriptor(
				showPipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, resources.Frame),
					BindGroupEntry.ForTexture(1, resources.Coverage),
					BindGroupEntry.ForBuffer(2, resources.Uniform),
					BindGroupEntry.ForBuffer(4, resources.Pixels, 0, pixelBytes),
				},
				"GpuRegionBlurShow"));
			try
			{
				if (kind == BlurKind.Recursive)
				{
					// Every batch but the last is submitted before the next rewrites the uniforms' batch lines (a queue
					// write lands ahead of anything not yet submitted); the columns start once all the rows are done.
					var batches = new List<(IComputePipeline Pipeline, int First, int End, string Label)>();
					for (int row = 0; row < box.Height; row += rowsPerBatch)
					{
						batches.Add((resources.AcrossRows, row, Math.Min(box.Height, row + rowsPerBatch), "GpuRegionBlurRows"));
					}

					for (int column = 0; column < box.Width; column += columnsPerBatch)
					{
						batches.Add((resources.DownColumns, column, Math.Min(box.Width, column + columnsPerBatch), "GpuRegionBlurColumns"));
					}

					for (int i = 0; i < batches.Count; i++)
					{
						var batch = batches[i];
						if (i > 0)
						{
							context.Submit();
						}

						device.WriteBuffer(resources.Uniform, 0, Settings(box, kind, deviceRadius, channels, slight, (batch.First, batch.End)));
						using (var compute = device.BeginComputePass(batch.Label))
						{
							compute.SetPipeline(batch.Pipeline);
							compute.SetBindGroup(0, computeGroup);
							compute.Dispatch((uint)((batch.End - batch.First + 63) / 64));
						}
					}
				}
				else if (kind != BlurKind.Box3x3)
				{
					using (var compute = device.BeginComputePass("GpuRegionBlurAcross"))
					{
						compute.SetPipeline(resources.AcrossPixels);
						compute.SetBindGroup(0, computeGroup);
						compute.Dispatch((uint)((box.Width + 7) / 8), (uint)((box.Height + 7) / 8));
					}
				}

				using (var encoder = device.BeginRenderPass(new RenderPassDescriptor(
					new[] { new ColorAttachment(target, LoadOp.Load) },
					DepthAttachment.None,
					"GpuRegionBlurShow")))
				{
					encoder.SetPipeline(showPipeline);
					encoder.SetBindGroup(0, showGroup);
					encoder.SetScissor(show.X, show.Y, show.Width, show.Height);
					encoder.Draw(3);
				}

				// The uniforms, the copy and the buffer are rewritten by the next blur; submitting now keeps those
				// writes from overtaking these passes in the queue.
				context.Submit();
			}
			finally
			{
				computeGroup?.Dispose();
				showGroup.Dispose();
			}
		}

		/// <summary>How many lines of <paramref name="length"/> pixels one scratch binding holds, at most all <paramref name="count"/>.</summary>
		private static int LinesPerBatch(int length, int count, ulong bindingLimit)
		{
			ulong lineBytes = (ulong)Math.Max(1, length) * ScratchPixelSize;
			if (lineBytes > bindingLimit)
			{
				throw new NotSupportedException($"BlurBox's {length} pixel line needs {lineBytes:N0} bytes, past this device's {bindingLimit:N0} byte storage binding limit.");
			}

			return (int)Math.Min((ulong)Math.Max(1, count), bindingLimit / lineBytes);
		}

		/// <summary>The storage buffer <paramref name="buffer"/>, replaced by a bigger one if it holds fewer than <paramref name="bytes"/>.</summary>
		private static IGpuBuffer Fit(IRenderDevice device, IGpuBuffer buffer, ref ulong capacity, ulong bytes)
		{
			if (buffer != null && capacity >= bytes)
			{
				return buffer;
			}

			buffer?.Dispose();
			capacity = bytes;
			return device.CreateBuffer(BufferUsage.Storage, bytes);
		}

		private static byte[] Settings((int X, int Y, int Width, int Height) box, BlurKind kind, double radius, ColorChannels channels, SlightBlur slight, (int First, int End) lines)
		{
			var bytes = new byte[UniformSize];
			var span = bytes.AsSpan();
			int stackRadius = Util.uround(radius);
			(int mul, int shr) = stack_blur.DivisionTable(stackRadius);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(0), box.X);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(4), box.Y);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(8), box.Width);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(12), box.Height);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(16), (int)kind);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(20), stackRadius);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(24), mul);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(28), shr);
			BinaryPrimitives.WriteSingleLittleEndian(span.Slice(32), (channels & ColorChannels.Red) != 0 ? 1 : 0);
			BinaryPrimitives.WriteSingleLittleEndian(span.Slice(36), (channels & ColorChannels.Green) != 0 ? 1 : 0);
			BinaryPrimitives.WriteSingleLittleEndian(span.Slice(40), (channels & ColorChannels.Blue) != 0 ? 1 : 0);
			if (kind == BlurKind.Recursive)
			{
				(double b, double b1, double b2, double b3) = RecursiveBlur.Coefficients(radius);
				BinaryPrimitives.WriteSingleLittleEndian(span.Slice(48), (float)b);
				BinaryPrimitives.WriteSingleLittleEndian(span.Slice(52), (float)b1);
				BinaryPrimitives.WriteSingleLittleEndian(span.Slice(56), (float)b2);
				BinaryPrimitives.WriteSingleLittleEndian(span.Slice(60), (float)b3);
			}
			else
			{
				BinaryPrimitives.WriteSingleLittleEndian(span.Slice(48), (float)slight.CenterWeight);
				BinaryPrimitives.WriteSingleLittleEndian(span.Slice(52), (float)slight.NeighborWeight);
			}

			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(64), lines.First);
			BinaryPrimitives.WriteInt32LittleEndian(span.Slice(68), lines.End);

			return bytes;
		}

		private static IRenderPipeline ShowPipeline(GlCompatContext context, TextureFormat format)
		{
			var cache = context.Pipelines;
			var module = cache.GetShaderModule(ShaderModuleKey);
			var replace = new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.Zero);
			return cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"showMain",
				Array.Empty<VertexBufferLayout>(),
				new[] { new ColorTargetState(format, false, replace, replace) },
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 2, ShaderStage.Fragment, BindingType.UniformBuffer),
					new BindGroupLayoutEntry(0, 4, ShaderStage.Fragment, BindingType.ReadOnlyStorageBuffer),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"GpuRegionBlurShow"));
		}

		private static (int X, int Y, int Width, int Height) Intersect((int X, int Y, int Width, int Height) a, (int X, int Y, int Width, int Height) b)
		{
			int x0 = Math.Max(a.X, b.X);
			int y0 = Math.Max(a.Y, b.Y);
			int x1 = Math.Min(a.X + a.Width, b.X + b.Width);
			int y1 = Math.Min(a.Y + a.Height, b.Y + b.Height);
			return (x0, y0, Math.Max(0, x1 - x0), Math.Max(0, y1 - y0));
		}

		/// <summary>One context's coverage layer, frame copy, pixel buffer, uniforms and compute pipelines, reused from call to call.</summary>
		internal sealed class Resources : IDisposable
		{
			public bool InUse;

			public IGpuTexture Coverage;

			public IGpuTexture Frame;

			public IGpuBuffer Pixels;

			public ulong PixelCapacity;

			public IGpuBuffer Scratch;

			public ulong ScratchCapacity;

			public IGpuBuffer Uniform;

			public IComputePipeline AcrossPixels;

			public IComputePipeline AcrossRows;

			public IComputePipeline DownColumns;

			public void EnsurePipelines(GlCompatContext context)
			{
				if (this.AcrossPixels != null)
				{
					return;
				}

				var module = context.Pipelines.GetShaderModule(ShaderModuleKey);
				var layout = new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Compute, BindingType.Texture),
					new BindGroupLayoutEntry(0, 2, ShaderStage.Compute, BindingType.UniformBuffer),
					new BindGroupLayoutEntry(0, 3, ShaderStage.Compute, BindingType.StorageBuffer),
				};

				var recursiveLayout = new[] { layout[0], layout[1], layout[2], new BindGroupLayoutEntry(0, 5, ShaderStage.Compute, BindingType.StorageBuffer) };

				var device = context.Device;
				this.AcrossPixels = device.CreateComputePipeline(new ComputePipelineDescriptor(module, "acrossPixels", layout, "GpuRegionBlurAcross"));
				this.AcrossRows = device.CreateComputePipeline(new ComputePipelineDescriptor(module, "acrossRows", recursiveLayout, "GpuRegionBlurRows"));
				this.DownColumns = device.CreateComputePipeline(new ComputePipelineDescriptor(module, "downColumns", recursiveLayout, "GpuRegionBlurColumns"));
			}

			/// <summary>Releases the resources with their context (<see cref="GlCompatContext.Own"/>).</summary>
			public void Dispose()
			{
				this.Coverage?.Dispose();
				this.Frame?.Dispose();
				this.Pixels?.Dispose();
				this.Scratch?.Dispose();
				this.Uniform?.Dispose();
				this.AcrossPixels?.Dispose();
				this.AcrossRows?.Dispose();
				this.DownColumns?.Dispose();
			}
		}
	}
}
