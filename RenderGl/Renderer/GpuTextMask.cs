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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// <see cref="Graphics2DGpu"/>'s text path: an identified fill (a text run) is drawn as its cached AGG coverage
	/// mask - see <see cref="TextCoverageMaskCache"/> - instead of the halo fill, which would embolden it by half a
	/// pixel.
	/// </summary>
	internal static class GpuTextMask
	{
		/// <summary>Scratch for <see cref="TryDraw"/>'s masks, so a text draw allocates no list.</summary>
		[ThreadStatic]
		private static List<TextCoverageMaskCache.CoverageMask> masks;

		/// <summary>
		/// Draws <paramref name="vertexSource"/> under <paramref name="currentTransform"/> as coverage masks, as much
		/// of it as <paramref name="clip"/> (in the target's pixels) lets through. False when the source names no
		/// identity or is placed out of range; the caller fills it.
		/// </summary>
		public static bool TryDraw(Graphics2DGpu graphics, IVertexSource vertexSource, Affine currentTransform, Color color, RectangleDouble clip)
		{
			if (!IdentifiedFillPlacement.TryUnwrap(vertexSource, currentTransform, out var identifiedSource, out Affine transform)
				|| !(identifiedSource.RenderIdentity is object identity)
				|| !(Math.Abs(transform.tx) < 1e7)
				|| !(Math.Abs(transform.ty) < 1e7))
			{
				return false;
			}

			// The whole-pixel placement becomes the composite origin and only the phase is rasterized, so a
			// run moved by whole pixels reuses its mask.
			int offsetX = (int)Math.Floor(transform.tx);
			int offsetY = (int)Math.Floor(transform.ty);
			Affine maskTransform = transform;
			maskTransform.tx = IdentifiedFillPlacement.NormalizeZeroPhase(transform.tx - offsetX);
			maskTransform.ty = IdentifiedFillPlacement.NormalizeZeroPhase(transform.ty - offsetY);

			// Only what the clip lets through is asked for: a run too big for one mask is tiled, and only its
			// visible tiles are rasterized.
			RectangleDouble visible = clip;
			visible.IntersectWithRectangle(new RectangleDouble(0, 0, graphics.Width, graphics.Height));
			visible.Offset(-offsetX, -offsetY);
			var runMasks = masks ??= new List<TextCoverageMaskCache.CoverageMask>();
			runMasks.Clear();
			TextCoverageMaskCache.GetMasks(identity, identifiedSource, maskTransform, visible, runMasks);

			GL gl = graphics.gl;
			graphics.PushOrthoProjection();
			gl.Disable(EnableCap.Lighting);
			gl.Enable(EnableCap.Texture2D);
			gl.Disable(EnableCap.DepthTest);
			gl.Enable(EnableCap.Blend);

			// Premultiplied mask times premultiplied color, blended as premultiplied source over.
			gl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);
			gl.Color4(color);
			foreach (var mask in runMasks)
			{
				gl.PushMatrix();
				gl.Translate(mask.OriginX + offsetX, mask.OriginY + offsetY, 0);
				ImageTexturePlugin.GetImageTexturePlugin(gl, mask.Image, false).DrawToGL();
				gl.PopMatrix();
			}

			runMasks.Clear();
			graphics.PopOrthoProjection();
			return true;
		}
	}
}
