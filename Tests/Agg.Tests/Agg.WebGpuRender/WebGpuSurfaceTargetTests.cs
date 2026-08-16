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
using System.Windows.Forms;
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The swapchain half of the backend, against a real (hidden) window: surface creation from an HWND,
	/// the per-frame acquire/present handshake, and reconfiguration on resize.
	/// <para>
	/// A hidden form is enough - a window does not have to be visible to own a swapchain - which keeps
	/// these as fast as the offscreen tests and stops them stealing focus from whoever is at the machine.
	/// </para>
	/// </summary>
	[NotInParallel]
	public class WebGpuSurfaceTargetTests
	{
		[Test]
		public async Task ASurfaceOverAnHwndAcquiresPresentsAndResizes()
		{
			using (var form = new Form { Width = 320, Height = 240, ShowInTaskbar = false })
			{
				form.CreateControl();
				_ = form.Handle;

				using (var device = new WebGpuRenderDevice(false, WGPUBackendType.D3D12, "surfaceTests"))
				using (var surface = device.CreateSurfaceTarget(form.Handle, IntPtr.Zero, 320, 240, "testWindow"))
				{
					await Assert.That(surface.Width).IsEqualTo(320u);
					await Assert.That(surface.Height).IsEqualTo(240u);

					// Bgra8 is what the goldens were captured in, and every Windows swapchain offers it -
					// if this ever changes, the window and the golden images stop being the same pixels.
					await Assert.That(surface.Format).IsEqualTo(TextureFormat.Bgra8Unorm);

					var frame = surface.AcquireCurrentTexture();
					await Assert.That(frame).IsNotNull();
					await Assert.That(frame.Descriptor.Width).IsEqualTo(320u);

					// Acquiring twice in a frame yields the same texture rather than a second one: the host
					// calls it from every NewGraphics2D and must not end up with two frames in flight.
					await Assert.That(surface.AcquireCurrentTexture()).IsEqualTo(frame);

					using (var encoder = device.BeginRenderPass(
						new RenderPassDescriptor(frame, LoadOp.Clear, new ClearColor(0, 0.5, 1, 1), "surfaceFrame")))
					{
					}

					device.Present(surface);

					await Assert.That(surface.PresentedFrameCount).IsEqualTo(1L);
					await Assert.That(surface.CurrentTexture).IsNull();
					await Assert.That(device.LastUncapturedError).IsNull();

					// Resizing is explicit in WebGPU: without the reconfigure the next acquire would answer
					// Outdated forever.
					surface.Configure(200, 150);
					await Assert.That(surface.Width).IsEqualTo(200u);

					var resizedFrame = surface.AcquireCurrentTexture();
					await Assert.That(resizedFrame).IsNotNull();
					await Assert.That(resizedFrame.Descriptor.Height).IsEqualTo(150u);

					using (var encoder = device.BeginRenderPass(
						new RenderPassDescriptor(resizedFrame, LoadOp.Clear, ClearColor.Black, "resizedFrame")))
					{
					}

					device.Present(surface);
					await Assert.That(surface.PresentedFrameCount).IsEqualTo(2L);
					await Assert.That(device.LastUncapturedError).IsNull();
				}
			}
		}

		[Test]
		public async Task PresentingIsRefusedWhileAPassIsOpen()
		{
			using (var form = new Form { Width = 64, Height = 64, ShowInTaskbar = false })
			{
				form.CreateControl();

				using (var device = new WebGpuRenderDevice(false, WGPUBackendType.D3D12, "surfaceTests"))
				using (var surface = device.CreateSurfaceTarget(form.Handle, IntPtr.Zero, 64, 64, "testWindow"))
				{
					var frame = surface.AcquireCurrentTexture();
					var encoder = device.BeginRenderPass(new RenderPassDescriptor(frame, LoadOp.Clear, ClearColor.Black));

					try
					{
						await Assert.That(() => device.Present(surface)).Throws<InvalidOperationException>();
					}
					finally
					{
						encoder.Dispose();
					}

					device.Present(surface);
					await Assert.That(surface.PresentedFrameCount).IsEqualTo(1L);
				}
			}
		}
	}
}
