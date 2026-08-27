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

using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The browser host resolves shortcuts from <c>KeyboardEvent.code</c>, so this table is the whole of
	/// what makes Ctrl+S a Save and an arrow key a nudge. It is pure managed code with no DOM behind it,
	/// which is why these run on every desktop OS rather than only where a browser could be launched.
	/// </summary>
	public class BrowserKeyTranslationTests
	{
		/// <summary>The letter block, which is a contiguous range rather than 26 cases - so the ends and one
		/// in the middle are what prove the arithmetic.</summary>
		[Test]
		[Arguments("KeyA", Keys.A)]
		[Arguments("KeyS", Keys.S)]
		[Arguments("KeyZ", Keys.Z)]
		public async Task LettersResolve(string code, Keys expected)
		{
			await Assert.That(BrowserKeyboard.TranslateKeyCode(code)).IsEqualTo(expected);
		}

		/// <summary>Both digit blocks, which agg keeps apart: the main row is D0-D9 and the keypad is
		/// NumPad0-9, and a widget that reads one does not read the other.</summary>
		[Test]
		[Arguments("Digit0", Keys.D0)]
		[Arguments("Digit7", Keys.D7)]
		[Arguments("Digit9", Keys.D9)]
		[Arguments("Numpad0", Keys.NumPad0)]
		[Arguments("Numpad7", Keys.NumPad7)]
		[Arguments("Numpad9", Keys.NumPad9)]
		public async Task DigitsResolve(string code, Keys expected)
		{
			await Assert.That(BrowserKeyboard.TranslateKeyCode(code)).IsEqualTo(expected);
		}

		/// <summary>F1 through F12. F13 and up exist in the DOM and agg binds nothing to them, so they are
		/// deliberately not in the table.</summary>
		[Test]
		[Arguments("F1", Keys.F1)]
		[Arguments("F5", Keys.F5)]
		[Arguments("F12", Keys.F12)]
		public async Task FunctionKeysResolve(string code, Keys expected)
		{
			await Assert.That(BrowserKeyboard.TranslateKeyCode(code)).IsEqualTo(expected);
			await Assert.That(BrowserKeyboard.TranslateKeyCode("F13")).IsEqualTo(Keys.None);
		}

		/// <summary>Navigation and editing: the keys a text box and a 3D view both live on.</summary>
		[Test]
		[Arguments("ArrowLeft", Keys.Left)]
		[Arguments("ArrowRight", Keys.Right)]
		[Arguments("ArrowUp", Keys.Up)]
		[Arguments("ArrowDown", Keys.Down)]
		[Arguments("Home", Keys.Home)]
		[Arguments("End", Keys.End)]
		[Arguments("PageUp", Keys.PageUp)]
		[Arguments("PageDown", Keys.PageDown)]
		[Arguments("Enter", Keys.Enter)]
		[Arguments("NumpadEnter", Keys.Enter)]
		[Arguments("Escape", Keys.Escape)]
		[Arguments("Tab", Keys.Tab)]
		[Arguments("Backspace", Keys.Back)]
		[Arguments("Delete", Keys.Delete)]
		[Arguments("Insert", Keys.Insert)]
		[Arguments("Space", Keys.Space)]
		public async Task NamedKeysResolve(string code, Keys expected)
		{
			await Assert.That(BrowserKeyboard.TranslateKeyCode(code)).IsEqualTo(expected);
		}

		/// <summary>
		/// The punctuation row. A code names a position and never a symbol, so unlike the mac and X11 hosts
		/// there is no shifted spelling to fold - which is precisely why Ctrl+Shift+= cannot drift away from
		/// Ctrl+= here, and worth an assertion that says so.
		/// </summary>
		[Test]
		[Arguments("Minus", Keys.OemMinus)]
		[Arguments("Equal", Keys.Oemplus)]
		[Arguments("BracketLeft", Keys.OemOpenBrackets)]
		[Arguments("BracketRight", Keys.OemCloseBrackets)]
		[Arguments("Backslash", Keys.OemPipe)]
		[Arguments("Semicolon", Keys.OemSemicolon)]
		[Arguments("Quote", Keys.OemQuotes)]
		[Arguments("Comma", Keys.Oemcomma)]
		[Arguments("Period", Keys.OemPeriod)]
		[Arguments("Slash", Keys.OemQuestion)]
		[Arguments("Backquote", Keys.Oemtilde)]
		public async Task PunctuationResolves(string code, Keys expected)
		{
			await Assert.That(BrowserKeyboard.TranslateKeyCode(code)).IsEqualTo(expected);
		}

		/// <summary>
		/// The zoom shortcuts, which are the punctuation keys anything actually binds - and the pair that
		/// broke on the mac host before its character table existed.
		/// </summary>
		[Test]
		public async Task TheZoomShortcutsResolveWhicheverWayTheyAreShifted()
		{
			KeyEventArgs plain = BrowserKeyboard.MakeKeyEventArgs("Equal", ctrlKey: true, shiftKey: false, altKey: false, metaKey: false);
			KeyEventArgs shifted = BrowserKeyboard.MakeKeyEventArgs("Equal", ctrlKey: true, shiftKey: true, altKey: false, metaKey: false);

			await Assert.That(plain.KeyCode).IsEqualTo(Keys.Oemplus);
			await Assert.That(shifted.KeyCode).IsEqualTo(Keys.Oemplus);
			await Assert.That(BrowserKeyboard.MakeKeyEventArgs("Minus", true, false, false, false).KeyCode)
				.IsEqualTo(Keys.OemMinus);
		}

		/// <summary>
		/// A bare modifier is a real keydown in the DOM, so it has to resolve to the physical key rather
		/// than to None - a widget watching for Shift going down has nothing else to watch.
		/// </summary>
		[Test]
		[Arguments("ShiftLeft", Keys.ShiftKey)]
		[Arguments("ShiftRight", Keys.ShiftKey)]
		[Arguments("ControlLeft", Keys.ControlKey)]
		[Arguments("ControlRight", Keys.ControlKey)]
		[Arguments("AltLeft", Keys.Menu)]
		[Arguments("AltRight", Keys.Menu)]
		[Arguments("MetaLeft", Keys.LWin)]
		[Arguments("MetaRight", Keys.RWin)]
		public async Task ModifierKeysResolveToTheirOwnKey(string code, Keys expected)
		{
			await Assert.That(BrowserKeyboard.TranslateKeyCode(code)).IsEqualTo(expected);
			await Assert.That(BrowserKeyboard.IsModifierKeyCode(code)).IsTrue();
		}

		/// <summary>
		/// Anything the table does not name is None, and so is the empty code an IME composition arrives
		/// with. A near miss is included because the letter and digit ranges are prefix arithmetic and a
		/// sloppy range test would turn "KeyAB" or "Digit" into a key.
		/// </summary>
		[Test]
		[Arguments("MediaPlayPause")]
		[Arguments("Unidentified")]
		[Arguments("KeyAB")]
		[Arguments("Key")]
		[Arguments("Digit")]
		[Arguments("Numpad")]
		[Arguments("")]
		[Arguments(null)]
		public async Task UnknownCodesAreNoKey(string code)
		{
			await Assert.That(BrowserKeyboard.TranslateKeyCode(code)).IsEqualTo(Keys.None);
			await Assert.That(BrowserKeyboard.IsModifierKeyCode(code)).IsFalse();
		}

		/// <summary>
		/// An unknown key still has to carry its modifiers, or a chord on a layout agg has no key for stops
		/// looking like a chord at all.
		/// </summary>
		[Test]
		public async Task AnUnknownCodeKeepsItsModifiers()
		{
			KeyEventArgs keyEvent = BrowserKeyboard.MakeKeyEventArgs("IntlBackslash", ctrlKey: true, shiftKey: false, altKey: false, metaKey: false);

			await Assert.That(keyEvent.KeyCode).IsEqualTo(Keys.None);
			await Assert.That(keyEvent.Control).IsTrue();
		}

		/// <summary>
		/// Every combination of the four booleans, because the modifier flags are what a shortcut is matched
		/// on and a mask error only shows up in the combinations. Command is Control as well, which is the
		/// mac host's rule carried over: on a mac the key the user reaches for is Command.
		/// </summary>
		[Test]
		[Arguments(false, false, false, false, Keys.None)]
		[Arguments(true, false, false, false, Keys.Control)]
		[Arguments(false, true, false, false, Keys.Shift)]
		[Arguments(false, false, true, false, Keys.Alt)]
		[Arguments(false, false, false, true, Keys.Control)]
		[Arguments(true, true, false, false, Keys.Control | Keys.Shift)]
		[Arguments(false, true, false, true, Keys.Control | Keys.Shift)]
		[Arguments(true, false, true, false, Keys.Control | Keys.Alt)]
		[Arguments(true, true, true, true, Keys.Control | Keys.Shift | Keys.Alt)]
		public async Task ModifierFlagsCombine(bool ctrl, bool shift, bool alt, bool meta, Keys expected)
		{
			await Assert.That(BrowserKeyboard.TranslateModifiers(ctrl, shift, alt, meta)).IsEqualTo(expected);

			// And the same answer has to reach the event, or a shortcut and a gesture would disagree about
			// the same keyboard.
			await Assert.That(BrowserKeyboard.MakeKeyEventArgs("KeyS", ctrl, shift, alt, meta).Modifiers)
				.IsEqualTo(expected);
		}

		/// <summary>
		/// The down-state keys are a set and not an OR, because ShiftKey/ControlKey/Menu are consecutive
		/// integers - OR-ing them would produce unrelated key codes. This is what Keyboard.IsKeyDown reads.
		/// </summary>
		[Test]
		public async Task ModifierDownStateKeysAreASet()
		{
			var held = BrowserKeyboard.ModifierDownStateKeys(ctrlKey: false, shiftKey: true, altKey: false, metaKey: true);

			await Assert.That(held).Contains(Keys.ShiftKey);
			await Assert.That(held).Contains(Keys.ControlKey);
			await Assert.That(held).DoesNotContain(Keys.Menu);
		}

		/// <summary>
		/// KeyboardEvent.key is what supplies the typed character, and telling a character from a key name
		/// is a length test with one exception: the length-1 control characters some engines report for
		/// named keys must never be typed into a text box.
		/// </summary>
		[Test]
		[Arguments("a", true)]
		[Arguments("Z", true)]
		[Arguments("!", true)]
		[Arguments(" ", true)]
		[Arguments("é", true)]
		[Arguments("Enter", false)]
		[Arguments("ArrowLeft", false)]
		[Arguments("F5", false)]
		[Arguments("Dead", false)]
		[Arguments("\r", false)]
		[Arguments("\b", false)]
		[Arguments("\u001b", false)]
		[Arguments("", false)]
		[Arguments(null, false)]
		public async Task PrintableKeysAreTheOnlyOnesThatType(string key, bool expected)
		{
			await Assert.That(BrowserKeyboard.IsPrintableKey(key)).IsEqualTo(expected);
		}

		/// <summary>
		/// The browser can only ever tell us the modifier state on an event, so the last event's flags are
		/// the whole of what ModifierKeys can report - and a blur has to clear them, because a modifier
		/// released while the page was not looking sends nothing at all.
		/// </summary>
		[Test]
		public async Task TheModifierStateIsWhateverTheLastEventSaid()
		{
			var state = new BrowserModifierState();

			await Assert.That(state.ModifierKeys).IsEqualTo(Keys.None);

			state.Update(ctrlKey: true, shiftKey: true, altKey: false, metaKey: false);
			await Assert.That(state.ModifierKeys).IsEqualTo(Keys.Control | Keys.Shift);
			await Assert.That(state.DownStateKeys).Contains(Keys.ControlKey);

			state.Update(ctrlKey: false, shiftKey: true, altKey: false, metaKey: false);
			await Assert.That(state.ModifierKeys).IsEqualTo(Keys.Shift);

			state.Clear();
			await Assert.That(state.ModifierKeys).IsEqualTo(Keys.None);
			await Assert.That(state.DownStateKeys).IsEmpty();
		}
	}
}
