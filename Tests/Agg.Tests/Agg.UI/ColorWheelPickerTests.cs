/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>ColorWheelPicker's ring / triangle / alpha / hex editing, Cancel / Select, and ColorDialog.</summary>
	public class ColorWheelPickerTests
	{
		private static void Press(GuiWidget widget, Vector2 position)
		{
			widget.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, position.X, position.Y, 0));
			widget.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, position.X, position.Y, 0));
		}

		[Test]
		public async Task RingTriangleAndAlphaDragsChangeTheColourLive()
		{
			var picker = new ColorWheelPicker(Color.Red, new ThemeConfig());
			int changes = 0;
			picker.ColorChanged += (s, e) => changes++;

			// Straight up the ring is hue 90; red's full saturation and value carry over.
			var ringMiddle = (picker.RingInnerRadius + picker.RingOuterRadius) / 2;
			Press(picker, picker.WheelCenter + new Vector2(0, ringMiddle));
			await Assert.That(System.Math.Abs(picker.Hue - 90)).IsLessThan(1e-6);
			await Assert.That(picker.Color).IsEqualTo(HsvColor.FromHsv(90, 1, 1));

			// The triangle's white corner.
			var white = ColorWheelMath.PointAt(0, 1, picker.WheelCenter, picker.TriangleRadius, picker.Hue);
			Press(picker, white + (picker.WheelCenter - white) * .001);
			await Assert.That(picker.Color.red).IsGreaterThan((byte)250);
			await Assert.That(picker.Color.blue).IsGreaterThan((byte)250);

			var alpha = picker.AlphaBounds;
			Press(picker, new Vector2(alpha.Left, alpha.Center.Y));
			await Assert.That(picker.Color.alpha).IsEqualTo((byte)0);
			await Assert.That(picker.HexField.Text.Length).IsEqualTo(9).Because("a translucent colour shows #RRGGBBAA");
			await Assert.That(changes).IsEqualTo(3);
		}

		[Test]
		public async Task TypingAHexSetsTheColour()
		{
			var picker = new ColorWheelPicker(Color.Red, new ThemeConfig());
			int changes = 0;
			picker.ColorChanged += (s, e) => changes++;

			picker.HexField.Text = "#12";
			await Assert.That(picker.Color).IsEqualTo(Color.Red).Because("a half-typed hex leaves the colour alone");

			picker.HexField.Text = "#00FF0080";
			await Assert.That(picker.Color).IsEqualTo(new Color(0, 255, 0, 128));
			await Assert.That(changes).IsEqualTo(1);
		}

		[Test]
		public async Task CancelPutsBackTheOriginalAndSelectCommits()
		{
			var picker = new ColorWheelPicker(Color.Red, new ThemeConfig());
			var events = new List<string>();
			picker.ColorChanged += (s, e) => events.Add("changed " + HsvColor.ToHex(picker.Color));
			picker.Selected += (s, e) => events.Add("selected " + HsvColor.ToHex(picker.Color));
			picker.Canceled += (s, e) => events.Add("canceled");

			picker.Color = Color.Blue;
			picker.SelectButton.InvokeClick();
			picker.CancelButton.InvokeClick();

			await Assert.That(picker.Color).IsEqualTo(Color.Red);
			await Assert.That(picker.HexField.Text).IsEqualTo("#FF0000");
			await Assert.That(string.Join(", ", events)).IsEqualTo("changed #0000FF, selected #0000FF, changed #FF0000, canceled");
		}

		[Test]
		public async Task NoColorIsPassThroughUntilTheColourIsEdited()
		{
			var picker = new ColorWheelPicker(Color.Red, new ThemeConfig(), allowNone: true);
			await Assert.That(picker.PassThrough).IsFalse();

			picker.NoColorCheckBox.Checked = true;
			await Assert.That(picker.PassThrough).IsTrue();
			await Assert.That(picker.Color).IsEqualTo(Color.Transparent);

			var ringMiddle = (picker.RingInnerRadius + picker.RingOuterRadius) / 2;
			Press(picker, picker.WheelCenter + new Vector2(ringMiddle, 0));
			await Assert.That(picker.PassThrough).IsFalse();
			await Assert.That(picker.NoColorCheckBox.Checked).IsFalse();
			await Assert.That(picker.Color).IsEqualTo(Color.Red);

			// A transparent starting colour starts ticked.
			var none = new ColorWheelPicker(Color.Transparent, new ThemeConfig(), allowNone: true);
			await Assert.That(none.PassThrough).IsTrue();
		}

		[Test]
		public async Task DialogClosesOnSelectAndCancelsWhenDismissed()
		{
			var host = new GuiWidget(800, 600);
			var overlay = new ModalOverlay();
			host.AddChild(overlay);

			var picker = new ColorWheelPicker(Color.Red, new ThemeConfig());
			var selected = 0;
			picker.Selected += (s, e) => selected++;
			var dialog = new ColorDialog(picker, new ThemeConfig());
			overlay.Push(dialog);
			host.PerformLayout();
			await Assert.That(dialog.Width).IsGreaterThan(picker.Width);
			await Assert.That(dialog.Height).IsGreaterThan(picker.Height).Because("the title bar sits above the picker");

			picker.Color = Color.Blue;
			picker.SelectButton.InvokeClick();
			await Assert.That(selected).IsEqualTo(1);
			await Assert.That(overlay.Layers.Count).IsEqualTo(0);
			await Assert.That(picker.Color).IsEqualTo(Color.Blue).Because("Select keeps the colour");

			// Escape on the overlay (like × and a press outside) is Cancel: the live colour goes back.
			var second = new ColorWheelPicker(Color.Red, new ThemeConfig());
			var canceled = 0;
			second.Canceled += (s, e) => canceled++;
			overlay.Push(new ColorDialog(second, new ThemeConfig()));
			second.Color = Color.Blue;
			overlay.OnKeyDown(new KeyEventArgs(Keys.Escape));
			await Assert.That(overlay.Layers.Count).IsEqualTo(0);
			await Assert.That(canceled).IsEqualTo(1);
			await Assert.That(second.Color).IsEqualTo(Color.Red);

			// The title bar's × closes a floating (not overlaid) dialog the same way.
			var third = new ColorWheelPicker(Color.Red, new ThemeConfig());
			var floating = new ColorDialog(third, new ThemeConfig());
			host.AddChild(floating);
			third.Color = Color.Green;
			floating.CloseDialog();
			await Assert.That(floating.Parent).IsNull();
			await Assert.That(third.Color).IsEqualTo(Color.Red);
		}

		[Test]
		public async Task PressOutsideCancelsUnlessTheHostDecides()
		{
			var host = new GuiWidget(800, 600);
			var overlay = new ModalOverlay();
			host.AddChild(overlay);
			host.PerformLayout();

			// By default a press outside is Cancel, like Escape and ×.
			var picker = new ColorWheelPicker(Color.Red, new ThemeConfig());
			var dialog = new ColorDialog(picker, new ThemeConfig());
			overlay.Push(dialog);
			host.PerformLayout();
			picker.Color = Color.Blue;
			Press(overlay, new Vector2(2, 2));
			await Assert.That(overlay.Layers.Count).IsEqualTo(0);
			await Assert.That(dialog.ClosedByOutsidePress).IsTrue();
			await Assert.That(picker.Color).IsEqualTo(Color.Red);

			// With CancelOnOutsidePress off the colour stays, and Closed can tell the press from Escape.
			var second = new ColorWheelPicker(Color.Red, new ThemeConfig());
			var canceled = 0;
			second.Canceled += (s, e) => canceled++;
			var keeping = new ColorDialog(second, new ThemeConfig()) { CancelOnOutsidePress = false };
			bool? outsideAtClose = null;
			keeping.Closed += (s, e) => outsideAtClose = keeping.ClosedByOutsidePress;
			overlay.Push(keeping);
			host.PerformLayout();
			second.Color = Color.Blue;
			Press(overlay, new Vector2(2, 2));
			await Assert.That(outsideAtClose).IsEqualTo(true);
			await Assert.That(canceled).IsEqualTo(0);
			await Assert.That(second.Color).IsEqualTo(Color.Blue);

			// Escape still cancels, and is not an outside press.
			var third = new ColorWheelPicker(Color.Red, new ThemeConfig());
			var escaped = new ColorDialog(third, new ThemeConfig()) { CancelOnOutsidePress = false };
			overlay.Push(escaped);
			third.Color = Color.Blue;
			overlay.OnKeyDown(new KeyEventArgs(Keys.Escape));
			await Assert.That(escaped.ClosedByOutsidePress).IsFalse();
			await Assert.That(third.Color).IsEqualTo(Color.Red);
			await Assert.That(overlay.ClosingByOutsidePress).IsFalse();
		}

		[Test]
		public async Task PanelIsTheWindowFillOutlinedInTheWidgetStroke()
		{
			var theme = new ThemeConfig { BackgroundColor = new Color(10, 20, 30) };
			theme.EditFieldColors.Inactive.BorderColor = new Color(200, 100, 50);
			var picker = new ColorWheelPicker(Color.Red, theme);
			var image = new ImageBuffer((int)picker.Width, (int)picker.Height);
			picker.OnDraw(image.NewGraphics2D());

			// Inside the left padding, clear of the wheel and the rounded corners.
			await Assert.That(image.GetPixel(5, (int)(picker.Height / 2))).IsEqualTo(new Color(10, 20, 30));
			await Assert.That(image.GetPixel(0, (int)(picker.Height / 2)).red).IsGreaterThan((byte)100).Because("the border is the widget stroke");
		}
	}
}
