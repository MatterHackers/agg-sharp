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
using System.Windows.Forms;

namespace MatterHackers.WebGpu.Example
{
	/// <summary>
	/// The WinForms half of the HWND spike: a Control that hands its window handle to wgpu and paints
	/// through the swapchain instead of GDI. The minimal form of what
	/// <c>PlatformWin32.win32.WebGpuControl</c> grew into for the real window host.
	/// </summary>
	public class TriangleControl : Control
	{
		private TriangleRenderer renderer;

		public TriangleControl()
		{
			// Painting is entirely wgpu's job; letting WinForms erase or paint the background would
			// flicker over the presented frame.
			this.SetStyle(ControlStyles.UserPaint | ControlStyles.Opaque | ControlStyles.AllPaintingInWmPaint, true);
			this.SetStyle(ControlStyles.OptimizedDoubleBuffer, false);
		}

		public TriangleRenderer Renderer => renderer;

		protected override void OnHandleCreated(EventArgs e)
		{
			base.OnHandleCreated(e);

			renderer = new TriangleRenderer(
				this.Handle,
				Marshal.GetHINSTANCE(typeof(TriangleControl).Module),
				(uint)Math.Max(1, this.ClientSize.Width),
				(uint)Math.Max(1, this.ClientSize.Height));
		}

		protected override void OnHandleDestroyed(EventArgs e)
		{
			renderer?.Dispose();
			renderer = null;

			base.OnHandleDestroyed(e);
		}

		protected override void OnSizeChanged(EventArgs e)
		{
			base.OnSizeChanged(e);

			renderer?.Resize((uint)Math.Max(0, this.ClientSize.Width), (uint)Math.Max(0, this.ClientSize.Height));
		}

		protected override void OnPaint(PaintEventArgs e)
		{
			renderer?.RenderFrame();
		}

		protected override void OnPaintBackground(PaintEventArgs pevent)
		{
			// Intentionally empty - see the style flags in the constructor.
		}
	}
}
