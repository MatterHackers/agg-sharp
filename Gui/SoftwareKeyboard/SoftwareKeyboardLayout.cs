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

using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	/// <summary>What a text field wants from the on-screen keyboard: the layer it opens on when the field takes the focus.</summary>
	public enum KeyboardInputMode
	{
		/// <summary>Letters, starting shifted when the field is empty (sentence-start capitalisation).</summary>
		Text,

		/// <summary>Digits: opens straight onto the numbers layer, as iOS numberPad or an HTML number input does.</summary>
		Numeric,
	}

	public enum SoftwareKeyboardLayer
	{
		Letters,
		Numbers,
	}

	public enum SoftwareKeyKind
	{
		Character,
		Shift,
		Backspace,
		Return,
		LayerSwitch,
		Dismiss,
	}

	/// <summary>One key: what it does, what it types (for <see cref="SoftwareKeyKind.Character"/>) and its width in key units.</summary>
	public sealed class SoftwareKey
	{
		public SoftwareKey(SoftwareKeyKind kind, char character = '\0', double width = 1)
		{
			this.Kind = kind;
			this.Character = character;
			this.Width = width;
		}

		public SoftwareKeyKind Kind { get; }

		public char Character { get; }

		public double Width { get; }

		/// <summary>The key cap's text. Letters follow the shift state; the special keys are named, so any font can draw them.</summary>
		public string Label(SoftwareKeyboardState state)
		{
			switch (this.Kind)
			{
				case SoftwareKeyKind.Character:
					return this.Character == ' ' ? "space" : state.Apply(this.Character).ToString();
				case SoftwareKeyKind.Shift:
					return state.Shifted ? "SHIFT" : "shift";
				case SoftwareKeyKind.Backspace:
					return "del";
				case SoftwareKeyKind.Return:
					return "return";
				case SoftwareKeyKind.LayerSwitch:
					return state.Layer == SoftwareKeyboardLayer.Letters ? "123" : "ABC";
				default:
					return "hide";
			}
		}
	}

	/// <summary>
	/// The keyboard's rows per layer (a US QWERTY letter page and a digits-and-punctuation page) and the state that
	/// moves between them: which layer shows and whether the next letter is shifted. Kept apart from the widget so the
	/// layer and shift rules are unit-testable without drawing.
	/// </summary>
	public sealed class SoftwareKeyboardState
	{
		private static readonly IReadOnlyList<IReadOnlyList<SoftwareKey>> LetterRows = new[]
		{
			Characters("qwertyuiop"),
			Characters("asdfghjkl"),
			new[] { new SoftwareKey(SoftwareKeyKind.Shift, width: 1.5) }.Concat(Characters("zxcvbnm")).Append(new SoftwareKey(SoftwareKeyKind.Backspace, width: 1.5)).ToList(),
			BottomRow(),
		};

		private static readonly IReadOnlyList<IReadOnlyList<SoftwareKey>> NumberRows = new[]
		{
			Characters("1234567890"),
			Characters("-/:;()$&@\""),
			Characters(".,?!'#%*+=").Append(new SoftwareKey(SoftwareKeyKind.Backspace, width: 1.5)).ToList(),
			BottomRow(),
		};

		public SoftwareKeyboardLayer Layer { get; private set; } = SoftwareKeyboardLayer.Letters;

		/// <summary>One-shot shift: the next letter typed is upper case, then it clears.</summary>
		public bool Shifted { get; private set; }

		public IReadOnlyList<IReadOnlyList<SoftwareKey>> Rows => this.Layer == SoftwareKeyboardLayer.Letters ? LetterRows : NumberRows;

		/// <summary>Reset for a field that just took the focus: numeric fields open on the numbers, empty text fields shifted.</summary>
		public void Open(KeyboardInputMode mode, string existingText)
		{
			this.Layer = mode == KeyboardInputMode.Numeric ? SoftwareKeyboardLayer.Numbers : SoftwareKeyboardLayer.Letters;
			this.Shifted = mode == KeyboardInputMode.Text && string.IsNullOrEmpty(existingText);
		}

		/// <summary>The character a key types in the current state.</summary>
		public char Apply(char character) => this.Shifted && this.Layer == SoftwareKeyboardLayer.Letters ? char.ToUpperInvariant(character) : character;

		/// <summary>
		/// Update the state for a pressed key and return the character to type, if it types one. Shift toggles, the
		/// layer switch flips pages (and drops shift), and a typed letter uses up a one-shot shift.
		/// </summary>
		public char? Press(SoftwareKey key)
		{
			switch (key.Kind)
			{
				case SoftwareKeyKind.Shift:
					this.Shifted = !this.Shifted;
					return null;
				case SoftwareKeyKind.LayerSwitch:
					this.Layer = this.Layer == SoftwareKeyboardLayer.Letters ? SoftwareKeyboardLayer.Numbers : SoftwareKeyboardLayer.Letters;
					this.Shifted = false;
					return null;
				case SoftwareKeyKind.Character:
					char typed = this.Apply(key.Character);
					if (char.IsLetter(key.Character))
					{
						this.Shifted = false;
					}

					return typed;
				default:
					return null;
			}
		}

		private static List<SoftwareKey> Characters(string characters) => characters.Select(c => new SoftwareKey(SoftwareKeyKind.Character, c)).ToList();

		private static List<SoftwareKey> BottomRow() => new List<SoftwareKey>
		{
			new SoftwareKey(SoftwareKeyKind.Dismiss, width: 1.25),
			new SoftwareKey(SoftwareKeyKind.LayerSwitch, width: 1.25),
			new SoftwareKey(SoftwareKeyKind.Character, ',', 1),
			new SoftwareKey(SoftwareKeyKind.Character, ' ', 4),
			new SoftwareKey(SoftwareKeyKind.Character, '.', 1),
			new SoftwareKey(SoftwareKeyKind.Return, width: 1.5),
		};
	}
}
