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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// Grayscale coverage masks for identified fills (text runs - <see cref="IVertexSourceRenderIdentity"/>),
	/// rasterized by AGG's own scanline renderer and cached, so GPU text has exactly the software renderer's
	/// weight. This is agg-gui's glyph cache arrangement (<c>gl_renderer/glyph_cache.rs</c>): text is not
	/// tessellated at all, it is AGG coverage drawn as a texture in the text color.
	/// </summary>
	/// <remarks>
	/// The halo fill every other shape takes grows a shape by half a pixel, which small text shows as
	/// visibly bolder glyphs. A mask here is premultiplied white (every channel holds the coverage), so it
	/// modulates a premultiplied draw color into the software composite's bytes.
	/// <para>
	/// The key is the source's identity plus the transform's linear part and sub-pixel phase; the whole-pixel
	/// part of the translation is left to the caller as a composite origin, so a run drawn at a new whole
	/// pixel position reuses its mask. That is the same placement split the LCD path uses
	/// (<c>Graphics2D.TryRenderThroughLcd</c>).
	/// </para>
	/// </remarks>
	public static class TextCoverageMaskCache
	{
		/// <summary>Largest whole-run mask side, in pixels. A bigger run is cut into <see cref="TileExtent"/> tiles.</summary>
		public const int MaxMaskExtent = 2048;

		/// <summary>Side of the square grid cells an oversized run is tiled by, from the corner of its pixel bounds.</summary>
		public const int TileExtent = 512;

		/// <summary>Entry cap before the least recently used one is dropped - <c>LcdMaskCache.Capacity</c>'s.</summary>
		public const int Capacity = 1024;

		/// <summary>
		/// Total bytes of cached mask images before the least recently used entries are dropped, whatever the
		/// entry count says.
		/// </summary>
		/// <remarks>
		/// The same policy and reasoning as <c>LcdMaskCache.MaxCachedBytes</c>: entries are not the same size (a
		/// label is a few kilobytes, a large heading hundreds), and a scrolling console paints every distinct
		/// line through <c>TypeFacePrinter</c>, so an entry cap alone would not bound memory. A mask here is
		/// <c>Width * Height * 4</c> bytes, plus a GPU texture of the same size per context that has drawn it
		/// (released through <c>ImageTexturePlugin</c>'s weak table when the mask is evicted), so a full
		/// cache costs about twice this in total. Internal so tests can lower it.
		/// </remarks>
		internal static long MaxCachedBytes = 32L * 1024 * 1024;

		/// <summary>The tile coordinate that keys a whole-run mask rather than a tile of one.</summary>
		private const int WholeRun = int.MinValue;

		private static readonly object SyncRoot = new object();

		private static readonly Dictionary<MaskKey, Entry> Entries = new Dictionary<MaskKey, Entry>();

		/// <summary>Recency order, least recently used at the front; each entry holds its own node.</summary>
		private static readonly LinkedList<MaskKey> Recency = new LinkedList<MaskKey>();

		private static long cachedBytes;

		private static int rasterizedCount;

		/// <summary>Number of masks currently cached.</summary>
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

		/// <summary>Bytes of mask images currently held - what <see cref="MaxCachedBytes"/> bounds.</summary>
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

		/// <summary>Number of masks rasterized since the process started; a draw that hits the cache leaves it unchanged.</summary>
		public static int RasterizedCount
		{
			get
			{
				lock (SyncRoot)
				{
					return rasterizedCount;
				}
			}
		}

		/// <summary>
		/// Returns the mask of <paramref name="source"/> under <paramref name="maskTransform"/> (whose translation
		/// must be the sub-pixel phase only), rasterizing it on first use; null when it is empty or too large.
		/// </summary>
		public static CoverageMask GetMask(object identity, IVertexSource source, Affine maskTransform)
			=> GetWholeRun(identity, source, maskTransform).Mask;

		/// <summary>
		/// Adds to <paramref name="masks"/> what draws <paramref name="source"/> under <paramref name="maskTransform"/>
		/// (as for <see cref="GetMask"/>) wherever it meets <paramref name="visible"/>, a rectangle in the same
		/// phase-only placement: the one whole-run mask, or for a run too large for one, the
		/// <see cref="TileExtent"/> tiles of it that <paramref name="visible"/> touches. Nothing for an empty run.
		/// </summary>
		/// <remarks>
		/// Tiling is what keeps a long unwrapped line (a horizontally scrolling text view, a wide editor) at the
		/// software weight: with no mask it would take the halo fill, visibly bolder. Only visible tiles are
		/// rasterized, and each is cached, so a run thousands of pixels long costs what is on screen.
		/// </remarks>
		public static void GetMasks(object identity, IVertexSource source, Affine maskTransform, RectangleDouble visible, List<CoverageMask> masks)
		{
			var whole = GetWholeRun(identity, source, maskTransform);
			if (whole.Mask != null)
			{
				masks.Add(whole.Mask);
				return;
			}

			if (!(whole.RunBounds.Width > 0) || !(whole.RunBounds.Height > 0))
			{
				return;
			}

			// Clamped before the cast, so an unbounded clip cannot overflow; the run bounds the loops either way.
			RectangleInt run = PixelRegion(whole.RunBounds);
			int left = Math.Max(run.Left, (int)Math.Floor(Math.Max(visible.Left, int.MinValue / 2)));
			int bottom = Math.Max(run.Bottom, (int)Math.Floor(Math.Max(visible.Bottom, int.MinValue / 2)));
			int right = Math.Min(run.Right, (int)Math.Ceiling(Math.Min(visible.Right, int.MaxValue / 2)));
			int top = Math.Min(run.Top, (int)Math.Ceiling(Math.Min(visible.Top, int.MaxValue / 2)));
			// The grid starts at the run's own corner rather than the origin, so a line shorter than a tile is one
			// row of tiles wherever its baseline sits. Both differences are non-negative when the loops run at all.
			for (int tileY = (bottom - run.Bottom) / TileExtent; run.Bottom + tileY * TileExtent < top; tileY++)
			{
				for (int tileX = (left - run.Left) / TileExtent; run.Left + tileX * TileExtent < right; tileX++)
				{
					var tile = GetTile(identity, source, maskTransform, run, tileX, tileY);
					if (tile != null)
					{
						masks.Add(tile);
					}
				}
			}
		}

		/// <summary>Drops every entry.</summary>
		public static void Clear()
		{
			lock (SyncRoot)
			{
				Entries.Clear();
				Recency.Clear();
				cachedBytes = 0;
			}
		}

		private static Entry GetWholeRun(object identity, IVertexSource source, Affine maskTransform)
		{
			var key = KeyOf(identity, maskTransform, WholeRun, WholeRun);
			if (TryGet(key, out var hit))
			{
				return hit;
			}

			var placedSource = new VertexSourceApplyTransform(source, maskTransform);
			var bounds = placedSource.GetBounds();
			var mask = bounds.Width > 0 && bounds.Height > 0 && bounds.Width <= MaxMaskExtent && bounds.Height <= MaxMaskExtent
				? Rasterize(placedSource, PixelRegion(bounds))
				: null;
			return Insert(key, mask, bounds);
		}

		/// <summary>The part of <paramref name="run"/> in tile (<paramref name="tileX"/>, <paramref name="tileY"/>), rasterized on first use.</summary>
		private static CoverageMask GetTile(object identity, IVertexSource source, Affine maskTransform, RectangleInt run, int tileX, int tileY)
		{
			var key = KeyOf(identity, maskTransform, tileX, tileY);
			if (TryGet(key, out var hit))
			{
				return hit.Mask;
			}

			// The tile's cell trimmed to the run, so a text line's tiles are only as tall as the line.
			var region = new RectangleInt(
				run.Left + tileX * TileExtent,
				run.Bottom + tileY * TileExtent,
				Math.Min(run.Right, run.Left + (tileX + 1) * TileExtent),
				Math.Min(run.Top, run.Bottom + (tileY + 1) * TileExtent));
			var mask = region.Width > 0 && region.Height > 0
				? Rasterize(new VertexSourceApplyTransform(source, maskTransform), region)
				: null;
			return Insert(key, mask, default).Mask;
		}

		private static MaskKey KeyOf(object identity, Affine maskTransform, int tileX, int tileY)
			=> new MaskKey(identity, maskTransform.sx, maskTransform.shy, maskTransform.shx, maskTransform.sy, maskTransform.tx, maskTransform.ty, tileX, tileY);

		private static bool TryGet(MaskKey key, out Entry hit)
		{
			lock (SyncRoot)
			{
				if (Entries.TryGetValue(key, out hit))
				{
					// Promote, so the caps evict what is genuinely cold rather than what was inserted first.
					Recency.Remove(hit.RecencyNode);
					Recency.AddLast(hit.RecencyNode);
					return true;
				}
			}

			return false;
		}

		private static Entry Insert(MaskKey key, CoverageMask mask, RectangleDouble runBounds)
		{
			lock (SyncRoot)
			{
				rasterizedCount++;

				// Another thread may have inserted the same key meanwhile; keep the one already handed out.
				if (Entries.TryGetValue(key, out var raced))
				{
					return raced;
				}

				var entry = new Entry(mask, runBounds, Recency.AddLast(key));
				Entries[key] = entry;
				cachedBytes += BytesOf(mask);

				// Least recently used first for both caps. The byte cap stops at one entry: a mask larger than
				// the whole budget is still the one just asked for, and the next insert evicts it.
				while (Recency.Count > Capacity
					|| (cachedBytes > MaxCachedBytes && Recency.Count > 1))
				{
					var oldest = Recency.First.Value;
					Recency.RemoveFirst();
					if (Entries.Remove(oldest, out var evicted))
					{
						cachedBytes -= BytesOf(evicted.Mask);
					}
				}

				return entry;
			}
		}

		private static long BytesOf(CoverageMask mask) => mask == null ? 0 : (long)mask.Image.Width * mask.Image.Height * 4;

		/// <summary>
		/// The pixels <paramref name="bounds"/> can put coverage in, with a pixel of margin on each side: AGG coverage
		/// never reaches past the pixel an edge is in.
		/// </summary>
		private static RectangleInt PixelRegion(RectangleDouble bounds)
			=> new RectangleInt(
				(int)Math.Floor(bounds.Left) - 1,
				(int)Math.Floor(bounds.Bottom) - 1,
				(int)Math.Ceiling(bounds.Right) + 1,
				(int)Math.Ceiling(bounds.Top) + 1);

		/// <summary>
		/// Rasterizes the part of <paramref name="placedSource"/> in <paramref name="region"/>. AGG clips the path to
		/// the image, and clipping keeps the exact coverage of the pixels inside, so tiles cut from one run meet
		/// seamlessly.
		/// </summary>
		private static CoverageMask Rasterize(IVertexSource placedSource, RectangleInt region)
		{
			var image = new ImageBuffer(region.Width, region.Height, 32, new BlenderPreMultBGRA());
			var graphics = image.NewGraphics2D();
			graphics.Render(new VertexSourceApplyTransform(placedSource, Affine.NewTranslation(-region.Left, -region.Bottom)), Color.White);

			// Only the alpha channel is trusted as coverage: the blender leaves partially covered pixels'
			// color at full white, which modulates into a too-bright (not premultiplied) source. Copy the
			// coverage into every channel so the mask is premultiplied white.
			byte[] buffer = image.GetBuffer();
			for (int i = 0; i < buffer.Length; i += 4)
			{
				byte coverage = buffer[i + 3];
				buffer[i] = coverage;
				buffer[i + 1] = coverage;
				buffer[i + 2] = coverage;
			}

			return new CoverageMask(image, region.Left, region.Bottom);
		}

		/// <summary>A cached mask; for a whole-run entry also the run's placed bounds, which is what an oversized run is tiled over.</summary>
		private sealed record Entry(CoverageMask Mask, RectangleDouble RunBounds, LinkedListNode<MaskKey> RecencyNode);

		/// <summary>Tile coordinates are <see cref="WholeRun"/> for the whole-run mask.</summary>
		private readonly record struct MaskKey(object Identity, double Sx, double Shy, double Shx, double Sy, double PhaseX, double PhaseY, int TileX, int TileY);

		/// <summary>A rasterized mask: premultiplied white coverage, and where its bottom-left pixel sits relative to the phase-only placement.</summary>
		public sealed class CoverageMask
		{
			internal CoverageMask(ImageBuffer image, int originX, int originY)
			{
				Image = image;
				OriginX = originX;
				OriginY = originY;
			}

			/// <summary>The coverage image; every channel holds the coverage.</summary>
			public ImageBuffer Image { get; }

			/// <summary>X of the image's left column, before the caller's whole-pixel offset.</summary>
			public int OriginX { get; }

			/// <summary>Y of the image's bottom row, before the caller's whole-pixel offset.</summary>
			public int OriginY { get; }
		}
	}
}
