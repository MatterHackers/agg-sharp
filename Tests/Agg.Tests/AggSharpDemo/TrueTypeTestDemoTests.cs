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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// truetype_test (C++ truetype_test_02_win via agg-rust): LCD subpixel text with every knob local to the demo.
	public class TrueTypeTestDemoTests
	{
		[Test]
		public async Task IsListedUnderText()
		{
			var textDemos = DemoRegistry.GroupEntries(DemoRegistry.CreateAggDemos(), "Text");

			await Assert.That(textDemos.Any(demo => demo is TrueTypeTestDemo && demo.Name == "truetype_test")).IsTrue();
		}

		/// <summary>Every face draws ink into the text area at the default settings.</summary>
		[Test]
		public async Task EveryTypefaceDrawsText()
		{
			for (int face = 0; face < TrueTypeTestDemo.TypefaceNames.Length; face++)
			{
				var demo = new TrueTypeTestDemo();
				demo.TypefaceRbox.CurrentItem = face;
				ImageBuffer frame = Render(demo);

				int inked = TextAreaPixels(frame).Count(c => c.red < 128 && c.green < 128 && c.blue < 128);
				await Assert.That(inked).IsGreaterThan(1000).Because($"{TrueTypeTestDemo.TypefaceNames[face]} drew no text");
			}
		}

		/// <summary>LCD coverage leaves coloured fringes on glyph edges; Grayscale leaves only neutral greys.</summary>
		[Test]
		public async Task LcdFringesAndGrayscaleIsNeutral()
		{
			var demo = new TrueTypeTestDemo();
			int lcdChroma = TextAreaPixels(Render(demo)).Count(IsColoured);

			demo.GrayscaleCbox.Checked = true;
			int grayChroma = TextAreaPixels(Render(demo)).Count(IsColoured);

			await Assert.That(lcdChroma).IsGreaterThan(1000);
			await Assert.That(grayChroma).IsEqualTo(0);
		}

		/// <summary>The non-default knobs all render, and change the text.</summary>
		[Test]
		public async Task StyleKnobsChangeTheText()
		{
			byte[] plain = Render(new TrueTypeTestDemo()).GetBuffer().ToArray();

			var styled = new TrueTypeTestDemo();
			styled.FauxWeightSlider.Value = 0.5;
			styled.FauxItalicSlider.Value = 0.5;
			styled.WidthSlider.Value = 0.9;
			styled.IntervalSlider.Value = 0.1;
			styled.GammaSlider.Value = 1.8;
			styled.PrimaryWeightSlider.Value = 0.5;
			styled.HintingCbox.Checked = false;
			byte[] changed = Render(styled).GetBuffer();

			await Assert.That(changed.SequenceEqual(plain)).IsFalse();
		}

		/// <summary>
		/// Positive Faux Weight is heavier and negative lighter, on these TTF faces too: their outer contours load
		/// counter-clockwise, the opposite of the SVG fonts the contour sign was first written for.
		/// </summary>
		[Test]
		public async Task FauxWeightThickensAndThinsTheInk()
		{
			long Ink(double weight)
			{
				var demo = new TrueTypeTestDemo();
				demo.FauxWeightSlider.Value = weight;
				return TextAreaPixels(Render(demo)).Sum(c => 255L - c.green);
			}

			long regular = Ink(0);
			await Assert.That(Ink(0.6)).IsGreaterThan(regular * 11 / 10);
			await Assert.That(Ink(-0.6)).IsLessThan(regular * 9 / 10);
		}

		/// <summary>Kerning moves the pen between kerned pairs, so it changes the text.</summary>
		[Test]
		public async Task KerningChangesTheText()
		{
			var demo = new TrueTypeTestDemo();
			byte[] kerned = Render(demo).GetBuffer().ToArray();
			demo.KerningCbox.Checked = false;
			byte[] unkerned = Render(demo).GetBuffer();

			await Assert.That(unkerned.SequenceEqual(kerned)).IsFalse();
		}

		/// <summary>
		/// Drawn at 2 device pixels per demo pixel (a HiDPI view), the text area is rendered at 2x, so its LCD fringes
		/// land on device pixels instead of being a 1x image scaled up.
		/// </summary>
		[Test]
		public async Task TextAreaIsRenderedAtDeviceResolution()
		{
			var demo = new TrueTypeTestDemo();
			var frame = new ImageBuffer(demo.Width * 2, demo.Height * 2);
			Graphics2D graphics = frame.NewGraphics2D();
			graphics.SetTransform(Transform.Affine.NewScaling(2));
			demo.Draw(graphics);

			await Assert.That(demo.TextAreaImage.Width).IsEqualTo(demo.Width * 2);
			await Assert.That(demo.TextAreaImage.Height).IsEqualTo((demo.Height - TrueTypeTestDemo.TextAreaBottom) * 2);

			// Read from the text area itself: the software ImageGraphics2D, unlike the GPU, does not scale an image by
			// the transform, so the frame here shows the 2x image at half size. The GPU path draws it 1:1.
			ImageBuffer textArea = demo.TextAreaImage;
			int fringed = 0;
			for (int y = 0; y < textArea.Height; y++)
			{
				for (int x = 0; x < textArea.Width; x++)
				{
					fringed += IsColoured(textArea.GetPixel(x, y)) ? 1 : 0;
				}
			}

			await Assert.That(fringed).IsGreaterThan(1000);
		}

		/// <summary>Invert blacks out the text area only, and draws the text white on it.</summary>
		[Test]
		public async Task InvertSwapsTheTextAreaBackground()
		{
			var demo = new TrueTypeTestDemo();
			ImageBuffer normal = Render(demo);
			demo.InvertCbox.Checked = true;
			ImageBuffer inverted = Render(demo);

			// Left of x 10 the text area holds no glyphs; (158, 2) is between the radio box and the sliders.
			Color marginNormal = normal.GetPixel(3, 300);
			Color marginInverted = inverted.GetPixel(3, 300);
			await Assert.That((marginNormal.red, marginNormal.green, marginNormal.blue)).IsEqualTo(((byte)255, (byte)255, (byte)255));
			await Assert.That((marginInverted.red, marginInverted.green, marginInverted.blue)).IsEqualTo(((byte)0, (byte)0, (byte)0));

			Color controlsBackground = inverted.GetPixel(158, 2);
			await Assert.That((controlsBackground.red, controlsBackground.green, controlsBackground.blue)).IsEqualTo(((byte)255, (byte)255, (byte)255));

			int whiteInk = TextAreaPixels(inverted).Count(c => c.red > 128 && c.green > 128 && c.blue > 128);
			await Assert.That(whiteInk).IsGreaterThan(1000);
		}

		private static bool IsColoured(Color c)
		{
			return Math.Max(c.red, Math.Max(c.green, c.blue)) - Math.Min(c.red, Math.Min(c.green, c.blue)) > 32;
		}

		private static Color[] TextAreaPixels(ImageBuffer frame)
		{
			var pixels = new Color[frame.Width * (frame.Height - TrueTypeTestDemo.TextAreaBottom)];
			int i = 0;
			for (int y = TrueTypeTestDemo.TextAreaBottom; y < frame.Height; y++)
			{
				for (int x = 0; x < frame.Width; x++)
				{
					pixels[i++] = frame.GetPixel(x, y);
				}
			}

			return pixels;
		}

		private static ImageBuffer Render(TrueTypeTestDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
