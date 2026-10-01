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

using System;
using System.Collections.Generic;
using System.Linq;
using Markdig.Syntax.Inlines;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// Loads the images a rich document's image atoms point at, through the markdown viewer's loader
	/// (<see cref="MarkdownWidget.RetrieveImageSquenceAsync"/>), one load per url. When an image arrives the owner
	/// is told on the UI thread so it can re-lay out the blocks showing it at the image's real size.
	/// </summary>
	internal sealed class RichImageCache
	{
		private readonly Dictionary<string, string> urlByMarkdown = new();
		private readonly Dictionary<string, ImageSequence> sequences = new();
		private readonly Action<string> imageLoaded;

		public RichImageCache(Action<string> imageLoaded)
		{
			this.imageLoaded = imageLoaded;
		}

		/// <summary>
		/// Maps an image's markdown url to what the loader fetches (a relative path to a file or an asset url).
		/// </summary>
		public Func<string, string> ResolveUrl { get; set; }

		/// <summary>
		/// The image atom's url, read with Markdig so titles, angle brackets and escapes come out as the viewer
		/// reads them.
		/// </summary>
		public string UrlOf(InlineAtom atom)
		{
			if (!urlByMarkdown.TryGetValue(atom.RawMarkdown, out var url))
			{
				var document = Markdig.Markdown.Parse(atom.RawMarkdown);
				url = Markdig.Syntax.MarkdownObjectExtensions.Descendants<LinkInline>(document).FirstOrDefault(link => link.IsImage)?.Url ?? "";
				urlByMarkdown[atom.RawMarkdown] = url;
			}

			return url;
		}

		/// <summary>
		/// The atom's image once it has loaded, or null while it is loading (or has no loader or url). The first
		/// call for a url starts its load.
		/// </summary>
		public ImageBuffer Loaded(InlineAtom atom)
		{
			string url = UrlOf(atom);
			if (string.IsNullOrWhiteSpace(url))
			{
				return null;
			}

			if (!sequences.TryGetValue(url, out var sequence))
			{
				sequence = new ImageSequence();
				sequences[url] = sequence;

				// The loader invalidates the sequence when the image is in (WebCache also calls its done callback right
				// after; listening to one signal only keeps it to one relayout). A loader may do so on a worker
				// thread, and layout belongs to the UI thread.
				sequence.Invalidated += (s, e) => UiThread.RunOnIdle(() => imageLoaded(url));
				string resolved = ResolveUrl?.Invoke(url) ?? url;
				MarkdownWidget.RetrieveImageSquenceAsync?.Invoke(sequence, resolved, null);
			}

			if (sequence.NumFrames == 0)
			{
				return null;
			}

			var image = sequence.GetImageByIndex(0);
			return image != null && image.Width > 0 && image.Height > 0 ? image : null;
		}
	}
}
