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
ANY EXPRESS OR IMPLIED WARRANTIES ARE DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT
OWNER OR CONTRIBUTORS BE LIABLE FOR ANY DAMAGES ARISING IN ANY WAY OUT OF THE USE
OF THIS SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.
*/

// Hand written, deliberately outside Generated\: these types exist only in Dawn's
// Emscripten flavour of webgpu.h (emdawnwebgpu), not in the wgpu-native headers the
// generator runs against. Re-running the generator must not clobber or duplicate them.
// Mirrors emdawnwebgpu_pkg/webgpu/include/webgpu/webgpu.h.

using System.Runtime.InteropServices;

namespace MatterHackers.WebGpu
{
	/// <summary>
	/// Chained onto <c>WGPUSurfaceDescriptor</c> to make a surface out of a browser canvas,
	/// naming the canvas with a CSS selector such as <c>"#webgpu-canvas"</c>. This is the browser's
	/// stand-in for the platform window sources (HWND, Metal layer, Xlib) - there is no other way to
	/// get a surface under emdawnwebgpu.
	/// </summary>
	[StructLayout(LayoutKind.Sequential)]
	public unsafe struct WGPUEmscriptenSurfaceSourceCanvasHTMLSelector
	{
		public WGPUChainedStruct chain;

		public WGPUStringView selector;
	}

	/// <summary>
	/// The <c>WGPUSType</c> values emdawnwebgpu adds on top of the generated enum. They live in the
	/// 0x0004xxxx block webgpu.h reserves for the Emscripten implementation, so they can never collide
	/// with a future generated value.
	/// </summary>
	public static class WGPUEmscriptenSType
	{
		/// <summary><c>WGPUSType_EmscriptenSurfaceSourceCanvasHTMLSelector</c>.</summary>
		public const WGPUSType EmscriptenSurfaceSourceCanvasHTMLSelector = (WGPUSType)0x00040000;
	}
}
