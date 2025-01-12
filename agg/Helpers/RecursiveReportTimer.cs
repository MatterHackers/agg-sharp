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
    public class RecursiveReportTimer : IDisposable
    {
        private string name;
        private static readonly Stopwatch all_stopwatch = Stopwatch.StartNew();
        private double startTime;
        private static readonly Dictionary<string, TimerInfo> timers = new Dictionary<string, TimerInfo>();
        private static readonly Stack<string> activeTimers = new Stack<string>();
        private string parentName;

        private class TimerContext
        {
            public double Time { get; set; }
            public int CallCount { get; set; }
        }

        private class TimerInfo
        {
            public double TotalTime { get; set; }
            public int Depth { get; set; }
            public int TotalCallCount { get; set; }
            public HashSet<string> Children { get; } = new HashSet<string>();
            public Dictionary<string, TimerContext> TimePerParent { get; } = new Dictionary<string, TimerContext>();
        }

        public RecursiveReportTimer(string name)
        {
            lock (timers)
            {
                this.name = name;
                this.parentName = activeTimers.Count > 0 ? activeTimers.Peek() : null;
                if (!timers.ContainsKey(name))
                {
                    timers.Add(name, new TimerInfo
                    {
                        Depth = activeTimers.Count,
                        TotalCallCount = 1
                    });
                }
                else
                {
                    timers[name].TotalCallCount++;
                }
                if (parentName != null)
                {
                    timers[parentName].Children.Add(name);
                }
                activeTimers.Push(name);
            }
            startTime = all_stopwatch.Elapsed.TotalSeconds;
        }

        public void Dispose()
        {
            double elapsedTime = all_stopwatch.Elapsed.TotalSeconds - startTime;
            lock (timers)
            {
                if (timers.ContainsKey(name))
                {
                    TimerInfo timer = timers[name];
                    timer.TotalTime += elapsedTime;

                    if (parentName != null)
                    {
                        if (!timer.TimePerParent.ContainsKey(parentName))
                        {
                            timer.TimePerParent[parentName] = new TimerContext();
                        }
                        var context = timer.TimePerParent[parentName];
                        context.Time += elapsedTime;
                        context.CallCount++;
                    }
                }
                if (activeTimers.Count > 0 && activeTimers.Peek() == name)
                {
                    activeTimers.Pop();
                }
            }
        }

        private static double GetRootTime()
        {
            var rootTimer = timers.FirstOrDefault(t => t.Value.Depth == 0);
            return rootTimer.Value?.TotalTime ?? 0;
        }

        private static void PrintTimerRecursive(string timerName, TimerInfo timer, StringBuilder sb, string indent = "", string currentParent = null)
        {
            double rootTime = GetRootTime();
            string percentageStr = "";
            double timeToShow = timer.TotalTime;
            int callsToShow = timer.TotalCallCount;

            // For non-root timers, get the context-specific time and percentage
            if (currentParent != null && rootTime > 0)
            {
                if (timer.TimePerParent.TryGetValue(currentParent, out var context))
                {
                    timeToShow = context.Time;
                    callsToShow = context.CallCount;
                    double percentage = (timeToShow / rootTime) * 100;
                    percentageStr = $" ({percentage:0}%)";
                }
            }

            sb.AppendLine($"{indent}{timerName} ({callsToShow}): {timeToShow:0.000}s{percentageStr}");

            foreach (var childName in timer.Children.OrderBy(x => x))
            {
                if (timers.TryGetValue(childName, out var childTimer))
                {
                    PrintTimerRecursive(childName, childTimer, sb, indent + "  ", timerName);
                }
            }
        }

        public static void Report()
        {
            lock (timers)
            {
                var sb = new StringBuilder();
                var rootTimers = timers.Where(kvp => kvp.Value.Depth == 0)
                                     .OrderBy(kvp => kvp.Key);
                foreach (var kvp in rootTimers)
                {
                    PrintTimerRecursive(kvp.Key, kvp.Value, sb);
                }
                Debug.WriteLine(sb.ToString());
            }
        }

        public static void ReportAndRestart()
        {
            Report();
            Restart();
        }

        public static void ReportAndRestart(Graphics2D drawTo, double x, double y)
        {
            Dictionary<string, TimerInfo> timersCopy;
            lock (timers)
            {
                timersCopy = new Dictionary<string, TimerInfo>(timers);
            }
            var sb = new StringBuilder();
            var rootTimers = timersCopy.Where(kvp => kvp.Value.Depth == 0)
                                     .OrderBy(kvp => kvp.Key);
            foreach (var kvp in rootTimers)
            {
                PrintTimerRecursive(kvp.Key, kvp.Value, sb);
            }
            string[] lines = sb.ToString().TrimEnd().Split('\n');
            foreach (var line in lines)
            {
                drawTo.DrawString(line, x, y, backgroundColor: Color.White.WithAlpha(210), drawFromHintedCach: true);
                y -= 18;
            }
            Restart();
        }

        public static void Restart()
        {
            lock (timers)
            {
                timers.Clear();
                activeTimers.Clear();
            }
        }
    }
}