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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// One frame of a two-or-more finger gesture, as agg-gui's (and egui's) <c>MultiTouchInfo</c>: how much the
	/// fingers pinched, turned and slid since the last frame.
	/// </summary>
	/// <param name="NumTouches">How many fingers are down, two or more.</param>
	/// <param name="ZoomDelta">The spread ratio since the last frame; 1 is no zoom.</param>
	/// <param name="RotationDelta">Radians turned since the last frame, counter-clockwise positive (agg is Y-up).</param>
	/// <param name="TranslationDelta">How far the fingers' centre moved since the last frame.</param>
	/// <param name="Force">The fingers' mean pressure, 0 to 1; 0 where the platform reports none.</param>
	/// <param name="CenterPosition">Where the fingers' centre is now.</param>
	public readonly record struct MultiTouchInfo(
		int NumTouches,
		double ZoomDelta,
		double RotationDelta,
		Vector2 TranslationDelta,
		double Force,
		Vector2 CenterPosition);

	/// <summary>
	/// Turns the multi-position mouse moves a touch host delivers (<see cref="MouseEventArgs.NumPositions"/>)
	/// into <see cref="MultiTouchInfo"/> frames. A widget that wants pinch, rotate and two-finger pan owns one
	/// and feeds it every <see cref="GuiWidget.OnMouseMove"/>.
	/// </summary>
	/// <remarks>
	/// The maths is agg-gui's touch_state.rs: zoom is the mean ratio of each finger's distance from the
	/// centre, rotation the mean change of each finger's angle about it, translation the centre's move. The
	/// frame a finger lands or lifts reports no change at all, because the positions carry no finger ids - a
	/// changed count means the indices may now name different fingers, and comparing them would jump.
	/// </remarks>
	public class MultiTouchGesture
	{
		/// <summary>A finger closer than this to the centre has no meaningful angle or spread, so it sits the frame out.</summary>
		private const double MinimumRadius = 1;

		private Vector2[] previous;

		/// <summary>The latest frame, or null when fewer than two fingers are down.</summary>
		public MultiTouchInfo? Current { get; private set; }

		/// <summary>Folds one mouse move in.</summary>
		/// <returns>This frame's gesture, or null when the move carries fewer than two positions.</returns>
		/// <param name="force">The fingers' mean pressure where the host knows it; MouseEventArgs carries none.</param>
		public MultiTouchInfo? Update(MouseEventArgs mouseEvent, double force = 0)
		{
			int count = mouseEvent.NumPositions;
			if (count < 2)
			{
				this.Reset();
				return null;
			}

			var positions = new Vector2[count];
			for (int i = 0; i < count; i++)
			{
				positions[i] = mouseEvent.GetPosition(i);
			}

			Vector2 center = Centroid(positions);
			bool rebaseline = this.previous == null || this.previous.Length != count;

			double zoomDelta = 1;
			double rotationDelta = 0;
			Vector2 translationDelta = Vector2.Zero;
			if (!rebaseline)
			{
				Vector2 previousCenter = Centroid(this.previous);
				double zoomSum = 0;
				double rotationSum = 0;
				int measured = 0;
				for (int i = 0; i < count; i++)
				{
					Vector2 offset = positions[i] - center;
					Vector2 previousOffset = this.previous[i] - previousCenter;
					if (offset.Length > MinimumRadius && previousOffset.Length > MinimumRadius)
					{
						zoomSum += offset.Length / previousOffset.Length;
						rotationSum += WrapAngle(Math.Atan2(offset.Y, offset.X) - Math.Atan2(previousOffset.Y, previousOffset.X));
						measured++;
					}
				}

				if (measured > 0)
				{
					zoomDelta = zoomSum / measured;
					rotationDelta = WrapAngle(rotationSum / measured);
				}

				translationDelta = center - previousCenter;
			}

			this.previous = positions;
			this.Current = new MultiTouchInfo(count, zoomDelta, rotationDelta, translationDelta, force, center);
			return this.Current;
		}

		/// <summary>Ends the gesture: the next two-finger move is a fresh baseline. Call on mouse up.</summary>
		public void Reset()
		{
			this.previous = null;
			this.Current = null;
		}

		private static Vector2 Centroid(Vector2[] positions)
		{
			Vector2 sum = Vector2.Zero;
			foreach (Vector2 position in positions)
			{
				sum += position;
			}

			return sum / positions.Length;
		}

		/// <summary>Into (-pi, pi], so a finger crossing the +-180 degree seam reads as the small turn it is.</summary>
		private static double WrapAngle(double angle)
		{
			while (angle > Math.PI)
			{
				angle -= 2 * Math.PI;
			}

			while (angle <= -Math.PI)
			{
				angle += 2 * Math.PI;
			}

			return angle;
		}
	}
}
