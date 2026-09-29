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

using System.IO;
using System.Text;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="SvgWidget"/> must draw SVGs with the same presentation features the icon path honors
	/// (fill opacity, attributes inherited from a group) while keeping its sizing: document units times
	/// Scale, anchored top-left and cropped to width by height, not stretched to fill.
	/// </summary>
	public class SvgWidgetRenderTests
	{
		/// <summary>Loads a 100 by 100 document into an SvgWidget and draws it into a 100 by 100 image.</summary>
		private static ImageBuffer Render(string body, double scale = 1, int width = 100, int height = 100)
		{
			var svg = $"<svg xmlns=\"http://www.w3.org/2000/svg\" width=\"100\" height=\"100\" viewBox=\"0 0 100 100\">{body}</svg>";
			var widget = new SvgWidget();
			using (var stream = new MemoryStream(Encoding.UTF8.GetBytes(svg)))
			{
				widget.LoadSvg(stream, scale, width, height);
			}

			var target = new ImageBuffer(100, 100);
			widget.OnDraw(target.NewGraphics2D());
			return target;
		}

		/// <summary>The pixel at (x, y), y down from the top as SVG counts it.</summary>
		private static Color At(ImageBuffer image, int x, int y) => image.GetPixel(x, image.Height - 1 - y);

		[Test]
		public async Task FillOpacityFadesTheFill()
		{
			var image = Render("<rect width=\"100\" height=\"100\" fill=\"#000\" fill-opacity=\".32\"/>");
			await Assert.That((int)At(image, 50, 50).alpha).IsBetween(74, 90);
		}

		[Test]
		public async Task GroupStrokeIsInheritedAndFillNoneLeavesTheInsideClear()
		{
			var image = Render("<g fill=\"none\" stroke=\"#000\" stroke-width=\"10\"><rect x=\"20\" y=\"20\" width=\"60\" height=\"60\"/></g>");

			// The stroke lands on the rectangle's edge and its inside stays empty.
			await Assert.That((int)At(image, 20, 50).alpha).IsGreaterThan(200);
			await Assert.That((int)At(image, 50, 50).alpha).IsEqualTo(0);
		}

		[Test]
		public async Task DocumentUnitsScaleByScaleAnchoredTopLeft()
		{
			// Half scale into a 100 by 100 box: the document covers the top-left 50 by 50, nothing more.
			var image = Render("<rect width=\"100\" height=\"100\" fill=\"#000\"/>", scale: .5, width: 200, height: 200);

			await Assert.That((int)At(image, 10, 10).alpha).IsEqualTo(255);
			await Assert.That((int)At(image, 45, 45).alpha).IsEqualTo(255);
			await Assert.That((int)At(image, 55, 10).alpha).IsEqualTo(0);
			await Assert.That((int)At(image, 10, 55).alpha).IsEqualTo(0);
		}
	}
}
