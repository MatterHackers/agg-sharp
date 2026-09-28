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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>Splitter's Panel1Ratio keeps the first panel a fixed share of the splitter as it resizes.</summary>
	public class SplitterRatioTests
	{
		[Test]
		public async Task VerticalRatioSizesTheLeftPanelFromTheWidth()
		{
			var splitter = new Splitter { Orientation = Orientation.Vertical };
			var host = new GuiWidget(400, 100);
			host.AddChild(splitter);
			splitter.Panel1Ratio = 0.25;
			await Assert.That(splitter.SplitterDistance).IsEqualTo(100);
			await Assert.That(splitter.Panel1.Width).IsEqualTo(100);

			// Resizing keeps the share, taken from the width - not the height.
			host.Size = new Vector2(800, 50);
			await Assert.That(splitter.SplitterDistance).IsEqualTo(200);
			await Assert.That(splitter.Panel2.Width).IsEqualTo(800 - 200 - splitter.SplitterSize);
		}
	}
}
