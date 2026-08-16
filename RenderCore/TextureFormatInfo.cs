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

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// Pixel sizes and the texture-copy row alignment rule. Backends and callers must agree on this
	/// arithmetic exactly: getting it wrong does not throw, it shears the image by a few pixels per
	/// row, which is why it lives in one place and is unit tested.
	/// </summary>
	public static class TextureFormatInfo
	{
		/// <summary>
		/// The row alignment WebGPU requires of every buffer involved in a texture copy
		/// (<c>WGPU_COPY_BYTES_PER_ROW_ALIGNMENT</c>). D3D11 surfaced the same idea as RowPitch; the
		/// difference is that WebGPU <em>rejects</em> unaligned copies rather than reporting the pitch
		/// afterwards, so the padding has to be computed up front.
		/// </summary>
		public const uint CopyBytesPerRowAlignment = 256;

		/// <summary>The number of bytes one texel of <paramref name="format"/> occupies.</summary>
		/// <exception cref="ArgumentOutOfRangeException">The format has no fixed byte size (it is <see cref="TextureFormat.Undefined"/>).</exception>
		public static uint BytesPerPixel(TextureFormat format)
		{
			switch (format)
			{
				case TextureFormat.R8Unorm:
					return 1;

				case TextureFormat.Rg8Unorm:
					return 2;

				case TextureFormat.Rgba8Unorm:
				case TextureFormat.Bgra8Unorm:
				case TextureFormat.Depth32Float:
					return 4;

				case TextureFormat.Rg32Float:
				case TextureFormat.Rgba16Float:
					return 8;

				case TextureFormat.Rgba32Float:
					return 16;

				default:
					throw new ArgumentOutOfRangeException(nameof(format), format, "No fixed byte size for this format.");
			}
		}

		/// <summary>
		/// The tightly packed byte count of one row - width times pixel size, with no padding. This is
		/// what a naive caller assumes the stride is, and why <see cref="TextureReadResult.RowStride"/>
		/// has to be reported back.
		/// </summary>
		public static uint TightRowStride(TextureFormat format, uint width) => BytesPerPixel(format) * width;

		/// <summary>
		/// The row stride a texture readback actually uses: the tight stride rounded up to
		/// <see cref="CopyBytesPerRowAlignment"/>. A 64 pixel wide Rgba8 row is 256 bytes and needs no
		/// padding; a 65 pixel row is 260 bytes and is padded to 512.
		/// </summary>
		public static uint AlignedRowStride(TextureFormat format, uint width)
		{
			uint tight = TightRowStride(format, width);
			return AlignRowStride(tight);
		}

		/// <summary>Rounds a byte count up to the next multiple of <see cref="CopyBytesPerRowAlignment"/>.</summary>
		public static uint AlignRowStride(uint bytesPerRow)
		{
			uint remainder = bytesPerRow % CopyBytesPerRowAlignment;
			return remainder == 0 ? bytesPerRow : bytesPerRow + (CopyBytesPerRowAlignment - remainder);
		}
	}

	/// <summary>
	/// What a completed <see cref="IRenderDevice.ReadTextureAsync"/> produced. The caller supplied the
	/// destination, so the only thing it does not already know is how the rows are laid out inside it.
	/// </summary>
	public readonly struct TextureReadResult : IEquatable<TextureReadResult>
	{
		/// <summary>Creates a result describing a readback's layout.</summary>
		/// <param name="width">Width in pixels of the region that was read.</param>
		/// <param name="height">Height in pixels of the region that was read.</param>
		/// <param name="rowStride">Bytes from the start of one row to the start of the next.</param>
		public TextureReadResult(uint width, uint height, uint rowStride)
		{
			this.Width = width;
			this.Height = height;
			this.RowStride = rowStride;
		}

		/// <summary>Width in pixels of the region that was read.</summary>
		public uint Width { get; }

		/// <summary>Height in pixels of the region that was read.</summary>
		public uint Height { get; }

		/// <summary>
		/// Bytes from the start of one row to the start of the next in the destination buffer. This is
		/// at least the tightly packed row size and is usually larger - assuming tight packing silently
		/// shears the image.
		/// </summary>
		public uint RowStride { get; }

		/// <summary>The number of destination bytes the read filled, padding included.</summary>
		public ulong TotalBytes => (ulong)this.RowStride * this.Height;

		/// <inheritdoc/>
		public bool Equals(TextureReadResult other)
			=> this.Width == other.Width && this.Height == other.Height && this.RowStride == other.RowStride;

		/// <inheritdoc/>
		public override bool Equals(object obj) => obj is TextureReadResult other && this.Equals(other);

		/// <inheritdoc/>
		public override int GetHashCode() => HashCode.Combine(this.Width, this.Height, this.RowStride);

		/// <inheritdoc/>
		public override string ToString() => $"{this.Width}x{this.Height} stride {this.RowStride}";
	}
}
