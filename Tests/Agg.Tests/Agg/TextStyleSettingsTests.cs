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
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="TextStyleSettings"/>: each setting reshapes glyphs or advances as agg-gui's does on a face
	/// that opts in (<see cref="StyledTypeFace.ApplyTextStyleSettings"/>), a face that does not is untouched, a change
	/// reaches the caches that hold text built under the old style, and going back to the defaults gives back exactly
	/// the text drawn before.
	/// </summary>
	/// <remarks>
	/// The settings are process-wide, so every test is a keyless <c>[NotInParallel]</c> (no other text test can
	/// run while one holds a non-default style) and resets them in a finally block.
	/// </remarks>
	public class TextStyleSettingsTests
	{
		[Test]
		[NotInParallel]
		public async Task ReturningToTheDefaultsDrawsTheSameBytes()
		{
			try
			{
				byte[] before = Render("Hxgy").GetBuffer();
				TextStyleSettings.Width = 1.2;
				TextStyleSettings.Interval = .1;
				TextStyleSettings.FauxWeight = .5;
				TextStyleSettings.FauxItalic = .5;
				byte[] styled = Render("Hxgy").GetBuffer();
				TextStyleSettings.Reset();
				byte[] after = Render("Hxgy").GetBuffer();

				await Assert.That(styled.AsSpan().SequenceEqual(before)).IsFalse();
				await Assert.That(after.AsSpan().SequenceEqual(before)).IsTrue();
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		/// <summary>
		/// Only a face that opts in follows the settings, so CAD text geometry, SVG text and anything else drawn
		/// through a plain face stays exact whatever the System window says.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AFaceThatDoesNotOptInIsUnaffected()
		{
			try
			{
				var plain = new StyledTypeFace(LiberationSansFont.Instance, 12);
				var printer = new TypeFacePrinter("Hxgy", plain);
				byte[] before = Render(printer).GetBuffer();
				double advance = plain.GetAdvanceForCodePoint('H');
				RectangleDouble bounds = plain.GetGlyphForCodePoint('H').GetBounds();
				object identity = printer.RenderIdentity;

				TextStyleSettings.Width = 1.2;
				TextStyleSettings.Interval = .1;
				TextStyleSettings.FauxWeight = .5;
				TextStyleSettings.FauxItalic = .5;

				await Assert.That(Render(printer).GetBuffer().AsSpan().SequenceEqual(before)).IsTrue();
				await Assert.That(plain.GetAdvanceForCodePoint('H')).IsEqualTo(advance);
				await Assert.That(plain.GetGlyphForCodePoint('H').GetBounds()).IsEqualTo(bounds);
				await Assert.That(printer.RenderIdentity).IsEqualTo(identity);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		/// <summary>agg-gui's Width scales the outline only; the pen walk keeps the font's advances.</summary>
		[Test]
		[NotInParallel]
		public async Task WidthScalesOutlinesNotAdvancesAndIntervalAddsAFractionOfTheEm()
		{
			try
			{
				var style = new StyledTypeFace(LiberationSansFont.Instance, 12) { ApplyTextStyleSettings = true };
				double native = style.GetAdvanceForCodePoint('H');
				RectangleDouble nativeBounds = style.GetGlyphForCodePoint('H').GetBounds();

				TextStyleSettings.Width = 1.2;
				await Assert.That(style.GetAdvanceForCodePoint('H')).IsEqualTo(native);
				RectangleDouble wideBounds = style.GetGlyphForCodePoint('H').GetBounds();
				await Assert.That(wideBounds.Right).IsEqualTo(nativeBounds.Right * 1.2).Within(1e-6);

				TextStyleSettings.Width = 1;
				TextStyleSettings.Interval = .1;
				await Assert.That(style.GetAdvanceForCodePoint('H')).IsEqualTo(native + (.1 * style.EmSizeInPixels)).Within(1e-9);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		/// <summary>A printer measured before a change must not answer from its size cache afterwards.</summary>
		[Test]
		[NotInParallel]
		public async Task APrinterRemeasuresAfterAChange()
		{
			try
			{
				var printer = new TypeFacePrinter("Hello", new StyledTypeFace(LiberationSansFont.Instance, 12) { ApplyTextStyleSettings = true });
				double width = printer.GetSize().X;
				printer.GetOffset(0, 4, out var offset);
				TextStyleSettings.Interval = .1;

				double extra = 5 * .1 * printer.TypeFaceStyle.EmSizeInPixels;
				await Assert.That(printer.GetSize().X).IsEqualTo(width + extra).Within(1e-9);
				printer.GetOffset(0, 4, out var styledOffset);
				await Assert.That(styledOffset.X).IsEqualTo(offset.X + extra).Within(1e-9);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		[Test]
		[NotInParallel]
		public async Task FauxWeightThickensAndThinsTheInk()
		{
			try
			{
				long regular = Ink(Render("Hmo"));
				TextStyleSettings.FauxWeight = .8;
				long heavy = Ink(Render("Hmo"));
				TextStyleSettings.FauxWeight = -.8;
				long light = Ink(Render("Hmo"));

				await Assert.That(heavy).IsGreaterThan(regular * 11 / 10);
				await Assert.That(light).IsLessThan(regular * 9 / 10);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		/// <summary>A third of Faux Italic shears the outline, so the top of an upright stem leans right by that much of its height.</summary>
		[Test]
		[NotInParallel]
		public async Task FauxItalicLeansTheTopOfAGlyphRight()
		{
			try
			{
				var style = new StyledTypeFace(LiberationSansFont.Instance, 48) { ApplyTextStyleSettings = true };
				RectangleDouble upright = style.GetGlyphForCodePoint('l').GetBounds();
				TextStyleSettings.FauxItalic = .6;
				RectangleDouble slanted = style.GetGlyphForCodePoint('l').GetBounds();

				await Assert.That(slanted.Right - upright.Right).IsEqualTo(upright.Top * .2).Within(.01);
				await Assert.That(slanted.Bottom).IsEqualTo(upright.Bottom).Within(1e-9);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		/// <summary>The caches keyed on a glyph or a run must not serve pixels drawn under the old style.</summary>
		[Test]
		[NotInParallel]
		public async Task AChangeReachesTheGlyphImageCacheAndTheRunIdentity()
		{
			try
			{
				var style = new StyledTypeFace(LiberationSansFont.Instance, 12) { ApplyTextStyleSettings = true };
				ImageBuffer regularImage = style.GetImageForCodePoint('H', 0, 0, Color.Black);
				object regularIdentity = new TypeFacePrinter("H", style).RenderIdentity;
				long epoch = TextStyleSettings.Epoch;

				TextStyleSettings.FauxWeight = .8;

				await Assert.That(TextStyleSettings.Epoch).IsGreaterThan(epoch);
				await Assert.That(style.GetImageForCodePoint('H', 0, 0, Color.Black)).IsNotSameReferenceAs(regularImage);
				await Assert.That(new TypeFacePrinter("H", style).RenderIdentity).IsNotEqualTo(regularIdentity);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		[Test]
		[NotInParallel]
		public async Task SizeScaleSizesTextWidgetsBuiltAfterIt()
		{
			try
			{
				double regular = new TextWidget("Hello", pointSize: 12).Printer.TypeFaceStyle.EmSizeInPoints;
				TextStyleSettings.SizeScale = 1.5;
				var scaled = new TextWidget("Hello", pointSize: 12);

				await Assert.That(scaled.Printer.TypeFaceStyle.EmSizeInPoints).IsEqualTo(regular * 1.5).Within(1e-9);
				await Assert.That(scaled.PointSize).IsEqualTo(12).Within(1e-9);
				await Assert.That(scaled.Printer.TypeFaceStyle.ApplyTextStyleSettings).IsTrue();
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		[Test]
		[NotInParallel]
		public async Task SettingsClampToAggGuisRanges()
		{
			try
			{
				TextStyleSettings.SizeScale = 10;
				TextStyleSettings.Width = 0;
				TextStyleSettings.Interval = 1;
				TextStyleSettings.FauxWeight = -5;
				TextStyleSettings.FauxItalic = 5;

				await Assert.That(TextStyleSettings.SizeScale).IsEqualTo(3.0);
				await Assert.That(TextStyleSettings.Width).IsEqualTo(.75);
				await Assert.That(TextStyleSettings.Interval).IsEqualTo(.2);
				await Assert.That(TextStyleSettings.FauxWeight).IsEqualTo(-1.0);
				await Assert.That(TextStyleSettings.FauxItalic).IsEqualTo(1.0);
			}
			finally
			{
				TextStyleSettings.Reset();
			}
		}

		/// <summary>The text drawn as UI text is, through a face that opts in to the settings.</summary>
		private static ImageBuffer Render(string text)
		{
			var style = new StyledTypeFace(LiberationSansFont.Instance, 18) { ApplyTextStyleSettings = true };
			return Render(new TypeFacePrinter(text, style));
		}

		private static ImageBuffer Render(TypeFacePrinter printer)
		{
			var image = new ImageBuffer(120, 40);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			printer.Origin = new Vector2(4, 12);
			printer.Render(graphics2D, Color.Black);
			return image;
		}

		/// <summary>Total darkness drawn on the white image - a coverage sum.</summary>
		private static long Ink(ImageBuffer image)
		{
			long ink = 0;
			byte[] buffer = image.GetBuffer();
			for (int i = 0; i < buffer.Length; i += 4)
			{
				ink += 255 - buffer[i];
			}

			return ink;
		}
	}
}
