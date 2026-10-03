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
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;

// Kernel types are aliased rather than imported, as ManifoldKernel.cs explains.
using RustBooleanEngine = ManifoldSharp.BooleanEngine;
using RustCancelToken = ManifoldSharp.CancelToken;
using RustManifold = ManifoldSharp.Manifold;
using RustOpType = ManifoldSharp.OpType;
using RustPhases = ManifoldSharp.Phases;
using RustProgressReporter = ManifoldSharp.ProgressReporter;
using RustWindingRule = ManifoldSharp.WindingRule;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// The n-ary combine spelled out as a left fold of binary booleans, for the winding rules the
	/// kernel's CSG tree cannot express (see <see cref="ManifoldKernel.NeedsExplicitBoolean"/>),
	/// with the per-pair progress that binary entry point can report.
	/// </summary>
	internal static class ManifoldPairwiseFold
	{
		/// <summary>
		/// Configures the kernel before the first call that reaches it through this class.
		/// </summary>
		/// <remarks>
		/// <see cref="ManifoldKernel"/>'s type initializer is the configuration, so its own
		/// members configure the kernel by construction; this class is reached directly too, so it
		/// asks for the same thing. A caller that gets here before any boolean has run still gets
		/// the engine and parallelism a boolean would - see <see cref="ManifoldKernel.EnsureConfigured"/>.
		/// </remarks>
		static ManifoldPairwiseFold()
		{
			ManifoldKernel.EnsureConfigured();
		}

		/// <summary>
		/// Folds the operands together one pair at a time, which is what the explicit
		/// binary entry point - the only one that takes a winding rule and a progress
		/// sink - can express.
		/// </summary>
		/// <remarks>
		/// A left fold, so subtraction still means <c>((a - b) - c)</c>, exactly what
		/// BatchBoolean computes for the same list. The engine is passed explicitly
		/// rather than read from the process-global default, but it is the same
		/// <see cref="RustBooleanEngine.Auto"/> <see cref="ManifoldKernel"/>'s static constructor installs; note
		/// that Auto resolves to the robust engine for
		/// <see cref="RustWindingRule.Nonzero"/>, which is the point of asking for it.
		/// <para>
		/// The fold builds one <see cref="RustCancelToken"/> per pair, and each of those
		/// registers a callback on <paramref name="cancellationToken"/>'s source that is
		/// never unregistered (see <see cref="ManifoldCancellableBoolean.Boolean"/> for why the registration is not
		/// disposed). The accumulation is bounded, and by two things: the count is
		/// <c>manifolds.Count - 1</c> per boolean, and every caller in this tree drives a
		/// per-task <c>CancellationTokenSource</c> that is disposed when the operation
		/// finishes - so the callback list dies with the operation that owns it. What would
		/// break that is a long-lived, app-lifetime source threaded into repeated booleans:
		/// its list would grow by n-1 entries per call and never shrink. No caller does
		/// that today, and one that wants to should give each operation its own linked
		/// source rather than teaching this loop to reuse a token.
		/// </para>
		/// </remarks>
		internal static RustManifold CombinePairwise(
			List<RustManifold> manifolds,
			RustOpType operationType,
			RustWindingRule windingRule,
			CancellationToken cancellationToken,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted)
		{
			// AnyoneWatching rather than a null check: this fold is reached for a winding rule the
			// batch path cannot express, whoever is watching, so a targetless reporter does get here -
			// and building an adapter around its never-null no-op action would pay for a progress bar
			// nobody can see, once per pair.
			var progress = ManifoldKernel.AnyoneWatching(reporter)
				? new BooleanProgressAdapter(reporter, ratioCompleted, amountPerOperation, manifolds.Count - 1)
				: null;
			var progressSink = ProgressSinkFor(progress);

			// Only ever holds an intermediate this method created; manifolds[0] is the
			// caller's and is never assigned here.
			RustManifold accumulated = null;

			for (int i = 1; i < manifolds.Count; i++)
			{
				accumulated = ManifoldCancellableBoolean.Boolean(
					accumulated ?? manifolds[0],
					manifolds[i],
					operationType,
					RustBooleanEngine.Auto,
					windingRule,
					progressSink,
					cancellationToken);

				progress?.CompleteOperation(ManifoldKernel.CombineCompletePhase);
			}

			return accumulated;
		}

		/// <summary>
		/// <see cref="CombinePairwise"/> with the UI given its thread back between two operands.
		/// </summary>
		/// <remarks>
		/// Between, and only between: a single pairwise <see cref="ManifoldCancellableBoolean.Boolean"/> is one call that
		/// cannot hand anything back part way through, so this is the whole of what an n-ary union
		/// can offer a host where the job and the UI share a thread. The yield is placed after the
		/// step's own completion report so the bar has somewhere new to move to before it paints.
		/// </remarks>
		internal static async Task<RustManifold> CombinePairwiseAsync(
			List<RustManifold> manifolds,
			RustOpType operationType,
			RustWindingRule windingRule,
			CancellationToken cancellationToken,
			ProgressReporter reporter,
			double amountPerOperation,
			double ratioCompleted)
		{
			// The same AnyoneWatching test the synchronous fold makes, and it has to be the same one.
			// The null check this replaces was answering a different question than the routing in ManifoldKernel
			// did: it was here because the adapter's constructor refuses a null Action outright, so
			// null was excluded while a targetless reporter - which converts to a never-null no-op
			// action - sailed through and built an adapter reporting into the void. One test for both
			// makes that constructor's guard unreachable from here by construction.
			var progress = ManifoldKernel.AnyoneWatching(reporter)
				? new BooleanProgressAdapter(reporter, ratioCompleted, amountPerOperation, manifolds.Count - 1)
				: null;
			var progressSink = ProgressSinkFor(progress);

			RustManifold accumulated = null;

			for (int i = 1; i < manifolds.Count; i++)
			{
				accumulated = ManifoldCancellableBoolean.Boolean(
					accumulated ?? manifolds[0],
					manifolds[i],
					operationType,
					RustBooleanEngine.Auto,
					windingRule,
					progressSink,
					cancellationToken);

				progress?.CompleteOperation(ManifoldKernel.CombineCompletePhase);

				// The token is read with the yield: the yield is what lets the user press Stop at
				// all, so reading it here is what makes the press land on this pair rather than
				// after the whole fold.
				cancellationToken.ThrowIfCancellationRequested();
				await (reporter?.YieldToUi() ?? default);
			}

			return accumulated;
		}

		/// <summary>
		/// The kernel's progress sink for one of ours, or null when nobody is watching.
		/// </summary>
		/// <remarks>
		/// The kernel reports a <see cref="ManifoldSharp.Phase"/> and a fraction; the rest of
		/// the pipeline wants a name and a fraction. The name is the kernel's own
		/// <see cref="RustPhases.Name"/>, which is the same table the retired P/Invoke binding
		/// read out of the native library, so the strings a user sees are unchanged.
		/// <para>
		/// The callback may run on a worker thread - the boolean pipeline parallelizes - which
		/// is exactly the contract <see cref="BooleanProgressAdapter"/> is written against.
		/// </para>
		/// </remarks>
		internal static RustProgressReporter ProgressSinkFor(BooleanProgressAdapter adapter)
		{
			return adapter == null
				? null
				: new RustProgressReporter((phase, fraction) => adapter.Report((RustPhases.Name(phase), fraction)));
		}
	}
}
