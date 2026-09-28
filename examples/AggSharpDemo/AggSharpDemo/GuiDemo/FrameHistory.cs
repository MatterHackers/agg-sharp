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

using System.Collections.Generic;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The last <see cref="Capacity"/> frame times in milliseconds, oldest first - agg-gui's FrameHistory
	/// (widgets/performance.rs), which feeds the backend panel's sparkline and "Mean CPU usage" readout.
	/// </summary>
	/// <remarks>
	/// Not <see cref="MatterHackers.Agg.UI.AverageMillisecondTimer"/>: that one keeps whole milliseconds and
	/// always divides by its full window, so it reads low until it fills, and it cannot hand out its samples.
	/// </remarks>
	public class FrameHistory
	{
		/// <summary>About a second at 60 fps: long enough that one slow frame does not own the mean, short
		/// enough that a hitch still shows on the graph.</summary>
		public const int Capacity = 60;

		private readonly double[] times = new double[Capacity];

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
				foreach (double ms in this.Samples)
				{
					total += ms;
				}

				return total / this.Count;
			}
		}

		/// <summary>1000 / <see cref="MeanMs"/>, or 0 while there is nothing (or nothing measurable) to go on.</summary>
		public double Fps => this.MeanMs < 0.001 ? 0 : 1000 / this.MeanMs;

		/// <summary>The held samples from oldest to newest, the order the sparkline draws them.</summary>
		public IEnumerable<double> Samples
		{
			get
			{
				for (int i = 0; i < this.Count; i++)
				{
					yield return this.times[(this.head + Capacity - this.Count + i) % Capacity];
				}
			}
		}

		/// <summary>Appends a frame time, dropping the oldest once full.</summary>
		public void Push(double frameMs)
		{
			this.times[this.head] = frameMs;
			this.head = (this.head + 1) % Capacity;
			if (this.Count < Capacity)
			{
				this.Count++;
			}
		}
	}
}
