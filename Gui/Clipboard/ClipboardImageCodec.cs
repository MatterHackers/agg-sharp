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

using System.IO;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The byte form an image takes on a system clipboard: PNG going out, and whatever ImageSharp can
	/// read coming in. Shared by the platform clipboards so the part that can be got wrong - row order,
	/// channel order, bit depth - is one piece of code the desktop suite can round trip.
	/// </summary>
	/// <remarks>
	/// PNG because it is the one lossless, alpha-carrying format every clipboard agg runs on accepts:
	/// <c>public.png</c> on NSPasteboard, and the one image type <c>ClipboardItem</c> is required to take.
	/// </remarks>
	public static class ClipboardImageCodec
	{
		/// <summary>
		/// Encodes <paramref name="image"/> as PNG, or returns null when there is nothing to encode or the
		/// encoder refused it. Images that are not 32 bit are widened first, because the PNG path reads the
		/// buffer as BGRA.
		/// </summary>
		public static byte[] EncodePng(ImageBuffer image)
		{
			if (image == null || image.Width <= 0 || image.Height <= 0)
			{
				return null;
			}

			ImageBuffer bgra = image;
			if (image.BitDepth != 32)
			{
				bgra = new ImageBuffer(image.Width, image.Height);
				bgra.NewGraphics2D().Render(image, 0, 0);
			}

			using var stream = new MemoryStream();
			return ImageIO.SaveImageData(stream, ".png", bgra) ? stream.ToArray() : null;
		}

		/// <summary>
		/// Decodes clipboard image bytes (PNG, TIFF, or anything else ImageSharp recognises), or returns
		/// null when they are absent or not an image. Never throws: another application put these bytes
		/// there, and a malformed paste must not take this one down.
		/// </summary>
		public static ImageBuffer Decode(byte[] bytes)
		{
			if (bytes == null || bytes.Length == 0)
			{
				return null;
			}

			try
			{
				var image = new ImageBuffer();
				using var stream = new MemoryStream(bytes, writable: false);
				return ImageIO.LoadImageData(stream, image) ? image : null;
			}
			catch
			{
				return null;
			}
		}
	}
}
