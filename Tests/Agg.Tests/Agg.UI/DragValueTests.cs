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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// DragValue's value logic, driven through the real widget: mouse drags with speed, step and clamp,
	/// a click opening the edit field, and typed text parsed, clamped and committed or cancelled.
	/// </summary>
	public class DragValueTests
	{
		private static MouseEventArgs Mouse(double x) => new MouseEventArgs(MouseButtons.Left, 1, x, 5, 0);

		/// <summary>Presses at pressX, moves to each position in turn, then releases at the last one.</summary>
		private static void Drag(DragValue dragValue, double pressX, params double[] positions)
		{
			dragValue.OnMouseDown(Mouse(pressX));
			foreach (var x in positions)
			{
				dragValue.OnMouseMove(Mouse(x));
			}

			dragValue.OnMouseUp(Mouse(positions.Length > 0 ? positions[^1] : pressX));
		}

		[Test]
		public async Task DragMovesTheValueBySpeedPerDesignUnit()
		{
			var dragValue = new DragValue(10, 0, 100, new ThemeConfig()) { Speed = .5 };
			int changes = 0;
			dragValue.ValueChanged += (s, e) => changes++;

			var scale = GuiWidget.DeviceScale;
			Drag(dragValue, 20 * scale, 25 * scale, 40 * scale);

			// 20 design units right at half a unit each, measured from the press rather than from where
			// the threshold was crossed.
			await Assert.That(dragValue.Value).IsEqualTo(20);
			await Assert.That(changes).IsEqualTo(2);
			await Assert.That(dragValue.IsEditing).IsFalse().Because("a drag is not a click");
			await Assert.That(dragValue.IsDragging).IsFalse();
		}

		[Test]
		public async Task DragClampsToTheRangeAndSnapsToStep()
		{
			var dragValue = new DragValue(50, 0, 60, new ThemeConfig()) { Step = 5 };
			var scale = GuiWidget.DeviceScale;

			Drag(dragValue, 0, 7 * scale);
			await Assert.That(dragValue.Value).IsEqualTo(55).Because("57 snaps to the nearest step of 5");

			Drag(dragValue, 0, 500 * scale);
			await Assert.That(dragValue.Value).IsEqualTo(60);

			Drag(dragValue, 0, -500 * scale);
			await Assert.That(dragValue.Value).IsEqualTo(0);

			await Assert.That(dragValue.ValueForDrag(10, 2.4)).IsEqualTo(10);
		}

		[Test]
		public async Task MovementUnderTheThresholdIsAClickThatOpensTheEditor()
		{
			var dragValue = new DragValue(3.25, 0, 10, new ThemeConfig());
			var scale = GuiWidget.DeviceScale;

			Drag(dragValue, 10 * scale, (10 + DragValue.DragThreshold / 2) * scale);

			await Assert.That(dragValue.Value).IsEqualTo(3.25);
			await Assert.That(dragValue.IsEditing).IsTrue();
			await Assert.That(dragValue.EditField.Visible).IsTrue();
			await Assert.That(dragValue.EditField.Text).IsEqualTo("3.25").Because("the edit text is the bare number");
		}

		[Test]
		public async Task TypedTextIsParsedClampedAndCommitted()
		{
			var dragValue = new DragValue(1, -10, 10, new ThemeConfig()) { Suffix = " mm", Decimals = 1 };
			int changes = 0;
			dragValue.ValueChanged += (s, e) => changes++;

			dragValue.BeginEdit();
			dragValue.EditField.Text = "4.5 mm";
			dragValue.CommitEdit();
			await Assert.That(dragValue.Value).IsEqualTo(4.5);
			await Assert.That(dragValue.DisplayText).IsEqualTo("4.5 mm");
			await Assert.That(dragValue.IsEditing).IsFalse();

			dragValue.BeginEdit();
			dragValue.EditField.Text = "250";
			dragValue.CommitEdit();
			await Assert.That(dragValue.Value).IsEqualTo(10);

			dragValue.BeginEdit();
			dragValue.EditField.Text = "not a number";
			dragValue.CommitEdit();
			await Assert.That(dragValue.Value).IsEqualTo(10).Because("unparsable text leaves the value alone");
			await Assert.That(changes).IsEqualTo(2);
		}

		[Test]
		public async Task EscapeCancelsAndEnterCommits()
		{
			var dragValue = new DragValue(2, 0, 10, new ThemeConfig());

			dragValue.BeginEdit();
			dragValue.EditField.Text = "7";
			dragValue.EditField.InternalTextEditWidget.OnKeyDown(new KeyEventArgs(Keys.Escape));
			await Assert.That(dragValue.IsEditing).IsFalse();
			await Assert.That(dragValue.Value).IsEqualTo(2);

			dragValue.BeginEdit();
			dragValue.EditField.Text = "7";
			dragValue.EditField.InternalTextEditWidget.OnKeyDown(new KeyEventArgs(Keys.Enter));
			await Assert.That(dragValue.IsEditing).IsFalse();
			await Assert.That(dragValue.Value).IsEqualTo(7);
		}

		[Test]
		public async Task ParseIgnoresPrefixSuffixAndUsesAPointForDecimals()
		{
			var dragValue = new DragValue(0, 0, 1, new ThemeConfig()) { Prefix = "x: ", Suffix = "°" };

			await Assert.That(dragValue.TryParseTyped(" x: 12.5° ", out double parsed)).IsTrue();
			await Assert.That(parsed).IsEqualTo(12.5);
			await Assert.That(dragValue.TryParseTyped("-3", out parsed)).IsTrue();
			await Assert.That(parsed).IsEqualTo(-3);
			await Assert.That(dragValue.TryParseTyped("", out _)).IsFalse();
			await Assert.That(dragValue.TryParseTyped("NaN", out _)).IsFalse();
		}

		[Test]
		public async Task NaNIsIgnored()
		{
			var dragValue = new DragValue(4, 0, 10, new ThemeConfig());
			int changes = 0;
			dragValue.ValueChanged += (s, e) => changes++;

			dragValue.Value = double.NaN;

			await Assert.That(dragValue.Value).IsEqualTo(4);
			await Assert.That(changes).IsEqualTo(0);
		}

		[Test]
		public async Task KeyboardOpensTheEditorAndArrowsNudge()
		{
			var dragValue = new DragValue(5, 0, 10, new ThemeConfig()) { Speed = .5 };
			await Assert.That(dragValue.TabStop).IsTrue();

			dragValue.OnKeyDown(new KeyEventArgs(Keys.Up));
			await Assert.That(dragValue.Value).IsEqualTo(5.5).Because("without a Step a key moves by Speed");
			dragValue.OnKeyDown(new KeyEventArgs(Keys.Left));
			await Assert.That(dragValue.Value).IsEqualTo(5);

			dragValue.Step = 2;
			dragValue.OnKeyDown(new KeyEventArgs(Keys.Right));
			await Assert.That(dragValue.Value).IsEqualTo(8).Because("with a Step a key moves by it, snapped");

			dragValue.OnKeyDown(new KeyEventArgs(Keys.Space));
			await Assert.That(dragValue.IsEditing).IsTrue();

			// Up in the open field nudges what is typed, as egui's DragValue does.
			dragValue.EditField.Text = "4";
			dragValue.EditField.InternalTextEditWidget.OnKeyDown(new KeyEventArgs(Keys.Up));
			await Assert.That(dragValue.Value).IsEqualTo(6);
			await Assert.That(dragValue.EditField.Text).IsEqualTo("6.00");

			dragValue.CancelEdit();
			dragValue.OnKeyDown(new KeyEventArgs(Keys.Enter));
			await Assert.That(dragValue.IsEditing).IsTrue();
		}

		[Test]
		public async Task TheBoxFollowsTheTextBothWaysButKeepsAWiderCallerWidth()
		{
			var dragValue = new DragValue(1, 0, 1000000, new ThemeConfig());
			var shortWidth = dragValue.Width;

			dragValue.Value = 123456;
			await Assert.That(dragValue.Width).IsGreaterThan(shortWidth);

			dragValue.Value = 1;
			await Assert.That(dragValue.Width).IsEqualTo(shortWidth).Because("the box shrinks back with the text");

			dragValue.Width = 500;
			dragValue.Value = 2;
			await Assert.That(dragValue.Width).IsEqualTo(500);
		}

		[Test]
		public async Task FocusMovingElsewhereCommitsTheEdit()
		{
			var parent = new GuiWidget(400, 100);
			var dragValue = new DragValue(1, 0, 10, new ThemeConfig());
			var other = new GuiWidget(50, 20) { TabStop = true };
			parent.AddChild(dragValue);
			parent.AddChild(other);

			dragValue.BeginEdit();
			await Assert.That(dragValue.EditField.InternalTextEditWidget.Focused).IsTrue();
			dragValue.EditField.Text = "6";

			other.Focus();

			await Assert.That(dragValue.IsEditing).IsFalse();
			await Assert.That(dragValue.Value).IsEqualTo(6);
		}

		[Test]
		public async Task ConstructionClampsAndProgrammaticSetsRaiseOnlyOnChange()
		{
			var dragValue = new DragValue(500, 0, 100, new ThemeConfig());
			int changes = 0;
			dragValue.ValueChanged += (s, e) => changes++;

			await Assert.That(dragValue.Value).IsEqualTo(100);

			dragValue.Value = 100;
			dragValue.Value = -4;
			await Assert.That(dragValue.Value).IsEqualTo(0);
			await Assert.That(changes).IsEqualTo(1);
			await Assert.That(dragValue.Width).IsGreaterThanOrEqualTo(dragValue.IntrinsicMinimumWidth - .001);
		}
	}
}
