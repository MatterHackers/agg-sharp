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

using Gaming.Audio;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;

namespace Gaming.Game
{
	public class GamePlatform : SystemWindow
	{
		private System.Diagnostics.Stopwatch potentialDrawsStopWatch = new System.Diagnostics.Stopwatch();
		private Vector2 potentialDrawsBudgetPosition;
		private MatterHackers.Agg.UI.CheckBox showPotentialDrawsBudgetGraph;
		private DataViewGraph potentialDrawsBudgetGraph;

		private System.Diagnostics.Stopwatch potentialUpdatesStopWatch = new System.Diagnostics.Stopwatch();
		private Vector2 potentialUpdatesBudgetPosition;
		private MatterHackers.Agg.UI.CheckBox showPotentialUpdatesBudgetGraph;
		private DataViewGraph potentialUpdatesBudgetGraph;

		private System.Diagnostics.Stopwatch actualDrawsStopWatch = new System.Diagnostics.Stopwatch();
		private Vector2 actualDrawsBudgetPosition;
		private MatterHackers.Agg.UI.CheckBox showActualDrawsBudgetGraph;
		private DataViewGraph actualDrawsBudgetGraph;

		private bool showFrameRate;
		private int lastSystemTickCount;
		private double secondsLeftOverFromLastUpdate;
		private double secondsPerUpdate;
		private int maxUpdatesPerDraw;
		private double numSecondsSinceStart;

		private static String potentialDrawsPerSecondString = "Potential Draws Per Second";
		private static String actualDrawsPerSecondString = "Actual Draws Per Second";
		private static String potentialUpdatesPerSecondString = "Potential Updates Per Second";

		public GamePlatform(int framesPerSecond, int maxUpdatesPerDraw, double width, double height)
			: base(width, height)
		{
			AnchorAll();
			showFrameRate = true;
			secondsPerUpdate = 1.0 / (double)framesPerSecond;
			this.maxUpdatesPerDraw = maxUpdatesPerDraw;

			AudioSystem.Startup();
			UiThread.SetInterval(OnIdle, secondsPerUpdate);
		}

		public override void OnLoad(EventArgs args)
		{
			CreateGraphs();
			base.OnLoad(args);
		}

		public bool ShowFrameRate
		{
			get { return showFrameRate; }
			set
			{
				showFrameRate = value;
				if (showFrameRate)
				{
					showActualDrawsBudgetGraph.Visible = true;
					showPotentialUpdatesBudgetGraph.Visible = true;
					showPotentialDrawsBudgetGraph.Visible = true;
				}
				else
				{
					showActualDrawsBudgetGraph.Visible = false;
					showPotentialUpdatesBudgetGraph.Visible = false;
					showPotentialDrawsBudgetGraph.Visible = false;
				}
			}
		}

		public virtual void OnUpdate(double NumSecondsPassed)
		{
		}

		private void CreateGraphs()
		{
			int FrameRateOffset = -15;
			ColorF FrameRateControlColor = new ColorF(1, 1, 1, 1);

			potentialDrawsBudgetPosition = new Vector2(10, (double)Height + FrameRateOffset);
			showPotentialDrawsBudgetGraph = new MatterHackers.Agg.UI.CheckBox(potentialDrawsBudgetPosition.X, potentialDrawsBudgetPosition.Y, "D:000.000");
			showPotentialDrawsBudgetGraph.TextColor = FrameRateControlColor.ToColor();
			//showPotentialDrawsBudgetGraph.inactive_color(FrameRateControlColor);
			AddChild(showPotentialDrawsBudgetGraph);
			potentialDrawsBudgetGraph = new DataViewGraph(potentialDrawsBudgetPosition, 100, 100);

			potentialUpdatesBudgetPosition = new Vector2(115, (double)Height + FrameRateOffset);
			showPotentialUpdatesBudgetGraph = new MatterHackers.Agg.UI.CheckBox(potentialUpdatesBudgetPosition.X, potentialUpdatesBudgetPosition.Y, "U:000.000");
			showPotentialUpdatesBudgetGraph.TextColor = FrameRateControlColor.ToColor();
			//showPotentialUpdatesBudgetGraph.inactive_color(FrameRateControlColor);
			AddChild(showPotentialUpdatesBudgetGraph);
			potentialUpdatesBudgetGraph = new DataViewGraph(potentialUpdatesBudgetPosition, 100, 100);

			actualDrawsBudgetPosition = new Vector2(220, (double)Height + FrameRateOffset);
			showActualDrawsBudgetGraph = new MatterHackers.Agg.UI.CheckBox(actualDrawsBudgetPosition.X, actualDrawsBudgetPosition.Y, "A:000.000");
			showActualDrawsBudgetGraph.TextColor = FrameRateControlColor.ToColor();
			//showActualDrawsBudgetGraph.inactive_color(FrameRateControlColor);
			AddChild(showActualDrawsBudgetGraph);
			actualDrawsBudgetGraph = new DataViewGraph(actualDrawsBudgetPosition, 100, 100);
		}

