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
using System.Threading.Tasks;

namespace MatterHackers.Agg
{
	/// <summary>
	/// The progress channel a long running job reports through: a synchronous <see cref="Report"/> that is
	/// safe from anywhere, plus an awaitable <see cref="YieldToUi"/> that only costs something on a host
	/// where the job and the UI share one thread.
	/// </summary>
	/// <remarks>
	/// <para>The pipeline has always passed progress as a bare <c>Action&lt;double, string&gt;</c> (ratio,
	/// message). That works on desktop, where the job runs on the thread pool and the UI thread paints the
	/// bar on its own. In the browser there is one thread for both, so a job holds the frame for its whole
	/// duration and the bar never moves. Fixing that means a job must be able to hand the thread back from
	/// where it reports - i.e. the reporter must be awaitable.</para>
	/// <para>Making the delegate itself async was rejected: it forces async into geometry inner loops and
	/// <c>Parallel</c> bodies, and it cannot work at the native CSG callback, which physically cannot await.
	/// So reporting stays synchronous and yielding is a separate, opt-in call. Deep sync code and native
	/// callbacks keep calling <see cref="Report"/>; only a job's top level async flow calls
	/// <see cref="YieldToUi"/>.</para>
	/// <para>The implicit conversions to and from <c>Action&lt;double, string&gt;</c> are what keep this
	/// from being a ~200 site rewrite: every site that merely forwards a reporter onward compiles unchanged,
	/// and only direct invocations have to become <see cref="Report"/> calls - which the compiler finds for
	/// you.</para>
	/// </remarks>
	public class ProgressReporter
	{
		/// <summary>
		/// The shortest gap between two real UI yields.
		/// </summary>
		/// <remarks>
		/// A yield is not free: on the browser it is a two idle turn hop back to the event loop, so it costs
		/// on the order of one to two frames. Jobs report far more often than that - some per triangle - and
		/// yielding on every report would spend more of the job hopping the loop than working. At 50 ms the
		/// bar still updates 20 times a second, well past the ~10 per second where motion already reads as
		/// smooth, while the hop overhead stays a few percent of a job of any interesting length.
		/// </remarks>
		public const long YieldThrottleMs = 50;

		/// <summary>
		/// Installed by the host to make <see cref="YieldToUi"/> actually yield - MatterCAD sets it to
		/// <c>UiThread.YieldToFrame</c> at boot, and only in the browser. Left null everywhere else, which
		/// is what makes yielding free on desktop.
		/// </summary>
		/// <remarks>
		/// agg cannot see <c>UiThread</c> (it lives in Gui, which agg does not reference), so the pump is
		/// injected from above rather than called directly. This is process global mutable state; a test
		/// that installs a hook must restore it in a finally block and must not run in parallel with
		/// another that does.
		/// </remarks>
		public static Func<Task> UiYield { get; set; }

		/// <summary>
		/// A reporter that swallows every report and never yields - the stand in for today's <c>null</c>
		/// reporter paths, so callers do not have to null check.
		/// </summary>
		public static readonly ProgressReporter Null = new ProgressReporter(null);

		private readonly Action<double, string> target;

		/// <summary>
		/// The <see cref="Report"/> method group, made once. Cached so converting the same reporter to an
		/// <c>Action</c> twice hands back the same delegate instance, which lets the reverse conversion
		/// recognize its own wrapper instead of stacking a new layer on every round trip.
		/// </summary>
		private readonly Action<double, string> asAction;

		/// <summary>
		/// The reporter this one yields through, for a child made by <see cref="Scaled"/>; null for a
		/// reporter that owns its own throttle.
		/// </summary>
		private readonly ProgressReporter yieldParent;

		/// <summary>
		/// Milliseconds (<see cref="Environment.TickCount64"/>) at the last real yield. Zero to start, so
		/// the first yield of a job always goes through and the bar paints before the work begins.
		/// </summary>
		private long lastYieldMs;

		public ProgressReporter(Action<double, string> target)
			: this(target, null)
		{
		}

		private ProgressReporter(Action<double, string> target, ProgressReporter yieldParent)
		{
			this.target = target;
			this.yieldParent = yieldParent;
			this.asAction = this.Report;
		}

		/// <summary>
		/// Whether anything is actually listening. False for <see cref="Null"/> and for any reporter built
		/// around a null action - the two shapes "nobody is watching" arrives in.
		/// </summary>
		/// <remarks>
		/// Callers that have to tell a watched job from an unwatched one test this rather than comparing
		/// against the <see cref="Null"/> singleton: <c>new ProgressReporter(null)</c> is just as unwatched
		/// and would slip past a reference comparison.
		/// </remarks>
		public bool HasTarget => target != null;

