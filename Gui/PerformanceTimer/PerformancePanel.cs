/*
Copyright (c) 2015, Lars Brubaker
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
using System.Collections.Generic;
using System.Diagnostics;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public class PerformancePanel : FlowLayoutWidget
	{
		private static PerformanceDisplayWidget panels = null;

		private static Dictionary<string, PerformancePanel> resultsPanels = new Dictionary<string, PerformancePanel>();

		private int recursionCount = 0;

		private Dictionary<string, PerformanceTimerDisplayData> timeDisplayData = new Dictionary<string, PerformanceTimerDisplayData>();

		private FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);

		public PerformancePanel(string name)
			: base(FlowDirection.TopToBottom)
		{
			this.Name = name;
			Margin = new BorderDouble(5);
			Padding = new BorderDouble(3);
			VAnchor |= VAnchor.Top;

			if (panels == null)
			{
				panels = new PerformanceDisplayWidget();
				panels.Selectable = false;
				panels.HAnchor |= HAnchor.Left;
				panels.VAnchor |= VAnchor.Top;
				panels.Visible = false; // start out not visible

				if (true) // only add this when doing testing
				{
					UiThread.RunOnIdle(() =>
					{
						if (PerformanceTimer.GetParentWindowFunction != null)
						{
							GuiWidget parentWindow = PerformanceTimer.GetParentWindowFunction();
							parentWindow.AddChild(panels);
#if DEBUG
							parentWindow.KeyDown += ParentWindow_KeyDown;
							parentWindow.MouseDown += this.ParentWindow_MouseDown;
#endif
						}
					});
				}
			}

			// add in the column title
			{
				TextWidget titleWidget = new TextWidget(name, pointSize: 14)
				{
					BackgroundColor = new Color(),
					TextColor = new Color(20, 120, 20),
				};
				titleWidget.Printer.DrawFromHintedCache = true;
				AddChild(titleWidget);
			}

			AddChild(topToBottom);

			panels.AddChild(this);

			BackgroundColor = new Color(Color.White, 180);
		}

		public static PerformancePanel GetNamedPanel(string panelName)
		{
			if (!resultsPanels.ContainsKey(panelName))
			{
				PerformancePanel timingPanelToReportTo = new PerformancePanel(panelName);
				resultsPanels.Add(panelName, timingPanelToReportTo);
			}

			return resultsPanels[panelName];
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			// Make sure the children are in the right draw order for the way they were called
			List<PerformanceTimerDisplayData> allRecords = new List<PerformanceTimerDisplayData>();

			foreach (KeyValuePair<string, PerformanceTimerDisplayData> displayItemKeyValue in timeDisplayData)
			{
				allRecords.Add(displayItemKeyValue.Value);
			}

			allRecords.Sort(SortOnDrawOrder);

			foreach (PerformanceTimerDisplayData timerData in allRecords)
			{
				int curIndex = -1;
				topToBottom.Children.Modify((list) =>
				{
					curIndex = list.IndexOf(timerData.widget);

					if (curIndex != -1)
					{
						if (timerData.drawOrder < int.MaxValue
						&& curIndex != timerData.drawOrder)
						{
							list.RemoveAt(curIndex);
							list.Insert(Math.Min(timerData.drawOrder, topToBottom.Children.Count), timerData.widget);
						}
					}
				});

				timerData.startTimeMs = 0;
				timerData.TotalCount = 0;
				timerData.ActiveCount = 0;
			}

			base.OnDraw(graphics2D);
		}

		public void Stop(PerformanceTimer timer)
		{
			recursionCount--;

			PerformanceTimerDisplayData timerData = timeDisplayData[timer.Name];

			timerData.ActiveCount--;

			if (timerData.ActiveCount == 0)
			{
				timerData.endTimeMs = UiThread.CurrentTimerMs;

				string outputText = "{0:0.00} ms - {1}".FormatWith(timerData.ElapsedMs, timer.Name);
				if (timerData.TotalCount > 1)
				{
					outputText += " ({0})".FormatWith(timerData.TotalCount);
				}

				if (recursionCount > 0)
				{
					if (recursionCount == 1)
					{
						outputText = "|_" + outputText;
					}
					else
					{
						outputText = new string(' ', recursionCount - 1) + "|_" + outputText;
					}
				}

				// TODO: put this is a pre-draw variable to set next time we are going to draw
				// Doing it here causes an invalidate and endless drawing.
				timerData.widget.Text = outputText;
			}
		}

		internal void Start(PerformanceTimer timer)
		{
			if (!timeDisplayData.ContainsKey(timer.Name))
			{
				PerformanceTimerDisplayData newTimerData = new PerformanceTimerDisplayData(timer.Name)
				{
					widget = new TextWidget("waiting")
					{
						AutoExpandBoundsToText = true,
						TextColor = new Color(120, 20, 20),
						HAnchor = HAnchor.Left,
					}
				};

				newTimerData.widget.Printer.DrawFromHintedCache = true;
				timeDisplayData.Add(timer.Name, newTimerData);

				topToBottom.AddChild(newTimerData.widget);
			}

			if (recursionCount == 0)
			{
				foreach (KeyValuePair<string, PerformanceTimerDisplayData> displayItemKeyValue in timeDisplayData)
				{
					displayItemKeyValue.Value.drawOrder = int.MaxValue;
				}
			}


			PerformanceTimerDisplayData timerData = timeDisplayData[timer.Name];

			if (timerData.ActiveCount == 0)
			{
				if (timerData.startTimeMs == 0)
				{
					timerData.startTimeMs = UiThread.CurrentTimerMs;
				}
				else // Add on to the time we have tracked so far. We have not show any time yet.
				{
					long timeSoFar = timerData.endTimeMs - timerData.startTimeMs;
					timerData.startTimeMs = UiThread.CurrentTimerMs - timeSoFar;
				}

				timerData.drawOrder = recursionCount;
			}
			timerData.ActiveCount++;
			timerData.TotalCount++;

			recursionCount++;
		}

		static int SortOnDrawOrder(PerformanceTimerDisplayData x, PerformanceTimerDisplayData y)
		{
			return x.drawOrder.CompareTo(y.drawOrder);
		}

#if DEBUG
		private void ParentWindow_KeyDown(object sender, KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Escape)
			{
				panels.Visible = !panels.Visible;
			}
		}

		private void ParentWindow_MouseDown(object sender, MouseEventArgs e)
		{
			if (e.NumPositions == 4)
			{
				panels.Visible = !panels.Visible;
			}
		}
#endif

		internal class PerformanceTimerDisplayData
		{
			internal int ActiveCount;
			internal int drawOrder;
			internal long endTimeMs;
			internal long startTimeMs;
			internal int TotalCount;
			internal TextWidget widget;
			private string name;

			public PerformanceTimerDisplayData(string name)
			{
				this.name = name;
			}

			internal long ElapsedMs
			{
				get
				{
					return endTimeMs - startTimeMs;
				}
			}

			public override string ToString()
			{
				return "{0}, {1}".FormatWith(name, TotalCount);
			}
		}

		private class PerformanceDisplayWidget : FlowLayoutWidget
		{
			public override GuiWidget AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
			{
				childToAdd.BoundsChanged += (sender, e) =>
				{
					GuiWidget child = sender as GuiWidget;
					if (child != null)
					{
						child.MinimumSize = new Vector2(Math.Max((double)child.MinimumSize.X, child.Width), 0);
					}
				};

				return base.AddChild(childToAdd, indexInChildrenList);
			}
		}
	}
}