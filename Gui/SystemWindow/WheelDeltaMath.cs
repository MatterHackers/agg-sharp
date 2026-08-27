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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Turns a host's scroll and pinch numbers into agg's wheel units.
	/// </summary>
	/// <remarks>
	/// Every host reports scrolling the same two ways - a precise device (trackpad, touch surface) in points
	/// of travel, or a detent device (a real wheel) in accelerated lines - and agg's consumers were all
	/// written against Win32's 120-per-detent wheel. That conversion is arithmetic with no platform in it, so
	/// it lives here rather than in any one window host: the macOS host was where it was worked out, and the
	/// browser host meets exactly the same two kinds of event.
	/// </remarks>
	public static class WheelDeltaMath
	{
		/// <summary>
		/// Wheel units per unit of pinch magnification.
		/// </summary>
		/// <remarks>
		/// A magnify gesture reports the <em>incremental</em> change in scale for one event, in the same units
		/// Apple's own sample code accumulates into a zoom factor: magnification 1.0 in total means "twice the
		/// size". Consumers of agg's wheel treat one 120-unit detent as one zoom step, and the 3D view's step
		/// closes 20% of the distance to what is under the pointer. Closing a fraction f of that distance
		/// scales the view by about 1/(1-f), so matching a magnification of m needs f = m, which is m / 0.2 =
		/// 5m detents, i.e. 600m wheel units. A comfortable pinch runs to roughly m = 1, so it travels about
		/// five detents - the same order as a comfortable two-finger scroll, which the precise scroll
		/// conversion below turns into several hundred units.
		/// </remarks>
		private const double MagnifyWheelDeltaPerUnit = 600;

		/// <summary>
		/// Fills both of a mouse event's wheel axes from one scroll event's scrolling deltas.
		/// </summary>
		/// <remarks>
		/// A two finger trackpad scroll carries travel on both axes at once, so both have to come across, and
		/// through the same scale, or a diagonal gesture would come out at the wrong angle. The signs are
		/// carried straight through from the host: positive Y is the forward wheel agg already reads, and
		/// positive X is a gesture whose content should move right.
		/// <para>
		/// That angle is preserved only for a precise scroll. A non-precise event is a detent device on both
		/// axes - a tilt wheel clicks sideways the same way the wheel clicks forward - so each axis quantizes
		/// to its own signed detent and a mixed tilt-and-wheel event deliberately comes out square rather
		/// than at the ratio the host reported. See <see cref="ScrollingDeltaToWheelDelta"/> for why an
		/// accelerated line count is not a magnitude worth preserving.
		/// </para>
		/// </remarks>
		public static void ApplyScrollingDeltas(MouseEventArgs args, double scrollingDeltaX, double scrollingDeltaY, bool precise, double backingScale)
		{
			args.WheelDelta = ScrollingDeltaToWheelDelta(scrollingDeltaY, precise, backingScale);
			args.WheelDeltaX = ScrollingDeltaToWheelDelta(scrollingDeltaX, precise, backingScale);

			// Both axes come from one event and so are the same kind of scroll. The flag is what stops a
			// consumer scaling a precise delta a second time - see ScrollingDeltaToWheelDelta for who owns DPI.
			args.WheelDeltaIsPreciseScroll = precise;
		}

		/// <summary>
		/// Converts one axis of a scroll event's travel into agg's wheel units.
		/// </summary>
		/// <remarks>
		/// agg's consumers were written against Win32's 120-per-detent wheel. A line-based scroll (a real
		/// wheel) becomes one signed detent per event - the v120 convention. macOS accelerates line-based
		/// deltas and exposes no notch count, so the same physical notch reports about 0.1 lines turned
		/// slowly and many lines turned fast, while Win32 reports an unaccelerated 120 per detent no matter
		/// how fast the wheel spins. Consumers scale proportionally to that 120 (MatterCAD's TrackballZoom
		/// zooms by WheelDelta / 120 steps), so passing the acceleration through turned one fast notch into
		/// several detents of zoom. A trackpad instead reports points of travel, and ScrollableWidget
		/// divides WheelDelta by 5 to get pixels - so scaling by 5 x backingScale makes a trackpad drag move
		/// the content the same distance as the fingers.
		/// <para>
		/// <b>This is where DPI is applied to a precise scroll, and the only place.</b> backingScale is the
		/// per-window scale of the display the window is actually on, which is the only correct answer for a
		/// physical distance and the only one that stays correct when a window is dragged between a Retina
		/// screen and an external 1x one. <c>GuiWidget.DeviceScale</c> is not a substitute: it is a user
		/// text-size preference, process-wide rather than per-window, and on a Retina mac MatterCAD sets it
		/// to 1.6 rather than 2. A consumer that scaled by it again would move the content 1.6x too far,
		/// which is the bug <see cref="MouseEventArgs.WheelDeltaIsPreciseScroll"/> exists to prevent - and
		/// why it is set alongside these numbers rather than inferred from them.
		/// </para>
		/// </remarks>
		public static int ScrollingDeltaToWheelDelta(double scrollingDelta, bool precise, double backingScale)
		{
			if (double.IsNaN(scrollingDelta) || double.IsInfinity(scrollingDelta))
			{
				// Neither branch survives a nonsense delta: (int) of a NaN is a huge negative number rather
				// than nothing, which would fling the content, and Math.Sign throws on a NaN - out of a
				// platform event callback, so a crash rather than a fling.
				return 0;
			}

			return precise
				? (int)Math.Round(scrollingDelta * backingScale * 5)
				: Math.Sign(scrollingDelta) * 120;
		}

		/// <summary>
		/// Converts one magnify event's incremental magnification into agg's wheel units. See
		/// <see cref="MagnifyWheelDeltaPerUnit"/> for where the scale comes from; the sign is carried
		/// straight through, so fingers apart (positive) is a forward wheel, which is zoom in.
		/// </summary>
		public static int MagnificationToWheelDelta(double magnification)
		{
			if (double.IsNaN(magnification) || double.IsInfinity(magnification))
			{
				return 0;
			}

			return (int)Math.Round(magnification * MagnifyWheelDeltaPerUnit);
		}
	}
}
