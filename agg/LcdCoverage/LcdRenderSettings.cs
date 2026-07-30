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
using MatterHackers.Agg.Transform;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Process-wide LCD subpixel rendering state: the user toggle, the two filter style parameters, the
	/// hard effective-scale gate that overrides the toggle, and the generation counter caches watch to
	/// invalidate themselves.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference's <c>font_settings.rs</c> (<c>lcd_enabled</c>,
	/// <c>current_primary_weight</c>, <c>current_gamma</c>, <c>TYPOGRAPHY_EPOCH</c>). The reference keeps
	/// these in thread-locals because its UI is single threaded; agg-sharp paints from more than one thread
	/// over its lifetime (widget paint, image export, tests), so they are process-wide and lock-guarded
	/// here - a shared setting read once per fill is nowhere near the cost of the raster it gates.
	/// <para>
	/// Deliberately <b>not</b> where the toggle is persisted: the application layer owns the UI and the
	/// user-settings round trip (MatterCAD seeds <see cref="Enabled"/> at startup, next to the existing
	/// text-size read), and this is the value the render path reads.
	/// </para>
	/// </remarks>
	public static class LcdRenderSettings
	{
		/// <summary>
		/// Effective scale above which LCD subpixel rendering is refused <b>regardless of
		/// <see cref="Enabled"/></b>: past it the physical pixels are too small for their R/G/B stripes to
		/// resolve, so the subpixel geometry buys nothing and the mask pipeline is pure overhead.
		/// </summary>
		/// <remarks>
		/// The reference's threshold (<c>font_settings.rs:174</c>), including the sense of the comparison -
		/// see <see cref="EffectiveScaleAllowsLcd"/>.
		/// </remarks>
		public const double MaxEffectiveScale = 1.25;

		private static readonly object SyncRoot = new object();

		/// <summary>
		/// Default off, unlike the reference (which defaults LCD on at standard density). agg-sharp is a
		/// library with existing pixel-exact expectations - screenshot comparisons, image export, the test
		/// suite - so the new raster path has to be opted into rather than appear under everything.
		/// </summary>
		private static bool enabled;

		private static double primaryWeight = LcdFilter.DefaultPrimaryWeight;

		private static double gamma = LcdFilter.DefaultGamma;

		/// <summary>
		/// Starts at 1 so a consumer that stores 0 for "never checked" is guaranteed to see a mismatch on
		/// its first comparison - the reference's <c>TYPOGRAPHY_EPOCH</c> seeds the same way
		/// (<c>font_settings.rs:39</c>) for the same reason.
		/// </summary>
		private static long epoch = 1;

		/// <summary>
		/// Whether the LCD subpixel path is wanted at all. Off by default; the effective-scale gate can
		/// still refuse LCD when this is on (see <see cref="EffectiveScaleAllowsLcd"/>), but nothing turns
		/// it on when this is off.
		/// </summary>
		public static bool Enabled
		{
			get
			{
				lock (SyncRoot)
				{
					return enabled;
				}
			}

			set
			{
				lock (SyncRoot)
				{
					if (enabled == value)
					{
						return;
					}

					enabled = value;
				}

				OnSettingChanged();
			}
		}

		/// <summary>
		/// Center-tap weight of the 5-tap filter. At <see cref="LcdFilter.DefaultPrimaryWeight"/> the
		/// byte-exact integer kernel is used; any other value takes the parameterized float path, which
		/// rounds differently.
		/// </summary>
		public static double PrimaryWeight
		{
			get
			{
				lock (SyncRoot)
				{
					return primaryWeight;
				}
			}

			set
			{
				lock (SyncRoot)
				{
					if (SameValue(primaryWeight, value))
					{
						return;
					}

					primaryWeight = value;
				}

				OnSettingChanged();
			}
		}

		/// <summary>
		/// Curve applied after the filter sum. At <see cref="LcdFilter.DefaultGamma"/> none is applied.
		/// </summary>
		public static double Gamma
		{
			get
			{
				lock (SyncRoot)
				{
					return gamma;
				}
			}

			set
			{
				lock (SyncRoot)
				{
					if (SameValue(gamma, value))
					{
						return;
					}

					gamma = value;
				}

				OnSettingChanged();
			}
		}

		/// <summary>
		/// Monotonically increasing generation, bumped whenever a setting here actually changes value.
		/// </summary>
		/// <remarks>
		/// A write of the value a setting already holds is <b>not</b> a change and bumps nothing, which is a
		/// deliberate divergence from the reference: it bumps unconditionally, but its consumers only compare
		/// epochs, where ours also throws the mask cache away (see <see cref="OnSettingChanged"/>). Seeding a
		/// setting from persisted user settings, or re-applying it on a settings-page rebuild, would otherwise
		/// discard every cached mask and force every backbuffer to repaint for nothing.
		/// <para>
		/// This is the <c>TYPOGRAPHY_EPOCH</c> analogue. Anything holding rendered pixels that depend on
		/// these settings - a widget backbuffer, a glyph image cache, a GL texture - stores the epoch it was
		/// built under and rebuilds when it no longer matches, which is how a toggle reaches caches that
		/// have no other reason to be notified. The mask cache needs no such comparison because it is
		/// cleared outright (see <see cref="OnSettingChanged"/>).
		/// </para>
		/// </remarks>
		public static long Epoch => Interlocked.Read(ref epoch);

		/// <summary>
		/// The hard gate: false when <paramref name="effectiveScale"/> is past
		/// <see cref="MaxEffectiveScale"/>. Checked <b>before</b> and independently of
		/// <see cref="Enabled"/>, so it overrides an explicit user opt-in rather than being overridden by
		/// it.
		/// </summary>
		/// <param name="effectiveScale">Physical pixels per logical unit at the point of the draw - in
		/// agg-sharp that is the length of the current transform's x basis vector, see
		/// <see cref="EffectiveScaleOf"/>.</param>
		/// <remarks>
		/// The comparison mirrors the reference's <c>if effective_scale() &gt; 1.25 { return false }</c>
		/// exactly, both in strictness and in polarity:
		/// <list type="bullet">
		/// <item><description>the boundary itself is <b>allowed</b> - 1.25 passes, and only something
		/// larger fails, so a 125% display stays on the LCD path;</description></item>
		/// <item><description>it is written as a negated <c>&gt;</c> rather than <c>&lt;=</c> so a NaN
		/// scale is allowed through, as it is in Rust (where <c>NaN &gt; 1.25</c> is false). Nothing is
		/// painted either way - a NaN transform yields an empty bounding box, and the ordinary raster path
		/// draws nothing from NaN coordinates either - so matching the reference costs nothing and
		/// keeps the gate a single, checkable statement.</description></item>
		/// </list>
		/// </remarks>
		public static bool EffectiveScaleAllowsLcd(double effectiveScale)
		{
			return !(effectiveScale > MaxEffectiveScale);
		}

		/// <summary>
		/// Both gates in the order the reference applies them: the scale cap first, then the toggle.
		/// </summary>
		public static bool IsEnabledAtScale(double effectiveScale)
		{
			return EffectiveScaleAllowsLcd(effectiveScale) && Enabled;
		}

		/// <summary>
		/// Physical pixels per logical unit implied by <paramref name="transform"/>: the length of its x
		/// basis vector, <c>sqrt(sx^2 + shy^2)</c>.
		/// </summary>
		/// <remarks>
		/// The reference computes its <c>ctm_scale</c> the same way (<c>gfx_ctx.rs:654</c>) and derives the
		/// gate from a separately tracked <c>device_scale * ux_scale</c>; agg-sharp has no such global, so
		/// the transform is the single source of truth - which is also the more local answer, since a
		/// scaled sub-render is exactly the case the gate cares about. Only the x column is measured
		/// because only X is supersampled; a transform that scales Y differently does not change how many
		/// physical pixels a subpixel stripe spans.
		/// </remarks>
		public static double EffectiveScaleOf(Affine transform)
		{
			return Math.Sqrt((transform.sx * transform.sx) + (transform.shy * transform.shy));
		}

		/// <summary>
		/// Bumps <see cref="Epoch"/> and clears the mask cache. Called only from a setter that saw a genuine
		/// change - see <see cref="Epoch"/> for why a no-op write does neither.
		/// </summary>
		/// <remarks>
		/// The clear is not optional: every cached mask was filtered with the old
		/// <see cref="PrimaryWeight"/> / <see cref="Gamma"/>, and although both are part of
		/// <see cref="LcdMaskKey"/> - so stale entries could never be served - leaving them would pin masks
		/// nothing will ask for again and let a slider drag evict the live ones.
		/// </remarks>
		private static void OnSettingChanged()
		{
			Interlocked.Increment(ref epoch);
			LcdMaskCache.Clear();
		}

		/// <summary>
		/// Whether a setter is being handed the value it already holds. Compared by <b>bit pattern</b>, the
		/// same equality <see cref="LcdMaskKey"/> uses for these two doubles: a NaN written twice is one
		/// setting, and -0.0 over 0.0 is a change, so "no bump" here always means "no cached mask could have
		/// come out differently".
		/// </summary>
		private static bool SameValue(double current, double value)
		{
			return BitConverter.DoubleToInt64Bits(current) == BitConverter.DoubleToInt64Bits(value);
		}
	}
}
