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

using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The line tokenizer behind the code editor: agg-gui's rust_highlighter token classes, plus C#.</summary>
	public class SyntaxHighlighterTests
	{
		private static string Describe(string line, SyntaxLanguage language)
		{
			return string.Join(" ", SyntaxHighlighter.HighlightLine(line, language)
				.Select(s => s.Kind + ":" + line.Substring(s.Start, s.Length)));
		}

		[Test]
		public async Task RustLineGetsKeywordsStringsNumbersAndComments()
		{
			await Assert.That(Describe("let greeting = \"Hello, agg-gui!\"; // hi", SyntaxLanguage.Rust))
				.IsEqualTo("Keyword:let String:\"Hello, agg-gui!\" Comment:// hi");
			await Assert.That(Describe("    .map(|i| i as f64 * 0.1)", SyntaxLanguage.Rust))
				.IsEqualTo("Keyword:as Number:0.1");

			// Identifiers that only contain a keyword are not keywords; a number may carry a suffix.
			await Assert.That(Describe("letter fn_name 1_000u32", SyntaxLanguage.Rust)).IsEqualTo("Number:1_000u32");
		}

		[Test]
		public async Task EscapesAndUnclosedStringsStayInTheString()
		{
			await Assert.That(Describe("\"a\\\"b\" x", SyntaxLanguage.Rust)).IsEqualTo("String:\"a\\\"b\"");
			await Assert.That(Describe("x = \"open // not a comment", SyntaxLanguage.Rust))
				.IsEqualTo("String:\"open // not a comment");
		}

		[Test]
		public async Task RustLifetimesAreNotCharLiteralsButCSharpCharsAre()
		{
			await Assert.That(Describe("fn f<'a>(x: &'a str)", SyntaxLanguage.Rust)).IsEqualTo("Keyword:fn");
			await Assert.That(Describe("char c = 'x';", SyntaxLanguage.CSharp)).IsEqualTo("Keyword:char String:'x'");
		}

		[Test]
		public async Task CSharpVerbatimStringsKeepBackslashesAndDoubledQuotes()
		{
			await Assert.That(Describe("var p = @\"C:\\dir \"\"q\"\"\"; return;", SyntaxLanguage.CSharp))
				.IsEqualTo("Keyword:var String:@\"C:\\dir \"\"q\"\"\" Keyword:return");
		}

		[Test]
		public async Task SurrogatePairsAreSteppedOverWhole()
		{
			// An emoji between tokens must not shift or split the spans around it.
			string line = "if \U0001F600 true";
			await Assert.That(Describe(line, SyntaxLanguage.Rust)).IsEqualTo("Keyword:if Keyword:true");
		}

		[Test]
		public async Task PaletteFollowsTheBackgroundAndKeepsLightTokensDark()
		{
			await Assert.That(SyntaxPalette.For(dark: true)).IsEqualTo(SyntaxPalette.Dark);
			await Assert.That(SyntaxPalette.Light.Keyword).IsNotEqualTo(SyntaxPalette.Dark.Keyword);
			await Assert.That(SyntaxPalette.Dark.ColorOf(SyntaxTokenKind.Plain, Color.White)).IsEqualTo(Color.White);

			// agg-gui's legibility rule: every light-theme token is well darker than the white editor.
			foreach (SyntaxTokenKind kind in new[] { SyntaxTokenKind.Keyword, SyntaxTokenKind.String, SyntaxTokenKind.Comment, SyntaxTokenKind.Number })
			{
				ColorF color = SyntaxPalette.Light.ColorOf(kind, Color.Black).ToColorF();
				double luminance = .299 * color.red + .587 * color.green + .114 * color.blue;
				await Assert.That(1 - luminance).IsGreaterThan(.25);
			}
		}
	}
}
