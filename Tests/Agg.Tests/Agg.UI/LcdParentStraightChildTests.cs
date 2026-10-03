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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A widget's RGBA backbuffer is labelled <see cref="BlenderPreMultBGRA"/> but holds straight colour
	/// (ImageGraphics2D.StraightOverDestination), so an LCD-coverage parent reads it straight, as every other
	/// consumer does. Read as premultiplied, a translucent child pixel added its whole colour on top of the
	/// parent: white at alpha 0.3 over grey 40 came out 255 instead of 0.3 * 255 + 0.7 * 40 = 105.
	/// </summary>
	public class LcdParentStraightChildTests
	{
		private static readonly Color Dark = new Color(40, 40, 40);

		/// <summary>The image draw an LCD parent makes of a rounded child's buffer: a translucent corner pixel.</summary>
		[Test]
		public async Task AStraightHeldBackbufferPixelBlendsStraightOverAnLcdBuffer()
		{
			var lcd = new LcdBuffer(4, 4);
			var graphics = new LcdBufferGraphics2D(lcd);
			graphics.Clear(Dark);

			var child = new ImageBuffer(4, 4, 32, new BlenderPreMultBGRA());
			child.SetPixel(1, 1, new Color(255, 255, 255, 77));
			graphics.Render(child, 0, 0);

			var surface = new ImageBuffer(4, 4);
			lcd.CompositeOnto(surface, 0, 0);
			int expected = (int)Math.Round((77 / 255.0 * 255) + ((1 - (77 / 255.0)) * 40));
			await Assert.That(Math.Abs(surface.GetPixel(1, 1).red - expected)).IsLessThanOrEqualTo(1)
				.Because($"straight-over is {expected}, was {surface.GetPixel(1, 1).red}");
			await Assert.That(surface.GetPixel(0, 0)).IsEqualTo(Dark);
		}

		/// <summary>
		/// End to end: a faded child (always RGBA) with a translucent background inside a double-buffered
		/// parent that resolves LCD coverage. Half-white at opacity 0.5 over grey 40 lands at alpha 0.25: 94
		/// straight-over; the premultiplied-faded copy read as premultiplied gave 158.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AFadedTranslucentChildInsideAnLcdParentCompositesStraightOver()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				var parent = new GuiWidget(40, 20) { BackgroundColor = Dark, DoubleBuffer = true };
				var child = new GuiWidget(20, 10)
				{
					BackgroundColor = new Color(255, 255, 255, 128),
					DoubleBuffer = true,
					BackbufferOpacity = .5,
				};
				parent.AddChild(child);
				var root = new GuiWidget(40, 20);
				root.AddChild(parent);

				var surface = new ImageBuffer(40, 20);
				surface.NewGraphics2D().Clear(Color.White);
				await Assert.That(parent.ResolveBackbufferMode(surface.NewGraphics2D())).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("the parent has to be the LCD destination for this to test it");
				root.OnDraw(surface.NewGraphics2D());
				await Assert.That(surface.GetPixel(30, 15)).IsEqualTo(Dark)
					.Because("the parent's own background is drawn beside the child");

				double alpha = (128 / 255.0) * .5;
				int expected = (int)Math.Round((alpha * 255) + ((1 - alpha) * 40));
				int actual = surface.GetPixel(5, 5).red;
				await Assert.That(Math.Abs(actual - expected)).IsLessThanOrEqualTo(2)
					.Because($"straight-over is {expected}, was {actual}");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}
	}
}
