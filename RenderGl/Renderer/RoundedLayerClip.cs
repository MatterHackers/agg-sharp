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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// Draws a <see cref="GpuRenderTarget"/>'s textured quad cut to a rounded rectangle - agg-gui's
	/// <c>set_layer_rounded_clip</c> on the layer composite.
	/// </summary>
	/// <remarks>
	/// Geometry rather than a per-pixel mask in a new shader: the composite already goes through the compat
	/// layer's textured, vertex-coloured pipeline, and the vertex colour scales the premultiplied texel. The
	/// rounded rectangle shrunk by half a pixel is filled at full opacity, and a ring out to the rectangle grown
	/// by half a pixel fades the vertex colour to 0, so across the edge the texel is scaled by
	/// clamp(0.5 - distance, 0, 1) - the same coverage <see cref="RoundedClipCoverage"/> gives the CPU
	/// backbuffer. Along straight whole-pixel edges that is exactly 1 inside and 0 outside, so only the corners
	/// change.
	/// </remarks>
	internal static class RoundedLayerClip
	{
		/// <summary>
		/// Emits the clipped quad. The caller has bound the texture, set the blend and the projection.
		/// </summary>
		/// <param name="gl">The destination's GL facade.</param>
		/// <param name="quad">Where the whole texture lands, in the destination's coordinates: Left/Bottom is
		/// where the texture's bottom-left lands, so under a flipped scale Right may be less than Left.</param>
		/// <param name="clip">The clip rectangle in the destination's coordinates, either way round.</param>
		/// <param name="radius">Corner radius in the destination's coordinates.</param>
		/// <param name="alpha">The composite's opacity as a premultiplied vertex colour.</param>
		public static void Draw(GL gl, RectangleDouble quad, RectangleDouble clip, double radius, byte alpha)
		{
			// The geometry wants ordinary rectangles; quad as given keeps mapping positions to texture
			// coordinates the right way round under a flip.
			var bounds = Normalized(quad);
			clip = Normalized(clip);
			if (clip.Width <= 0 || clip.Height <= 0 || bounds.Width <= 0 || bounds.Height <= 0)
			{
				return;
			}

			radius = RoundedClipCoverage.ClampRadius(clip, radius);

			// Enough segments that the polygon strays well under a tenth of a pixel from the arc.
			int segments = Math.Clamp((int)Math.Ceiling(radius), 2, 32);
			var inner = Outline(clip, radius, -0.5, segments);
			var outer = Outline(clip, radius, 0.5, segments);
			var center = clip.Center;

			gl.Begin(BeginMode.Triangles);
			for (int i = 0; i < inner.Count; i++)
			{
				int next = (i + 1) % inner.Count;
				Vertex(gl, quad, bounds, center, alpha);
				Vertex(gl, quad, bounds, inner[i], alpha);
				Vertex(gl, quad, bounds, inner[next], alpha);

				Vertex(gl, quad, bounds, inner[i], alpha);
				Vertex(gl, quad, bounds, outer[i], 0);
				Vertex(gl, quad, bounds, outer[next], 0);

				Vertex(gl, quad, bounds, inner[i], alpha);
				Vertex(gl, quad, bounds, outer[next], 0);
				Vertex(gl, quad, bounds, inner[next], alpha);
			}

			gl.End();
		}

		/// <summary>
		/// The rounded rectangle offset outward by <paramref name="offset"/> (inward when negative),
		/// counter-clockwise from the bottom-left corner, with the same number of points on every corner so an
		/// inner and an outer outline pair up point for point.
		/// </summary>
		private static List<Vector2> Outline(RectangleDouble clip, double radius, double offset, int segments)
		{
			// Offsetting a rounded rectangle keeps each corner's centre and adds the offset to its radius.
			double cornerRadius = Math.Max(0, radius + offset);
			double left = clip.Left + radius;
			double right = clip.Right - radius;
			double bottom = clip.Bottom + radius;
			double top = clip.Top - radius;
			if (radius + offset < 0)
			{
				// A radius smaller than the inward offset: the corner is a point on the shrunk rectangle.
				left = clip.Left - offset;
				right = clip.Right + offset;
				bottom = clip.Bottom - offset;
				top = clip.Top + offset;
			}

			var centers = new[] { new Vector2(left, bottom), new Vector2(right, bottom), new Vector2(right, top), new Vector2(left, top) };
			var points = new List<Vector2>(4 * (segments + 1));
			for (int corner = 0; corner < 4; corner++)
			{
				double start = Math.PI + (corner * Math.PI / 2);
				for (int step = 0; step <= segments; step++)
				{
					double angle = start + (step * Math.PI / 2 / segments);
					points.Add(centers[corner] + new Vector2(Math.Cos(angle) * cornerRadius, Math.Sin(angle) * cornerRadius));
				}
			}

			return points;
		}

		/// <summary>A vertex at <paramref name="position"/>, its texture coordinate found from where it sits in
		/// the quad. The texture's row 0 is the top of its picture, so v runs 1 at the bottom to 0 at the top.</summary>
		/// <remarks>
		/// Clamped to the quad's <paramref name="bounds"/>: a clip reaching past the texture (the half pixel it
		/// is grown by, see WidgetBackbuffer) would otherwise smear the clamped edge texels into a faint ring
		/// outside it. Clamping flattens the fringe onto the straight edges, where it adds nothing anyway.
		/// </remarks>
		private static void Vertex(GL gl, RectangleDouble quad, RectangleDouble bounds, Vector2 position, byte alpha)
		{
			double x = Math.Clamp(position.X, bounds.Left, bounds.Right);
			double y = Math.Clamp(position.Y, bounds.Bottom, bounds.Top);
			gl.Color4(alpha, alpha, alpha, alpha);
			gl.TexCoord2((x - quad.Left) / (quad.Right - quad.Left), 1 - ((y - quad.Bottom) / (quad.Top - quad.Bottom)));
			gl.Vertex2(x, y);
		}

		private static RectangleDouble Normalized(RectangleDouble r)
		{
			return new RectangleDouble(Math.Min(r.Left, r.Right), Math.Min(r.Bottom, r.Top), Math.Max(r.Left, r.Right), Math.Max(r.Bottom, r.Top));
		}
	}
}
