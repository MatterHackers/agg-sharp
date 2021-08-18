/*
Copyright (c) 2018, Lars Brubaker
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
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class ResponsiveImageWidget : GuiWidget
	{
		private ImageBuffer checkerboard;
		private ImageBuffer _image;

		public bool RenderCheckerboard { get; set; }

		public ResponsiveImageWidget(ImageBuffer initialImage)
		{
			this.HAnchor = HAnchor.Stretch;
			this.Image = initialImage;
		}

		public override RectangleDouble LocalBounds
		{
			get => base.LocalBounds;
			set
			{
				var newBounds = value;
				if (Image.Width > 0)
				{
					var scale = Math.Min(GuiWidget.DeviceScale, Math.Min(this.MaximumSize.X, newBounds.Width) / Image.Width);
					newBounds.Top = newBounds.Bottom + Image.Height * scale;
				}

				base.LocalBounds = newBounds;
			}
		}

		private void ImageChanged(object s, EventArgs e)
		{
			// kill whatever resize process we are running
			var newBounds = LocalBounds;
			if (Image.Width > 0)
			{
				var scale = Math.Min(GuiWidget.DeviceScale, newBounds.Width / Image.Width);
				MaximumSize = new Vector2(Image.Width * GuiWidget.DeviceScale,
					Image.Height * GuiWidget.DeviceScale);
				newBounds.Top = newBounds.Bottom + Image.Height * scale;
				base.LocalBounds = newBounds;
			}

			Invalidate();
		}

		public ImageBuffer Image
		{
			get => _image;
			set
			{
				if(_image != null)
				{
					_image.ImageChanged -= ImageChanged;
				}
				_image = value;
				_image.ImageChanged += ImageChanged;
				ImageChanged(this, null);
			}
		}

		private void RenderCheckerboard2(ImageBuffer image, int size, Color colorA, Color colorB)
		{
			var graphics2D = image.NewGraphics2D();
			var width = image.Width;
			var height = image.Height;

			byte[] buffer = image.GetBuffer();
			Parallel.For(0, image.Height, (y) =>
			{
				int imageOffset = image.GetBufferOffsetY(y);

				for (int x = 0; x < image.Width; x++)
				{
					int imageBufferOffsetWithX = imageOffset + x * 4;
					if (((x / size) + (y / size)) % 2 == 0)
					{
						buffer[imageBufferOffsetWithX + 0] = colorA.blue;
						buffer[imageBufferOffsetWithX + 1] = colorA.green;
						buffer[imageBufferOffsetWithX + 2] = colorA.red;
					}
					else
					{
						buffer[imageBufferOffsetWithX + 0] = colorB.blue;
						buffer[imageBufferOffsetWithX + 1] = colorB.green;
						buffer[imageBufferOffsetWithX + 2] = colorB.red;
					}

					buffer[imageBufferOffsetWithX + 3] = 255;
				}
			});
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (RenderCheckerboard)
			{
				if (checkerboard == null)
				{
					checkerboard = new ImageBuffer();
				}

				if (checkerboard.Width != graphics2D.Width || checkerboard.Height != graphics2D.Height)
				{
					checkerboard.Allocate(graphics2D.Width, graphics2D.Height, 32, new BlenderBGRA());
					// render a checkerboard that can show through the alpha mask
					var w = (int)(10 * GuiWidget.DeviceScale);
					RenderCheckerboard2(checkerboard, w, Color.White, Color.LightGray);
				}

				graphics2D.Render(checkerboard, 0, 0);
			}

			base.OnDraw(graphics2D);
		
			if (Image != null)
			{
				var sizeX = Math.Min(Width, Image.Width);
				graphics2D.Render(Image, (Width - sizeX) / 2, 0, sizeX, Height);
			}
		}
	}
}