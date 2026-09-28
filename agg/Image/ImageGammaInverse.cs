//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007-2026
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------

using System;

namespace MatterHackers.Agg.Image
{
	/// <summary>
	/// C++ AGG's pixfmt <c>apply_gamma_inv</c> and <c>apply_gamma_dir</c> (<c>apply_gamma_inv_rgb</c> / <c>_rgba</c>
	/// and their <c>_dir_</c> twins), kept out of <see cref="ImageBuffer"/> so that file does not grow.
	/// </summary>
	public static class ImageGammaInverse
	{
		/// <summary>
		/// Every pixel's red, green and blue replaced by <c>gamma.inv</c> of itself; alpha is left alone. For 24
		/// and 32 bit images.
		/// </summary>
		public static void Apply(ImageBuffer image, GammaLookUpTable gamma) => MapColorChannels(image, gamma.inv);

		/// <summary>
		/// C++ pixfmt <c>apply_gamma_dir</c>: every pixel's red, green and blue replaced by <c>gamma.dir</c> of
		/// itself (into linear light, as pattern_resample does to its picture); alpha is left alone. For 24 and 32
		/// bit images.
		/// </summary>
		public static void ApplyDir(ImageBuffer image, GammaLookUpTable gamma) => MapColorChannels(image, gamma.dir);

		private static void MapColorChannels(ImageBuffer image, Func<int, byte> map)
		{
			if (image.BitDepth != 24 && image.BitDepth != 32)
			{
				throw new NotSupportedException("Applying a gamma needs a 24 or 32 bit image.");
			}

			byte[] buffer = image.GetBuffer();
			int pixelStep = image.GetBytesBetweenPixelsInclusive();
			for (int y = 0; y < image.Height; y++)
			{
				int offset = image.GetBufferOffsetXY(0, y);
				for (int x = 0; x < image.Width; x++)
				{
					buffer[offset + ImageBuffer.OrderR] = map(buffer[offset + ImageBuffer.OrderR]);
					buffer[offset + ImageBuffer.OrderG] = map(buffer[offset + ImageBuffer.OrderG]);
					buffer[offset + ImageBuffer.OrderB] = map(buffer[offset + ImageBuffer.OrderB]);
					offset += pixelStep;
				}
			}

			image.MarkImageChanged();
		}
	}
}
