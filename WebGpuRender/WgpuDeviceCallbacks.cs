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
using MatterHackers.WebGpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The device-lifetime callbacks wgpu calls on a <see cref="WebGpuRenderDevice"/>: uncaptured errors
	/// and device loss, in a desktop and a browser shape.
	/// <para>
	/// <b>Why two shapes.</b> Both callbacks carry their message as a <c>WGPUStringView</c> passed by value.
	/// On the desktop (wgpu-native, x64/arm64) that 16-byte struct travels in registers and a by-value
	/// managed parameter reads it. Under the wasm32 C ABI clang passes any struct of more than one
	/// scalar indirectly - the callee receives a pointer to the caller's copy - and mono-wasm's generated
	/// reverse thunk (<c>wasm_native_to_interp_*</c> in pinvoke-table.h) declares that slot as
	/// <c>void*</c> but hands the interpreter the address <em>of the pointer</em>, so a by-value managed
	/// parameter reads the pointer and its neighbouring argument as if they were the view. That is what
	/// turned every browser GPU error into "Validation: " plus garbage. The browser shape takes
	/// <c>WGPUStringView*</c>, which is exactly what arrives.
	/// </para>
	/// <para>
	/// Only the browser can prove the browser shape reads the real message; desktop tests pin the decode
	/// helper and the declared signatures.
	/// </para>
	/// </summary>
	internal static unsafe class WgpuDeviceCallbacks
	{
		/// <summary>The uncaptured-error callback for this platform.</summary>
		/// <param name="isBrowser">Whether the device runs on the browser's WebGPU (emdawnwebgpu).</param>
		public static delegate* unmanaged[Cdecl]<WGPUDevice*, WGPUErrorType, WGPUStringView, void*, void*, void> UncapturedError(bool isBrowser)
			=> isBrowser
				? (delegate* unmanaged[Cdecl]<WGPUDevice*, WGPUErrorType, WGPUStringView, void*, void*, void>)(delegate* unmanaged[Cdecl]<WGPUDevice*, WGPUErrorType, WGPUStringView*, void*, void*, void>)&OnUncapturedErrorBrowser
				: &OnUncapturedError;

		/// <summary>The device-lost callback for this platform.</summary>
		/// <param name="isBrowser">Whether the device runs on the browser's WebGPU (emdawnwebgpu).</param>
		public static delegate* unmanaged[Cdecl]<WGPUDevice*, WGPUDeviceLostReason, WGPUStringView, void*, void*, void> DeviceLost(bool isBrowser)
			=> isBrowser
				? (delegate* unmanaged[Cdecl]<WGPUDevice*, WGPUDeviceLostReason, WGPUStringView, void*, void*, void>)(delegate* unmanaged[Cdecl]<WGPUDevice*, WGPUDeviceLostReason, WGPUStringView*, void*, void*, void>)&OnDeviceLostBrowser
				: &OnDeviceLost;

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnUncapturedError(WGPUDevice* device, WGPUErrorType type, WGPUStringView message, void* userdata1, void* userdata2)
			=> ReportUncapturedError(type, &message, userdata1);

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnUncapturedErrorBrowser(WGPUDevice* device, WGPUErrorType type, WGPUStringView* message, void* userdata1, void* userdata2)
			=> ReportUncapturedError(type, message, userdata1);

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnDeviceLost(WGPUDevice* device, WGPUDeviceLostReason reason, WGPUStringView message, void* userdata1, void* userdata2)
			=> ReportDeviceLost(reason, &message, userdata1);

		[UnmanagedCallersOnly(CallConvs = new[] { typeof(CallConvCdecl) })]
		private static void OnDeviceLostBrowser(WGPUDevice* device, WGPUDeviceLostReason reason, WGPUStringView* message, void* userdata1, void* userdata2)
			=> ReportDeviceLost(reason, message, userdata1);

		private static void ReportUncapturedError(WGPUErrorType type, WGPUStringView* message, void* userdata)
		{
			// An exception must not unwind into the native caller, so everything here is inside a catch-all.
			try
			{
				FromUserdata(userdata)?.ReportUncapturedError($"{type}: {WgpuStrings.ToManaged(message)}");
			}
			catch (Exception)
			{
			}
		}

		private static void ReportDeviceLost(WGPUDeviceLostReason reason, WGPUStringView* message, void* userdata)
		{
			try
			{
				FromUserdata(userdata)?.ReportDeviceLost($"{reason}: {WgpuStrings.ToManaged(message)}");
			}
			catch (Exception)
			{
			}
		}

		private static WebGpuRenderDevice FromUserdata(void* userdata)
			=> userdata == null ? null : GCHandle.FromIntPtr((nint)userdata).Target as WebGpuRenderDevice;
	}
}