		/// <summary>
		/// A reporter that maps a 0..1 ratio into a slice of this one's progress budget - what a phase of a
		/// job is handed so it can report 0..1 of its own work without knowing where in the whole it sits.
		/// </summary>
		/// <param name="amount">How much of this reporter's budget the child's full range covers.</param>
		/// <param name="offset">Where in this reporter's budget the child's range starts.</param>
		/// <remarks>
		/// The child yields through its parent rather than keeping a throttle of its own, and that is the
		/// point of this method existing instead of a hand written wrapper. A wrapper starts life with its
		/// last yield at zero, so its first <see cref="YieldToUi"/> always hops the event loop; a job that
		/// makes one per item - a load with four hundred cached mesh links, say - then pays four hundred
		/// unthrottled hops for work that takes no time at all. Sharing the parent's state keeps the throttle
		/// a budget per job however many children the job carves it into.
		/// <para>A reporter nobody is watching hands back itself: it reports nowhere and never yields, so
		/// there is nothing for a child to do differently.</para>
		/// </remarks>
		public ProgressReporter Scaled(double amount, double offset = 0)
		{
			if (target == null)
			{
				return this;
			}

			return new ProgressReporter((ratio, message) => Report(offset + (ratio * amount), message), this);
		}

		/// <summary>
		/// Reports progress to whatever this reporter wraps. Synchronous and safe from any thread, including
		/// from a native callback.
		/// </summary>
		/// <param name="ratio">Fraction complete, 0 to 1.</param>
		/// <param name="message">What the job is doing now.</param>
		public void Report(double ratio, string message)
		{
			target?.Invoke(ratio, message);
		}

		/// <summary>
		/// Hands the thread back to the UI long enough for it to paint, if this host needs that and enough
		/// time has passed since the last time. Completes synchronously otherwise, so it is cheap to await
		/// in a loop.
		/// </summary>
		/// <remarks>
		/// Call this only from a job's top level async flow. Calling it from inside a <c>Parallel</c> body
		/// or a native callback is a bug - those are not the job's flow, and on the browser they are not
		/// even the thread that paints.
		/// </remarks>
		public ValueTask YieldToUi()
		{
			// A scaled child has no throttle of its own - the whole job shares the one at the top, so a job
			// that carves its budget into many slices cannot buy itself extra hops by making more of them.
			if (yieldParent != null)
			{
				return yieldParent.YieldToUi();
			}

			var uiYield = UiYield;

			// The desktop path (no hook installed) costs no allocation and no state machine. A reporter
			// with no target - Null, and the sync-only paths that use it - never yields either: nothing is
			// showing this job's progress, so there is no frame worth painting.
			if (uiYield == null
				|| target == null)
			{
				return default;
			}

			long now = Environment.TickCount64;
			if (now - lastYieldMs < YieldThrottleMs)
			{
				return default;
			}

			// Stamped before the hop, not after: the throttle is about how often we interrupt the job, and
			// timing from the start keeps a slow pump from stretching the gap further than asked.
			lastYieldMs = now;

			// A hook that hands back null means "nothing to wait for", not a crash: the reporter is process
			// global state a host installs once, and ValueTask's constructor would throw on the null.
			return new ValueTask(uiYield() ?? Task.CompletedTask);
		}

		/// <summary>
		/// <see cref="Report"/> followed by <see cref="YieldToUi"/> - the usual shape at a job's progress
		/// points.
		/// </summary>
		public ValueTask ReportAndYield(double ratio, string message)
		{
			Report(ratio, message);

			return YieldToUi();
		}

		/// <summary>
		/// Lets a reporter be passed to anything still typed as <c>Action&lt;double, string&gt;</c>.
		/// </summary>
		public static implicit operator Action<double, string>(ProgressReporter reporter)
		{
			// A null reporter converts to a null action rather than to a no-op, so a call site that tests
			// its action for null still sees what it was given.
			return reporter?.asAction;
		}

		/// <summary>
		/// Lets an existing <c>Action&lt;double, string&gt;</c> be handed to anything that now wants a
		/// reporter; a null action becomes <see cref="Null"/>.
		/// </summary>
		public static implicit operator ProgressReporter(Action<double, string> action)
		{
			if (action == null)
			{
				return Null;
			}

			// Unwrap rather than wrap again when this action IS a reporter's Report: converting back and
			// forth across forwarding sites would otherwise build a chain of wrappers, and each layer
			// would carry its own throttle state, so the innermost one would never see a yield.
			if (action.Target is ProgressReporter wrapped
				&& ReferenceEquals(wrapped.asAction, action))
			{
				return wrapped;
			}

			return new ProgressReporter(action);
		}
	}
}
