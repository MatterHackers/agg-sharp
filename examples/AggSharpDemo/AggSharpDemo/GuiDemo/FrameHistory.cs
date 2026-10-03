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

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>One frame in a <see cref="FrameHistory"/>: its draw time and what the garbage collector did around it.</summary>
	public readonly struct FrameSample
	{
		public FrameSample(double ms, int gen0SinceLastFrame, bool gcDuringDraw, long allocatedBytes)
		{
			this.Ms = ms;
			this.Gen0SinceLastFrame = gen0SinceLastFrame;
			this.GcDuringDraw = gcDuringDraw;
			this.AllocatedBytes = allocatedBytes;
		}

		/// <summary>How long the draw took.</summary>
		public double Ms { get; }

		/// <summary>Gen0 collections between the previous frame's draw starting and this one's ending - so a collection
		/// in event handling or layout between draws counts too. Gen1 and gen2 collections count here as well, since
		/// the runtime bumps the gen0 count for every collection that includes gen0.</summary>
		public int Gen0SinceLastFrame { get; }

		/// <summary>Whether a collection ran inside this frame's draw itself.</summary>
		public bool GcDuringDraw { get; }

		/// <summary>Bytes allocated on the drawing thread over the same interval as <see cref="Gen0SinceLastFrame"/>
		/// (on wasm, single threaded, that is the whole app).</summary>
		public long AllocatedBytes { get; }

		/// <summary>Whether any collection fell in this frame's interval.</summary>
		public bool HadGc => this.Gen0SinceLastFrame > 0 || this.GcDuringDraw;
	}

	/// <summary>
	/// The last <see cref="Capacity"/> frame times in milliseconds, oldest first - agg-gui's FrameHistory
	/// (widgets/performance.rs), which feeds the backend panel's sparkline and "Mean CPU usage" readout - plus,
	/// per frame, whether the garbage collector ran and how much was allocated, to tell GC pauses from other spikes.
	/// </summary>
	/// <remarks>
	/// Not <see cref="MatterHackers.Agg.UI.AverageMillisecondTimer"/>: that one keeps whole milliseconds and
	/// always divides by its full window, so it reads low until it fills, and it cannot hand out its samples.
	/// Pushing and reading the statistics allocate nothing, so measuring does not cause the collections it counts.
	/// </remarks>
	public class FrameHistory
	{
		/// <summary>About a second at 60 fps: long enough that one slow frame does not own the mean, short
		/// enough that a hitch still shows on the graph.</summary>
		public const int Capacity = 60;

		/// <summary>A frame is slow when it takes more than this many times the window's median: a spike against
		/// whatever the page normally costs, rather than against 16.7 ms, which a heavy page (or the browser) may
		/// miss on every frame.</summary>
		public const double SlowFactor = 2;

		private readonly FrameSample[] frames = new FrameSample[Capacity];

		// Reused by MedianMs so finding slow frames does not allocate per frame.
		private readonly double[] sortScratch = new double[Capacity];

		private int head;

		/// <summary>How many samples are held (at most <see cref="Capacity"/>).</summary>
		public int Count { get; private set; }

		/// <summary>The mean of the held samples; 0 when there are none.</summary>
		public double MeanMs
		{
			get
			{
				if (this.Count == 0)
				{
					return 0;
				}

				double total = 0;
				for (int i = 0; i < this.Count; i++)
				{
					total += this[i].Ms;
				}

				return total / this.Count;
			}
		}

		/// <summary>1000 / <see cref="MeanMs"/>, or 0 while there is nothing (or nothing measurable) to go on.</summary>
		public double Fps => this.MeanMs < 0.001 ? 0 : 1000 / this.MeanMs;

		/// <summary>The mean of <see cref="FrameSample.AllocatedBytes"/> over the held frames; 0 when there are none.</summary>
		public double MeanAllocatedBytes
		{
			get
			{
				if (this.Count == 0)
				{
					return 0;
				}

				double total = 0;
				for (int i = 0; i < this.Count; i++)
				{
					total += this[i].AllocatedBytes;
				}

				return total / this.Count;
			}
		}

		/// <summary>The median frame time of the held samples; 0 when there are none.</summary>
		public double MedianMs
		{
			get
			{
				int count = this.Count;
				if (count == 0)
				{
					return 0;
				}

				for (int i = 0; i < count; i++)
				{
					this.sortScratch[i] = this[i].Ms;
				}

				Array.Sort(this.sortScratch, 0, count);
				return count % 2 == 1 ? this.sortScratch[count / 2] : (this.sortScratch[count / 2 - 1] + this.sortScratch[count / 2]) / 2;
			}
		}

		/// <summary>The held samples from oldest to newest, the order the sparkline draws them.</summary>
		public IEnumerable<double> Samples
		{
			get
			{
				for (int i = 0; i < this.Count; i++)
				{
					yield return this[i].Ms;
				}
			}
		}

		/// <summary>The held frame <paramref name="index"/> places from the oldest (0) to the newest (Count - 1).</summary>
		public FrameSample this[int index]
		{
			get
			{
				if ((uint)index >= (uint)this.Count)
				{
					throw new ArgumentOutOfRangeException(nameof(index));
				}

				return this.frames[(this.head + Capacity - this.Count + index) % Capacity];
			}
		}

		/// <summary>Counts the held frames slower than <see cref="SlowFactor"/> times the median, and how many of
		/// those had a collection in their interval - "were the spikes GCs?" in two numbers.</summary>
		public void CountSlowFrames(out int slowFrames, out int slowFramesWithGc)
		{
			slowFrames = 0;
			slowFramesWithGc = 0;
			double threshold = this.MedianMs * SlowFactor;
			for (int i = 0; i < this.Count; i++)
			{
				FrameSample frame = this[i];
				if (frame.Ms > threshold)
				{
					slowFrames++;
					if (frame.HadGc)
					{
						slowFramesWithGc++;
					}
				}
			}
		}

		/// <summary>Appends a frame time with no GC information, dropping the oldest once full.</summary>
		public void Push(double frameMs) => this.Push(new FrameSample(frameMs, 0, false, 0));

		/// <summary>Appends a frame, dropping the oldest once full.</summary>
		public void Push(FrameSample frame)
		{
			this.frames[this.head] = frame;
			this.head = (this.head + 1) % Capacity;
			if (this.Count < Capacity)
			{
				this.Count++;
			}
		}
	}
}
