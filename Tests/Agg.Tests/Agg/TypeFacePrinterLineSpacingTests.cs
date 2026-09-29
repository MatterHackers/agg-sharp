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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="TypeFacePrinter.LineSpacing"/>: 1 (the default) keeps the one-em line advance
	/// MatterCAD lays out against; other values move every line after the first by that many ems.
	/// </summary>
	public class TypeFacePrinterLineSpacingTests
	{
		private static TypeFacePrinter Printer(string text) => new TypeFacePrinter(text, new StyledTypeFace(AggContext.DefaultFont, 12));

		private static double LowestVertexY(TypeFacePrinter printer)
		{
			double lowest = double.MaxValue;
			foreach (var vertex in printer.Vertices())
			{
				if (vertex.IsVertex)
				{
					lowest = Math.Min(lowest, vertex.Position.Y);
				}
			}

			return lowest;
		}

		[Test]
		public async Task DefaultSpacingAdvancesOneEmPerLine()
		{
			var printer = Printer("Ag\nAg\nAg");
			double em = printer.TypeFaceStyle.EmSizeInPixels;

			await Assert.That(printer.LineSpacing).IsEqualTo(1.0);
			await Assert.That(printer.GetSize().Y).IsEqualTo(em * 3).Within(1e-9);
			await Assert.That(printer.GetOffsetLeftOfCharacterIndex(6).Y).IsEqualTo(-em * 2).Within(1e-9);
			await Assert.That(LowestVertexY(printer) - LowestVertexY(Printer("Ag"))).IsEqualTo(-em * 2).Within(1.0);
		}

		[Test]
		public async Task OneAndAHalfSpacingAdvancesOneAndAHalfEmsPerLine()
		{
			var printer = Printer("Ag\nAg\nAg");
			double em = printer.TypeFaceStyle.EmSizeInPixels;
			printer.LineSpacing = 1.5;

			// The first line stays one em tall; each line after it adds 1.5 em.
			await Assert.That(printer.GetSize().Y).IsEqualTo(em + em * 1.5 * 2).Within(1e-9);
			await Assert.That(printer.GetOffsetLeftOfCharacterIndex(6).Y).IsEqualTo(-em * 3).Within(1e-9);
			// The drawn glyphs move too, not just the measurements.
			await Assert.That(LowestVertexY(printer) - LowestVertexY(Printer("Ag"))).IsEqualTo(-em * 3).Within(1.0);
		}

		[Test]
		public async Task WrappedTextWidgetKeepsItsSpacingAcrossTextChanges()
		{
			// TextWidget rebuilds its printer on every text change, so the spacing has to be carried across.
			var single = new WrappedTextWidget("one two three four five six seven eight nine ten", 12) { Width = 60 };
			var spaced = new WrappedTextWidget("one two three four five six seven eight nine ten", 12) { Width = 60 };
			spaced.LineSpacing = 1.5;
			spaced.Text = "ten nine eight seven six five four three two one";

			await Assert.That(single.LineSpacing).IsEqualTo(1.0);
			await Assert.That(spaced.TextWidget.Printer.LineSpacing).IsEqualTo(1.5);
			await Assert.That(spaced.TextWidget.Height).IsGreaterThan(single.TextWidget.Height * 1.3);
		}
	}
}
