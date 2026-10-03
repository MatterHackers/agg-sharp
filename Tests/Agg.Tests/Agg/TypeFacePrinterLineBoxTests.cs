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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	/// <summary>Where <see cref="TypeFacePrinter.LocalBounds"/> puts its one-em line box against the baseline.</summary>
	public class TypeFacePrinterLineBoxTests
	{
		/// <summary>An SVG font whose font-face gives no ascent or descent, as some old fonts do.</summary>
		private const string NoMetricsFont =
			"<font id=\"bare\" horiz-adv-x=\"500\"><font-face font-family=\"Bare\" units-per-em=\"1000\" panose-1=\"0 0 0 0 0 0 0 0 0 0\" bbox=\"0 0 500 700\"/>"
			+ "<missing-glyph horiz-adv-x=\"500\"/><glyph unicode=\"A\" horiz-adv-x=\"500\" d=\"M0 0L500 0L250 700Z\"/></font>";

		/// <summary>With no ascent and descent to centre on, the box starts at the baseline (the descent, 0) as
		/// it always did, rather than straddling it and clipping the top half of every glyph.</summary>
		[Test]
		public async Task AFaceWithNoAscentOrDescentKeepsItsBoxOnTheBaseline()
		{
			TypeFace face = TypeFace.LoadFrom(NoMetricsFont);
			var printer = new TypeFacePrinter("A", new StyledTypeFace(face, 12));

			await Assert.That(printer.LocalBounds.Bottom).IsEqualTo(0).Within(1e-9);
			await Assert.That(printer.LocalBounds.Top).IsEqualTo(printer.TypeFaceStyle.EmSizeInPixels).Within(1e-9);
		}

		/// <summary>
		/// Every <see cref="Baseline"/> prints, and its box sits where the name says against the origin:
		/// Text puts the baseline there, BoundsTop the top of the ascent, BoundsCenter half the ascent,
		/// TextCenter the middle of the line box and BoundsBottom its bottom. The glyphs move with the box.
		/// </summary>
		[Test]
		public async Task EveryBaselinePlacesTheBoxWhereItsNameSays()
		{
			var style = new StyledTypeFace(AggContext.DefaultFont, 24);
			double lineBoxBottom = new TypeFacePrinter("H", style).LineBoxBottomInPixels;
			double em = style.EmSizeInPixels;
			double ascent = style.AscentInPixels;

			double textGlyphBottom = GlyphBottom(new TypeFacePrinter("H", style, baseline: Baseline.Text));

			foreach (Baseline baseline in Enum.GetValues<Baseline>())
			{
				var printer = new TypeFacePrinter("H", style, baseline: baseline);
				RectangleDouble bounds = printer.LocalBounds;

				double expectedBottom = baseline switch
				{
					Baseline.Text => lineBoxBottom,
					Baseline.BoundsTop => lineBoxBottom - ascent,
					Baseline.BoundsCenter => lineBoxBottom - ascent / 2,
					Baseline.TextCenter => -em / 2,
					Baseline.BoundsBottom => 0,
					_ => throw new NotImplementedException(),
				};

				await Assert.That(bounds.Bottom).IsEqualTo(expectedBottom).Within(1e-9);
				await Assert.That(bounds.Top - bounds.Bottom).IsEqualTo(em).Within(1e-9);

				// The glyphs shift by the same amount as the box (to within the whole-pixel baseline snap).
				double glyphShift = GlyphBottom(printer) - textGlyphBottom;
				await Assert.That(glyphShift).IsEqualTo(bounds.Bottom - lineBoxBottom).Within(1.0);
			}
		}

		private static double GlyphBottom(TypeFacePrinter printer)
		{
			return printer.Vertices().Where(vertex => !vertex.IsStop).Min(vertex => vertex.Position.Y);
		}
	}
}
