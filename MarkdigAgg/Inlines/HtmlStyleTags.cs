/*
Copyright(c) 2026, Lars Brubaker, John Lewin
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
using System.Text.RegularExpressions;
using Markdig.Syntax.Inlines;

namespace Markdig.Renderers.Agg.Inlines
{
	/// <summary>
	/// Bare inline HTML style tags (&lt;strong&gt;/&lt;b&gt;, &lt;em&gt;/&lt;i&gt;, &lt;del&gt;/&lt;s&gt;/&lt;strike&gt;) that style
	/// the text between them the way emphasis does. The viewer and the rich editor's reader both pair them here, so
	/// a span the writer had to wrap in tags (where ** would not read as bold) looks and edits the same in each.
	/// </summary>
	internal static class HtmlStyleTags
	{
		// Only bare tags count: one with attributes (<b class=y>) stays unknown HTML.
		private static readonly Regex StyleTag = new Regex("^</?(strong|b|em|i|del|s|strike)>$", RegexOptions.IgnoreCase);

		/// <summary>The style a tag applies.</summary>
		public enum Kind
		{
			Bold,
			Italic,
			Strike,
		}

		/// <summary>
		/// The style tags among a container's direct children that pair up in order, each mapped to true for the
		/// opening tag and false for the closing one. Unpaired and crossed tags are left out.
		/// </summary>
		public static Dictionary<HtmlInline, bool> Pair(ContainerInline container)
		{
			var paired = new Dictionary<HtmlInline, bool>();
			var open = new List<HtmlInline>();
			foreach (var inline in container)
			{
				if (inline is not HtmlInline tag || Name(tag) == null)
				{
					continue;
				}

				if (!tag.Tag.StartsWith("</"))
				{
					open.Add(tag);
				}
				else if (open.Count > 0 && Name(open[^1]) == Name(tag))
				{
					paired[open[^1]] = true;
					paired[tag] = false;
					open.RemoveAt(open.Count - 1);
				}
				else
				{
					// Crossed tags: the open ones can no longer close in order, so they stay unknown HTML.
					open.Clear();
				}
			}

			return paired;
		}

		/// <summary>The style a paired tag applies.</summary>
		public static Kind StyleOf(HtmlInline tag) => Name(tag) switch
		{
			"strong" or "b" => Kind.Bold,
			"em" or "i" => Kind.Italic,
			_ => Kind.Strike,
		};

		/// <summary>The lower-case element name of a bare style tag, or null for any other HTML.</summary>
		private static string Name(HtmlInline tag)
		{
			var match = StyleTag.Match(tag.Tag ?? "");
			return match.Success ? match.Groups[1].Value.ToLowerInvariant() : null;
		}
	}
}
