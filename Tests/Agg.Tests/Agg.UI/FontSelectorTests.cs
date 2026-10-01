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
using MatterHackers.Agg.Platform;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="FontSelector"/> shows every font's name in that font's own face - each row of the open list and the
	/// closed field alike - falls back to the UI font for a face that cannot draw its own name, and sizes rows to the
	/// face's full ascent and descent so a tall face is not clipped.
	/// </summary>
	public class FontSelectorTests
	{
		private static TextWidget RowText(MenuItem item) => item.Descendants<TextWidget>().First();

		private static TypeFace FaceOf(TextWidget text) => text.Printer.TypeFaceStyle.TypeFace;

		private static TextWidget ClosedLabel(FontSelector selector) => selector.Descendants<TextWidget>().First();

		[Test]
		public async Task EachRowIsDrawnInItsOwnFace()
		{
			var selector = new FontSelector("Font", Color.Black);
			MenuItem sans = selector.AddFont("Liberation Sans", LiberationSansFont.Instance);
			MenuItem bold = selector.AddFont("Liberation Sans Bold", LiberationSansBoldFont.Instance);
			MenuItem nunito = selector.AddFont("Nunito", DemoText.Nunito);

			await Assert.That(FaceOf(RowText(sans))).IsSameReferenceAs(LiberationSansFont.Instance);
			await Assert.That(FaceOf(RowText(bold))).IsSameReferenceAs(LiberationSansBoldFont.Instance);
			await Assert.That(FaceOf(RowText(nunito))).IsSameReferenceAs(DemoText.Nunito);
		}

		[Test]
		public async Task SelectingAFontRaisesTheEventAndShowsItsNameInItsFace()
		{
			var selector = new FontSelector("Font", Color.Black);
			selector.AddFont("Liberation Sans", LiberationSansFont.Instance);
			selector.AddFont("Nunito", DemoText.Nunito, "nunito-value");

			int changes = 0;
			selector.SelectionChanged += (s, e) => changes++;

			selector.SelectedIndex = 1;

			await Assert.That(changes).IsEqualTo(1);
			await Assert.That(selector.SelectedValue).IsEqualTo("nunito-value");
			await Assert.That(selector.SelectedTypeFace).IsSameReferenceAs(DemoText.Nunito);
			await Assert.That(ClosedLabel(selector).Text).IsEqualTo("Nunito");
			await Assert.That(FaceOf(ClosedLabel(selector))).IsSameReferenceAs(DemoText.Nunito);

			selector.SelectedIndex = 0;
			await Assert.That(FaceOf(ClosedLabel(selector))).IsSameReferenceAs(LiberationSansFont.Instance);
		}

		/// <summary>Font Awesome maps its icons to private use code points and has no letters: its name would draw as boxes.</summary>
		[Test]
		public async Task AFaceThatCannotDrawItsNameFallsBackToTheUiFont()
		{
			await Assert.That(FontSelector.CanDrawName(IconFont.TypeFace, "Font Awesome")).IsFalse();
			await Assert.That(FontSelector.CanDrawName(DemoText.Nunito, "Nunito")).IsTrue();

			var selector = new FontSelector("Font", Color.Black);
			MenuItem icons = selector.AddFont("Font Awesome", IconFont.TypeFace);
			selector.SelectedIndex = 0;

			await Assert.That(FaceOf(RowText(icons))).IsSameReferenceAs(AggContext.DefaultFont);
			await Assert.That(FaceOf(ClosedLabel(selector))).IsSameReferenceAs(AggContext.DefaultFont);

			// The selection is still the icon face; only its label is drawn in another.
			await Assert.That(selector.SelectedTypeFace).IsSameReferenceAs(IconFont.TypeFace);
		}

		/// <summary>
		/// TypeFacePrinter's line box is one em, centred on the face's ascent and descent; Nunito spans 1.36 em, so a row
		/// sized to the em box alone would clip its ascenders and descenders.
		/// </summary>
		[Test]
		public async Task RowsOfATallFaceSpanItsWholeAscentAndDescent()
		{
			var selector = new FontSelector("Font", Color.Black);
			TextWidget row = RowText(selector.AddFont("Nunito", DemoText.Nunito));
			selector.SelectedIndex = 0;

			StyledTypeFace style = row.Printer.TypeFaceStyle;
			double span = style.AscentInPixels - style.DescentInPixels;
			await Assert.That(span).IsGreaterThan(style.EmSizeInPixels);
			await Assert.That(row.LocalBounds.Height).IsGreaterThanOrEqualTo(span - .001);
			await Assert.That(ClosedLabel(selector).LocalBounds.Height).IsGreaterThanOrEqualTo(span - .001);
		}

		/// <summary>An SVG face whose ascent plus descent is 2.7 em, with glyphs whose ink fills that whole span.</summary>
		private static TypeFace TallFace()
		{
			const string Glyph = @"horiz-adv-x=""600"" d=""M0 -900L600 -900L600 1800L0 1800z""";
			return TypeFace.LoadFrom(@"<svg><defs>
<font id=""Tall"" horiz-adv-x=""600"" >
<font-face font-family=""Tall"" font-weight=""400"" units-per-em=""1000"" panose-1=""2 11 6 4 2 2 2 2 2 4"" ascent=""1800"" descent=""-900"" x-height=""500"" cap-height=""700"" bbox=""0 -900 600 1800"" underline-thickness=""50"" underline-position=""-100"" unicode-range=""U+0041-U+007A"" />
<missing-glyph horiz-adv-x=""600"" d=""M0 0z"" />
<glyph glyph-name=""T"" unicode=""T"" " + Glyph + @" />
<glyph glyph-name=""a"" unicode=""a"" " + Glyph + @" />
<glyph glyph-name=""l"" unicode=""l"" " + Glyph + @" />
</font></defs></svg>");
		}

		/// <summary>
		/// Changing the font must not move the System window's layout, so the closed field keeps the height the UI font
		/// gives it whatever face is selected (agg-gui's combo is a fixed CLOSED_H), and a tall face's name is fitted
		/// inside it rather than clipped.
		/// </summary>
		[Test]
		public async Task TheClosedFieldKeepsItsHeightAndHoldsATallFacesInk()
		{
			var selector = new FontSelector("Font", Color.Black) { HAnchor = HAnchor.Stretch };
			selector.AddFont("Liberation Sans", LiberationSansFont.Instance);
			selector.AddFont("Nunito", DemoText.Nunito);
			selector.AddFont("Tall", TallFace());
			var host = new GuiWidget(300, 200);
			host.AddChild(selector);

			selector.SelectedIndex = 0;
			host.PerformLayout();
			double height = selector.Height;

			for (int i = 1; i < 3; i++)
			{
				selector.SelectedIndex = i;
				host.PerformLayout();
				await Assert.That(selector.Height).IsEqualTo(height).Within(1e-9).Because(selector.SelectedLabel);

				// The name's ink runs from the descent to the ascent about the label's baseline (its origin): it must
				// lie inside the label, which clips it, and the label inside the field's border.
				TextWidget label = ClosedLabel(selector);
				StyledTypeFace style = label.Printer.TypeFaceStyle;
				await Assert.That(label.LocalBounds.Bottom).IsLessThanOrEqualTo(style.DescentInPixels + 1e-6).Because(selector.SelectedLabel);
				await Assert.That(label.LocalBounds.Top).IsGreaterThanOrEqualTo(style.AscentInPixels - 1e-6).Because(selector.SelectedLabel);

				RectangleDouble inside = selector.LocalBounds;
				inside.Deflate(selector.DeviceBorder);
				await Assert.That(label.BoundsRelativeToParent.Bottom).IsGreaterThanOrEqualTo(inside.Bottom - 1e-6).Because(selector.SelectedLabel);
				await Assert.That(label.BoundsRelativeToParent.Top).IsLessThanOrEqualTo(inside.Top + 1e-6).Because(selector.SelectedLabel);
			}
		}

		/// <summary>
		/// A face added through a loader is not read until its row is first drawn, so building the list costs nothing for
		/// faces the user never scrolls to (MatterCAD's font field parses its fonts this way).
		/// </summary>
		[Test]
		public async Task ALazyFaceLoadsWhenItsRowIsFirstDrawn()
		{
			int loads = 0;
			var selector = new FontSelector("Font", Color.Black);
			MenuItem nunito = selector.AddFontLazy("Nunito", () =>
			{
				loads++;
				return DemoText.Nunito;
			});

			await Assert.That(loads).IsEqualTo(0);
			await Assert.That(FaceOf(RowText(nunito))).IsSameReferenceAs(AggContext.DefaultFont);

			var image = new ImageBuffer(200, 60);
			nunito.OnDraw(image.NewGraphics2D());
			nunito.OnDraw(image.NewGraphics2D());

			await Assert.That(loads).IsEqualTo(1);
			await Assert.That(FaceOf(RowText(nunito))).IsSameReferenceAs(DemoText.Nunito);

			selector.SelectedIndex = 0;
			await Assert.That(loads).IsEqualTo(1);
			await Assert.That(FaceOf(ClosedLabel(selector))).IsSameReferenceAs(DemoText.Nunito);
		}

		private static BorderDouble RowPadding(MenuItem item) => item.Children.First().Padding;

		/// <summary>
		/// Fitting the closed label to a face moves only the label's own margins; rows added after a selection still get
		/// the list's row padding.
		/// </summary>
		[Test]
		public async Task SelectingAFontLeavesTheRowPaddingAlone()
		{
			var selector = new FontSelector("Font", Color.Black);
			MenuItem first = selector.AddFont("Nunito", DemoText.Nunito);
			BorderDouble padding = selector.MenuItemsPadding;

			selector.SelectedIndex = 0;
			MenuItem second = selector.AddFont("Liberation Sans", LiberationSansFont.Instance);

			await Assert.That(selector.MenuItemsPadding).IsEqualTo(padding);
			await Assert.That(RowPadding(second)).IsEqualTo(RowPadding(first));
		}

		/// <summary>The closed field's fixed height follows padding a caller sets after construction, and a selection keeps it.</summary>
		[Test]
		public async Task ClosedHeightFollowsPaddingSetAfterConstruction()
		{
			var selector = new FontSelector("Font", Color.Black) { HAnchor = HAnchor.Stretch };
			selector.AddFont("Liberation Sans", LiberationSansFont.Instance);
			selector.AddFont("Nunito", DemoText.Nunito);
			var host = new GuiWidget(300, 200);
			host.AddChild(selector);
			selector.SelectedIndex = 0;
			host.PerformLayout();
			double defaultHeight = selector.Height;

			var padding = new BorderDouble(10, 20, 40, 20);
			selector.MenuItemsPadding = padding;
			host.PerformLayout();
			double paddedHeight = selector.Height;
			await Assert.That(paddedHeight).IsGreaterThan(defaultHeight);

			selector.SelectedIndex = 1;
			host.PerformLayout();
			await Assert.That(selector.Height).IsEqualTo(paddedHeight).Within(1e-9);
			await Assert.That(selector.MenuItemsPadding).IsEqualTo(padding);
		}

		/// <summary>A loader that throws is treated as giving no face: the UI font is shown and selection still works.</summary>
		[Test]
		public async Task ALoaderThatThrowsShowsTheUiFont()
		{
			var selector = new FontSelector("Font", Color.Black);
			MenuItem broken = selector.AddFontLazy("Broken", () => throw new System.IO.IOException("unreadable font file"));
			int changes = 0;
			selector.SelectionChanged += (s, e) => changes++;

			broken.OnDraw(new ImageBuffer(200, 60).NewGraphics2D());
			await Assert.That(FaceOf(RowText(broken))).IsSameReferenceAs(AggContext.DefaultFont);

			selector.SelectedIndex = 0;
			await Assert.That(changes).IsEqualTo(1);
			await Assert.That(selector.SelectedTypeFace).IsNull();
			await Assert.That(FaceOf(ClosedLabel(selector))).IsSameReferenceAs(AggContext.DefaultFont);
		}

		[Test]
		public async Task ANullNameCannotBeDrawn()
		{
			await Assert.That(FontSelector.CanDrawName(DemoText.Nunito, null)).IsFalse();
		}
	}
}
