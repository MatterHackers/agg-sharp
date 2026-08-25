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
using MatterHackers.Agg.Platform.Linux;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// An X11 key event carries a keycode, which is a hardware position and says nothing about which letter
	/// it is; the keysym the layout resolves it to is what agg's <see cref="Keys"/> maps onto. These cover
	/// that table and the modifier composition around it - the whole of the key translation, and all of it
	/// reachable without an X server.
	/// </summary>
	public class X11KeyTranslationTests
	{
		[Test]
		[Arguments(X11.XK_Return, Keys.Enter)]
		[Arguments(X11.XK_KP_Enter, Keys.Enter)]
		[Arguments(X11.XK_Escape, Keys.Escape)]
		[Arguments(X11.XK_Tab, Keys.Tab)]
		[Arguments(X11.XK_BackSpace, Keys.Back)]
		[Arguments(X11.XK_Delete, Keys.Delete)]
		[Arguments(X11.XK_Insert, Keys.Insert)]
		[Arguments(X11.XK_Home, Keys.Home)]
		[Arguments(X11.XK_End, Keys.End)]
		[Arguments(X11.XK_Page_Up, Keys.PageUp)]
		[Arguments(X11.XK_Page_Down, Keys.PageDown)]
		[Arguments(X11.XK_Left, Keys.Left)]
		[Arguments(X11.XK_Up, Keys.Up)]
		[Arguments(X11.XK_Right, Keys.Right)]
		[Arguments(X11.XK_Down, Keys.Down)]
		[Arguments(X11.XK_space, Keys.Space)]
		public async Task NamedKeysResolve(ulong keysym, Keys expected)
		{
			await Assert.That(X11SystemWindow.TranslateKeySym(keysym)).IsEqualTo(expected);
		}

		/// <summary>
		/// Shift+Tab does not produce XK_Tab. It produces XK_ISO_Left_Tab, which lives a whole keysym page
		/// below the rest of the function keys - so a host that only names XK_Tab loses back-tab navigation
		/// entirely and the user can tab forwards through a dialog but never back.
		/// </summary>
		[Test]
		public async Task ShiftTabIsStillTab()
		{
			await Assert.That(X11SystemWindow.TranslateKeySym(X11.XK_ISO_Left_Tab)).IsEqualTo(Keys.Tab);

			KeyEventArgs backTab = X11SystemWindow.MakeKeyEventArgs(X11.XK_ISO_Left_Tab, X11.ShiftMask);

			await Assert.That(backTab.KeyCode).IsEqualTo(Keys.Tab);
			await Assert.That(backTab.Shift).IsTrue();
		}

		/// <summary>
		/// The keysym for a shifted letter is the uppercase one, so both spellings have to land on the same
		/// key - which is what WinForms reports either way. Without it Ctrl+Shift+Z would be a different
		/// shortcut from Ctrl+Z.
		/// </summary>
		[Test]
		[Arguments(X11.XK_a, Keys.A)]
		[Arguments(X11.XK_A, Keys.A)]
		[Arguments(X11.XK_z, Keys.Z)]
		[Arguments(X11.XK_Z, Keys.Z)]
		public async Task LettersResolveWhateverTheirCase(ulong keysym, Keys expected)
		{
			await Assert.That(X11SystemWindow.TranslateKeySym(keysym)).IsEqualTo(expected);
		}

		[Test]
		public async Task DigitsResolve()
		{
			await Assert.That(X11SystemWindow.TranslateKeySym(X11.XK_0)).IsEqualTo(Keys.D0);
			await Assert.That(X11SystemWindow.TranslateKeySym(X11.XK_9)).IsEqualTo(Keys.D9);
			await Assert.That(X11SystemWindow.TranslateKeySym('5')).IsEqualTo(Keys.D5);
		}

		[Test]
		public async Task FunctionKeysResolveAcrossTheWholeRange()
		{
			await Assert.That(X11SystemWindow.TranslateKeySym(X11.XK_F1)).IsEqualTo(Keys.F1);
			await Assert.That(X11SystemWindow.TranslateKeySym(X11.XK_F5)).IsEqualTo(Keys.F5);
			await Assert.That(X11SystemWindow.TranslateKeySym(X11.XK_F12)).IsEqualTo(Keys.F12);
		}

		/// <summary>
		/// The keypad is two keyboards in one: with Num Lock on the keys report the digit and operator
		/// keysyms, with it off they report the navigation ones. Both spellings have to arrive as something
		/// sensible, because which one the user gets is not the application's choice.
		/// </summary>
		[Test]
		[Arguments(X11.XK_KP_0, Keys.NumPad0)]
		[Arguments(X11.XK_KP_5, Keys.NumPad5)]
		[Arguments(X11.XK_KP_9, Keys.NumPad9)]
		[Arguments(X11.XK_KP_Add, Keys.Add)]
		[Arguments(X11.XK_KP_Subtract, Keys.Subtract)]
		[Arguments(X11.XK_KP_Multiply, Keys.Multiply)]
		[Arguments(X11.XK_KP_Divide, Keys.Divide)]
		[Arguments(X11.XK_KP_Decimal, Keys.Decimal)]
		[Arguments(X11.XK_KP_Home, Keys.Home)]
		[Arguments(X11.XK_KP_Left, Keys.Left)]
		[Arguments(X11.XK_KP_Delete, Keys.Delete)]
		public async Task KeypadKeysResolve(ulong keysym, Keys expected)
		{
			await Assert.That(X11SystemWindow.TranslateKeySym(keysym)).IsEqualTo(expected);
		}

		/// <summary>
		/// A bare modifier is an ordinary KeyPress/KeyRelease on X11 - unlike AppKit, which has a separate
		/// FlagsChanged for it - so it has to resolve to the physical key rather than to nothing.
		/// </summary>
		[Test]
		[Arguments(X11.XK_Shift_L, Keys.ShiftKey)]
		[Arguments(X11.XK_Shift_R, Keys.ShiftKey)]
		[Arguments(X11.XK_Control_L, Keys.ControlKey)]
		[Arguments(X11.XK_Control_R, Keys.ControlKey)]
		[Arguments(X11.XK_Alt_L, Keys.Menu)]
		[Arguments(X11.XK_Alt_R, Keys.Menu)]
		[Arguments(X11.XK_Super_L, Keys.LWin)]
		[Arguments(X11.XK_Super_R, Keys.RWin)]
		public async Task ModifierKeysResolveToTheirPhysicalKey(ulong keysym, Keys expected)
		{
			await Assert.That(X11SystemWindow.TranslateKeySym(keysym)).IsEqualTo(expected);
		}

		/// <summary>The zoom shortcuts, each with the shifted spelling of its key alongside the unshifted one.</summary>
		[Test]
		public async Task PunctuationResolvesUnderBothSpellings()
		{
			await Assert.That(X11SystemWindow.TranslateKeySym('=')).IsEqualTo(Keys.Oemplus);
			await Assert.That(X11SystemWindow.TranslateKeySym('+')).IsEqualTo(Keys.Oemplus);
			await Assert.That(X11SystemWindow.TranslateKeySym('-')).IsEqualTo(Keys.OemMinus);
			await Assert.That(X11SystemWindow.TranslateKeySym('_')).IsEqualTo(Keys.OemMinus);
			await Assert.That(X11SystemWindow.TranslateKeySym('/')).IsEqualTo(Keys.OemQuestion);
			await Assert.That(X11SystemWindow.TranslateKeySym('?')).IsEqualTo(Keys.OemQuestion);
		}

		/// <summary>
		/// A dead key, a media key, a letter outside Latin-1 - none of them is a key agg has, and inventing
		/// one for them would fire an unrelated shortcut.
		/// </summary>
		[Test]
		public async Task UnknownKeysymsAreNone()
		{
			// XK_dead_acute.
			await Assert.That(X11SystemWindow.TranslateKeySym(0xFE51)).IsEqualTo(Keys.None);

			// XK_AudioPlay, out in the XF86 vendor page.
			await Assert.That(X11SystemWindow.TranslateKeySym(0x1008FF14)).IsEqualTo(Keys.None);

			// A Latin-1 letter with no agg key: e acute.
			await Assert.That(X11SystemWindow.TranslateKeySym(0x00E9)).IsEqualTo(Keys.None);

			// XK_VoidSymbol, which is what XLookupString reports for a key with no symbol at all.
			await Assert.That(X11SystemWindow.TranslateKeySym(0xFFFFFF)).IsEqualTo(Keys.None);
		}

		/// <summary>
		/// The composition itself: the key and the modifiers have to end up in one <see cref="Keys"/> word,
		/// with the key readable back out of <see cref="KeyEventArgs.KeyCode"/> and the modifiers out of the
		/// Shift/Control/Alt properties. This is what a shortcut is matched on.
		/// </summary>
		[Test]
		public async Task ModifiersAreComposedOntoTheKey()
		{
			KeyEventArgs controlS = X11SystemWindow.MakeKeyEventArgs('s', X11.ControlMask);

			await Assert.That(controlS.KeyCode).IsEqualTo(Keys.S);
			await Assert.That(controlS.Control).IsTrue();
			await Assert.That(controlS.Shift).IsFalse();
			await Assert.That(controlS.Alt).IsFalse();

			KeyEventArgs controlShiftZ = X11SystemWindow.MakeKeyEventArgs(X11.XK_Z, X11.ControlMask | X11.ShiftMask);

			await Assert.That(controlShiftZ.KeyCode).IsEqualTo(Keys.Z);
			await Assert.That(controlShiftZ.Control).IsTrue();
			await Assert.That(controlShiftZ.Shift).IsTrue();

			KeyEventArgs altF4 = X11SystemWindow.MakeKeyEventArgs(X11.XK_F4, X11.Mod1Mask);

			await Assert.That(altF4.KeyCode).IsEqualTo(Keys.F4);
			await Assert.That(altF4.Alt).IsTrue();
		}

		/// <summary>An unknown key still has to carry its modifiers, or a chord on it matches nothing at all
		/// rather than merely matching no key.</summary>
		[Test]
		public async Task AnUnknownKeyStillCarriesItsModifiers()
		{
			KeyEventArgs keyEvent = X11SystemWindow.MakeKeyEventArgs(0x00E9, X11.ControlMask);

			await Assert.That(keyEvent.KeyCode).IsEqualTo(Keys.None);
			await Assert.That(keyEvent.Control).IsTrue();
		}

		/// <summary>
		/// The state word carries the buttons currently held in its upper bits as well as the modifiers in
		/// its lower ones. A host that did not mask them off would OR a button mask into a Keys value and get
		/// an unrelated key.
		/// </summary>
		[Test]
		public async Task HeldButtonsAreNotMistakenForModifiers()
		{
			KeyEventArgs keyEvent = X11SystemWindow.MakeKeyEventArgs(
				X11.XK_a,
				X11.Button1Mask | X11.Button3Mask | X11.ShiftMask);

			await Assert.That(keyEvent.KeyCode).IsEqualTo(Keys.A);
			await Assert.That(keyEvent.Shift).IsTrue();
			await Assert.That(keyEvent.Control).IsFalse();
			await Assert.That(keyEvent.Alt).IsFalse();
		}
	}
}
