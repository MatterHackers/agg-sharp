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
	// The GUI demo's Widget Gallery window (agg-gui's demo-ui/src/windows/gallery.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class WidgetGalleryWindowTests
	{
		private static DemoSpec GallerySpec => GuiDemoSpecs.All.First(s => s.Title == "Widget Gallery");

		private static WidgetGalleryWindow Build(DemoTheme demoTheme = null)
		{
			var page = new GuiWidget(360, 290);
			GuiWidget content = GuiDemoSpecs.CreateContent(GallerySpec, demoTheme);
			page.AddChild(content);
			page.PerformLayout();
			return (WidgetGalleryWindow)content;
		}

		[Test]
		public async Task BuildsEveryGalleryRowNamedAndScrolls()
		{
			WidgetGalleryWindow gallery = Build();
			await Assert.That(gallery.Name).IsEqualTo("Widget Gallery Content");

			string[] names =
			{
				"Gallery TextEdit", "Gallery Button", "Gallery Link", "Gallery Checkbox",
				"Gallery Radio First", "Gallery Radio Second", "Gallery Radio Third",
				"Gallery Segmented", "Gallery Segmented Size",
				"Gallery Selectable First", "Gallery Selectable Second", "Gallery Selectable Third",
				"Gallery ComboBox", "Gallery Slider", "Gallery DragValue", "Gallery ProgressBar",
				"Gallery Spinner Small", "Gallery Spinner", "Gallery ColorPicker", "Gallery Color Wheel", "Gallery Image",
				"Gallery Image Button", "Gallery CollapsingHeader", "Gallery ToggleSwitch",
				"Gallery Visible", "Gallery Interactive", "Gallery Opacity",
				"Gallery Doc Label", "Gallery Doc Hyperlink", "Gallery Doc CollapsingHeader",
			};
			foreach (string name in names)
			{
				await Assert.That(gallery.FindDescendant(name)).IsNotNull();
			}

			// gallery.rs's defaults: scalar 42, first choice, bool off, header closed, fully opaque.
			await Assert.That(gallery.Scalar).IsEqualTo(42);
			await Assert.That(gallery.DragValue.Value).IsEqualTo(42);
			await Assert.That(gallery.SelectedChoice).IsEqualTo(0);
			await Assert.That(gallery.CheckBox.Checked).IsFalse();
			await Assert.That(gallery.CollapsingHeader.Expanded).IsFalse();
			await Assert.That(gallery.OpacityValue.Value).IsEqualTo(1);

			// The gallery is far taller than agg-gui's 290 high window, so it scrolls.
			await Assert.That(gallery.ScrollArea.Height).IsGreaterThan(gallery.Height);
		}

		[Test]
		public async Task SharedStateMovesEveryBoundWidget()
		{
			WidgetGalleryWindow gallery = Build();

			// The slider drives the drag value and the progress bar.
			gallery.Slider.Value = 180;
			await Assert.That(gallery.Scalar).IsEqualTo(180);
			await Assert.That(gallery.DragValue.Value).IsEqualTo(180);
			await Assert.That(gallery.ProgressBar.RatioComplete).IsEqualTo(.5);

			// A segment moves the radio, the selectable labels and the combo box.
			gallery.SegmentedControl.SelectedIndex = 2;
			await Assert.That(gallery.SelectedChoice).IsEqualTo(2);
			await Assert.That(gallery.RadioButtons[2].Checked).IsTrue();
			await Assert.That(gallery.RadioButtons[0].Checked).IsFalse();
			await Assert.That(gallery.ComboBox.SelectedIndex).IsEqualTo(2);

			// And back from a selectable label.
			gallery.SelectableButtons[1].InvokeClick();
			await Assert.That(gallery.SegmentedControl.SelectedIndex).IsEqualTo(1);

			// Button and Link both flip the checkbox.
			gallery.Button.InvokeClick();
			await Assert.That(gallery.CheckBox.Checked).IsTrue();
			gallery.Link.Activate();
			await Assert.That(gallery.CheckBox.Checked).IsFalse();

			// The colour wheel drives the colour picker live, as gallery.rs's shared colour cell does.
			gallery.ColorWheel.Color = Color.Orange;
			await Assert.That(gallery.ColorPicker.Color).IsEqualTo(Color.Orange);

			// Interactive off disables the grid and dims it; opacity fades it through the scope's backbuffer.
			gallery.InteractiveCheckBox.Checked = false;
			await Assert.That(gallery.FindDescendant("Gallery Grid").Enabled).IsFalse();
			await Assert.That(gallery.Scope.BackbufferOpacity).IsEqualTo(.4).Within(1e-9);
			gallery.InteractiveCheckBox.Checked = true;
			gallery.OpacityValue.Value = .5;
			await Assert.That(gallery.Scope.BackbufferOpacity).IsEqualTo(.5).Within(1e-9);
			await Assert.That(gallery.Scope.DoubleBuffer).IsTrue();

			// Visible off hides the grid and the Interactive / Opacity controls.
			gallery.VisibleCheckBox.Checked = false;
			await Assert.That(gallery.FindDescendant("Gallery Grid").Visible).IsFalse();
			await Assert.That(gallery.InteractiveCheckBox.Parent.Visible).IsFalse();
		}

		[Test]
		public async Task ThemeChangeRecoloursAndKeepsTheToggleState()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			WidgetGalleryWindow gallery = Build(demoTheme);
			gallery.ToggleSwitch.Checked = true;

			demoTheme.SetPreference(ThemePreference.Light);

			await Assert.That(gallery.ToggleSwitch.Checked).IsTrue();
			await Assert.That(gallery.ToggleSwitch.Parent).IsNotNull();
			await Assert.That(gallery.CheckBox.TextColor).IsEqualTo(DemoPalette.Light.TextColor);
		}
	}
}
