/*
Copyright(c) 2026, Lars Brubaker
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
DISCLAIMED.IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
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
using System.Diagnostics;
using MatterHackers.Agg.UI;

namespace Markdig.Agg.Editing
{
	/// <summary>
	/// The rich editor's caret blink, timed as <see cref="InternalTextEditWidget"/> does it: shown for 0.6 s, hidden
	/// for 0.6 s, read from a clock rather than from counted callbacks, with one chain of idle callbacks at a time
	/// (a generation number retires a chain an earlier focus left behind). Any caret move restarts it showing, so
	/// the caret never vanishes the moment it lands.
	/// </summary>
	internal sealed class RichCaretBlink
	{
		private const double OnSeconds = .6;
		private const double OffSeconds = .6;

		private readonly GuiWidget owner;
		private readonly Func<bool> active;
		private readonly Stopwatch clock = new Stopwatch();
		private int generation;

		/// <param name="owner">The widget repainted at each edge of the blink.</param>
		/// <param name="active">Whether to keep blinking (focused and open).</param>
		public RichCaretBlink(GuiWidget owner, Func<bool> active)
		{
			this.owner = owner;
			this.active = active;
		}

		/// <summary>
		/// Whether the caret is in the showing half of its cycle; always true while the blink is stopped.
		/// </summary>
		public bool Showing => !clock.IsRunning
			|| clock.ElapsedMilliseconds % ((OnSeconds + OffSeconds) * 1000) < OnSeconds * 1000;

		/// <summary>
		/// Shows the caret now and blinks from here while the editor is active.
		/// </summary>
		public void Restart()
		{
			if (!active())
			{
				clock.Reset();
				return;
			}

			clock.Restart();
			Schedule(++generation);
			owner.Invalidate();
		}

		// Wakes at the next edge of the blink - when the caret must be painted the other way - and no more often.
		private void Schedule(int chain)
		{
			double cycle = (OnSeconds + OffSeconds) * 1000;
			double position = clock.ElapsedMilliseconds % cycle;
			double untilEdge = position < OnSeconds * 1000 ? OnSeconds * 1000 - position : cycle - position;
			UiThread.RunOnIdle(
				() =>
				{
					if (chain != generation)
					{
						return;
					}

					if (active())
					{
						owner.Invalidate();
						Schedule(chain);
					}
					else
					{
						clock.Reset();
					}
				},
				Math.Max(untilEdge, 1) / 1000);
		}
	}
}
