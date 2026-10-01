/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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

using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using Markdig.Agg.Editing;
using TUnit.Assertions;
using TUnit.Core;

namespace Markdig.Agg.Tests
{
	/// <summary>
	/// The writer's promise, checked as a property: inlines built the way the editor builds them (styled runs,
	/// code, atoms, with and without spaces between) write to markdown that reads back as the same text with
	/// the same style on every character. Whitespace styles are not compared - the writer moves whitespace
	/// outside delimiters on purpose.
	/// </summary>
	public class RichInlineRoundTripTests
	{
		[Test]
		public async Task ReviewedEdgesReadBack()
		{
			var cases = new[]
			{
				new RichInline[] { Run("a"), Run("b", bold: true, strike: true), Run("c") },
				new RichInline[] { Run("b", bold: true, code: true), Run("c") },
				new RichInline[] { Run("ab"), Run("q.", bold: true), Run("cd") },
				new RichInline[] { Image(bold: true), Run("c") },
				new RichInline[] { Run("a"), Run("\"b\"", bold: true), Run("c") },
				new RichInline[] { Run("x "), Run("y", italic: true), Run("z", bold: true, italic: true) },
			};
			foreach (var inlines in cases)
			{
				await AssertReadsBack(inlines);
			}
		}

		/// <summary>
		/// Every sequence of three pieces (letters, punctuation, code, an image) in every style combination,
		/// touching or separated by a space.
		/// </summary>
		[Test]
		public async Task EveryThreeRunPermutationReadsBack()
		{
			var failures = new List<string>();
			var pieces = new[] { "a", "q.", "code", "image" };
			for (int separator = 0; separator < 2; separator++)
			{
				for (int first = 0; first < 32; first++)
				{
					for (int second = 0; second < 32; second++)
					{
						for (int third = 0; third < 32; third++)
						{
							var inlines = new List<RichInline>();
							int[] picks = { first, second, third };
							for (int k = 0; k < 3; k++)
							{
								if (k > 0 && separator == 1)
								{
									inlines.Add(Run(" "));
								}

								inlines.Add(Piece(pieces[picks[k] / 8], picks[k] % 8));
							}

							string failure = ReadBackFailure(inlines);
							if (failure != null)
							{
								failures.Add(failure);
							}
						}
					}
				}
			}

			await Assert.That(string.Join("\n", failures.Take(20))).IsEqualTo("");
		}

		private static RichInline Piece(string piece, int style)
		{
			bool bold = (style & 1) != 0, italic = (style & 2) != 0, strike = (style & 4) != 0;
			return piece switch
			{
				"code" => Run("c", bold, italic, strike, code: true),
				"image" => Image(bold, italic, strike),
				_ => Run(piece, bold, italic, strike),
			};
		}

		private static async Task AssertReadsBack(IReadOnlyList<RichInline> inlines)
		{
			await Assert.That(ReadBackFailure(inlines)).IsNull();
		}

		/// <summary>
		/// Null when the written markdown reads back as the inlines; otherwise what was written and how it differs.
		/// </summary>
		private static string ReadBackFailure(IReadOnlyList<RichInline> inlines)
		{
			string markdown = RichInlineWriter.Write(inlines);
			var blocks = RichMarkdownParser.Parse(markdown).Blocks;
			if (blocks.Count != 1 || !blocks[0].IsTextBlock)
			{
				return $"{Describe(inlines)} -> {markdown} -> {blocks.Count} blocks";
			}

			string wanted = Describe(inlines);
			string found = Describe(blocks[0].Inlines);
			return wanted == found ? null : $"{wanted} -> {markdown} -> {found}";
		}

		/// <summary>
		/// The text with each non-space character's style: "a[B]" is a bold a, "`c`" code, "{img}" the image.
		/// </summary>
		private static string Describe(IReadOnlyList<RichInline> inlines)
		{
			var text = new StringBuilder();
			foreach (var inline in inlines)
			{
				string style = (inline.Bold ? "B" : "") + (inline.Italic ? "I" : "") + (inline.Strike ? "S" : "");
				if (inline is InlineAtom atom)
				{
					text.Append('{').Append(atom.RawMarkdown).Append('}').Append(style.Length > 0 ? "[" + style + "]" : "");
					continue;
				}

				var run = (RichRun)inline;
				foreach (char c in run.Text)
				{
					text.Append(run.Code ? "`" + c + "`" : c.ToString());
					if (!char.IsWhiteSpace(c) && style.Length > 0)
					{
						text.Append('[').Append(style).Append(']');
					}
				}
			}

			return text.ToString();
		}

		private static RichRun Run(string text, bool bold = false, bool italic = false, bool strike = false, bool code = false)
		{
			return new RichRun(text) { Bold = bold, Italic = italic, Strike = strike, Code = code };
		}

		private static InlineAtom Image(bool bold = false, bool italic = false, bool strike = false)
		{
			return new InlineAtom(InlineAtomKind.Image, "![i](a.png)") { Bold = bold, Italic = italic, Strike = strike };
		}
	}
}
