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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Slider is one of the few widgets whose LocalBounds is derived - the union of its track, its thumb and
	/// its value readout - with a setter that does nothing. Nothing therefore writes its bounds, so nothing
	/// stamps its cached screen clipping when they change, and it would go on painting through the clip
	/// rectangle it had before its text appeared. See GuiWidget.InvalidateScreenClipping.
	/// </summary>
	public class SliderClippingTests
	{
		[Test]
		public async Task ValueReadoutIsNotClippedAwayByBoundsFromBeforeItHadText()
		{
			var root = new GuiWidget(200, 100);

			// The readout hangs below the thumb, so the slider sits high enough in the parent to leave room
			// for it and the parent cannot be what is doing the clipping.
			var slider = new Slider(new Vector2(20, 60), 160);
			root.AddChild(slider);

			// First paint with no text: the slider caches a clip rectangle that is just track and thumb.
			var beforeText = Render(root);

			// Giving the slider a format string makes the readout appear under the thumb and grows the
			// derived bounds downward. A real caller does this, or changes Value with a format already set,
			// while the slider is on screen.
			slider.Text = "value {0:0.00}";
			slider.Value = 0.5;

			var afterText = Render(root);

			// The readout's own screen rectangle is where the new pixels have to land. It is a child, so it
			// is only visible to the extent its parent's clip rectangle lets it be. Looking only at the
			// readout keeps the thumb, which moves with the value either way, out of the count.
			GuiWidget readout = slider.Children[0];
			RectangleDouble textOnScreen = readout.TransformToScreenSpace(readout.LocalBounds);

			int drawnPixels = 0;
			for (int y = (int)textOnScreen.Bottom + 1; y < (int)textOnScreen.Top; y++)
			{
				for (int x = (int)textOnScreen.Left + 1; x < (int)textOnScreen.Right; x++)
				{
					if (beforeText.GetPixel(x, y) != afterText.GetPixel(x, y))
					{
						drawnPixels++;
					}
				}
			}

			// A few glyphs at the default size cover far more than this; the floor only has to be above the
			// thumb's own handful of moved pixels, which land inside the old clip rectangle either way.
			await Assert.That(drawnPixels).IsGreaterThan(100)
				.Because("the value readout must paint after the slider's derived bounds grow to include it");
		}

		private static ImageBuffer Render(GuiWidget widget)
		{
			var image = new ImageBuffer(200, 100);

			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);

			widget.OnDraw(graphics2D);

			return image;
		}
	}
}
