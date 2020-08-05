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

using System;

namespace MatterHackers.Agg.UI
{
	public class ProgressBar : GuiWidget
	{
		public Color FillColor { get; set; }

		public event EventHandler ProgressChanged;

		private double ratioComplete;

		public ProgressBar()
		{
			this.BorderColor = Color.Black;
		}

		public ProgressBar(double width, double height)
			: base(width, height)
		{
		}

		public int PercentComplete
		{
			get => (int)(ratioComplete * 100 + .5);

			set
			{
				if (value != (int)(ratioComplete * ratioComplete + .5))
				{
					ProgressChanged?.Invoke(this, null);
					ratioComplete = value / 100.0;
					Invalidate();
				}
			}
		}

		public double RatioComplete
		{
			get => ratioComplete;

			set
			{
				if (value != ratioComplete)
				{
					ProgressChanged?.Invoke(this, null);
					ratioComplete = value;
					Invalidate();
				}
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			// Restrict fill to valid values
			var fillWidth = Math.Min(Width, Width * RatioComplete);
			if (fillWidth > 0 && fillWidth <= this.Width)
			{
				graphics2D.FillRectangle(0, 0, fillWidth, Height, FillColor);
			}

			graphics2D.Rectangle(LocalBounds, BorderColor);
		}
	}
}