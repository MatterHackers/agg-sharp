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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Answers what may be typed at the caret of a text field. Asked by <see cref="TextSuggestionController"/> after
	/// every typed character, and while its list is open after every edit, caret move and accepted suggestion.
	/// Returning <see cref="TextSuggestionList.Empty"/> (or null) keeps the list closed, so the provider alone
	/// decides what triggers suggestions.
	/// </summary>
	public interface ITextSuggestionProvider
	{
		TextSuggestionList GetSuggestions(string text, int caret);
	}

	/// <summary>One row of a suggestion list.</summary>
	public class TextSuggestion
	{
		/// <param name="insertText">What replaces the list's span when this row is accepted.</param>
		/// <param name="label">What the row shows on its left; the insert text when null.</param>
		/// <param name="detail">Drawn right aligned and dimmer, e.g. a current value. Optional.</param>
		/// <param name="description">One line shown below the list while this row is highlighted. Optional.</param>
		public TextSuggestion(string insertText, string label = null, string detail = null, string description = null)
		{
			this.InsertText = insertText ?? "";
			this.Label = label ?? this.InsertText;
			this.Detail = detail;
			this.Description = description;
		}

		public string InsertText { get; }

		public string Label { get; }

		public string Detail { get; }

		public string Description { get; }
	}

	/// <summary>
	/// The suggestions for one caret position and the span of the text an accepted one replaces:
	/// [<see cref="ReplaceStart"/>, <see cref="ReplaceStart"/> + <see cref="ReplaceLength"/>).
	/// </summary>
	public class TextSuggestionList
	{
		public static readonly TextSuggestionList Empty = new TextSuggestionList(0, 0, Array.Empty<TextSuggestion>());

		public TextSuggestionList(int replaceStart, int replaceLength, IReadOnlyList<TextSuggestion> suggestions)
		{
			this.ReplaceStart = replaceStart;
			this.ReplaceLength = replaceLength;
			this.Suggestions = suggestions ?? Array.Empty<TextSuggestion>();
		}

		public int ReplaceStart { get; }

		public int ReplaceLength { get; }

		public IReadOnlyList<TextSuggestion> Suggestions { get; }
	}
}
