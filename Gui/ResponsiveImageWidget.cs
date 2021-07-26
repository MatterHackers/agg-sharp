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
		private ImageBuffer _image;

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

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (Image != null)
			{
				graphics2D.Render(Image, 0, 0, Width, Height);
			}

			base.OnDraw(graphics2D);
		}
	}
}