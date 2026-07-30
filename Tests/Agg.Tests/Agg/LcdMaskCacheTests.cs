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

using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="LcdMaskCache"/>: the LRU of rasterized coverage masks, ported from the agg-gui Rust
	/// reference (<c>lcd_coverage\mask.rs</c>). Two things have to hold - a hit must not re-rasterize, and
	/// <b>every</b> parameter that changes the bytes must miss - and only the second one fails loudly on its
	/// own, so the hit tests assert against <see cref="LcdMaskCache.BuildCount"/> rather than against equal
	/// bytes (a rebuild would produce equal bytes too).
	/// </summary>
	/// <remarks>
	/// The cache and <see cref="LcdRenderSettings"/> are process-wide, so every test here is
	/// <c>[NotInParallel]</c> and starts from a cleared cache. A constraint key would only serialize these
	/// against each other, and any other test in the assembly that renders through the LCD path could still
	/// race the shared state.
	/// </remarks>
	public class LcdMaskCacheTests
	{
		/// <summary>
		/// A hit hands back the <b>same instance</b>, which is what makes the mask worth caching at all: the
		/// reference shares an <c>Arc</c> so a GL backend can key a texture cache on its identity, and the
		/// C# port shares the object for the same reason.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task HitReturnsTheSameMaskWithoutRasterizingAgain()
		{
			LcdMaskCache.Clear();
			VertexStorage path = Rectangle(4.3, 3.2, 19.7, 10.6);
			object identity = "HitReturnsTheSameMask";

			long beforeFirst = LcdMaskCache.BuildCount;
			bool built = LcdMaskCache.TryGetBoundedMask(
				identity, 32, 16, path, Affine.NewIdentity(), out LcdMask first, out int firstX, out int firstY);
			await Assert.That(built).IsTrue();
			await Assert.That(LcdMaskCache.BuildCount - beforeFirst).IsEqualTo(1L)
				.Because("the first call has to rasterize");

			long beforeSecond = LcdMaskCache.BuildCount;
			bool hit = LcdMaskCache.TryGetBoundedMask(
				identity, 32, 16, path, Affine.NewIdentity(), out LcdMask second, out int secondX, out int secondY);
			await Assert.That(hit).IsTrue();
			await Assert.That(LcdMaskCache.BuildCount - beforeSecond).IsEqualTo(0L)
				.Because("the second call must not rasterize");
			await Assert.That(ReferenceEquals(first, second)).IsTrue()
				.Because("a hit shares the cached instance, which is what a texture cache keys on");

			// The origin is cached with the bytes: a mask is only meaningful at the position it was built for.
			await Assert.That(secondX).IsEqualTo(firstX);
			await Assert.That(secondY).IsEqualTo(firstY);
			await Assert.That(LcdMaskCache.Count).IsEqualTo(1);
		}

		/// <summary>
		/// A null path identity bypasses the cache entirely - the honest answer for throwaway geometry, and
		/// what the reference does for every general vector fill (only its text path, which has a stable
		/// identity, is cached).
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task NullIdentityAlwaysRasterizesAndStoresNothing()
		{
			LcdMaskCache.Clear();
			VertexStorage path = Rectangle(4.3, 3.2, 19.7, 10.6);

			long before = LcdMaskCache.BuildCount;
			LcdMaskCache.TryGetBoundedMask(null, 32, 16, path, Affine.NewIdentity(), out LcdMask first, out _, out _);
			LcdMaskCache.TryGetBoundedMask(null, 32, 16, path, Affine.NewIdentity(), out LcdMask second, out _, out _);

			await Assert.That(LcdMaskCache.BuildCount - before).IsEqualTo(2L);
			await Assert.That(ReferenceEquals(first, second)).IsFalse();
			await Assert.That(LcdMaskCache.Count).IsEqualTo(0);
		}

		/// <summary>
		/// Every component of <see cref="LcdMaskKey"/> has to be load bearing: changing any one of them
		/// rasterizes again, and repeating the changed call then hits. A parameter missing from the key is the
		/// bug this catches - it would silently serve a mask rendered under the previous value, which in the
		/// reference is what a style-slider drag looked like.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task EveryKeyComponentMisses()
		{
			LcdMaskCache.Clear();
			VertexStorage path = Rectangle(4.3, 3.2, 19.7, 10.6);
			VertexStorage otherPath = Rectangle(4.3, 3.2, 18.1, 10.6);
			var noClip = default(RectangleDouble?);
			const double pw = LcdFilter.DefaultPrimaryWeight;
			const double gm = LcdFilter.DefaultGamma;

			// Baseline: rasterized once, then cached.
			await AssertMiss("baseline", "a", 32, 16, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_non_zero, pw, gm, false);

			await AssertMiss("path identity", "b", 32, 16, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_non_zero, pw, gm, false);

			// A different vertex source under the same identity is deliberately NOT a miss - identity is the
			// caller's promise, which is why it has to be a value that only equals itself when the geometry
			// matches. Assert the promise's consequence so the contract is visible here.
			long beforeLie = LcdMaskCache.BuildCount;
			LcdMaskCache.TryGetBoundedMask("a", 32, 16, otherPath, Affine.NewIdentity(), out _, out _, out _);
			await Assert.That(LcdMaskCache.BuildCount - beforeLie).IsEqualTo(0L)
				.Because("identity is the caller's promise: reusing one for different geometry serves the old mask");

			// Transform: the linear part, a sub-pixel translation (a different phase in the mask) and a whole
			// pixel translation (identical bytes, but a different origin, so a different entry).
			await AssertMiss("transform scale", "a", 32, 16, path, Affine.NewScaling(1.1), noClip, filling_rule_e.fill_non_zero, pw, gm, false);
			await AssertMiss("sub-pixel translation", "a", 32, 16, path, Affine.NewTranslation(0.25, 0), noClip, filling_rule_e.fill_non_zero, pw, gm, false);
			await AssertMiss("whole-pixel translation", "a", 32, 16, path, Affine.NewTranslation(5, 0), noClip, filling_rule_e.fill_non_zero, pw, gm, false);

			// Destination size and clip both trim the mask, so both change its bytes.
			await AssertMiss("buffer width", "a", 31, 16, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_non_zero, pw, gm, false);
			await AssertMiss("buffer height", "a", 32, 15, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_non_zero, pw, gm, false);
			await AssertMiss("clip added", "a", 32, 16, path, Affine.NewIdentity(), new RectangleDouble(6, 4, 18, 10), filling_rule_e.fill_non_zero, pw, gm, false);
			await AssertMiss("clip changed", "a", 32, 16, path, Affine.NewIdentity(), new RectangleDouble(6, 4, 17, 10), filling_rule_e.fill_non_zero, pw, gm, false);

			await AssertMiss("fill rule", "a", 32, 16, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_even_odd, pw, gm, false);

			// The style parameters and the gray flag - the reference's LcdMaskKey carries all three.
			await AssertMiss("primary weight", "a", 32, 16, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_non_zero, 0.4, gm, false);
			await AssertMiss("gamma", "a", 32, 16, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_non_zero, pw, 1.2, false);
			await AssertMiss("gray flag", "a", 32, 16, path, Affine.NewIdentity(), noClip, filling_rule_e.fill_non_zero, pw, gm, true);
		}

		/// <summary>
		/// The cap is enforced by dropping the least recently used entry, and a hit counts as a use: the
		/// entry touched just before the overflow survives, the untouched one next to it does not. Evicting
		/// by insertion order instead would throw away exactly the mask that is being repainted every frame.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task EvictionAtCapacityDropsTheLeastRecentlyUsed()
		{
			LcdMaskCache.Clear();

			// A small path keeps 1025 rasters cheap - the mask is about 20x12 pixels.
			VertexStorage path = Rectangle(2.3, 2.2, 13.7, 9.6);
			Affine identityTransform = Affine.NewIdentity();

			for (int i = 0; i < LcdMaskCache.Capacity; i++)
			{
				LcdMaskCache.TryGetBoundedMask(i, 16, 12, path, identityTransform, out _, out _, out _);
			}

			await Assert.That(LcdMaskCache.Count).IsEqualTo(LcdMaskCache.Capacity);

			// Touch the oldest entry so it becomes the newest.
			long beforeTouch = LcdMaskCache.BuildCount;
			LcdMaskCache.TryGetBoundedMask(0, 16, 12, path, identityTransform, out _, out _, out _);
			await Assert.That(LcdMaskCache.BuildCount - beforeTouch).IsEqualTo(0L)
				.Because("entry 0 is still cached at this point");

			// One past the cap: something has to go, and it must be entry 1 (now the least recently used).
			LcdMaskCache.TryGetBoundedMask(LcdMaskCache.Capacity, 16, 12, path, identityTransform, out _, out _, out _);
			await Assert.That(LcdMaskCache.Count).IsEqualTo(LcdMaskCache.Capacity)
				.Because("the cache never grows past its cap");

			long beforeEvicted = LcdMaskCache.BuildCount;
			LcdMaskCache.TryGetBoundedMask(1, 16, 12, path, identityTransform, out _, out _, out _);
			await Assert.That(LcdMaskCache.BuildCount - beforeEvicted).IsEqualTo(1L)
				.Because("entry 1 was the least recently used and must have been evicted");

			long beforeKept = LcdMaskCache.BuildCount;
			LcdMaskCache.TryGetBoundedMask(0, 16, 12, path, identityTransform, out _, out _, out _);
			await Assert.That(LcdMaskCache.BuildCount - beforeKept).IsEqualTo(0L)
				.Because("entry 0 was promoted by the hit and must have survived");
		}

		/// <summary>
		/// Changing any LCD setting bumps <see cref="LcdRenderSettings.Epoch"/> - the reference's
		/// <c>TYPOGRAPHY_EPOCH</c>, which backbuffered widgets compare against to self-invalidate - and
		/// clears the mask cache outright.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task SettingChangeBumpsTheEpochAndClearsTheCache()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			double wasPrimaryWeight = LcdRenderSettings.PrimaryWeight;
			double wasGamma = LcdRenderSettings.Gamma;
			try
			{
				VertexStorage path = Rectangle(4.3, 3.2, 19.7, 10.6);

				foreach (string setting in new[] { "Enabled", "PrimaryWeight", "Gamma" })
				{
					LcdMaskCache.Clear();
					LcdMaskCache.TryGetBoundedMask(setting, 32, 16, path, Affine.NewIdentity(), out _, out _, out _);
					await Assert.That(LcdMaskCache.Count).IsEqualTo(1);

					long epochBefore = LcdRenderSettings.Epoch;
					switch (setting)
					{
						case "Enabled":
							LcdRenderSettings.Enabled = !LcdRenderSettings.Enabled;
							break;

						case "PrimaryWeight":
							LcdRenderSettings.PrimaryWeight = 0.4;
							break;

						default:
							LcdRenderSettings.Gamma = 1.2;
							break;
					}

					await Assert.That(LcdRenderSettings.Epoch > epochBefore).IsTrue()
						.Because($"{setting} must bump the epoch so pixel caches can self-invalidate");
					await Assert.That(LcdMaskCache.Count).IsEqualTo(0)
						.Because($"{setting} must clear the mask cache");
				}
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
				LcdRenderSettings.PrimaryWeight = wasPrimaryWeight;
				LcdRenderSettings.Gamma = wasGamma;
			}
		}

		/// <summary>
		/// Writing a setting the value it already holds is not a change: no epoch bump, no cache clear.
		/// Seeding the toggle from persisted user settings at startup, or re-applying it when a settings page
		/// is rebuilt, is exactly that write - and it must not discard every cached mask and force every
		/// backbuffer that compares epochs to repaint.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task NoOpSettingWriteChangesNothing()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			double wasPrimaryWeight = LcdRenderSettings.PrimaryWeight;
			double wasGamma = LcdRenderSettings.Gamma;
			try
			{
				LcdMaskCache.Clear();
				VertexStorage path = Rectangle(4.3, 3.2, 19.7, 10.6);
				LcdMaskCache.TryGetBoundedMask("NoOpSettingWrite", 32, 16, path, Affine.NewIdentity(), out _, out _, out _);
				await Assert.That(LcdMaskCache.Count).IsEqualTo(1);

				long epochBefore = LcdRenderSettings.Epoch;
				LcdRenderSettings.Enabled = wasEnabled;
				LcdRenderSettings.PrimaryWeight = wasPrimaryWeight;
				LcdRenderSettings.Gamma = wasGamma;

				await Assert.That(LcdRenderSettings.Epoch).IsEqualTo(epochBefore)
					.Because("a write that changes no value must not bump the epoch");
				await Assert.That(LcdMaskCache.Count).IsEqualTo(1)
					.Because("a write that changes no value must not clear the mask cache");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
				LcdRenderSettings.PrimaryWeight = wasPrimaryWeight;
				LcdRenderSettings.Gamma = wasGamma;
			}
		}

		/// <summary>
		/// Asserts that the given call rasterizes (it is a miss), and that repeating it does not (it was
		/// stored under its own key). Both halves matter: the first proves the varied parameter is in the
		/// key, the second proves it is in the key <i>consistently</i>, rather than the entry being
		/// unreachable.
		/// </summary>
		private static async Task AssertMiss(
			string what,
			object identity,
			int bufferWidth,
			int bufferHeight,
			IVertexSource path,
			Affine transform,
			RectangleDouble? clip,
			filling_rule_e fillRule,
			double primaryWeight,
			double gamma,
			bool gray)
		{
			long beforeMiss = LcdMaskCache.BuildCount;
			bool built = LcdMaskCache.TryGetBoundedMask(
				identity, bufferWidth, bufferHeight, path, transform, out LcdMask mask, out _, out _, clip, fillRule, primaryWeight, gamma, gray);
			await Assert.That(built).IsTrue().Because($"{what} must produce a mask");
			await Assert.That(LcdMaskCache.BuildCount - beforeMiss).IsEqualTo(1L)
				.Because($"changing {what} must miss the cache");

			long beforeHit = LcdMaskCache.BuildCount;
			LcdMaskCache.TryGetBoundedMask(
				identity, bufferWidth, bufferHeight, path, transform, out LcdMask again, out _, out _, clip, fillRule, primaryWeight, gamma, gray);
			await Assert.That(LcdMaskCache.BuildCount - beforeHit).IsEqualTo(0L)
				.Because($"{what} must then be cached under its own key");
			await Assert.That(ReferenceEquals(mask, again)).IsTrue();
		}

		/// <summary>An axis-aligned rectangle path, corners as given.</summary>
		private static VertexStorage Rectangle(double left, double bottom, double right, double top)
		{
			var path = new VertexStorage();
			path.MoveTo(left, bottom);
			path.LineTo(right, bottom);
			path.LineTo(right, top);
			path.LineTo(left, top);
			path.ClosePolygon();

			return path;
		}
	}
}
