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
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class ThreeDAnimationWindowTests
	{
		[Test]
		public async Task TheSsaaSegmentsSetTheBarGridsFactor()
		{
			var spec = GuiDemoSpecs.All.First(s => s.Title == "3D Animation");
			var window = (ThreeDAnimationWindow)GuiDemoSpecs.CreateContent(spec, new DemoTheme());

			await Assert.That(window.Name).IsEqualTo(spec.ContentName);
			await Assert.That(window.FindDescendant("3D Animation Bar Grid")).IsSameReferenceAs(window.BarGrid);
			await Assert.That(window.FindDescendant("3D Animation SSAA")).IsSameReferenceAs(window.Ssaa);

			// Starts at 4x (linear 2); each segment is its linear factor.
			await Assert.That(window.Ssaa.SelectedLabel).IsEqualTo("4×");
			window.Ssaa.SelectedIndex = 3;
			await Assert.That(window.BarGrid.SsaaFactor).IsEqualTo(4);
			window.Ssaa.SelectedIndex = 0;
			await Assert.That(window.BarGrid.SsaaFactor).IsEqualTo(1);
		}
	}
}
