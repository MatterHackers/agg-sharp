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

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// One piece of a text-bearing block: either a styled run of text or an atom the caret steps over whole.
	/// </summary>
	public abstract class RichInline
	{
		/// <summary>
		/// How many caret positions this inline occupies in its block's plain text.
		/// </summary>
		public abstract int Length { get; }

		public abstract RichInline Clone();
	}

	/// <summary>
	/// Text sharing one style. Text never contains markdown syntax; the writer adds delimiters and escaping.
	/// </summary>
	public class RichRun : RichInline
	{
		public RichRun()
		{
		}

		public RichRun(string text)
		{
			Text = text;
		}

		public string Text { get; set; } = "";

		public bool Bold { get; set; }

		public bool Italic { get; set; }

		public bool Code { get; set; }

		public bool Strike { get; set; }

		/// <summary>
		/// The link target when this run is (part of) a link's text, otherwise null.
		/// </summary>
		public string LinkUrl { get; set; }

		/// <summary>
		/// The link's title ([text](url "title")), or null when it has none.
		/// </summary>
		public string LinkTitle { get; set; }

		/// <summary>
		/// The reference label as written when the link is a reference link ([text][label], [label][] or [label]),
		/// or null for an inline link. When set, the writer emits [text][label] so the definition elsewhere in the
		/// document stays the link's source of truth; LinkUrl is then only the resolved target, for clicking.
		/// </summary>
		public string LinkLabel { get; set; }

		public override int Length => Text.Length;

		/// <summary>
		/// True when the two runs would render and write identically apart from their text, so they can be merged.
		/// </summary>
		public bool HasSameStyle(RichRun other)
		{
			return Bold == other.Bold
				&& Italic == other.Italic
				&& Code == other.Code
				&& Strike == other.Strike
				&& LinkUrl == other.LinkUrl
				&& LinkTitle == other.LinkTitle
				&& LinkLabel == other.LinkLabel;
		}

		/// <summary>
		/// A run with this run's style and the given text.
		/// </summary>
		public RichRun WithText(string text)
		{
			var copy = (RichRun)Clone();
			copy.Text = text;
			return copy;
		}

		public override RichInline Clone()
		{
			return (RichRun)MemberwiseClone();
		}

		public override string ToString() => Text;
	}

	public enum InlineAtomKind
	{
		Image,
		Html,
		Autolink,
		HardBreak,

		/// <summary>
		/// Any other inline the model does not cover (mark, sub/superscript, task checkbox, footnote reference...),
		/// kept as its markdown so it survives a rewrite of its block.
		/// </summary>
		Raw,
	}

	/// <summary>
	/// An inline the rich view does not edit inside (image, inline HTML, autolink, hard break).
	/// It keeps its markdown verbatim so writing a dirty block reproduces it exactly.
	/// </summary>
	public class InlineAtom : RichInline
	{
		public InlineAtom()
		{
		}

		public InlineAtom(InlineAtomKind kind, string rawMarkdown)
		{
			Kind = kind;
			RawMarkdown = rawMarkdown;
		}

		public InlineAtomKind Kind { get; set; }

		public string RawMarkdown { get; set; } = "";

		/// <summary>
		/// Always 1: the caret can sit before or after an atom but never inside it.
		/// </summary>
		public override int Length => 1;

		public override RichInline Clone()
		{
			return (InlineAtom)MemberwiseClone();
		}

		public override string ToString() => RawMarkdown;
	}
}
