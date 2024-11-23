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
using System.Linq;
using Markdig.Agg;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace Markdig.Renderers.Agg.Inlines
{
    public class ImageLinkSimpleX : FlowLayoutWidget
	{
		private static ImageBuffer icon = StaticData.Instance.LoadIcon("internet.png", 16, 16);

		public ImageLinkSimpleX(AggRenderer renderer, string imageUrl, string linkUrl = null)
		{
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Fit;
			this.Selectable = false;

			this.ImageUrl = imageUrl;
			this.LinkUrl = linkUrl;

			this.aggRenderer = renderer;

			if (linkUrl != null)
			{
				this.Selectable = true;
			}

			sequenceWidget = new ResponsiveImageSequenceWidget(new ImageSequence(icon))
			{
				Cursor = Cursors.Hand,
			};

			sequenceWidget.MaximumSizeChanged += (s, e) =>
			{
				this.MinStretchOrFitHorizontal(20 * GuiWidget.DeviceScale, sequenceWidget.MaximumSize.X);
				if (aggRenderer.RootWidget.Parents<MarkdownWidget>().FirstOrDefault() is MarkdownWidget markdownWidget)
				{
					markdownWidget.Width += 1;
				}
			};

			sequenceWidget.Click += SequenceWidget_Click;

			this.AddChild(sequenceWidget);
		}

		private void SequenceWidget_Click(object sender, MouseEventArgs e)
		{
			if (this.LinkUrl != null)
			{
				if (LinkUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
				{
#if DEBUG
                    if (MarkdownWidget.LaunchBrowser == null)
                    {
                        throw new Exception("You must set the LaunchBrowser action to open a browser.");
                    }
#endif
                    MarkdownWidget.LaunchBrowser?.Invoke(LinkUrl);
				}
				else
				{
					try
					{
						if (aggRenderer.RootWidget.Parents<MarkdownWidget>().FirstOrDefault() is MarkdownWidget markdownWidget)
						{
							markdownWidget.LoadUri(LinkUrl);
						}
					}
					catch
					{
					}
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (!hasBeenLoaded)
			{
				if (ImageUrl.StartsWith("http"))
				{
#if DEBUG
					if (MarkdownWidget.RetrieveImageSquenceAsync == null)
					{
                        throw new Exception("You must set the RetrieveImageSquenceAsync action to retrieve an image.");
                    }
#endif
                    MarkdownWidget.RetrieveImageSquenceAsync?.Invoke(sequenceWidget.ImageSequence, ImageUrl, null);
				}

				hasBeenLoaded = true;
			}

			base.OnDraw(graphics2D);
		}

		/// <summary>
		/// Sets this control to Stretch and all direct parent FlowLayoutWidgets to Stretch, it then ensures
		/// this and all direct parent FlowLayouts have a max width of the contents of this.
		/// </summary>
		/// <param name="absoluteMinWidth">The minimum size will be set to the larger of the existing minimum size or this value.</param>
		/// <param name="absoluteMaxWidth">The maximum size will be set to this value.</param>
		private void MinStretchOrFitHorizontal(double absoluteMinWidth, double absoluteMaxWidth)
		{
			this.HAnchor = HAnchor.Stretch;

			MinimumSize = new Vector2(Math.Max(absoluteMinWidth, MinimumSize.X), MinimumSize.Y);
			MaximumSize = new Vector2(absoluteMaxWidth, MaximumSize.Y);
		}

		public string ImageUrl { get; }

		private string LinkUrl { get; }

		private AggRenderer aggRenderer;
		private bool hasBeenLoaded;
		private ResponsiveImageSequenceWidget sequenceWidget;
	}
}
