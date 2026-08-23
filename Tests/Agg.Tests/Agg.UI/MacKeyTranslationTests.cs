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

using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Mac;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

using static MatterHackers.Agg.Platform.Mac.AppKitConstants;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A keyDown carries a hardware key position, not a letter, so every letter and digit shortcut has to
	/// be resolved from the layout-resolved characters instead. Without that, Cmd+S arrived as a bare
	/// Control modifier with no key attached and no shortcut in the application could ever match it.
	/// </summary>
	public class MacKeyTranslationTests
	{
		/// <summary>The US-layout hardware position of S; on another layout it is another letter, which is
		/// exactly why a key code cannot be what a letter shortcut is matched on.</summary>
		private const ushort VkSOnUsLayout = 0x01;

		[Test]
		public async Task CommandSIsControlS()
		{
			KeyEventArgs keyEvent = MacSystemWindow.MakeKeyEventArgs(VkSOnUsLayout, "s", NSEventModifierFlagCommand);

			await Assert.That(keyEvent.KeyCode).IsEqualTo(Keys.S);
			await Assert.That(keyEvent.Control).IsTrue();
		}

		[Test]
		public async Task PhysicalControlSIsAlsoControlS()
		{
			KeyEventArgs keyEvent = MacSystemWindow.MakeKeyEventArgs(VkSOnUsLayout, "s", NSEventModifierFlagControl);

			await Assert.That(keyEvent.KeyCode).IsEqualTo(Keys.S);
			await Assert.That(keyEvent.Control).IsTrue();
		}

		/// <summary>The everyday shortcuts, each of which was equally dead before the letters resolved.</summary>
		[Test]
		[Arguments("z", Keys.Z)]
		[Arguments("y", Keys.Y)]
		[Arguments("a", Keys.A)]
		[Arguments("c", Keys.C)]
		[Arguments("v", Keys.V)]
		[Arguments("x", Keys.X)]
		public async Task CommandLetterShortcutsResolve(string characters, Keys expected)
		{
			KeyEventArgs keyEvent = MacSystemWindow.MakeKeyEventArgs(0, characters, NSEventModifierFlagCommand);

			await Assert.That(keyEvent.KeyCode).IsEqualTo(expected);
			await Assert.That(keyEvent.Control).IsTrue();
		}

		/// <summary>
		/// charactersIgnoringModifiers drops Command and Option but keeps Shift, so a shifted key arrives
		/// spelled differently while WinForms reports the same key code either way.
		/// </summary>
		[Test]
		public async Task ShiftedSpellingsShareTheirKeyCode()
		{
			KeyEventArgs shiftedZ = MacSystemWindow.MakeKeyEventArgs(
				0,
				"Z",
				NSEventModifierFlagCommand | NSEventModifierFlagShift);

			await Assert.That(shiftedZ.KeyCode).IsEqualTo(Keys.Z);
			await Assert.That(shiftedZ.Control).IsTrue();
			await Assert.That(shiftedZ.Shift).IsTrue();

			// The 3D view's zoom shortcuts: Cmd+= and Cmd++ are one key, as are Cmd+- and Cmd+_.
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, "=", NSEventModifierFlagCommand).KeyCode)
				.IsEqualTo(Keys.Oemplus);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, "+", NSEventModifierFlagCommand).KeyCode)
				.IsEqualTo(Keys.Oemplus);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, "-", NSEventModifierFlagCommand).KeyCode)
				.IsEqualTo(Keys.OemMinus);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, "_", NSEventModifierFlagCommand).KeyCode)
				.IsEqualTo(Keys.OemMinus);
		}

		[Test]
		public async Task DigitsResolve()
		{
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, "1", 0).KeyCode).IsEqualTo(Keys.D1);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, "0", 0).KeyCode).IsEqualTo(Keys.D0);
		}

		/// <summary>
		/// The named keys have to keep winning: their characters are private-use-area codes that are not
		/// text and must never be mistaken for one.
		/// </summary>
		[Test]
		public async Task NamedKeysBeatTheirCharacters()
		{
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(VkForwardDelete, "", 0).KeyCode)
				.IsEqualTo(Keys.Delete);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(VkReturn, "\r", 0).KeyCode).IsEqualTo(Keys.Enter);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(VkEscape, "", 0).KeyCode).IsEqualTo(Keys.Escape);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(VkDelete, "", 0).KeyCode).IsEqualTo(Keys.Back);
		}

		/// <summary>A dead key, or a character agg has no key for, still has to carry its modifiers.</summary>
		[Test]
		public async Task UnknownCharactersAreNoKeyButKeepTheirModifiers()
		{
			KeyEventArgs keyEvent = MacSystemWindow.MakeKeyEventArgs(0, "é", NSEventModifierFlagCommand);

			await Assert.That(keyEvent.KeyCode).IsEqualTo(Keys.None);
			await Assert.That(keyEvent.Control).IsTrue();

			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, null, 0).KeyCode).IsEqualTo(Keys.None);
			await Assert.That(MacSystemWindow.MakeKeyEventArgs(0, string.Empty, 0).KeyCode).IsEqualTo(Keys.None);
		}
	}
}
