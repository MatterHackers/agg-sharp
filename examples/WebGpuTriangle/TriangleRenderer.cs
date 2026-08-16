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
using System.Runtime.InteropServices;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.WebGpu.Example
{
	/// <summary>
	/// Owns the whole wgpu object graph for one HWND: instance, surface, adapter, device, pipeline, and
	/// the surface configuration that has to be redone on every resize. This is the Phase 0 shape of
	/// what eventually becomes WebGpuControl - deliberately one file with no abstraction, so the actual
	/// call sequence a window host has to get right is readable start to finish.
	/// </summary>
	public sealed unsafe class TriangleRenderer : IDisposable
	{
		private const string TriangleWgsl = @"
struct VertexOut
{
	@builtin(position) position : vec4<f32>,
	@location(0) color : vec3<f32>,
}

@vertex
fn vs_main(@builtin(vertex_index) index : u32) -> VertexOut
{
	var positions = array<vec2<f32>, 3>(
		vec2<f32>( 0.0,  0.8),
		vec2<f32>(-0.8, -0.8),
		vec2<f32>( 0.8, -0.8));
	var colors = array<vec3<f32>, 3>(
		vec3<f32>(1.0, 0.0, 0.0),
		vec3<f32>(0.0, 1.0, 0.0),
		vec3<f32>(0.0, 0.0, 1.0));

	var out : VertexOut;
	out.position = vec4<f32>(positions[index], 0.0, 1.0);
	out.color = colors[index];
	return out;
}

@fragment
fn fs_main(in : VertexOut) -> @location(0) vec4<f32>
{
	return vec4<f32>(in.color, 1.0);
}
";

		private WGPUInstance instance;
		private WGPUSurface surface;
		private WGPUAdapter adapter;
		private WGPUDevice device;
		private WGPUQueue queue;
		private WGPUShaderModule shaderModule;
		private WGPURenderPipeline pipeline;
		private WGPUTextureFormat surfaceFormat;
		private uint configuredWidth;
		private uint configuredHeight;

		/// <summary>The adapter wgpu actually chose, for the "did we get D3D12" question.</summary>
		public WGPUBackendType BackendType { get; private set; }

		public string AdapterName { get; private set; } = string.Empty;

		/// <summary>
		/// Non null once wgpu has reported a validation error or lost the device.
		/// <para>
		/// Static, not per instance: the error and device-lost callbacks are <see cref="UnmanagedCallersOnlyAttribute"/>
		/// entry points, which cannot close over an instance, and routing one through the callbacks'
		/// userdata slots would mean a pinned handle that has to outlive the device. This spike creates
		/// exactly one renderer per process, so process-wide state is honest here; anything that wants two
		/// live renderers has to add a registry keyed by that userdata first.
		/// </para>
		/// </summary>
		public static string FirstError => firstError;

		private static string firstError;

		/// <summary>
		/// Set by <see cref="OnDeviceLost"/> for any reason other than an ordinary teardown. Same
		/// single-instance constraint as <see cref="FirstError"/>.
		/// </summary>
		private static bool deviceLost;

		public int FramesRendered { get; private set; }

		public TriangleRenderer(IntPtr hwnd, IntPtr hinstance, uint width, uint height)
		{
			instance = wgpuCreateInstance(null);
			if (instance.IsNull)
			{
				throw new InvalidOperationException("wgpuCreateInstance returned null");
			}

			surface = CreateSurfaceFromHwnd(instance, hwnd, hinstance);
			adapter = RequestAdapter(instance, surface, WGPUBackendType.D3D12);

			var info = default(WGPUAdapterInfo);
			if (wgpuAdapterGetInfo(adapter, &info) == WGPUStatus.Success)
			{
				this.BackendType = info.backendType;
				this.AdapterName = ToManaged(info.device);
				wgpuAdapterInfoFreeMembers(info);
			}

			device = RequestDevice(instance, adapter);
			queue = wgpuDeviceGetQueue(device);

			surfaceFormat = PreferredSurfaceFormat(surface, adapter);
			shaderModule = CreateWgslModule(device, TriangleWgsl);
			pipeline = CreateTrianglePipeline(device, shaderModule, surfaceFormat);

			this.Resize(width, height);
		}

		/// <summary>
		/// (Re)configures the swapchain. WebGPU has no implicit resize: presenting against a surface whose
		/// configured size no longer matches the window returns Outdated forever until this is called.
		/// </summary>
		public void Resize(uint width, uint height)
		{
			if (width == 0 || height == 0 || device.IsNull)
			{
				return;
			}

			var configuration = new WGPUSurfaceConfiguration
			{
				device = device,
				format = surfaceFormat,
				usage = WGPUTextureUsage.RenderAttachment,
				width = width,
				height = height,
				// Opaque, not Auto: the eventual UI never writes alpha in its LCD text passes, and any
				// alpha-respecting composition mode would show that as see-through text.
				alphaMode = WGPUCompositeAlphaMode.Opaque,
				presentMode = WGPUPresentMode.Fifo,
			};

			wgpuSurfaceConfigure(surface, &configuration);
			configuredWidth = width;
			configuredHeight = height;
		}

		/// <summary>Draws one frame and presents it. Returns false if the frame had to be skipped.</summary>
		public bool RenderFrame()
		{
			// A lost device rejects every call that follows, so keep submitting nothing rather than a
			// stream of failures. The caller sees the loss through FirstError and shuts down.
			if (deviceLost || device.IsNull || configuredWidth == 0)
			{
				return false;
			}

			var surfaceTexture = default(WGPUSurfaceTexture);
			wgpuSurfaceGetCurrentTexture(surface, &surfaceTexture);

			switch (surfaceTexture.status)
			{
				case WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal:
				case WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal:
					break;

				case WGPUSurfaceGetCurrentTextureStatus.Timeout:
				case WGPUSurfaceGetCurrentTextureStatus.Outdated:
				case WGPUSurfaceGetCurrentTextureStatus.Lost:
					// Recoverable: the window changed underneath us, so rebuild the swapchain and let the
					// next frame draw.
					if (!surfaceTexture.texture.IsNull)
					{
						wgpuTextureRelease(surfaceTexture.texture);
					}

					this.Resize(configuredWidth, configuredHeight);
					return false;

				default:
					firstError ??= $"wgpuSurfaceGetCurrentTexture returned {surfaceTexture.status}";
					return false;
			}

			WGPUTextureView view = wgpuTextureCreateView(surfaceTexture.texture, null);

			var encoderDescriptor = new WGPUCommandEncoderDescriptor { label = NullString() };
			WGPUCommandEncoder encoder = wgpuDeviceCreateCommandEncoder(device, &encoderDescriptor);

			var colorAttachment = new WGPURenderPassColorAttachment
			{
				view = view,
				depthSlice = WGPUConstants.WGPU_DEPTH_SLICE_UNDEFINED,
				loadOp = WGPULoadOp.Clear,
				storeOp = WGPUStoreOp.Store,
				clearValue = new WGPUColor { r = 0.1, g = 0.1, b = 0.15, a = 1.0 },
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

			var commandBufferDescriptor = new WGPUCommandBufferDescriptor { label = NullString() };
			WGPUCommandBuffer commands = wgpuCommandEncoderFinish(encoder, &commandBufferDescriptor);
			wgpuQueueSubmit(queue, 1, &commands);
			wgpuCommandBufferRelease(commands);
			wgpuCommandEncoderRelease(encoder);

			wgpuSurfacePresent(surface);

			wgpuTextureViewRelease(view);
			wgpuTextureRelease(surfaceTexture.texture);

			this.FramesRendered++;

			return true;
		}

		public void Dispose()
		{
			if (!pipeline.IsNull)
			{
				wgpuRenderPipelineRelease(pipeline);
				pipeline = default;
			}

			if (!shaderModule.IsNull)
			{
				wgpuShaderModuleRelease(shaderModule);
				shaderModule = default;
			}

			if (!queue.IsNull)
			{
				wgpuQueueRelease(queue);
				queue = default;
			}

			if (!surface.IsNull)
			{
				wgpuSurfaceUnconfigure(surface);
				wgpuSurfaceRelease(surface);
				surface = default;
			}

			if (!device.IsNull)
			{
				wgpuDeviceRelease(device);
				device = default;
			}

			if (!adapter.IsNull)
			{
				wgpuAdapterRelease(adapter);
				adapter = default;
			}

			if (!instance.IsNull)
			{
				wgpuInstanceRelease(instance);
				instance = default;
			}
		}

		private static WGPUSurface CreateSurfaceFromHwnd(WGPUInstance instance, IntPtr hwnd, IntPtr hinstance)
		{
			var windowsSource = new WGPUSurfaceSourceWindowsHWND
			{
				chain = new WGPUChainedStruct { sType = WGPUSType.SurfaceSourceWindowsHWND },
				hinstance = (void*)hinstance,
				hwnd = (void*)hwnd,
			};

			var descriptor = new WGPUSurfaceDescriptor
			{
				nextInChain = (WGPUChainedStruct*)&windowsSource,
				label = NullString(),
			};

			WGPUSurface surface = wgpuInstanceCreateSurface(instance, &descriptor);
			if (surface.IsNull)
			{
				throw new InvalidOperationException("wgpuInstanceCreateSurface returned null for the control's HWND");
			}

			return surface;
		}

		private static WGPUTextureFormat PreferredSurfaceFormat(WGPUSurface surface, WGPUAdapter adapter)
		{
			var capabilities = default(WGPUSurfaceCapabilities);
			if (wgpuSurfaceGetCapabilities(surface, adapter, &capabilities) != WGPUStatus.Success
				|| capabilities.formatCount == 0)
			{
				throw new InvalidOperationException("wgpuSurfaceGetCapabilities reported no supported formats");
			}

			// wgpu lists the surface's preferred format first.
			WGPUTextureFormat format = capabilities.formats[0];
			wgpuSurfaceCapabilitiesFreeMembers(capabilities);

			return format;
		}

		private static WGPUAdapter RequestAdapter(WGPUInstance instance, WGPUSurface surface, WGPUBackendType preferredBackend)
		{
			var result = new AdapterResult();
			var options = new WGPURequestAdapterOptions
			{
				featureLevel = WGPUFeatureLevel.Core,
				powerPreference = WGPUPowerPreference.HighPerformance,
				backendType = preferredBackend,
				compatibleSurface = surface,
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

		private static WGPUDevice RequestDevice(WGPUInstance instance, WGPUAdapter adapter)
		{
			var result = new DeviceResult();
			var descriptor = new WGPUDeviceDescriptor
			{
				label = NullString(),
				defaultQueue = new WGPUQueueDescriptor { label = NullString() },
				deviceLostCallbackInfo = new WGPUDeviceLostCallbackInfo
				{
					mode = WGPUCallbackMode.AllowProcessEvents,
					callback = &OnDeviceLost,
				},
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

		private static WGPUShaderModule CreateWgslModule(WGPUDevice device, string wgsl)
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

		private static WGPURenderPipeline CreateTrianglePipeline(WGPUDevice device, WGPUShaderModule module, WGPUTextureFormat format)
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
		private static WGPUStringView NullString() => new WGPUStringView { data = null, length = WGPUConstants.WGPU_STRLEN };

		private static WGPUStringView NullTerminated(nint utf8) => new WGPUStringView { data = (byte*)utf8, length = WGPUConstants.WGPU_STRLEN };

		private static string ToManaged(WGPUStringView view)
		{
			if (view.data == null)
			{
				return string.Empty;
			}

			return view.length == WGPUConstants.WGPU_STRLEN
				? Marshal.PtrToStringUTF8((nint)view.data)
				: Marshal.PtrToStringUTF8((nint)view.data, (int)view.length);
		}

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

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnAdapterRequested(WGPURequestAdapterStatus status, WGPUAdapter adapter, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (AdapterResult*)userdata1;
			result->Adapter = adapter;
			result->Status = (int)status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnDeviceRequested(WGPURequestDeviceStatus status, WGPUDevice device, WGPUStringView message, void* userdata1, void* userdata2)
		{
			var result = (DeviceResult*)userdata1;
			result->Device = device;
			result->Status = (int)status;
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnUncapturedError(WGPUDevice* device, WGPUErrorType type, WGPUStringView message, void* userdata1, void* userdata2)
		{
			firstError ??= $"{type}: {ToManaged(message)}";
		}

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnDeviceLost(WGPUDevice* device, WGPUDeviceLostReason reason, WGPUStringView message, void* userdata1, void* userdata2)
		{
			// Destroyed is the ordinary shutdown path - only the surprising reasons are failures.
			if (reason != WGPUDeviceLostReason.Destroyed)
			{
				firstError ??= $"device lost ({reason}): {ToManaged(message)}";
				deviceLost = true;
			}
		}
	}
}
