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
using MatterHackers.WebGpu;
using static MatterHackers.WebGpu.Wgpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The copies <see cref="WebGpuRenderDevice"/> records or performs: texture to texture on its command encoder,
	/// and a mapped readback buffer out to managed memory. The device does the checks (disposed, pass open,
	/// resource types) and hands over native handles.
	/// </summary>
	internal static unsafe class WebGpuCopies
	{
		/// <summary>
		/// Records a copy of the <paramref name="width"/> by <paramref name="height"/> rectangle at
		/// (<paramref name="x"/>, <paramref name="y"/>) from <paramref name="source"/> to the same place in
		/// <paramref name="destination"/> on <paramref name="encoder"/>, so it is ordered with the draws around it.
		/// </summary>
		public static void TextureToTexture(WGPUCommandEncoder encoder, WebGpuTexture source, WebGpuTexture destination, int x, int y, int width, int height)
		{
			if (width <= 0 || height <= 0)
			{
				return;
			}

			var origin = new WGPUOrigin3D { x = (uint)x, y = (uint)y, z = 0 };
			var copySource = new WGPUTexelCopyTextureInfo { texture = source.Handle, mipLevel = 0, origin = origin, aspect = WGPUTextureAspect.All };
			var copyDestination = new WGPUTexelCopyTextureInfo { texture = destination.Handle, mipLevel = 0, origin = origin, aspect = WGPUTextureAspect.All };
			var copySize = new WGPUExtent3D { width = (uint)width, height = (uint)height, depthOrArrayLayers = 1 };
			wgpuCommandEncoderCopyTextureToTexture(encoder, &copySource, &copyDestination, &copySize);
		}

		/// <summary>
		/// Copies an already-mapped readback buffer out and unmaps it. Shared by both wait strategies: the
		/// desktop reaches it straight after its spin, the browser from the continuation of its map promise,
		/// and neither leg gets its own copy of the unmap rule.
		/// </summary>
		/// <param name="readback">A buffer that is currently mapped for read.</param>
		/// <param name="totalBytes">The mapped range, from offset 0.</param>
		/// <param name="destination">Where the mapped bytes are copied to.</param>
		public static void CopyMappedRange(WGPUBuffer readback, ulong totalBytes, Span<byte> destination)
		{
			try
			{
				var mapped = wgpuBufferGetConstMappedRange(readback, 0, (nuint)totalBytes);
				if (mapped == null)
				{
					throw new InvalidOperationException("wgpuBufferGetConstMappedRange returned null.");
				}

				new ReadOnlySpan<byte>(mapped, (int)totalBytes).CopyTo(destination);
			}
			finally
			{
				// Releasing a still-mapped buffer is undefined, so a throw out of the copy must not skip this.
				wgpuBufferUnmap(readback);
			}
		}
	}
}
