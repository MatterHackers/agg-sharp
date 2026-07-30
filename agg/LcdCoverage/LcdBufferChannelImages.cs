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

namespace MatterHackers.Agg.LcdCoverage
{
	/// <summary>
	/// An <see cref="LcdBuffer"/>'s two planes repacked as the three ordinary premultiplied BGRA images a
	/// three-pass color-masked GPU composite draws: one image per subpixel channel, carrying that channel's
	/// premultiplied color in its own color slot and that channel's alpha in the alpha slot.
	/// </summary>
	/// <remarks>
	/// <b>Why three images and not one.</b> Dual-source blending is not portable, so the reference composites
	/// an LCD backbuffer by drawing the same quad three times with an R / G / B color write mask, each pass
	/// taking that channel's coverage as the source alpha (<c>demo-wgpu\src\pipelines.rs</c> <c>lcb_r</c> /
	/// <c>lcb_g</c> / <c>lcb_b</c>, <c>LCB_WGSL</c> in <c>shaders.rs</c>). Its fragment shader picks the
	/// channel out of two bound plane textures with a uniform; agg-sharp's GL path is fixed function and has
	/// no shader to select with, so the selection is baked into the pixels instead - three textures, each
	/// pre-reduced to one channel. That is the whole of the divergence, and it buys the LCD composite the
	/// right to run with no shader support at all.
	/// <para>
	/// Each image is exactly the reference shader's output for its pass: <c>vec4(col * cc, aa)</c> with
	/// <c>col</c> the channel's unit basis vector - so pass R holds <c>(colorRed, 0, 0, alphaRed)</c>, and the
	/// two masked-off color channels are zero rather than left as the other channels' data. The pass's write
	/// mask discards them either way, so what the zeroing actually buys is narrower than it looks. The
	/// texture uploader blits each image through <see cref="BlenderPreMultBGRA"/> onto a transparent
	/// destination, and that blend is byte exact for <i>any</i> source bytes - <c>dst * (1 - a)</c> is zero,
	/// so the source survives whatever it holds. The one place it is not exact is a source alpha of 0, which
	/// <c>BlendPixels</c> skips outright and leaves the destination transparent. Zeroing keeps every pixel
	/// <b>valid premultiplied</b> (<c>color_c &lt;= alpha_c</c> holds per channel in an
	/// <see cref="LcdBuffer"/>, so it holds here), so a skipped pixel is one that had nothing but zeros to
	/// write and the blit stays lossless.
	/// </para>
	/// <para>
	/// <b>Y orientation.</b> No flip. The reference flips (<c>lcd_coverage.rs</c> <c>flip_plane</c>) because
	/// its cached backbuffer planes are stored top-row-first while its buffers are Y-up; here both the source
	/// <see cref="LcdBuffer"/> and the destination <see cref="ImageBuffer"/> are Y-up (row 0 is the bottom),
	/// and agg-sharp's GL texture path is Y-up end to end - a texture is uploaded straight out of an
	/// <see cref="ImageBuffer"/> and drawn on a quad whose <c>t = 0</c> edge is its bottom. Row <c>y</c> of the
	/// buffer is therefore row <c>y</c> of the image, and the images land the same way up as the plain RGBA
	/// backbuffer blit they replace.
	/// </para>
	/// </remarks>
	public sealed class LcdBufferChannelImages
	{
		/// <summary>Number of passes, one per subpixel channel: R, G, B.</summary>
		public const int ChannelCount = 3;

		private readonly ImageBuffer[] images = new ImageBuffer[ChannelCount];

		private int builtFromChangedCount;

		private bool built;

		/// <summary>
		/// Allocates the three images for a <paramref name="width"/> x <paramref name="height"/> buffer. They
		/// hold no pixels until <see cref="UpdateFrom"/> fills them.
		/// </summary>
		public LcdBufferChannelImages(int width, int height)
		{
			if (width < 0 || height < 0)
			{
				throw new ArgumentException(
					$"LcdBufferChannelImages dimensions must not be negative, was {width} x {height}.",
					width < 0 ? nameof(width) : nameof(height));
			}

			this.Width = width;
			this.Height = height;

			for (int channel = 0; channel < ChannelCount; channel++)
			{
				// Premultiplied, because that is what the color plane holds - and because the GL texture
				// uploader round trips the image through this blender onto a transparent destination, where
				// premultiplied source-over is the identity and any other blender would not be.
				this.images[channel] = new ImageBuffer(width, height, 32, new BlenderPreMultBGRA());
			}
		}

