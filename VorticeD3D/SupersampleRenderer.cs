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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.RenderGl.OpenGl;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// 3x3 (9x) supersampled anti-aliasing. The whole frame is rendered into an
	/// off-screen target at 3x the backbuffer resolution, then downsampled to the
	/// backbuffer with a 9-tap box filter — one render pass, immediate result.
	/// This replaced the temporal 16x Halton-jitter accumulator: no multi-frame
	/// convergence, no scene-change fingerprinting, the same quality every frame.
	/// Full-frame capture swaps renderTargetView/depthStencilView so ALL rendering
	/// (scene pipeline AND GL immediate mode gizmos/lines/controls) goes to the
	/// supersample target.
	/// </summary>
	public partial class VorticeD3DGl
	{
		/// <summary>
		/// Linear supersample factor: the capture target is 3x the backbuffer in
		/// each dimension, so every screen pixel averages a 3x3 block of samples.
		/// </summary>
		public const int SupersampleScale = 3;

		// Full-frame capture: off-screen target at SupersampleScale times the
		// backbuffer size that receives ALL rendering during capture.
		private ColorTextureTarget sampleFrameTarget;
		private ID3D11Texture2D sampleFrameDepthTexture;
		private ID3D11DepthStencilView sampleFrameDepthView;
		private ID3D11RenderTargetView savedMainRTV;
		private ID3D11DepthStencilView savedMainDSV;
		private int savedRenderTargetHeight;

		private ID3D11PixelShader downsamplePS;
		private ID3D11Buffer downsampleBuffer;

		// The device-pixel scale between logical GL/backbuffer coordinates and the
		// active default render target. 1 normally; SupersampleScale while a
		// full-frame capture is redirecting rendering to the 3x sample target.
		// Applied wherever logical coordinates cross into D3D device coordinates
		// (viewport, scissor, scene-effect target sizes, pixel-space shader widths).
		private int supersampleScale = 1;

		/// <summary>
		/// Redirects all subsequent rendering to the off-screen supersample target by
		/// swapping the renderTargetView and depthStencilView fields. The target is
		/// SupersampleScale times the backbuffer size; Viewport/Scissor and the scene
		/// pipeline scale all logical coordinates by <see cref="supersampleScale"/> so
		/// GL→D3D coordinate math lands on the same relative positions.
		/// </summary>
		public void BeginFullFrameCapture(Agg.RectangleDouble viewport)
		{
			if (currentBackBuffer == null)
			{
				return;
			}

			int width = (int)currentBackBuffer.Description.Width * SupersampleScale;
			int height = (int)currentBackBuffer.Description.Height * SupersampleScale;

			sampleFrameTarget = EnsureColorTarget(sampleFrameTarget, width, height, Format.R8G8B8A8_UNorm);
			EnsureSampleFrameDepth(width, height);

			// Save the real render targets
			savedMainRTV = renderTargetView;
			savedMainDSV = depthStencilView;
			savedRenderTargetHeight = renderTargetHeight;

			// Swap to the supersample target — all rendering now goes here
			renderTargetView = sampleFrameTarget.RenderTargetView;
			depthStencilView = sampleFrameDepthView;
			supersampleScale = SupersampleScale;
			renderTargetHeight = height;

			// Clear to transparent so only the 3D viewport region has content.
			// When blitting back, transparent pixels contribute nothing via alpha blending.
			context.ClearRenderTargetView(renderTargetView, new Color4(0, 0, 0, 0));
			context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
			context.OMSetRenderTargets(renderTargetView, depthStencilView);

			// Re-apply viewport and scissor so they pick up the new scale
			Viewport(viewportX, viewportY, viewportWidth, viewportHeight);
			if (scissorEnabled)
			{
				Scissor(scissorX, scissorY, scissorWidth, scissorHeight);
			}
		}

		/// <summary>
		/// Restores the original render targets after full-frame capture.
		/// </summary>
		public void EndFullFrameCapture()
		{
			if (savedMainRTV == null)
			{
				return;
			}

			renderTargetView = savedMainRTV;
			depthStencilView = savedMainDSV;
			savedMainRTV = null;
			savedMainDSV = null;
			supersampleScale = 1;
			renderTargetHeight = savedRenderTargetHeight;

			context.OMSetRenderTargets(renderTargetView, depthStencilView);

			// Re-apply viewport and scissor at the restored 1x scale
			Viewport(viewportX, viewportY, viewportWidth, viewportHeight);
			if (scissorEnabled)
			{
				Scissor(scissorX, scissorY, scissorWidth, scissorHeight);
			}
		}

		/// <summary>
		/// Box-downsamples the 3x supersample target onto the backbuffer with a
		/// 9-tap filter. Call after EndFullFrameCapture — this completes the frame.
		/// </summary>
		public void DownsampleAndBlitFullFrame()
		{
			if (sampleFrameTarget == null)
			{
				return;
			}

			EnsureDownsampleResources();

			// Upload the source texel size for the 9-tap box filter
			var mapped = context.Map(downsampleBuffer, MapMode.WriteDiscard);
			unsafe
			{
				float* ptr = (float*)mapped.DataPointer;
				ptr[0] = 1.0f / sampleFrameTarget.Width;
				ptr[1] = 1.0f / sampleFrameTarget.Height;
				ptr[2] = 0;
				ptr[3] = 0;
			}

			context.Unmap(downsampleBuffer, 0);

			context.OMSetRenderTargets(renderTargetView, depthStencilView);

			// The supersample texture covers the whole backbuffer (scaled 3x) with the
			// 3D content in the viewport region and transparent (0,0,0,0) everywhere
			// else. Blit with full-backbuffer viewport and alpha blending — transparent
			// pixels outside the 3D viewport contribute nothing to the backbuffer.
			int bbWidth = (int)(currentBackBuffer?.Description.Width ?? (uint)renderTargetHeight);
			int bbHeight = (int)(currentBackBuffer?.Description.Height ?? (uint)renderTargetHeight);
			context.RSSetViewport(new Viewport(bbWidth, bbHeight));

			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(downsamplePS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetConstantBuffer(2, downsampleBuffer);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			// Use One/OneMinusSrcAlpha (premultiplied alpha blending) because the
			// captured content has premultiplied RGB (from SrcAlpha blending when
			// the scene was rendered to the sample target). Using SrcAlpha here would
			// double-premultiply, making semi-transparent content like the bed too dark.
			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));
			context.PSSetShaderResource(0, sampleFrameTarget.ShaderResourceView);
			context.Draw(3, 0);
			context.PSSetShaderResource(0, null);

			// Invalidate GL state tracking — we set D3D blend/depth/rasterizer state
			// directly above, bypassing the GL abstraction. Without this, the next
			// ApplyRenderState() may skip re-applying the correct state because
			// lastApplied* still points to the pre-blit values.
			lastAppliedBlendState = null;
			lastAppliedDepthStencilState = null;
			lastAppliedRasterizerState = null;
			renderStateDirty = true;
		}

		private void EnsureDownsampleResources()
		{
			if (downsamplePS != null)
			{
				return;
			}

			string postProcessHlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.NodeDesignerPostProcess.hlsl");
			byte[] downsamplePsByteCode = Vortice.D3DCompiler.Compiler.Compile(
				postProcessHlsl, "Downsample3x3PS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			downsamplePS = device.CreatePixelShader(downsamplePsByteCode);

			downsampleBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = 16, // one float4: DownsampleSettings
				Usage = ResourceUsage.Dynamic,
				BindFlags = BindFlags.ConstantBuffer,
				CPUAccessFlags = CpuAccessFlags.Write,
			});
		}

		private void EnsureSampleFrameDepth(int width, int height)
		{
			if (sampleFrameDepthTexture != null)
			{
				var desc = sampleFrameDepthTexture.Description;
				if ((int)desc.Width == width && (int)desc.Height == height)
				{
					return;
				}

				sampleFrameDepthView?.Dispose();
				sampleFrameDepthTexture.Dispose();
			}

			sampleFrameDepthTexture = device.CreateTexture2D(new Texture2DDescription
			{
				Width = (uint)width,
				Height = (uint)height,
				MipLevels = 1,
				ArraySize = 1,
				Format = Format.D24_UNorm_S8_UInt,
				SampleDescription = new SampleDescription(1, 0),
				Usage = ResourceUsage.Default,
				BindFlags = BindFlags.DepthStencil,
			});
			sampleFrameDepthView = device.CreateDepthStencilView(sampleFrameDepthTexture);
		}

		private void DisposeSupersampleResources()
		{
			downsamplePS?.Dispose();
			downsamplePS = null;
			downsampleBuffer?.Dispose();
			downsampleBuffer = null;
			sampleFrameTarget?.Dispose();
			sampleFrameTarget = null;
			sampleFrameDepthView?.Dispose();
			sampleFrameDepthView = null;
			sampleFrameDepthTexture?.Dispose();
			sampleFrameDepthTexture = null;
		}
	}
}
