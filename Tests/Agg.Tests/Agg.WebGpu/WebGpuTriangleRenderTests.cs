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
using System.IO;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.WebGpu;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The offscreen half of the Phase 0 triangle spike: proves the generated binding can drive a real
	/// GPU end to end - adapter, device, WGSL shader module, render pipeline, render pass, and a
	/// texture-to-buffer readback - without any window. This is the shape the eventual
	/// <c>WebGpuRenderDevice</c> readback path takes, so the awkward parts (256 byte row alignment,
	/// unmanaged callbacks, poll-until-mapped) get exercised here first.
	/// </summary>
	public class WebGpuTriangleRenderTests
	{
		private const int RenderSize = 256;

		// Deliberately exact in 8 bit: a channel that is 0 or 255 cannot be confused with rounding.
		private static readonly byte[] ClearRgba = { 0, 0, 255, 255 };

		private static readonly byte[] TriangleRgba = { 255, 0, 0, 255 };

		private const string TriangleWgsl = @"
@vertex
fn vs_main(@builtin(vertex_index) index : u32) -> @builtin(position) vec4<f32>
{
	var positions = array<vec2<f32>, 3>(
		vec2<f32>( 0.0,  0.8),
		vec2<f32>(-0.8, -0.8),
		vec2<f32>( 0.8, -0.8));
	return vec4<f32>(positions[index], 0.0, 1.0);
}

@fragment
fn fs_main() -> @location(0) vec4<f32>
{
	return vec4<f32>(1.0, 0.0, 0.0, 1.0);
}
";

		/// <summary>
		/// Set by <see cref="OnUncapturedError"/>. wgpu reports validation failures out of band rather
		/// than by failing the call that caused them, so without this a bad descriptor shows up only as a
		/// blank image much later.
		/// <para>
		/// Static because the uncaptured error callback is an <see cref="UnmanagedCallersOnlyAttribute"/>
		/// entry point, which cannot close over anything; routing per device state through the callback's
		/// userdata slot would mean a pinned handle that outlives the device. That makes this process-wide
		/// state, so it is only correct while one test in this class holds a device at a time - keep it
		/// that way, or pay for the userdata plumbing.
		/// </para>
		/// </summary>
		private static string uncapturedError;

		[Test]
		public async Task OffscreenTriangleRendersAndReadsBack()
		{
			uncapturedError = null;

			var (image, backend, adapterName) = RenderTriangleOffscreen();

			Console.WriteLine($"wgpu adapter: {adapterName} (backend {backend})");

			string pngPath = Path.Combine(Path.GetTempPath(), "MatterCADTests", "WebGpuOffscreenTriangle.png");
			Directory.CreateDirectory(Path.GetDirectoryName(pngPath));
			// ImageIO.SaveImageData refuses to overwrite, so the artifact would otherwise go stale.
			File.Delete(pngPath);
			ImageIO.SaveImageData(pngPath, image);
			Console.WriteLine($"wgpu triangle written to {pngPath}");

			await Assert.That(uncapturedError).IsNull();
			await Assert.That(backend).IsEqualTo(WGPUBackendType.D3D12);
			await Assert.That(File.Exists(pngPath)).IsTrue();

			// The corner is well outside the triangle and the centre is well inside it, so neither
			// depends on rasterization edge rules.
			await Assert.That(PixelAt(image, 0, 0)).IsEqualTo(Describe(ClearRgba));
			await Assert.That(PixelAt(image, RenderSize / 2, RenderSize / 2)).IsEqualTo(Describe(TriangleRgba));
		}

		/// <summary>
		/// Reads a pixel as "R,G,B,A" - a string so a mismatch reports both colours rather than "arrays
		/// differ". The <see cref="ImageBuffer"/> is agg native (BGRA bytes, origin at the bottom left),
		/// so both the channel order and the row are flipped back here.
		/// </summary>
		private static string PixelAt(ImageBuffer image, int x, int y)
		{
			var buffer = image.GetBuffer();
			int offset = image.GetBufferOffsetXY(x, image.Height - 1 - y);
			return Describe(new[] { buffer[offset + 2], buffer[offset + 1], buffer[offset + 0], buffer[offset + 3] });
		}

		private static string Describe(byte[] rgba) => string.Join(",", rgba);

		private static unsafe (ImageBuffer Image, WGPUBackendType Backend, string AdapterName) RenderTriangleOffscreen()
		{
			WGPUInstance instance = wgpuCreateInstance(null);
			if (instance.IsNull)
			{
				throw new InvalidOperationException("wgpuCreateInstance returned null");
			}

			try
			{
				WGPUAdapter adapter = RequestAdapter(instance, WGPUBackendType.D3D12);
				try
				{
					var info = default(WGPUAdapterInfo);
					if (wgpuAdapterGetInfo(adapter, &info) != WGPUStatus.Success)
					{
						throw new InvalidOperationException("wgpuAdapterGetInfo failed");
					}

					// WGPUAdapterInfo owns wgpu-allocated strings, so everything wanted from it has to be
					// copied into managed memory before the members are freed.
					WGPUBackendType backendType;
					string adapterName;
					try
					{
						backendType = info.backendType;
						adapterName = ToManaged(info.device);
					}
					finally
					{
						wgpuAdapterInfoFreeMembers(info);
					}

					WGPUDevice device = RequestDevice(instance, adapter);
					try
					{
						return (RenderAndReadBack(instance, device), backendType, adapterName);
					}
					finally
					{
						wgpuDeviceRelease(device);
					}
				}
				finally
				{
					wgpuAdapterRelease(adapter);
				}
			}
			finally
			{
				wgpuInstanceRelease(instance);
			}
		}

		private static unsafe ImageBuffer RenderAndReadBack(WGPUInstance instance, WGPUDevice device)
		{
			WGPUQueue queue = wgpuDeviceGetQueue(device);
			WGPUShaderModule shaderModule = CreateWgslModule(device, TriangleWgsl);
			WGPURenderPipeline pipeline = CreateTrianglePipeline(device, shaderModule, WGPUTextureFormat.RGBA8Unorm);

			var textureDescriptor = new WGPUTextureDescriptor
			{
				label = NullString(),
				usage = WGPUTextureUsage.RenderAttachment | WGPUTextureUsage.CopySrc,
				dimension = WGPUTextureDimension._2D,
				size = new WGPUExtent3D { width = RenderSize, height = RenderSize, depthOrArrayLayers = 1 },
				format = WGPUTextureFormat.RGBA8Unorm,
				mipLevelCount = 1,
				sampleCount = 1,
			};

			WGPUTexture texture = wgpuDeviceCreateTexture(device, &textureDescriptor);
			WGPUTextureView view = wgpuTextureCreateView(texture, null);

			// WebGPU requires copyTextureToBuffer rows to be a multiple of 256 bytes; at 256 RGBA pixels
			// the stride happens to already be 1024, but the rounding is written out because every real
			// readback path has to do it.
			uint bytesPerRow = (uint)((RenderSize * 4 + 255) / 256 * 256);
			ulong readbackSize = (ulong)bytesPerRow * RenderSize;

			var bufferDescriptor = new WGPUBufferDescriptor
			{
				label = NullString(),
				usage = WGPUBufferUsage.CopyDst | WGPUBufferUsage.MapRead,
				size = readbackSize,
				mappedAtCreation = false,
			};

			WGPUBuffer readback = wgpuDeviceCreateBuffer(device, &bufferDescriptor);

			try
			{
				var encoderDescriptor = new WGPUCommandEncoderDescriptor { label = NullString() };
				WGPUCommandEncoder encoder = wgpuDeviceCreateCommandEncoder(device, &encoderDescriptor);

				var colorAttachment = new WGPURenderPassColorAttachment
				{
					view = view,
					depthSlice = WGPUConstants.WGPU_DEPTH_SLICE_UNDEFINED,
					loadOp = WGPULoadOp.Clear,
					storeOp = WGPUStoreOp.Store,
					clearValue = new WGPUColor { r = ClearRgba[0] / 255.0, g = ClearRgba[1] / 255.0, b = ClearRgba[2] / 255.0, a = ClearRgba[3] / 255.0 },
				};

				var passDescriptor = new WGPURenderPassDescriptor
				{
					label = NullString(),
					colorAttachmentCount = 1,
					colorAttachments = &colorAttachment,
				};

				WGPURenderPassEncoder pass = wgpuCommandEncoderBeginRenderPass(encoder, &passDescriptor);
				wgpuRenderPassEncoderSetPipeline(pass, pipeline);
				wgpuRenderPassEncoderDraw(pass, 3, 1, 0, 0);
				wgpuRenderPassEncoderEnd(pass);
				wgpuRenderPassEncoderRelease(pass);

				var source = new WGPUTexelCopyTextureInfo
				{
					texture = texture,
					mipLevel = 0,
					origin = default,
					aspect = WGPUTextureAspect.All,
				};

				var destination = new WGPUTexelCopyBufferInfo
				{
					layout = new WGPUTexelCopyBufferLayout { offset = 0, bytesPerRow = bytesPerRow, rowsPerImage = RenderSize },
					buffer = readback,
				};

				var copySize = new WGPUExtent3D { width = RenderSize, height = RenderSize, depthOrArrayLayers = 1 };
				wgpuCommandEncoderCopyTextureToBuffer(encoder, &source, &destination, &copySize);

				var commandBufferDescriptor = new WGPUCommandBufferDescriptor { label = NullString() };
				WGPUCommandBuffer commands = wgpuCommandEncoderFinish(encoder, &commandBufferDescriptor);
				wgpuQueueSubmit(queue, 1, &commands);
				wgpuCommandBufferRelease(commands);
				wgpuCommandEncoderRelease(encoder);

				return MapAndCopyToImage(instance, device, readback, bytesPerRow, readbackSize);
			}
			finally
			{
				wgpuBufferRelease(readback);
				wgpuTextureViewRelease(view);
				wgpuTextureRelease(texture);
				wgpuRenderPipelineRelease(pipeline);
				wgpuShaderModuleRelease(shaderModule);
				wgpuQueueRelease(queue);
			}
		}

		private static unsafe ImageBuffer MapAndCopyToImage(WGPUInstance instance, WGPUDevice device, WGPUBuffer readback, uint bytesPerRow, ulong readbackSize)
		{
			var mapResult = new MapResult { Status = 0 };
			var callbackInfo = new WGPUBufferMapCallbackInfo
			{
				mode = WGPUCallbackMode.AllowProcessEvents,
				callback = &OnBufferMapped,
				userdata1 = &mapResult,
			};

			wgpuBufferMapAsync(readback, WGPUMapMode.Read, 0, (nuint)readbackSize, callbackInfo);

			// Native only test code, so blocking on the GPU is fine here; the shipping IRenderDevice
			// readback is async because the browser has no equivalent of DevicePoll(wait: true).
			for (int spin = 0; spin < 1000 && mapResult.Status == 0; spin++)
			{
				WgpuNative.wgpuDevicePoll(device, true, null);
				wgpuInstanceProcessEvents(instance);
			}

			if (mapResult.Status != (int)WGPUMapAsyncStatus.Success)
			{
				throw new InvalidOperationException($"wgpuBufferMapAsync did not succeed (status {mapResult.Status})");
			}

			var mapped = (byte*)wgpuBufferGetConstMappedRange(readback, 0, (nuint)readbackSize);
			if (mapped == null)
			{
				throw new InvalidOperationException("wgpuBufferGetConstMappedRange returned null");
			}

			var image = new ImageBuffer(RenderSize, RenderSize, 32, new BlenderBGRA());
			var destination = image.GetBuffer();

			// Unmap in a finally: releasing a still-mapped buffer is undefined, so a throw out of the copy
			// must not be able to skip it.
			try
			{
				for (int y = 0; y < RenderSize; y++)
				{
					// wgpu's rows run top down, agg's run bottom up.
					byte* row = mapped + (long)y * bytesPerRow;
					int destinationOffset = image.GetBufferOffsetY(RenderSize - 1 - y);
					for (int x = 0; x < RenderSize; x++)
					{
						destination[destinationOffset + (x * 4) + 0] = row[(x * 4) + 2];
						destination[destinationOffset + (x * 4) + 1] = row[(x * 4) + 1];
						destination[destinationOffset + (x * 4) + 2] = row[(x * 4) + 0];
						destination[destinationOffset + (x * 4) + 3] = row[(x * 4) + 3];
					}
				}
			}
			finally
			{
				wgpuBufferUnmap(readback);
			}

			return image;
		}

		private static unsafe WGPUAdapter RequestAdapter(WGPUInstance instance, WGPUBackendType preferredBackend)
		{
			var result = new AdapterResult();
			var options = new WGPURequestAdapterOptions
			{
				featureLevel = WGPUFeatureLevel.Core,
				powerPreference = WGPUPowerPreference.HighPerformance,
				backendType = preferredBackend,
			};

			var callbackInfo = new WGPURequestAdapterCallbackInfo
			{
				mode = WGPUCallbackMode.AllowProcessEvents,
				callback = &OnAdapterRequested,
				userdata1 = &result,
			};

			wgpuInstanceRequestAdapter(instance, &options, callbackInfo);

			for (int spin = 0; spin < 1000 && result.Status == 0; spin++)
			{
				wgpuInstanceProcessEvents(instance);
			}

			if (result.Status != (int)WGPURequestAdapterStatus.Success || result.Adapter.IsNull)
			{
				throw new InvalidOperationException($"wgpuInstanceRequestAdapter failed (status {result.Status})");
			}

			return result.Adapter;
		}

		private static unsafe WGPUDevice RequestDevice(WGPUInstance instance, WGPUAdapter adapter)
		{
			var result = new DeviceResult();
			var descriptor = new WGPUDeviceDescriptor
			{
				label = NullString(),
				defaultQueue = new WGPUQueueDescriptor { label = NullString() },
				uncapturedErrorCallbackInfo = new WGPUUncapturedErrorCallbackInfo { callback = &OnUncapturedError },
			};

			var callbackInfo = new WGPURequestDeviceCallbackInfo
			{
				mode = WGPUCallbackMode.AllowProcessEvents,
				callback = &OnDeviceRequested,
				userdata1 = &result,
			};

			wgpuAdapterRequestDevice(adapter, &descriptor, callbackInfo);

			for (int spin = 0; spin < 1000 && result.Status == 0; spin++)
			{
				wgpuInstanceProcessEvents(instance);
			}

			if (result.Status != (int)WGPURequestDeviceStatus.Success || result.Device.IsNull)
			{
				throw new InvalidOperationException($"wgpuAdapterRequestDevice failed (status {result.Status})");
			}

			return result.Device;
		}

		private static unsafe WGPUShaderModule CreateWgslModule(WGPUDevice device, string wgsl)
		{
			nint code = Marshal.StringToCoTaskMemUTF8(wgsl);
			try
			{
				var source = new WGPUShaderSourceWGSL
				{
					chain = new WGPUChainedStruct { sType = WGPUSType.ShaderSourceWGSL },
					code = NullTerminated(code),
				};

				var descriptor = new WGPUShaderModuleDescriptor
				{
					nextInChain = (WGPUChainedStruct*)&source,
					label = NullString(),
				};

				WGPUShaderModule module = wgpuDeviceCreateShaderModule(device, &descriptor);
				if (module.IsNull)
				{
					throw new InvalidOperationException("wgpuDeviceCreateShaderModule returned null");
				}

				return module;
			}
			finally
			{
				Marshal.FreeCoTaskMem(code);
			}
		}

		private static unsafe WGPURenderPipeline CreateTrianglePipeline(WGPUDevice device, WGPUShaderModule module, WGPUTextureFormat format)
		{
			nint vertexEntry = Marshal.StringToCoTaskMemUTF8("vs_main");
			nint fragmentEntry = Marshal.StringToCoTaskMemUTF8("fs_main");
			try
			{
				var colorTarget = new WGPUColorTargetState
				{
					format = format,
					writeMask = WGPUColorWriteMask.All,
				};

				var fragment = new WGPUFragmentState
				{
					module = module,
					entryPoint = NullTerminated(fragmentEntry),
					targetCount = 1,
					targets = &colorTarget,
				};

				var descriptor = new WGPURenderPipelineDescriptor
				{
					label = NullString(),
					// A null layout asks wgpu to derive the bind group layouts from the shader; the
					// triangle has no bindings at all, so there is nothing to derive.
					vertex = new WGPUVertexState { module = module, entryPoint = NullTerminated(vertexEntry) },
					primitive = new WGPUPrimitiveState
					{
						topology = WGPUPrimitiveTopology.TriangleList,
						frontFace = WGPUFrontFace.CCW,
						cullMode = WGPUCullMode.None,
					},
					multisample = new WGPUMultisampleState { count = 1, mask = uint.MaxValue },
					fragment = &fragment,
				};

				WGPURenderPipeline pipeline = wgpuDeviceCreateRenderPipeline(device, &descriptor);
				if (pipeline.IsNull)
				{
					throw new InvalidOperationException("wgpuDeviceCreateRenderPipeline returned null");
				}

				return pipeline;
			}
			finally
			{
				Marshal.FreeCoTaskMem(vertexEntry);
				Marshal.FreeCoTaskMem(fragmentEntry);
			}
		}

		/// <summary>webgpu.h spells "no string" as a null pointer with the WGPU_STRLEN sentinel length.</summary>
		private static unsafe WGPUStringView NullString() => new WGPUStringView { data = null, length = WGPUConstants.WGPU_STRLEN };

		private static unsafe WGPUStringView NullTerminated(nint utf8) => new WGPUStringView { data = (byte*)utf8, length = WGPUConstants.WGPU_STRLEN };

		private static unsafe string ToManaged(WGPUStringView view)
		{
			if (view.data == null)
			{
				return string.Empty;
			}

			return view.length == WGPUConstants.WGPU_STRLEN
				? Marshal.PtrToStringUTF8((nint)view.data)
				: Marshal.PtrToStringUTF8((nint)view.data, (int)view.length);
		}

		// The callbacks below are native entry points, so they take a pointer to the caller's stack slot
		// rather than closing over anything. Status starts at zero - not a legal WGPU status - so the
		// polling loops can tell "not called yet" from any real answer.
		private struct AdapterResult
		{
			public int Status;

			public WGPUAdapter Adapter;
		}

		private struct DeviceResult
		{
			public int Status;

			public WGPUDevice Device;
		}

		private struct MapResult
		{
			public int Status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static unsafe void OnAdapterRequested(WGPURequestAdapterStatus status, WGPUAdapter adapter, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (AdapterResult*)userdata1;
			result->Adapter = adapter;
			result->Status = (int)status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static unsafe void OnDeviceRequested(WGPURequestDeviceStatus status, WGPUDevice device, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (DeviceResult*)userdata1;
			result->Device = device;
			result->Status = (int)status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static unsafe void OnBufferMapped(WGPUMapAsyncStatus status, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (MapResult*)userdata1;
			result->Status = (int)status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static unsafe void OnUncapturedError(WGPUDevice* device, WGPUErrorType type, WGPUStringView message, void* userdata1, void* userdata2)
		{
			uncapturedError = $"{type}: {ToManaged(message)}";
		}
	}
}
