using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace MatterHackers.Agg
{
	public static class TraceTiming
	{
		private static Dictionary<string, Stopwatch> timers = new Dictionary<string, Stopwatch>();

		[Conditional("TRACE")]
		public static void StartTracking(string section)
		{
			Trace.WriteLine("TimerStarted: " + section);
			timers.Add(section, Stopwatch.StartNew());
		}

		[Conditional("TRACE")]
		public static void ReportAndRestart(string section, string task)
		{
			Stopwatch timer;
			if (timers.TryGetValue(section, out timer))
			{
				Trace.WriteLine($"TimerElapsed: {section}.MsTo{task}:{timer.ElapsedMilliseconds}");
				timer.Restart();
			}
		}

		[Conditional("TRACE")]
		public static void Restart(string section)
		{
			Stopwatch timer;
			if (timers.TryGetValue(section, out timer))
			{
				timer.Restart();
			}
		}

		[Conditional("TRACE")]
		public static void Report(string section, string task)
		{
			Stopwatch timer;
			if (timers.TryGetValue(section, out timer))
			{
				Trace.WriteLine($"TimerElapsed: {section}.MsTo{task}:{timer.ElapsedMilliseconds}");
			}
		}

		[Conditional("TRACE")]
		public static void ReportAndStop(string section, string task)
		{
			Stopwatch timer;
			if (timers.TryGetValue(section, out timer))
			{
				Trace.WriteLine($"TimerStopped: {section}.MsTo{task}:{timer.ElapsedMilliseconds}");
				timers.Remove(section);
				timer.Stop();
			}
		}
	}
}
