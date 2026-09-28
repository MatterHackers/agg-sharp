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
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// C++ pixfmt's solid blends (<c>blend_solid_hspan</c>, <c>blend_hline</c>, <c>blend_vline</c>, <c>blend_pixel</c>) hand
	/// the cover to the blender with the color. A premultiplied blender scales the color by the cover as well as the
	/// alpha; folding the cover into the alpha alone, as ImageBuffer used to, added the whole color to a partly
	/// covered pixel, so premultiplied anti-aliased edges came out too light, up to white. The color is (102, 77, 26),
	/// C++ rasterizers2's line color, at cover 128 over (200, 200, 200).
	/// </summary>
	public class ImageBufferSolidBlendTests
	{
		private static readonly Color LineColor = new Color(102, 77, 26, 255);

		private static readonly Color Background = new Color(200, 200, 200, 255);

		/// <summary>C++ blender_rgb_pre: prelerp(200, multiply(c, 128), multiply(255, 128)).</summary>
		private static readonly Color PremultipliedResult = new Color(151, 139, 113, 255);

		/// <summary>C++ blender_rgba: lerp(200, c, multiply(255, 128)) - unchanged by passing the cover through.</summary>
		private static readonly Color StraightResult = new Color(151, 138, 113, 255);

		[Test]
		public async Task PremultipliedSolidHspanScalesColorByCover()
		{
			ImageBuffer image = NewImage(new BlenderPreMultBGR(), 24);
			image.blend_solid_hspan(0, 0, 1, LineColor, new byte[] { 128 }, 0);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(PremultipliedResult);
		}

		[Test]
		public async Task PremultipliedSolidVspanScalesColorByCover()
		{
			ImageBuffer image = NewImage(new BlenderPreMultBGR(), 24);
			image.blend_solid_vspan(0, 0, 1, LineColor, new byte[] { 128 }, 0);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(PremultipliedResult);
		}

		[Test]
		public async Task PremultipliedHlineAndVlineScaleColorByCover()
		{
			ImageBuffer image = NewImage(new BlenderPreMultBGR(), 24);
			image.blend_hline(0, 0, 1, LineColor, 128);
			image.blend_vline(0, 1, 1, LineColor, 128);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(PremultipliedResult);
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(PremultipliedResult);
			await Assert.That(image.GetPixel(0, 1)).IsEqualTo(PremultipliedResult);
			await Assert.That(image.GetPixel(1, 1)).IsEqualTo(Background);
		}

		[Test]
		public async Task BlendPixelBlendsAtCover()
		{
			ImageBuffer image = NewImage(new BlenderPreMultBGR(), 24);
			image.BlendPixel(1, 1, LineColor, 128);
			await Assert.That(image.GetPixel(1, 1)).IsEqualTo(PremultipliedResult);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(Background);

			// Opaque at full cover is a copy.
			image.BlendPixel(0, 0, LineColor, 255);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(LineColor);
		}

		[Test]
		public async Task StraightBlenderIsUnchanged()
		{
			ImageBuffer image = NewImage(new BlenderBGRA(), 32);
			image.blend_solid_hspan(0, 0, 1, LineColor, new byte[] { 128 }, 0);
			image.blend_hline(1, 0, 1, LineColor, 128);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(StraightResult);
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(StraightResult);
		}

		[Test]
		public async Task StraightBlenderVerticalBlendsAreUnchanged()
		{
			ImageBuffer image = NewImage(new BlenderBGRA(), 32);
			image.blend_solid_vspan(0, 0, 2, LineColor, new byte[] { 128, 128 }, 0);
			image.blend_vline(1, 0, 1, LineColor, 128);
			for (int y = 0; y < 2; y++)
			{
				await Assert.That(image.GetPixel(0, y)).IsEqualTo(StraightResult);
				await Assert.That(image.GetPixel(1, y)).IsEqualTo(StraightResult);
			}
		}

		/// <summary>
		/// Blenders that fold a partial cover into alpha themselves must round it as the solid path did before it
		/// handed them the cover (rgba8 multiply: 100 * 100 / 255 = 39, where (a * cover + 255) &gt;&gt; 8 gives 40):
		/// alpha 100 at cover 100 draws exactly as alpha 39 at full cover.
		/// </summary>
		[Test]
		[Arguments("BlenderBGRAExactCopy")]
		[Arguments("BlenderBGRAHalfHalf")]
		[Arguments("blenderGrayFromRed")]
		[Arguments("blenderGrayClampedMax")]
		public async Task PartialCoverFoldsIntoAlphaRounded(string blenderName)
		{
			var color = new Color(0, 60, 255, 100);
			ImageBuffer covered = NewImage(NewBlender(blenderName), BitsFor(blenderName));
			covered.blend_solid_hspan(0, 0, 1, color, new byte[] { 100 }, 0);
			ImageBuffer folded = NewImage(NewBlender(blenderName), BitsFor(blenderName));
			folded.blend_solid_hspan(0, 0, 1, new Color(0, 60, 255, 39), new byte[] { 255 }, 0);

			await Assert.That(covered.GetBuffer()).IsEquivalentTo(folded.GetBuffer(), CollectionOrdering.Matching);
		}

		/// <summary>
		/// A comp-op blender composites every pixel through its operator, as C++ pixfmt_custom_blend_rgba's blend_pix
		/// does: an opaque color at full cover is not simply copied, nor a transparent one skipped.
		/// </summary>
		[Test]
		public async Task CompOpMultiplyBlendsAnOpaqueColorAtFullCover()
		{
			await AssertCompOp(CompOp.Multiply, LineColor, 255);
		}

		[Test]
		public async Task CompOpSrcWithATransparentColorClears()
		{
			ImageBuffer image = NewImage(new BlenderCompOpBGRA(CompOp.Src), 32);
			image.blend_hline(0, 0, 1, new Color(10, 20, 30, 0), 255);
			await Assert.That(image.GetPixel(0, 0)).IsEqualTo(new Color(0, 0, 0, 0));
			await Assert.That(image.GetPixel(1, 0)).IsEqualTo(new Color(0, 0, 0, 0));
		}

		[Test]
		public async Task CompOpSrcAtPartialCoverBlends()
		{
			await AssertCompOp(CompOp.Src, LineColor, 128);
		}

		private static async Task AssertCompOp(CompOp op, Color color, byte cover)
		{
			ImageBuffer image = NewImage(new BlenderCompOpBGRA(op), 32);
			image.blend_solid_hspan(0, 0, 1, color, new[] { cover }, 0);
			image.blend_hline(1, 0, 1, color, cover);
			image.blend_vline(0, 1, 1, color, cover);
			image.BlendPixel(1, 1, color, cover);

			// The operator itself, applied to one pixel of the same background.
			ImageBuffer expected = NewImage(new BlenderCompOpBGRA(op), 32);
			BlenderCompOpBGRA.BlendPix(op, expected.GetBuffer(), expected.GetBufferOffsetXY(0, 0), color, cover);
			Color expectedPixel = expected.GetPixel(0, 0);
			await Assert.That(expectedPixel).IsNotEqualTo(color);
			for (int y = 0; y < 2; y++)
			{
				for (int x = 0; x < 2; x++)
				{
					await Assert.That(image.GetPixel(x, y)).IsEqualTo(expectedPixel);
				}
			}
		}

		private static IRecieveBlenderByte NewBlender(string name)
		{
			switch (name)
			{
				case "BlenderBGRAExactCopy": return new BlenderBGRAExactCopy();
				case "BlenderBGRAHalfHalf": return new BlenderBGRAHalfHalf();
				case "blenderGrayFromRed": return new blenderGrayFromRed(1);
				default: return new blenderGrayClampedMax(1);
			}
		}

		private static int BitsFor(string name) => name.StartsWith("blenderGray") ? 8 : 32;

		private static ImageBuffer NewImage(IRecieveBlenderByte blender, int bitsPerPixel)
		{
			var image = new ImageBuffer(2, 2, bitsPerPixel, blender);
			for (int y = 0; y < 2; y++)
			{
				image.copy_hline(0, y, 2, Background);
			}

			return image;
		}
	}
}
