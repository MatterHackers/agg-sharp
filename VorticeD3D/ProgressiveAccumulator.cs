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

using System;
using MatterHackers.RenderGl.OpenGl;
using Vortice.Direct3D;
using Vortice.Direct3D11;
using Vortice.DXGI;
using Vortice.Mathematics;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// Progressive anti-aliasing via jittered sub-pixel accumulation.
	/// When the scene is static, each frame renders with a tiny sub-pixel offset
	/// and the results are blended into an accumulation buffer using a running average.
	/// Full-frame capture swaps renderTargetView/depthStencilView so ALL rendering
	/// (scene pipeline AND GL immediate mode gizmos/lines/controls) goes to the sample target.
	/// </summary>
	public partial class VorticeD3DGl
	{
		// Ping-pong accumulation targets (R16G16B16A16_Float for precision)
		private ColorTextureTarget accumTargetA;
		private ColorTextureTarget accumTargetB;
		private ID3D11PixelShader accumulatePS;
		private ID3D11Buffer accumulationBuffer;
		private int accumulatedSampleCount;
		private double jitterOffsetX;
		private double jitterOffsetY;

		// Full-frame capture: off-screen target that receives ALL rendering.
		// Sized to match the backbuffer so all GL→D3D coordinate math works identically.
		private ColorTextureTarget sampleFrameTarget;
		private ID3D11Texture2D sampleFrameDepthTexture;
		private ID3D11DepthStencilView sampleFrameDepthView;
		private ID3D11RenderTargetView savedMainRTV;
		private ID3D11DepthStencilView savedMainDSV;

		public int MaxAccumulationSamples { get; set; } = 16;

		public int AccumulatedSampleCount => accumulatedSampleCount;

		public bool IsAccumulationComplete => accumulatedSampleCount >= MaxAccumulationSamples;

		/// <summary>
		/// Sets the sub-pixel jitter offset for the current frame.
		/// Applied to the projection matrix in UpdateTransformBuffer without
		/// modifying WorldView (which would trigger invalidation).
		/// Values are in pixel units, typically in [-0.5, 0.5].
		/// </summary>
		public void SetJitterOffset(double x, double y)
		{
			jitterOffsetX = x;
			jitterOffsetY = y;
		}

		/// <summary>
		/// Resets the accumulation buffer and sample count.
		/// Call when any scene state changes (camera, objects, options).
		/// </summary>
		public void ResetAccumulation()
		{
			accumulatedSampleCount = 0;
		}

		/// <summary>
		/// Redirects all subsequent rendering to an off-screen sample target by swapping
		/// the renderTargetView and depthStencilView fields. The sample target is
		/// backbuffer-sized so all GL→D3D coordinate conversions work identically
		/// to normal rendering — no offset adjustments needed anywhere.
		/// </summary>
		public void BeginFullFrameCapture(Agg.RectangleDouble viewport)
		{
			if (currentBackBuffer == null)
			{
				return;
			}

			int width = (int)currentBackBuffer.Description.Width;
			int height = (int)currentBackBuffer.Description.Height;

			sampleFrameTarget = EnsureColorTarget(sampleFrameTarget, width, height, Format.R8G8B8A8_UNorm);
			EnsureSampleFrameDepth(width, height);

			// Save the real render targets
			savedMainRTV = renderTargetView;
			savedMainDSV = depthStencilView;

			// Swap to sample target — all rendering now goes here
			renderTargetView = sampleFrameTarget.RenderTargetView;
			depthStencilView = sampleFrameDepthView;

			// Clear to transparent so only the 3D viewport region has content.
			// When blitting back, transparent pixels contribute nothing via alpha blending.
			context.ClearRenderTargetView(renderTargetView, new Color4(0, 0, 0, 0));
			context.ClearDepthStencilView(depthStencilView, DepthStencilClearFlags.Depth | DepthStencilClearFlags.Stencil, 1.0f, 0);
			context.OMSetRenderTargets(renderTargetView, depthStencilView);
			// renderTargetHeight stays unchanged — target matches backbuffer dimensions
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

			context.OMSetRenderTargets(renderTargetView, depthStencilView);
		}

		/// <summary>
		/// Blends the full-frame captured sample into the accumulation buffer
		/// and blits the accumulated result to the screen.
		/// </summary>
		public void AccumulateAndBlitFullFrame()
		{
			if (sampleFrameTarget == null)
			{
				return;
			}

			EnsureAccumulationResources();

			int width = sampleFrameTarget.Width;
			int height = sampleFrameTarget.Height;
			accumTargetA = EnsureColorTarget(accumTargetA, width, height, Format.R16G16B16A16_Float);
			accumTargetB = EnsureColorTarget(accumTargetB, width, height, Format.R16G16B16A16_Float);

			if (accumulatedSampleCount == 0)
			{
				// First sample: copy directly to accumulation target A
				BlitTextureToTarget(sampleFrameTarget.ShaderResourceView, accumTargetA);
			}
			else
			{
				// Blend new sample into accumulation using running average
				float weight = 1.0f / (accumulatedSampleCount + 1);
				BlendSampleIntoAccumulation(sampleFrameTarget.ShaderResourceView, weight);
			}

			accumulatedSampleCount++;

			// Blit accumulation result to screen
			BlitAccumulationToScreen();
		}

		/// <summary>
		/// Composites the previously accumulated result to the screen without re-rendering.
		/// Use when the scene fingerprint is unchanged and accumulation is complete.
		/// </summary>
		public void CompositeAccumulatedResult()
		{
			if (accumTargetA == null || accumulatedSampleCount == 0)
			{
				return;
			}

			BlitAccumulationToScreen();
		}

		private void EnsureAccumulationResources()
		{
			if (accumulatePS != null)
			{
				return;
			}

			string postProcessHlsl = ReadEmbeddedResource("MatterHackers.VorticeD3D.Shaders.NodeDesignerPostProcess.hlsl");
			byte[] accumPsByteCode = Vortice.D3DCompiler.Compiler.Compile(
				postProcessHlsl, "AccumulatePS", "NodeDesignerPostProcess.hlsl", "ps_5_0").ToArray();
			accumulatePS = device.CreatePixelShader(accumPsByteCode);

			accumulationBuffer = device.CreateBuffer(new BufferDescription
			{
				ByteWidth = 16, // one float4: AccumSettings
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

		private void BlitTextureToTarget(ID3D11ShaderResourceView source, ColorTextureTarget destination)
		{
			BindColorTarget(destination);
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(copyTexturePS);
			context.PSSetSampler(0, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			context.OMSetBlendState(GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, ColorWriteEnable.All));
			context.PSSetShaderResource(0, source);
			context.Draw(3, 0);
			context.PSSetShaderResource(0, null);
		}

		private void BlendSampleIntoAccumulation(ID3D11ShaderResourceView newSample, float weight)
		{
			// Upload blend weight
			var mapped = context.Map(accumulationBuffer, MapMode.WriteDiscard);
			unsafe
			{
				float* ptr = (float*)mapped.DataPointer;
				ptr[0] = weight;
				ptr[1] = 0;
				ptr[2] = 0;
				ptr[3] = 0;
			}

			context.Unmap(accumulationBuffer, 0);

			// Ping-pong: read from A + new sample, write blended result to B
			BindColorTarget(accumTargetB);
			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(accumulatePS);
			context.PSSetSampler(0, pointClampSampler);
			context.PSSetConstantBuffer(2, accumulationBuffer);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			context.OMSetBlendState(GetOrCreateBlendState(false, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.Zero, ColorWriteEnable.All));
			context.PSSetShaderResource(0, newSample);
			context.PSSetShaderResource(1, accumTargetA.ShaderResourceView);
			context.Draw(3, 0);
			context.PSSetShaderResource(0, null);
			context.PSSetShaderResource(1, null);

			// Swap A and B so A always holds the latest accumulated result
			(accumTargetA, accumTargetB) = (accumTargetB, accumTargetA);
		}

		private void BlitAccumulationToScreen()
		{
			context.OMSetRenderTargets(renderTargetView, depthStencilView);

			// The accumulation texture is backbuffer-sized with the 3D content in the
			// correct viewport region and transparent (0,0,0,0) everywhere else.
			// Blit with full-backbuffer viewport and alpha blending — transparent pixels
			// outside the 3D viewport contribute nothing to the backbuffer.
			int bbWidth = (int)(currentBackBuffer?.Description.Width ?? (uint)renderTargetHeight);
			int bbHeight = (int)(currentBackBuffer?.Description.Height ?? (uint)renderTargetHeight);
			context.RSSetViewport(new Viewport(bbWidth, bbHeight));

			context.IASetInputLayout(null);
			context.IASetPrimitiveTopology(PrimitiveTopology.TriangleList);
			context.VSSetShader(fullscreenVS);
			context.PSSetShader(copyTexturePS);
			context.PSSetSampler(0, pointClampSampler);
			context.OMSetDepthStencilState(GetOrCreateDepthStencilState(false, ComparisonFunction.Always, false));
			context.RSSetState(rasterizerNoCull);
			// Use One/OneMinusSrcAlpha (premultiplied alpha blending) because the
			// accumulated content has premultiplied RGB (from SrcAlpha blending when
			// the scene was rendered to the sample target). Using SrcAlpha here would
			// double-premultiply, making semi-transparent content like the bed too dark.
			context.OMSetBlendState(GetOrCreateBlendState(true, (int)BlendingFactorSrc.One, (int)BlendingFactorDest.OneMinusSrcAlpha, ColorWriteEnable.All));
			context.PSSetShaderResource(0, accumTargetA.ShaderResourceView);
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

		private void DisposeAccumulator()
		{
			accumTargetA?.Dispose();
			accumTargetA = null;
			accumTargetB?.Dispose();
			accumTargetB = null;
			accumulatePS?.Dispose();
			accumulatePS = null;
			accumulationBuffer?.Dispose();
			accumulationBuffer = null;
			sampleFrameTarget?.Dispose();
			sampleFrameTarget = null;
			sampleFrameDepthView?.Dispose();
			sampleFrameDepthView = null;
			sampleFrameDepthTexture?.Dispose();
			sampleFrameDepthTexture = null;
			accumulatedSampleCount = 0;
		}
	}
}
