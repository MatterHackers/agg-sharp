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

using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// What a <see cref="ProgressBar"/> reports as it moves, and what it actually paints.
	/// </summary>
	/// <remarks>
	/// The two rendering tests pin <see cref="GuiWidget.DeviceScale"/> so their expected geometry is the
	/// same on a Retina panel as on a 1:1 one. It is process-wide, so those tests are keyless
	/// <c>[NotInParallel]</c> - exclusive, not merely serialized against their siblings - and restore the
	/// previous value in a finally. See <c>ScrollableWheelDpiTests</c> for the same pattern.
	/// </remarks>
	public class ProgressBarTests
	{
		/// <summary>
		/// PercentComplete has to report a change only when the progress actually moved. Its guard used to
		/// compare the incoming percent against the square of the current ratio rather than against the
		/// current percent, so re-setting the same value fired ProgressChanged (and invalidated) again.
		/// </summary>
		[Test]
		public async Task SettingTheSamePercentTwiceReportsOneChange()
		{
			var progressBar = new ProgressBar(80, 24);

			int changeCount = 0;
			progressBar.ProgressChanged += (s, e) => changeCount++;

			progressBar.PercentComplete = 50;
			progressBar.PercentComplete = 50;

			await Assert.That(changeCount).IsEqualTo(1);
			await Assert.That(progressBar.PercentComplete).IsEqualTo(50);
			await Assert.That(progressBar.RatioComplete).IsEqualTo(.5);
		}

		/// <summary>
		/// The progress fill is drawn in local coordinates, which do not have to start at the origin. The
		/// fill used to be built from a hard coded 0, 0, so a bar whose LocalBounds were offset painted its
		/// progress somewhere other than over its own background.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task FillFollowsLocalBoundsRatherThanTheOrigin()
		{
			const int left = 8;
			const int bottom = 4;
			const int width = 20;
			const int height = 10;

			var progressBar = new ProgressBar(width, height)
			{
				FillColor = Color.Green,
				RatioComplete = .5,
			};

			progressBar.LocalBounds = new RectangleDouble(left, bottom, left + width, bottom + height);

			var savedDeviceScale = GuiWidget.DeviceScale;
			ImageBuffer image;
			try
			{
				GuiWidget.DeviceScale = 1;

				image = new ImageBuffer(left + width * 2, bottom + height * 2);
				var graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				progressBar.OnDraw(graphics2D);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}

			int middleRow = bottom + height / 2;

			await Assert.That(image.GetPixel(left + 2, middleRow))
				.IsEqualTo(Color.Green)
				.Because("the fill starts at the left of the local bounds");

			await Assert.That(image.GetPixel(left - 2, middleRow))
				.IsEqualTo(Color.White)
				.Because("nothing should be painted to the left of the local bounds");

			await Assert.That(image.GetPixel(left + width - 2, middleRow))
				.IsEqualTo(Color.White)
				.Because("a half complete bar should not reach the right of the local bounds");
		}

		/// <summary>
		/// A bar with a BorderColor but no BackgroundOutlineWidth still has to show its border.
		/// GuiWidget.RenderBackground draws a border only when the outline width is greater than zero, so
		/// ProgressBar draws the legacy sharp one pixel rectangle itself - and draws it after the fill, so a
		/// full bar cannot paint over it.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task ZeroOutlineWidthStillDrawsTheBorderOverTheFill()
		{
			const int width = 20;
			const int height = 10;

			var progressBar = new ProgressBar(width, height)
			{
				BackgroundColor = Color.Blue,
				BorderColor = Color.Red,
				FillColor = Color.Green,
				BackgroundOutlineWidth = 0,
				RatioComplete = 1,
			};

			var savedDeviceScale = GuiWidget.DeviceScale;
			ImageBuffer image;
			try
			{
				GuiWidget.DeviceScale = 1;

				image = new ImageBuffer(width, height);
				var graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				progressBar.OnDrawBackground(graphics2D);
				progressBar.OnDraw(graphics2D);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}

			await Assert.That(image.GetPixel(0, 0))
				.IsEqualTo(Color.Red)
				.Because("the corner of the border has to be drawn even with a zero outline width");

			await Assert.That(image.GetPixel(0, height / 2))
				.IsEqualTo(Color.Red)
				.Because("the border is drawn after the fill, so a full bar cannot cover its left edge");

			await Assert.That(image.GetPixel(width / 2, height / 2))
				.IsEqualTo(Color.Green)
				.Because("a full bar should be filled between its borders");
		}
	}
}
