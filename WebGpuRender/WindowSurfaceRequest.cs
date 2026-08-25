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
	/// The native drawable a device should build its swapchain over. Passed to
	/// <see cref="WebGpuRenderDevice"/>'s constructor so the surface can exist before the adapter request
	/// and be handed to it as <c>compatibleSurface</c>.
	/// <para>
	/// What "native drawable" means is per platform, and the platforms do not agree on its shape: Windows
	/// wants an HWND plus (optionally) its HINSTANCE, macOS wants neither a window nor a view but a
	/// <c>CAMetalLayer*</c> - the layer is the thing Metal can actually hand back drawables from - and
	/// X11 wants two values at once, a <c>Display*</c> and an XID. Rather than one field per platform,
	/// this carries a single <see cref="NativeSurfaceHandle"/> whose meaning is decided by the OS the
	/// process is running on, plus the Windows-only <see cref="ModuleHandle"/> and the X11-only
	/// <see cref="XlibWindow"/>. Use the <see cref="ForWindowsHwnd"/> / <see cref="ForMetalLayer"/> /
	/// <see cref="ForXlibWindow"/> factories so the call site says which handle it is holding.
	/// </para>
	/// </summary>
	public sealed class WindowSurfaceRequest
	{
		/// <summary>
		/// Describes a native drawable to make a surface over. This form cannot describe an X11 drawable:
		/// there is no parameter for the window XID, so on Linux it yields a request with a display and no
		/// window, which surface creation rejects. Use <see cref="ForXlibWindow"/> there.
		/// </summary>
		/// <param name="nativeSurfaceHandle">
		/// The platform's native surface handle: an HWND on Windows, a <c>CAMetalLayer*</c> on macOS, the
		/// X11 <c>Display*</c> on Linux - see <see cref="ForXlibWindow"/>.
		/// </param>
		/// <param name="moduleHandle">The module instance (HINSTANCE), or zero. Windows only; ignored elsewhere.</param>
		/// <param name="width">Initial swapchain width in pixels.</param>
		/// <param name="height">Initial swapchain height in pixels.</param>
		/// <param name="label">Optional debug label.</param>
		public WindowSurfaceRequest(IntPtr nativeSurfaceHandle, IntPtr moduleHandle, uint width, uint height, string label = null)
			: this(nativeSurfaceHandle, moduleHandle, 0, width, height, label)
		{
		}

		/// <summary>
		/// The full form, private because only <see cref="ForXlibWindow"/> has an X11 window to pass and
		/// every other call site would have to pass a zero it does not mean.
		/// </summary>
		private WindowSurfaceRequest(IntPtr nativeSurfaceHandle, IntPtr moduleHandle, ulong xlibWindow, uint width, uint height, string label)
		{
			this.NativeSurfaceHandle = nativeSurfaceHandle;
			this.ModuleHandle = moduleHandle;
			this.XlibWindow = xlibWindow;
			this.Width = width;
			this.Height = height;
			this.Label = label;
		}

		/// <summary>Describes a Windows window (HWND) to make a surface over.</summary>
		/// <param name="hwnd">The window handle.</param>
		/// <param name="hinstance">The module instance, or zero - wgpu only uses it as a hint.</param>
		/// <param name="width">Initial swapchain width in pixels.</param>
		/// <param name="height">Initial swapchain height in pixels.</param>
		/// <param name="label">Optional debug label.</param>
		public static WindowSurfaceRequest ForWindowsHwnd(IntPtr hwnd, IntPtr hinstance, uint width, uint height, string label = null)
		{
			return new WindowSurfaceRequest(hwnd, hinstance, width, height, label);
		}

		/// <summary>
		/// Describes a macOS <c>CAMetalLayer</c> to make a surface over. The host is responsible for the
		/// layer itself: making the view layer-backed, giving it a <c>CAMetalLayer</c>, and keeping the
		/// layer's <c>drawableSize</c> in step with the <paramref name="width"/> and
		/// <paramref name="height"/> this surface is configured at (Metal will scale a mismatched drawable
		/// rather than complain, which shows up as a soft image on a Retina display and nothing else).
		/// </summary>
		/// <param name="metalLayer">A pointer to the <c>CAMetalLayer</c>, not to the NSView or NSWindow.</param>
		/// <param name="width">Initial swapchain width in pixels.</param>
		/// <param name="height">Initial swapchain height in pixels.</param>
		/// <param name="label">Optional debug label.</param>
		public static WindowSurfaceRequest ForMetalLayer(IntPtr metalLayer, uint width, uint height, string label = null)
		{
			return new WindowSurfaceRequest(metalLayer, IntPtr.Zero, width, height, label);
		}

		/// <summary>
		/// Describes an X11 window to make a surface over. X11 is the only platform here that needs two
		/// values to name a drawable - the connection and the window - so the <c>Display*</c> is what goes
		/// in <see cref="NativeSurfaceHandle"/> and the XID gets its own <see cref="XlibWindow"/>. That
		/// split is deliberate rather than arbitrary: the surface creation path guards on
		/// <c>NativeSurfaceHandle == IntPtr.Zero</c> to mean "no drawable at all", and the display is the
		/// half that is a pointer and can be null, so putting it there keeps that one guard meaningful on
		/// every OS. (An XID is an unsigned long, not a pointer, and on a 32-bit-pointer host would not
		/// even fit in an <see cref="IntPtr"/>, which is why it is a <see cref="ulong"/>.)
		/// </summary>
		/// <param name="display">The <c>Display*</c> from <c>XOpenDisplay</c>. Must stay open for the surface's life.</param>
		/// <param name="window">The X11 window XID. wgpu presents to this drawable directly.</param>
		/// <param name="width">Initial swapchain width in pixels.</param>
		/// <param name="height">Initial swapchain height in pixels.</param>
		/// <param name="label">Optional debug label.</param>
		/// <exception cref="ArgumentException">The display is null or the window is <c>None</c> (zero).</exception>
		public static WindowSurfaceRequest ForXlibWindow(IntPtr display, ulong window, uint width, uint height, string label = null)
		{
			if (display == IntPtr.Zero)
			{
				throw new ArgumentException("An X11 surface needs an open Display*.", nameof(display));
			}

			// Zero is X11's None, never a real window, and wgpu would take it and fail later inside
			// Vulkan rather than at the call that got it wrong.
			if (window == 0)
			{
				throw new ArgumentException("An X11 surface needs a window XID; zero is None.", nameof(window));
			}

			return new WindowSurfaceRequest(display, IntPtr.Zero, window, width, height, label);
		}

		/// <summary>
		/// The platform's native surface handle: an HWND on Windows, a <c>CAMetalLayer*</c> on macOS, the
		/// X11 <c>Display*</c> on Linux (the window itself is <see cref="XlibWindow"/>).
		/// </summary>
		public IntPtr NativeSurfaceHandle { get; }

		/// <summary>The module instance (HINSTANCE), or zero. Windows only; ignored elsewhere.</summary>
		public IntPtr ModuleHandle { get; }

		/// <summary>
		/// The X11 window XID, or zero. Linux/X11 only; ignored elsewhere. Separate from
		/// <see cref="NativeSurfaceHandle"/> because X11 needs both it and the display - see
		/// <see cref="ForXlibWindow"/>.
		/// </summary>
		public ulong XlibWindow { get; }

		/// <summary>Initial swapchain width in pixels.</summary>
		public uint Width { get; }

		/// <summary>Initial swapchain height in pixels.</summary>
		public uint Height { get; }

		/// <summary>Optional debug label.</summary>
		public string Label { get; }
	}
}
