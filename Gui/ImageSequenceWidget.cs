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
using System;
using System.Diagnostics;

namespace MatterHackers.Agg.UI
{
	public class ImageSequenceWidget : GuiWidget
	{
		private Stopwatch runningTime = new Stopwatch();
		private ImageSequence imageSequence;
		private bool runAnimation = false;
		private double lastTimeUpdated = 0;

		public int currentFrame;
		public int CurrentFrame
		{
			get { return currentFrame; }
		}

		public bool RunAnimation
		{
			get { return runAnimation; }
			set
			{
				if (value != runAnimation)
				{
					runAnimation = value;
					if (RunAnimation)
					{
						// we just turned it on so make sure the update is being called
						lastTimeUpdated = 0;
						runningTime.Restart();
						UiThread.RunOnIdle(UpdateAnimation);
					}
				}
			}
		}

		public bool ForcePixelAlignment { get; set; }

		public ImageSequenceWidget(int width, int height)
		{
			ForcePixelAlignment = true;
			LocalBounds = new RectangleDouble(0, 0, width, height);
			RunAnimation = true;
		}

		public ImageSequenceWidget(ImageSequence initialImageSequence)
			: this(initialImageSequence.Width, initialImageSequence.Height)
		{
			ImageSequence = initialImageSequence;
		}

		public ImageSequence ImageSequence
		{
			get
			{
				return imageSequence;
			}

			set
			{
				imageSequence = value;
				LocalBounds = new RectangleDouble(0, 0, imageSequence.Width, imageSequence.Height);
			}
		}

		public override void OnClosed(EventArgs e)
		{
			RunAnimation = false;
			base.OnClosed(e);
		}

		private void UpdateAnimation()
		{
			if (runningTime.Elapsed.TotalSeconds - lastTimeUpdated > imageSequence.SecondsPerFrame)
			{
				lastTimeUpdated = runningTime.Elapsed.TotalSeconds;
				currentFrame = (1+CurrentFrame) % imageSequence.NumFrames;
				Invalidate();
			}

			if (RunAnimation)
			{
				UiThread.RunOnIdle(UpdateAnimation);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			if (imageSequence != null)
			{
				graphics2D.Render(imageSequence.GetImageByIndex(CurrentFrame % imageSequence.NumFrames), 0, 0);
			}
			base.OnDraw(graphics2D);
		}
	}
}