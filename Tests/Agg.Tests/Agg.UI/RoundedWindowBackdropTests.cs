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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A rounded <see cref="WindowWidget"/>'s panel paints the window's body into its own buffer, as an opaque
	/// backdrop LCD text can be drawn over. These pin what that buffer has to keep right: following a recolour,
	/// anti-aliasing its corner once, and not costing every other buffer a repaint on an LCD style change.
	/// </summary>
	/// <remarks>DeviceScale and the LCD settings are process wide, so these are <c>[NotInParallel]</c>.</remarks>
	[NotInParallel]
	public class RoundedWindowBackdropTests
	{
		private static readonly Color Body = new Color(200, 220, 240);

		private static ImageBuffer Draw(GuiWidget root)
		{
			var image = new ImageBuffer((int)root.Width, (int)root.Height);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			root.OnDraw(graphics2D);
			return image;
		}

		private static (GuiWidget Root, WindowWidget Window, TextWidget Label) Build()
		{
			var root = new GuiWidget(300, 200);
			var client = new GuiWidget(200, 100) { HAnchor = HAnchor.Stretch, VAnchor = VAnchor.Stretch };
			var label = new TextWidget("Hello World", pointSize: 12, textColor: Color.Black) { Position = new Vector2(10, 40) };
			client.AddChild(label);
			var window = new WindowWidget(new ThemeConfig(), client)
			{
				BackgroundColor = Body,
				CornerRadius = 8,
				WindowBorder = 0,
				DoubleBuffer = true,
			};
			window.Position = new Vector2(20, 20);
			root.AddChild(window);
			root.PerformLayout();
			return (root, window, label);
		}

		private static bool HasChroma(ImageBuffer image, RectangleDouble area)
		{
			for (int y = (int)area.Bottom; y < (int)area.Top; y++)
			{
				for (int x = (int)area.Left; x < (int)area.Right; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (Math.Abs(pixel.Red0To255 - pixel.Blue0To255) > 24)
					{
						return true;
					}
				}
			}

			return false;
		}

		/// <summary>
		/// The body is painted inside the panel's cached buffer, which a window's own invalidate does not reach,
		/// so a recolour has to invalidate the panel - or the old body stays on screen. Going translucent also
		/// has to take the backdrop's LCD permission away again: chroma over a see-through body is wrong.
		/// </summary>
		[Test]
		public async Task RecolouringARoundedWindowRepaintsItsBodyAndReEvaluatesLcd()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			double wasScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				LcdRenderSettings.Enabled = true;
				var (root, window, label) = Build();
				RectangleDouble text = label.TransformToScreenSpace(label.LocalBounds);
				RectangleDouble client = window.ClientArea.TransformToScreenSpace(window.ClientArea.LocalBounds);
				int x = (int)client.Right - 10;
				int y = (int)client.Bottom + 10;

				ImageBuffer first = Draw(root);
				await Assert.That(first.GetPixel(x, y)).IsEqualTo(Body);
				await Assert.That(HasChroma(first, text)).IsTrue().Because("LCD text over an opaque body");

				var green = new Color(40, 200, 90);
				window.BackgroundColor = green;
				await Assert.That(Draw(root).GetPixel(x, y)).IsEqualTo(green).Because("the panel's buffer has to repaint the new body");

				window.BackgroundColor = new Color(Color.Black, 0);
				ImageBuffer translucent = Draw(root);
				await Assert.That(translucent.GetPixel(x, y)).IsEqualTo(Color.White).Because("a transparent body shows the parent");
				await Assert.That(HasChroma(translucent, text)).IsFalse().Because("no subpixel colour over a see-through body");

				var title = new Color(90, 120, 200);
				window.BackgroundColor = Body;
				Draw(root);
				window.TitleBarColor = title;
				RectangleDouble titleBar = window.TitleBar.TransformToScreenSpace(window.TitleBar.LocalBounds);
				await Assert.That(Draw(root).GetPixel((int)titleBar.Center.X, (int)titleBar.Bottom + 2)).IsEqualTo(title)
					.Because("a new title bar colour has to reach the panel's buffer too");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
				GuiWidget.DeviceScale = wasScale;
			}
		}

		/// <summary>
		/// The body's corner is anti-aliased once - by the rounded clip the panel's buffer is composited
		/// through - not by an anti-aliased fill and then the clip again, which faded the corner edge.
		/// </summary>
		/// <remarks>
		/// Run on a premultiplied-labelled destination (a parent backbuffer or snapshot) and a straight one (the
		/// software window surface). Both read the buffer straight, so a clip that faded the corner's colour as well
		/// as its alpha shows a dark rim on either; the straight one shows it at once (corner red 203 for 241).
		/// </remarks>
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task TheBodyCornerIsAntiAliasedOnce(bool premultipliedDestination)
		{
			double wasScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				var (root, window, _) = Build();
				// The buffer's clipped corner is straight (only its alpha is cut) and the composite reads it
				// straight-over, whichever blender labels the destination.
				var image = new ImageBuffer((int)root.Width, (int)root.Height, 32, premultipliedDestination ? new BlenderPreMultBGRA() : new BlenderBGRA());
				Graphics2D graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);
				root.OnDraw(graphics2D);
				RectangleDouble panel = window.ClientArea.Parent.TransformToScreenSpace(window.ClientArea.Parent.LocalBounds);

				// The clip the panel's buffer goes through (WidgetBackbuffer.Rasterize): grown half a pixel, radius too.
				RectangleDouble clip = panel;
				clip.Inflate(.5);
				int checkedPixels = 0;
				for (int y = (int)panel.Bottom; y < (int)panel.Bottom + 8; y++)
				{
					for (int x = (int)panel.Left; x < (int)panel.Left + 8; x++)
					{
						double coverage = RoundedClipCoverage.Coverage(clip, 8.5, x + .5, y + .5);
						if (coverage <= 0 || coverage >= 1)
						{
							continue;
						}

						int expected = (int)Math.Round(255 + (Body.Red0To255 - 255) * coverage);
						int actual = image.GetPixel(x, y).Red0To255;
						await Assert.That(Math.Abs(actual - expected)).IsLessThanOrEqualTo(2)
							.Because($"corner pixel ({x}, {y}) at coverage {coverage:0.00} should be {expected}, was {actual}");
						checkedPixels++;
					}
				}

				await Assert.That(checkedPixels).IsGreaterThan(3);
			}
			finally
			{
				GuiWidget.DeviceScale = wasScale;
			}
		}

		private class CountingWidget : GuiWidget
		{
			public CountingWidget()
				: base(40, 20)
			{
				this.DoubleBuffer = true;
				this.BackgroundColor = Color.Gray;
			}

			public int Paints { get; private set; }

			public override void OnDraw(Graphics2D graphics2D)
			{
				this.Paints++;
				base.OnDraw(graphics2D);
			}
		}

		/// <summary>
		/// An LCD style change (dragging the Gamma slider) only re-rasters buffers that can hold subpixel pixels;
		/// an ordinary buffer has nothing the change could alter.
		/// </summary>
		[Test]
		public async Task AnLcdStyleChangeDoesNotRepaintAnOrdinaryBuffer()
		{
			double wasGamma = LcdRenderSettings.Gamma;
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = false;
				var root = new GuiWidget(100, 50);
				var widget = new CountingWidget();
				root.AddChild(widget);
				Draw(root);
				int paints = widget.Paints;

				LcdRenderSettings.Gamma = wasGamma + .1;
				Draw(root);
				await Assert.That(widget.Paints).IsEqualTo(paints);
			}
			finally
			{
				LcdRenderSettings.Gamma = wasGamma;
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}
	}
}
