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
	/// Markdown that spans several blocks: one &lt;div align&gt; wrapper, one blockquote or one top-level list.
	/// A single instance is shared by reference among its member blocks, so membership is explicit rather than
	/// inferred from separators (two adjacent quotes or lists stay two groups). <see cref="RichBlock.Clone"/>
	/// keeps the reference; <see cref="RichDocument.Clone"/> clones each group once and remaps its members.
	/// <para>
	/// Writer rule: a group writes its original bytes (member separators and sources, plus an align group's
	/// wrapper) only while its members are still one contiguous run of exactly <see cref="OriginalMemberCount"/>
	/// blocks, all clean (and, for an align group, all still at the group's <see cref="RichAlignGroup.Alignment"/>).
	/// Otherwise the whole group is regenerated - wrapper, list indentation from Depth, and the separators inside
	/// it - with at least one blank line between it and neighbours of another kind.
	/// </para>
	/// </summary>
	public abstract class RichBlockGroup
	{
		/// <summary>
		/// How many blocks the group had when parsed; a different count means an edit added or removed members.
		/// 0 for a group created by an edit, which is never written from original bytes.
		/// </summary>
		public int OriginalMemberCount { get; set; }

		public RichBlockGroup Clone() => (RichBlockGroup)MemberwiseClone();
	}

	/// <summary>
	/// A &lt;div align&gt; wrapper around paragraphs and headings. A "left" wrapper is a group too, so an untouched
	/// one round-trips; blocks outside any group are simply Left.
	/// </summary>
	public class RichAlignGroup : RichBlockGroup
	{
		/// <summary>
		/// The wrapper's alignment as parsed. A member whose <see cref="RichBlock.Alignment"/> differs has been
		/// re-aligned, so the group no longer writes its original bytes.
		/// </summary>
		public RichAlignment Alignment { get; set; }

		/// <summary>
		/// From the &lt;div align="..."&gt; tag through the blank line(s) before the first member's source.
		/// Written after the first member's SeparatorBefore, so separators stay whitespace only.
		/// </summary>
		public string OpenSource { get; set; } = "";

		/// <summary>
		/// From the blank line(s) after the last member's source through &lt;/div&gt;; the line break after the
		/// tag is the next block's separator.
		/// </summary>
		public string CloseSource { get; set; } = "";

		/// <summary>
		/// For a one-line element (<c>&lt;h2 align="center"&gt;Title&lt;/h2&gt;</c>), the whole line as parsed; null
		/// for a wrapper. The member's own OriginalSource stays its markdown ("## Title"), so a clean member written
		/// without the group still reads back as what it is; this line replaces it only while the group is intact.
		/// </summary>
		public string OneLineSource { get; set; }
	}

	/// <summary>
	/// One blockquote; each member Quote block is one of its paragraphs.
	/// </summary>
	public class RichQuoteGroup : RichBlockGroup
	{
	}

	/// <summary>
	/// One top-level list; its nested items (any Depth) belong to the same group.
	/// </summary>
	public class RichListGroup : RichBlockGroup
	{
	}
}
