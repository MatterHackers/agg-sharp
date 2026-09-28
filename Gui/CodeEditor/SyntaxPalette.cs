/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The colours a <see cref="SyntaxTokenKind"/> is drawn in: agg-gui's theme-aware syntax_palette
	/// (demo-ui code_editor_demo.rs). The light set is darker and more saturated so every token keeps a clear
	/// contrast against a near-white editor.
	/// </summary>
	public sealed class SyntaxPalette
	{
		public static SyntaxPalette Dark { get; } = new SyntaxPalette(
			keyword: Rgb(0.55, 0.63, 0.96),
			stringLiteral: Rgb(0.68, 0.84, 0.5),
			comment: Rgb(0.48, 0.53, 0.6),
			number: Rgb(0.92, 0.66, 0.42));

		public static SyntaxPalette Light { get; } = new SyntaxPalette(
			keyword: Rgb(0.13, 0.28, 0.70),
			stringLiteral: Rgb(0.20, 0.52, 0.24),
			comment: Rgb(0.45, 0.50, 0.58),
			number: Rgb(0.68, 0.38, 0.10));

		public SyntaxPalette(Color keyword, Color stringLiteral, Color comment, Color number)
		{
			Keyword = keyword;
			String = stringLiteral;
			Comment = comment;
			Number = number;
		}

		public Color Keyword { get; }

		public Color String { get; }

		public Color Comment { get; }

		public Color Number { get; }

		/// <summary>The palette for a dark or a light editor background.</summary>
		public static SyntaxPalette For(bool dark) => dark ? Dark : Light;

		/// <summary>The colour of <paramref name="kind"/>; plain text takes <paramref name="plain"/>.</summary>
		public Color ColorOf(SyntaxTokenKind kind, Color plain)
		{
			switch (kind)
			{
				case SyntaxTokenKind.Keyword: return Keyword;
				case SyntaxTokenKind.String: return String;
				case SyntaxTokenKind.Comment: return Comment;
				case SyntaxTokenKind.Number: return Number;
				default: return plain;
			}
		}

		// agg-gui's colours are 0..1 floats.
		private static Color Rgb(double r, double g, double b) => new ColorF(r, g, b).ToColor();
	}
}
