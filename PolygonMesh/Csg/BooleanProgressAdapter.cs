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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Adapts the kernel's per-phase progress callback to the
	/// <c>Action&lt;double, string&gt;</c> reporter the rest of the pipeline uses,
	/// mapping every callback into the slice of the 0..1 bar this boolean owns.
	/// </summary>
	/// <remarks>
	/// <para>
	/// The kernel reports <c>(phase, fraction)</c> where the fraction is the
	/// progress of that phase alone, so the raw values fall back to zero at every
	/// phase change and there is no way to know in advance how many phases a given
	/// engine will run. Rather than invent a phase total, this keeps a high-water
	/// mark: the ratio handed to the reporter never decreases, so a new phase simply
	/// holds the bar until its fraction overtakes what the previous phase reached.
	/// The phase name in the message is what actually tells the user where they are.
	/// </para>
	/// <para>
	/// The kernel's callback may arrive on a native worker thread and is never
	/// re-entered concurrently, but it is not guaranteed to be the same thread twice,
	/// so the high-water mark is guarded. The reporter is invoked under that same
	/// lock to keep the ratios the caller sees in the order they were computed.
	/// </para>
	/// <para>
	/// This adapter stays synchronous forever, and must never call
	/// <see cref="MatterHackers.Agg.ProgressReporter.YieldToUi"/>. <see cref="Report"/> is called from inside a
	/// native frame: the kernel is part way through a boolean, holding its own state, and there is
	/// no way to suspend that frame and resume it later - which is what awaiting means. On
	/// mono-wasm an async callback would also have to return to the native caller before its
	/// continuation ran, so the "yield" would hand the frame back to a boolean that had already
	/// gone on without it. And the call can arrive on a native worker thread, which on any host is
	/// not the thread that paints. A boolean's yields therefore live in the managed loop around the
	/// kernel - see <see cref="ManifoldKernel"/>'s async pairwise fold - between native calls,
	/// never inside one.
	/// </para>
	/// </remarks>
	internal sealed class BooleanProgressAdapter : IProgress<(string Phase, double? Fraction)>
	{
		private readonly Action<double, string> reporter;
		private readonly double windowStart;
		private readonly double windowSize;
		private readonly int operationCount;
		private readonly object sync = new object();

		private int completedOperations;
		private double highWaterRatio;

		/// <param name="reporter">The pipeline's reporter; ratio first, message second.</param>
		/// <param name="ratioCompleted">Where this boolean's slice of the bar starts.</param>
		/// <param name="amountPerOperation">How much of the bar the whole boolean owns.</param>
		/// <param name="operationCount">
		/// How many pairwise booleans the n-ary combine will run. The slice is split
		/// evenly between them, which is the only estimate available before the
		/// intermediate results exist.
		/// </param>
		internal BooleanProgressAdapter(
			Action<double, string> reporter,
			double ratioCompleted,
			double amountPerOperation,
			int operationCount)
		{
			this.reporter = reporter ?? throw new ArgumentNullException(nameof(reporter));
			this.windowStart = ratioCompleted;
			this.windowSize = amountPerOperation;
			this.operationCount = Math.Max(1, operationCount);
			this.highWaterRatio = ratioCompleted;
		}

		public void Report((string Phase, double? Fraction) value)
		{
			lock (this.sync)
			{
				double stepSize = this.windowSize / this.operationCount;
				double stepStart = this.windowStart + (stepSize * this.completedOperations);

				// A null fraction is the ABI's indeterminate phase - it says nothing about
				// how far along the phase is, so only the message changes.
				double candidate = value.Fraction.HasValue
					? stepStart + (stepSize * Math.Clamp(value.Fraction.Value, 0, 1))
					: this.highWaterRatio;

				this.Publish(candidate, value.Phase);
			}
		}

		/// <summary>
		/// Closes out one pairwise boolean, moving the bar to that step's boundary so
		/// the next step starts from where this one ended.
		/// </summary>
		internal void CompleteOperation(string phase)
		{
			lock (this.sync)
			{
				if (this.completedOperations < this.operationCount)
				{
					this.completedOperations++;
				}

				this.Publish(this.windowStart + (this.windowSize / this.operationCount * this.completedOperations), phase);
			}
		}

		private void Publish(double candidate, string phase)
		{
			if (candidate > this.highWaterRatio)
			{
				this.highWaterRatio = candidate;
			}

			var message = string.IsNullOrEmpty(phase) ? "Boolean" : $"Boolean: {phase}";

			try
			{
				this.reporter(this.highWaterRatio, message);
			}
			catch
			{
				// This can be running inside a native frame, where an escaping exception
				// would be captured and rethrown at the caller, killing a boolean that
				// otherwise succeeded. A progress sink that fails is not a reason to lose
				// the geometry.
			}
		}
	}
}
