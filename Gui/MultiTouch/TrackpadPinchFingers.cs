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
using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>Where one trackpad gesture event sits in its gesture's life.</summary>
	public enum TrackpadGesturePhase
	{
		/// <summary>The first event of a pinch or rotation: the fingers are placed about the pointer afresh.</summary>
		Began,

		/// <summary>Any event in the middle, and any event from a device that reports no phase.</summary>
		Changed,

		/// <summary>The fingers lifted (or the gesture was cancelled).</summary>
		Ended,
	}

	/// <summary>
	/// Turns a trackpad's pinch and rotate - which a mac reports as a scale and an angle, never as finger
	/// positions - into two virtual fingers either side of the pointer, so everything that follows two-position
	/// mouse moves (<see cref="MultiTouchGesture"/>, ScenePanZoom's pinch) follows a trackpad too.
	/// </summary>
	/// <remarks>
	/// The fingers sit symmetrically about the pointer, so their centre never moves (no pan) while their spread
	/// is the running scale and their angle the running rotation: MultiTouchGesture then reads back exactly the
	/// reported magnification and turn. Pure maths, so the desktop suite tests it; the mac host feeds it.
	/// </remarks>
	public class TrackpadPinchFingers
	{
		/// <summary>Keeps a pinch all the way in from bringing the fingers together, where they would have no angle.</summary>
		private const double MinimumScale = .05;

		private readonly double radius;

		private Vector2 anchor;

		private double scale;

		private double angle;

		/// <param name="radius">How far each finger starts from the pointer, in the pixels the moves are sent in.</param>
		public TrackpadPinchFingers(double radius)
		{
			this.radius = radius;
		}

		/// <summary>True between a gesture's first event and its end.</summary>
		public bool Active { get; private set; }

		/// <summary>Folds in one pinch event.</summary>
		/// <param name="magnification">The change of scale this event, as NSEvent's magnification: 0.1 is 10% bigger.</param>
		/// <returns>The moves to send, oldest first: each the positions of one frame.</returns>
		public IReadOnlyList<Vector2[]> Magnify(Vector2 pointer, double magnification, TrackpadGesturePhase phase)
		{
			return this.Step(pointer, phase, () => this.scale = Math.Max(MinimumScale, this.scale * (1 + magnification)));
		}

		/// <summary>Folds in one rotate event.</summary>
		/// <param name="degrees">The turn this event, as NSEvent's rotation: degrees, counter-clockwise positive.</param>
		/// <returns>The moves to send, oldest first: each the positions of one frame.</returns>
		public IReadOnlyList<Vector2[]> Rotate(Vector2 pointer, double degrees, TrackpadGesturePhase phase)
		{
			return this.Step(pointer, phase, () => this.angle += degrees * Math.PI / 180);
		}

		private IReadOnlyList<Vector2[]> Step(Vector2 pointer, TrackpadGesturePhase phase, Action apply)
		{
			var frames = new List<Vector2[]>();
			if (!this.Active || phase == TrackpadGesturePhase.Began)
			{
				// A frame at rest first: a recogniser takes the first two-finger frame as its baseline, and this
				// event's own change must not be spent on that.
				this.Active = true;
				this.anchor = pointer;
				this.scale = 1;
				this.angle = 0;
				frames.Add(this.Fingers());
			}

			apply();
			frames.Add(this.Fingers());

			if (phase == TrackpadGesturePhase.Ended)
			{
				// One finger left, where the pointer is: every consumer ends its pinch on a single-position move.
				this.Active = false;
				frames.Add(new[] { this.anchor });
			}

			return frames;
		}

		private Vector2[] Fingers()
		{
			Vector2 offset = new Vector2(Math.Cos(this.angle), Math.Sin(this.angle)) * (this.radius * this.scale);
			return new[] { this.anchor - offset, this.anchor + offset };
		}
	}
}
