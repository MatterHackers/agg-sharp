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
using MatterHackers.VectorMath;
using static MatterHackers.Agg.Platform.Mac.AppKitConstants;
using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Delivers a trackpad pinch (NSEventMagnify) and rotation (NSEventRotate) to agg.
	/// </summary>
	/// <remarks>
	/// A pinch goes out twice. As a wheel, marked <see cref="MouseEventArgs.FromTrackpadPinch"/>: a pinch is its
	/// own event type, not a modified scroll, and a wheel is what every agg consumer already reads as zoom (fingers
	/// apart, a positive magnification, is zoom in - the direction a wheel pushed forward means). And, with the
	/// rotation, as buttonless two-finger moves from <see cref="TrackpadPinchFingers"/>, for the widgets that
	/// follow fingers - ScenePanZoom's pinch, the Lion view, <see cref="MultiTouchGesture"/>. Those skip the marked
	/// wheel so they zoom once.
	/// </remarks>
	internal sealed class MacTrackpadGestures
	{
		/// <summary>Each virtual finger's starting distance from the pointer, in design units.</summary>
		private const double FingerRadius = 50;

		private readonly TrackpadPinchFingers fingers = new TrackpadPinchFingers(FingerRadius * GuiWidget.DeviceScale);

		/// <param name="pointerArgs">The event made into agg mouse args (pointer position; a magnify's wheel delta).</param>
		public void Deliver(IntPtr nsEvent, long type, MouseEventArgs pointerArgs, SystemWindow window)
		{
			TrackpadGesturePhase phase = ToPhase(Send_Q(nsEvent, Sel("phase")));
			var frames = type == NSEventTypeMagnify
				? this.fingers.Magnify(pointerArgs.Position, Send_d(nsEvent, Sel("magnification")), phase)
				: this.fingers.Rotate(pointerArgs.Position, Send_d(nsEvent, Sel("rotation")), phase);

			if (type == NSEventTypeMagnify)
			{
				pointerArgs.FromTrackpadPinch = true;
				window.OnMouseWheel(pointerArgs);
			}

			foreach (Vector2[] frame in frames)
			{
				window.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, frame, 0, null));
			}
		}

		/// <summary>NSEventPhase to the fingers' phase. A device that reports no phase is always mid-gesture.</summary>
		internal static TrackpadGesturePhase ToPhase(ulong phase)
		{
			if ((phase & (NSEventPhaseEnded | NSEventPhaseCancelled)) != 0)
			{
				return TrackpadGesturePhase.Ended;
			}

			return (phase & NSEventPhaseBegan) != 0 ? TrackpadGesturePhase.Began : TrackpadGesturePhase.Changed;
		}
	}
}
