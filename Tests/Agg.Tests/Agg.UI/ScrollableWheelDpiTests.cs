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

using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Pins which of a scroll's two scales <see cref="ScrollableWidget"/> is allowed to apply. A precise
	/// (trackpad) delta arrives already measured in device pixels, so the widget must move the content by
	/// exactly that many and no more; a line based delta (a real wheel detent) carries no size at all, so the
	/// widget supplies <see cref="GuiWidget.DeviceScale"/> to keep a detent worth the same number of the
	/// text lines it is drawing. Getting that backwards double counted DPI and scrolled a Retina trackpad at
	/// twice the finger travel.
	/// </summary>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> is process-wide and read by anything that lays out or paints, so
	/// every test here is a keyless <c>[NotInParallel]</c> - exclusive, not merely serialized against its
	/// siblings - and restores the previous value in a finally. See <c>FontCacheEvictionTests</c> for the
	/// same pattern and what a mere constraint key failed to prevent.
	/// </remarks>
	public class ScrollableWheelDpiTests
	{
		[Test]
		[NotInParallel]
		public async Task ATrackpadScrollMovesTheContentExactlyAsFarAsTheFingers()
		{
			// 100 wheel units is 20 device pixels of travel that the platform already scaled for the display.
			// The user's text size says how big a line of text is, which has nothing to do with how far a
			// finger moved, so it must not appear here at either scale or on either axis.
			await Assert.That(ScrolledBy(PreciseScroll(wheelDelta: -100), deviceScale: 1).Y).IsEqualTo(20).Within(0.001);
			await Assert.That(ScrolledBy(PreciseScroll(wheelDelta: -100), deviceScale: 2).Y).IsEqualTo(20).Within(0.001);

			await Assert.That(ScrolledBy(PreciseScroll(wheelDeltaX: -100), deviceScale: 1).X).IsEqualTo(-20).Within(0.001);
			await Assert.That(ScrolledBy(PreciseScroll(wheelDeltaX: -100), deviceScale: 2).X).IsEqualTo(-20).Within(0.001);
		}

		[Test]
		[NotInParallel]
		public async Task AWheelDetentScrollsWithTheSizeTheWidgetsAreDrawnAt()
		{
			// Win32's 120 per detent says "one detent", not "this far" - so 24 pixels at the size the UI was
			// designed at, and proportionally more when the widgets themselves are drawn bigger. This is the
			// one scroll that does want DeviceScale, and it is the reason the widget cannot simply drop it.
			await Assert.That(ScrolledBy(LineScroll(wheelDelta: -120), deviceScale: 1).Y).IsEqualTo(24).Within(0.001);
			await Assert.That(ScrolledBy(LineScroll(wheelDelta: -120), deviceScale: 2).Y).IsEqualTo(48).Within(0.001);

			await Assert.That(ScrolledBy(LineScroll(wheelDeltaX: -120), deviceScale: 1).X).IsEqualTo(-24).Within(0.001);
			await Assert.That(ScrolledBy(LineScroll(wheelDeltaX: -120), deviceScale: 2).X).IsEqualTo(-48).Within(0.001);
		}

		[Test]
		[NotInParallel]
		public async Task TheClonedEventKeepsThePreciseFlag()
		{
			// This is the constructor GuiWidget uses to re-base an event into a child's coordinates. If the
			// flag were dropped here every nested scroll panel - which is every one that matters, the path
			// editor and the sheet editor both sit inside another - would go back to double counting DPI.
			var original = new MouseEventArgs(MouseButtons.None, 0, 10, 20, -100)
			{
				WheelDeltaX = -45,
				WheelDeltaIsPreciseScroll = true,
			};

			var moved = new MouseEventArgs(original, 3, 4);

			await Assert.That(moved.WheelDeltaIsPreciseScroll).IsTrue();
		}

		[Test]
		[NotInParallel]
		public async Task AnEventNobodyMarkedIsTreatedAsAWheel()
		{
			// Every platform but the mac, and every synthetic event in the codebase, packs Win32 wheel units
			// and never sets the flag - so the default has to be the line based path they were written for.
			var wheelEvent = new MouseEventArgs(MouseButtons.None, 0, 10, 20, -120);

			await Assert.That(wheelEvent.WheelDeltaIsPreciseScroll).IsFalse();
			await Assert.That(ScrolledBy(wheelEvent, deviceScale: 2).Y).IsEqualTo(48).Within(0.001);
		}

		private static MouseEventArgs PreciseScroll(int wheelDelta = 0, int wheelDeltaX = 0)
		{
			return new MouseEventArgs(MouseButtons.None, 0, 100, 100, wheelDelta)
			{
				WheelDeltaX = wheelDeltaX,
				WheelDeltaIsPreciseScroll = true,
			};
		}

		private static MouseEventArgs LineScroll(int wheelDelta = 0, int wheelDeltaX = 0)
		{
			return new MouseEventArgs(MouseButtons.None, 0, 100, 100, wheelDelta)
			{
				WheelDeltaX = wheelDeltaX,
			};
		}

		/// <summary>
		/// How far one scroll event moves the content of a panel with room to move on both axes, with
		/// <see cref="GuiWidget.DeviceScale"/> held at <paramref name="deviceScale"/> for the duration.
		/// </summary>
		private static Vector2 ScrolledBy(MouseEventArgs scrollEvent, double deviceScale)
		{
			double savedDeviceScale = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = deviceScale;

				// Twice the view in both directions, so nothing here is measuring a clamp at the end of the travel.
				var scrollable = new ScrollableWidget(200, 200, autoScroll: true);
				scrollable.AddChild(new GuiWidget(400, 400));
				scrollable.PerformLayout();

				Vector2 before = scrollable.ScrollPosition;
				scrollable.OnMouseWheel(scrollEvent);

				return scrollable.ScrollPosition - before;
			}
			finally
			{
				GuiWidget.DeviceScale = savedDeviceScale;
			}
		}
	}
}
