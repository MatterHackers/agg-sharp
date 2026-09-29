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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// CheckBox, RadioButton and ToggleSwitchView drawn in agg-gui's look from ThemeConfig.Current,
	/// without moving any size, hit area or label position MatterCAD's layouts are built around.
	/// </summary>
	/// <remarks>
	/// Keyless <c>[NotInParallel]</c>, exclusive of every other test, for two reasons. The tests write the
	/// process-wide <see cref="GuiWidget.DeviceScale"/>, which every layout reads (see SharedStateKeys). And
	/// checking a box or hovering it queues <see cref="CheckBoxViewStates.PostUpdateSetCorrectVisibilityStates"/>
	/// on the process-wide <see cref="UiThread"/> idle queue: a test pumping UiThread on another thread ran
	/// that queued update while this test drew, so the switch's states were mid-layout and the "on" image came
	/// out as bare background. Keyed on DeviceScale and Current only, this class ran alongside those pumps.
	/// </remarks>
	[NotInParallel]
	public class CheckRadioToggleStyleTests
	{
		private const int HostPad = 4;

		/// <summary>The metrics main had before the restyle, at DeviceScale 1 and then 1.5.</summary>
		private const string MetricsBeforeRestyle =
			"91.148x16 min 91.148x16 label 20,0\n"
			+ "85.219x14.667 min 85.219x14.667 label 20,0\n"
			+ "62.805x16 min 62.805x16 label 21,0\n"
			+ "59.321x14.667 min 59.321x14.667 label 21,0\n"
			+ "59.602x20 min 59.602x20 label none\n"
			+ "62.786x20 min 62.786x20 label 0,3.333\n"
			+ "136.723x24 min 136.723x24 label 30,0\n"
			+ "127.829x22 min 127.829x22 label 30,0\n"
			+ "94.207x24 min 94.207x24 label 31.5,0\n"
			+ "88.981x22 min 88.981x22 label 31.5,0\n"
			+ "89.402x30 min 89.402x30 label none\n"
			+ "89.402x20 min 89.402x20 label 0,0";

		private static ThemeConfig DarkTheme => new DemoTheme(ThemePreference.Dark).Theme;

		private static ThemeConfig LightTheme => new DemoTheme(ThemePreference.Light).Theme;

		private static string Metrics(GuiWidget widget)
		{
			var label = widget.Descendants<TextWidget>().FirstOrDefault(t => t.Visible);
			string labelAt = "none";
			if (label != null)
			{
				var bounds = label.TransformToParentSpace(widget, label.LocalBounds);
				labelAt = $"{bounds.Left:0.###},{bounds.Bottom:0.###}";
			}

			return $"{widget.Width:0.###}x{widget.Height:0.###} min {widget.MinimumSize.X:0.###}x{widget.MinimumSize.Y:0.###} label {labelAt}";
		}

		private static string AllMetrics()
		{
			var parts = new List<string>();
			foreach (var scale in new[] { 1.0, 1.5 })
			{
				GuiWidget.DeviceScale = scale;
				parts.Add(Metrics(new CheckBox("Checkbox")));
				parts.Add(Metrics(new CheckBox("Checkbox", Color.Black, 11)));
				parts.Add(Metrics(new RadioButton("Radio")));
				parts.Add(Metrics(new RadioButton("Radio", Color.Black, 11)));
				parts.Add(Metrics(Toggle("", "", 40 * scale, 20 * scale)));
				parts.Add(Metrics(Toggle("On", "Off", 40, 20)));
			}

			return string.Join("\n", parts);
		}

		private static CheckBox Toggle(string on, string off, double width, double height, Color? onColor = null)
			=> new CheckBox(new ToggleSwitchView(on, off, width, height, Color.Gray, onColor ?? new Color(0, 90, 220), Color.White, Color.Black, Color.Gray));

		/// <summary>
		/// Draws the widget, at <see cref="HostPad"/> inside a host filled with the theme's background, with the
		/// theme as ThemeConfig.Current. Hover moves the mouse over the widget's left end, where the mark is.
		/// </summary>
		private static ImageBuffer Render(GuiWidget widget, ThemeConfig theme, bool hover = false)
		{
			ThemeConfig.Current = theme;
			var host = new GuiWidget(widget.Width + HostPad * 2, widget.Height + HostPad * 2)
			{
				BackgroundColor = theme.BackgroundColor,
			};
			widget.Position = new Vector2(HostPad, HostPad);
			host.AddChild(widget);
			if (hover)
			{
				host.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, HostPad + 6 * GuiWidget.DeviceScale, HostPad + widget.Height / 2, 0));
			}

			// CheckBoxViewStates picks its visible state on idle; pick it now.
			(widget.Children.FirstOrDefault() as CheckBoxViewStates)?.PostUpdateSetCorrectVisibilityStates();

			var image = new ImageBuffer((int)Math.Ceiling(host.Width), (int)Math.Ceiling(host.Height));
			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(theme.BackgroundColor);
			host.OnDraw(graphics2D);
			return image;
		}

		private static async Task RunAtScale(double scale, Func<Task> test)
		{
			var savedScale = GuiWidget.DeviceScale;
			var savedTheme = ThemeConfig.Current;
			try
			{
				GuiWidget.DeviceScale = scale;
				await test();
			}
			finally
			{
				GuiWidget.DeviceScale = savedScale;
				ThemeConfig.Current = savedTheme;
			}
		}

		private static bool Near(Color a, Color b, int tolerance = 12)
			=> Math.Abs(a.red - b.red) <= tolerance && Math.Abs(a.green - b.green) <= tolerance && Math.Abs(a.blue - b.blue) <= tolerance;

		private static bool IsWhite(Color c) => c.red > 225 && c.green > 225 && c.blue > 225;

		[Test]
		public async Task SizesAndLabelPositionsAreUnchanged()
		{
			var saved = GuiWidget.DeviceScale;
			string metrics;
			try
			{
				metrics = AllMetrics();
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}

			await Assert.That(metrics).IsEqualTo(MetricsBeforeRestyle);
		}

		[Test]
		public async Task ThemeConfigCurrentFallsBackToADefaultOnNull()
		{
			var saved = ThemeConfig.Current;
			try
			{
				ThemeConfig.Current = null;
				await Assert.That(ThemeConfig.Current).IsNotNull();
				await Assert.That(ThemeConfig.Current.PrimaryAccentColor).IsEqualTo(ThemeConfig.DefaultTheme().PrimaryAccentColor);
			}
			finally
			{
				ThemeConfig.Current = saved;
			}
		}

		/// <summary>
		/// At DeviceScale 1 the check box is a 12 box at (2, 2) inside the 20 wide slot, so on a 16 high check
		/// box its lower left is (HostPad + 2, HostPad + 2) in the image (whose row 0 is the bottom).
		/// </summary>
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task CheckBoxIsAccentFilledWithAWhiteTickWhenChecked(bool dark)
		{
			await RunAtScale(1, async () =>
			{
				var theme = dark ? DarkTheme : LightTheme;
				int left = HostPad + 2;
				int bottom = HostPad + 2;

				var uncheckedImage = Render(new CheckBox("Checkbox"), theme);
				var checkedImage = Render(new CheckBox("Checkbox") { Checked = true }, theme);
				var hovered = Render(new CheckBox("Checkbox"), theme, hover: true);
				var disabled = Render(new CheckBox("Checkbox") { Checked = true, Enabled = false }, theme);

				// the top left of the box, clear of the tick
				var accentPixel = checkedImage.GetPixel(left + 3, bottom + 9);
				await Assert.That(Near(accentPixel, theme.PrimaryAccentColor)).IsTrue().Because($"checked fills the box with the accent, got {accentPixel}");

				// the tick's lower vertex, at 42% across and 28% up
				var tickPixel = checkedImage.GetPixel(left + 5, bottom + 3);
				await Assert.That(IsWhite(tickPixel)).IsTrue().Because($"the tick is white, got {tickPixel}");

				var uncheckedPixel = uncheckedImage.GetPixel(left + 6, bottom + 6);
				var expectedFill = dark ? theme.BackgroundColor : theme.ButtonBackgroundColor;
				await Assert.That(Near(uncheckedPixel, expectedFill, 2)).IsTrue().Because($"unchecked shows the neutral fill, got {uncheckedPixel}");
				var borderPixel = uncheckedImage.GetPixel(left + 6, bottom);
				await Assert.That(Near(borderPixel, expectedFill, 10)).IsFalse().Because($"unchecked draws a border, got {borderPixel}");

				var hoverPixel = hovered.GetPixel(left + 6, bottom + 6);
				await Assert.That(Near(hoverPixel, uncheckedPixel, 2)).IsFalse().Because($"hover shades the box, got {hoverPixel}");

				var disabledPixel = disabled.GetPixel(left + 3, bottom + 9);
				await Assert.That(Near(disabledPixel, theme.PrimaryAccentColor)).IsFalse().Because($"disabled fades the accent, got {disabledPixel}");
			});
		}

		/// <summary>Indeterminate (egui's tri-state) keeps the accent fill but swaps the tick for a white dash.</summary>
		[Test]
		public async Task IndeterminateCheckBoxIsAccentFilledWithAWhiteDash()
		{
			await RunAtScale(1, async () =>
			{
				var theme = LightTheme;
				int left = HostPad + 2;
				int bottom = HostPad + 2;
				var image = Render(new CheckBox("Checkbox") { Indeterminate = true }, theme);

				var accentPixel = image.GetPixel(left + 3, bottom + 9);
				await Assert.That(Near(accentPixel, theme.PrimaryAccentColor)).IsTrue().Because($"indeterminate fills the box with the accent, got {accentPixel}");

				// the dash crosses the middle of the 12 box; it is 1.5 thick so no row is fully covered
				var dashPixel = image.GetPixel(left + 6, bottom + 6);
				await Assert.That(dashPixel.red > theme.PrimaryAccentColor.red + 100).IsTrue().Because($"the dash is white, got {dashPixel}");

				// where the tick's lower vertex would be is still accent - no tick
				var noTickPixel = image.GetPixel(left + 5, bottom + 3);
				await Assert.That(Near(noTickPixel, theme.PrimaryAccentColor)).IsTrue().Because($"no tick is drawn, got {noTickPixel}");
			});
		}

		/// <summary>The radio circle is 11 square at the view's left, centered vertically; its radius is 5.</summary>
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task RadioIsAnAccentCircleWithAnInnerDotWhenSelected(bool dark)
		{
			await RunAtScale(1, async () =>
			{
				var theme = dark ? DarkTheme : LightTheme;
				var radio = new RadioButton("Radio") { Checked = true };
				var image = Render(radio, theme);
				var circle = radio.Descendants<RadioCircleWidget>().First();
				var center = circle.TransformToParentSpace(radio, circle.LocalBounds).Center + new Vector2(HostPad, HostPad);

				var ring = image.GetPixel((int)(center.X + 3), (int)center.Y);
				await Assert.That(Near(ring, theme.PrimaryAccentColor)).IsTrue().Because($"a selected radio is accent, got {ring}");
				var dot = image.GetPixel((int)center.X, (int)center.Y);
				await Assert.That(Near(dot, theme.ButtonBackgroundColor)).IsTrue().Because($"with an inner widget background dot, got {dot}");

				var off = Render(new RadioButton("Radio"), theme);
				var offPixel = off.GetPixel((int)(center.X + 3), (int)center.Y);
				await Assert.That(Near(offPixel, theme.PrimaryAccentColor)).IsFalse().Because($"an unselected radio is not accent, got {offPixel}");
			});
		}

		/// <summary>The switch is 40 x 20 at the host's pad; its knob sits at one end or the other.</summary>
		[Test]
		[Arguments(true)]
		[Arguments(false)]
		public async Task ToggleKnobIsWhiteAtTheEndMatchingItsState(bool dark)
		{
			await RunAtScale(1, async () =>
			{
				var theme = dark ? DarkTheme : LightTheme;
				var onColor = new Color(0, 90, 220);
				int middle = HostPad + 10;

				var offImage = Render(Toggle("", "", 40, 20, onColor), theme);
				var toggleOn = Toggle("", "", 40, 20, onColor);
				toggleOn.Checked = true;
				var onImage = Render(toggleOn, theme);

				await Assert.That(IsWhite(offImage.GetPixel(HostPad + 10, middle))).IsTrue().Because("off puts the knob at the left");
				await Assert.That(IsWhite(offImage.GetPixel(HostPad + 30, middle))).IsFalse().Because("off leaves the right end track coloured");

				await Assert.That(IsWhite(onImage.GetPixel(HostPad + 30, middle))).IsTrue().Because("on puts the knob at the right");
				var track = onImage.GetPixel(HostPad + 10, middle);
				await Assert.That(Near(track, onColor)).IsTrue().Because($"on fills the track with the on colour, got {track}");

				// the pill's corner is rounded: its very corner shows the background
				var corner = onImage.GetPixel(HostPad, HostPad);
				await Assert.That(Near(corner, theme.BackgroundColor, 4)).IsTrue().Because($"the track is a pill, got {corner}");
			});
		}
	}
}
