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

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>ColorPicker open/drag/cancel/select and when ColorChanged and Selected fire.</summary>
	public class ColorPickerTests
	{
		private static void Drag(ColorPicker picker, double x, double y)
		{
			picker.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
			picker.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
		}

		[Test]
		public async Task SwatchClickOpensAndSvDragPicksTheColour()
		{
			var picker = new ColorPicker(Color.Red, new ThemeConfig());
			int changes = 0;
			picker.ColorChanged += (s, e) => changes++;
			var closedHeight = picker.Height;

			var swatch = picker.SwatchBounds.Center;
			Drag(picker, swatch.X, swatch.Y);
			await Assert.That(picker.IsOpen).IsTrue();
			await Assert.That(picker.Height > closedHeight).IsTrue().Because("the panel pushes siblings down");

			// Bottom-left of the square is black whatever the hue; top-right is the pure hue.
			var sv = picker.SaturationValueBounds;
			Drag(picker, sv.Left, sv.Bottom);
			await Assert.That(picker.Color).IsEqualTo(Color.Black);
			Drag(picker, sv.Right, sv.Top);
			await Assert.That(picker.Color).IsEqualTo(Color.Red);

			// Hue at the middle of the bar is cyan.
			var hue = picker.HueBounds;
			Drag(picker, hue.Center.X, hue.Center.Y);
			await Assert.That(picker.Color).IsEqualTo(new Color(0, 255, 255));

			var alpha = picker.AlphaBounds;
			Drag(picker, alpha.Left, alpha.Center.Y);
			await Assert.That(picker.Color.alpha).IsEqualTo((byte)0);
			await Assert.That(changes).IsEqualTo(4);
		}

		[Test]
		public async Task SelectCommitsOnceAndCloses()
		{
			var picker = new ColorPicker(Color.Red, new ThemeConfig());
			var selected = new List<Color>();
			picker.Selected += (s, e) => selected.Add(picker.Color);
			var closedHeight = picker.Height;

			picker.Open();
			var sv = picker.SaturationValueBounds;
			Drag(picker, sv.Left, sv.Bottom);
			await Assert.That(selected.Count).IsEqualTo(0).Because("dragging is live ColorChanged, not a commit");

			picker.SelectButton.InvokeClick();

			await Assert.That(picker.IsOpen).IsFalse();
			await Assert.That(picker.Height).IsEqualTo(closedHeight);
			await Assert.That(selected.Count).IsEqualTo(1);
			await Assert.That(selected[0]).IsEqualTo(Color.Black);

			picker.Open();
			picker.Cancel();
			await Assert.That(selected.Count).IsEqualTo(1).Because("Cancel is not a commit");
		}

		[Test]
		public async Task CancelRestoresTheOpeningColour()
		{
			var picker = new ColorPicker(Color.Red, new ThemeConfig());
			picker.Open();
			var sv = picker.SaturationValueBounds;
			Drag(picker, sv.Left, sv.Bottom);
			int changes = 0;
			picker.ColorChanged += (s, e) => changes++;

			picker.OnKeyDown(new KeyEventArgs(Keys.Escape));

			await Assert.That(picker.IsOpen).IsFalse();
			await Assert.That(picker.Color).IsEqualTo(Color.Red);
			await Assert.That(changes).IsEqualTo(1);
		}

		[Test]
		public async Task CancelAfterAHueDragOnBlackRestoresTheHue()
		{
			// The hue moves while black stays black, so the colour never changes - Cancel must still put
			// the hue marker back.
			var picker = new ColorPicker(Color.Black, new ThemeConfig());
			picker.Open();
			var hue = picker.HueBounds;
			Drag(picker, hue.Center.X, hue.Center.Y);
			await Assert.That(picker.Hue > 100).IsTrue();

			picker.Cancel();

			await Assert.That(picker.Hue).IsEqualTo(0.0);
			await Assert.That(picker.Color).IsEqualTo(Color.Black);
		}

		[Test]
		public async Task CancelBackToAGreyRestoresItsHsvExactly()
		{
			var picker = new ColorPicker(Color.Blue, new ThemeConfig());
			picker.Color = Color.Gray;
			var savedHue = picker.Hue;
			var savedValue = picker.Value;

			picker.Open();
			var hue = picker.HueBounds;
			Drag(picker, hue.Left, hue.Center.Y);
			var sv = picker.SaturationValueBounds;
			Drag(picker, sv.Right, sv.Top);
			await Assert.That(picker.Color).IsEqualTo(Color.Red);

			picker.Cancel();

			await Assert.That(picker.Color).IsEqualTo(Color.Gray);
			await Assert.That(picker.Hue).IsEqualTo(savedHue).Because("the grey was being shown at hue 240");
			await Assert.That(picker.Saturation).IsEqualTo(0.0);
			await Assert.That(picker.Value).IsEqualTo(savedValue);
		}

		[Test]
		public async Task HexReadoutStaysCentredWhenItsTextChanges()
		{
			var picker = new ColorPicker(Color.Red, new ThemeConfig());
			picker.Open();
			var alpha = picker.AlphaBounds;

			// Opaque shows #RRGGBB; translucent adds two digits, so the text widens and must recentre.
			Drag(picker, alpha.Left + alpha.Width / 2, alpha.Center.Y);

			var hex = picker.HexText;
			await Assert.That(hex.Text).IsEqualTo(HsvColor.ToHex(picker.Color));
			await Assert.That(hex.Text.Length).IsEqualTo(9);
			await Assert.That(hex.Width >= hex.Printer.GetSize(hex.Text).X - .5).IsTrue().Because("the readout must fit the longer text");
			var centre = hex.Position.X + hex.Width / 2;
			await Assert.That(Math.Abs(centre - picker.Width / 2) <= 1).IsTrue();
		}

		[Test]
		public async Task ProgrammaticSetRaisesOnlyOnChangeAndGreyKeepsHue()
		{
			var picker = new ColorPicker(Color.Blue, new ThemeConfig());
			int changes = 0;
			picker.ColorChanged += (s, e) => changes++;

			picker.Color = Color.Blue;
			picker.Color = Color.Gray;

			await Assert.That(changes).IsEqualTo(1);
			await Assert.That(picker.Hue).IsEqualTo(240.0);
			await Assert.That(picker.Saturation).IsEqualTo(0.0);
		}
	}
}
