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
using Markdig.Syntax.Inlines;

namespace Markdig.Renderers.Agg.Inlines
{
	/// <summary>
	/// An Agg renderer for an <see cref="HtmlInline"/>. A paired bare style tag (&lt;strong&gt;, &lt;em&gt;, &lt;del&gt;
	/// and their short forms) styles the words up to its close the way emphasis does; every other tag, and a style
	/// tag that is unpaired, has attributes or crosses another, is dropped as before.
	/// </summary>
	public class AggHtmlInlineRenderer : AggObjectRenderer<HtmlInline>
	{
		// Markdig writes a container's children in order, so the pairing of the container last seen is reused for
		// each of its tags rather than recomputed per tag.
		private ContainerInline pairedContainer;
		private Dictionary<HtmlInline, bool> pairedTags;

		protected override void Write(AggRenderer renderer, HtmlInline obj)
		{
			if (obj.Parent != pairedContainer)
			{
				pairedContainer = obj.Parent;
				pairedTags = pairedContainer == null ? new Dictionary<HtmlInline, bool>() : HtmlStyleTags.Pair(pairedContainer);
			}

			if (!pairedTags.TryGetValue(obj, out bool opens))
			{
				return;
			}

			if (opens)
			{
				// The words between the tags are siblings of the tags, so the span stays pushed until the close
				// arrives; pairing is properly nested within one container, so each close pops its own open.
				renderer.Push(HtmlStyleTags.StyleOf(obj) switch
				{
					HtmlStyleTags.Kind.Bold => new EmphasisInlineX('*', 2),
					HtmlStyleTags.Kind.Italic => new EmphasisInlineX('*', 1),
					_ => new EmphasisInlineX('~'),
				});
			}
			else
			{
				renderer.Pop();
			}
		}
	}
}
