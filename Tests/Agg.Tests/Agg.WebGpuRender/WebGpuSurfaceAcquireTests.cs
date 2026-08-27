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
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The swapchain policy decisions - what to do with each acquire status, how a surface size is
	/// clamped, and which surface format the swapchain is configured with - as pure functions, so they
	/// are pinned without a live GPU. The same three policies live in agg-gui-wgpu's <c>gpu.rs</c>
	/// (<c>surface_acquire_action</c>, <c>clamp_surface_size</c>, <c>pick_surface_format</c>) and the two
	/// implementations are kept in step deliberately.
	/// <para>
	/// The browser-shaped cases here are the only part of the browser render path a desktop suite can
	/// judge: a canvas reports its own capability list and its own (empty) set of present modes, and those
	/// two answers decide whether a browser build configures a swapchain at all.
	/// </para>
	/// </summary>
	public class WebGpuSurfaceAcquireTests
	{
		[Test]
		public async Task AStaleSwapchainReconfiguresRatherThanSkipping()
		{
			// What a resize looks like from the acquire: reconfiguring at the known size is the only way
			// back, so these must not be a silent skip (the resize-black-screen regression).
			await Assert.That(WebGpuSurfaceTarget.ActionFor(WGPUSurfaceGetCurrentTextureStatus.Outdated))
				.IsEqualTo(SurfaceAcquireAction.Reconfigure);
			await Assert.That(WebGpuSurfaceTarget.ActionFor(WGPUSurfaceGetCurrentTextureStatus.Lost))
				.IsEqualTo(SurfaceAcquireAction.Reconfigure);
		}

		[Test]
		public async Task ATimeoutSkipsTheFrameAndAsksForAnother()
		{
			// A Timeout means the compositor was simply not ready - the swapchain is still valid, so
			// reconfiguring it would throw away a perfectly good one and can itself provoke another
			// Timeout. Skip, but wake back up: a reactive host would otherwise idle forever.
			await Assert.That(WebGpuSurfaceTarget.ActionFor(WGPUSurfaceGetCurrentTextureStatus.Timeout))
				.IsEqualTo(SurfaceAcquireAction.SkipAndRetry);
		}

		[Test]
		public async Task OccludedAndValidationErrorsSkipWithoutSpinning()
		{
			// Occluded: the window is not visible, so a self-requested redraw would just burn CPU.
			// Error: the C-API validation status - a retry gets the same answer, and it must not take the
			// window down the way an unknown status does.
			await Assert.That(WebGpuSurfaceTarget.ActionFor(WebGpuSurfaceTarget.OccludedStatus))
				.IsEqualTo(SurfaceAcquireAction.Skip);
			await Assert.That(WebGpuSurfaceTarget.ActionFor(WGPUSurfaceGetCurrentTextureStatus.Error))
				.IsEqualTo(SurfaceAcquireAction.Skip);
		}

		[Test]
		public async Task ASuccessfulAcquirePresents()
		{
			await Assert.That(WebGpuSurfaceTarget.ActionFor(WGPUSurfaceGetCurrentTextureStatus.SuccessOptimal))
				.IsEqualTo(SurfaceAcquireAction.Present);
			await Assert.That(WebGpuSurfaceTarget.ActionFor(WGPUSurfaceGetCurrentTextureStatus.SuccessSuboptimal))
				.IsEqualTo(SurfaceAcquireAction.Present);
		}

		[Test]
		public async Task AnUnknownStatusIsAFailure()
		{
			// Anything the header does not define is a driver or binding bug; the acquire throws so it is
			// seen rather than silently dropping every frame.
			await Assert.That(WebGpuSurfaceTarget.ActionFor((WGPUSurfaceGetCurrentTextureStatus)0x7FFF0001))
				.IsEqualTo(SurfaceAcquireAction.Fail);
		}

		[Test]
		public async Task SurfaceSizesAreClampedToTheDeviceLimit()
		{
			// An over-large window, or a corrupted restored size, degrades to the GPU limit instead of
			// failing wgpu validation; zero (minimized) becomes one.
			await Assert.That(WebGpuSurfaceTarget.ClampSurfaceSize(0, 0, 8192)).IsEqualTo((1u, 1u));
			await Assert.That(WebGpuSurfaceTarget.ClampSurfaceSize(20000, 100, 8192)).IsEqualTo((8192u, 100u));
			await Assert.That(WebGpuSurfaceTarget.ClampSurfaceSize(320, 240, 8192)).IsEqualTo((320u, 240u));

			// A device that reports a zero limit still has to produce a legal (1x1) configuration.
			await Assert.That(WebGpuSurfaceTarget.ClampSurfaceSize(320, 240, 0)).IsEqualTo((1u, 1u));
		}

		[Test]
		public async Task Bgra8IsPreferredAndSrgbIsAvoided()
		{
			// Bgra8Unorm keeps the golden images the same pixels the window shows.
			await Assert.That(WebGpuRenderDevice.PickSurfaceFormat(new[]
				{
					WGPUTextureFormat.RGBA8Unorm,
					WGPUTextureFormat.BGRA8Unorm,
				}))
				.IsEqualTo(WGPUTextureFormat.BGRA8Unorm);

			// Without Bgra8Unorm, any non-sRGB format beats the surface's own first preference: the 2D
			// stack already writes gamma-encoded bytes, so an sRGB view would encode them a second time.
			await Assert.That(WebGpuRenderDevice.PickSurfaceFormat(new[]
				{
					WGPUTextureFormat.BGRA8UnormSrgb,
					WGPUTextureFormat.RGBA8Unorm,
				}))
				.IsEqualTo(WGPUTextureFormat.RGBA8Unorm);

			// All-sRGB surface: nothing better exists, so take the surface's first preference.
			await Assert.That(WebGpuRenderDevice.PickSurfaceFormat(new[]
				{
					WGPUTextureFormat.BGRA8UnormSrgb,
					WGPUTextureFormat.RGBA8UnormSrgb,
				}))
				.IsEqualTo(WGPUTextureFormat.BGRA8UnormSrgb);
		}

		[Test]
		public async Task ABrowserShapedCapabilityListStillLandsOnBgra8()
		{
			// What a canvas offers: emdawnwebgpu answers getCapabilities with the two 8-bit RGBA orders,
			// preferred format first, and which one is first depends on the platform the browser is running
			// on. Both orders must still choose Bgra8Unorm, or a browser capture and a desktop golden stop
			// being the same pixels for a reason nobody would look for.
			await Assert.That(WebGpuRenderDevice.PickSurfaceFormat(new[]
				{
					WGPUTextureFormat.BGRA8Unorm,
					WGPUTextureFormat.RGBA8Unorm,
				}))
				.IsEqualTo(WGPUTextureFormat.BGRA8Unorm);

			await Assert.That(WebGpuRenderDevice.PickSurfaceFormat(new[]
				{
					WGPUTextureFormat.RGBA8Unorm,
					WGPUTextureFormat.BGRA8Unorm,
					WGPUTextureFormat.RGBA16Float,
				}))
				.IsEqualTo(WGPUTextureFormat.BGRA8Unorm);
		}

		[Test]
		public async Task ASurfaceThatOffersNothingIsAFailureRatherThanAGuess()
		{
			// Deliberately not a fallback. An empty capability list means this adapter cannot present to
			// this surface at all; configuring an invented format would move the failure into wgpu's
			// validation - out of band, several calls later, with nothing pointing back here. The same
			// answer for null, which is what a binding that skipped the query hands over.
			await Assert.That(() => WebGpuRenderDevice.PickSurfaceFormat(Array.Empty<WGPUTextureFormat>()))
				.Throws<InvalidOperationException>();
			await Assert.That(() => WebGpuRenderDevice.PickSurfaceFormat(null))
				.Throws<InvalidOperationException>();
		}

		[Test]
		public async Task AnUnsupportedPresentModeDegradesToFifo()
		{
			// Fifo is the one mode WebGPU guarantees, so it is granted without being looked for - which is
			// what makes a surface that reports no modes at all (a canvas: the page paces presents through
			// requestAnimationFrame and there is nothing to choose) configurable rather than fatal.
			await Assert.That(WebGpuSurfaceTarget.ResolvePresentMode(WGPUPresentMode.Fifo, Array.Empty<WGPUPresentMode>()))
				.IsEqualTo(WGPUPresentMode.Fifo);

			// AGG_PRESENT_MODE=immediate reaches a wasm build's environment as easily as a desktop one, and
			// must not take the window down where it cannot be honoured.
			await Assert.That(WebGpuSurfaceTarget.ResolvePresentMode(WGPUPresentMode.Immediate, Array.Empty<WGPUPresentMode>()))
				.IsEqualTo(WGPUPresentMode.Fifo);
			await Assert.That(WebGpuSurfaceTarget.ResolvePresentMode(WGPUPresentMode.Mailbox, null))
				.IsEqualTo(WGPUPresentMode.Fifo);

			// And a mode the surface really does offer is taken.
			await Assert.That(WebGpuSurfaceTarget.ResolvePresentMode(
					WGPUPresentMode.Immediate,
					new[] { WGPUPresentMode.Fifo, WGPUPresentMode.Immediate }))
				.IsEqualTo(WGPUPresentMode.Immediate);
		}
	}
}
