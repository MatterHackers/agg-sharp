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
using System.Diagnostics;

namespace MatterHackers.Agg.UI
{
	public class PerformanceTimer : IDisposable
	{
		public static Func<GuiWidget> GetParentWindowFunction = null;
		private static string lastPanelName = "";

		public string Name { get; }

		private PerformancePanel timingPanelToReportTo;

		static internal bool InPerformanceMeasuring = false;

		static object locker = new object();

		public PerformanceTimer(string panelName, string name)
		{
			lock (locker)
			{
				if (!InPerformanceMeasuring)
				{
					InPerformanceMeasuring = true;
					if (panelName == "_LAST_")
					{
						panelName = lastPanelName;
					}

					this.timingPanelToReportTo = PerformancePanel.GetNamedPanel(panelName);
					this.Name = name;
					this.timingPanelToReportTo.Start(this);

					lastPanelName = panelName;
					InPerformanceMeasuring = false;
				}
			}
		}

		public void Dispose()
		{
			lock (locker)
			{
				// Check that we actually created a time (we don't when we find things that are happening while timing.
				if (!InPerformanceMeasuring)
				{
					InPerformanceMeasuring = true;
					timingPanelToReportTo.Stop(this);
					InPerformanceMeasuring = false;
				}
			}
		}
	}

	public class QuickTimer : IDisposable
	{
		Stopwatch timer;
		string name;

		public QuickTimer(string name)
		{
			this.name = name;
			timer = Stopwatch.StartNew();
		}

		public void Dispose()
		{
			Debug.WriteLine("{0}: {1:0.0}ms", name, timer.ElapsedMilliseconds);
		}
	}
}