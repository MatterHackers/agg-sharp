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
	public class Animation : IDisposable
	{
		#region draw target

		private GuiWidget _drawTarget;

		public GuiWidget DrawTarget
		{
			get
			{
				return _drawTarget;
			}
			set
			{
				if (value != _drawTarget)
				{
					// check if we are already hooked to a widget
					if (_drawTarget != null)
					{
						_drawTarget.AfterDraw -= MarkHaveDrawn;
					}

					_drawTarget = value;

					// check for null as we might have cleared it
					if (_drawTarget != null)
					{
						_drawTarget.AfterDraw += MarkHaveDrawn;

						// kick off our first draw
						_drawTarget.Invalidate();
					}
				}
			}
		}

		#endregion draw target

		public class UpdateEvent
		{
			public double SecondsPassed { get; internal set; }
			public bool ShouldDraw { get; set; } = true;
		}

		public EventHandler<UpdateEvent> Update;
		private bool haveDrawn = false;
		private long lastTimeMs;
		private RunningInterval runningInterval;
		private double secondsLeftOverFromLastUpdate;

		public Animation()
		{
		}

		public double FramesPerSecond
		{
			get => 1.0 / SecondsPerUpdate;
			set => SecondsPerUpdate = 1.0 / value;
		}

		public bool IsRunning { get; set; }

		#region MaxUpdatesPerDraw

		private int _maxUpdatesPerDraw = 3;

		public int MaxUpdatesPerDraw
		{
			get => _maxUpdatesPerDraw;
			set
			{
				if (value != _maxUpdatesPerDraw)
				{
					_maxUpdatesPerDraw = Math.Max(1, value);
				}
			}
		}

		#endregion MaxUpdatesPerDraw

		#region SecondsPerUpdate

		private double _secondsPerUpdate = 1.0/30.0;
		public double SecondsPerUpdate
		{
			get
			{
				return _secondsPerUpdate;
			}
			set
			{
				if (value != _secondsPerUpdate)
				{
					_secondsPerUpdate = value;
					if (runningInterval != null)
					{
						// change the interval to the new timing
						Stop();
						Start();
					}
				}
			}
		}

		#endregion SecondsPerUpdate

		public void Dispose()
		{
			Stop();
			DrawTarget = null;
		}

		/// <summary>
		/// override this to do any updating in a derived class
		/// </summary>
		public virtual bool OnUpdate(UpdateEvent updateEvent)
		{
			Update?.Invoke(this, updateEvent);
			return updateEvent.ShouldDraw;
		}

		public void Start()
		{
			// check twice as often as we need to make sure we don't mis our update by too much
			runningInterval = UiThread.SetInterval(this.ProcessElapsedTime, this.SecondsPerUpdate / 2);
			this.IsRunning = true;
		}

		public void Stop()
		{
			if (runningInterval != null)
			{
				this.IsRunning = false;
				UiThread.ClearInterval(runningInterval);
			}
		}

		private void MarkHaveDrawn(object sender, DrawEventArgs e)
		{
			haveDrawn = true;
		}

		private void ProcessElapsedTime()
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
			double maxSecondsToCatchUpOn = MaxUpdatesPerDraw * SecondsPerUpdate;
			if (numSecondsPassedSinceLastUpdate > maxSecondsToCatchUpOn)
			{
				numSecondsPassedSinceLastUpdate = maxSecondsToCatchUpOn;
				secondsLeftOverFromLastUpdate = 0.0;
			}

			// Reset our last tick count. Do this as soon as we can, to make the time more accurate.
			lastTimeMs = currentTimeMs;

			bool needToDoDraw = false;

			// if enough time has gone by that we are willing to do an update
			while (numSecondsPassedSinceLastUpdate >= SecondsPerUpdate)
			{
				// call update with time slices that are as big as secondsPerUpdate
				needToDoDraw |= OnUpdate(new UpdateEvent() { SecondsPassed = SecondsPerUpdate });

				if (DrawTarget.HasBeenClosed)
				{
					DrawTarget.AfterDraw -= MarkHaveDrawn;
					Stop();
				}

				// take out the amount of time we updated and check again
				numSecondsPassedSinceLastUpdate -= SecondsPerUpdate;
			}

			// if there was an update do a draw
			if (needToDoDraw)
			{
				haveDrawn = false;
				DrawTarget.Invalidate();
			}

			// remember the time that we didn't use up yet
			secondsLeftOverFromLastUpdate = numSecondsPassedSinceLastUpdate;
		}
	}
}