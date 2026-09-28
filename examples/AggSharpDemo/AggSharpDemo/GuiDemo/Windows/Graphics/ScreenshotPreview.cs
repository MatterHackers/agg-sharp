/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics
{
	/// <summary>
	/// agg-gui's screenshot ImageView: the latest capture shrunk to fit and centred on the app background with a
	/// text-coloured outline, or "No screenshot taken yet." in dim text before the first capture.
	/// </summary>
	public class ScreenshotPreview : GuiWidget
	{
		public const string EmptyText = "No screenshot taken yet.";

		private readonly DemoTheme demoTheme;

		public ScreenshotPreview(DemoTheme demoTheme)
		{
			this.demoTheme = demoTheme;
			this.HAnchor = HAnchor.Stretch;
			this.VAnchor = VAnchor.Stretch;
			this.MinimumSize = new VectorMath.Vector2(0, 120 * DeviceScale);
		}

		/// <summary>The capture shown, or null for none yet.</summary>
		public ImageBuffer Image { get; set; }

		/// <summary>Where the capture lands inside the preview: shrunk to fit, aspect kept, centred.</summary>
		public static RectangleDouble FitRect(double imageWidth, double imageHeight, RectangleDouble bounds)
		{
			double scale = Math.Min(bounds.Width / imageWidth, bounds.Height / imageHeight);
			double width = imageWidth * scale;
			double height = imageHeight * scale;
			double left = bounds.Left + ((bounds.Width - width) / 2);
			double bottom = bounds.Bottom + ((bounds.Height - height) / 2);
			return new RectangleDouble(left, bottom, left + width, bottom + height);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			DemoPalette palette = this.demoTheme.Palette;
			RectangleDouble bounds = this.LocalBounds;
			// The app background rather than a widget fill, so it reads as a neutral pane on every theme.
			graphics2D.Render(new RoundedRect(bounds, 4 * DeviceScale), palette.BackgroundColor);

			if (this.Image != null)
			{
				RectangleDouble target = FitRect(this.Image.Width, this.Image.Height, bounds);
				graphics2D.Render(this.Image, target.Left, target.Bottom, target.Width, target.Height);
				graphics2D.Rectangle(target, palette.TextColor, DeviceScale);
			}
			else
			{
				graphics2D.DrawString(
					EmptyText,
					bounds.Center.X,
					bounds.Center.Y,
					this.demoTheme.Theme.DefaultFontSize * 13 / 12,
					Justification.Center,
					Baseline.BoundsCenter,
					palette.TextDim);
			}

			base.OnDraw(graphics2D);
		}
	}
}
