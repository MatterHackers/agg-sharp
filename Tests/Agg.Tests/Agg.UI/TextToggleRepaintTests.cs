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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The process-wide text toggles the GUI demo's System window exposes (LCD subpixel text, baseline snapping)
	/// reach text the way the demo draws it: double-buffered labels, inside rounded, buffered windows.
	/// </summary>
	/// <remarks>Both settings are process wide, so these are <c>[NotInParallel]</c> and restore them.</remarks>
	[NotInParallel]
	public class TextToggleRepaintTests
	{
		private static ImageBuffer Draw(GuiWidget root)
		{
			var image = new ImageBuffer((int)root.Width, (int)root.Height);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			root.OnDraw(graphics2D);
			return image;
		}

		private static GuiWidget LabelAtFractionalHeight()
		{
			var root = new GuiWidget(120, 40);
			// A fractional Y puts the baseline between pixels, so snapping it moves the glyphs.
			root.AddChild(new TextWidget("Hxg", pointSize: 12) { Position = new Vector2(6, 8.4) });
			return root;
		}

		/// <summary>
		/// A label rastered once kept its cached pixels when baseline snapping was turned off, because nothing
		/// told its backbuffer the setting had changed - so snapping looked permanently on.
		/// </summary>
		[Test]
		public async Task TurningBaselineSnappingOffRepaintsACachedLabel()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				GuiWidget root = LabelAtFractionalHeight();
				byte[] snapped = Draw(root).GetBuffer().ToArray();

				TypeFacePrinter.SnapBaselinesToWholePixels = false;
				byte[] afterToggle = Draw(root).GetBuffer().ToArray();
				byte[] freshUnsnapped = Draw(LabelAtFractionalHeight()).GetBuffer().ToArray();

				await Assert.That(freshUnsnapped.SequenceEqual(snapped)).IsFalse()
					.Because("the fixture has to place the baseline where snapping moves it");
				await Assert.That(afterToggle.SequenceEqual(freshUnsnapped)).IsTrue()
					.Because("the label already on screen has to repaint unsnapped, as a new one paints");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		/// <summary>
		/// Every demo window is rounded and buffered, and a rounded window's panel was a transparent compositing
		/// layer, which refuses subpixel chroma - so turning LCD text on left every label in every window grey.
		/// The panel paints the window's opaque body into its own buffer, as agg-gui's window layer does, and
		/// then its text can be subpixel.
		/// </summary>
		[Test]
		public async Task LcdTextInsideARoundedWindowHasSubpixelColour()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			double wasScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				LcdRenderSettings.Enabled = false;
				var root = new GuiWidget(300, 200);
				var client = new GuiWidget(200, 100) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
				var label = new TextWidget("Hello World", pointSize: 12, textColor: Color.Black) { Position = new Vector2(10, 40) };
				client.AddChild(label);
				var window = new WindowWidget(new ThemeConfig(), client)
				{
					BackgroundColor = Color.White,
					CornerRadius = 8,
					DoubleBuffer = true,
				};
				window.Position = new Vector2(20, 20);
				root.AddChild(window);
				root.PerformLayout();

				RectangleDouble text = label.TransformToScreenSpace(label.LocalBounds);
				await Assert.That(HasChroma(Draw(root), text)).IsFalse().Because("greyscale text has none");

				LcdRenderSettings.Enabled = true;
				await Assert.That(HasChroma(Draw(root), text)).IsTrue()
					.Because("black LCD text on white has coloured fringes where its stems cut through a pixel");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
				GuiWidget.DeviceScale = wasScale;
			}
		}

		/// <summary>
		/// The demo's windows scroll their content, so the case that matters is a label inside a scrolled
		/// container inside the rounded window.
		/// </summary>
		[Test]
		public async Task LcdTextInAScrolledContainerInsideARoundedWindowHasSubpixelColour()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			double wasScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				LcdRenderSettings.Enabled = false;
				var root = new GuiWidget(300, 200);
				var scroll = new ScrollableWidget(autoScroll: true) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
				var column = new FlowLayoutWidget(FlowDirection.TopToBottom) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Fit };
				TextWidget label = null;
				for (int i = 0; i < 12; i++)
				{
					var line = new TextWidget("Hello World " + i, pointSize: 12, textColor: Color.Black);
					label ??= line;
					column.AddChild(line);
				}

				// As SystemFontTab does: the scroll area only stretches its content when asked to.
				scroll.ScrollArea.HAnchor = HAnchor.Stretch;
				scroll.AddChild(column);
				var client = new GuiWidget(200, 100) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
				client.AddChild(scroll);
				var window = new WindowWidget(new ThemeConfig(), client)
				{
					BackgroundColor = Color.White,
					CornerRadius = 8,
					DoubleBuffer = true,
				};
				window.Position = new Vector2(20, 20);
				root.AddChild(window);
				root.PerformLayout();

				RectangleDouble text = label.TransformToScreenSpace(label.LocalBounds);
				await Assert.That(text.Height > 0 && text.Bottom >= 0 && text.Top <= root.Height).IsTrue()
					.Because($"the label has to be on screen, it was at {text}");
				await Assert.That(HasChroma(Draw(root), text)).IsFalse().Because("greyscale text has none");

				LcdRenderSettings.Enabled = true;
				await Assert.That(HasChroma(Draw(root), text)).IsTrue()
					.Because("a scrolled label over the window's opaque body can be subpixel");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
				GuiWidget.DeviceScale = wasScale;
			}
		}

		private static bool HasChroma(ImageBuffer image, RectangleDouble area)
		{
			for (int y = (int)area.Bottom; y < (int)area.Top; y++)
			{
				for (int x = (int)area.Left; x < (int)area.Right; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (System.Math.Abs(pixel.Red0To255 - pixel.Blue0To255) > 24)
					{
						return true;
					}
				}
			}

			return false;
		}
	}
}
