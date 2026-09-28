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

namespace MatterHackers.Agg.Tests
{
	public class BlenderPreMultBGRATests
	{
		/// <summary>
		/// An opaque color at half cover over white blends as C++ blender_rgba_pre does: the color and alpha are
		/// scaled by the cover (mult_cover), then prelerp. The port used to ignore a partial cover and copy the
		/// opaque color, so an image's anti-aliased edge came out hard (image_perspective showed it).
		/// </summary>
		[Test]
		public async Task PartialCoverScalesAnOpaqueColor()
		{
			var blender = new BlenderPreMultBGRA();
			var buffer = new byte[] { 255, 255, 255, 255 };
			var colors = new[] { new Color(200, 100, 50, 255) };
			var covers = new byte[] { 128 };

			blender.BlendPixels(buffer, 0, colors, 0, covers, 0, false, 1);

			await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(new Color(227, 177, 152, 255));
		}

		/// <summary>
		/// A straight-alpha color (red above its alpha) is not valid premultiplied input, but it must not wrap: red
		/// sums to 319 and saturates at 255 rather than wrapping to 63, a dark speck.
		/// </summary>
		[Test]
		public async Task StraightAlphaColorSaturatesInsteadOfWrapping()
		{
			var blender = new BlenderPreMultBGRA();
			var buffer = new byte[] { 255, 255, 255, 255 };
			var colors = new[] { new Color(255, 0, 0, 128) };
			var covers = new byte[] { 128 };

			blender.BlendPixels(buffer, 0, colors, 0, covers, 0, false, 1);

			await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(new Color(255, 191, 191, 255));
		}

		/// <summary>The same with one cover for the whole span.</summary>
		[Test]
		public async Task PartialFirstCoverForAllScalesAnOpaqueColor()
		{
			var blender = new BlenderPreMultBGRA();
			var buffer = new byte[] { 255, 255, 255, 255, 255, 255, 255, 255 };
			var colors = new[] { new Color(200, 100, 50, 255), new Color(200, 100, 50, 255) };
			var covers = new byte[] { 128 };

			blender.BlendPixels(buffer, 0, colors, 0, covers, 0, true, 2);

			await Assert.That(blender.PixelToColor(buffer, 4)).IsEqualTo(new Color(227, 177, 152, 255));
		}
		/// <summary>
		/// A non-opaque color at full cover blends by the same exact source-over as a partial cover: each channel is
		/// prelerp(p, q, a) = p + q - multiply(p, a). The full-cover path used to add the color to a truncated
		/// (p * (255 - a) + 255) >> 8, one level high: gray 9 under half-transparent black (9 * 127 / 255 = 4.48) came out 5, not 4.
		/// </summary>
		[Test]
		public async Task FullCoverOfATranslucentColorIsExactSourceOver()
		{
			var blender = new BlenderPreMultBGRA();
			var buffer = new byte[] { 9, 9, 9, 255 };

			blender.BlendPixels(buffer, 0, new[] { new Color(0, 0, 0, 128) }, 0, new byte[] { 255 }, 0, false, 1);

			await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(new Color(4, 4, 4, 255));
		}

		/// <summary>Full cover agrees with prelerp for a spread of destinations and translucent colors, on both span paths.</summary>
		[Test]
		public async Task FullCoverMatchesPrelerpForSeveralColors()
		{
			var destinations = new[] { new Color(255, 255, 255, 255), new Color(10, 200, 90, 180), new Color(0, 0, 0, 0), new Color(128, 64, 32, 255) };
			var sources = new[] { new Color(0, 0, 0, 128), new Color(100, 50, 20, 100), new Color(1, 2, 3, 3), new Color(200, 180, 160, 254), new Color(60, 120, 180, 200) };
			var blender = new BlenderPreMultBGRA();
			foreach (var destination in destinations)
			{
				foreach (var source in sources)
				{
					var expected = new Color(
						System.Math.Min(Rgba8Math.Prelerp(destination.red, source.red, source.alpha), 255),
						System.Math.Min(Rgba8Math.Prelerp(destination.green, source.green, source.alpha), 255),
						System.Math.Min(Rgba8Math.Prelerp(destination.blue, source.blue, source.alpha), 255),
						System.Math.Min(Rgba8Math.Prelerp(destination.alpha, source.alpha, source.alpha), 255));
					foreach (bool firstCoverForAll in new[] { false, true })
					{
						var buffer = new byte[4];
						blender.CopyPixels(buffer, 0, destination, 1);
						blender.BlendPixels(buffer, 0, new[] { source }, 0, new byte[] { 255 }, 0, firstCoverForAll, 1);
						await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(expected);
					}
				}
			}
		}

		/// <summary>BlenderPolyColorPreMultBGRA blends by the same exact source-over: gray 9 under half-transparent black is 4.</summary>
		[Test]
		public async Task PolyColorFullCoverIsExactSourceOver()
		{
			var blender = new BlenderPolyColorPreMultBGRA(new Color(255, 255, 255, 255));
			var buffer = new byte[] { 9, 9, 9, 255 };

			blender.BlendPixels(buffer, 0, new[] { new Color(0, 0, 0, 128) }, 0, new byte[] { 255 }, 0, false, 1);

			await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(new Color(4, 4, 4, 255));
		}
	}
}
