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
using System.Diagnostics;
using System.IO;
using System.Net.Http;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;

namespace Markdig.Renderers.Agg.Inlines
{
    public class ImageLinkAdvancedX : FlowLayoutWidget
	{
		private static HttpClient client = new HttpClient();

		// Lazy so the StaticData I/O happens at first use rather than at type initialization
		private static readonly Lazy<ImageBuffer> icon = new Lazy<ImageBuffer>(
			() => StaticData.Instance.LoadIcon("internet.png", 16, 16));

		public string Url { get; }

		private readonly ImageBuffer imageBuffer;
		private readonly ImageWidget imageWidget;
		private bool fetchStarted;

		public ImageLinkAdvancedX(string url)
		{
			HAnchor = HAnchor.Fit;
			VAnchor = VAnchor.Fit;
			this.Url = url;

			imageBuffer = new ImageBuffer(icon.Value);
			imageWidget = new ImageWidget(imageBuffer);

			this.AddChild(imageWidget);
		}

		public override void OnLoad(EventArgs args)
		{
			base.OnLoad(args);

			// kick off the fetch once the widget is part of a live tree (not in the constructor)
			if (!fetchStarted)
			{
				fetchStarted = true;
				this.StartFetch();
			}
		}

		private void StartFetch()
		{
			// Deliberately not awaited: OnLoad cannot await, and the widget is fully usable showing its
			// placeholder icon until the image arrives. FetchAsync handles its own failures, so nothing
			// is left as an unobserved task exception.
			_ = this.FetchAsync();
		}

		private async Task FetchAsync()
		{
			try
			{
				if (!Url.StartsWith("http"))
				{
					return;
				}

				var response = await client.GetAsync(Url, HttpCompletionOption.ResponseHeadersRead);

				if (!response.IsSuccessStatusCode)
				{
					return;
				}

				var stream = await response.Content.ReadAsStreamAsync();

				// Where this resumes depends on whether a synchronization context was installed on the
				// thread that called OnLoad, so it may well be a thread-pool thread. All widget-tree and
				// image-buffer mutation is marshaled to the UI thread rather than betting on it.
				UiThread.RunOnIdle(() =>
				{
					if (this.HasBeenClosed)
					{
						// the widget went away before the response arrived
						return;
					}

					// response.Headers.TryGetValues("", s[""] == "" ||
					if (string.Equals(Path.GetExtension(Url), ".svg", StringComparison.OrdinalIgnoreCase))
					{
						// Load svg into SvgWidget, swap for ImageWidget
						try
						{
							var svgWidget = new SvgWidget()
							{
								Border = 1,
								BorderColor = Color.YellowGreen
							};

							svgWidget.LoadSvg(stream, 1);

							this.ReplaceChild(imageWidget, svgWidget);
						}
						catch (Exception svgEx)
						{
							Debug.WriteLine("Error loading svg: {0} :: {1}", Url, svgEx.Message);
						}
					}
					else
					{
						// Load img
						if (!ImageIO.LoadImageData(stream, imageBuffer))
						{
							Debug.WriteLine("Error loading image: " + Url);
						}
					}
				});
			}
			catch (Exception ex)
			{
				// Full ToString, not just Message: this catch now covers the whole fetch - the request, the
				// response read, and the RunOnIdle scheduling - so the stack is what says which one failed.
				Console.WriteLine(ex.ToString());
			}
		}
	}
}
