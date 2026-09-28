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

namespace MatterHackers.Agg.Tests
{
	public class BlenderPreMultBGRTests
	{
		/// <summary>
		/// An opaque color at half cover over white blends as C++ blender_rgb_pre does: the color and alpha are
		/// scaled by the cover (mult_cover), then prelerp. The port used to add the uncovered color, so it drew an
		/// anti-aliased image edge at full strength (image_filters' circle showed it).
		/// </summary>
		[Test]
		public async Task PartialCoverScalesAnOpaqueColor()
		{
			var blender = new BlenderPreMultBGR();
			var buffer = new byte[] { 255, 255, 255 };
			var colors = new[] { new Color(200, 100, 50, 255) };
			var covers = new byte[] { 128 };

			blender.BlendPixels(buffer, 0, colors, 0, covers, 0, false, 1);

			await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(new Color(227, 177, 152, 255));
		}

		/// <summary>The same with one cover for the whole span, which must reach every pixel.</summary>
		[Test]
		public async Task PartialFirstCoverForAllScalesAnOpaqueColor()
		{
			var blender = new BlenderPreMultBGR();
			var buffer = new byte[] { 255, 255, 255, 255, 255, 255 };
			var colors = new[] { new Color(200, 100, 50, 255), new Color(200, 100, 50, 255) };
			var covers = new byte[] { 128 };

			blender.BlendPixels(buffer, 0, colors, 0, covers, 0, true, 2);

			await Assert.That(blender.PixelToColor(buffer, 3)).IsEqualTo(new Color(227, 177, 152, 255));
		}

		/// <summary>A straight-alpha color saturates at 255 rather than wrapping to a dark speck.</summary>
		[Test]
		public async Task StraightAlphaColorSaturatesInsteadOfWrapping()
		{
			var blender = new BlenderPreMultBGR();
			var buffer = new byte[] { 255, 255, 255 };

			blender.BlendPixel(buffer, 0, new Color(255, 0, 0, 128));

			await Assert.That(blender.PixelToColor(buffer, 0)).IsEqualTo(new Color(255, 127, 127, 255));
		}
	}
}
