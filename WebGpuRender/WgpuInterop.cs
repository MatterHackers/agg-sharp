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
using System.Runtime.InteropServices;
using MatterHackers.WebGpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The <c>WGPUStringView</c> ergonomics the Phase 0 spike called out as the most repeated wart:
	/// webgpu.h spells "no string" as a null pointer carrying the <c>WGPU_STRLEN</c> sentinel length,
	/// spells "null terminated" as a real pointer carrying the same sentinel, and hands strings back
	/// either way. Written once here rather than at every call site.
	/// </summary>
	public static unsafe class WgpuStrings
	{
		/// <summary>The "no string" view: null data with the <c>WGPU_STRLEN</c> sentinel length.</summary>
		public static WGPUStringView Null => new WGPUStringView { data = null, length = WGPUConstants.WGPU_STRLEN };

		/// <summary>
		/// Wraps an already allocated, null terminated UTF-8 buffer. The buffer must outlive every wgpu
		/// call the view is passed to - use <see cref="Utf8Buffer"/> rather than calling this directly.
		/// </summary>
		/// <param name="utf8">Pointer to null terminated UTF-8 bytes, or zero for no string.</param>
		public static WGPUStringView NullTerminated(nint utf8)
			=> utf8 == 0 ? Null : new WGPUStringView { data = (byte*)utf8, length = WGPUConstants.WGPU_STRLEN };

		/// <summary>
		/// Copies a view wgpu handed back into a managed string. Handles both forms of the length field,
		/// because wgpu uses both: adapter info is null terminated, error messages are counted.
		/// </summary>
		/// <param name="view">The view to read.</param>
		public static string ToManaged(WGPUStringView view)
		{
			if (view.data == null)
			{
				return string.Empty;
			}

			return view.length == WGPUConstants.WGPU_STRLEN
				? Marshal.PtrToStringUTF8((nint)view.data)
				: Marshal.PtrToStringUTF8((nint)view.data, (int)view.length);
		}
	}

	/// <summary>
	/// A UTF-8 copy of a managed string that lives exactly as long as the <c>using</c> block around it.
	/// Every label and entry point name has to be pinned unmanaged memory for the duration of the wgpu
	/// call that reads it, and forgetting the free is the easy mistake; this makes the lifetime the
	/// shape of the code.
	/// <para>
	/// A null or empty string produces the "no string" view rather than a pointer to an empty buffer,
	/// which is what webgpu.h wants for an omitted optional label.
	/// </para>
	/// </summary>
	public readonly unsafe struct Utf8Buffer : IDisposable
	{
		private readonly nint pointer;

		/// <summary>Allocates an unmanaged UTF-8 copy of <paramref name="text"/>.</summary>
		/// <param name="text">The text to copy; null or empty allocates nothing.</param>
		public Utf8Buffer(string text)
		{
			this.pointer = string.IsNullOrEmpty(text) ? 0 : Marshal.StringToCoTaskMemUTF8(text);
		}

		/// <summary>The view to hand to wgpu. Valid only until this buffer is disposed.</summary>
		public WGPUStringView View => WgpuStrings.NullTerminated(this.pointer);

		/// <summary>Frees the unmanaged copy.</summary>
		public void Dispose()
		{
			if (this.pointer != 0)
			{
				Marshal.FreeCoTaskMem(this.pointer);
			}
		}
	}
}
