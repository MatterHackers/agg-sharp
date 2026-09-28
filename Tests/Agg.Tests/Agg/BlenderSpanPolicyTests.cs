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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Every byte blender's span policy (which colors it skips, copies or blends, and how a cover scales the
	/// alpha) is its own BlendPixels; a vertical span must apply it exactly as a horizontal one does, and neither
	/// may write the covers into the caller's colors.
	/// </summary>
	public class BlenderSpanPolicyTests
	{
		private const int Length = 6;

		public static IEnumerable<string> BlenderNames()
		{
			yield return nameof(BlenderBGRA);
			yield return nameof(BlenderRGBA);
			yield return nameof(BlenderPreMultBGRA);
			yield return nameof(BlenderBGRAHalfHalf);
			yield return nameof(BlenderBGRAExactCopy);
			yield return nameof(BlenderGammaBGRA);
			yield return nameof(BlenderPolyColorPreMultBGRA);
			yield return nameof(BlenderCompOpBGRA);
			yield return nameof(BlenderBGR);
			yield return nameof(BlenderGammaBGR);
			yield return nameof(BlenderPreMultBGR);
			yield return nameof(blender_gray);
			yield return nameof(blenderGrayFromRed);
			yield return nameof(blenderGrayClampedMax);
			yield return nameof(BlenderGrayExact);
		}

		/// <summary>
		/// A 1 x n image and an n x 1 image lay their pixels out identically, so the same colors and covers as a
		/// vspan into one and an hspan into the other must leave the same bytes - transparent colors included.
		/// </summary>
		[Test]
		[MethodDataSource(nameof(BlenderNames))]
		public async Task VspanBlendsAsHspanDoes(string blenderName)
		{
			foreach ((byte[] covers, bool firstCoverForAll) in CoverCases())
			{
				(ImageBuffer row, int bitDepth) = NewImage(blenderName, Length, 1);
				(ImageBuffer column, _) = NewImage(blenderName, 1, Length);

				row.blend_color_hspan(0, 0, Length, Colors(), 0, covers, 0, firstCoverForAll);
				column.blend_color_vspan(0, 0, Length, Colors(), 0, covers, 0, firstCoverForAll);

				await Assert.That(column.GetBuffer().SequenceEqual(row.GetBuffer())).IsTrue()
					.Because($"{blenderName} ({bitDepth} bit), covers [{string.Join(",", covers)}] firstCoverForAll {firstCoverForAll}");
			}
		}

		/// <summary>A span blend with a partial cover leaves the caller's color array as it was.</summary>
		[Test]
		[MethodDataSource(nameof(BlenderNames))]
		public async Task SpanBlendLeavesTheCallersColors(string blenderName)
		{
			foreach ((byte[] covers, bool firstCoverForAll) in CoverCases())
			{
				(ImageBuffer row, _) = NewImage(blenderName, Length, 1);
				(ImageBuffer column, _) = NewImage(blenderName, 1, Length);
				Color[] hspanColors = Colors();
				Color[] vspanColors = Colors();

				row.blend_color_hspan(0, 0, Length, hspanColors, 0, covers, 0, firstCoverForAll);
				column.blend_color_vspan(0, 0, Length, vspanColors, 0, covers, 0, firstCoverForAll);

				await Assert.That(hspanColors.SequenceEqual(Colors())).IsTrue().Because($"{blenderName} hspan");
				await Assert.That(vspanColors.SequenceEqual(Colors())).IsTrue().Because($"{blenderName} vspan");
			}
		}

		private static IEnumerable<(byte[] Covers, bool FirstCoverForAll)> CoverCases()
		{
			yield return (new byte[] { 255 }, true);
			yield return (new byte[] { 100 }, true);
			yield return (new byte[] { 255, 0, 1, 128, 255, 200 }, false);
		}

		private static Color[] Colors() => new[]
		{
			new Color(200, 10, 30, 255),
			new Color(1, 2, 3, 0),
			new Color(90, 180, 40, 128),
			new Color(250, 250, 250, 0),
			new Color(20, 60, 220, 1),
			new Color(120, 30, 70, 200),
		};

		private static (ImageBuffer Image, int BitDepth) NewImage(string blenderName, int width, int height)
		{
			(IRecieveBlenderByte blender, int bitDepth) = blenderName switch
			{
				nameof(BlenderBGRA) => ((IRecieveBlenderByte)new BlenderBGRA(), 32),
				nameof(BlenderRGBA) => (new BlenderRGBA(), 32),
				nameof(BlenderPreMultBGRA) => (new BlenderPreMultBGRA(), 32),
				nameof(BlenderBGRAHalfHalf) => (new BlenderBGRAHalfHalf(), 32),
				nameof(BlenderBGRAExactCopy) => (new BlenderBGRAExactCopy(), 32),
				nameof(BlenderGammaBGRA) => (new BlenderGammaBGRA(new GammaLookUpTable(2.0)), 32),
				nameof(BlenderPolyColorPreMultBGRA) => (new BlenderPolyColorPreMultBGRA(new Color(60, 120, 180, 150)), 32),
				nameof(BlenderCompOpBGRA) => (new BlenderCompOpBGRA(CompOp.Overlay), 32),
				nameof(BlenderBGR) => (new BlenderBGR(), 24),
				nameof(BlenderGammaBGR) => (new BlenderGammaBGR(new GammaLookUpTable(2.0)), 24),
				nameof(BlenderPreMultBGR) => (new BlenderPreMultBGR(), 24),
				nameof(blender_gray) => (new blender_gray(1), 8),
				nameof(blenderGrayFromRed) => (new blenderGrayFromRed(1), 8),
				nameof(blenderGrayClampedMax) => (new blenderGrayClampedMax(1), 8),
				nameof(BlenderGrayExact) => (new BlenderGrayExact(1), 8),
				_ => throw new ArgumentException(blenderName),
			};

			var image = new ImageBuffer(width, height, bitDepth, blender);
			byte[] buffer = image.GetBuffer();
			for (int i = 0; i < buffer.Length; i++)
			{
				buffer[i] = (byte)((i * 37) + 11);
			}

			return (image, bitDepth);
		}
	}
}
