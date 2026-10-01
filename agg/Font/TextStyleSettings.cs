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
using System.Threading;

namespace MatterHackers.Agg.Font
{
	/// <summary>
	/// Process-wide typography style: a text size multiplier, horizontal glyph width, letter spacing, synthetic
	/// emboldening and synthetic slant. Every default is the identity, and at the identity the text path takes
	/// exactly the code it took before these existed, so nothing changes until an application sets one.
	/// </summary>
	/// <remarks>
	/// Ported from agg-gui's <c>font_settings.rs</c> (<c>set_font_size_scale</c>, <c>set_width</c>,
	/// <c>set_interval</c>, <c>set_faux_weight</c>, <c>set_faux_italic</c>), including its clamp ranges. As with
	/// <see cref="LcdCoverage.LcdRenderSettings"/>, persisting the values is the application's job.
	/// <para>
	/// Width, Interval, Faux Weight and Faux Italic are applied by a <see cref="StyledTypeFace"/> that opts in
	/// (<see cref="StyledTypeFace.ApplyTextStyleSettings"/>) to every glyph it makes, and Interval to every advance
	/// it measures - Width scales outlines only, as in agg-gui. A face that does not opt in ignores them all. <see cref="SizeScale"/> is applied only by
	/// <c>TextWidget</c>, to text widgets built (or re-sized) after the change - a UI text size, not a geometry
	/// size, so text drawn directly at a point size is not scaled by it.
	/// </para>
	/// </remarks>
	public static class TextStyleSettings
	{
		private static readonly object SyncRoot = new object();

		private static double sizeScale = 1;

		private static double width = 1;

		private static double interval;

		private static double fauxWeight;

		private static double fauxItalic;

		/// <summary>
		/// Written by every setter, read on every glyph and advance: true while all four glyph-shaping settings
		/// are at their defaults, so the common case pays one volatile read.
		/// </summary>
		private static volatile bool glyphStyleIsIdentity = true;

		/// <summary>Starts at 1 so a consumer storing 0 for "never checked" sees a mismatch, as <c>LcdRenderSettings.Epoch</c> does.</summary>
		private static long epoch = 1;

		/// <summary>Multiplier on every text widget's point size. 1 = unchanged; clamped to 0.5 - 3.</summary>
		public static double SizeScale
		{
			get { lock (SyncRoot) { return sizeScale; } }
			set => Set(ref sizeScale, Math.Clamp(value, .5, 3));
		}

		/// <summary>Horizontal scale of every glyph outline (advances are unchanged, as in agg-gui). 1 = native widths; clamped to 0.75 - 1.25.</summary>
		public static double Width
		{
			get { lock (SyncRoot) { return width; } }
			set => Set(ref width, Math.Clamp(value, .75, 1.25));
		}

		/// <summary>Extra letter spacing added to every advance, as a fraction of the em. 0 = unchanged; clamped to -0.2 - 0.2.</summary>
		public static double Interval
		{
			get { lock (SyncRoot) { return interval; } }
			set => Set(ref interval, Math.Clamp(value, -.2, .2));
		}

		/// <summary>
		/// Synthetic weight by offsetting each outline's contour: positive is heavier, negative lighter, 0 unchanged.
		/// Clamped to -1 - 1; within 0.05 of zero nothing is applied (agg-gui's dead zone).
		/// </summary>
		public static double FauxWeight
		{
			get { lock (SyncRoot) { return fauxWeight; } }
			set => Set(ref fauxWeight, Math.Clamp(value, -1, 1));
		}

		/// <summary>Synthetic slant: each outline is sheared by a third of this. 0 = upright; clamped to -1 - 1.</summary>
		public static double FauxItalic
		{
			get { lock (SyncRoot) { return fauxItalic; } }
			set => Set(ref fauxItalic, Math.Clamp(value, -1, 1));
		}

		/// <summary>
		/// Bumped whenever a setting here actually changes value. Anything holding pixels or layout built from
		/// text stores the epoch it was built under and rebuilds on a mismatch.
		/// </summary>
		public static long Epoch => Interlocked.Read(ref epoch);

		/// <summary>Whether Width, Interval, Faux Weight and Faux Italic are all at their defaults.</summary>
		internal static bool GlyphStyleIsIdentity => glyphStyleIsIdentity;

		/// <summary>Contour offset, in pixels, that Faux Weight asks of a glyph at <paramref name="emSizeInPixels"/>; 0 in the dead zone.</summary>
		/// <remarks>
		/// agg-gui's <c>-faux_weight * size / 15</c>: the sign is flipped because a negative contour width grows
		/// an outline, and the divisor is its reference demo's slider-to-pixels conversion.
		/// </remarks>
		internal static double FauxWeightInPixels(double emSizeInPixels)
		{
			double weight = FauxWeight;
			return Math.Abs(weight) < .05 ? 0 : -weight * emSizeInPixels / 15;
		}

		/// <summary>
		/// Bumps <see cref="Epoch"/> for a text setting that lives elsewhere but, like these, changes the pixels of
		/// text already rastered (<see cref="TypeFacePrinter.SnapBaselinesToWholePixels"/>), so the caches that
		/// watch this epoch rebuild. Glyph images are left alone: each is one glyph, which a baseline move does
		/// not reshape.
		/// </summary>
		internal static void NotifyTextLayoutChanged()
		{
			Interlocked.Increment(ref epoch);
		}

		/// <summary>Puts every setting back to its default.</summary>
		public static void Reset()
		{
			SizeScale = 1;
			Width = 1;
			Interval = 0;
			FauxWeight = 0;
			FauxItalic = 0;
		}

		private static void Set(ref double field, double value)
		{
			lock (SyncRoot)
			{
				if (BitConverter.DoubleToInt64Bits(field) == BitConverter.DoubleToInt64Bits(value))
				{
					return;
				}

				field = value;
				glyphStyleIsIdentity = width == 1 && interval == 0 && fauxWeight == 0 && fauxItalic == 0;
			}

			Interlocked.Increment(ref epoch);
			// Cached glyph images were drawn under the old style and their key does not name it.
			StyledTypeFaceImageCache.Clear();
		}
	}
}
