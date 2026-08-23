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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
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
	/// The right pointing arrow a <see cref="PopupMenu.SubMenuItemButton"/> paints for itself was built from
	/// raw device pixels, so on a Retina panel it stayed a 6x10 speck beside text that had doubled.
	/// </summary>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> is process wide, so these are keyless <c>[NotInParallel]</c> -
	/// exclusive, not merely serialized against each other - and restore the previous value in a finally.
	/// See <c>SliderDeviceScaleTests</c> for the same pattern.
	/// </remarks>
	public class SubMenuArrowDeviceScaleTests
	{
		// The arrow is the only thing drawn, so its geometry is the bounds of everything painted.
		private const double ArrowWidthAtScaleOne = 6;
		private const double ArrowHeightAtScaleOne = 10;

		/// <summary>
		/// Antialiasing feathers the sloped edges, so a measured extent can run a pixel past the geometry at
		/// each end. That slop does not grow with the arrow, so it stays a fixed tolerance.
		/// </summary>
		private const double AntialiasSlop = 2;

		[Test]
		[NotInParallel]
		public async Task ArrowKeepsItsShapeAtScaleOne()
		{
			(double width, double height) arrow = DrawnArrowExtent(deviceScale: 1);

			await Assert.That(arrow.width).IsEqualTo(ArrowWidthAtScaleOne).Within(AntialiasSlop);
			await Assert.That(arrow.height).IsEqualTo(ArrowHeightAtScaleOne).Within(AntialiasSlop);
		}

		[Test]
		[NotInParallel]
		public async Task ArrowDoublesWithDeviceScale()
		{
			(double width, double height) arrow = DrawnArrowExtent(deviceScale: 2);

			await Assert.That(arrow.width).IsEqualTo(ArrowWidthAtScaleOne * 2).Within(AntialiasSlop)
				.Because("the arrow is the menu item's own geometry, so it has to double when everything else does");
			await Assert.That(arrow.height).IsEqualTo(ArrowHeightAtScaleOne * 2).Within(AntialiasSlop);
		}

		/// <summary>
		/// Renders a sub menu item and returns the pixel bounds of everything it painted. The item is given
		/// blank content and is drawn without its background, so the arrow is all that lands on the image.
		/// </summary>
		private static (double width, double height) DrawnArrowExtent(double deviceScale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = deviceScale;

				var subMenuItem = new PopupMenu.SubMenuItemButton(new GuiWidget(10, 10), new ThemeConfig());

				// The item sizes itself from the theme, so the canvas has to follow it - at scale 2 the row is
				// twice as wide and the arrow sits twice as far to the right.
				var image = new ImageBuffer((int)Math.Ceiling(subMenuItem.Width) + 2, (int)Math.Ceiling(subMenuItem.Height) + 2);
				var graphics2D = image.NewGraphics2D();
				graphics2D.Clear(Color.White);

				subMenuItem.OnDraw(graphics2D);

				return ExtentOfPaintedPixels(image);
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}

		private static (double width, double height) ExtentOfPaintedPixels(ImageBuffer image)
		{
			int left = int.MaxValue;
			int right = int.MinValue;
			int bottom = int.MaxValue;
			int top = int.MinValue;

			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					if (image.GetPixel(x, y) != Color.White)
					{
						left = Math.Min(left, x);
						right = Math.Max(right, x);
						bottom = Math.Min(bottom, y);
						top = Math.Max(top, y);
					}
				}
			}

			if (right < left)
			{
				return (0, 0);
			}

			// Inclusive pixel indices, so the extent is one more than the span.
			return (right - left + 1, top - bottom + 1);
		}
	}
}
