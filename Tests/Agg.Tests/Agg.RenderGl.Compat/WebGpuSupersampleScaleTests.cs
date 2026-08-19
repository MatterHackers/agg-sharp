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
using MatterHackers.RenderGl.Scene;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The full-frame capture's supersample factor against the device's <c>maxTextureDimension2D</c>.
	/// <para>
	/// A fixed 3x capture is only legal while the window stays small. Fullscreen on a retina display is
	/// 3024x1898 device pixels, and 3x that is 9072 - past the 8192 WebGPU grants by default. An
	/// over-limit texture is not refused by wgpu-native with an error return: it hands back a non-null
	/// error texture whose view fails validation inside Rust at the next queue submit, where the panic
	/// cannot unwind across the FFI boundary and aborts the process. So the factor has to come down
	/// instead, and these are the sizes it has to come down at.
	/// </para>
	/// </summary>
	public class WebGpuSupersampleScaleTests
	{
		/// <summary>The default WebGPU 2D texture limit, which is what the crash was measured against.</summary>
		private const uint DefaultLimit = 8192;

		[Test]
		public async Task AWindowSizedFrameKeepsTheFullThreeTimesSupersample()
		{
			// 2178x1336 (a large windowed view) x3 is 6534x4008, inside the limit - and every golden image
			// is captured at 3, so anything at or below 2730 in both axes has to stay there.
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(2178, 1336, DefaultLimit)).IsEqualTo(3);
		}

		[Test]
		public async Task AFullscreenRetinaFrameDropsToTwo()
		{
			// The reported crash: 3024x1898 device pixels. 3x is 9072 and aborts; 2x is 6048 and fits.
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(3024, 1898, DefaultLimit)).IsEqualTo(2);
		}

		[Test]
		public async Task AFrameTooLargeToSupersampleAtAllFallsBackToOne()
		{
			// 6016x3384 (a 6K display) leaves no room for even 2x. Supersampling off is a softer frame;
			// the alternative is a process abort.
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(6016, 3384, DefaultLimit)).IsEqualTo(1);
		}

		[Test]
		public async Task ARaisedDeviceLimitKeepsTheFullSupersampleAtFullscreen()
		{
			// The same fullscreen size against the 16384 every desktop adapter actually supports, which is
			// why the device asks for the adapter's real limit rather than accepting the default.
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(3024, 1898, 16384)).IsEqualTo(3);
		}

		[Test]
		public async Task ASizeBeyondAnySupersampleStillReportsOneRatherThanZero()
		{
			// Nothing can render this within the limit, but returning 0 would size a zero-pixel target and
			// returning a negative would not size one at all. 1 is the floor.
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(30000, 30000, DefaultLimit)).IsEqualTo(1);
		}

		[Test]
		public async Task ADegenerateSizeIsAnsweredRatherThanThrown()
		{
			// A zero or negative viewport reaches here from a collapsed widget; the caller clamps the target
			// size afterwards, so this only has to not throw on the way through.
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(0, 0, DefaultLimit)).IsGreaterThanOrEqualTo(1);
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(-4, -4, DefaultLimit)).IsGreaterThanOrEqualTo(1);
		}

		[Test]
		public async Task AnUnknownDeviceLimitStillLeavesAUsableScale()
		{
			// A device that reported nothing must not turn into a zero-scale frame.
			await Assert.That(WebGpuSceneRenderer.SupersampleScaleFor(800, 600, 0)).IsEqualTo(1);
		}
	}
}
