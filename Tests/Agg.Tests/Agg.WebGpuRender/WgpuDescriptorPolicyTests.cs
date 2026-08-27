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
using MatterHackers.WebGpu;
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The descriptor defaults that differ between wgpu-native and the browser. Pure policy, so both legs
	/// are testable from any desktop OS - which matters, because the browser leg is otherwise only
	/// exercised by publishing a wasm head and looking at it.
	/// </summary>
	public class WgpuDescriptorPolicyTests
	{
		/// <summary>
		/// The desktop answer is webgpu.h's own INIT macro, and every golden image was captured through it.
		/// </summary>
		[Test]
		public async Task ALoadedDepthAttachmentIsUndefinedOnTheDesktop()
		{
			float value = WgpuDescriptors.DepthClearValue(WGPULoadOp.Load, 1.0f, forBrowser: false);

			await Assert.That(float.IsNaN(value)).IsTrue();
		}

		/// <summary>
		/// The browser cannot take the NaN: emdawnwebgpu hands the float straight to beginRenderPass, whose
		/// WebIDL parameter is a restricted float, so a NaN throws a TypeError out of the first render pass
		/// of the first frame - which is a canvas that never paints, not a validation warning.
		/// </summary>
		[Test]
		public async Task ALoadedDepthAttachmentIsFiniteInTheBrowser()
		{
			float value = WgpuDescriptors.DepthClearValue(WGPULoadOp.Load, 1.0f, forBrowser: true);

			await Assert.That(float.IsFinite(value)).IsTrue();
		}

		/// <summary>A clearing attachment carries the value asked for, on both implementations.</summary>
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task AClearingDepthAttachmentCarriesTheRequestedValue(bool forBrowser)
		{
			await Assert.That(WgpuDescriptors.DepthClearValue(WGPULoadOp.Clear, 0.25f, forBrowser)).IsEqualTo(0.25f);
		}
	}
}
