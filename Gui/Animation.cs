/*
Copyright (c) 2018, Lars Brubaker
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
	public class Animation
	{
		private GuiWidget drawTarget;
		private bool haveDrawn = false;
		private long lastTimeMs;
		private int maxUpdatesPerDraw;
		private RunningInterval runningInterval;
		private double secondsLeftOverFromLastUpdate;
		private double secondsPerUpdate;
		private Action<double> update;

		/// <summary>
		/// Attaches an animation engine to the given drawTarget and ensures a smooth animation.		///
		/// </summary>
		/// <param name="drawTarget">GuiWidget that is the draw target of this animation</param>
		/// <param name="update">A function to call to process one interation of the update loop</param>
		/// <param name="secondsPerUpdate">The delay between each update 1/fps if you want to think of it that way.</param>
		/// <param name="maxUpdatesPerDraw">The maximum number of updates to do between draws (animation will slow down if exceeded)</param>
		public Animation(GuiWidget drawTarget,
			Action<double> update,
			double secondsPerUpdate,
			// optional stuff
			int maxUpdatesPerDraw = 3)
		{
			this.update = update;
			this.drawTarget = drawTarget;
			this.secondsPerUpdate = secondsPerUpdate;
			this.maxUpdatesPerDraw = maxUpdatesPerDraw;

			drawTarget.AfterDraw += DrawTarget_AfterDraw;

			// check twice as often as we need to to make sure we don't mis our update by too much
			runningInterval = UiThread.SetInterval(this.OnIdle, this.secondsPerUpdate / 2);

			// kick off our first draw
			drawTarget.Initialize();
		}

		public bool Continue
		{
			get
			{
				return runningInterval == null ? false : runningInterval.Continue;
			}
			set
			{
				if (runningInterval != null)
				{
					runningInterval.Continue = value;
				}
			}
		}

		private void DrawTarget_AfterDraw(object sender, DrawEventArgs e)
		{
			haveDrawn = true;
		}

		private void OnIdle()
		{
			if (!haveDrawn)
			{
				return;
			}

			double numSecondsPassedSinceLastUpdate = 0;

			long currentTimeMs = UiThread.CurrentTimerMs;

			// figure out how many seconds have passed
			numSecondsPassedSinceLastUpdate = (double)((currentTimeMs - lastTimeMs) / 1000.0);

			// add to it what we had left over from last time.
			numSecondsPassedSinceLastUpdate += secondsLeftOverFromLastUpdate;

			// limit it to the max that we are willing to consider
			double maxSecondsToCatchUpOn = maxUpdatesPerDraw * secondsPerUpdate;
			if (numSecondsPassedSinceLastUpdate > maxSecondsToCatchUpOn)
			{
				numSecondsPassedSinceLastUpdate = maxSecondsToCatchUpOn;
				secondsLeftOverFromLastUpdate = 0.0;
			}

			// Reset our last tick count. Do this as soon as we can, to make the time more accurate.
			lastTimeMs = currentTimeMs;

			bool wasUpdate = false;

			// if enough time has gone by that we are willing to do an update
			while (numSecondsPassedSinceLastUpdate >= secondsPerUpdate)
			{
				wasUpdate = true;

				// call update with time slices that are as big as secondsPerUpdate
				update?.Invoke(secondsPerUpdate);

				if (drawTarget.HasBeenClosed)
				{
					drawTarget.AfterDraw -= DrawTarget_AfterDraw;
					Continue = false;
				}

				// take out the amount of time we updated and check again
				numSecondsPassedSinceLastUpdate -= secondsPerUpdate;
			}

			// if there was an update do a draw
			if (wasUpdate)
			{
				haveDrawn = false;
				drawTarget.Invalidate();
			}

			// remember the time that we didn't use up yet
			secondsLeftOverFromLastUpdate = numSecondsPassedSinceLastUpdate;
		}
	}
}