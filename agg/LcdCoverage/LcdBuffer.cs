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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Two-plane render target that can hold LCD subpixel coverage: 3 bytes per pixel of
	/// <b>premultiplied</b> per-channel color (<see cref="ColorPlane"/>) plus 3 bytes per pixel of
	/// <b>per-channel</b> alpha (<see cref="AlphaPlane"/>). Row 0 is the bottom row, matching
	/// <see cref="LcdMask"/> and <see cref="Image.ImageBuffer"/>.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference (<c>lcd_coverage.rs</c> <c>LcdBuffer</c>). The arithmetic is
	/// deliberately <see cref="float"/> throughout, in the reference's operation order, to match its
	/// <c>f32</c> results bit for bit.
	/// <para>
	/// <b>Why a second plane instead of one alpha per pixel.</b> LCD rendering produces a distinct coverage
	/// per R/G/B channel, and a single per-pixel alpha cannot represent that at glyph edges - which is
	/// exactly where all the information is. A widget backbuffer is drawn in software and then composited
	/// as a texture, so any per-channel coverage that is not carried through the backbuffer is lost before
	/// it reaches the screen (MatterCAD's <c>GuiWidget</c> allocates a single-alpha premultiplied BGRA
	/// <see cref="Image.ImageBuffer"/> for that job). Splitting alpha per channel gives each subpixel column
	/// its own Porter-Duff state, so paints accumulate independently through the same premultiplied
	/// source-over math a normal RGBA surface uses - three streams instead of one.
	/// </para>
	/// <para>
	/// <b>Transparent, not black.</b> A fresh buffer is zero in <i>both</i> planes, which reads as "no paint
	/// has landed here" rather than "intentionally black". That is what lets a cached buffer composite onto
	/// any destination without painting a black rectangle over the unpainted parts - the failure mode that
	/// killed the reference's first single-plane design.
	/// </para>
	/// </remarks>
	public partial class LcdBuffer
	{
		/// <summary>
		/// Largest plane this will allocate, per plane. Past it the requested size is treated as
		/// pathological and a 1x1 buffer is returned instead, mirroring the reference's clamp: the frame's
		/// text does not render, but the process keeps running and the offending widget's bounds get clamped
		/// by the next layout pass. Throwing would take the application down for what is always a caller
		/// bug in a paint path, and .NET would otherwise attempt the allocation and raise
		/// <see cref="OutOfMemoryException"/> from somewhere less diagnosable.
		/// </summary>
		private const long MaxPlaneBytes = 512L * 1024L * 1024L;

		private readonly byte[] color;

		private readonly byte[] alpha;

		/// <summary>
		/// Allocates a fully transparent buffer - both planes zero, so compositing it onto a destination
		/// leaves that destination untouched everywhere no paint has landed yet.
		/// </summary>
		public LcdBuffer(int width, int height)
		{
			if (width < 0 || height < 0)
			{
				throw new ArgumentException(
					$"LcdBuffer dimensions must not be negative, was {width} x {height}.",
					width < 0 ? nameof(width) : nameof(height));
			}

			long bytes = (long)width * height * 3L;
			if (bytes > MaxPlaneBytes)
			{
				width = 1;
				height = 1;
				bytes = 3L;
			}

			this.Width = width;
			this.Height = height;
			this.color = new byte[bytes];
			this.alpha = new byte[bytes];
		}

		public int Width { get; }

		public int Height { get; }

		/// <summary>
		/// Premultiplied per-channel color, length <c>Width * Height * 3</c>, stride <c>Width * 3</c>,
		/// channel order R, G, B. Each byte is <c>channel_color * channel_alpha</c>.
		/// </summary>
		/// <remarks>
		/// Handed out for direct read and write, like the reference's <c>color_plane</c> /
		/// <c>color_plane_mut</c> pair: the plane is the buffer's storage, not a copy of it. Callers that
		/// write here are responsible for keeping it consistent with <see cref="AlphaPlane"/> (premultiplied
		/// color must never exceed its channel's alpha), which every method on this class maintains.
		/// </remarks>
		public byte[] ColorPlane => this.color;

		/// <summary>
		/// Per-channel alpha, same layout as <see cref="ColorPlane"/>: the accumulated opacity of each
		/// subpixel column, 0 for untouched and 255 for fully opaque.
		/// </summary>
		public byte[] AlphaPlane => this.alpha;

		/// <summary>
		/// Index into either plane of the red byte of pixel (<paramref name="x"/>, <paramref name="y"/>);
		/// green and blue follow at +1 and +2. Y is measured from the bottom.
		/// </summary>
		public int PixelOffset(int x, int y)
		{
			return ((y * this.Width) + x) * 3;
		}

		/// <summary>
		/// Overwrites every pixel with a solid color. A flat clear has no per-subpixel differentiation, so
		/// all three alpha channels take <paramref name="fill"/>'s alpha and all three color channels take
		/// its premultiplied color.
		/// </summary>
		/// <remarks>
		/// This <b>replaces</b> rather than blends - the reference's <c>clear</c> is the "start from this
		/// background" primitive, not a paint.
		/// </remarks>
		public void Clear(Color fill)
		{
			// Color is byte-backed, so these are already inside 0..1 and the reference's clamps on the
			// components cannot bite.
			float a = fill.alpha / 255.0f;
			byte redByte = ToByte((fill.red / 255.0f) * a);
			byte greenByte = ToByte((fill.green / 255.0f) * a);
			byte blueByte = ToByte((fill.blue / 255.0f) * a);
			byte alphaByte = ToByte(a);

			for (int offset = 0; offset < this.color.Length; offset += 3)
			{
				this.color[offset] = redByte;
				this.color[offset + 1] = greenByte;
				this.color[offset + 2] = blueByte;
				this.alpha[offset] = alphaByte;
				this.alpha[offset + 1] = alphaByte;
				this.alpha[offset + 2] = alphaByte;
			}
		}

		/// <summary>
		/// Fills a vector path through the whole LCD pipeline: 3x horizontal raster, 5-tap filter, then a
		/// per-channel premultiplied source-over composite into this buffer.
		/// </summary>
		/// <param name="path">Any vertex source. Coordinates are in this buffer's pixel space (Y-up, origin
		/// bottom-left) after <paramref name="transform"/> is applied.</param>
		/// <param name="fill">The draw color; its alpha scales every channel's coverage.</param>
		/// <param name="transform">Path space to this buffer's pixel space, typically the caller's CTM.</param>
		/// <param name="clip">Optional clip rect in this buffer's pixel coordinates. Enforced by
		/// <see cref="BoundedMaskBuilder"/>, which both trims the mask's extent to the clip and applies it as
		/// a raster clip; the second, composite-time clip below is redundant for this path (see the comment
		/// there).</param>
		/// <param name="fillRule">Fill rule for the path.</param>
		/// <remarks>
		/// This is the general vector entry point, not a text feature: text is one caller among rect fills,
		/// strokes and widget paint, and every one of them gets the identical treatment because they all
		/// come through here.
		/// <para>
		/// The mask is sized to the transformed path's bbox rather than to the whole buffer
		/// (<see cref="BoundedMaskBuilder"/>), which makes a small fill cost O(bbox) instead of O(buffer)
		/// while producing byte-identical output - see that class for the translation-invariance argument.
		/// </para>
		/// <para>
		/// The composite origin is whole pixels because <see cref="BoundedMaskBuilder"/> reports whole
		/// pixels; that is a requirement, not a convenience. Sub-pixel placement of a finished mask would
		/// smear each channel's phase into its neighbors and destroy the subpixel geometry.
		/// </para>
		/// </remarks>
		public void FillPath(
			IVertexSource path,
			Color fill,
			Affine transform,
			RectangleDouble? clip = null,
			filling_rule_e fillRule = filling_rule_e.fill_non_zero)
		{
			if (path == null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			bool built = BoundedMaskBuilder.TryBuild(
				this.Width,
				this.Height,
				path,
				transform,
				out LcdMask mask,
				out int originX,
				out int originY,
				clip,
				fillRule);
			if (!built)
			{
				return;
			}

			// Passing the clip on to the composite is provably a no-op from here: BoundedMaskBuilder sizes the
			// mask to [floor(left), ceil(right)) x [floor(bottom), ceil(top)) - the same rect, with the same
			// rounding, that ToPixelClip produces - so no mask pixel can land outside it. Kept because the
			// reference passes it too (lcd_coverage.rs fill_path), and deleting it would fail no test; the
			// clip parameter earns its keep only for callers that reach CompositeMask directly with a mask
			// they built themselves.
			this.CompositeMask(mask, fill, originX, originY, ToPixelClip(clip));
		}

		/// <summary>
		/// Composites an <see cref="LcdMask"/> into this buffer with per-channel <b>premultiplied</b>
		/// Porter-Duff source-over. Per channel <c>c</c>:
		/// <code>
		/// ea_c      = fill.alpha * mask_c / 255
		/// color_c  := fill_c * ea_c + color_c * (1 - ea_c)
		/// alpha_c  := ea_c          + alpha_c * (1 - ea_c)
		/// </code>
		/// </summary>
		/// <param name="mask">Per-channel coverage. Empty masks are a no-op.</param>
		/// <param name="fill">The draw color. Its alpha scales every channel's coverage, so a half-opacity
		/// color paints a half-faded blit rather than a solid one.</param>
		/// <param name="destX">Buffer x of the mask's left column.</param>
		/// <param name="destY">Buffer y of the mask's <b>bottom</b> row - both are Y-up, so mask row
		/// <c>my</c> lands on buffer row <c>destY + my</c> with no flip.</param>
		/// <param name="clip">Optional clip in this buffer's integer pixel coordinates,
		/// <see cref="RectangleInt.Left"/>/<see cref="RectangleInt.Bottom"/> inclusive and
		/// <see cref="RectangleInt.Right"/>/<see cref="RectangleInt.Top"/> exclusive (the reference's
		/// half-open <c>(x1, y1, x2, y2)</c>). Used by widgets painting inside a clipping parent.</param>
		/// <remarks>
		/// The color plane accumulates <c>fill_c * ea_c</c>, the premultiplied contribution of this paint,
		/// and the alpha plane runs the same Porter-Duff composite independently per channel. A mask
		/// hanging off the buffer edge composites only the overlapping part; that is clipping, not an error.
		/// </remarks>
		public void CompositeMask(LcdMask mask, Color fill, int destX, int destY, RectangleInt? clip = null)
		{
			if (mask == null)
			{
				throw new ArgumentNullException(nameof(mask));
			}

			if (mask.Width == 0 || mask.Height == 0)
			{
				return;
			}

			if (!this.TryResolveClip(clip, out int clipLeft, out int clipBottom, out int clipRight, out int clipTop))
			{
				return;
			}

			float sourceAlpha = fill.alpha / 255.0f;
			float sourceRed = fill.red / 255.0f;
			float sourceGreen = fill.green / 255.0f;
			float sourceBlue = fill.blue / 255.0f;

			for (int maskY = 0; maskY < mask.Height; maskY++)
			{
				int y = destY + maskY;
				if (y < clipBottom || y >= clipTop)
				{
					continue;
				}

				for (int maskX = 0; maskX < mask.Width; maskX++)
				{
					int x = destX + maskX;
					if (x < clipLeft || x >= clipRight)
					{
						continue;
					}

					int maskOffset = mask.PixelOffset(maskX, maskY);
					float effectiveRed = sourceAlpha * (mask.Data[maskOffset] / 255.0f);
					float effectiveGreen = sourceAlpha * (mask.Data[maskOffset + 1] / 255.0f);
					float effectiveBlue = sourceAlpha * (mask.Data[maskOffset + 2] / 255.0f);
					if (effectiveRed == 0.0f && effectiveGreen == 0.0f && effectiveBlue == 0.0f)
					{
						continue;
					}

					int offset = this.PixelOffset(x, y);
					float bufferRed = this.color[offset] / 255.0f;
					float bufferGreen = this.color[offset + 1] / 255.0f;
					float bufferBlue = this.color[offset + 2] / 255.0f;
					float bufferAlphaRed = this.alpha[offset] / 255.0f;
					float bufferAlphaGreen = this.alpha[offset + 1] / 255.0f;
					float bufferAlphaBlue = this.alpha[offset + 2] / 255.0f;

					this.color[offset] = ToByte((sourceRed * effectiveRed) + (bufferRed * (1.0f - effectiveRed)));
					this.color[offset + 1] = ToByte((sourceGreen * effectiveGreen) + (bufferGreen * (1.0f - effectiveGreen)));
					this.color[offset + 2] = ToByte((sourceBlue * effectiveBlue) + (bufferBlue * (1.0f - effectiveBlue)));
					this.alpha[offset] = ToByte(effectiveRed + (bufferAlphaRed * (1.0f - effectiveRed)));
					this.alpha[offset + 1] = ToByte(effectiveGreen + (bufferAlphaGreen * (1.0f - effectiveGreen)));
					this.alpha[offset + 2] = ToByte(effectiveBlue + (bufferAlphaBlue * (1.0f - effectiveBlue)));
				}
			}
		}

		/// <summary>
		/// Composites <paramref name="source"/> onto this buffer at (<paramref name="destX"/>,
		/// <paramref name="destY"/>) with per-channel premultiplied source-over - the buffer-level analogue
		/// of <see cref="CompositeMask"/>. Per channel <c>c</c>:
		/// <code>
		/// color_c  := source.color_c + color_c * (1 - source.alpha_c)
		/// alpha_c  := source.alpha_c + alpha_c * (1 - source.alpha_c)
		/// </code>
		/// </summary>
		/// <param name="source">The buffer to paint. Pixels whose three alphas are all zero do not touch
		/// this buffer at all, which is what lets a popped layer leave unpainted areas alone with no seed
		/// trick.</param>
		/// <param name="destX">Buffer x of the source's left column.</param>
		/// <param name="destY">Buffer y of the source's bottom row; both buffers are Y-up, so no flip.</param>
		/// <param name="clip">Optional clip in this buffer's integer pixel coordinates, half-open on the
		/// right and top edges - see <see cref="CompositeMask"/>.</param>
		/// <remarks>
		/// No modulation of the source color is needed here: it is already premultiplied, so
		/// <c>source + dest * (1 - source_alpha)</c> is the plain Porter-Duff expression. This preserves
		/// full LCD chroma through a nested-buffer round trip, which is the whole reason a nested
		/// LCD-coverage widget can flush into an LCD-coverage parent without collapsing.
		/// </remarks>
		public void CompositeBuffer(LcdBuffer source, int destX, int destY, RectangleInt? clip = null)
		{
			if (source == null)
			{
				throw new ArgumentNullException(nameof(source));
			}

			if (source.Width == 0 || source.Height == 0)
			{
				return;
			}

			if (!this.TryResolveClip(clip, out int clipLeft, out int clipBottom, out int clipRight, out int clipTop))
			{
				return;
			}

			for (int sourceY = 0; sourceY < source.Height; sourceY++)
			{
				int y = destY + sourceY;
				if (y < clipBottom || y >= clipTop)
				{
					continue;
				}

				for (int sourceX = 0; sourceX < source.Width; sourceX++)
				{
					int x = destX + sourceX;
					if (x < clipLeft || x >= clipRight)
					{
						continue;
					}

					int sourceOffset = source.PixelOffset(sourceX, sourceY);
					float sourceAlphaRed = source.alpha[sourceOffset] / 255.0f;
					float sourceAlphaGreen = source.alpha[sourceOffset + 1] / 255.0f;
					float sourceAlphaBlue = source.alpha[sourceOffset + 2] / 255.0f;
					if (sourceAlphaRed == 0.0f && sourceAlphaGreen == 0.0f && sourceAlphaBlue == 0.0f)
					{
						continue;
					}

					float sourceRed = source.color[sourceOffset] / 255.0f;
					float sourceGreen = source.color[sourceOffset + 1] / 255.0f;
					float sourceBlue = source.color[sourceOffset + 2] / 255.0f;

					int offset = this.PixelOffset(x, y);
					float bufferRed = this.color[offset] / 255.0f;
					float bufferGreen = this.color[offset + 1] / 255.0f;
					float bufferBlue = this.color[offset + 2] / 255.0f;
					float bufferAlphaRed = this.alpha[offset] / 255.0f;
					float bufferAlphaGreen = this.alpha[offset + 1] / 255.0f;
					float bufferAlphaBlue = this.alpha[offset + 2] / 255.0f;

					this.color[offset] = ToByte(sourceRed + (bufferRed * (1.0f - sourceAlphaRed)));
					this.color[offset + 1] = ToByte(sourceGreen + (bufferGreen * (1.0f - sourceAlphaGreen)));
					this.color[offset + 2] = ToByte(sourceBlue + (bufferBlue * (1.0f - sourceAlphaBlue)));
					this.alpha[offset] = ToByte(sourceAlphaRed + (bufferAlphaRed * (1.0f - sourceAlphaRed)));
					this.alpha[offset + 1] = ToByte(sourceAlphaGreen + (bufferAlphaGreen * (1.0f - sourceAlphaGreen)));
					this.alpha[offset + 2] = ToByte(sourceAlphaBlue + (bufferAlphaBlue * (1.0f - sourceAlphaBlue)));
				}
			}
		}

		/// <summary>
		/// Intersects <paramref name="clip"/> with this buffer's pixel rect; false when nothing is left to
		/// write to. The out parameters are always a half-open rect (right and top exclusive).
		/// </summary>
		private bool TryResolveClip(RectangleInt? clip, out int left, out int bottom, out int right, out int top)
		{
			return TryResolveClip(clip, this.Width, this.Height, out left, out bottom, out right, out top);
		}

		/// <summary>
		/// Intersects <paramref name="clip"/> with a <paramref name="width"/> x <paramref name="height"/>
		/// pixel rect; false when the intersection is empty.
		/// </summary>
		private static bool TryResolveClip(
			RectangleInt? clip, int width, int height, out int left, out int bottom, out int right, out int top)
		{
			if (clip == null)
			{
				left = 0;
				bottom = 0;
				right = width;
				top = height;
			}
			else
			{
				RectangleInt rect = clip.Value;
				left = Math.Max(rect.Left, 0);
				bottom = Math.Max(rect.Bottom, 0);
				right = Math.Min(rect.Right, width);
				top = Math.Min(rect.Top, height);
			}

			return left < right && bottom < top;
		}

		/// <summary>
		/// A continuous clip rect as the integer pixel rect the composite loops enforce: floor on the left
		/// and bottom, ceil on the (exclusive) right and top, so any pixel the rect touches at all is kept.
		/// Matches the reference's <c>rect_to_pixel_clip</c> and the bounds
		/// <see cref="BoundedMaskBuilder"/> already applies to the mask, so the two clips cannot disagree
		/// about which pixels are in.
		/// </summary>
		/// <remarks>
		/// Public because every caller that has a clip in continuous coordinates - a
		/// <see cref="MatterHackers.Agg.Graphics2D"/>'s clipping rect, say - needs this exact rounding to
		/// reach <see cref="CompositeMask"/> or <see cref="CompositeOnto"/>, and a second implementation of it
		/// would be a second chance to disagree about a boundary pixel.
		/// </remarks>
		public static RectangleInt? ToPixelClip(RectangleDouble? clip)
		{
			if (clip == null)
			{
				return null;
			}

			RectangleDouble rect = clip.Value;

			// Saturating, so an "effectively unbounded" clip passed as double.MaxValue widens the box
			// instead of inverting it (see SaturatingMath).
			return new RectangleInt(
				SaturatingMath.Floor(rect.Left),
				SaturatingMath.Floor(rect.Bottom),
				SaturatingMath.Ceiling(rect.Right),
				SaturatingMath.Ceiling(rect.Top));
		}

		/// <summary>
		/// Quantizes a 0..1 channel to a byte, rounding half up then clamping - the reference's
		/// <c>(value * 255.0 + 0.5).clamp(0.0, 255.0) as u8</c>, in the same order (the truncating cast
		/// after the clamp is what makes 255.0 land on 255 rather than wrapping).
		/// </summary>
		private static byte ToByte(float value)
		{
			return (byte)Math.Clamp((value * 255.0f) + 0.5f, 0.0f, 255.0f);
		}
	}
}
