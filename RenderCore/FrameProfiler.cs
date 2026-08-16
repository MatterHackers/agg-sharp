/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MatterHackers.RenderCore
{
	/// <summary>
	/// A frame-time breakdown that costs nothing when it is off. Sections accumulate elapsed time and
	/// counters accumulate events, both per frame; every <c>AGG_FRAME_PROFILE_EVERY</c> frames the
	/// averages are written to the console.
	/// <para>
	/// Turned on with <c>AGG_FRAME_PROFILE=1</c>. When off, <see cref="IsEnabled"/> is a static readonly
	/// false that the JIT folds away, so the call sites scattered through the render path are free.
	/// </para>
	/// <para>
	/// Deliberately single threaded and lock free: it is meant for the UI thread's paint. A thumbnail
	/// worker that wandered in would corrupt nothing worse than the numbers.
	/// </para>
	/// </summary>
	public static class FrameProfiler
	{
		/// <summary>True when <c>AGG_FRAME_PROFILE</c> asked for the breakdown.</summary>
		public static readonly bool IsEnabled = ReadEnabled();

		private static readonly int ReportEveryFrames = ReadReportInterval();

		// Everything below is behind this lock. The paint thread is not the only writer: thumbnail
		// workers create GPU resources on their own contexts, and attributing those is half the point of
		// the counters - but unguarded dictionary writes from two threads corrupt the table, which would
		// take the render path down with it rather than merely spoiling a number.
		private static readonly object gate = new object();

		// Insertion ordered so the report reads in the order the frame actually happens.
		private static readonly List<string> sectionOrder = new List<string>();
		private static readonly Dictionary<string, long> sectionTicks = new Dictionary<string, long>(StringComparer.Ordinal);

		private static readonly List<string> counterOrder = new List<string>();
		private static readonly Dictionary<string, long> counterTotals = new Dictionary<string, long>(StringComparer.Ordinal);

		private static readonly Stopwatch frameWatch = new Stopwatch();
		private static long frameTicks;
		private static int framesInWindow;

		/// <summary>How many frames have been profiled since the process started.</summary>
		public static int FrameCount { get; private set; }

		/// <summary>
		/// Starts a frame. Everything measured until <see cref="EndFrame"/> belongs to it.
		/// </summary>
		public static void BeginFrame()
		{
			if (!IsEnabled)
			{
				return;
			}

			frameWatch.Restart();
		}

		/// <summary>
		/// Ends a frame, folds it into the rolling window, and reports when the window is full.
		/// </summary>
		public static void EndFrame()
		{
			if (!IsEnabled || !frameWatch.IsRunning)
			{
				return;
			}

			frameWatch.Stop();
			frameTicks += frameWatch.ElapsedTicks;
			framesInWindow++;
			FrameCount++;

			if (framesInWindow >= ReportEveryFrames)
			{
				Report();
			}
		}

		/// <summary>
		/// Times a section. Use with <c>using</c>; nesting is allowed and each name accumulates on its own,
		/// so nested sections double count against their parent on purpose.
		/// </summary>
		/// <param name="name">The section's name in the report.</param>
		public static Section Time(string name) => new Section(name);

		/// <summary>Adds to a per-frame counter.</summary>
		/// <param name="name">The counter's name in the report.</param>
		/// <param name="amount">How much to add; defaults to one event.</param>
		public static void Count(string name, long amount = 1)
		{
			if (!IsEnabled)
			{
				return;
			}

			lock (gate)
			{
				if (!counterTotals.TryGetValue(name, out var total))
				{
					counterOrder.Add(name);
				}

				counterTotals[name] = total + amount;
			}
		}

		/// <summary>
		/// Records the first stack that reached a place the profile is suspicious of, once per process.
		/// Used to answer "who touched this?" - the answer is a stack, not a count.
		/// </summary>
		/// <param name="name">A label for the site.</param>
		public static void FirstTouch(string name)
		{
			if (!IsEnabled || firstTouches.Contains(name))
			{
				return;
			}

			firstTouches.Add(name);
			Console.WriteLine($"[frame] FIRST TOUCH {name}\n{new StackTrace(1, true)}");
		}

		private static readonly HashSet<string> firstTouches = new HashSet<string>(StringComparer.Ordinal);

		/// <summary>Writes the averages for the frames gathered so far and starts a new window.</summary>
		public static void Report()
		{
			if (!IsEnabled || framesInWindow == 0)
			{
				return;
			}

			double frames = framesInWindow;
			double toMs = 1000.0 / Stopwatch.Frequency;

			lock (gate)
			{
				var text = new StringBuilder();
				text.AppendLine(
					$"[frame] {framesInWindow} frames, avg {frameTicks * toMs / frames:0.00} ms  (total frames {FrameCount})");

				foreach (var name in sectionOrder)
				{
					text.AppendLine($"[frame]   {name,-28} {sectionTicks[name] * toMs / frames,8:0.00} ms");
					sectionTicks[name] = 0;
				}

				foreach (var name in counterOrder)
				{
					text.AppendLine($"[frame]   #{name,-27} {counterTotals[name] / frames,8:0.0} /frame");
					counterTotals[name] = 0;
				}

				Console.Write(text.ToString());
			}

			frameTicks = 0;
			framesInWindow = 0;
		}

		private static void AddSectionTicks(string name, long ticks)
		{
			lock (gate)
			{
				if (!sectionTicks.TryGetValue(name, out var total))
				{
					sectionOrder.Add(name);
				}

				sectionTicks[name] = total + ticks;
			}
		}

		private static bool ReadEnabled()
		{
			var value = Environment.GetEnvironmentVariable("AGG_FRAME_PROFILE");
			return !string.IsNullOrEmpty(value) && value != "0";
		}

		private static int ReadReportInterval()
		{
			var value = Environment.GetEnvironmentVariable("AGG_FRAME_PROFILE_EVERY");
			return int.TryParse(value, out var frames) && frames > 0 ? frames : 60;
		}

		/// <summary>
		/// The scope returned by <see cref="Time"/>. A struct with no allocation, and a no-op when the
		/// profiler is off.
		/// </summary>
		public readonly struct Section : IDisposable
		{
			private readonly string name;
			private readonly long startTicks;

			internal Section(string name)
			{
				this.name = IsEnabled ? name : null;
				this.startTicks = IsEnabled ? Stopwatch.GetTimestamp() : 0;
			}

			/// <summary>Stops timing and folds the elapsed time into this section's total.</summary>
			public void Dispose()
			{
				if (this.name != null)
				{
					AddSectionTicks(this.name, Stopwatch.GetTimestamp() - this.startTicks);
				}
			}
		}
	}
}
