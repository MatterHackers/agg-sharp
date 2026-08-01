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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.ImageProcessing;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class BlenderTableTests
	{
		// The saturation table the blenders historically built lazily in their
		// instance constructors: table[i] = min(i, 255) over 512 entries.
		private static int[] BuildReferenceSaturateTable()
		{
			int[] table = new int[1 << 9];
			for (int i = 0; i < table.Length; i++)
			{
				table[i] = Math.Min(i, 255);
			}

			return table;
		}

		// Reimplements BlenderPreMultBGRA.BlendPixel using the reference table so the
		// test fails if the eager static initializer changed the blend semantics.
		private static byte[] ExpectedPreMultBGRABlend(byte[] dest, Color sourceColor)
		{
			int[] saturate = BuildReferenceSaturateTable();
			byte[] expected = (byte[])dest.Clone();
			int oneOverAlpha = 255 - sourceColor.alpha;
			int r = saturate[((dest[ImageBuffer.OrderR] * oneOverAlpha + 255) >> 8) + sourceColor.red];
			int g = saturate[((dest[ImageBuffer.OrderG] * oneOverAlpha + 255) >> 8) + sourceColor.green];
			int b = saturate[((dest[ImageBuffer.OrderB] * oneOverAlpha + 255) >> 8) + sourceColor.blue];
			int a = dest[ImageBuffer.OrderA];
			expected[ImageBuffer.OrderR] = (byte)r;
			expected[ImageBuffer.OrderG] = (byte)g;
			expected[ImageBuffer.OrderB] = (byte)b;
			expected[ImageBuffer.OrderA] = (byte)(255 - saturate[(oneOverAlpha * (255 - a) + 255) >> 8]);
			return expected;
		}

		[Test]
		public async Task BlenderPreMultBGRABlendPixelMatchesReferenceAlgorithm()
		{
			// A premultiplied source pixel whose components would saturate without the table.
			var sourceColor = new Color(200, 180, 160, 128);
			byte[] dest = new byte[4];
			dest[ImageBuffer.OrderR] = 100;
			dest[ImageBuffer.OrderG] = 240;
			dest[ImageBuffer.OrderB] = 250;
			dest[ImageBuffer.OrderA] = 64;

			byte[] expected = ExpectedPreMultBGRABlend(dest, sourceColor);

			byte[] actual = (byte[])dest.Clone();
			var blender = new BlenderPreMultBGRA();
			blender.BlendPixel(actual, 0, sourceColor);

			await Assert.That(actual[ImageBuffer.OrderR]).IsEqualTo(expected[ImageBuffer.OrderR]);
			await Assert.That(actual[ImageBuffer.OrderG]).IsEqualTo(expected[ImageBuffer.OrderG]);
			await Assert.That(actual[ImageBuffer.OrderB]).IsEqualTo(expected[ImageBuffer.OrderB]);
			await Assert.That(actual[ImageBuffer.OrderA]).IsEqualTo(expected[ImageBuffer.OrderA]);

			// Saturating case: table index ((250 * 245 + 255) >> 8) + 250 = 490,
			// which must clamp to 255 instead of wrapping or reading a zero entry.
			var saturatingSource = new Color(250, 250, 250, 10);
			byte[] saturatingDest = new byte[4];
			saturatingDest[ImageBuffer.OrderR] = 250;
			saturatingDest[ImageBuffer.OrderG] = 250;
			saturatingDest[ImageBuffer.OrderB] = 250;
			saturatingDest[ImageBuffer.OrderA] = 255;
			blender.BlendPixel(saturatingDest, 0, saturatingSource);
			await Assert.That((int)saturatingDest[ImageBuffer.OrderR]).IsEqualTo(255);
			await Assert.That((int)saturatingDest[ImageBuffer.OrderG]).IsEqualTo(255);
			await Assert.That((int)saturatingDest[ImageBuffer.OrderB]).IsEqualTo(255);
		}

		[Test]
		public async Task BlendPixelsWithTransparentSourceKeepsRemainingPixelsAligned()
		{
			var blender = new BlenderPreMultBGRA();

			// A fully transparent source pixel followed by two opaque ones. With a cover
			// of 128 the transparent pixel's alpha computes to 0, which is the case that
			// used to skip the loop without advancing the source/dest offsets - so the
			// loop kept re-reading the transparent pixel and never drew the rest of the span.
			var sourceColors = new Color[]
			{
				new Color(0, 0, 0, 0),
				new Color(200, 0, 0, 255),
				new Color(0, 200, 0, 255),
			};
			byte[] sourceCovers = new byte[] { 128 };

			byte[] dest = new byte[12];
			blender.BlendPixels(dest, 0, sourceColors, 0, sourceCovers, 0, true, 3);

			// Pixel 0 came from the transparent color, so it must be untouched.
			await Assert.That((int)dest[0 + ImageBuffer.OrderR]).IsEqualTo(0);
			await Assert.That((int)dest[0 + ImageBuffer.OrderG]).IsEqualTo(0);
			await Assert.That((int)dest[0 + ImageBuffer.OrderB]).IsEqualTo(0);
			await Assert.That((int)dest[0 + ImageBuffer.OrderA]).IsEqualTo(0);

			// Pixel 1 is the red source, pixel 2 the green one - under the bug both were
			// left unwritten because the loop never advanced past the transparent pixel.
			await Assert.That((int)dest[4 + ImageBuffer.OrderR]).IsGreaterThan(0);
			await Assert.That((int)dest[4 + ImageBuffer.OrderG]).IsEqualTo(0);
			await Assert.That((int)dest[8 + ImageBuffer.OrderG]).IsGreaterThan(0);
			await Assert.That((int)dest[8 + ImageBuffer.OrderR]).IsEqualTo(0);

			// Blending the same two opaque colors on their own must produce byte-identical
			// pixels, which pins the exact values without restating the blend math here.
			byte[] withoutTransparent = new byte[8];
			blender.BlendPixels(withoutTransparent, 0, sourceColors, 1, sourceCovers, 0, true, 2);

			for (int i = 0; i < 8; i++)
			{
				await Assert.That((int)dest[4 + i]).IsEqualTo((int)withoutTransparent[i]);
			}
		}

		[Test]
		public async Task SubtractLookupClampsCorrectly()
		{
			var imageA = new ImageBuffer(2, 1, 32);
			var imageB = new ImageBuffer(2, 1, 32);
			var result = new ImageBuffer(2, 1, 32);

			byte[] bufferA = imageA.GetBuffer();
			byte[] bufferB = imageB.GetBuffer();

			// Pixel 0: A - B goes negative -> must clamp to 0.
			bufferA[0] = 10; bufferA[1] = 20; bufferA[2] = 30; bufferA[3] = 255;
			bufferB[0] = 50; bufferB[1] = 20; bufferB[2] = 5; bufferB[3] = 255;

			// Pixel 1: plain positive difference.
			bufferA[4] = 200; bufferA[5] = 150; bufferA[6] = 100; bufferA[7] = 255;
			bufferB[4] = 55; bufferB[5] = 150; bufferB[6] = 99; bufferB[7] = 255;

			Subtract.DoSubtract(result, imageA, imageB);

			byte[] resultBuffer = result.GetBuffer();
			await Assert.That((int)resultBuffer[0]).IsEqualTo(0);
			await Assert.That((int)resultBuffer[1]).IsEqualTo(0);
			await Assert.That((int)resultBuffer[2]).IsEqualTo(25);
			await Assert.That((int)resultBuffer[3]).IsEqualTo(255);
			await Assert.That((int)resultBuffer[4]).IsEqualTo(145);
			await Assert.That((int)resultBuffer[5]).IsEqualTo(0);
			await Assert.That((int)resultBuffer[6]).IsEqualTo(1);
			await Assert.That((int)resultBuffer[7]).IsEqualTo(255);
		}

		[Test]
		public async Task ConcurrentBlenderConstructionProducesConsistentResults()
		{
			const int threadCount = 8;
			const int pixelCount = 64;

			// Deterministic source colors and dest pixels.
			var sourceColors = new Color[pixelCount];
			byte[] initialDest = new byte[pixelCount * 4];
			var random = new Random(12345);
			for (int i = 0; i < pixelCount; i++)
			{
				sourceColors[i] = new Color(random.Next(256), random.Next(256), random.Next(256), random.Next(1, 255));
				for (int j = 0; j < 4; j++)
				{
					initialDest[i * 4 + j] = (byte)random.Next(256);
				}
			}

			// Single-threaded reference result.
			byte[] referenceRgba = BlendAllRgba(initialDest, sourceColors);
			byte[] referenceGray = BlendAllGray(initialDest, sourceColors);

			// All threads construct blenders and blend immediately; with the old lazy
			// constructor fill a thread could observe a partially built table.
			var barrier = new Barrier(threadCount);
			byte[][] rgbaResults = new byte[threadCount][];
			byte[][] grayResults = new byte[threadCount][];
			var threads = new Thread[threadCount];
			for (int t = 0; t < threadCount; t++)
			{
				int threadIndex = t;
				threads[t] = new Thread(() =>
				{
					barrier.SignalAndWait();
					rgbaResults[threadIndex] = BlendAllRgba(initialDest, sourceColors);
					grayResults[threadIndex] = BlendAllGray(initialDest, sourceColors);
				});
				threads[t].Start();
			}

			foreach (var thread in threads)
			{
				thread.Join();
			}

			for (int t = 0; t < threadCount; t++)
			{
				await Assert.That(rgbaResults[t]).IsEquivalentTo(referenceRgba);
				await Assert.That(grayResults[t]).IsEquivalentTo(referenceGray);
			}
		}

		private static byte[] BlendAllRgba(byte[] initialDest, Color[] sourceColors)
		{
			byte[] dest = (byte[])initialDest.Clone();
			var blender = new BlenderPreMultBGRA();
			var blenderBgr = new BlenderPreMultBGR();
			for (int i = 0; i < sourceColors.Length; i++)
			{
				blender.BlendPixel(dest, i * 4, sourceColors[i]);
			}

			// Also exercise the BGR variant's table on the RGB channels.
			for (int i = 0; i < sourceColors.Length; i++)
			{
				blenderBgr.BlendPixel(dest, i * 4, sourceColors[i]);
			}

			return dest;
		}

		private static byte[] BlendAllGray(byte[] initialDest, Color[] sourceColors)
		{
			byte[] dest = (byte[])initialDest.Clone();
			var gray = new blender_gray(1);
			var grayFromRed = new blenderGrayFromRed(1);
			var grayClamped = new blenderGrayClampedMax(1);
			for (int i = 0; i < sourceColors.Length; i++)
			{
				gray.BlendPixel(dest, i, sourceColors[i]);
				grayFromRed.BlendPixel(dest, i + 1, sourceColors[i]);
				grayClamped.BlendPixel(dest, i + 2, sourceColors[i]);
			}

			return dest;
		}
	}
}
