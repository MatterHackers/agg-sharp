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

namespace MatterHackers.Agg.UI
{
    public class PerformancePannel : FlowLayoutWidget
    {
		internal class PerformanceTimerDisplayData
		{
			internal int drawOrder;
			internal TextWidget widget;
			internal int ActiveCount;
			internal int TotalCount;
			internal Stopwatch timer;
		}

		private static PerformanceGroup pannels = null;
        private static Dictionary<string, PerformancePannel> resultsPannels = new Dictionary<string, PerformancePannel>();

        private FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
        private int recursionCount = 0;
        private Dictionary<string, PerformanceTimerDisplayData> timeDisplayData = new Dictionary<string, PerformanceTimerDisplayData>();

		internal void Start(PerformanceTimer timer)
		{
			if (!timeDisplayData.ContainsKey(timer.Name))
			{
				PerformanceTimerDisplayData newTimerData = new PerformanceTimerDisplayData()
				{
					widget = new TextWidget("waiting")
					{
						AutoExpandBoundsToText = true,
						TextColor = new RGBA_Bytes(120, 20, 20),
						HAnchor = HAnchor.ParentLeft,
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
				timerData.timer = Stopwatch.StartNew();
				timerData.drawOrder = recursionCount;
			}
			timerData.ActiveCount++;
			timerData.TotalCount++;

			recursionCount++;
		}

		public PerformancePannel(string name)
            : base(FlowDirection.TopToBottom)
        {
            this.Name = name;
            Margin = new BorderDouble(5);
            Padding = new BorderDouble(3);
            VAnchor |= VAnchor.ParentTop;

            if (pannels == null)
            {
                pannels = new PerformanceGroup();
                pannels.Selectable = false;
                pannels.HAnchor |= HAnchor.ParentLeft;
                pannels.VAnchor |= VAnchor.ParentTop;
                pannels.Visible = false; // start out not visible
                UiThread.RunOnIdle(() =>
                {
					if (PerformanceTimer.GetParentWindowFunction != null)
					{
						GuiWidget parentWindow = PerformanceTimer.GetParentWindowFunction();
						parentWindow.AddChild(pannels);
						parentWindow.KeyDown += ParentWindow_KeyDown;
					}
                });
            }

            // add in the column title
            {
                TextWidget titleWidget = new TextWidget(name, pointSize: 14)
                {
                    BackgroundColor = new RGBA_Bytes(),
                    TextColor = new RGBA_Bytes(20, 120, 20),
                };
                titleWidget.Printer.DrawFromHintedCache = true;
                AddChild(titleWidget);
            }

            AddChild(topToBottom);

            pannels.AddChild(this);

            BackgroundColor = new RGBA_Bytes(RGBA_Bytes.White, 180);
        }

        private event EventHandler unregisterEvents;

        public static PerformancePannel GetNamedPannel(string pannelName)
        {
			if (!resultsPannels.ContainsKey(pannelName))
			{
				PerformancePannel timingPannelToReportTo = new PerformancePannel(pannelName);
				resultsPannels.Add(pannelName, timingPannelToReportTo);
			}

            return resultsPannels[pannelName];
        }

        public override void OnClosed(EventArgs e)
        {
            if (unregisterEvents != null)
            {
                unregisterEvents(this, null);
            }
            base.OnClosed(e);
        }

		static int SortOnDrawOrder(PerformanceTimerDisplayData x, PerformanceTimerDisplayData y)
		{
			return x.drawOrder.CompareTo(y.drawOrder);
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

			foreach (PerformanceTimerDisplayData record in allRecords)
			{
				int curIndex = topToBottom.Children.IndexOf(record.widget);
				if (curIndex != -1)
				{
					if (record.drawOrder < int.MaxValue
					&& curIndex != record.drawOrder)
					{
						topToBottom.Children.RemoveAt(curIndex);
						topToBottom.Children.Insert(record.drawOrder, record.widget);
					}
				}
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
				timerData.timer.Stop();

				string outputText = "{0:0.00} ms - {1}".FormatWith(timerData.timer.Elapsed.TotalSeconds * 1000, timer.Name);
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
				// Doing it here causes an invalidate and endlelss drawing.
				timerData.widget.Text = outputText;

				timerData.TotalCount = 0;
			}
		}

        private void ParentWindow_KeyDown(object sender, KeyEventArgs keyEvent)
        {
            if (keyEvent.KeyCode == Keys.Escape)
            {
                pannels.Visible = !pannels.Visible;
            }
        }

        private class PerformanceGroup : FlowLayoutWidget
        {
            public override void AddChild(GuiWidget childToAdd, int indexInChildrenList = -1)
            {
                childToAdd.BoundsChanged += (sender, e) =>
                {
                    GuiWidget child = sender as GuiWidget;
                    if (child != null)
                    {
                        child.MinimumSize = new VectorMath.Vector2(Math.Max(child.MinimumSize.x, child.Width), 0);
                    }
                };

                base.AddChild(childToAdd, indexInChildrenList);
            }
        }
    }
}