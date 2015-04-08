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
		private System.Diagnostics.Stopwatch m_PotentialDrawsStopWatch = new System.Diagnostics.Stopwatch();
		private Vector2 m_PotentialDrawsBudgetPosition;
		private MatterHackers.Agg.UI.CheckBox m_ShowPotentialDrawsBudgetGraph;
		private DataViewGraph m_PotentialDrawsBudgetGraph;

		private System.Diagnostics.Stopwatch m_PotentialUpdatesStopWatch = new System.Diagnostics.Stopwatch();
		private Vector2 m_PotentialUpdatesBudgetPosition;
		private MatterHackers.Agg.UI.CheckBox m_ShowPotentialUpdatesBudgetGraph;
		private DataViewGraph m_PotentialUpdatesBudgetGraph;

		private System.Diagnostics.Stopwatch m_ActualDrawsStopWatch = new System.Diagnostics.Stopwatch();
		private Vector2 m_ActualDrawsBudgetPosition;
		private MatterHackers.Agg.UI.CheckBox m_ShowActualDrawsBudgetGraph;
		private DataViewGraph m_ActualDrawsBudgetGraph;

		private bool m_ShowFrameRate;
		private int m_LastSystemTickCount;
		private double m_SecondsLeftOverFromLastUpdate;
		private double m_SecondsPerUpdate;
		private int m_MaxUpdatesPerDraw;
		private double m_NumSecondsSinceStart;

		private static String m_PotentialDrawsPerSecondString = "Potential Draws Per Second";
		private static String m_ActualDrawsPerSecondString = "Actual Draws Per Second";
		private static String m_PotentialUpdatesPerSecondString = "Potential Updates Per Second";

		public GamePlatform(int FramesPerSecond, int MaxUpdatesPerDraw, double width, double height)
			: base(width, height)
		{
			AnchorAll();
			m_ShowFrameRate = true;
			m_SecondsPerUpdate = 1.0 / (double)FramesPerSecond;
			m_MaxUpdatesPerDraw = MaxUpdatesPerDraw;

			AudioSystem.Startup();
			UiThread.RunOnIdle(OnIdle);
		}

		public override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);
			CreateGraphs();
		}

		public bool ShowFrameRate
		{
			get { return m_ShowFrameRate; }
			set
			{
				m_ShowFrameRate = value;
				if (m_ShowFrameRate)
				{
					m_ShowActualDrawsBudgetGraph.Visible = true;
					m_ShowPotentialUpdatesBudgetGraph.Visible = true;
					m_ShowPotentialDrawsBudgetGraph.Visible = true;
				}
				else
				{
					m_ShowActualDrawsBudgetGraph.Visible = false;
					m_ShowPotentialUpdatesBudgetGraph.Visible = false;
					m_ShowPotentialDrawsBudgetGraph.Visible = false;
				}
			}
		}

		public virtual void OnUpdate(double NumSecondsPassed)
		{
		}

		private void CreateGraphs()
		{
			int FrameRateOffset = -15;
			RGBA_Floats FrameRateControlColor = new RGBA_Floats(1, 1, 1, 1);

			m_PotentialDrawsBudgetPosition = new Vector2(10, (double)Height + FrameRateOffset);
			m_ShowPotentialDrawsBudgetGraph = new MatterHackers.Agg.UI.CheckBox(m_PotentialDrawsBudgetPosition.x, m_PotentialDrawsBudgetPosition.y, "D:000.000");
			m_ShowPotentialDrawsBudgetGraph.TextColor = FrameRateControlColor.GetAsRGBA_Bytes();
			//m_ShowPotentialDrawsBudgetGraph.inactive_color(FrameRateControlColor);
			AddChild(m_ShowPotentialDrawsBudgetGraph);
			m_PotentialDrawsBudgetGraph = new DataViewGraph(m_PotentialDrawsBudgetPosition, 100, 100);

			m_PotentialUpdatesBudgetPosition = new Vector2(115, (double)Height + FrameRateOffset);
			m_ShowPotentialUpdatesBudgetGraph = new MatterHackers.Agg.UI.CheckBox(m_PotentialUpdatesBudgetPosition.x, m_PotentialUpdatesBudgetPosition.y, "U:000.000");
			m_ShowPotentialUpdatesBudgetGraph.TextColor = FrameRateControlColor.GetAsRGBA_Bytes();
			//m_ShowPotentialUpdatesBudgetGraph.inactive_color(FrameRateControlColor);
			AddChild(m_ShowPotentialUpdatesBudgetGraph);
			m_PotentialUpdatesBudgetGraph = new DataViewGraph(m_PotentialUpdatesBudgetPosition, 100, 100);

			m_ActualDrawsBudgetPosition = new Vector2(220, (double)Height + FrameRateOffset);
			m_ShowActualDrawsBudgetGraph = new MatterHackers.Agg.UI.CheckBox(m_ActualDrawsBudgetPosition.x, m_ActualDrawsBudgetPosition.y, "A:000.000");
			m_ShowActualDrawsBudgetGraph.TextColor = FrameRateControlColor.GetAsRGBA_Bytes();
			//m_ShowActualDrawsBudgetGraph.inactive_color(FrameRateControlColor);
			AddChild(m_ShowActualDrawsBudgetGraph);
			m_ActualDrawsBudgetGraph = new DataViewGraph(m_ActualDrawsBudgetPosition, 100, 100);
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

			if (m_ShowFrameRate)
			{
				int GraphOffsetY = -105;
				if (m_ShowPotentialDrawsBudgetGraph.Checked)
				{
					Affine Position = Affine.NewTranslation(0, GraphOffsetY);
					m_PotentialDrawsBudgetGraph.Draw(Position, graphics2D);
				}

				if (m_ShowPotentialUpdatesBudgetGraph.Checked)
				{
					Affine Position = Affine.NewTranslation(0, GraphOffsetY);
					m_PotentialUpdatesBudgetGraph.Draw(Position, graphics2D);
				}

				if (m_ShowActualDrawsBudgetGraph.Checked)
				{
					Affine Position = Affine.NewTranslation(0, GraphOffsetY);
					m_ActualDrawsBudgetGraph.Draw(Position, graphics2D);
				}
			}
		}

		public void OnIdle(object state)
		{
			UiThread.RunOnIdle(OnIdle);

			if (!haveDrawn)
			{
				return;
			}

			double NumSecondsPassedSinceLastUpdate = 0.0f;

			int ThisSystemTickCount = Environment.TickCount;

			// handle the counter rolling over
			if (ThisSystemTickCount < m_LastSystemTickCount)
			{
				m_LastSystemTickCount = ThisSystemTickCount;
			}

			// figure out how many seconds have passed
			NumSecondsPassedSinceLastUpdate = (double)((ThisSystemTickCount - m_LastSystemTickCount) / 1000.0f);

			// add to it what we had left over from last time.
			NumSecondsPassedSinceLastUpdate += m_SecondsLeftOverFromLastUpdate;

			// limit it to the max that we are willing to consider
			double MaxSecondsToCatchUpOn = m_MaxUpdatesPerDraw * m_SecondsPerUpdate;
			if (NumSecondsPassedSinceLastUpdate > MaxSecondsToCatchUpOn)
			{
				NumSecondsPassedSinceLastUpdate = MaxSecondsToCatchUpOn;
				m_SecondsLeftOverFromLastUpdate = 0.0f;
			}

			// Reset our last tick count. Do this as soon as we can, to make the time more accurate.
			m_LastSystemTickCount = ThisSystemTickCount;

			bool WasUpdate = false;

			// if enough time has gone by that we are willing to do an update
			while (NumSecondsPassedSinceLastUpdate >= m_SecondsPerUpdate && m_PotentialUpdatesBudgetGraph != null)
			{
				WasUpdate = true;

				m_PotentialUpdatesStopWatch.Restart();
				// call update with time slices that are as big as m_SecondsPerUpdate
				OnUpdate(m_SecondsPerUpdate);
				m_PotentialUpdatesStopWatch.Stop();
				double Seconds = (double)(m_PotentialUpdatesStopWatch.Elapsed.TotalMilliseconds / 1000);
				if (Seconds == 0) Seconds = 1;
				m_PotentialUpdatesBudgetGraph.AddData(m_PotentialUpdatesPerSecondString, 1.0f / Seconds);
				string Lable = string.Format("U:{0:F2}", m_PotentialUpdatesBudgetGraph.GetAverageValue(m_PotentialUpdatesPerSecondString));
				m_ShowPotentialUpdatesBudgetGraph.Text = Lable;

				m_NumSecondsSinceStart += m_SecondsPerUpdate;
				// take out the amount of time we updated and check again
				NumSecondsPassedSinceLastUpdate -= m_SecondsPerUpdate;
			}

			// if there was an update do a draw
			if (WasUpdate)
			{
				m_PotentialDrawsStopWatch.Restart();
				//OnDraw(NewGraphics2D());
				Invalidate();
				m_PotentialDrawsStopWatch.Stop();
				double Seconds = (double)(m_PotentialDrawsStopWatch.Elapsed.TotalMilliseconds / 1000);
				if (Seconds == 0) Seconds = 1;
				m_PotentialDrawsBudgetGraph.AddData(m_PotentialDrawsPerSecondString, 1.0f / Seconds);
				string Lable = string.Format("D:{0:F2}", m_PotentialDrawsBudgetGraph.GetAverageValue(m_PotentialDrawsPerSecondString));
				m_ShowPotentialDrawsBudgetGraph.Text = Lable;

				m_ActualDrawsStopWatch.Stop();
				Seconds = (double)(m_ActualDrawsStopWatch.Elapsed.TotalMilliseconds / 1000);
				if (Seconds == 0) Seconds = 1;
				m_ActualDrawsBudgetGraph.AddData(m_ActualDrawsPerSecondString, 1.0f / Seconds);
				Lable = string.Format("A:{0:F2}", m_ActualDrawsBudgetGraph.GetAverageValue(m_ActualDrawsPerSecondString));
				m_ShowActualDrawsBudgetGraph.Text = Lable;
				m_ActualDrawsStopWatch.Restart();
			}
			else // if there is more than 3 ms before the next update could happen then sleep for 1 ms.
			{
				double ThreeMiliSeconds = 3 / 1000.0f;
				if (ThreeMiliSeconds < m_SecondsPerUpdate - NumSecondsPassedSinceLastUpdate)
				{
					System.Threading.Thread.Sleep(1);
				}
			}

			// remember the time that we didn't use up yet
			m_SecondsLeftOverFromLastUpdate = NumSecondsPassedSinceLastUpdate;
		}
	}
}