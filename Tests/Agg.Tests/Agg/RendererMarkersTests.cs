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

namespace MatterHackers.Agg.Tests.Agg
{
	public class RendererMarkersTests
	{
		/// <summary>
		/// A marker centered just left of the image still draws the part that reaches into it. C++
		/// renderer_markers::visible builds the box's right edge as x+y, not x+r, so at y 0 it culled this
		/// cross; tools/cpp-renderer/patches/agg_renderer_markers.h carries the same fix.
		/// </summary>
		[Test]
		public async Task MarkerLeftOfTheClipBoxDrawsItsVisiblePart()
		{
			var image = new ImageBuffer(10, 10);
			var markers = new RendererMarkers(new ImageClippingProxy(image)) { LineColor = Color.Black, FillColor = Color.Black };

			markers.Marker(-2, 0, 4, MarkerType.Cross);

			await Assert.That(image.GetPixel(0, 0).alpha).IsEqualTo((byte)255);
			await Assert.That(image.GetPixel(2, 0).alpha).IsEqualTo((byte)255);
			await Assert.That(image.GetPixel(3, 0).alpha).IsEqualTo((byte)0);
		}
	}
}
