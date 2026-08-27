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

using System;
using System.Collections.Generic;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Turns a DOM <c>KeyboardEvent</c> into agg's <see cref="Keys"/>.
	/// </summary>
	/// <remarks>
	/// Pure - no JS interop, no state - so the whole key translation runs in the desktop test suite, in the
	/// same spirit as <c>MacSystemWindow.MakeKeyEventArgs</c> and <c>X11SystemWindow.TranslateKeySym</c>.
	/// <para/>
	/// The browser hands us two spellings of the same keystroke and they answer different questions.
	/// <c>code</c> is the physical key position - <c>KeyA</c> is "where A sits on a US layout" and produces
	/// Q on AZERTY - and <c>key</c> is what the active layout says that position typed. agg's shortcuts are
	/// all spelled as key codes, and the desktop hosts resolve them from the layout (macOS from
	/// <c>charactersIgnoringModifiers</c>, X11 from the keysym) precisely so Cmd+S is Save on every layout.
	/// <para/>
	/// This host uses <c>code</c> anyway, which is a deliberate difference. In a browser the layout-resolved
	/// spelling is not reliably available for a chord: with a modifier held, Chrome and Safari report
	/// <c>key</c> as the unmodified character but Firefox on some layouts reports the dead-key result, and
	/// on a non-Latin layout (Cyrillic, Greek) <c>key</c> is a character agg has no <see cref="Keys"/> for at
	/// all - so Ctrl+C would stop being Copy the moment the user switched layouts, which is exactly the bug
	/// browsers themselves work around by matching accelerators on <c>code</c>. <c>code</c> costs the AZERTY
	/// user a shortcut in the wrong position; <c>key</c> costs the Cyrillic user every shortcut there is.
	/// <c>key</c> is still what supplies the typed character - see <see cref="IsPrintableKey"/>.
	/// </remarks>
	public static class BrowserKeyboard
	{
		/// <summary>
		/// Maps a <c>KeyboardEvent.code</c> onto agg's <see cref="Keys"/>.
		/// </summary>
		/// <returns><see cref="Keys.None"/> for a code agg has no key for - a media key, an IME key, the
		/// keys of a layout nobody has bound anything to. The modifiers still ride along on the event; see
		/// <see cref="MakeKeyEventArgs"/>.</returns>
		public static Keys TranslateKeyCode(string code)
		{
			if (string.IsNullOrEmpty(code))
			{
				// A composed character from an IME arrives with code "" (or "Unidentified"); there is no key
				// there to name, and the text of it comes through as a key press instead.
				return Keys.None;
			}

			// The four contiguous families are ranges rather than 58 cases, and the prefixes make them
			// unambiguous: only letters are KeyX, only main-row digits are DigitX, and so on.
			if (TryTranslatePrefixed(code, "Key", 'A', 'Z', Keys.A, out Keys letter))
			{
				return letter;
			}

			if (TryTranslatePrefixed(code, "Digit", '0', '9', Keys.D0, out Keys digit))
			{
				return digit;
			}

			if (TryTranslatePrefixed(code, "Numpad", '0', '9', Keys.NumPad0, out Keys numpadDigit))
			{
				return numpadDigit;
			}

			if (TryTranslateFunctionKey(code, out Keys functionKey))
			{
				return functionKey;
			}

			switch (code)
			{
				// Named editing and navigation keys.
				case "Enter":
				case "NumpadEnter":
					return Keys.Enter;
				case "Escape":
					return Keys.Escape;
				case "Tab":
					return Keys.Tab;
				case "Backspace":
					return Keys.Back;
				case "Delete":
					return Keys.Delete;
				case "Insert":
					return Keys.Insert;
				case "Space":
					return Keys.Space;
				case "Home":
					return Keys.Home;
				case "End":
					return Keys.End;
				case "PageUp":
					return Keys.PageUp;
				case "PageDown":
					return Keys.PageDown;
				case "ArrowLeft":
					return Keys.Left;
				case "ArrowRight":
					return Keys.Right;
				case "ArrowUp":
					return Keys.Up;
				case "ArrowDown":
					return Keys.Down;

				// The main-row punctuation. One agg key per physical key: unlike the mac and X11 hosts there
				// is no shifted spelling to fold away, because a code names the position and never the
				// symbol on it - Shift+Equal is still "Equal".
				case "Minus":
					return Keys.OemMinus;
				case "Equal":
					return Keys.Oemplus;
				case "BracketLeft":
					return Keys.OemOpenBrackets;
				case "BracketRight":
					return Keys.OemCloseBrackets;
				case "Backslash":
					return Keys.OemPipe;
				case "Semicolon":
					return Keys.OemSemicolon;
				case "Quote":
					return Keys.OemQuotes;
				case "Comma":
					return Keys.Oemcomma;
				case "Period":
					return Keys.OemPeriod;
				case "Slash":
					return Keys.OemQuestion;
				case "Backquote":
					return Keys.Oemtilde;

				// The keypad's non-digit keys, which agg names separately from the main row's.
				case "NumpadMultiply":
					return Keys.Multiply;
				case "NumpadAdd":
					return Keys.Add;
				case "NumpadSubtract":
					return Keys.Subtract;
				case "NumpadDecimal":
					return Keys.Decimal;
				case "NumpadDivide":
					return Keys.Divide;

				// agg has no keypad-equals; the main-row one is the nearest thing that means the same. Same
				// call X11SystemWindow makes for XK_KP_Equal.
				case "NumpadEqual":
					return Keys.Oemplus;

				// A bare modifier is a real keydown/keyup in the DOM, as it is on X11 and unlike AppKit's
				// separate flagsChanged, so these resolve to the physical key rather than to None. Left and
				// right share one key code, which is what WinForms reports for either.
				case "ShiftLeft":
				case "ShiftRight":
					return Keys.ShiftKey;
				case "ControlLeft":
				case "ControlRight":
					return Keys.ControlKey;

				// Keys.Menu is agg's Alt, not the context-menu key - that is Keys.Apps, below.
				case "AltLeft":
				case "AltRight":
					return Keys.Menu;

				// Command on a mac, the Windows key elsewhere; the browser calls both Meta.
				case "MetaLeft":
					return Keys.LWin;
				case "MetaRight":
					return Keys.RWin;
				case "ContextMenu":
					return Keys.Apps;

				case "CapsLock":
					return Keys.CapsLock;
				case "NumLock":
					return Keys.NumLock;
				case "ScrollLock":
					return Keys.Scroll;
				case "Pause":
					return Keys.Pause;
				case "PrintScreen":
					return Keys.PrintScreen;

				default:
					return Keys.None;
			}
		}

		/// <summary>
		/// Composes the agg key event a keydown or keyup carries, from the parts of the KeyboardEvent that
		/// determine it.
		/// </summary>
		public static KeyEventArgs MakeKeyEventArgs(string code, bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)
			=> new KeyEventArgs(TranslateKeyCode(code) | TranslateModifiers(ctrlKey, shiftKey, altKey, metaKey));

		/// <summary>
		/// Maps a KeyboardEvent's (or a mouse event's) modifier booleans onto the agg down-state keys they
		/// imply.
		/// </summary>
		/// <remarks>
		/// Note the two-to-one mapping: <c>metaKey</c> <em>and</em> <c>ctrlKey</c> both produce
		/// <see cref="Keys.ControlKey"/>, which is what the mac host does with Command and Control and for
		/// the same reason - every agg shortcut and every 3D view gesture is spelled "Control+X", and on a
		/// mac the key the user reaches for is Command. It costs nothing on Windows or Linux, where a
		/// browser only reports metaKey for the Windows/Super key, which agg binds nothing to.
		/// <para/>
		/// The answer is a set and not an OR'd <see cref="Keys"/> value because ShiftKey (16), ControlKey
		/// (17) and Menu (18) are consecutive integers rather than disjoint bits - OR-ing them would produce
		/// unrelated key codes. The modifier <em>flags</em> <see cref="TranslateModifiers"/> returns are
		/// disjoint bits and do combine.
		/// </remarks>
		public static IReadOnlySet<Keys> ModifierDownStateKeys(bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)
		{
			var downKeys = new HashSet<Keys>();

			if (shiftKey)
			{
				downKeys.Add(Keys.ShiftKey);
			}

			if (ctrlKey || metaKey)
			{
				downKeys.Add(Keys.ControlKey);
			}

			if (altKey)
			{
				downKeys.Add(Keys.Menu);
			}

			return downKeys;
		}

		/// <summary>
		/// The modifier bits agg carries on a <see cref="KeyEventArgs"/> and reports from a window's
		/// <c>ModifierKeys</c>.
		/// </summary>
		/// <remarks>
		/// Expressed in terms of <see cref="ModifierDownStateKeys"/> so the two cannot drift apart: what
		/// <c>Keyboard.IsKeyDown(Keys.Control)</c> says and what <c>ModifierKeys</c> says have to agree, or a
		/// gesture that checks one and a shortcut that checks the other disagree about the same keyboard.
		/// </remarks>
		public static Keys TranslateModifiers(bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)
		{
			Keys modifiers = Keys.None;

			foreach (Keys downKey in ModifierDownStateKeys(ctrlKey, shiftKey, altKey, metaKey))
			{
				// Unlike the down-state keys these are disjoint bits, so they OR cleanly.
				modifiers |= downKey switch
				{
					Keys.ShiftKey => Keys.Shift,
					Keys.ControlKey => Keys.Control,
					Keys.Menu => Keys.Alt,
					_ => Keys.None,
				};
			}

			return modifiers;
		}

		/// <summary>
		/// Whether a <c>KeyboardEvent.key</c> is a character the user typed, and so should become an agg key
		/// press, rather than the name of a key that only ever becomes a key down.
		/// </summary>
		/// <remarks>
		/// The DOM makes this cheap to decide and easy to get subtly wrong. <c>key</c> is either a single
		/// grapheme of text ("a", "Z", "!", "é", " ") or a multi-character name from the UI Events spec
		/// ("Enter", "ArrowLeft", "F5", "Dead", "Unidentified") - so length is very nearly the whole test,
		/// and it is the test browsers themselves document. What length alone does not catch is the odd
		/// engine reporting a named key as its C0 control character - Escape as U+001B, Backspace as U+0008,
		/// Enter as a carriage return - all of which are length 1 and none of which may be typed into a text
		/// box. So controls are excluded explicitly rather than trusted to the length.
		/// <para/>
		/// A named key is never text even when agg has no <see cref="Keys"/> for it, which is why this asks
		/// about <c>key</c> and not about the result of <see cref="TranslateKeyCode"/>.
		/// </remarks>
		public static bool IsPrintableKey(string key)
			=> key != null
				&& key.Length == 1
				&& !char.IsControl(key[0]);

		/// <summary>
		/// Whether a code names a key that is only ever a modifier, so a host can tell a chord's modifier
		/// half from its subject without re-deriving it from <see cref="TranslateKeyCode"/>'s answer.
		/// </summary>
		public static bool IsModifierKeyCode(string code)
			=> code == "ShiftLeft" || code == "ShiftRight"
				|| code == "ControlLeft" || code == "ControlRight"
				|| code == "AltLeft" || code == "AltRight"
				|| code == "MetaLeft" || code == "MetaRight";

		/// <summary>
		/// True when <paramref name="code"/> is <paramref name="prefix"/> followed by a single character in
		/// <paramref name="first"/>..<paramref name="last"/>, and hands back the agg key that far along from
		/// <paramref name="firstKey"/>.
		/// </summary>
		/// <remarks>
		/// The letter, digit and keypad-digit blocks are all contiguous in <see cref="Keys"/> - the same
		/// property the mac and X11 hosts lean on - so all three families are this one range test.
		/// </remarks>
		private static bool TryTranslatePrefixed(string code, string prefix, char first, char last, Keys firstKey, out Keys key)
		{
			key = Keys.None;

			if (code.Length != prefix.Length + 1 || !code.StartsWith(prefix, StringComparison.Ordinal))
			{
				return false;
			}

			char position = code[prefix.Length];
			if (position < first || position > last)
			{
				return false;
			}

			key = firstKey + (position - first);
			return true;
		}

		/// <summary>
		/// F1 through F12. Not a prefix range like the families above, because the number is one or two
		/// digits and because F13 and up exist in the DOM while nothing in agg is bound to them.
		/// </summary>
		private static bool TryTranslateFunctionKey(string code, out Keys key)
		{
			key = code switch
			{
				"F1" => Keys.F1,
				"F2" => Keys.F2,
				"F3" => Keys.F3,
				"F4" => Keys.F4,
				"F5" => Keys.F5,
				"F6" => Keys.F6,
				"F7" => Keys.F7,
				"F8" => Keys.F8,
				"F9" => Keys.F9,
				"F10" => Keys.F10,
				"F11" => Keys.F11,
				"F12" => Keys.F12,
				_ => Keys.None,
			};

			return key != Keys.None;
		}
	}

	/// <summary>
	/// Remembers which modifiers the last input event reported, so a window can answer
	/// <c>IPlatformWindow.ModifierKeys</c> between events.
	/// </summary>
	/// <remarks>
	/// The browser has no equivalent of <c>+[NSEvent modifierFlags]</c> or <c>XQueryPointer</c>: there is no
	/// way to ask what is held right now, only to be told on each event. So the last event's flags are the
	/// whole of the answer, and they are updated from mouse and wheel events as well as key events - every
	/// DOM input event carries the four booleans, and a modifier pressed while the pointer is moving would
	/// otherwise not be noticed until the next keystroke.
	/// <para/>
	/// The known hole is a modifier pressed or released while the page has no focus (or is tabbed away
	/// from), which sends no event at all; the state re-syncs on the first event after focus returns. A
	/// window can force that by feeding a focus/blur handler through <see cref="Clear"/>.
	/// </remarks>
	public sealed class BrowserModifierState
	{
		private bool ctrlKey;
		private bool shiftKey;
		private bool altKey;
		private bool metaKey;

		/// <summary>Records what an input event says is held.</summary>
		public void Update(bool ctrlKey, bool shiftKey, bool altKey, bool metaKey)
		{
			this.ctrlKey = ctrlKey;
			this.shiftKey = shiftKey;
			this.altKey = altKey;
			this.metaKey = metaKey;
		}

		/// <summary>Forgets everything, for a blur - a modifier released while the page was not looking
		/// would otherwise be reported as held forever.</summary>
		public void Clear() => this.Update(false, false, false, false);

		/// <summary>The modifier flags to report from <c>ModifierKeys</c>.</summary>
		public Keys ModifierKeys
			=> BrowserKeyboard.TranslateModifiers(this.ctrlKey, this.shiftKey, this.altKey, this.metaKey);

		/// <summary>The keys <c>Keyboard</c> should have in its down state; see
		/// <see cref="BrowserKeyboard.ModifierDownStateKeys"/> for why this is a set.</summary>
		public IReadOnlySet<Keys> DownStateKeys
			=> BrowserKeyboard.ModifierDownStateKeys(this.ctrlKey, this.shiftKey, this.altKey, this.metaKey);
	}
}
