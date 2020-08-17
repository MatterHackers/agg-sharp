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
	public class ResponsiveImageSequenceWidget : GuiWidget
	{
		private ImageSequence _imageSequence;

		private Animation animation = new Animation();

		private double currentTime = 0;

		public ResponsiveImageSequenceWidget(ImageSequence initialSequence)
		{
			HAnchor = HAnchor.Stretch;

			ImageSequence = initialSequence;
			MaximumSize = new Vector2(initialSequence.Width * GuiWidget.DeviceScale,
				initialSequence.Height * GuiWidget.DeviceScale);
			if (ImageSequence != null)
			{
				ImageSequence.Invalidated += ImageChanged;
			}

			animation.DrawTarget = this;
			animation.Update += (s, updateEvent) =>
			{
				var currentImageIndex = ImageSequence.GetImageIndexByTime(currentTime);

				currentTime += updateEvent.SecondsPassed;
				while (ImageSequence.Time > 0
					&& currentTime > ImageSequence.Time)
				{
					currentTime -= ImageSequence.Time;
				}

				var newImageIndex = ImageSequence.GetImageIndexByTime(currentTime);
				updateEvent.ShouldDraw = currentImageIndex != newImageIndex;
			};

			RunAnimation = true;
		}

		public ImageSequence ImageSequence
		{
			get => _imageSequence;

			set
			{
				if (_imageSequence != value)
				{
					// clear the old one
					if (_imageSequence != null)
					{
						_imageSequence.Invalidated -= ResetImageIndex;
					}

					_imageSequence = value;
					animation.FramesPerSecond = _imageSequence.FramesPerSecond;
					currentTime = 0;
					_imageSequence.Invalidated += ResetImageIndex;
				}
			}
		}

		public override RectangleDouble LocalBounds
		{
			get => base.LocalBounds;

			set
			{
				var newBounds = value;
				if (ImageSequence.Width > 0)
				{
					var scale = Math.Min(GuiWidget.DeviceScale, newBounds.Width / ImageSequence.Width);
					newBounds.Top = newBounds.Bottom + ImageSequence.Height * scale;
				}

				base.LocalBounds = newBounds;
			}
		}

		public bool RunAnimation
		{
			get
			{
				return animation != null && animation.IsRunning;
			}

			set
			{
				if (animation != null
					&& value != animation.IsRunning)
				{
					if (value)
					{
						animation.Start();
					}
					else
					{
						animation.Stop();
					}
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (this.ImageSequence?.Frames.Count > 0)
			{
				var currentImage = ImageSequence.GetImageByTime(currentTime);
				graphics2D.Render(currentImage, 0, 0, Width, Height);
			}

			base.OnDraw(graphics2D);
		}

		private void ImageChanged(object s, EventArgs e)
		{
			// kill whatever resize process we are running
			var newBounds = LocalBounds;
			if (ImageSequence.Width > 0)
			{
				var scale = Math.Min(GuiWidget.DeviceScale, newBounds.Width / ImageSequence.Width);
				MaximumSize = new Vector2(ImageSequence.Width * GuiWidget.DeviceScale,
					ImageSequence.Height * GuiWidget.DeviceScale);
				newBounds.Top = newBounds.Bottom + ImageSequence.Height * scale;
				base.LocalBounds = newBounds;
			}

			Invalidate();
		}

		private void ResetImageIndex(object sender, EventArgs e)
		{
			currentTime = 0;
			Invalidate();
		}
	}
}