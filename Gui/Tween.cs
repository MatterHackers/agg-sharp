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
	/// A scalar that moves smoothly to a target over a fixed time, eased out (cubic): agg-gui's animation::Tween.
	/// Retargeting mid-flight starts the new move from where the value is now, so a quick reversal (a window
	/// closed while it is still fading in) turns around instead of snapping.
	/// </summary>
	/// <remarks>
	/// Every time - the duration and each moment passed in - is in milliseconds. The moment is passed in (e.g.
	/// <see cref="UiThread.CurrentTimerMs"/>) rather than read here, so a test can step it with a clock of its own. It runs no timer: the owner calls <see cref="Step"/> from its
	/// draw and invalidates while <see cref="IsAnimating"/>, so frames are only drawn while something moves.
	/// The ease is anchored at the moment the target changes, not at the previous step: a widget at rest is not
	/// drawn, so measuring from the last draw would see seconds pass on the first draw after a change and jump
	/// straight to the end (the scroll bar's fade never showed that way).
	/// </remarks>
	public sealed class Tween
	{
		private readonly double durationMs;

		private double startValue;

		private long startMs;

		/// <param name="initial">The value, and target, it starts settled at.</param>
		/// <param name="durationMs">How long a full move to a new target takes, in milliseconds.</param>
		public Tween(double initial, double durationMs)
		{
			this.Value = initial;
			this.Target = initial;
			this.startValue = initial;
			this.durationMs = durationMs;
		}

		/// <summary>The value as of the last <see cref="Step"/>, or the one it was built or <see cref="Reset"/> at.
		/// <see cref="SetTarget"/> does not move it.</summary>
		public double Value { get; private set; }

		/// <summary>Where it is heading: the value last passed to <see cref="SetTarget"/>.</summary>
		public double Target { get; private set; }

		/// <summary>True until a <see cref="Step"/> has reached <see cref="Target"/>.</summary>
		public bool IsAnimating { get; private set; }

		/// <summary>Jumps to <paramref name="value"/> with nothing in flight.</summary>
		public void Reset(double value)
		{
			this.Value = value;
			this.Target = value;
			this.startValue = value;
			this.IsAnimating = false;
		}

		/// <summary>Heads for <paramref name="target"/> from the current value, starting at <paramref name="nowMs"/>.
		/// Does nothing when that is already the target.</summary>
		public void SetTarget(double target, long nowMs)
		{
			if (Math.Abs(this.Target - target) <= 1e-9)
			{
				return;
			}

			this.startValue = this.Value;
			this.Target = target;
			this.startMs = nowMs;
			this.IsAnimating = true;
		}

		/// <summary>Moves the value to where it is at <paramref name="nowMs"/> and returns it.</summary>
		public double Step(long nowMs)
		{
			if (!this.IsAnimating)
			{
				return this.Value;
			}

			double progress = this.durationMs <= 0 ? 1 : Math.Clamp((nowMs - this.startMs) / this.durationMs, 0, 1);
			if (progress >= 1)
			{
				this.Value = this.Target;
				this.IsAnimating = false;
			}
			else
			{
				double eased = 1 - Math.Pow(1 - progress, 3);
				this.Value = this.startValue + (this.Target - this.startValue) * eased;
			}

			return this.Value;
		}
	}
}
