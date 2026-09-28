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
using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s <see cref="IAlphaMaskGraphics"/>: the draws go into a transparent premultiplied
	/// layer the size of the surface, the mask is drawn over the layer blending Zero / SrcAlpha - which scales every
	/// premultiplied channel by the mask's coverage - and the layer is composited source-over.
	/// </summary>
	/// <remarks>
	/// No shader of its own: the multiply is fixed-function blending. The mask is uploaded as white with its coverage
	/// in alpha, sampled nearest so each mask pixel stays one block of the surface's pixels, as in software.
	/// </remarks>
	internal static class GpuAlphaMask
	{
		private static readonly ConditionalWeakTable<GL, Resources> ResourcesByGl = new ConditionalWeakTable<GL, Resources>();

		public static void Draw(Graphics2DGpu graphics, IImageByte mask, Action draw)
		{
			if (mask == null)
			{
				throw new ArgumentNullException(nameof(mask));
			}

			if (draw == null)
			{
				throw new ArgumentNullException(nameof(draw));
			}

			var gl = graphics.gl;
			if (!(gl?.GpuContext is GlCompatContext context) || context.Passes.ColorTarget == null)
			{
				throw new NotSupportedException("DrawMasked needs a GPU render target.");
			}

			var resources = ResourcesByGl.GetValue(gl, g => context.Own(new Resources(g)));
			if (resources.Layer.IsDrawing)
			{
				throw new NotSupportedException("DrawMasked cannot be nested inside another DrawMasked.");
			}

			ImageBuffer maskTexture = resources.Fill(mask);
			int scale = context.CoordinateScale;
			int width = graphics.Width;
			int height = graphics.Height;
			using (resources.Layer.BeginDraw(width * scale, height * scale))
			{
				// As GpuRetainedLayer.Begin: back to logical pixels, so the caller's graphics draws into the layer
				// exactly where it would have drawn on the surface.
				context.CoordinateScale = scale;
				gl.Viewport(0, 0, width, height);
				gl.Scissor(0, 0, width, height);

				draw();

				MultiplyByMask(graphics, maskTexture);
			}

			var saved = graphics.GetTransform();
			try
			{
				// The layer's texture is CoordinateScale times the surface; Composite sizes its quad by the texture.
				graphics.SetTransform(Affine.NewScaling(1.0 / scale));
				resources.Layer.Composite(graphics, 0, 0);
			}
			finally
			{
				graphics.SetTransform(saved);
			}
		}

		/// <summary>Scales the layer by the mask, placed through the current transform, and clears it outside the mask.</summary>
		private static void MultiplyByMask(Graphics2DGpu graphics, ImageBuffer maskTexture)
		{
			var gl = graphics.gl;
			var transform = graphics.GetTransform();
			double left = 0, bottom = 0, right = maskTexture.Width, top = maskTexture.Height;
			transform.Transform(ref left, ref bottom);
			transform.Transform(ref right, ref top);

			graphics.PushOrthoProjection();
			gl.Disable(EnableCap.Lighting);
			gl.Disable(EnableCap.DepthTest);
			// The draws may have clipped; the multiply covers the whole layer.
			gl.Scissor(0, 0, graphics.Width, graphics.Height);
			gl.Enable(EnableCap.Blend);

			// Outside the mask nothing shows: Zero / Zero clears what is there.
			gl.Disable(EnableCap.Texture2D);
			gl.BlendFunc(BlendingFactorSrc.Zero, BlendingFactorDest.Zero);
			gl.Color4(Color.White);
			double width = graphics.Width, height = graphics.Height;
			double maskLeft = Math.Min(left, right), maskRight = Math.Max(left, right);
			double maskBottom = Math.Min(bottom, top), maskTop = Math.Max(bottom, top);
			Quad(gl, 0, 0, width, maskBottom);
			Quad(gl, 0, maskTop, width, height);
			Quad(gl, 0, maskBottom, maskLeft, maskTop);
			Quad(gl, maskRight, maskBottom, width, maskTop);

			var plugin = ImageTexturePlugin.GetImageTexturePlugin(gl, maskTexture, false, textureMagFilterLinear: false);
			gl.Enable(EnableCap.Texture2D);
			gl.BlendFunc(BlendingFactorSrc.Zero, BlendingFactorDest.SrcAlpha);
			gl.Translate(left, bottom, 0);
			gl.Scale(transform.sx, transform.sy, 1);
			plugin.DrawToGL();

			graphics.PopOrthoProjection();
		}

		private static void Quad(GL gl, double left, double bottom, double right, double top)
		{
			if (right <= left || top <= bottom)
			{
				return;
			}

			gl.Begin(BeginMode.TriangleFan);
			gl.Vertex2(left, bottom);
			gl.Vertex2(left, top);
			gl.Vertex2(right, top);
			gl.Vertex2(right, bottom);
			gl.End();
		}

		/// <summary>One GL's layer and mask texture, reused from call to call.</summary>
		private sealed class Resources : IDisposable
		{
			private ImageBuffer maskTexture;

			public Resources(GL gl)
			{
				this.Layer = new GpuRenderTarget(gl);
			}

			public GpuRenderTarget Layer { get; }

			/// <summary>Releases the layer with its context (<see cref="GlCompatContext.Own"/>).</summary>
			public void Dispose() => this.Layer.Dispose();

			/// <summary>Copies the mask's coverage into the alpha of the reused white texture image.</summary>
			public ImageBuffer Fill(IImageByte mask)
			{
				if (this.maskTexture == null || this.maskTexture.Width != mask.Width || this.maskTexture.Height != mask.Height)
				{
					this.maskTexture = new ImageBuffer(mask.Width, mask.Height);
				}

				byte[] source = mask.GetBuffer();
				byte[] destination = this.maskTexture.GetBuffer();
				// An 8-bit mask's coverage is its byte, a 32-bit one's its alpha (BGRA's fourth byte).
				int channel = mask.BitDepth == 32 ? 3 : 0;
				int step = mask.GetBytesBetweenPixelsInclusive();
				for (int y = 0; y < mask.Height; y++)
				{
					int from = mask.GetBufferOffsetXY(0, y) + channel;
					int to = this.maskTexture.GetBufferOffsetXY(0, y);
					for (int x = 0; x < mask.Width; x++)
					{
						destination[to++] = 255;
						destination[to++] = 255;
						destination[to++] = 255;
						destination[to++] = source[from];
						from += step;
					}
				}

				this.maskTexture.MarkImageChanged();
				return this.maskTexture;
			}
		}
	}
}
