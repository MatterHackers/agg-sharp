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
using MatterHackers.Agg.Platform.Mac;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using static MatterHackers.Agg.Platform.Mac.AppKitConstants;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// macOS reports a bare modifier press only through flagsChanged, and the flags word it carries is
	/// the only thing that says which modifiers are held. These cover the translation from that word to
	/// agg's keys - in particular that Command and physical Control both land on agg's Control, which is
	/// why the state has to be derived from the whole word rather than tracked per physical key.
	/// </summary>
	public class MacModifierFlagsTests
	{
		[Test]
		public async Task NoFlagsHoldsNothing()
		{
			await AssertDownStateKeys(0);
		}

		[Test]
		public async Task CommandAloneIsControl()
		{
			await AssertDownStateKeys(NSEventModifierFlagCommand, Keys.ControlKey);
		}

		[Test]
		public async Task PhysicalControlAloneIsControl()
		{
			await AssertDownStateKeys(NSEventModifierFlagControl, Keys.ControlKey);
		}

		[Test]
		public async Task CommandAndControlTogetherAreStillOneControl()
		{
			// Both map onto the same agg key, so holding both must be indistinguishable from holding one -
			// and releasing either one on its own must leave Control held, which is exactly what deriving
			// the state from the flags word (rather than from per-key downs and ups) buys.
			await AssertDownStateKeys(NSEventModifierFlagCommand | NSEventModifierFlagControl, Keys.ControlKey);
			await AssertDownStateKeys(NSEventModifierFlagControl, Keys.ControlKey);
			await AssertDownStateKeys(NSEventModifierFlagCommand, Keys.ControlKey);
		}

		[Test]
		public async Task ShiftIsShiftKeyAndOptionIsMenu()
		{
			await AssertDownStateKeys(NSEventModifierFlagShift, Keys.ShiftKey);
			await AssertDownStateKeys(NSEventModifierFlagOption, Keys.Menu);
		}

		[Test]
		public async Task CommandWithShiftIsThePanChord()
		{
			await AssertDownStateKeys(
				NSEventModifierFlagCommand | NSEventModifierFlagShift,
				Keys.ControlKey,
				Keys.ShiftKey);
		}

		[Test]
		public async Task CommandWithOptionIsTheZoomChord()
		{
			await AssertDownStateKeys(
				NSEventModifierFlagCommand | NSEventModifierFlagOption,
				Keys.ControlKey,
				Keys.Menu);
		}

		[Test]
		public async Task CapsLockIsNotAModifierAggKnows()
		{
			// Caps lock arrives as a flags change like any other modifier; agg has no key for it and must
			// not mistake it for one.
			await AssertDownStateKeys(NSEventModifierFlagCapsLock);
		}

		[Test]
		public async Task ModifierKeysAgreeWithTheDownStateKeys()
		{
			await Assert.That(MacSystemWindow.TranslateModifiers(0)).IsEqualTo(Keys.None);
			await Assert.That(MacSystemWindow.TranslateModifiers(NSEventModifierFlagCommand)).IsEqualTo(Keys.Control);
			await Assert.That(MacSystemWindow.TranslateModifiers(NSEventModifierFlagControl)).IsEqualTo(Keys.Control);
			await Assert.That(MacSystemWindow.TranslateModifiers(NSEventModifierFlagCommand | NSEventModifierFlagControl)).IsEqualTo(Keys.Control);
			await Assert.That(MacSystemWindow.TranslateModifiers(NSEventModifierFlagShift)).IsEqualTo(Keys.Shift);
			await Assert.That(MacSystemWindow.TranslateModifiers(NSEventModifierFlagOption)).IsEqualTo(Keys.Alt);
			await Assert.That(MacSystemWindow.TranslateModifiers(NSEventModifierFlagCommand | NSEventModifierFlagShift))
				.IsEqualTo(Keys.Control | Keys.Shift);
			await Assert.That(MacSystemWindow.TranslateModifiers(NSEventModifierFlagCommand | NSEventModifierFlagOption))
				.IsEqualTo(Keys.Control | Keys.Alt);
		}

		/// <summary>
		/// The whole point of the exercise: what the flags word implies has to end up in
		/// <see cref="Keyboard"/>, because that is where the 3D view's drag gestures read it from.
		/// </summary>
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task FlagsReachKeyboardDownStateAndComeBackOut()
		{
			Keyboard.Clear();
			try
			{
				ApplyFlags(NSEventModifierFlagCommand | NSEventModifierFlagShift | NSEventModifierFlagOption);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsTrue();
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsTrue();
				await Assert.That(Keyboard.IsKeyDown(Keys.Alt)).IsTrue();

				// Letting go of Command while Control is still held must leave Control down.
				ApplyFlags(NSEventModifierFlagControl);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsTrue();
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.Alt)).IsFalse();

				ApplyFlags(0);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.Alt)).IsFalse();
			}
			finally
			{
				Keyboard.Clear();
			}
		}

		/// <summary>
		/// Losing focus has to release the modifiers this window put down - macOS delivers flagsChanged
		/// only to the key window, so a modifier released while another application is frontmost is never
		/// reported to us and would stay latched forever (the Cmd-Tab-away-and-back case) - and has to
		/// release nothing else. <see cref="Keyboard"/> is process-wide and an automation run writes to it
		/// directly, so a blunt clear here turns an incidental focus change into a mid-test flake.
		/// </summary>
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task LosingFocusReleasesOnlyTheModifiersItPutDown()
		{
			Keyboard.Clear();
			try
			{
				IReadOnlySet<Keys> applied = MacSystemWindow.ApplyModifierFlagsToKeyboard(NSEventModifierFlagControl);

				// Not ours: what a test that pokes Shift straight in and then shift-clicks leaves behind,
				// and an ordinary key some other caller latched. Neither came from a flags change.
				Keyboard.SetKeyDownState(Keys.Shift, true);
				Keyboard.SetKeyDownState(Keys.A, true);

				ulong remembered = MacSystemWindow.ReleaseAppliedModifierKeys(applied);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.ControlKey)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsTrue();
				await Assert.That(Keyboard.IsKeyDown(Keys.A)).IsTrue();

				// The word the next flags change computes its transitions against. A stale one here would
				// make the Command-dropped detection fire on nothing the user did.
				await Assert.That(remembered).IsEqualTo(0UL);
			}
			finally
			{
				Keyboard.Clear();
			}
		}

		/// <summary>
		/// Regaining focus has to re-derive the down state from the live flags word, because a latch left
		/// over from before (or a modifier the user is genuinely still holding as focus returns) is the
		/// only state there is - no flags change arrived while the window was not key.
		/// </summary>
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task RegainingFocusCorrectsAStaleLatch()
		{
			Keyboard.Clear();
			try
			{
				// A latch no flags change put there, which is exactly what this path exists to correct.
				// Seeded as the fanned-out Keys.Control and not Keys.ControlKey because that is the
				// spelling an automation run leaves behind - AggInputMethods writes only Keys.Control, so
				// a run that threw between PressModifierKeys and ReleaseModifierKeys latches that one and
				// leaves Keys.ControlKey up. Keys.Control is also the flag the 3D view gestures read.
				Keyboard.SetKeyDownState(Keys.Control, true);

				ApplyFlags(0);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsFalse();

				// And the other direction: focus can return with Command genuinely still held.
				ApplyFlags(NSEventModifierFlagCommand);

				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsTrue();
			}
			finally
			{
				Keyboard.Clear();
			}
		}

		/// <summary>
		/// What HandleFlagsChanged does with the translation, minus the NSEvent it cannot be given here.
		/// </summary>
		private static void ApplyFlags(ulong flags) => MacSystemWindow.ApplyModifierFlagsToKeyboard(flags);

		private static async Task AssertDownStateKeys(ulong flags, params Keys[] expected)
		{
			IReadOnlySet<Keys> actual = MacSystemWindow.ModifierDownStateKeys(flags);

			await Assert.That(actual.Count).IsEqualTo(expected.Length);
			foreach (Keys key in expected)
			{
				await Assert.That(actual.Contains(key)).IsTrue();
			}
		}
	}
}
