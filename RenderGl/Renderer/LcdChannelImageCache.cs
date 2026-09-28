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

using System.Runtime.CompilerServices;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The CPU-side per-channel pass images <see cref="Graphics2DGpu"/>'s LCD composites draw through - three
	/// for an <see cref="LcdBuffer"/>, three for an <see cref="LcdMask"/> - packed from the planes and cached
	/// process wide, weakly keyed on the buffer or mask they came from.
	/// </summary>
	internal static class LcdChannelImageCache
	{
		// The three per-channel pass images an LcdBuffer composites through. Like Graphics2DGpu's aATextureImages these are
		// cpu side ImageBuffers with no gl affinity, so one set is shared by every context and the per
		// context part is left to ImageTexturePlugin, which already keys its textures by (pixel buffer,
		// context) and re-uploads on InvalidateGlCaches through MarkAllImagesNeedRefresh.
		// Weak on the buffer so a widget's planes take their pass images with them when the widget goes.
		private static readonly ConditionalWeakTable<LcdBuffer, LcdBufferChannelImages> lcdChannelImages = new ConditionalWeakTable<LcdBuffer, LcdBufferChannelImages>();
		private static readonly object lcdChannelImagesLock = new object();

		// The same arrangement for a single mask's three pass images. Weak on the mask because a mask lives in
		// LcdMaskCache, which is LRU bounded - an evicted mask has to be able to take its textures with it, and
		// a strong table here would pin every glyph run the process ever drew.
		// No change stamp, where the buffer table needs one: a mask is finished when it is built and is handed
		// out read only (see Graphics2D.CompositeLcdMask), so one pack per mask is all there ever is.
		private static readonly ConditionalWeakTable<LcdMask, ImageBuffer[]> lcdMaskChannelImages = new ConditionalWeakTable<LcdMask, ImageBuffer[]>();
		private static readonly object lcdMaskChannelImagesLock = new object();

		/// <summary>
		/// This mask's three per-channel pass images: channel <c>c</c>'s coverage as white premultiplied by
		/// itself, built once and then shared by every draw of that mask.
		/// </summary>
		/// <remarks>
		/// The pack runs outside the lock, so two threads that both miss can both build - the loser's images
		/// are simply dropped, and only the published set is ever drawn with. That is the same trade the buffer
		/// table above makes: holding a process wide lock across an O(width * height) pass would park every
		/// other context behind a glyph run's repack.
		/// </remarks>
		internal static ImageBuffer[] GetMaskChannelImages(LcdMask mask)
		{
			lock (lcdMaskChannelImagesLock)
			{
				if (lcdMaskChannelImages.TryGetValue(mask, out ImageBuffer[] cached))
				{
					return cached;
				}
			}

			ImageBuffer[] built = PackMaskChannelImages(mask);

			lock (lcdMaskChannelImagesLock)
			{
				if (lcdMaskChannelImages.TryGetValue(mask, out ImageBuffer[] published))
				{
					return published;
				}

				lcdMaskChannelImages.Add(mask, built);
				return built;
			}
		}

		/// <summary>
		/// Reduces <paramref name="mask"/> to one ordinary premultiplied BGRA image per pass, each holding
		/// channel <c>c</c>'s coverage in all four bytes.
		/// </summary>
		/// <remarks>
		/// White premultiplied by the coverage, rather than the coverage in alpha alone: see
		/// <see cref="Graphics2DGpu.CompositeLcdMask"/> for why the image has to be premultiplied to survive the texture
		/// uploader, and <see cref="LcdBufferChannelImages"/> for why a valid premultiplied image
		/// (<c>color &lt;= alpha</c>, trivially true here) makes that blit lossless. The two color channels the
		/// pass's write mask discards are white too, which costs nothing and keeps the image a plain
		/// interpretation of itself - a coverage image - rather than a channel-selecting one, because unlike
		/// the buffer form there is no per-channel color to select.
		/// <para>
		/// Row <c>y</c> in, row <c>y</c> out. Both the mask and the image are Y-up and agg-sharp's GL texture
		/// path is Y-up end to end, so there is no flip anywhere in this composite.
		/// </para>
		/// </remarks>
		private static ImageBuffer[] PackMaskChannelImages(LcdMask mask)
		{
			var images = new ImageBuffer[LcdBufferChannelImages.ChannelCount];

			for (int channel = 0; channel < images.Length; channel++)
			{
				var image = new ImageBuffer(mask.Width, mask.Height, 32, new BlenderPreMultBGRA());
				byte[] pixels = image.GetBuffer();
				int bytesPerPixel = image.GetBytesBetweenPixelsInclusive();

				for (int y = 0; y < mask.Height; y++)
				{
					int rowOffset = image.GetBufferOffsetXY(0, y);
					int source = mask.PixelOffset(0, y) + channel;

					for (int x = 0; x < mask.Width; x++, source += 3)
					{
						byte coverage = mask.Data[source];
						int offset = rowOffset + (x * bytesPerPixel);
						pixels[offset + ImageBuffer.OrderR] = coverage;
						pixels[offset + ImageBuffer.OrderG] = coverage;
						pixels[offset + ImageBuffer.OrderB] = coverage;
						pixels[offset + ImageBuffer.OrderA] = coverage;
					}
				}

				images[channel] = image;
			}

			return images;
		}

		/// <summary>
		/// This buffer's three per-channel pass images, repacked if the buffer has been painted since they
		/// were last built.
		/// </summary>
		/// <remarks>
		/// The lock covers only the table, not the repack: the repack writes into images owned by this
		/// buffer, and a buffer is painted and composited by the one thread that owns it, so two threads
		/// racing here would already be racing over the planes themselves. Holding a process wide lock across
		/// an O(width * height) pass over a full window backbuffer, on the other hand, would park every other
		/// context behind it.
		/// </remarks>
		internal static LcdBufferChannelImages GetBufferChannelImages(LcdBuffer buffer)
		{
			LcdBufferChannelImages images;
			lock (lcdChannelImagesLock)
			{
				if (!lcdChannelImages.TryGetValue(buffer, out images)
					|| images.Width != buffer.Width
					|| images.Height != buffer.Height)
				{
					images = new LcdBufferChannelImages(buffer.Width, buffer.Height);
					lcdChannelImages.AddOrUpdate(buffer, images);
				}
			}

			images.UpdateFrom(buffer);
			return images;
		}
	}
}
