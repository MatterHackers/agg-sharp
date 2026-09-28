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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class RenderingTestWindowTests
	{
		private static RenderingTestWindow Build()
		{
			var spec = GuiDemoSpecs.All.First(s => s.Title == "Rendering Test");
			var window = (RenderingTestWindow)GuiDemoSpecs.CreateContent(spec, new DemoTheme());
			var host = new GuiWidget(spec.DefaultWidth, spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return window;
		}

		[Test]
		public async Task BuildsEveryTestAndDraws()
		{
			var window = Build();

			await Assert.That(window.FindDescendant("Rendering Test Scroll")).IsSameReferenceAs(window.ScrollArea);
			await Assert.That(window.FindDescendant("Rendering Test Pixel Lines")).IsSameReferenceAs(window.PixelLines);
			await Assert.That(window.FindDescendant("Rendering Test Bitmap Lines")).IsSameReferenceAs(window.BitmapLines);
			await Assert.That(window.FindDescendant("Rendering Test Colors")).IsSameReferenceAs(window.Colors);
			await Assert.That(window.FindDescendant("Rendering Test Squares")).IsNotNull();
			await Assert.That(window.FindDescendant("Rendering Test Strokes")).IsNotNull();
			await Assert.That(window.FindDescendant("Rendering Test Blending")).IsSameReferenceAs(window.Blending);

			// The tests sit inside the column's padding.
			await Assert.That(window.PixelLines.TransformToScreenSpace(window.PixelLines.LocalBounds).Left).IsGreaterThanOrEqualTo(10);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			var blending = new ImageBuffer((int)window.Blending.Width, (int)window.Blending.Height);
			window.Blending.OnDraw(blending.NewGraphics2D());
		}

		[Test]
		public async Task StripesAreExactPixelsDirectAndThroughTheBitmap()
		{
			var window = Build();
			int w = (int)window.PixelLines.Width;
			int h = (int)window.PixelLines.Height;

			ImageBuffer Draw(GuiWidget lines, double offset)
			{
				var image = new ImageBuffer(w + 2, h + 2);
				Graphics2D graphics2D = image.NewGraphics2D();

				// A fractional placement must still land every stripe on whole pixels.
				graphics2D.SetTransform(Affine.NewTranslation(offset, offset));
				lines.OnDraw(graphics2D);
				return image;
			}

			ImageBuffer direct = Draw(window.PixelLines, .3);
			ImageBuffer bitmap = Draw(window.BitmapLines, .3);

			await Assert.That(direct.GetPixel(0, 10)).IsEqualTo(Color.White);
			await Assert.That(direct.GetPixel(1, 10)).IsEqualTo(Color.Black);
			int rowsX = RenderingTestPixelLines.Count + 20;
			await Assert.That(direct.GetPixel(rowsX, 0)).IsEqualTo(Color.White);
			await Assert.That(direct.GetPixel(rowsX, 1)).IsEqualTo(Color.Black);

			for (int y = 0; y < h; y += 7)
			{
				for (int x = 0; x < w; x += 5)
				{
					await Assert.That(bitmap.GetPixel(x, y)).IsEqualTo(direct.GetPixel(x, y));
				}
			}
		}

		[Test]
		public async Task TheWheelScrollsTheTests()
		{
			var window = Build();
			var scroll = window.ScrollArea;
			double before = scroll.ScrollPositionFromTop.Y;

			scroll.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, scroll.Width / 2, scroll.Height / 2, -120));

			await Assert.That(scroll.ScrollPositionFromTop.Y).IsNotEqualTo(before);
			await Assert.That(RenderingTestColors.Lerp(new ColorF(0, 0, 0, 0), new ColorF(1, 1, 1, 1), .25)).IsEqualTo(new ColorF(.25, .25, .25, .25));
		}
	}
}
