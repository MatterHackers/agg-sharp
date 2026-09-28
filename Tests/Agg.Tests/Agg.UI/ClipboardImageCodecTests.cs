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

using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The encoding every platform clipboard's image path goes through. The platform halves (NSPasteboard,
	/// ClipboardItem) only move these bytes, so a round trip here is what proves an image comes back the
	/// right way up and with its channels in the right order.
	/// </summary>
	public class ClipboardImageCodecTests
	{
		[Test]
		public async Task APngRoundTripKeepsEveryPixelAndItsOrientation()
		{
			var source = new ImageBuffer(3, 2);
			source.SetPixel(0, 0, new Color(255, 0, 0, 255));
			source.SetPixel(2, 0, new Color(0, 255, 0, 128));
			source.SetPixel(1, 1, new Color(0, 0, 255, 255));

			byte[] png = ClipboardImageCodec.EncodePng(source);

			await Assert.That(png).IsNotNull();

			// The PNG signature, so a platform that tags these bytes public.png / image/png is telling the truth.
			await Assert.That(png[1]).IsEqualTo((byte)'P');
			await Assert.That(png[2]).IsEqualTo((byte)'N');
			await Assert.That(png[3]).IsEqualTo((byte)'G');

			ImageBuffer decoded = ClipboardImageCodec.Decode(png);

			await Assert.That(decoded.Width).IsEqualTo(3);
			await Assert.That(decoded.Height).IsEqualTo(2);
			for (int y = 0; y < 2; y++)
			{
				for (int x = 0; x < 3; x++)
				{
					await Assert.That(decoded.GetPixel(x, y)).IsEqualTo(source.GetPixel(x, y));
				}
			}
		}

		[Test]
		public async Task A24BitImageIsWidenedRatherThanMisread()
		{
			var source = new ImageBuffer(2, 2, 24, new BlenderBGR());
			source.NewGraphics2D().Clear(new Color(10, 20, 30));

			ImageBuffer decoded = ClipboardImageCodec.Decode(ClipboardImageCodec.EncodePng(source));

			await Assert.That(decoded.GetPixel(1, 1)).IsEqualTo(new Color(10, 20, 30, 255));
		}

		/// <summary>Another application's bytes: nothing, or not an image, answers null rather than throwing.</summary>
		[Test]
		public async Task NothingOrGarbageDecodesToNull()
		{
			await Assert.That(ClipboardImageCodec.Decode(null)).IsNull();
			await Assert.That(ClipboardImageCodec.Decode(new byte[] { 1, 2, 3, 4 })).IsNull();
			await Assert.That(ClipboardImageCodec.EncodePng(null)).IsNull();
		}
	}
}
