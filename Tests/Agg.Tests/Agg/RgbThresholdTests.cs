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
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.ImageProcessing.Tests
{
	/// <summary>
	/// The MaxRGB32 threshold tests take a pixel as over the threshold when any of its colour channels is.
	/// They read the first byte three times, so a pixel whose only bright channel was green or red (the
	/// bytes after blue in BGRA) never counted.
	/// </summary>
	public class RgbThresholdTests
	{
		[Test]
		[Arguments(0, 0, 200, true)]
		[Arguments(0, 200, 0, true)]
		[Arguments(200, 0, 0, true)]
		[Arguments(50, 60, 70, false)]
		public async Task MaxRGB32SeesEveryColourChannel(byte blue, byte green, byte red, bool expected)
		{
			byte[] bgra = { blue, green, red, 255 };

			await Assert.That(InvertLightness.MaxRGB32(bgra, 0, 100)).IsEqualTo(expected);
			await Assert.That(Threshold.MaxRGB32(bgra, 0, 100)).IsEqualTo(expected);
		}

		[Test]
		public async Task ThresholdTurnsAPixelWithABrightRedChannelWhite()
		{
			var image = new ImageBuffer(1, 1);
			image.SetPixel(0, 0, new Color(200, 0, 0));

			image.DoThreshold(100);

			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(Color.White);
		}

		/// <summary>
		/// The one-argument DoThreshold also serves 8 bit images, one byte per pixel; testing three bytes there
		/// would read the neighbours and run off the end of the buffer at the last pixel.
		/// </summary>
		[Test]
		public async Task ThresholdOnAGrayImageTestsEachPixelsOwnByte()
		{
			var image = new ImageBuffer(3, 1, 8, new BlenderGrayExact(1));
			byte[] buffer = image.GetBuffer();
			int offset = image.GetBufferOffsetY(0);
			buffer[offset + 0] = 50;
			buffer[offset + 1] = 50;
			buffer[offset + 2] = 200;

			image.DoThreshold(100);

			await Assert.That(buffer[offset + 0]).IsEqualTo((byte)0);
			await Assert.That(buffer[offset + 1]).IsEqualTo((byte)0);
			await Assert.That(buffer[offset + 2]).IsEqualTo((byte)255);
		}
	}
}
