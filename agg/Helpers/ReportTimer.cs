/*
Copyright (c) 2025, Lars Brubaker, Kevin Pope
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
using System.Linq;
using System.Text;

namespace MatterHackers.Agg
{
    public class ReportTimer : IDisposable
    {
        private string name;
        private int group;
        private static readonly Stopwatch all_stopwatch = Stopwatch.StartNew();
        private double startTime;

        private class TimerInfo
        {
            public double TotalTime { get; set; }
            public int CallCount { get; set; }
        }

        private static readonly Dictionary<(int group, string name), TimerInfo> timers = new Dictionary<(int group, string name), TimerInfo>();

        public ReportTimer(string name, int group = 0)
        {
            this.name = name;
            this.group = group;

            var key = (group, name);
            lock (timers)
            {
                if (!timers.ContainsKey(key))
                {
                    timers.Add(key, new TimerInfo { CallCount = 1 });
                }
                else
                {
                    timers[key].CallCount++;
                }
            }
            startTime = all_stopwatch.Elapsed.TotalSeconds;
        }

        public void Dispose()
        {
            double elapsedTime = all_stopwatch.Elapsed.TotalSeconds - startTime;
            var key = (group, name);
            lock (timers)
            {
                if (timers.ContainsKey(key))
                {
                    timers[key].TotalTime += elapsedTime;
                }
            }
        }

        public static void Report(int group = 0)
        {
            Dictionary<(int group, string name), TimerInfo> timersCopy;
            lock (timers)
            {
                timersCopy = new Dictionary<(int group, string name), TimerInfo>(timers);
            }

            var sb = new StringBuilder();

            if (group != 0)
            {
                var groupTimers = timersCopy.Where(t => t.Key.group == group);
                ReportGroup(sb, groupTimers, group);
            }
            else
            {
                // Group the timers by their group number
                var groupedTimers = timersCopy.GroupBy(t => t.Key.group);
                foreach (var grouping in groupedTimers.OrderBy(g => g.Key))
                {
                    ReportGroup(sb, grouping, grouping.Key);
                }
            }

            Debug.WriteLine(sb.ToString());
        }

        private static void ReportGroup(StringBuilder sb, IEnumerable<KeyValuePair<(int group, string name), TimerInfo>> groupTimers, int groupNum)
        {
            sb.AppendLine($"Function Timing Report - Group {groupNum}:");
            sb.AppendLine("----------------------");

            double totalTime = groupTimers.Sum(t => t.Value.TotalTime);

            foreach (var timer in groupTimers.OrderByDescending(t => t.Value.TotalTime))
            {
                double percentage = (timer.Value.TotalTime / totalTime) * 100;
                sb.AppendLine(
                    $"{timer.Key.name} ({timer.Value.CallCount} calls): " +
                    $"{timer.Value.TotalTime:0.000}s ({percentage:0.0}%) " +
                    $"[{timer.Value.TotalTime / timer.Value.CallCount:0.000}s avg per call]"
                );
            }
            sb.AppendLine();
        }

        public static void ReportAndRestart(int group = 0)
        {
            Report(group);
            Restart(group);
        }

        public static void ReportAndRestart(Graphics2D drawTo, double x, double y, int group = 0)
        {
            Dictionary<(int group, string name), TimerInfo> timersCopy;
            lock (timers)
            {
                timersCopy = new Dictionary<(int group, string name), TimerInfo>(timers);
            }

            var sb = new StringBuilder();

            if (group != 0)
            {
                var groupTimers = timersCopy.Where(t => t.Key.group == group);
                ReportGroup(sb, groupTimers, group);
            }
            else
            {
                var groupedTimers = timersCopy.GroupBy(t => t.Key.group);
                foreach (var grouping in groupedTimers.OrderBy(g => g.Key))
                {
                    ReportGroup(sb, grouping, grouping.Key);
                }
            }

            string[] lines = sb.ToString().TrimEnd().Split('\n');
            foreach (var line in lines)
            {
                drawTo.DrawString(line, x, y, backgroundColor: Color.White.WithAlpha(210), drawFromHintedCach: true);
                y -= 18;
            }

            Restart(group);
        }

        public static void Restart(int group = 0)
        {
            lock (timers)
            {
                if (group != 0)
                {
                    // Remove only timers for the specified group
                    var keysToRemove = timers.Keys.Where(k => k.group == group).ToList();
                    foreach (var key in keysToRemove)
                    {
                        timers.Remove(key);
                    }
                }
                else
                {
                    timers.Clear();
                }
            }
        }
    }
}