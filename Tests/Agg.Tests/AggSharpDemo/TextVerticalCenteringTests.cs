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
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	/// <summary>
	/// A label centred in a row - a button's "Click me!", the sidebar's "About", a gallery label - sits with
	/// as much room above its letters as below them, as agg-gui centres a run on its ascent and descent.
	/// Nunito, the demo's face, has a tall ascent (1.011 em) and deep descent (0.353 em); a text box that
	/// started at the descent and was one em tall put the box's centre about 0.2 em under the letters' centre,
	/// so every centred label rode high.
	/// </summary>
	public class TextVerticalCenteringTests
	{
		private const int RowHeight = 28;

		/// <summary>The rows of ink-free pixels above and below the text drawn centred in a row. The texts
		/// measured have no descenders, so their ink runs from the baseline to the cap height.</summary>
		private static (int Above, int Below) InkGaps(TypeFace typeFace, string text)
		{
			var row = new GuiWidget(200, RowHeight) { DoubleBuffer = false };
			row.AddChild(new TextWidget(text, pointSize: DemoText.Points(DemoText.BodyPixels), textColor: Color.Black, typeFace: typeFace)
			{
				HAnchor = HAnchor.Center,
				VAnchor = VAnchor.Center,
			});
			row.PerformLayout();

			var image = new ImageBuffer((int)row.Width, (int)row.Height);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);
			row.OnDraw(graphics);

			int inkBottom = -1;
			int inkTop = -1;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					if (image.GetPixel(x, y).red < 128)
					{
						inkBottom = inkBottom < 0 ? y : inkBottom;
						inkTop = y;
						break;
					}
				}
			}

			return (image.Height - 1 - inkTop, inkBottom);
		}

		[Test]
		[Arguments("Click me!")]
		[Arguments("About")]
		[Arguments("Label")]
		public async Task NunitoLabelsSitCentredInTheirRow(string text)
		{
			(int above, int below) = InkGaps(DemoText.Nunito, text);
			await Assert.That(below).IsGreaterThan(0);
			await Assert.That(above - below).IsBetween(-1, 1);
		}

		/// <summary>An underline sits at the descent's depth, under the descenders, as agg-gui's hyperlink draws
		/// it a descent below the baseline - not at the line box's bottom, which on Nunito crosses the g and y.</summary>
		[Test]
		public async Task NunitoUnderlineSitsUnderTheDescenders()
		{
			var text = new TextWidget("agg-sharp gallery", pointSize: DemoText.Points(DemoText.BodyPixels), textColor: Color.Black, typeFace: DemoText.Nunito)
			{
				Underline = true,
			};
			RectangleDouble bounds = text.LocalBounds;
			var image = new ImageBuffer((int)Math.Ceiling(bounds.Width), (int)Math.Ceiling(bounds.Height) + 8);
			Graphics2D graphics = image.NewGraphics2D();
			graphics.Clear(Color.White);

			// Four spare rows under the box, so an underline below it still lands in the image
			const int Spare = 4;
			graphics.SetTransform(Transform.Affine.NewTranslation(-bounds.Left, -bounds.Bottom + Spare));
			text.OnDraw(graphics);

			int underlineRow = -1;
			for (int y = 0; y < image.Height && underlineRow < 0; y++)
			{
				int inked = 0;
				for (int x = 0; x < image.Width; x++)
				{
					inked += image.GetPixel(x, y).red < 160 ? 1 : 0;
				}

				underlineRow = inked > image.Width * 9 / 10 ? y : -1;
			}

			await Assert.That(underlineRow).IsGreaterThanOrEqualTo(0);
			double belowBaseline = underlineRow + 0.5 - (-bounds.Bottom + Spare);
			await Assert.That(belowBaseline).IsEqualTo(text.Printer.TypeFaceStyle.DescentInPixels).Within(1.5);
		}
	}
}
