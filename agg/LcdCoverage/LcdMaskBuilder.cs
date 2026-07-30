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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Accumulator for an <see cref="LcdMask"/>: rasterizes any number of vector paths into a
	/// 3x-horizontally-supersampled gray coverage buffer, then collapses that buffer into the packed
	/// 3-byte-per-pixel mask (<see cref="FinalizeMask"/> for LCD subpixel coverage,
	/// <see cref="FinalizeGray"/> for the chroma-free fallback).
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference (<c>lcd_coverage\mask.rs</c> <c>LcdMaskBuilder</c> and
	/// <c>rasterize_paths_into_gray</c>).
	/// <para>
	/// This is deliberately a general vector-level seam, not a font feature: text is one caller among
	/// rect fills, strokes and widget paint. Nothing here knows about the destination color - the
	/// output is pure coverage, which is what makes a mask cacheable and background independent.
	/// </para>
	/// <para>
	/// The rasterization stage is the stock agg-sharp pipeline (8 bit-per-pixel
	/// <see cref="ImageBuffer"/> + <see cref="ScanlineRasterizer"/> + <see cref="scanline_unpacked_8"/> +
	/// <see cref="ScanlineRenderer"/>), rendered with an opaque white so each output byte carries that
	/// subpixel's coverage. No rasterizer or scanline code is modified or duplicated; the one substitution
	/// is <see cref="BlenderGrayExact"/> in place of <see cref="blender_gray"/>, for the rounding reason
	/// documented on <c>CoverageColor</c>.
	/// </para>
	/// </remarks>
	public class LcdMaskBuilder
	{
		/// <summary>
		/// How far outside the gray buffer, in pixels, the rasterizer's overflow clip box sits. Everything
		/// this pipeline actually rasterizes is orders of magnitude closer than that - masks are sized from a
		/// path bbox clipped to the destination - so the box never touches real geometry. It is a fixed
		/// distance rather than "as far out as possible" because a segment clipped to the box still costs one
		/// cell per row and column it crosses, and <c>RasterizerCellsAa</c> silently drops coverage past
		/// <c>cell_block_limit</c> (4,194,304 cells).
		/// </summary>
		private const int OverflowClipPadPixels = 4096;

		/// <summary>
		/// Hard cap on the span the overflow clip box may permit on either axis; the pad is clamped to it for
		/// a gray buffer big enough to need the clamp. <c>RasterizerCellsAa.line</c> subdivides any segment
		/// whose <c>dx</c> reaches its <c>dx_limit</c> of <c>16384 &lt;&lt; 8</c> subunits, and every piece of
		/// a subdivided segment still costs one cell per row and column it crosses - so the cap bounds the
		/// cells (and the array growth behind them) a single clipped edge can cost. 16382 keeps every span
		/// the box permits under the limit, with two pixels to spare for the +/-1 subunit the clipper's own
		/// <c>iround</c> can add to a clipped endpoint.
		/// </summary>
		private const int MaxClipSpanPixels = 16382;

		/// <summary>
		/// Rendered with full coverage: <see cref="BlenderGrayExact"/> converts white to the gray value
		/// 255, so a fully covered subpixel byte lands on 255 and a partly covered one on AGG's coverage
		/// estimate for that subpixel - exactly the input <see cref="LcdFilter"/> expects.
		/// </summary>
		/// <remarks>
		/// Measured byte-exactness against the Rust reference, and the reason this pipeline does not use
		/// the stock <see cref="blender_gray"/>: fully covered subpixels take <c>CopyPixels</c> and land on
		/// an exact 255 either way, but <b>partly</b> covered ones came out one less than the reference,
		/// because <see cref="blender_gray"/> interpolates with a plain <c>&gt;&gt; 8</c> where AGG's (and
		/// the Rust port's) <c>lerp</c> uses the rounding-correct <c>((t &gt;&gt; 8) + t) &gt;&gt; 8</c> -
		/// a half-covered subpixel read 127 instead of 128. <see cref="BlenderGrayExact"/> replicates AGG's
		/// lerp bit for bit, so over the zeroed buffer each subpixel byte now equals AGG's coverage
		/// estimate exactly.
		/// </remarks>
		private static readonly Color CoverageColor = new Color(255, 255, 255);

		/// <summary>
		/// Null for a degenerate (zero width or height) mask, where there is nothing to rasterize into.
		/// </summary>
		private readonly ImageBuffer grayImage;

		/// <summary>
		/// Rendering goes through the clipping proxy rather than <see cref="grayImage"/> directly: it is
		/// what bounds writes to the buffer (paths routinely extend past the mask) and what applies the
		/// caller's clip rect, mirroring the Rust reference's <c>RendererBase</c> + <c>clip_box_i</c>.
		/// </summary>
		private readonly ImageClippingProxy clippedGray;

		private readonly ScanlineRasterizer rasterizer;

		private readonly scanline_unpacked_8 scanlineCache;

		private readonly ScanlineRenderer scanlineRenderer;

		/// <summary>
		/// Allocates a zeroed builder for a <paramref name="maskWidth"/> x <paramref name="maskHeight"/>
		/// mask; the internal gray buffer is <c>(maskWidth * 3) x maskHeight</c> bytes.
		/// </summary>
		/// <param name="maskWidth">Output mask width in whole pixels.</param>
		/// <param name="maskHeight">Output mask height in rows.</param>
		/// <param name="clip">Optional clip rectangle in <b>mask pixel</b> coordinates (post-transform).
		/// Coverage outside it never reaches the gray buffer, so it cannot reach the mask either.</param>
		/// <param name="fillRule">Fill rule for every path added to this builder.</param>
		public LcdMaskBuilder(
			int maskWidth,
			int maskHeight,
			RectangleDouble? clip = null,
			filling_rule_e fillRule = filling_rule_e.fill_non_zero)
			: this(maskWidth, maskHeight, clip, fillRule, applyOverflowClipBox: true)
		{
		}

		/// <param name="applyOverflowClipBox">Always true in production; see
		/// <see cref="CreateWithUnclippedRasterizer"/> and <see cref="ApplyOverflowClipBox"/>.</param>
		private LcdMaskBuilder(
			int maskWidth,
			int maskHeight,
			RectangleDouble? clip,
			filling_rule_e fillRule,
			bool applyOverflowClipBox)
		{
			if (maskWidth < 0 || maskHeight < 0)
			{
				throw new ArgumentException("LcdMaskBuilder dimensions must not be negative.");
			}

			this.MaskWidth = maskWidth;
			this.MaskHeight = maskHeight;
			this.GrayWidth = maskWidth * 3;
			this.GrayHeight = maskHeight;
			this.Clip = clip;
			this.FillRule = fillRule;

			if (this.GrayWidth == 0 || this.GrayHeight == 0)
			{
				// Nothing can be rasterized into a zero-area buffer; AddPath becomes a no-op and the
				// finalizers return the (empty) mask.
				return;
			}

			// A freshly allocated 8bpp ImageBuffer has stride == width, buffer offset 0 and Y-up rows,
			// which is exactly the layout LcdFilter documents for the gray buffer.
			this.grayImage = new ImageBuffer(this.GrayWidth, this.GrayHeight, 8, new BlenderGrayExact(1));
			this.clippedGray = new ImageClippingProxy(this.grayImage);
			if (clip != null)
			{
				this.ApplyClip(clip.Value);
			}

			this.scanlineCache = new scanline_unpacked_8();
			this.scanlineRenderer = new ScanlineRenderer();
			this.rasterizer = new ScanlineRasterizer();
			this.rasterizer.filling_rule(fillRule);

			if (applyOverflowClipBox)
			{
				this.ApplyOverflowClipBox();
			}
		}

		/// <summary>Output mask width in whole pixels.</summary>
		public int MaskWidth { get; }

		/// <summary>Output mask height in rows.</summary>
		public int MaskHeight { get; }

		/// <summary>Subpixels per gray row - always <c>MaskWidth * 3</c>.</summary>
		public int GrayWidth { get; }

		/// <summary>Gray buffer rows - always <c>MaskHeight</c>.</summary>
		public int GrayHeight { get; }

		/// <summary>Clip rectangle in mask pixel coordinates, or null when unclipped.</summary>
		public RectangleDouble? Clip { get; }

		/// <summary>Fill rule applied to every path added to this builder.</summary>
		public filling_rule_e FillRule { get; }

		/// <summary>
		/// The accumulated 3x-wide gray coverage, stride <see cref="GrayWidth"/>, rows Y-up (row 0 is the
		/// bottom). Exposed to the tests so the raster stage can be pinned independently of the filter;
		/// production callers want <see cref="FinalizeMask"/> / <see cref="FinalizeGray"/>.
		/// </summary>
		internal byte[] GrayBuffer => this.grayImage?.GetBuffer() ?? Array.Empty<byte>();

		/// <summary>
		/// A builder whose rasterizer has <b>no</b> vector clip box at all - the Rust reference's behaviour,
		/// and the ground truth the overflow clip box has to reproduce byte for byte. Exposed only so the
		/// tests can make that comparison against the real production pipeline rather than a copy of it;
		/// anything with coordinates big enough to overflow AGG's 24.8 cell math will die here.
		/// </summary>
		internal static LcdMaskBuilder CreateWithUnclippedRasterizer(int maskWidth, int maskHeight)
		{
			return new LcdMaskBuilder(
				maskWidth, maskHeight, null, filling_rule_e.fill_non_zero, applyOverflowClipBox: false);
		}

		/// <summary>
		/// Rasterizes <paramref name="path"/> under <paramref name="transform"/> into the gray buffer,
		/// accumulating over whatever earlier paths already wrote (the rasterizer is reset per path, the
		/// buffer is not - which is what lets overlapping glyphs share one non-zero fill).
		/// </summary>
		/// <param name="transform">Path space to <b>mask pixel</b> space. Only X is supersampled, so the
		/// builder scales sx, shx and tx by 3 and leaves shy, sy and ty alone.</param>
		/// <param name="path">Any vertex source; curves are flattened first, exactly as the Rust
		/// reference runs its paths through <c>ConvCurve</c> before <c>ConvTransform</c>.</param>
		public void AddPath(Affine transform, IVertexSource path)
		{
			if (path == null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			if (this.rasterizer == null)
			{
				return;
			}

			Affine supersampledX = transform;
			supersampledX.sx *= 3.0;
			supersampledX.shx *= 3.0;
			supersampledX.tx *= 3.0;

			// shy, sy and ty are untouched - the gray buffer is 3x wide but the same height, so scaling
			// the Y row of the matrix would squash the path vertically.
			var deviceSpacePath = new VertexSourceApplyTransform(new FlattenCurves(path), supersampledX);

			this.rasterizer.reset();
			this.rasterizer.add_path(deviceSpacePath);
			this.scanlineRenderer.RenderSolid(this.clippedGray, this.rasterizer, this.scanlineCache, CoverageColor);
		}

		/// <summary>
		/// Collapses the gray buffer with the 5-tap LCD filter and returns the packed subpixel coverage
		/// mask. Named <c>FinalizeMask</c> rather than the reference's <c>finalize</c> because
		/// <c>Finalize</c> is reserved by <see cref="object"/>.
		/// </summary>
		/// <param name="primaryWeight">Center tap weight; the default takes the byte-exact integer
		/// filter.</param>
		/// <param name="gamma">Curve applied after the filter sum; the default applies none.</param>
		public LcdMask FinalizeMask(
			double primaryWeight = LcdFilter.DefaultPrimaryWeight,
			double gamma = LcdFilter.DefaultGamma)
		{
			if (this.MaskWidth == 0 || this.MaskHeight == 0)
			{
				return new LcdMask(this.MaskWidth, this.MaskHeight);
			}

			return LcdFilter.Apply5TapFilter(
				this.GrayBuffer,
				this.GrayWidth,
				this.MaskWidth,
				this.MaskHeight,
				primaryWeight,
				gamma);
		}

		/// <summary>
		/// Collapses the gray buffer by box-averaging each triple of subpixels into one value replicated
		/// across R/G/B: the same packed layout and the same composite path as
		/// <see cref="FinalizeMask"/>, but with no chroma. This is the fallback wherever LCD subpixel
		/// geometry is not valid (inside a transparent compositing layer, or above the effective-scale
		/// gate).
		/// </summary>
		public LcdMask FinalizeGray()
		{
			if (this.MaskWidth == 0 || this.MaskHeight == 0)
			{
				return new LcdMask(this.MaskWidth, this.MaskHeight);
			}

			return LcdFilter.ApplyGrayCollapse(this.GrayBuffer, this.GrayWidth, this.MaskWidth, this.MaskHeight);
		}

		/// <summary>
		/// Gives the rasterizer a vector clip box that exists <b>purely</b> as int-overflow protection, far
		/// enough outside the gray buffer that no plausible geometry ever touches it. Writes to the buffer
		/// are bounded by <see cref="clippedGray"/>, not by this box - same as the Rust reference, which
		/// clips only at the renderer (<c>rasterize_paths_into_gray</c>, mask.rs:634-644).
		/// </summary>
		/// <remarks>
		/// A box is needed at all because a bare <see cref="ScanlineRasterizer"/> does no vector clipping,
		/// and then an extreme coordinate (say 1e12, which saturates to <see cref="int.MaxValue"/> in AGG's
		/// 24.8 fixed point) reaches <c>RasterizerCellsAa.line</c>, whose <c>dx</c> is an <see cref="int"/>:
		/// the overflow makes its subdivision recurse without converging and the raster dies. The reference
		/// needs no such box because its cells subdivision computes <c>dx</c> in <c>i64</c>; matching a crash
		/// is not a goal.
		/// <para>
		/// The box sits thousands of pixels clear of the buffer (<see cref="OverflowClipPadPixels"/>) rather
		/// than on its edge, because clipping real geometry is <b>not</b> free: AGG's Liang-Barsky clipper
		/// computes the crossing coordinate through
		/// <c>mul_div</c> -> <c>iround</c> (+/-1/512 px), and <c>line</c> re-seeds its integer y interpolation
		/// from that rounded endpoint, so a segment crossing the box at a non-axis-aligned angle shifts by up
		/// to 1/256 px along its whole length - a +/-1 gray byte, at interior columns as much as at the
		/// crossing. Geometry that stays inside is untouched: <see cref="VectorClipper.line_to"/> sees
		/// clipping flags of 0 at both ends and hands the segment to <c>ras.line</c> verbatim.
		/// </para>
		/// <para>
		/// Worst case arithmetic the widest box this can produce has to survive, in the rasterizer's 24.8
		/// subunits (span and coordinate are both bounded by <see cref="MaxClipSpanPixels"/> = 16382 px):
		/// <list type="bullet">
		/// <item>coordinate: 16382 * 256 = 4,193,792 - comfortably an <see cref="int"/>.</item>
		/// <item><c>dx = x2 - x1</c> and <c>dy = y2 - y1</c>: 4,193,792, plus the +/-1 subunit a clipped
		/// endpoint can round by, so 4,193,794 - under <c>dx_limit</c> (4,194,304), so a clipped edge is
		/// traversed once rather than split into pieces.</item>
		/// <item><c>p = (256 - fy1) * dx</c>, the largest product in line(): 256 * 4,193,794 = 1,073,611,264.
		/// line() computes it in 64 bits, so this is headroom rather than a constraint.
		/// <c>render_hline</c>'s <c>(256 - fx1) * (y2 - y1)</c> cannot exceed 65,536 because both terms are
		/// sub-pixel.</item>
		/// <item>cells: one per row and column a clipped segment crosses, so at most ~2 * (spanX + spanY) =
		/// ~65k of the 4,194,304 <c>cell_block_limit</c> allows.</item>
		/// </list>
		/// A gray buffer bigger than <see cref="MaxClipSpanPixels"/> leaves no room for a pad and falls back
		/// to a buffer-tight box, reintroducing the rounding above at its edges - but such a buffer exceeds
		/// <c>dx_limit</c> by itself, with or without a clip box, so its edges get subdivided regardless.
		/// </para>
		/// </remarks>
		private void ApplyOverflowClipBox()
		{
			int padX = Math.Max(0, Math.Min(OverflowClipPadPixels, (MaxClipSpanPixels - this.GrayWidth) / 2));
			int padY = Math.Max(0, Math.Min(OverflowClipPadPixels, (MaxClipSpanPixels - this.GrayHeight) / 2));

			this.rasterizer.SetVectorClipBox(-padX, -padY, this.GrayWidth + padX, this.GrayHeight + padY);
		}

		/// <summary>
		/// Translates a clip rect given in mask pixels into the gray buffer's subpixel grid. The gray
		/// buffer is 3x wide in X, so the X bounds are multiplied by 3; the clip box is inclusive on both
		/// ends, hence the <c>-1</c> after the ceil on the right and top edges.
		/// </summary>
		/// <remarks>
		/// The bounds go through <see cref="SaturatingMath"/> so an "effectively unbounded" clip rect
		/// (<see cref="double.MaxValue"/> as a sentinel) still behaves as unbounded rather than inverting
		/// the box.
		/// </remarks>
		private void ApplyClip(RectangleDouble clip)
		{
			int x1 = SaturatingMath.MultiplyBy3(SaturatingMath.Floor(clip.Left));
			int y1 = SaturatingMath.Floor(clip.Bottom);
			int x2 = SaturatingMath.MultiplyBy3(SaturatingMath.Ceiling(clip.Right)) - 1;
			int y2 = SaturatingMath.Ceiling(clip.Top) - 1;

			// Returns false (and leaves an empty clip box) when the rect misses the buffer entirely, so
			// no coverage is written at all - the same outcome as the reference's clip_box_i.
			this.clippedGray.SetClippingBox(x1, y1, x2, y2);
		}
	}
}
