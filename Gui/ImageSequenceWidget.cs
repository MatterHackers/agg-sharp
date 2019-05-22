/*
Copyright (c) 2014, Lars Brubaker
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

using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg.UI
{
	public class ImageSequenceWidget : GuiWidget
	{
		private ImageSequence _imageSequence;
		private Animation animation = new Animation();
		private double currentTime = 0;

		public ImageSequenceWidget(int width, int height)
		{
			LocalBounds = new RectangleDouble(0, 0, width, height);

			animation.DrawTarget = this;
			animation.Update += this.Animation_Update;

			RunAnimation = true;
		}

		public ImageSequenceWidget(ImageSequence initialImageSequence)
			: this(initialImageSequence.Width, initialImageSequence.Height)
		{
			ImageSequence = initialImageSequence;
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

		private void ResetImageIndex(object sender, EventArgs e)
		{
			currentTime = 0;
			this.Width = ImageSequence.Width;
			this.Height = ImageSequence.Height;
			Invalidate();
		}

		private void Animation_Update(object sender, Animation.UpdateEvent updateEvent)
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
		}

		public bool MaintainAspecRatio { get; set; } = true;

		public bool RunAnimation
		{
			get { return animation != null && animation.IsRunning; }
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

		public bool AllowStretching { get; set; } = false;

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (ImageSequence != null)
			{
				var currentImage = ImageSequence.GetImageByTime(currentTime);
				var bottomLeft = Vector2.Zero;
				var ratio = 1.0;
				if (MaintainAspecRatio)
				{
					ratio = Math.Min(Width / currentImage.Width, Height / currentImage.Height);
					if(!AllowStretching)
					{
						ratio = Math.Min(ratio, 1);
					}
				}

				graphics2D.Render(currentImage,
					Width / 2 - (currentImage.Width * ratio) / 2,
					Height / 2 - (currentImage.Height * ratio) / 2,
					0,
					ratio, ratio);
			}
			base.OnDraw(graphics2D);
		}
	}
}