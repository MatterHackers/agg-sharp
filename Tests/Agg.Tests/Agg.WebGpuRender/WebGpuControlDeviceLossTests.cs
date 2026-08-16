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

using System.Threading.Tasks;
using System.Windows.Forms;
using MatterHackers.Agg.UI;
using MatterHackers.WebGpu;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.WebGpuRender
{
	/// <summary>
	/// <c>WebGpuControl</c>'s device-loss recovery, driven for real: the device is destroyed, wgpu raises
	/// the device-lost callback, and the control has to notice at the top of the next frame and rebuild
	/// everything hanging off the dead device.
	/// </summary>
	/// <remarks>
	/// Needs a real HWND (the control makes its swapchain from one), so these run on an off-screen WinForms
	/// form rather than headless, and never in parallel with anything else that owns a wgpu device.
	/// </remarks>
	[NotInParallel]
	public class WebGpuControlDeviceLossTests
	{
		/// <summary>Creates the native windows without showing anything.</summary>
		/// <param name="host">The owning form.</param>
		/// <param name="control">The control that needs an HWND.</param>
		private static void ForceHandles(Form host, WebGpuControl control)
		{
			_ = host.Handle;
			_ = control.Handle;
		}

		[Test]
		public async Task DestroyedDeviceIsRebuiltOnTheNextFrame()
		{
			using var host = new Form { Width = 320, Height = 240 };
			using var control = new WebGpuControl { Dock = DockStyle.Fill };

			host.Controls.Add(control);

			// Touching Handle rather than showing the form: the control needs an HWND to make a surface
			// over, and CreateControl is a no-op while the parent is invisible - reading Handle forces the
			// window to exist without putting anything on screen or stealing focus.
			ForceHandles(host, control);
			control.InitializeWebGpu();

			await Assert.That(control.IsWebGpuInitialized).IsTrue();

			var originalDevice = control.Device;
			var originalGl = control.Gl;

			// One healthy frame first, so recovery is being asked of a control in its normal steady state.
			control.BeginFrame();
			control.Present();

			originalDevice.DestroyDeviceToSimulateLoss();

			await Assert.That(originalDevice.IsDeviceLost).IsTrue()
				.Because("wgpuDeviceDestroy must raise the device-lost callback, or the recovery has nothing to trigger on");

			// The frame that notices. It draws nothing - the point is that the control comes back with a
			// live device instead of throwing or rendering into freed memory.
			control.BeginFrame();

			await Assert.That(control.DeviceRecoveryCount).IsEqualTo(1);
			await Assert.That(control.IsWebGpuInitialized).IsTrue();
			await Assert.That(control.Device).IsNotSameReferenceAs(originalDevice);
			await Assert.That(control.Device.IsDeviceLost).IsFalse();

			// A new GL facade, not the old one re-pointed: every cache in Graphics2DGpu is keyed on it, so
			// reusing the instance would hand the new device the dead one's texture and display-list ids.
			await Assert.That(control.Gl).IsNotSameReferenceAs(originalGl);

			// And the rebuilt control renders: this is the frame a repainting app would draw.
			control.BeginFrame();
			control.Present();

			await Assert.That(control.Device.LastUncapturedError).IsNull();
		}

		/// <summary>
		/// <c>AGG_PRESENT_MODE</c> reaches the swapchain. Immediate is what the automation harness sets, so
		/// this is the wiring the both-provider matrix's timings depend on.
		/// </summary>
		[Test]
		public async Task PresentModeIsConfigurable()
		{
			using var host = new Form { Width = 320, Height = 240 };
			using var control = new WebGpuControl { Dock = DockStyle.Fill };

			host.Controls.Add(control);
			ForceHandles(host, control);
			control.InitializeWebGpu();

			control.PresentMode = WGPUPresentMode.Immediate;

			// Not asserted as Immediate outright: a surface that does not support it falls back to Fifo by
			// design, and this test must not fail on a machine whose driver refuses tearing.
			await Assert.That(control.PresentMode == WGPUPresentMode.Immediate
				|| control.PresentMode == WGPUPresentMode.Fifo).IsTrue();

			control.BeginFrame();
			control.Present();

			await Assert.That(control.Device.LastUncapturedError).IsNull();

			control.PresentMode = WGPUPresentMode.Fifo;
			await Assert.That(control.PresentMode).IsEqualTo(WGPUPresentMode.Fifo);

			control.BeginFrame();
			control.Present();

			await Assert.That(control.Device.LastUncapturedError).IsNull();
		}
	}
}