		public override void OnClosed(EventArgs e)
		{
			AudioSystem.Shutdown();

			base.OnClosed(e);
		}

		private bool haveDrawn = false;

		public override void OnDraw(Graphics2D graphics2D)
		{
			haveDrawn = true;
			base.OnDraw(graphics2D);

			if (showFrameRate)
			{
				int GraphOffsetY = -105;
				if (showPotentialDrawsBudgetGraph.Checked)
				{
					Affine Position = Affine.NewTranslation(0, GraphOffsetY);
					potentialDrawsBudgetGraph.Draw(Position, graphics2D);
				}

				if (showPotentialUpdatesBudgetGraph.Checked)
				{
					Affine Position = Affine.NewTranslation(0, GraphOffsetY);
					potentialUpdatesBudgetGraph.Draw(Position, graphics2D);
				}

				if (showActualDrawsBudgetGraph.Checked)
				{
					Affine Position = Affine.NewTranslation(0, GraphOffsetY);
					actualDrawsBudgetGraph.Draw(Position, graphics2D);
				}
			}
		}

		public void OnIdle()
		{
			if (!haveDrawn)
			{
				return;
			}

			double numSecondsPassedSinceLastUpdate = 0;

			int ThisSystemTickCount = Environment.TickCount;

			// handle the counter rolling over
			if (ThisSystemTickCount < lastSystemTickCount)
			{
				lastSystemTickCount = ThisSystemTickCount;
			}

			// figure out how many seconds have passed
			numSecondsPassedSinceLastUpdate = (double)((ThisSystemTickCount - lastSystemTickCount) / 1000.0);

			// add to it what we had left over from last time.
			numSecondsPassedSinceLastUpdate += secondsLeftOverFromLastUpdate;

			// limit it to the max that we are willing to consider
			double MaxSecondsToCatchUpOn = maxUpdatesPerDraw * secondsPerUpdate;
			if (numSecondsPassedSinceLastUpdate > MaxSecondsToCatchUpOn)
			{
				numSecondsPassedSinceLastUpdate = MaxSecondsToCatchUpOn;
				secondsLeftOverFromLastUpdate = 0.0;
			}

			// Reset our last tick count. Do this as soon as we can, to make the time more accurate.
			lastSystemTickCount = ThisSystemTickCount;

			bool WasUpdate = false;

			// if enough time has gone by that we are willing to do an update
			while (numSecondsPassedSinceLastUpdate >= secondsPerUpdate && potentialUpdatesBudgetGraph != null)
			{
				WasUpdate = true;

				potentialUpdatesStopWatch.Restart();
				// call update with time slices that are as big as secondsPerUpdate
				OnUpdate(secondsPerUpdate);
				potentialUpdatesStopWatch.Stop();
				double Seconds = (double)(potentialUpdatesStopWatch.Elapsed.TotalMilliseconds / 1000);
				if (Seconds == 0) Seconds = 1;
				potentialUpdatesBudgetGraph.AddData(potentialUpdatesPerSecondString, 1.0 / Seconds);
				string Lable = string.Format("U:{0:F2}", potentialUpdatesBudgetGraph.GetAverageValue(potentialUpdatesPerSecondString));
				showPotentialUpdatesBudgetGraph.Text = Lable;

				numSecondsSinceStart += secondsPerUpdate;
				// take out the amount of time we updated and check again
				numSecondsPassedSinceLastUpdate -= secondsPerUpdate;
			}

			// if there was an update do a draw
			if (WasUpdate)
			{
				potentialDrawsStopWatch.Restart();
				//OnDraw(NewGraphics2D());
				Invalidate();
				potentialDrawsStopWatch.Stop();
				double Seconds = (double)(potentialDrawsStopWatch.Elapsed.TotalMilliseconds / 1000);
				if (Seconds == 0) Seconds = 1;
				potentialDrawsBudgetGraph.AddData(potentialDrawsPerSecondString, 1.0 / Seconds);
				string Lable = string.Format("D:{0:F2}", potentialDrawsBudgetGraph.GetAverageValue(potentialDrawsPerSecondString));
				showPotentialDrawsBudgetGraph.Text = Lable;

				actualDrawsStopWatch.Stop();
				Seconds = (double)(actualDrawsStopWatch.Elapsed.TotalMilliseconds / 1000);
				if (Seconds == 0) Seconds = 1;
				actualDrawsBudgetGraph.AddData(actualDrawsPerSecondString, 1.0 / Seconds);
				Lable = string.Format("A:{0:F2}", actualDrawsBudgetGraph.GetAverageValue(actualDrawsPerSecondString));
				showActualDrawsBudgetGraph.Text = Lable;
				actualDrawsStopWatch.Restart();
			}

			// remember the time that we didn't use up yet
			secondsLeftOverFromLastUpdate = numSecondsPassedSinceLastUpdate;
		}
	}
}