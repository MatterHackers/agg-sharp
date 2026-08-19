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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Every piece of <see cref="Slider"/> geometry the widget picks for itself has to grow with
	/// <see cref="GuiWidget.DeviceScale"/>. The track length is the caller's to size, but the thumb and the
	/// value readout's drop are the widget's own defaults, and they were raw device pixels - so on a Retina
	/// panel the thumb stayed a 10x20 speck next to text that had doubled.
	/// </summary>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> is process wide, so these are keyless <c>[NotInParallel]</c> -
	/// exclusive, not merely serialized against each other - and restore the previous value in a finally.
	/// See <c>ScrollableWheelDpiTests</c> for the same pattern.
	/// </remarks>
	public class SliderDeviceScaleTests
	{
		[Test]
		[NotInParallel]
		public async Task ThumbGrowsWithDeviceScale()
		{
			(double width, double height) atOne = ThumbSize(deviceScale: 1);
			(double width, double height) atTwo = ThumbSize(deviceScale: 2);

			await Assert.That(atTwo.width).IsEqualTo(atOne.width * 2).Within(0.001)
				.Because("the thumb is the slider's own default size, so it has to double when everything else does");
			await Assert.That(atTwo.height).IsEqualTo(atOne.height * 2).Within(0.001);
		}

		[Test]
		[NotInParallel]
		public async Task TheDrawnThumbAndTrackBothGrowWithDeviceScale()
		{
			// LocalBounds is derived from the track and the thumb, so its height is the thumb height and its
			// width is the caller's track plus the half thumb that overhangs each end. Measuring it catches a
			// thumb that scaled but a track that did not, and vice versa.
			RectangleDouble atOne = DerivedBounds(deviceScale: 1);
			RectangleDouble atTwo = DerivedBounds(deviceScale: 2);

			await Assert.That(atTwo.Height).IsEqualTo(atOne.Height * 2).Within(0.001);

			// The caller passes the track length in device pixels already, so a scaled slider is given twice
			// the length; the whole widget therefore doubles rather than growing by only the thumb.
			await Assert.That(atTwo.Width).IsEqualTo(atOne.Width * 2).Within(0.001);
		}

		[Test]
		[NotInParallel]
		public async Task AVerticalSliderDropsItsReadoutByAScaledAmount()
		{
			await Assert.That(ReadoutDropOnVerticalSlider(deviceScale: 2))
				.IsEqualTo(ReadoutDropOnVerticalSlider(deviceScale: 1) * 2).Within(0.001)
				.Because("the readout has to clear a label whose text has doubled");
		}

		private static (double width, double height) ThumbSize(double deviceScale)
		{
			return WithDeviceScale(deviceScale, () =>
			{
				var slider = NewSlider(deviceScale);
				RectangleDouble thumb = slider.GetThumbHitBounds();
				return (thumb.Width, thumb.Height);
			});
		}

		private static RectangleDouble DerivedBounds(double deviceScale)
		{
			return WithDeviceScale(deviceScale, () => NewSlider(deviceScale).LocalBounds);
		}

		private static double ReadoutDropOnVerticalSlider(double deviceScale)
		{
			return WithDeviceScale(deviceScale, () =>
			{
				var slider = new Slider(Vector2.Zero, 160 * deviceScale, orientation: Orientation.Vertical);

				// The readout only takes a position once it has text to lay out.
				slider.Text = "{0:0.00}";

				return -slider.Children[0].OriginRelativeParent.Y;
			});
		}

		/// <summary>
		/// A slider sized the way a caller does it - the track length is theirs to scale, everything else is
		/// the widget's own default.
		/// </summary>
		private static Slider NewSlider(double deviceScale)
		{
			return new Slider(Vector2.Zero, 160 * deviceScale);
		}

		private static T WithDeviceScale<T>(double deviceScale, Func<T> measure)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = deviceScale;
				return measure();
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}
	}
}
