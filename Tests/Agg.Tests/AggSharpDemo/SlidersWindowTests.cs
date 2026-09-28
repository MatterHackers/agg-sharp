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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Sliders window (agg-gui's demo-ui/src/windows/sliders_demo.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class SlidersWindowTests
	{
		private static DemoSpec SlidersSpec => GuiDemoSpecs.All.First(s => s.Title == "Sliders");

		private static SlidersWindow Build()
		{
			var page = new GuiWidget(360, 290);
			GuiWidget content = GuiDemoSpecs.CreateContent(SlidersSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (SlidersWindow)content;
		}

		[Test]
		public async Task BuildsEveryControlNamedWithEguiDefaults()
		{
			SlidersWindow window = Build();
			await Assert.That(window.Name).IsEqualTo("Sliders Content");

			string[] names =
			{
				"Sliders Demo Slider", "Sliders Assign PI", "Sliders Range Min", "Sliders Range Max",
				"Sliders Trailing Fill", "Sliders Handle Circle", "Sliders Handle Rectangle", "Sliders Use Steps",
				"Sliders Step", "Sliders Type i32", "Sliders Type f64", "Sliders Orientation Horizontal",
				"Sliders Orientation Vertical", "Sliders Logarithmic", "Sliders Clamping Never", "Sliders Clamping Edits",
				"Sliders Clamping Always", "Sliders Smart Aim", "Sliders Reset",
			};
			foreach (string name in names)
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			// egui's defaults: 10 in a logarithmic, always clamped 0..10000 with smart aim.
			await Assert.That(window.DemoSlider.Value).IsEqualTo(10);
			await Assert.That(window.DemoSlider.Logarithmic).IsTrue();
			await Assert.That(window.DemoSlider.SmartAim).IsTrue();
			await Assert.That(window.DemoSlider.Clamping).IsEqualTo(SliderClamping.Always);
			await Assert.That(window.MaximumSlider.Value).IsEqualTo(10000);
			await Assert.That(window.DemoValueText).IsEqualTo("10.00");
			await Assert.That(window.StepValue.Visible).IsFalse();
		}

		[Test]
		public async Task OptionsRebuildTheDemoSliderAndKeepItsValue()
		{
			SlidersWindow window = Build();

			// Use steps: the step editor shows, and the arrows move a step at a time.
			window.UseStepsCheckBox.Checked = true;
			await Assert.That(window.StepValue.Visible).IsTrue();
			await Assert.That(window.DemoSlider.Step).IsEqualTo(10);
			window.DemoSlider.OnKeyDown(new KeyEventArgs(Keys.Right));
			await Assert.That(window.Value).IsEqualTo(20);
			await Assert.That(window.DemoValueText).IsEqualTo("20");

			// i32 hides Assign PI; Vertical turns the slider; the value survives both rebuilds.
			window.TypeRadios[0].Checked = true;
			await Assert.That(window.DemoSlider.Integer).IsTrue();
			await Assert.That(window.FindDescendant("Sliders Assign PI")).IsNull();
			window.OrientationRadios[1].Checked = true;
			await Assert.That(window.DemoSlider.Orientation).IsEqualTo(Orientation.Vertical);
			await Assert.That(window.DemoSlider.Value).IsEqualTo(20);

			// Moving the range's minimum up clamps the demo slider's value into it.
			window.MinimumSlider.Value = 100;
			await Assert.That(window.Minimum).IsEqualTo(100);
			await Assert.That(window.DemoSlider.Value).IsEqualTo(100);

			// Reset brings back egui's defaults, and Assign PI sets pi.
			window.Reset();
			await Assert.That(window.DemoSlider.Orientation).IsEqualTo(Orientation.Horizontal);
			await Assert.That(window.TypeRadios[1].Checked).IsTrue();
			window.AssignPi();
			await Assert.That(window.Value).IsEqualTo(System.Math.PI);
			await Assert.That(window.DemoValueText).IsEqualTo("3.142");
		}
	}
}
