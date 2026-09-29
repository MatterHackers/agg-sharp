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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Svg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	// agg/Svg group compositing: mix-blend-mode and isolation. A yellow backdrop under a cyan square, whose
	// multiply is green and whose normal source-over is cyan.
	public class SvgCompositingTests
	{
		private const string Backdrop = "<rect width=\"100\" height=\"100\" fill=\"#ffff00\"/>";

		private static ImageBuffer Render(string body)
		{
			return SvgDocument.RenderToImage($"<svg xmlns=\"http://www.w3.org/2000/svg\" viewBox=\"0 0 100 100\">{Backdrop}{body}</svg>", 100, 100);
		}

		private static string Cyan(string attributes) => $"<rect x=\"20\" y=\"20\" width=\"60\" height=\"60\" fill=\"#00ffff\" {attributes}/>";

		private static (int Red, int Green, int Blue) CenterColor(ImageBuffer image)
		{
			Color c = image.GetPixel(50, 50);
			return (c.red, c.green, c.blue);
		}

		[Test]
		public async Task MixBlendModeBlendsAnElementWithWhatIsBehindIt()
		{
			await Assert.That(CenterColor(Render(Cyan("style=\"mix-blend-mode:multiply\"")))).IsEqualTo((0, 255, 0));
			await Assert.That(CenterColor(Render(Cyan("style=\"mix-blend-mode:difference\"")))).IsEqualTo((255, 0, 255));

			// The backdrop outside the square is untouched.
			await Assert.That(Render(Cyan("style=\"mix-blend-mode:multiply\"")).GetPixel(5, 5).blue).IsEqualTo((byte)0);
		}

		[Test]
		public async Task MixBlendModeAndIsolationAreReadOnlyFromStyles()
		{
			// Neither is a presentation attribute: as attributes they are ignored.
			await Assert.That(CenterColor(Render(Cyan("mix-blend-mode=\"multiply\"")))).IsEqualTo((0, 255, 255));
			await Assert.That(CenterColor(Render($"<g isolation=\"isolate\">{Cyan("style=\"mix-blend-mode:multiply\"")}</g>"))).IsEqualTo((0, 255, 0));
		}

		[Test]
		public async Task AnIsolatedGroupBlendsItsChildrenOnlyWithEachOther()
		{
			// isolation:isolate and a group opacity both start a new, transparent backdrop for the children.
			await Assert.That(CenterColor(Render($"<g style=\"isolation:isolate\">{Cyan("style=\"mix-blend-mode:multiply\"")}</g>"))).IsEqualTo((0, 255, 255));
			await Assert.That(CenterColor(Render($"<g opacity=\"0.999\">{Cyan("style=\"mix-blend-mode:multiply\"")}</g>"))).IsEqualTo((0, 255, 255));
		}
	}
}

