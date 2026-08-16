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
using MatterHackers.RenderGl.Compat;
using MatterHackers.VectorMath;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The Phase 0 triangle spike, re-shot through <see cref="IRenderDevice"/> instead of raw P/Invoke:
	/// create a buffer, a shader module, a pipeline, a bind group, open a pass, draw, submit, read back,
	/// look at pixels. If this passes, the seam really is a pass-through to webgpu.h.
	/// <para>
	/// It also pins the two things that are easy to get wrong and impossible to see afterwards: that the
	/// canned WGSL agrees with <see cref="GlUniformBlock"/>'s published offsets (the uniform written here
	/// is built with that class, not by hand), and that the vertex layout in
	/// <see cref="GlShaderKeys"/> is the layout the shader actually declares.
	/// </para>
	/// </summary>
	[NotInParallel]
	public class WebGpuRenderDeviceTests
	{
		private const uint RenderSize = 64;

		[Test]
		public async Task ATriangleDrawnThroughTheSeamReadsBackAsPixels()
		{
			using (var harness = WebGpuRenderTestHarness.Create(RenderSize, RenderSize))
			{
				var device = harness.Device;

				// Positions are already in clip space, so the uniform block carries identity matrices -
				// which still exercises the vec * mat multiplication order, because a transposed identity
				// is still an identity only if the layout is right.
				var uniformBytes = new byte[GlUniformBlock.SizeInBytes];
				GlUniformBlock.WriteMatrix(uniformBytes, GlUniformBlock.ModelViewMatrixOffset, Matrix4X4.Identity);
				GlUniformBlock.WriteMatrix(uniformBytes, GlUniformBlock.ProjectionMatrixOffset, Matrix4X4.Identity);
				GlUniformBlock.WriteMatrix(uniformBytes, GlUniformBlock.TextureMatrixOffset, Matrix4X4.Identity);

				var uniformBuffer = device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, GlUniformBlock.SizeInBytes);
				device.WriteBuffer(uniformBuffer, 0, uniformBytes);

				var vertexBuffer = device.CreateBuffer(
					BufferUsage.Vertex | BufferUsage.CopyDst,
					3 * GlShaderKeys.ColoredVertexLayout.ArrayStride,
					ColoredTriangle());

				var module = device.CreateShaderModule(GlShaderKeys.PositionColor);
				var pipeline = device.CreateRenderPipeline(new RenderPipelineDescriptor(
					module,
					GlShaderKeys.VertexEntryPoint,
					module,
					GlShaderKeys.SmoothFragmentEntryPoint,
					new[] { GlShaderKeys.ColoredVertexLayout },
					new[] { new ColorTargetState(harness.Target.Descriptor.Format) },
					GlShaderKeys.UntexturedBindGroupLayout,
					DepthStencilState.None,
					PrimitiveTopology.TriangleList,
					CullMode.None,
					FrontFace.Ccw,
					1,
					"deviceTriangle"));

				var bindGroup = device.CreateBindGroup(new BindGroupDescriptor(
					pipeline,
					GlShaderKeys.BindGroupIndex,
					new[] { BindGroupEntry.ForBuffer(GlShaderKeys.UniformBinding, uniformBuffer, 0, GlUniformBlock.SizeInBytes) }));

				using (var encoder = device.BeginRenderPass(new RenderPassDescriptor(
					harness.Target,
					LoadOp.Clear,
					new ClearColor(0, 0, 1, 1),
					"deviceTrianglePass")))
				{
					encoder.SetPipeline(pipeline);
					encoder.SetBindGroup((int)GlShaderKeys.BindGroupIndex, bindGroup);
					encoder.SetVertexBuffer(0, vertexBuffer);
					encoder.Draw(3);
				}

				var image = await harness.ReadAsync();

				// The corner is well outside the triangle and the centre well inside it, so neither
				// assertion depends on a rasterization edge rule.
				await Assert.That(image.PixelAt(0, 0)).IsEqualTo(ReadbackImage.Rgba(0, 0, 255, 255));
				await Assert.That(image.PixelAt((int)RenderSize / 2, (int)RenderSize / 2))
					.IsEqualTo(ReadbackImage.Rgba(255, 0, 0, 255));

				// wgpu reports validation failures out of band, so a clean-looking render can still have
				// been built from a rejected descriptor. Asserting this is how that shows up as a failure.
				await Assert.That(device.LastUncapturedError).IsNull();
				await Assert.That(device.AdapterBackend).IsEqualTo(WGPUBackendType.D3D12);
				await Assert.That(device.IsDeviceLost).IsFalse();

				bindGroup.Dispose();
				pipeline.Dispose();
				module.Dispose();
				vertexBuffer.Dispose();
				uniformBuffer.Dispose();
			}
		}

		[Test]
		public async Task ReadbackReportsThePaddedRowStrideRatherThanTheTightOne()
		{
			// 65 Rgba8 pixels is 260 bytes tightly packed, which WebGPU pads to 512. A caller walking the
			// destination as if it were 260 would shear the image by 63 pixels per row, so the reported
			// stride is the whole contract of the readback.
			using (var harness = WebGpuRenderTestHarness.Create(65, 4))
			{
				using (harness.Device.BeginRenderPass(new RenderPassDescriptor(
					harness.Target,
					LoadOp.Clear,
					new ClearColor(0, 1, 0, 1),
					"stridePass")))
				{
				}

				var image = await harness.ReadAsync();

				await Assert.That(image.RowStride).IsEqualTo(512u);
				await Assert.That(image.PixelAt(64, 3)).IsEqualTo(ReadbackImage.Rgba(0, 255, 0, 255));
				await Assert.That(harness.Device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		public async Task EverythingAPassForbidsThrowsWhileOneIsOpen()
		{
			using (var harness = WebGpuRenderTestHarness.Create(16, 16))
			{
				var device = harness.Device;
				var encoder = device.BeginRenderPass(new RenderPassDescriptor(harness.Target, LoadOp.Clear, ClearColor.Black, "openPass"));

				try
				{
					await Assert.That(() => device.Submit()).Throws<InvalidOperationException>();
					// The pass rule is checked before the surface is even looked at, so an open pass
					// wins over the foreign-surface complaint.
					await Assert.That(() => device.Present(new UnsupportedSurface())).Throws<InvalidOperationException>();
					await Assert.That(() => device.BeginRenderPass(new RenderPassDescriptor(harness.Target)))
						.Throws<InvalidOperationException>();

					var destination = new byte[TextureFormatInfo.AlignedRowStride(TextureFormat.Rgba8Unorm, 16) * 16];
					await Assert.That(async () => await device.ReadTextureAsync(harness.Target, destination))
						.Throws<InvalidOperationException>();
				}
				finally
				{
					encoder.Dispose();
				}

				// And the rules relax again once the pass ends: the same calls now work.
				device.Submit();
				await Assert.That(device.OpenPass).IsNull();
			}
		}

		[Test]
		public async Task AnEndedPassRefusesFurtherWorkAndAForeignSurfaceIsRefused()
		{
			using (var harness = WebGpuRenderTestHarness.Create(16, 16))
			{
				var encoder = harness.Device.BeginRenderPass(new RenderPassDescriptor(harness.Target, LoadOp.Clear, ClearColor.Black));
				encoder.Dispose();

				// Disposing twice is a no-op, matching RecordingRenderEncoder, so a helper's using block
				// can sit inside a caller's.
				encoder.Dispose();

				await Assert.That(() => encoder.Draw(3)).Throws<InvalidOperationException>();
				await Assert.That(() => encoder.SetScissor(0, 0, 8, 8)).Throws<InvalidOperationException>();

				// Presenting is real now, but only for a swapchain this device made: anything else is a
				// resource from another device and is refused rather than reinterpreted.
				await Assert.That(() => harness.Device.Present(new UnsupportedSurface())).Throws<ArgumentException>();
			}
		}

		[Test]
		public async Task ANegativeScissorIsRefusedRatherThanWrappingAround()
		{
			using (var harness = WebGpuRenderTestHarness.Create(16, 16))
			{
				using (var encoder = harness.Device.BeginRenderPass(new RenderPassDescriptor(harness.Target, LoadOp.Clear, ClearColor.Black)))
				{
					// WebGPU scissors are unsigned; passing -1 through would become 4294967295 and fail
					// validation out of band, several frames from where the mistake was made.
					await Assert.That(() => encoder.SetScissor(-1, 0, 8, 8)).Throws<ArgumentOutOfRangeException>();
				}
			}
		}

		[Test]
		public async Task AnUnknownShaderKeyIsRejected()
		{
			using (var harness = WebGpuRenderTestHarness.Create(16, 16))
			{
				await Assert.That(() => harness.Device.CreateShaderModule("NoSuchShader")).Throws<ArgumentException>();
			}
		}

		/// <summary>
		/// Three clip-space vertices in the canned colored layout: position (float3) then color (float4),
		/// 28 bytes apart. Built by hand rather than through the compat layer so a layout mistake shows up
		/// here and not only in the integration suite.
		/// </summary>
		private static byte[] ColoredTriangle()
		{
			var vertices = new[]
			{
				new[] { 0.0f, 0.8f, 0.0f, 1f, 0f, 0f, 1f },
				new[] { -0.8f, -0.8f, 0.0f, 1f, 0f, 0f, 1f },
				new[] { 0.8f, -0.8f, 0.0f, 1f, 0f, 0f, 1f },
			};

			int stride = (int)GlShaderKeys.ColoredVertexLayout.ArrayStride;
			var bytes = new byte[vertices.Length * stride];
			for (int vertex = 0; vertex < vertices.Length; vertex++)
			{
				for (int component = 0; component < vertices[vertex].Length; component++)
				{
					BitConverter.GetBytes(vertices[vertex][component])
						.CopyTo(bytes, (vertex * stride) + (component * 4));
				}
			}

			return bytes;
		}

		/// <summary>A surface the device has never seen, only to prove Present refuses it.</summary>
		private class UnsupportedSurface : ISurfaceTarget
		{
			public string Label => "unsupported";

			public TextureFormat Format => TextureFormat.Bgra8Unorm;

			public uint Width => 16;

			public uint Height => 16;

			public IGpuTexture AcquireCurrentTexture() => throw new NotSupportedException();

			public void Dispose()
			{
			}
		}
	}
}
