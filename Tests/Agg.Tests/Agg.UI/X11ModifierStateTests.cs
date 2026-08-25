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

using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Linux;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// X11 puts a modifier mask on every input event, and that word is the only thing that says which
	/// modifiers are held. These cover the translation from it to agg's keys, the one-event-behind
	/// correction a bare modifier key needs, and - the reason any of it exists - that losing the focus
	/// releases exactly what this window put down and nothing else.
	/// </summary>
	public class X11ModifierStateTests
	{
		[Test]
		public async Task NoModifiersHoldNothing()
		{
			await AssertDownStateKeys(0);
		}

		[Test]
		public async Task EachModifierMapsToItsPhysicalKey()
		{
			await AssertDownStateKeys(X11.ShiftMask, Keys.ShiftKey);
			await AssertDownStateKeys(X11.ControlMask, Keys.ControlKey);
			await AssertDownStateKeys(X11.Mod1Mask, Keys.Menu);
		}

		[Test]
		public async Task ChordsCombine()
		{
			await AssertDownStateKeys(X11.ControlMask | X11.ShiftMask, Keys.ControlKey, Keys.ShiftKey);
			await AssertDownStateKeys(X11.ControlMask | X11.Mod1Mask, Keys.ControlKey, Keys.Menu);
		}

		/// <summary>
		/// Caps Lock, Num Lock, Super and AltGr all ride in the same word and none of them is a modifier agg
		/// knows. Mistaking one for a modifier it does know would make a user with Caps Lock on unable to
		/// click on anything normally.
		/// </summary>
		[Test]
		public async Task TheModifiersAggHasNoKeyForAreIgnored()
		{
			await AssertDownStateKeys(X11.LockMask);
			await AssertDownStateKeys(X11.Mod2Mask);
			await AssertDownStateKeys(X11.Mod4Mask);
			await AssertDownStateKeys(X11.Mod5Mask);

			// And they must not disturb the ones it does.
			await AssertDownStateKeys(X11.LockMask | X11.Mod2Mask | X11.ShiftMask, Keys.ShiftKey);
		}

		/// <summary>The button half of the state word is not modifiers at all.</summary>
		[Test]
		public async Task HeldButtonsAreNotModifiers()
		{
			await AssertDownStateKeys(X11.Button1Mask | X11.Button2Mask | X11.Button3Mask);
		}

		[Test]
		public async Task TheFlagFormAgreesWithTheDownStateKeys()
		{
			await Assert.That(X11SystemWindow.TranslateModifiers(0)).IsEqualTo(Keys.None);
			await Assert.That(X11SystemWindow.TranslateModifiers(X11.ShiftMask)).IsEqualTo(Keys.Shift);
			await Assert.That(X11SystemWindow.TranslateModifiers(X11.ControlMask)).IsEqualTo(Keys.Control);
			await Assert.That(X11SystemWindow.TranslateModifiers(X11.Mod1Mask)).IsEqualTo(Keys.Alt);
			await Assert.That(X11SystemWindow.TranslateModifiers(X11.ControlMask | X11.ShiftMask))
				.IsEqualTo(Keys.Control | Keys.Shift);
			await Assert.That(X11SystemWindow.TranslateModifiers(X11.ControlMask | X11.Mod1Mask))
				.IsEqualTo(Keys.Control | Keys.Alt);
		}

		/// <summary>
		/// X11's state word is the state <em>before</em> the event, so the KeyPress of Shift carries no
		/// ShiftMask and its KeyRelease carries one. Applying it straight would report every modifier
		/// inverted for as long as it is held - Shift down reads as Shift up, and letting go reads as
		/// pressing it.
		/// </summary>
		[Test]
		public async Task AModifierPressAndReleaseAreCorrectedForThemselves()
		{
			await Assert.That(X11SystemWindow.StateAfterModifierKey(0, X11.XK_Shift_L, pressed: true))
				.IsEqualTo(X11.ShiftMask);
			await Assert.That(X11SystemWindow.StateAfterModifierKey(X11.ShiftMask, X11.XK_Shift_L, pressed: false))
				.IsEqualTo(0u);

			await Assert.That(X11SystemWindow.StateAfterModifierKey(0, X11.XK_Control_R, pressed: true))
				.IsEqualTo(X11.ControlMask);
			await Assert.That(X11SystemWindow.StateAfterModifierKey(0, X11.XK_Alt_L, pressed: true))
				.IsEqualTo(X11.Mod1Mask);
		}

		/// <summary>
		/// Letting go of the left Shift while the right one is still held must leave Shift held. Both keys
		/// carry the one mask, so the correction has to be by mask and not by physical key.
		/// </summary>
		[Test]
		public async Task AnOrdinaryKeyIsNotCorrectedAndKeepsWhatIsHeld()
		{
			await Assert.That(X11SystemWindow.StateAfterModifierKey(X11.ShiftMask, X11.XK_a, pressed: true))
				.IsEqualTo(X11.ShiftMask);

			// The buttons ride along in the same word and are not part of the modifier state.
			await Assert.That(X11SystemWindow.StateAfterModifierKey(X11.ShiftMask | X11.Button1Mask, X11.XK_a, pressed: true))
				.IsEqualTo(X11.ShiftMask);
		}

		/// <summary>
		/// The event a bare modifier press produces has to say the modifier is held, and its release has to
		/// say it is not - which is the opposite of what X11's own state word says on both, that word being
		/// the state before its own event. A widget watching <c>OnKeyDown</c> to learn that Shift went down
		/// would otherwise be told Shift is up at the exact moment it was pressed, and told it is down at the
		/// moment it was let go.
		/// </summary>
		[Test]
		public async Task ABareModifierEventReportsTheModifierItIs()
		{
			// The press, as HandleKeyPress builds it: from the state corrected for this event, not the raw
			// state word (which carries no ShiftMask, Shift not having been down when it was sampled).
			uint statePressed = X11SystemWindow.StateAfterModifierKey(0, X11.XK_Shift_L, pressed: true);
			KeyEventArgs shiftDown = X11SystemWindow.MakeKeyEventArgs(X11.XK_Shift_L, statePressed);

			await Assert.That(shiftDown.KeyCode).IsEqualTo(Keys.ShiftKey);
			await Assert.That(shiftDown.Shift).IsTrue();

			// The release, as HandleKeyRelease builds it. The raw state word here *does* carry ShiftMask.
			uint stateReleased = X11SystemWindow.StateAfterModifierKey(X11.ShiftMask, X11.XK_Shift_L, pressed: false);
			KeyEventArgs shiftUp = X11SystemWindow.MakeKeyEventArgs(X11.XK_Shift_L, stateReleased);

			await Assert.That(shiftUp.KeyCode).IsEqualTo(Keys.ShiftKey);
			await Assert.That(shiftUp.Shift).IsFalse();

			// Control and Alt take the same path.
			await Assert.That(X11SystemWindow.MakeKeyEventArgs(
				X11.XK_Control_L,
				X11SystemWindow.StateAfterModifierKey(0, X11.XK_Control_L, pressed: true)).Control).IsTrue();
			await Assert.That(X11SystemWindow.MakeKeyEventArgs(
				X11.XK_Alt_L,
				X11SystemWindow.StateAfterModifierKey(0, X11.XK_Alt_L, pressed: true)).Alt).IsTrue();
		}

		/// <summary>
		/// The correction must not disturb the rest of a chord: letting go of Shift while Control is still
		/// held reports a ShiftKey up that still carries Control, because Control genuinely still is held.
		/// </summary>
		[Test]
		public async Task ReleasingOneModifierOfAChordKeepsTheOther()
		{
			uint stateAfter = X11SystemWindow.StateAfterModifierKey(
				X11.ShiftMask | X11.ControlMask,
				X11.XK_Shift_L,
				pressed: false);

			KeyEventArgs shiftUp = X11SystemWindow.MakeKeyEventArgs(X11.XK_Shift_L, stateAfter);

			await Assert.That(shiftUp.KeyCode).IsEqualTo(Keys.ShiftKey);
			await Assert.That(shiftUp.Shift).IsFalse();
			await Assert.That(shiftUp.Control).IsTrue();
		}

		/// <summary>
		/// The whole point of the exercise: what the state word implies has to end up in
		/// <see cref="Keyboard"/>, because that is where the 3D view's drag gestures read it from.
		/// </summary>
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task ModifiersReachKeyboardDownStateAndComeBackOut()
		{
			Keyboard.Clear();
			try
			{
				X11SystemWindow.ApplyModifierFlagsToKeyboard(X11.ControlMask | X11.ShiftMask | X11.Mod1Mask);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsTrue();
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsTrue();
				await Assert.That(Keyboard.IsKeyDown(Keys.Alt)).IsTrue();

				X11SystemWindow.ApplyModifierFlagsToKeyboard(X11.ControlMask);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsTrue();
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.Alt)).IsFalse();

				X11SystemWindow.ApplyModifierFlagsToKeyboard(0);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsFalse();
			}
			finally
			{
				Keyboard.Clear();
			}
		}

		/// <summary>
		/// Losing the focus releases only what this window applied. <see cref="Keyboard"/> is process wide
		/// and other callers write to it directly - an automation run sets Shift down and then shift-clicks -
		/// so a blunt <c>Keyboard.Clear()</c> here turns any incidental focus change into a dropped selection
		/// with no visible cause.
		/// </summary>
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task FocusLossReleasesOnlyWhatThisWindowApplied()
		{
			Keyboard.Clear();
			try
			{
				// What this window applied, from a real event: Control, and only Control.
				IReadOnlySet<Keys> applied = X11SystemWindow.ApplyModifierFlagsToKeyboard(X11.ControlMask);

				// What somebody else put there afterwards - an automation run that sets Shift down and then
				// shift-clicks. This window never heard about it, and losing the focus must not undo it.
				Keyboard.SetKeyDownState(Keys.ShiftKey, true);

				await Assert.That(X11SystemWindow.ReleaseAppliedModifierKeys(applied)).IsEqualTo(0u);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.ControlKey)).IsFalse();

				// Still held, because this window is not what put it down.
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsTrue();
			}
			finally
			{
				Keyboard.Clear();
			}
		}

		/// <summary>Releasing what was applied when nothing was is not an error and touches nothing.</summary>
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task ReleasingAnEmptySetIsHarmless()
		{
			Keyboard.Clear();
			try
			{
				IReadOnlySet<Keys> applied = X11SystemWindow.ApplyModifierFlagsToKeyboard(0);

				Keyboard.SetKeyDownState(Keys.ShiftKey, true);

				X11SystemWindow.ReleaseAppliedModifierKeys(applied);

				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsTrue();
			}
			finally
			{
				Keyboard.Clear();
			}
		}

		private static async Task AssertDownStateKeys(uint state, params Keys[] expected)
		{
			IReadOnlySet<Keys> downKeys = X11SystemWindow.ModifierDownStateKeys(state);

			await Assert.That(downKeys.Count).IsEqualTo(expected.Length);

			foreach (Keys key in expected)
			{
				await Assert.That(downKeys.Contains(key)).IsTrue();
			}
		}
	}
}
