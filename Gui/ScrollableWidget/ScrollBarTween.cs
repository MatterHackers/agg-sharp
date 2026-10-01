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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// A value easing towards a target over a fixed time, ease-out cubic - agg-gui's <c>Tween</c>, which drives its
	/// scroll bar's fade and hover growth.
	/// </summary>
	/// <remarks>
	/// The ease is anchored at the moment the target changes, not at the previous step. A bar at rest is not drawn,
	/// so measuring from the last draw saw seconds pass on the first draw after a hover and jumped straight to the
	/// end - the fade never showed.
	/// </remarks>
	internal sealed class ScrollBarTween
	{
		private readonly double durationMs;

		private double from;

		private double target;

		private long startMs = -1;

		public ScrollBarTween(double initial, double durationMs)
		{
			this.durationMs = durationMs;
			Reset(initial);
		}

		/// <summary>The current eased value.</summary>
		public double Value { get; private set; }

		/// <summary>The value being eased towards.</summary>
		public double Target => target;

		/// <summary>Whether the value is still on its way to <see cref="Target"/>.</summary>
		public bool Animating => startMs >= 0;

		/// <summary>Jumps to <paramref name="value"/> with nothing in flight.</summary>
		public void Reset(double value)
		{
			from = target = Value = value;
			startMs = -1;
		}

		/// <summary>Starts easing from the current value to <paramref name="newTarget"/> at <paramref name="nowMs"/>;
		/// the same target again leaves the ease in flight alone.</summary>
		public void SetTarget(double newTarget, long nowMs)
		{
			if (newTarget != target)
			{
				from = Value;
				target = newTarget;
				startMs = nowMs;
			}
		}

		/// <summary>Moves the value to where the ease is at <paramref name="nowMs"/>; true if it changed.</summary>
		public bool Step(long nowMs)
		{
			if (!Animating)
			{
				return false;
			}

			double progress = Math.Clamp((nowMs - startMs) / durationMs, 0, 1);
			double eased = 1 - Math.Pow(1 - progress, 3);
			double before = Value;
			Value = from + (target - from) * eased;
			if (progress >= 1)
			{
				Value = target;
				startMs = -1;
			}

			return Value != before;
		}
	}
}