		public int Width { get; }

		public int Height { get; }

		/// <summary>
		/// The image for one pass: 0 red, 1 green, 2 blue. Live, not a copy - the same instances across
		/// updates, so a texture cache keyed on an image's pixel buffer (as
		/// <c>ImageTexturePlugin</c>'s is) keeps its entry and re-uploads in place rather than leaking a new
		/// texture per repaint.
		/// </summary>
		public ImageBuffer this[int channel]
		{
			get
			{
				if (channel < 0 || channel >= ChannelCount)
				{
					throw new ArgumentOutOfRangeException(nameof(channel));
				}

				return this.images[channel];
			}
		}

		/// <summary>
		/// Whether this set was built for <paramref name="buffer"/> at its current contents - same size, same
		/// <see cref="LcdBuffer.ChangedCount"/>.
		/// </summary>
		public bool IsCurrentFor(LcdBuffer buffer)
		{
			return buffer != null
				&& this.built
				&& this.Width == buffer.Width
				&& this.Height == buffer.Height
				&& this.builtFromChangedCount == buffer.ChangedCount;
		}

		/// <summary>
		/// Repacks the three images from <paramref name="buffer"/> if they are not already current, and
		/// returns whether it did any work.
		/// </summary>
		/// <remarks>
		/// Rewrites the existing images in place and marks them changed, rather than allocating new ones: the
		/// texture cache downstream keys on the pixel array's identity and invalidates on
		/// <see cref="ImageBuffer.ChangedCount"/>, so writing in place turns a repaint into one re-upload over
		/// a texture name the context already owns, where fresh arrays would strand the old textures until a
		/// finalizer ran.
		/// </remarks>
		public bool UpdateFrom(LcdBuffer buffer)
		{
			if (buffer == null)
			{
				throw new ArgumentNullException(nameof(buffer));
			}

			if (buffer.Width != this.Width || buffer.Height != this.Height)
			{
				throw new ArgumentException(
					$"LcdBufferChannelImages is {this.Width} x {this.Height} but the buffer is {buffer.Width} x {buffer.Height}.",
					nameof(buffer));
			}

			if (this.IsCurrentFor(buffer))
			{
				return false;
			}

			for (int channel = 0; channel < ChannelCount; channel++)
			{
				Pack(buffer, channel, this.images[channel]);
				this.images[channel].MarkImageChanged();
			}

			this.builtFromChangedCount = buffer.ChangedCount;
			this.built = true;
			return true;
		}

		/// <summary>
		/// Writes one channel's pass image: that channel's premultiplied color in its own color slot, zero in
		/// the other two, that channel's alpha in the alpha slot. Row <c>y</c> in, row <c>y</c> out - see the
		/// class remarks for why there is no flip.
		/// </summary>
		private static void Pack(LcdBuffer buffer, int channel, ImageBuffer destination)
		{
			byte[] pixels = destination.GetBuffer();
			int bytesPerPixel = destination.GetBytesBetweenPixelsInclusive();
			byte[] color = buffer.ColorPlane;
			byte[] alpha = buffer.AlphaPlane;

			// Which of the destination's four bytes this pass's color lands in. The other two color bytes are
			// masked off by the pass's glColorMask and are written as zero.
			int colorByte = channel == 0
				? ImageBuffer.OrderR
				: channel == 1 ? ImageBuffer.OrderG : ImageBuffer.OrderB;

			for (int y = 0; y < buffer.Height; y++)
			{
				int rowOffset = destination.GetBufferOffsetXY(0, y);
				int source = buffer.PixelOffset(0, y) + channel;

				for (int x = 0; x < buffer.Width; x++, source += 3)
				{
					int offset = rowOffset + (x * bytesPerPixel);
					pixels[offset + ImageBuffer.OrderR] = 0;
					pixels[offset + ImageBuffer.OrderG] = 0;
					pixels[offset + ImageBuffer.OrderB] = 0;
					pixels[offset + colorByte] = color[source];
					pixels[offset + ImageBuffer.OrderA] = alpha[source];
				}
			}
		}
	}
}
