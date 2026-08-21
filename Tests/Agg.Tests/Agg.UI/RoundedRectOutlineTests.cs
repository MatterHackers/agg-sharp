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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Rendering checks for "stadium" shapes - a rounded rect whose corner radius is exactly half its
	/// height, so the two end caps are full semicircles. Insetting such a rect (for a background behind a
	/// border, or for a stroke centerline) without also shrinking the radius leaves the corner arcs larger
	/// than the box that has to hold them, so they sweep past each other and the outline self-intersects.
	/// </summary>
	/// <remarks>
	/// Every test here renders through <see cref="RenderAtDeviceScaleOne"/>, which writes the process-wide
	/// <see cref="GuiWidget.DeviceScale"/>, so the whole class is a keyless <c>[NotInParallel]</c> -
	/// exclusive, not merely serialized against its siblings - and the helper restores the previous value in
	/// a finally. See <c>ScrollableWheelDpiTests</c> for the same pattern.
	/// </remarks>
	[NotInParallel]
	public class RoundedRectOutlineTests
	{
		private const int Width = 80;
		private const int Height = 24;
		private const double Radius = Height / 2.0;

		// wide enough that a radius which was not reduced along with the inset is unmistakable; the same
		// defect at a one pixel outline is a sub-pixel smear rather than something a test can pin down
		private const double OutlineWidth = 4;

		[Test]
		public async Task StadiumBackgroundOutlineStaysInsideTheStadium()
		{
			var image = RenderAtDeviceScaleOne(graphics2D =>
				GuiWidget.RenderBackground(graphics2D,
					new RectangleDouble(0, 0, Width, Height),
					Color.Blue,
					Radius,
					OutlineWidth,
					Color.Red));

			await AssertNothingPaintedOutsideTheStadium(image);
		}

		/// <summary>
		/// An outline too wide for the widget it surrounds has to paint nothing rather than something wrong.
		/// Insetting by more than half the height turns the rect inside out, and <c>RoundedRect</c>
		/// puts inverted bounds back in order instead of dropping them, so the over-inset background and
		/// stroke came back as ordinary thin rects and painted a band across the middle of the widget.
		/// </summary>
		[Test]
		public async Task AnOutlineTooWideForTheWidgetPaintsNothing()
		{
			// 30 on a 24 tall widget: the background insets by the full stroke and the stroke centerline by
			// half of it, and both are past the 12 of half height there is to give
			const double tooWideOutline = 30;

			var image = RenderAtDeviceScaleOne(graphics2D =>
				GuiWidget.RenderBackground(graphics2D,
					new RectangleDouble(0, 0, Width, Height),
					Color.Blue,
					Radius,
					tooWideOutline,
					Color.Red));

			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					await Assert.That(image.GetPixel(x, y))
						.IsEqualTo(Color.White)
						.Because($"pixel {x}, {y} should be left alone by a background and an outline that do not fit");
				}
			}
		}

		[Test]
		public async Task StadiumProgressBarOutlineStaysInsideTheStadium()
		{
			// no progress, so this sees only the background and the outline - the fill has its own tests
			var image = RenderAtDeviceScaleOne(graphics2D => DrawProgressBar(graphics2D, 0));

			await AssertNothingPaintedOutsideTheStadium(image);
		}

		/// <summary>
		/// An empty ProgressBar has to look exactly like the background every other GuiWidget gets - no
		/// more, no less. ProgressBar.OnDraw used to repeat the background fill and the outline that
		/// OnDrawBackground had already drawn, and a second pass of an antialiased stroke over the first
		/// darkens every partially covered edge pixel.
		/// </summary>
		[Test]
		public async Task EmptyProgressBarPaintsNothingBeyondTheStandardBackground()
		{
			var progressBar = RenderAtDeviceScaleOne(graphics2D => DrawProgressBar(graphics2D, 0));

			var backgroundOnly = RenderAtDeviceScaleOne(graphics2D =>
				GuiWidget.RenderBackground(graphics2D,
					new RectangleDouble(0, 0, Width, Height),
					Color.Blue,
					Radius,
					OutlineWidth,
					Color.Red));

			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					await Assert.That(progressBar.GetPixel(x, y))
						.IsEqualTo(backgroundOnly.GetPixel(x, y))
						.Because($"pixel {x}, {y} of an empty bar should be the plain widget background");
				}
			}
		}

		/// <summary>
		/// A fill narrower than the corner diameter has to give up some of its corner radius. Left at the
		/// full radius its two bottom arcs (and its two top arcs) sweep past each other and the fill paints
		/// a blob far to the right of where the progress actually reaches.
		/// </summary>
		[Test]
		public async Task NarrowProgressFillStaysWithinTheFillWidth()
		{
			const double ratioComplete = .02;

			// the fill lives inside the outline, so it starts at the inner edge and measures its progress
			// across only the room that is left there
			double fillWidth = (Width - OutlineWidth * 2) * ratioComplete;
			double fillRight = OutlineWidth + fillWidth;

			var empty = RenderAtDeviceScaleOne(graphics2D => DrawProgressBar(graphics2D, 0));
			var filled = RenderAtDeviceScaleOne(graphics2D => DrawProgressBar(graphics2D, ratioComplete));

			int firstColumnBeyondTheFill = (int)Math.Ceiling(fillRight);

			for (int x = firstColumnBeyondTheFill; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					await Assert.That(filled.GetPixel(x, y))
						.IsEqualTo(empty.GetPixel(x, y))
						.Because($"pixel {x}, {y} is past the {fillWidth} wide fill that ends at {fillRight} and should look the same as an empty bar");
				}
			}
		}

		/// <summary>
		/// The fill is painted after the outline, so it has to stop at the inside edge of the outline the
		/// way the background does. A fill drawn across the widget's whole bounds swallows the entire
		/// outline ring as the bar approaches 100%.
		/// </summary>
		[Test]
		public async Task FullProgressFillLeavesTheOutlineVisible()
		{
			var image = RenderAtDeviceScaleOne(graphics2D => DrawProgressBar(graphics2D, 1));

			int middleRow = Height / 2;

			// one pixel in from each end cap - far enough in that the whole pixel is inside the outline ring
			// (which spans the outer OutlineWidth of the stadium) rather than straddling its antialiased edge
			await Assert.That(image.GetPixel(1, middleRow))
				.IsEqualTo(Color.Red)
				.Because("the left end cap of the outline must survive a full fill");

			await Assert.That(image.GetPixel(Width - 2, middleRow))
				.IsEqualTo(Color.Red)
				.Because("the right end cap of the outline must survive a full fill");

			// and the fill really did paint, so the assertions above are not passing on an empty bar
			await Assert.That(image.GetPixel(Width / 2, middleRow))
				.IsEqualTo(Color.Green)
				.Because("a full bar should be filled between the end caps");
		}

		/// <summary>
		/// Draws a stadium ProgressBar the way the framework does: OnDrawBackground for the background and
		/// the outline, then OnDraw for the progress fill. ProgressBar.OnDraw paints only the fill, so
		/// calling it alone would leave the outline this file is about out of the image entirely.
		/// </summary>
		private static void DrawProgressBar(Graphics2D graphics2D, double ratioComplete)
		{
			var progressBar = NewStadiumProgressBar(ratioComplete);

			progressBar.OnDrawBackground(graphics2D);
			progressBar.OnDraw(graphics2D);
		}

		private static ProgressBar NewStadiumProgressBar(double ratioComplete)
		{
			return new ProgressBar(Width, Height)
			{
				BackgroundColor = Color.Blue,
				BorderColor = Color.Red,
				FillColor = Color.Green,
				BackgroundRadius = Radius,
				BackgroundOutlineWidth = OutlineWidth,
				RatioComplete = ratioComplete,
			};
		}

		/// <summary>
		/// Renders onto a white <see cref="ImageBuffer"/> the size of the widget. DeviceScale is a static
		/// global that scales every outline width, so it is pinned (and restored) to keep the expected
		/// geometry the same on a Retina display as on a 1:1 one.
		/// </summary>
		private static ImageBuffer RenderAtDeviceScaleOne(Action<Graphics2D> draw)
		{
			var savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;

				var image = new ImageBuffer(Width, Height);
				var graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				draw(graphics2D);

				return image;
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		private static async Task AssertNothingPaintedOutsideTheStadium(ImageBuffer image)
		{
			for (int x = 0; x < Width; x++)
			{
				for (int y = 0; y < Height; y++)
				{
					// a full pixel of slack keeps the antialiased edge of a correctly drawn stadium out of this
					if (DistanceOutsideStadium(x + .5, y + .5) > 1)
					{
						await Assert.That(image.GetPixel(x, y))
							.IsEqualTo(Color.White)
							.Because($"pixel {x}, {y} lies outside the stadium and should never be painted");
					}
				}
			}
		}

		/// <summary>
		/// How far a point sits outside the stadium (negative when inside). The stadium is every point
		/// within <see cref="Radius"/> of the segment joining the centers of the two end caps.
		/// </summary>
		private static double DistanceOutsideStadium(double x, double y)
		{
			double nearestOnAxis = Math.Clamp(x, Radius, Width - Radius);
			double dx = x - nearestOnAxis;
			double dy = y - (Height / 2.0);

			return Math.Sqrt(dx * dx + dy * dy) - Radius;
		}
	}
}
