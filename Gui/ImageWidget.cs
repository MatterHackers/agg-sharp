/*
Copyright (c) 2018, Lars Brubaker, John Lewin
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

using System;
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.UI
{
	public class ImageWidget : GuiWidget
	{
		private ImageBuffer image;

		public bool ForcePixelAlignment { get; set; }

		public bool AutoResize { get; set; } = true;

		private bool listenForImageChanged;

		public ImageWidget(int width, int height)
		{
			ForcePixelAlignment = true;
			LocalBounds = new RectangleDouble(0, 0, width, height);
		}

		public ImageWidget(ImageBuffer initialImage)
			: this(initialImage, true) // Image only constructor passes true for classic always registered/listening behavior
		{
		}

		public ImageWidget(ImageBuffer initialImage, bool listenForImageChanged)
			: this(initialImage.Width, initialImage.Height)
		{
			this.listenForImageChanged = listenForImageChanged;
			this.Image = initialImage;
		}

		private void ImageChanged(object s, EventArgs e)
		{
			if (this.AutoResize)
			{
				this.Width = this.Image.Width;
				this.Height = this.Image.Height;
			}

			Invalidate();
		}

		public virtual ImageBuffer Image
		{
			get  => image;
			set
			{
				if (image != null)
				{
					image.ImageChanged -= ImageChanged;
				}

				if (image != value)
				{
					if (listenForImageChanged)
					{
						image = value;
						image.ImageChanged += ImageChanged;
					}
					else
					{
						image = value;
					}

					if (AutoResize)
					{
						LocalBounds = new RectangleDouble(0, 0, image.Width, image.Height);
					}

					Invalidate();
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.Image != null)
			{
				RectangleDouble screenBounds = TransformToScreenSpace(LocalBounds);
				double pixelAlignXAdjust = 0;
				double pixelAlignYAdjust = 0;
				if (ForcePixelAlignment)
				{
					pixelAlignXAdjust = screenBounds.Left - (int)screenBounds.Left;
					pixelAlignYAdjust = screenBounds.Bottom - (int)screenBounds.Bottom;
				}
				graphics2D.Render(this.Image, -pixelAlignXAdjust, -pixelAlignYAdjust);
			}
			base.OnDraw(graphics2D);
		}

		public override void OnClosed(EventArgs e)
		{
			if (this.Image != null)
			{
				this.Image.ImageChanged -= ImageChanged;
			}

			base.OnClosed(e);
		}
	}
}