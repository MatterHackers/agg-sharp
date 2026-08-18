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
using System.Threading.Tasks;
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="Keyboard"/> is process-wide static state, so every test here clears it on the way in
	/// and on the way out, and they share the automation suite's parallel key - that suite drives the
	/// same down-state set (TextEditTests holds Shift across a few edits), and a key of our own would
	/// not keep the two apart.
	/// </summary>
	public class KeyboardTests
	{
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task ClearNonModifierKeysReleasesOnlyOrdinaryKeys()
		{
			Keyboard.Clear();
			try
			{
				Keyboard.SetKeyDownState(Keys.A, true);
				Keyboard.SetKeyDownState(Keys.ControlKey, true);

				Keyboard.ClearNonModifierKeys();

				await Assert.That(Keyboard.IsKeyDown(Keys.A)).IsFalse();
				await Assert.That(Keyboard.IsKeyDown(Keys.ControlKey)).IsTrue();

				// SetKeyDownState fans ControlKey out to Control, and the fanned-out flag has to survive
				// too - it is the one the 3D view gestures actually read.
				await Assert.That(Keyboard.IsKeyDown(Keys.Control)).IsTrue();
			}
			finally
			{
				Keyboard.Clear();
			}
		}

		/// <summary>
		/// Callers write the down state they want unconditionally and rely on this to stay quiet when
		/// nothing moved - the mac flags-changed path restates every modifier on every event, including
		/// the events fired for modifiers agg has no key for at all.
		/// </summary>
		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task SetKeyDownStateOnlyAnnouncesRealChanges()
		{
			Keyboard.Clear();

			int stateChangedCount = 0;
			EventHandler countStateChanges = (s, e) => stateChangedCount++;
			Keyboard.StateChanged += countStateChanges;
			try
			{
				Keyboard.SetKeyDownState(Keys.ControlKey, true);
				await Assert.That(stateChangedCount).IsEqualTo(1);

				// Already down, both spellings - nothing moved.
				Keyboard.SetKeyDownState(Keys.ControlKey, true);
				await Assert.That(stateChangedCount).IsEqualTo(1);

				// But a half-set state (what automation writing only Keys.Control leaves behind) is a real
				// change, and has to be repaired and announced rather than dismissed as a no-op.
				Keyboard.SetKeyDownState(Keys.ControlKey, false);
				Keyboard.SetKeyDownState(Keys.Control, true);
				stateChangedCount = 0;

				Keyboard.SetKeyDownState(Keys.ControlKey, true);

				await Assert.That(stateChangedCount).IsEqualTo(1);
				await Assert.That(Keyboard.IsKeyDown(Keys.ControlKey)).IsTrue();
			}
			finally
			{
				Keyboard.StateChanged -= countStateChanges;
				Keyboard.Clear();
			}
		}

		[Test]
		[NotInParallel(nameof(AutomationRunner.ShowWindowAndExecuteTests))]
		public async Task ClearNonModifierKeysWithNothingToReleaseIsSilent()
		{
			Keyboard.Clear();

			int stateChangedCount = 0;
			EventHandler countStateChanges = (s, e) => stateChangedCount++;
			Keyboard.StateChanged += countStateChanges;
			try
			{
				Keyboard.SetKeyDownState(Keys.ShiftKey, true);
				stateChangedCount = 0;

				Keyboard.ClearNonModifierKeys();

				// Nothing but modifiers were down, so nothing changed and nobody should have been told.
				await Assert.That(stateChangedCount).IsEqualTo(0);
				await Assert.That(Keyboard.IsKeyDown(Keys.Shift)).IsTrue();
			}
			finally
			{
				Keyboard.StateChanged -= countStateChanges;
				Keyboard.Clear();
			}
		}
	}
}
