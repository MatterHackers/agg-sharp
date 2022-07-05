/*
Copyright (c) 2016, Lars Brubaker, Kevin Pope
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

namespace MatterHackers.Agg
{
	/// <summary>
	/// A tiny class to allow for the quick timing of a block of code in the debugger.
	/// </summary>
	/// /// <example> 
	/// This sample shows how to use QuickTimer.
	/// <code>
	/// class SampleProgram
	/// {
	///     static int Main() 
	///     {
	///			// some code we want to time
	///			using(new QuickTimer("Time To Get Cookies")
	///			{
	///				GetCookies();
	///			}
	///			
	///         return 1;
	///     }
	/// }
	/// </code>
	/// </example>
	public class QuickTimer : IDisposable
	{
        private double minTimeToReport;
        private string name;
		private Stopwatch quickTimerTime = Stopwatch.StartNew();
		private double startTime;

		public QuickTimer(string name, double minTimeToReport = 0)
		{
			this.minTimeToReport = minTimeToReport;
			this.name = name;
			startTime = quickTimerTime.Elapsed.TotalMilliseconds;
		}

		public void Dispose()
		{
			double totalTime = (quickTimerTime.Elapsed.TotalMilliseconds - startTime) / 1000.0;
			if (totalTime > minTimeToReport)
			{
				Debug.WriteLine(name + ": {0:0.0}s".FormatWith(totalTime));
			}
		}
	}

	public class QuickTimer2Report : IDisposable
	{
		private string name;
		private Stopwatch quickTimerTime = Stopwatch.StartNew();
		private double startTime;

		private static Dictionary<string, double> timers = new Dictionary<string, double>();

		public QuickTimer2Report(string name)
		{
			this.name = name;
			if (!timers.ContainsKey(name))
			{
				timers.Add(name, 0);
			}

			startTime = quickTimerTime.Elapsed.TotalMilliseconds;
		}

		public void Dispose()
		{
			double totalTime = quickTimerTime.Elapsed.TotalMilliseconds - startTime;
			timers[name] = timers[name] + totalTime;
		}

		public static void Report()
		{
			foreach (var kvp in timers)
			{
				Debug.WriteLine(kvp.Key + ": {0:0.0}s".FormatWith(kvp.Value / 1000.0));
			}
		}
	}
}