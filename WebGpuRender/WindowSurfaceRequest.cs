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

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// The native window a device should build its swapchain over. Passed to
	/// <see cref="WebGpuRenderDevice"/>'s constructor so the surface can exist before the adapter request
	/// and be handed to it as <c>compatibleSurface</c>.
	/// </summary>
	public sealed class WindowSurfaceRequest
	{
		/// <summary>Describes a window to make a surface over.</summary>
		/// <param name="windowHandle">The native window handle (HWND on Windows).</param>
		/// <param name="moduleHandle">The module instance (HINSTANCE), or zero.</param>
		/// <param name="width">Initial swapchain width in pixels.</param>
		/// <param name="height">Initial swapchain height in pixels.</param>
		/// <param name="label">Optional debug label.</param>
		public WindowSurfaceRequest(IntPtr windowHandle, IntPtr moduleHandle, uint width, uint height, string label = null)
		{
			this.WindowHandle = windowHandle;
			this.ModuleHandle = moduleHandle;
			this.Width = width;
			this.Height = height;
			this.Label = label;
		}

		/// <summary>The native window handle (HWND on Windows).</summary>
		public IntPtr WindowHandle { get; }

		/// <summary>The module instance (HINSTANCE), or zero.</summary>
		public IntPtr ModuleHandle { get; }

		/// <summary>Initial swapchain width in pixels.</summary>
		public uint Width { get; }

		/// <summary>Initial swapchain height in pixels.</summary>
		public uint Height { get; }

		/// <summary>Optional debug label.</summary>
		public string Label { get; }
	}
}
