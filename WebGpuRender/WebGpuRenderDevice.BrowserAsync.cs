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
using MatterHackers.WebGpu;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The waiting halves of <see cref="WebGpuRenderDevice"/> that have to be able to <c>await</c>: device
	/// creation and the pending half of a texture readback.
	/// <para>
	/// <b>Why this is a separate file.</b> C# forbids <c>await</c> anywhere inside an <c>unsafe</c> context
	/// (CS4004), and the other half of this class is <c>unsafe</c> from its declaration outward because it
	/// is descriptor-filling P/Invoke. The <c>unsafe</c> modifier is per-declaration, not per-type, so a
	/// second partial declaration without it can await while still calling every private member next door.
	/// Everything that names a pointer therefore stays in <c>WebGpuRenderDevice.cs</c> and hands its result
	/// back as a <see cref="Task{TResult}"/>; everything that waits on one lives here.
	/// </para>
	/// <para>
	/// <b>Both platforms go through here.</b> <see cref="WebGpuRenderDevice.CreateAsync"/> is the one entry
	/// point a cross-platform host should use. On the desktop it runs the ordinary synchronous constructor
	/// and hands back an already-completed task, so nothing about the desktop device path changes; in the
	/// browser it is the only entry point that can work at all.
	/// </para>
	/// </summary>
	public sealed partial class WebGpuRenderDevice
	{
		/// <summary>
		/// Creates a device for <paramref name="windowSurface"/>, awaiting the adapter and device requests
		/// where the platform makes them asynchronous.
		/// <para>
		/// Desktop: the returned task is already complete. The work is the ordinary constructor - same
		/// order, same spin, same errors - so a desktop host that switches to this pays one task allocation
		/// once per device and nothing else.
		/// </para>
		/// <para>
		/// Browser: genuinely asynchronous. The adapter and the device are Promises, and the canvas cannot
		/// be painted until they settle. A host must therefore have somewhere to be while it waits (a
		/// "renderer not ready yet" state), and - the same fact from the other side - so must a host
		/// recovering from a lost device: sync recovery through the public constructor is refused in the
		/// browser, so the layer above has to route a loss back through here rather than freezing the
		/// canvas forever. That routing is the browser layer's, not this type's.
		/// </para>
		/// </summary>
		/// <param name="windowSurface">
		/// The drawable to present to - <see cref="WindowSurfaceRequest.ForBrowserCanvas"/> in the browser,
		/// one of the native factories on the desktop - or null for an offscreen device.
		/// </param>
		/// <param name="label">Optional debug label carried into wgpu's validation messages.</param>
		/// <exception cref="InvalidOperationException">The instance, adapter or device could not be created.</exception>
		public static async Task<WebGpuRenderDevice> CreateAsync(WindowSurfaceRequest windowSurface, string label = null)
		{
			if (!OperatingSystem.IsBrowser())
			{
				// Deliberately the plain constructor and not a re-implementation of it: the desktop device
				// path is the one every golden image is captured through, and it must not acquire a second
				// spelling. forceFallbackAdapter and preferredBackend keep their defaults - a caller that
				// needs either is asking a wgpu-native question and should say so through the constructor.
				return new WebGpuRenderDevice(false, WGPUBackendType.Undefined, label, windowSurface);
			}

			var device = new WebGpuRenderDevice(label);

			try
			{
				await device.InitializeBrowserAsync(windowSurface);
			}
			catch
			{
				device.Dispose();
				throw;
			}

			return device;
		}

		/// <summary>
		/// The browser's version of the constructor body: same order, awaits instead of spins. The surface
		/// is created before the adapter is requested for the same reason it is on the desktop - it is the
		/// request's <c>compatibleSurface</c>.
		/// </summary>
		/// <param name="windowSurface">The canvas to present to, or null for an offscreen device.</param>
		private async Task InitializeBrowserAsync(WindowSurfaceRequest windowSurface)
		{
			this.instance = CreateInstance();
			if (this.instance.IsNull)
			{
				throw new InvalidOperationException("wgpuCreateInstance returned null.");
			}

			WGPUSurface pendingSurface = windowSurface == null
				? default
				: CreateRawSurface(this.instance, windowSurface);

			try
			{
				AdapterResult adapterResult = await this.RequestAdapterBrowserAsync(pendingSurface);
				if (adapterResult.Status != (int)WGPURequestAdapterStatus.Success || adapterResult.Adapter.IsNull)
				{
					throw new InvalidOperationException(
						$"wgpuInstanceRequestAdapter failed (status {adapterResult.Status}). "
						+ "The browser reported no usable WebGPU adapter.");
				}

				this.adapter = adapterResult.Adapter;
				this.ReadAdapterInfo();
				this.device = await this.RequestBrowserDeviceWithLimitsFallbackAsync();
				this.queue = wgpuDeviceGetQueue(this.device);
				this.ReadDeviceLimits();

				if (windowSurface != null)
				{
					this.WindowSurface = this.ConfigureSurfaceTarget(pendingSurface, windowSurface);
					pendingSurface = default;
				}
			}
			finally
			{
				if (!pendingSurface.IsNull)
				{
					wgpuSurfaceRelease(pendingSurface);
				}
			}
		}

		/// <summary>
		/// The browser device request, with the same refusal policy the desktop applies: ask for the raised
		/// <c>maxTextureDimension2D</c>, and if the implementation will not grant it, take the defaults
		/// rather than leaving the page with no device.
		/// </summary>
		private async Task<WGPUDevice> RequestBrowserDeviceWithLimitsFallbackAsync()
		{
			WGPULimits requiredLimits = this.RequiredLimits();
			DeviceResult result = await this.RequestDeviceBrowserAsync(requiredLimits);

			if (!Succeeded(result) && RaisesALimit(requiredLimits))
			{
				result = await this.RequestDeviceBrowserAsync(UndefinedLimits());
			}

			if (!Succeeded(result))
			{
				throw new InvalidOperationException($"wgpuAdapterRequestDevice failed (status {result.Status}).");
			}

			return result.Device;
		}

		/// <summary>
		/// The pending half of a browser readback: wait for the map promise, copy the mapped range out,
		/// unmap, and release the readback buffer. Ownership of <paramref name="readback"/> transfers here
		/// from <see cref="ReadTextureAsync"/> - which is why the release lives in this method's finally and
		/// not in the caller's.
		/// </summary>
		/// <param name="readback">The buffer the texture copy was recorded into, already submitted.</param>
		/// <param name="result">The geometry of the read.</param>
		/// <param name="destination">Where the pixels are copied to.</param>
		private async Task<TextureReadResult> MapAndCopyBrowserAsync(
			WGPUBuffer readback,
			TextureReadResult result,
			Memory<byte> destination)
		{
			try
			{
				CallbackResult map = await MapForReadBrowserAsync(readback, result.TotalBytes);
				if (map.Status != (int)WGPUMapAsyncStatus.Success)
				{
					throw new InvalidOperationException($"wgpuBufferMapAsync did not succeed (status {map.Status}).");
				}

				CopyMappedRange(readback, result, destination.Span);
				return result;
			}
			finally
			{
				wgpuBufferRelease(readback);
			}
		}
	}
}
