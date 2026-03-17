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

namespace MatterHackers.RenderGl
{
	public static class ImageAlphaConverter
	{
		public static byte[] ConvertPremultipliedBgraToStraightAlpha(byte[] premultipliedBgra)
		{
			if (premultipliedBgra == null)
			{
				throw new ArgumentNullException(nameof(premultipliedBgra));
			}

			if (premultipliedBgra.Length % 4 != 0)
			{
				throw new ArgumentException("Expected BGRA pixels.", nameof(premultipliedBgra));
			}

			var straightAlphaBgra = new byte[premultipliedBgra.Length];
			for (int pixelOffset = 0; pixelOffset < premultipliedBgra.Length; pixelOffset += 4)
			{
				byte alpha = premultipliedBgra[pixelOffset + 3];
				straightAlphaBgra[pixelOffset + 0] = UnpremultiplyChannel(premultipliedBgra[pixelOffset + 0], alpha);
				straightAlphaBgra[pixelOffset + 1] = UnpremultiplyChannel(premultipliedBgra[pixelOffset + 1], alpha);
				straightAlphaBgra[pixelOffset + 2] = UnpremultiplyChannel(premultipliedBgra[pixelOffset + 2], alpha);
				straightAlphaBgra[pixelOffset + 3] = alpha;
			}

			return straightAlphaBgra;
		}

		private static byte UnpremultiplyChannel(byte channel, byte alpha)
		{
			if (alpha == 0)
			{
				return 0;
			}

			if (alpha == 255)
			{
				return channel;
			}

			return (byte)Math.Min(255, (channel * 255 + alpha / 2) / alpha);
		}
	}
}
