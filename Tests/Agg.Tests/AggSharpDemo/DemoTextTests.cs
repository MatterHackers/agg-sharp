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

using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo draws agg-gui's pixel sizes at agg-gui's size, in agg-gui's faces.
	public class DemoTextTests
	{
		[Test]
		public async Task AnAggGuiSizeDrawsThatManyPixelsPerEm()
		{
			var face = new StyledTypeFace(LiberationSansFont.Instance, DemoText.Points(13));
			await Assert.That(face.EmSizeInPixels).IsEqualTo(13).Within(1e-9);
		}

		[Test]
		public async Task TheFacesAreAggGuisNunitoAndCascadiaCode()
		{
			// Nunito 3.504 and Cascadia Code 2102.025's metrics, as agg-gui's demo/assets ship them.
			await Assert.That(DemoText.Nunito.UnitsPerEm).IsEqualTo(1000);
			await Assert.That(DemoText.Nunito.Ascent).IsEqualTo(1011);
			await Assert.That(DemoText.NunitoBold.UnitsPerEm).IsEqualTo(1000);
			await Assert.That(CodeFont.TypeFace.UnitsPerEm).IsEqualTo(2048);
		}

		[Test]
		[NotInParallel]
		public async Task TheHeadsMakeNunitoTheDefaultFace()
		{
			// Keyless: every text test reads the default faces.
			TypeFace wasFont = AggContext.DefaultFont;
			TypeFace wasBold = AggContext.DefaultFontBold;
			try
			{
				DemoText.UseNunitoAsDefault();
				await Assert.That(AggContext.DefaultFont).IsSameReferenceAs(DemoText.Nunito);
				await Assert.That(AggContext.DefaultFontBold).IsSameReferenceAs(DemoText.NunitoBold);

				// Symbols Nunito lacks come from Liberation Sans, emojis from Noto Emoji.
				await Assert.That(DemoText.Nunito.ResolveFace(0x25BC)).IsSameReferenceAs(LiberationSansFont.Instance);
				await Assert.That(DemoText.Nunito.ResolveFace(0x1F493)).IsSameReferenceAs(EmojiFont.TypeFace);

				var label = new TextWidget("Label", pointSize: DemoText.Points(13));
				await Assert.That(label.Printer.TypeFaceStyle.TypeFace).IsSameReferenceAs(DemoText.Nunito);
				await Assert.That(label.Printer.TypeFaceStyle.EmSizeInPixels).IsEqualTo(13 * GuiWidget.DeviceScale).Within(1e-9);
			}
			finally
			{
				AggContext.DefaultFont = wasFont;
				AggContext.DefaultFontBold = wasBold;
			}
		}
	}
}
