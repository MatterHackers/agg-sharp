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

using MatterHackers.RenderCore;
using MatterHackers.WebGpu;

namespace MatterHackers.WebGpuRender
{
	/// <summary>
	/// Builders for the webgpu structs whose correct default is <em>not</em> the zero value.
	/// <para>
	/// C# zero-initializes structs, webgpu.h publishes an <c>INIT</c> macro per struct, and where the
	/// two disagree the failure mode is a validation error raised out of band - the call that caused it
	/// succeeds and a later frame comes back blank. The Phase 0 spike hit exactly one of these
	/// (<c>depthSlice</c>); the rest were found by reading every <c>INIT</c> macro this backend touches.
	/// The known offenders, all handled here:
	/// </para>
	/// <list type="bullet">
	/// <item><description><c>WGPURenderPassColorAttachment.depthSlice</c> must be
	/// <c>WGPU_DEPTH_SLICE_UNDEFINED</c>, not 0 (0 means "layer 0 of a 3D texture").</description></item>
	/// <item><description><c>WGPUDepthStencilState.stencilReadMask</c>/<c>stencilWriteMask</c> must be
	/// <c>0xFFFFFFFF</c>.</description></item>
	/// <item><description><c>WGPUMultisampleState.count</c> must be at least 1 and <c>mask</c> must be
	/// <c>0xFFFFFFFF</c> - a zero mask discards every sample.</description></item>
	/// <item><description><c>WGPUSamplerDescriptor.lodMaxClamp</c> must be 32 and <c>maxAnisotropy</c> 1;
	/// a zero lod clamp pins sampling to the top mip.</description></item>
	/// <item><description><c>WGPUBindGroupEntry.size</c> must be <c>WGPU_WHOLE_SIZE</c> for "to the end
	/// of the buffer", not 0.</description></item>
	/// <item><description><c>WGPURenderPassDepthStencilAttachment.depthClearValue</c> must be
	/// <c>WGPU_DEPTH_CLEAR_VALUE_UNDEFINED</c> (NaN) when the depth is loaded rather than cleared.</description></item>
	/// </list>
	/// </summary>
	public static unsafe class WgpuDescriptors
	{
		/// <summary>Every bit set - the mask both the multisample state and the stencil masks default to.</summary>
		public const uint AllBits = 0xFFFFFFFF;

		/// <summary>The lod clamp webgpu.h's sampler INIT macro uses. Zero would pin sampling to mip 0.</summary>
		public const float DefaultLodMaxClamp = 32.0f;

		/// <summary>
		/// A color attachment with <c>depthSlice</c> correctly undefined. This is the Phase 0 finding:
		/// a zero-initialized attachment fails validation on a 2D target.
		/// </summary>
		/// <param name="view">The view written to.</param>
		/// <param name="loadOp">What happens to existing contents.</param>
		/// <param name="storeOp">What happens to the results.</param>
		/// <param name="clearValue">The clear value, used only when <paramref name="loadOp"/> clears.</param>
		public static WGPURenderPassColorAttachment ColorAttachment(
			WGPUTextureView view,
			WGPULoadOp loadOp,
			WGPUStoreOp storeOp,
			WGPUColor clearValue)
			=> new WGPURenderPassColorAttachment
			{
				view = view,
				depthSlice = WGPUConstants.WGPU_DEPTH_SLICE_UNDEFINED,
				loadOp = loadOp,
				storeOp = storeOp,
				clearValue = clearValue,
			};

		/// <summary>
		/// A depth attachment for a depth-only format. The stencil load and store ops stay
		/// <c>Undefined</c> deliberately: a format with no stencil aspect must not carry stencil ops, and
		/// zero already is Undefined.
		/// </summary>
		/// <param name="view">The depth view written to.</param>
		/// <param name="loadOp">What happens to existing depth.</param>
		/// <param name="storeOp">What happens to the resulting depth.</param>
		/// <param name="clearValue">The depth cleared to, used only when <paramref name="loadOp"/> clears.</param>
		public static WGPURenderPassDepthStencilAttachment DepthAttachment(
			WGPUTextureView view,
			WGPULoadOp loadOp,
			WGPUStoreOp storeOp,
			float clearValue)
			=> new WGPURenderPassDepthStencilAttachment
			{
				view = view,
				depthLoadOp = loadOp,
				depthStoreOp = storeOp,
				depthClearValue = loadOp == WGPULoadOp.Clear
					? clearValue
					: WGPUConstants.WGPU_DEPTH_CLEAR_VALUE_UNDEFINED,
			};

		/// <summary>Multisample state with the non-zero defaults filled in.</summary>
		/// <param name="sampleCount">Samples per pixel; 1 for no multisampling.</param>
		public static WGPUMultisampleState Multisample(uint sampleCount)
			=> new WGPUMultisampleState
			{
				count = sampleCount == 0 ? 1 : sampleCount,
				mask = AllBits,
				alphaToCoverageEnabled = false,
			};

		/// <summary>Depth state with the stencil masks webgpu.h's INIT macro sets.</summary>
		/// <param name="state">The RenderCore depth state; must have a depth format.</param>
		public static WGPUDepthStencilState DepthStencil(in DepthStencilState state)
			=> new WGPUDepthStencilState
			{
				format = WgpuEnums.ToWgpu(state.Format),
				depthWriteEnabled = state.DepthWriteEnabled ? WGPUOptionalBool.True : WGPUOptionalBool.False,
				depthCompare = WgpuEnums.ToWgpu(state.DepthCompare),
				stencilReadMask = AllBits,
				stencilWriteMask = AllBits,
				depthBias = state.DepthBias,
				depthBiasSlopeScale = state.DepthBiasSlopeScale,
				depthBiasClamp = state.DepthBiasClamp,
			};

		/// <summary>Sampler descriptor with the non-zero lod and anisotropy defaults filled in.</summary>
		/// <param name="descriptor">The RenderCore sampler state.</param>
		/// <param name="label">Label view; must outlive the create call.</param>
		public static WGPUSamplerDescriptor Sampler(in SamplerDescriptor descriptor, WGPUStringView label)
			=> new WGPUSamplerDescriptor
			{
				label = label,
				addressModeU = WgpuEnums.ToWgpu(descriptor.AddressModeU),
				addressModeV = WgpuEnums.ToWgpu(descriptor.AddressModeV),
				addressModeW = WGPUAddressMode.ClampToEdge,
				magFilter = WgpuEnums.ToWgpu(descriptor.MagFilter),
				minFilter = WgpuEnums.ToWgpu(descriptor.MinFilter),
				mipmapFilter = WgpuEnums.ToWgpuMipmap(descriptor.MipmapFilter),
				lodMinClamp = 0,
				lodMaxClamp = DefaultLodMaxClamp,
				compare = WGPUCompareFunction.Undefined,
				maxAnisotropy = 1,
			};

		/// <summary>Converts a clear color. Component order and type are already webgpu's.</summary>
		/// <param name="color">The RenderCore clear value.</param>
		public static WGPUColor Color(in ClearColor color)
			=> new WGPUColor { r = color.Red, g = color.Green, b = color.Blue, a = color.Alpha };
	}
}
