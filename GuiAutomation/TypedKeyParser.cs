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

namespace MatterHackers.GuiAutomation
{
	/// <summary>One key press the automation runner is going to put through a widget tree.</summary>
	public readonly struct TypedKey
	{
		/// <summary>Creates a stroke.</summary>
		/// <param name="key">The key code, with any modifier bits already or'd in.</param>
		/// <param name="character">The character it types, or '\0' when it types nothing.</param>
		public TypedKey(Keys key, char character)
		{
			this.Key = key;
			this.Character = character;
		}

		/// <summary>The key code with its modifier bits - what a <see cref="KeyEventArgs"/> is built from.</summary>
		public Keys Key { get; }

		/// <summary>The character this stroke types, or '\0' for a stroke that types nothing.</summary>
		public char Character { get; }

		/// <summary>True when a KeyPress should follow the KeyDown.</summary>
		public bool HasCharacter => this.Character != '\0';
	}

	/// <summary>
	/// Turns the strings tests hand <c>testRunner.Type</c> into key strokes.
	/// <para>
	/// The spelling is a deliberately small subset of what Windows' SendKeys accepts, because the two
	/// input methods have to read a test's string the same way: <c>^</c> is control on the key that
	/// follows it, <c>^+</c> is control and shift, and a name in braces - <c>{Enter}</c>, <c>{Esc}</c>,
	/// <c>{BACKSPACE}</c> - is that key rather than those letters. A bare <c>+</c> is *not* shift, unlike
	/// SendKeys: tests type expressions like <c>=40 + 5</c> into fields, and reading that plus as a
	/// modifier would quietly turn the rest of the string into something else.
	/// </para>
	/// <para>
	/// An unrecognised brace token throws rather than being typed out one character at a time. That
	/// silence is the bug this class was extracted to fix: a test that pressed nothing it meant to press
	/// still went green, because the keys it did send were harmless.
	/// </para>
	/// </summary>
	public static class TypedKeyParser
	{
		/// <summary>
		/// Characters whose key code is not just their upper-case value. Everything not listed here is
		/// typed as <c>(Keys)char.ToUpper(c)</c>, which is right for letters and digits.
		/// </summary>
		private static readonly Dictionary<char, Keys> CharToKeys = new Dictionary<char, Keys>()
		{
			['.'] = Keys.OemPeriod,
		};

		/// <summary>Brace-token spellings that are not the <see cref="Keys"/> member's own name.</summary>
		private static readonly Dictionary<string, Keys> TokenAliases
			= new Dictionary<string, Keys>(StringComparer.OrdinalIgnoreCase)
			{
				["ESC"] = Keys.Escape,
				["BACKSPACE"] = Keys.Back,
				["BKSP"] = Keys.Back,
				["BS"] = Keys.Back,
				["DEL"] = Keys.Delete,
				["PGUP"] = Keys.PageUp,
				["PGDN"] = Keys.PageDown,
				["ENTER"] = Keys.Enter,
				["BREAK"] = Keys.Cancel,
			};

		/// <summary>Reads a type string into the strokes it stands for, in order.</summary>
		/// <param name="textToType">The string a test passed to <c>Type</c>.</param>
		/// <exception cref="ArgumentException">A brace token is unterminated or names no key.</exception>
		public static IReadOnlyList<TypedKey> Parse(string textToType)
		{
			var strokes = new List<TypedKey>();
			if (string.IsNullOrEmpty(textToType))
			{
				return strokes;
			}

			for (int index = 0; index < textToType.Length; index++)
			{
				Keys modifiers = Keys.None;

				// Modifiers bind to the single stroke that follows them, so they are read here rather than
				// carried across the loop.
				if (textToType[index] == '^' && index + 1 < textToType.Length)
				{
					modifiers |= Keys.Control;
					index++;

					if (textToType[index] == '+' && index + 1 < textToType.Length)
					{
						modifiers |= Keys.Shift;
						index++;
					}
				}

				if (textToType[index] == '{')
				{
					int close = textToType.IndexOf('}', index);
					if (close < 0)
					{
						throw new ArgumentException(
							$"'{textToType}' opens a key token with '{{' that is never closed with '}}'.",
							nameof(textToType));
					}

					string token = textToType.Substring(index + 1, close - index - 1);
					strokes.Add(new TypedKey(modifiers | ParseToken(token, textToType), '\0'));
					index = close;
					continue;
				}

				char character = textToType[index];
				Keys key = CharToKeys.TryGetValue(character, out var mapped)
					? mapped
					: (Keys)char.ToUpper(character);

				// A control chord types no character: the KeyPress that would follow it on a real keyboard
				// carries a control code, and every widget that acts on the chord acts on the KeyDown.
				bool typesACharacter = (modifiers & Keys.Control) != Keys.Control;

				strokes.Add(new TypedKey(modifiers | key, typesACharacter ? character : '\0'));
			}

			return strokes;
		}

		private static Keys ParseToken(string token, string textToType)
		{
			if (TokenAliases.TryGetValue(token, out var alias))
			{
				return alias;
			}

			if (Enum.TryParse<Keys>(token, ignoreCase: true, out var parsed))
			{
				return parsed;
			}

			throw new ArgumentException(
				$"'{{{token}}}' in '{textToType}' does not name a key. Use a Keys member name "
				+ "({Enter}, {Escape}, {Left}, {F4}) or one of the SendKeys spellings ({ESC}, {BACKSPACE}, "
				+ "{DEL}, {PGUP}, {PGDN}).",
				nameof(textToType));
		}
	}
}
