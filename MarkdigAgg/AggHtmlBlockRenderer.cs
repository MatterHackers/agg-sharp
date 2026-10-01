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

using System.Net;
using System.Text.RegularExpressions;
using Markdig.Syntax;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg
{
	/// <summary>
	/// Renders the GitHub-style alignment HTML a markdown file may carry; every other HTML block is dropped,
	/// as it was before this renderer existed.
	/// <para>
	/// Markdig ends an HTML block at a blank line, so the stored form <c>&lt;div align="center"&gt;</c>, blank line,
	/// markdown, blank line, <c>&lt;/div&gt;</c> arrives as an opening HtmlBlock, ordinary markdown blocks, and a
	/// closing HtmlBlock. The open and close tags therefore push and pop <see cref="AggRenderer.BlockAlignment"/>,
	/// which the paragraph and heading renderers read for the blocks in between.
	/// </para>
	/// </summary>
	public class AggHtmlBlockRenderer : AggObjectRenderer<HtmlBlock>
	{
		private static readonly Regex DivOpen = new Regex(
			@"^<div(\s[^>]*)?>$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private static readonly Regex DivClose = new Regex(
			@"^</div\s*>$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		private static readonly Regex AlignAttribute = new Regex(
			// Start or whitespace before align, so data-align="center" and similar attributes don't count.
			@"(?:^|\s)align\s*=\s*[""']?(left|center|right)\b",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant);

		// The one-line form GitHub READMEs use: <p align="center">text</p> or <h2 align="center">Title</h2>.
		// The inner text may not open or close the same tag, so <p align="center">a</p><p>b</p> is not read
		// as one element gluing "a" and "b"; that block falls through to showing nothing.
		private static readonly Regex AlignedTextElement = new Regex(
			@"^<(p|h[1-6])(\s[^>]*)?>((?:(?!</?\1\b).)*)</\1\s*>$",
			RegexOptions.IgnoreCase | RegexOptions.CultureInvariant | RegexOptions.Singleline);

		private static readonly Regex AnyTag = new Regex(@"<[^>]*>", RegexOptions.CultureInvariant);

		protected override void Write(AggRenderer renderer, HtmlBlock obj)
		{
			var html = obj.Lines.ToString().Trim();

			if (DivOpen.Match(html) is { Success: true } open)
			{
				// A div without an align attribute inherits the alignment around it, so its own </div> pops
				// back to that alignment rather than ending an outer aligned div early.
				renderer.PushBlockAlignment(ParseAlignment(open.Groups[1].Value) ?? renderer.BlockAlignment);
				return;
			}

			if (DivClose.IsMatch(html))
			{
				// An unmatched close is ignored inside PopBlockAlignment.
				renderer.PopBlockAlignment();
				return;
			}

			var element = AlignedTextElement.Match(html);
			if (element.Success
				&& ParseAlignment(element.Groups[2].Value) is HAnchor alignment)
			{
				WriteAlignedText(renderer, obj, element.Groups[1].Value, element.Groups[3].Value, alignment);
			}
		}

		private static void WriteAlignedText(AggRenderer renderer, HtmlBlock obj, string tag, string innerHtml, HAnchor alignment)
		{
			// Inner markup is shown as plain text: GitHub's one-line form rarely carries more than a word or a
			// link, and showing its text beats dropping the whole line.
			var text = WebUtility.HtmlDecode(AnyTag.Replace(innerHtml, "")).Trim();
			text = Regex.Replace(text, @"\s+", " ");

			// An element holding only markup (a centered <img> logo) would otherwise add an empty padded row.
			if (text.Length == 0)
			{
				return;
			}

			FlowLeftRightWithWrapping row;
			if (tag.Length == 2 && char.ToLowerInvariant(tag[0]) == 'h')
			{
				row = new HeadingRowX(tag[1] - '0');
			}
			else
			{
				// Same spacing rule as AggParagraphRenderer, so an aligned paragraph sits like any other.
				var bottomMargin = obj.Parent is MarkdownDocument
					&& obj.Parent.Count > 1
					&& obj.Line != 0;
				row = new ParagraphX(bottomMargin)
				{
					RowMargin = 0,
					RowPadding = 3
				};
			}

			row.ContentHAnchor = alignment;
			renderer.Push(row);
			renderer.WriteText(text);
			renderer.Pop();
		}

		private static HAnchor? ParseAlignment(string attributes)
		{
			var match = AlignAttribute.Match(attributes ?? "");
			if (!match.Success)
			{
				return null;
			}

			return match.Groups[1].Value.ToLowerInvariant() switch
			{
				"center" => HAnchor.Center,
				"right" => HAnchor.Right,
				_ => HAnchor.Left
			};
		}
	}
}
