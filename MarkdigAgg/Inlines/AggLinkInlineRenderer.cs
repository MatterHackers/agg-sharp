/*
Copyright(c) 2024, Lars Brubaker, John Lewin
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

using System;
using Markdig.Agg;
using Markdig.Syntax.Inlines;

namespace Markdig.Renderers.Agg.Inlines
{

    /// <summary>
    /// A Agg renderer for a <see cref="LinkInline"/>.
    /// </summary>
    /// <seealso cref="Markdig.Renderers.Agg.AggObjectRenderer{Markdig.Syntax.Inlines.LinkInline}" />
    public class AggLinkInlineRenderer : AggObjectRenderer<LinkInline>
	{
		/// <inheritdoc/>
		protected override void Write(AggRenderer renderer, LinkInline link)
		{
			var url = link.GetDynamicUrl != null ? link.GetDynamicUrl() ?? link.Url : link.Url;

			if (!Uri.IsWellFormedUriString(url, UriKind.RelativeOrAbsolute))
			{
				url = "#";
			}

			if (!url.StartsWith("http"))
			{
				var pageID = url;

				url = renderer.BaseUri + "/" + url;

				renderer.ChildLinks.Add(new MarkdownDocumentLink()
				{
					Uri = new Uri(url),
					LinkInline = link,
					PageID = pageID
				});
			}

			if (link.IsImage)
			{
				if (link.Parent is LinkInline linkInLine)
				{
					renderer.WriteInline(new ImageLinkSimpleX(renderer, url, linkInLine.Url));
				}
				else
				{
					renderer.WriteInline(new ImageLinkSimpleX(renderer, url));
				}
			}
			else
			{
				if (link.FirstChild is LinkInline linkInLine
					&& linkInLine.IsImage)
				{
					renderer.WriteChildren(link);
				}
				else
				{
					renderer.Push(new TextLinkX(renderer, url, link));
					renderer.WriteChildren(link);
					renderer.Pop();
				}
			}
		}
	}
}
