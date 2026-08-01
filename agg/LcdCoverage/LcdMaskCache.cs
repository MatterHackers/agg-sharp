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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using filling_rule_e = MatterHackers.Agg.Util.filling_rule_e;

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// Everything that determines the bytes of a cached <see cref="LcdMask"/>, so that two draws with equal
	/// keys can share one raster and two draws with any difference cannot.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference's <c>LcdMaskKey</c> (<c>lcd_coverage\mask.rs:59-79</c>).
	/// <para>
	/// <b>Path identity is supplied by the caller</b>, matching the reference: it keys on the text string
	/// plus the font's pointer identity, never on the shaped outlines, because hashing the geometry costs
	/// what rasterizing it would have saved. agg-sharp has no stable hash for an
	/// <see cref="IVertexSource"/> either - a <c>VertexStorage</c> is mutable, and the wrappers around it
	/// (<c>Stroke</c>, <c>FlattenCurves</c>, <c>VertexSourceApplyTransform</c>) are rebuilt per draw - so
	/// the same rule applies here: whoever draws knows what it is drawing and says so.
	/// <paramref name="pathIdentity"/> must therefore be a value that is <see cref="object.Equals(object)"/>
	/// -equal exactly when the geometry is identical, and stable across frames to be worth caching at all.
	/// A tuple of the semantic inputs (the string and typeface behind a glyph run, an icon's name and size)
	/// is the intended shape; a mutable path object is not, because mutating it in place would silently
	/// serve the old raster.
	/// </para>
	/// <para>
	/// The doubles are compared and hashed by their <b>bit patterns</b>
	/// (<see cref="BitConverter.DoubleToInt64Bits"/>), the reference's <c>to_bits</c> trick: it gives value
	/// equality for free without an epsilon that would let a slider drag keep serving the previous style's
	/// masks.
	/// </para>
	/// </remarks>
	public readonly struct LcdMaskKey : IEquatable<LcdMaskKey>
	{
		private readonly object pathIdentity;
		private readonly long sxBits;
		private readonly long shyBits;
		private readonly long shxBits;
		private readonly long syBits;
		private readonly long txBits;
		private readonly long tyBits;
		private readonly int bufferWidth;
		private readonly int bufferHeight;
		private readonly bool hasClip;
		private readonly long clipLeftBits;
		private readonly long clipBottomBits;
		private readonly long clipRightBits;
		private readonly long clipTopBits;
		private readonly filling_rule_e fillRule;
		private readonly long primaryWeightBits;
		private readonly long gammaBits;
		private readonly bool gray;

		/// <param name="pathIdentity">Caller-supplied identity of the geometry - see the type remarks for
		/// what makes a good one. Must not be null; a caller with nothing stable to say should skip the
		/// cache instead (<see cref="LcdMaskCache.TryGetBoundedMask"/> does exactly that when handed
		/// null).</param>
		/// <param name="transform">Path space to destination pixel space. Included in full, translation and
		/// all: a bounded mask's dimensions and the sub-pixel phase of the path inside it both depend on
		/// where the path landed, and a cache entry carries its origin along with its bytes.</param>
		/// <param name="bufferWidth">Destination width - it can trim the mask, so it is part of the
		/// key.</param>
		/// <param name="bufferHeight">Destination height, for the same reason.</param>
		/// <param name="clip">Clip rect in destination pixels, which likewise trims the mask.</param>
		/// <param name="fillRule">Fill rule the path is rasterized with.</param>
		/// <param name="primaryWeight">Filter center-tap weight.</param>
		/// <param name="gamma">Post-filter curve.</param>
		/// <param name="gray">True for the chroma-free sibling. Same raster, different collapse, so the two
		/// must not collide.</param>
		public LcdMaskKey(
			object pathIdentity,
			Affine transform,
			int bufferWidth,
			int bufferHeight,
			RectangleDouble? clip,
			filling_rule_e fillRule,
			double primaryWeight,
			double gamma,
			bool gray)
		{
			if (pathIdentity == null)
			{
				throw new ArgumentNullException(nameof(pathIdentity));
			}

			this.pathIdentity = pathIdentity;
			this.sxBits = BitConverter.DoubleToInt64Bits(transform.sx);
			this.shyBits = BitConverter.DoubleToInt64Bits(transform.shy);
			this.shxBits = BitConverter.DoubleToInt64Bits(transform.shx);
			this.syBits = BitConverter.DoubleToInt64Bits(transform.sy);
			this.txBits = BitConverter.DoubleToInt64Bits(transform.tx);
			this.tyBits = BitConverter.DoubleToInt64Bits(transform.ty);
			this.bufferWidth = bufferWidth;
			this.bufferHeight = bufferHeight;
			this.hasClip = clip != null;
			RectangleDouble clipRect = clip ?? default;
			this.clipLeftBits = BitConverter.DoubleToInt64Bits(clipRect.Left);
			this.clipBottomBits = BitConverter.DoubleToInt64Bits(clipRect.Bottom);
			this.clipRightBits = BitConverter.DoubleToInt64Bits(clipRect.Right);
			this.clipTopBits = BitConverter.DoubleToInt64Bits(clipRect.Top);
			this.fillRule = fillRule;
			this.primaryWeightBits = BitConverter.DoubleToInt64Bits(primaryWeight);
			this.gammaBits = BitConverter.DoubleToInt64Bits(gamma);
			this.gray = gray;
		}

		public bool Equals(LcdMaskKey other)
		{
			// object.Equals rather than a direct call: the constructor rejects a null identity, but
			// default(LcdMaskKey) skips it, and a struct nobody can compare is a trap not a safeguard.
			return object.Equals(this.pathIdentity, other.pathIdentity)
				&& this.sxBits == other.sxBits
				&& this.shyBits == other.shyBits
				&& this.shxBits == other.shxBits
				&& this.syBits == other.syBits
				&& this.txBits == other.txBits
				&& this.tyBits == other.tyBits
				&& this.bufferWidth == other.bufferWidth
				&& this.bufferHeight == other.bufferHeight
				&& this.hasClip == other.hasClip
				&& this.clipLeftBits == other.clipLeftBits
				&& this.clipBottomBits == other.clipBottomBits
				&& this.clipRightBits == other.clipRightBits
				&& this.clipTopBits == other.clipTopBits
				&& this.fillRule == other.fillRule
				&& this.primaryWeightBits == other.primaryWeightBits
				&& this.gammaBits == other.gammaBits
				&& this.gray == other.gray;
		}

		public override bool Equals(object obj)
		{
			return obj is LcdMaskKey other && this.Equals(other);
		}

		public override int GetHashCode()
		{
			var hash = default(HashCode);
			hash.Add(this.pathIdentity);
			hash.Add(this.sxBits);
			hash.Add(this.shyBits);
			hash.Add(this.shxBits);
			hash.Add(this.syBits);
			hash.Add(this.txBits);
			hash.Add(this.tyBits);
			hash.Add(this.bufferWidth);
			hash.Add(this.bufferHeight);
			hash.Add(this.hasClip);
			hash.Add(this.clipLeftBits);
			hash.Add(this.clipBottomBits);
			hash.Add(this.clipRightBits);
			hash.Add(this.clipTopBits);
			hash.Add((int)this.fillRule);
			hash.Add(this.primaryWeightBits);
			hash.Add(this.gammaBits);
			hash.Add(this.gray);

			return hash.ToHashCode();
		}
	}

	/// <summary>
	/// Least-recently-used cache of rasterized <see cref="LcdMask"/>es and the origins they composite at, so
	/// repainting unchanged content re-runs the composite but not the raster or the filter.
	/// </summary>
	/// <remarks>
	/// Ported from the agg-gui Rust reference's mask cache (<c>lcd_coverage\mask.rs:89-96</c> and the
	/// get/insert/evict machinery around it), including the 1024-entry cap.
	/// <para>
	/// <b>Two caps, because entries are not the same size.</b> The reference counts entries only; agg-sharp
	/// also counts bytes, because an untrimmed mask (the cached text path) is bounded by
	/// <see cref="BoundedMaskBuilder.MaxUnclippedMaskExtentInPixels"/> rather than by any destination, so one
	/// entry can be far larger than a glyph run's few kilobytes. A scrolling console
	/// (<c>OutputScroll</c> paints every distinct line through <c>TypeFacePrinter</c>) fills entries with
	/// genuinely different content, so the entry cap alone would let the cache grow without a useful bound -
	/// see <see cref="MaxCachedBytes"/>.
	/// </para>
	/// <para>
	/// <b>Masks are handed out shared and must be treated as read-only.</b> A hit returns the very same
	/// <see cref="LcdMask"/> instance a previous caller got - the reference shares an <c>Arc</c> for the
	/// same reason, and a GL backend can key a texture cache on that instance's identity. Writing into a
	/// returned mask's <see cref="LcdMask.Data"/> would corrupt every later hit. Nothing in the composite
	/// paths does; they only read.
	/// </para>
	/// <para>
	/// Thread safety: one lock around the whole structure. Painting is a UI-thread activity, so contention
	/// is not the interesting case - correctness when an export or a test paints from another thread is.
	/// </para>
	/// </remarks>
	public static class LcdMaskCache
	{
		/// <summary>
		/// Entry cap before the least recently used one is dropped. The reference's <c>MASK_CACHE_MAX</c>.
		/// </summary>
		public const int Capacity = 1024;

		/// <summary>
		/// Total bytes of cached mask data before the least recently used entries are dropped, whatever the
		/// entry count says.
		/// </summary>
		/// <remarks>
		/// Sized the way <c>StyledTypeFaceImageCache.MaxCachedImages</c> is: from what the entries actually
		/// cost. A mask is <c>Width * Height * 3</c> bytes, so a UI-sized glyph run (~100 x 20 pixels) is
		/// ~6KB and the 1024-entry cap alone would bound that at ~6MB - but an untrimmed mask is only bounded
		/// by <see cref="BoundedMaskBuilder.MaxUnclippedMaskExtentInPixels"/>, which is 48MB for a single
		/// entry, so entry count says nothing about memory. 32MB holds several thousand ordinary text masks
		/// and refuses to hold a screenful of enormous ones.
		/// <para>
		/// <b>This counts mask bytes only, and on a GPU window a cached mask costs about five times them.</b>
		/// <c>Graphics2DGpu.CompositeLcdMask</c> hangs three BGRA pass images off each mask it draws
		/// (<c>3 * Width * Height * 4</c> bytes, ~4.4x the mask), plus up to three GL textures per context that
		/// has drawn it. None of that is counted here, and none of it can be: it lives in another assembly and
		/// is keyed weakly on the mask precisely so that eviction here releases it. So a full cache in front of
		/// a GPU rendered window is ~160MB of managed memory rather than 32MB, and some VRAM on top - which is
		/// the number to revisit if this cap is ever raised, not the 32MB.
		/// </para>
		/// <para>
		/// Internal (not const) so tests can lower it to force eviction without rasterizing 32MB of masks.
		/// </para>
		/// </remarks>
		internal static long MaxCachedBytes = 32L * 1024 * 1024;

		private static readonly object SyncRoot = new object();

		private static readonly Dictionary<LcdMaskKey, Entry> Entries = new Dictionary<LcdMaskKey, Entry>();

		/// <summary>
		/// Recency order, least recently used at the front. The reference scans a <c>VecDeque</c> to find
		/// the key it is promoting; keeping each key's node on its entry makes the promotion O(1) with the
		/// same semantics.
		/// </summary>
		private static readonly LinkedList<LcdMaskKey> Recency = new LinkedList<LcdMaskKey>();

		private static long buildCount;

		/// <summary>Sum of <c>Mask.Data.Length</c> over the entries currently held; guarded by SyncRoot.</summary>
		private static long cachedBytes;

		/// <summary>
		/// How many masks this cache has actually rasterized since the process started - it does not go down
		/// on eviction or <see cref="Clear"/>. Exists so a test can prove a hit was a hit: two calls that
		/// return equal bytes prove nothing on their own, because a rebuild would return equal bytes too.
		/// </summary>
		public static long BuildCount
		{
			get
			{
				lock (SyncRoot)
				{
					return buildCount;
				}
			}
		}

		/// <summary>Entries currently held.</summary>
		public static int Count
		{
			get
			{
				lock (SyncRoot)
				{
					return Entries.Count;
				}
			}
		}

		/// <summary>
		/// Bytes of mask data currently held - what <see cref="MaxCachedBytes"/> bounds, and the only number
		/// that describes what this cache costs (see the class remarks on why <see cref="Count"/> does not).
		/// </summary>
		public static long CachedBytes
		{
			get
			{
				lock (SyncRoot)
				{
					return cachedBytes;
				}
			}
		}

		/// <summary>
		/// Drops every entry. Called whenever a setting that changes the raster changes
		/// (<see cref="LcdRenderSettings"/>), and available to a caller that has invalidated the geometry
		/// behind a path identity it already used.
		/// </summary>
		public static void Clear()
		{
			lock (SyncRoot)
			{
				Entries.Clear();
				Recency.Clear();
				cachedBytes = 0;
			}
		}

		/// <summary>
		/// The cached form of <see cref="BoundedMaskBuilder.TryBuild"/>: returns the path's bbox-sized
		/// coverage mask and the whole-pixel destination origin it composites at, rasterizing only on a
		/// miss.
		/// </summary>
		/// <param name="pathIdentity">Caller-supplied identity of the geometry, or <b>null to bypass the
		/// cache entirely</b> and always rasterize. Null is the honest answer for a caller drawing
		/// throwaway geometry - the reference does not cache its general vector fills either
		/// (<c>lcd_coverage.rs</c> <c>fill_path</c> builds a bounded mask every call); only the text path,
		/// which has a stable identity, is cached. See <see cref="LcdMaskKey"/> for what makes an identity
		/// valid.</param>
		/// <param name="bufferWidth">Destination width in pixels.</param>
		/// <param name="bufferHeight">Destination height in pixels.</param>
		/// <param name="path">The vertex source to fill.</param>
		/// <param name="transform">Path space to destination pixel space.</param>
		/// <param name="mask">The coverage mask - <b>shared and read-only</b>, see the class remarks.</param>
		/// <param name="originX">Destination x of the mask's left column.</param>
		/// <param name="originY">Destination y of the mask's bottom row (Y-up).</param>
		/// <param name="clip">Optional clip rect in destination pixels.</param>
		/// <param name="fillRule">Fill rule for the path.</param>
		/// <param name="primaryWeight">Filter center-tap weight.</param>
		/// <param name="gamma">Post-filter curve.</param>
		/// <param name="gray">True for the chroma-free fallback collapse.</param>
		/// <returns>False when there is nothing to paint - the same "empty bbox" answer
		/// <see cref="BoundedMaskBuilder.TryBuild"/> gives. That answer is deliberately <b>not</b> cached:
		/// it is the cheap case (a bbox test, no raster), and caching it would spend entries on geometry
		/// that is scrolled off screen and evict the entries that are on it.</returns>
		public static bool TryGetBoundedMask(
			object pathIdentity,
			int bufferWidth,
			int bufferHeight,
			IVertexSource path,
			Affine transform,
			out LcdMask mask,
			out int originX,
			out int originY,
			RectangleDouble? clip = null,
			filling_rule_e fillRule = filling_rule_e.fill_non_zero,
			double primaryWeight = LcdFilter.DefaultPrimaryWeight,
			double gamma = LcdFilter.DefaultGamma,
			bool gray = false)
		{
			return GetMask(
				pathIdentity,
				trimToDestination: true,
				bufferWidth,
				bufferHeight,
				path,
				transform,
				out mask,
				out originX,
				out originY,
				clip,
				fillRule,
				primaryWeight,
				gamma,
				gray) == UnclippedMaskResult.Built;
		}

		/// <summary>
		/// The cached form of <see cref="BoundedMaskBuilder.BuildUnclipped"/>: the mask covers the path's
		/// whole padded bbox with no reference to any destination, so <b>one entry serves the same geometry at
		/// every whole-pixel position</b> and the caller composites it where it belongs.
		/// </summary>
		/// <remarks>
		/// This is the pairing the reference's cached text path uses, and the two halves are inseparable: a
		/// mask trimmed to a destination or a clip carries where it was drawn in its bytes, so it cannot be
		/// shared across positions (see <see cref="BoundedMaskBuilder.BuildUnclipped"/>). Untrimmed, the
		/// only thing position contributes is the sub-pixel phase in <paramref name="transform"/>'s
		/// translation - which the caller is expected to have reduced to the fraction, keeping the whole-pixel
		/// part for the composite origin.
		/// <para>
		/// Whoever composites the result owes it the clip that was not applied here.
		/// </para>
		/// </remarks>
		/// <param name="pathIdentity">Identity of the geometry, or null to bypass the cache - see
		/// <see cref="TryGetBoundedMask"/>.</param>
		/// <param name="path">The vertex source to fill.</param>
		/// <param name="transform">Path space to destination pixel space, ideally carrying only the sub-pixel
		/// phase of the placement (see the remarks).</param>
		/// <param name="mask">The coverage mask - <b>shared and read-only</b>.</param>
		/// <param name="originX">Destination x of the mask's left column, <b>which may be negative</b>: with
		/// no destination to trim against, the bbox lands wherever the transform puts it.</param>
		/// <param name="originY">Destination y of the mask's bottom row (Y-up), which may also be
		/// negative.</param>
		/// <param name="fillRule">Fill rule for the path.</param>
		/// <param name="primaryWeight">Filter center-tap weight.</param>
		/// <param name="gamma">Post-filter curve.</param>
		/// <param name="gray">True for the chroma-free fallback collapse.</param>
		/// <returns><see cref="UnclippedMaskResult.Empty"/> when the path carries no vertices, the same
		/// "nothing to paint" answer <see cref="TryGetBoundedMask"/> gives, and
		/// <see cref="UnclippedMaskResult.TooLarge"/> when the geometry is too big to serve this way and the
		/// caller has to paint it the ordinary way. Neither answer is cached: both are cheap to reach again
		/// (a bbox test, no raster), and spending entries on them would evict the masks that are on
		/// screen.</returns>
		public static UnclippedMaskResult GetUnclippedMask(
			object pathIdentity,
			IVertexSource path,
			Affine transform,
			out LcdMask mask,
			out int originX,
			out int originY,
			filling_rule_e fillRule = filling_rule_e.fill_non_zero,
			double primaryWeight = LcdFilter.DefaultPrimaryWeight,
			double gamma = LcdFilter.DefaultGamma,
			bool gray = false)
		{
			// The 0 x 0 "destination" and null clip in the key are what keeps these entries from colliding
			// with trimmed ones: TryGetBoundedMask refuses a destination that small outright, so no trimmed
			// mask can ever be filed under those key values.
			return GetMask(
				pathIdentity,
				trimToDestination: false,
				0,
				0,
				path,
				transform,
				out mask,
				out originX,
				out originY,
				null,
				fillRule,
				primaryWeight,
				gamma,
				gray);
		}

		private static UnclippedMaskResult GetMask(
			object pathIdentity,
			bool trimToDestination,
			int bufferWidth,
			int bufferHeight,
			IVertexSource path,
			Affine transform,
			out LcdMask mask,
			out int originX,
			out int originY,
			RectangleDouble? clip,
			filling_rule_e fillRule,
			double primaryWeight,
			double gamma,
			bool gray)
		{
			if (path == null)
			{
				throw new ArgumentNullException(nameof(path));
			}

			if (pathIdentity == null)
			{
				return Build(trimToDestination, bufferWidth, bufferHeight, path, transform, out mask, out originX, out originY, clip, fillRule, primaryWeight, gamma, gray);
			}

			var key = new LcdMaskKey(pathIdentity, transform, bufferWidth, bufferHeight, clip, fillRule, primaryWeight, gamma, gray);

			lock (SyncRoot)
			{
				if (Entries.TryGetValue(key, out Entry hit))
				{
					// Promote to most recently used, so the cap evicts what is genuinely cold rather than
					// whatever happened to be inserted first.
					Recency.Remove(hit.RecencyNode);
					Recency.AddLast(hit.RecencyNode);

					mask = hit.Mask;
					originX = hit.OriginX;
					originY = hit.OriginY;
					return UnclippedMaskResult.Built;
				}
			}

			UnclippedMaskResult result = Build(trimToDestination, bufferWidth, bufferHeight, path, transform, out mask, out originX, out originY, clip, fillRule, primaryWeight, gamma, gray);
			if (result != UnclippedMaskResult.Built)
			{
				return result;
			}

			lock (SyncRoot)
			{
				// Another thread can have inserted the same key while this one rasterized. Its mask is
				// equally valid (same key, same bytes), but the one already in the cache may have been
				// handed out, so keep that one and let this raster be the throwaway.
				if (Entries.TryGetValue(key, out Entry raced))
				{
					mask = raced.Mask;
					originX = raced.OriginX;
					originY = raced.OriginY;
					return UnclippedMaskResult.Built;
				}

				LinkedListNode<LcdMaskKey> node = Recency.AddLast(key);
				Entries[key] = new Entry(mask, originX, originY, node);
				cachedBytes += mask.Data.Length;

				// Both caps evict the same way, least recently used first. The byte cap stops at one entry
				// rather than emptying the cache: a single mask larger than the whole budget is still the one
				// that was just asked for, and dropping it would leave the caller rasterizing it every frame
				// for nothing. The next insert evicts it.
				while (Recency.Count > Capacity
					|| (cachedBytes > MaxCachedBytes && Recency.Count > 1))
				{
					LinkedListNode<LcdMaskKey> oldest = Recency.First;
					Recency.RemoveFirst();
					if (Entries.TryGetValue(oldest.Value, out Entry evicted))
					{
						cachedBytes -= evicted.Mask.Data.Length;
						Entries.Remove(oldest.Value);
					}
				}
			}

			return UnclippedMaskResult.Built;
		}

		private static UnclippedMaskResult Build(
			bool trimToDestination,
			int bufferWidth,
			int bufferHeight,
			IVertexSource path,
			Affine transform,
			out LcdMask mask,
			out int originX,
			out int originY,
			RectangleDouble? clip,
			filling_rule_e fillRule,
			double primaryWeight,
			double gamma,
			bool gray)
		{
			UnclippedMaskResult built;
			if (trimToDestination)
			{
				// A trimmed mask is bounded by the destination, so it has no size of its own to refuse:
				// TooLarge is not one of the answers this branch can give.
				built = BoundedMaskBuilder.TryBuild(
					bufferWidth,
					bufferHeight,
					path,
					transform,
					out mask,
					out originX,
					out originY,
					clip,
					fillRule,
					primaryWeight,
					gamma,
					gray)
					? UnclippedMaskResult.Built
					: UnclippedMaskResult.Empty;
			}
			else
			{
				built = BoundedMaskBuilder.BuildUnclipped(
					path,
					transform,
					out mask,
					out originX,
					out originY,
					fillRule,
					primaryWeight,
					gamma,
					gray);
			}

			if (built == UnclippedMaskResult.Built)
			{
				lock (SyncRoot)
				{
					buildCount++;
				}
			}

			return built;
		}

		private readonly struct Entry
		{
			public Entry(LcdMask mask, int originX, int originY, LinkedListNode<LcdMaskKey> recencyNode)
			{
				this.Mask = mask;
				this.OriginX = originX;
				this.OriginY = originY;
				this.RecencyNode = recencyNode;
			}

			public LcdMask Mask { get; }

			public int OriginX { get; }

			public int OriginY { get; }

			public LinkedListNode<LcdMaskKey> RecencyNode { get; }
		}
	}
}
