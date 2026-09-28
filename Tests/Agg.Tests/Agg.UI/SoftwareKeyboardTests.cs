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
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// SoftwareKeyboard, its layer/shift state and the controller that shows it on mobile input profiles.
	[NotInParallel(nameof(InputProfiles))]
	public class SoftwareKeyboardTests
	{
		private static SoftwareKey Key(SoftwareKeyboardState state, SoftwareKeyKind kind, char character = '\0')
		{
			return state.Rows.SelectMany(r => r).First(k => k.Kind == kind && (kind != SoftwareKeyKind.Character || k.Character == character));
		}

		[Test]
		public async Task ShiftIsOneShotAndTheLayerSwitchFlipsPages()
		{
			var state = new SoftwareKeyboardState();

			// An empty text field opens shifted, for a capital first letter, and the shift clears after one letter.
			state.Open(KeyboardInputMode.Text, string.Empty);
			await Assert.That(state.Shifted).IsTrue();
			await Assert.That(state.Press(Key(state, SoftwareKeyKind.Character, 'h'))).IsEqualTo('H');
			await Assert.That(state.Shifted).IsFalse();
			await Assert.That(state.Press(Key(state, SoftwareKeyKind.Character, 'i'))).IsEqualTo('i');

			// A field that already has text opens lower case; shift toggles.
			state.Open(KeyboardInputMode.Text, "abc");
			await Assert.That(state.Shifted).IsFalse();
			await Assert.That(state.Press(Key(state, SoftwareKeyKind.Shift))).IsNull();
			await Assert.That(state.Shifted).IsTrue();
			await Assert.That(Key(state, SoftwareKeyKind.Character, 'q').Label(state)).IsEqualTo("Q");

			// 123 goes to the numbers (dropping shift) and ABC comes back.
			state.Press(Key(state, SoftwareKeyKind.LayerSwitch));
			await Assert.That(state.Layer).IsEqualTo(SoftwareKeyboardLayer.Numbers);
			await Assert.That(state.Shifted).IsFalse();
			await Assert.That(Key(state, SoftwareKeyKind.LayerSwitch).Label(state)).IsEqualTo("ABC");
			await Assert.That(state.Press(Key(state, SoftwareKeyKind.Character, '7'))).IsEqualTo('7');
			state.Press(Key(state, SoftwareKeyKind.LayerSwitch));
			await Assert.That(state.Layer).IsEqualTo(SoftwareKeyboardLayer.Letters);

			// A numeric field opens straight onto the numbers, unshifted even when empty.
			state.Open(KeyboardInputMode.Numeric, string.Empty);
			await Assert.That(state.Layer).IsEqualTo(SoftwareKeyboardLayer.Numbers);
			await Assert.That(state.Shifted).IsFalse();
		}

		[Test]
		public async Task KeysTypeIntoTheTargetLikeAPhysicalKeyboard()
		{
			var root = new GuiWidget(400, 300);
			var field = new TextEditWidget(pixelWidth: 200);
			root.AddChild(field);
			int enterPresses = 0;
			field.EnterPressed += (s, e) => enterPresses++;
			field.Focus();

			var keyboard = new SoftwareKeyboard { Width = 400, Target = field };
			SoftwareKeyboardState state = keyboard.State;
			state.Open(field.KeyboardInputMode, field.Text);
			keyboard.Press(Key(state, SoftwareKeyKind.Character, 'h'));
			keyboard.Press(Key(state, SoftwareKeyKind.Character, 'i'));
			keyboard.Press(Key(state, SoftwareKeyKind.Character, 'x'));
			keyboard.Press(Key(state, SoftwareKeyKind.Backspace));
			keyboard.Press(Key(state, SoftwareKeyKind.Character, ' '));
			keyboard.Press(Key(state, SoftwareKeyKind.LayerSwitch));
			keyboard.Press(Key(state, SoftwareKeyKind.Character, '2'));
			await Assert.That(field.Text).IsEqualTo("Hi 2");

			// Return reaches a single-line field as Enter, not as a typed line break.
			keyboard.Press(Key(state, SoftwareKeyKind.Return));
			await Assert.That(enterPresses).IsEqualTo(1);
			await Assert.That(field.Text).IsEqualTo("Hi 2");

			// Every key of the page has room, and a point on a key finds it.
			foreach (SoftwareKey key in state.Rows.SelectMany(r => r))
			{
				RectangleDouble bounds = keyboard.BoundsOf(key);
				await Assert.That(bounds.Width).IsGreaterThan(10);
				await Assert.That(keyboard.KeyAt(bounds.Center)).IsSameReferenceAs(key);
			}

			keyboard.Style = InputProfile.MobileAndroid;
			keyboard.OnDraw(new ImageBuffer(400, (int)keyboard.Height).NewGraphics2D());
		}

		[Test]
		public async Task TheControllerShowsTheKeyboardOnlyOnMobileProfiles()
		{
			var root = new GuiWidget(400, 300);
			var field = new NumberEdit(0, pixelWidth: 100);
			root.AddChild(field);
			using var controller = new SoftwareKeyboardController();
			try
			{
				// The Desktop default: focusing a field brings nothing up.
				await Assert.That(InputProfiles.Current).IsEqualTo(InputProfile.Desktop);
				controller.Show(field);
				await Assert.That(controller.IsShown).IsFalse();

				InputProfiles.Current = InputProfile.MobileAndroid;
				controller.Show(field);
				await Assert.That(controller.IsShown).IsTrue();
				await Assert.That(controller.Keyboard.Parent).IsSameReferenceAs(root);
				await Assert.That(controller.Keyboard.Target).IsSameReferenceAs(field);
				await Assert.That(controller.Keyboard.Style).IsEqualTo(InputProfile.MobileAndroid);

				// A number edit opens on the numbers.
				await Assert.That(controller.Keyboard.State.Layer).IsEqualTo(SoftwareKeyboardLayer.Numbers);

				// Back to Desktop takes it down.
				InputProfiles.Current = InputProfile.Desktop;
				await Assert.That(controller.IsShown).IsFalse();
				await Assert.That(root.Children.Contains(controller.Keyboard)).IsFalse();
			}
			finally
			{
				InputProfiles.Current = InputProfile.Desktop;
			}
		}
	}
}
