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
using MatterHackers.VectorMath;

// Every type from the kernel is aliased rather than reached through its namespace, and
// the namespace is deliberately NOT imported: ManifoldSharp.Manifold and
// MatterHackers.PolygonMesh.Mesh are both "the mesh type" at a glance, and
// ManifoldSharp.ProgressReporter and MatterHackers.Agg.ProgressReporter are both
// "the progress sink". The Rust prefix names the kernel's lineage - ManifoldSharp is
// the exact-match C# port of manifold-rust, which the retired ManifoldRust NuGet
// package reached through P/Invoke - so a use site still says which library it means.
using RustBooleanConfig = ManifoldSharp.BooleanConfig;
using RustBooleanEngine = ManifoldSharp.BooleanEngine;
using RustManifold = ManifoldSharp.Manifold;
using RustOpType = ManifoldSharp.OpType;
using RustParallel = ManifoldSharp.ManifoldParallel;
using RustWindingRule = ManifoldSharp.WindingRule;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// The ManifoldSharp boolean backend: the kernel
	/// <see cref="BooleanProcessing.DoArray"/> runs every polygon-mode boolean through.
	/// </summary>
	/// <remarks>
	/// Internal, and everything a caller outside this assembly needs is re-exposed on
	/// <see cref="BooleanProcessing"/>: which kernel does the arithmetic is not part of
	/// the CSG contract, and there is no second engine to sit beside this one.
	/// <para>
	/// Three things it does that the C++ ManifoldNET engine it replaced could not, and
	/// which are why the migration happened: coordinates upload as <c>double</c> rather
	/// than being narrowed to <c>float</c> at the boundary, the run data needed for face
	/// colours comes back as ordinary managed collections (no raw P/Invoke and no
	/// reflection into a private handle field), and the caller's
	/// <see cref="CancellationToken"/> actually reaches the kernel.
	/// </para>
	/// <para>
	/// The kernel used to be a Rust cdylib behind the ManifoldRust P/Invoke binding and is
	/// now ManifoldSharp, the exact-match C# port of the same Rust source. What that swap
	/// changed here is bookkeeping, not arithmetic: a manifold is an ordinary managed
	/// object, so nothing needs disposing and no import can fail because a library would
	/// not load. What it did not change is the shape of the calls - the entry points in
	/// <see cref="ManifoldCancellableBoolean"/> reproduce the FFI's own definitions of the two
	/// composites the binding exposed (the n-ary <c>BatchBoolean</c> as a CSG tree, and
	/// cancellation as a thrown <see cref="OperationCanceledException"/>), so the geometry and
	/// the failure modes are the ones this code was written against.
	/// </para>
	/// <para>
	/// This class is the boolean's driver - configuration, the operand loop, and which combine
	/// runs - and each stage it drives is its own class: <see cref="ManifoldOperandBatch"/>
	/// collects the operands and their colour bookkeeping, <see cref="ManifoldImport"/> gets a
	/// mesh into the kernel, <see cref="ManifoldCancellableBoolean"/> and
	/// <see cref="ManifoldPairwiseFold"/> combine them, and <see cref="ManifoldResultReader"/>
	/// reads the answer back out as a <see cref="Mesh"/>.
	/// </para>
	/// </remarks>
	internal static class ManifoldKernel
	{
		/// <summary>
		/// Selects the kernel's boolean engine and its parallelism, once, before the first
		/// boolean runs.
		/// </summary>
		/// <remarks>
		/// Both settings are process-global, so they are made in a static constructor
		/// rather than per call. <see cref="RustBooleanEngine.Auto"/> keeps the fast exact
		/// pipeline for strictly manifold operands and only pays for the slower
		/// rational-arithmetic engine when an operand came in through the robust import as
		/// non-manifold soup.
		/// <para>
		/// Parallelism is on for the same reason: it is what this code has always run on.
		/// The Rust <c>parallel</c> Cargo feature was in manifold-ffi's default feature set,
		/// so every desktop native the ManifoldRust package shipped had rayon compiled in;
		/// the browser-wasm archive alone was built <c>--no-default-features</c>, because
		/// emscripten without <c>-pthread</c> has no worker threads to schedule on. The
		/// switch below is the C# stand-in for that Cargo feature and reproduces exactly
		/// that split, so the swap is performance-neutral rather than a silent
		/// single-threading of every boolean. It is safe to flip either way at any time:
		/// only sites whose output is provably identical to the sequential build are
		/// parallelized, so the geometry is bit-identical with it on or off (see Par.cs).
		/// </para>
		/// <para>
		/// Neither call can throw. <see cref="RustBooleanConfig.SetDefaultEngine"/> rejects
		/// only an undeclared enum value, and reading the parallel switch touches no
		/// environment of its own - <see cref="RustParallel"/> read that at its own class
		/// init, which is where a hostile environment would have been felt and where it
		/// would be that type's problem rather than a poisoned
		/// <c>TypeInitializationException</c> on this one. The kernel is managed now, so
		/// the library-load and version-handshake failures the old guard here existed for
		/// cannot happen at all; there is nothing left worth catching.
		/// </para>
		/// </remarks>
		static ManifoldKernel()
		{
			RustBooleanConfig.SetDefaultEngine(RustBooleanEngine.Auto);

			// An explicit MANIFOLD_PARALLEL wins, whichever way it points. Overwriting it
			// would make MANIFOLD_PARALLEL=0 mean nothing and quietly turn the port's
			// two-configuration determinism net into the same configuration run twice -
			// see ManifoldParallel.ConfiguredByEnvironment, which exists for this.
			if (!RustParallel.ConfiguredByEnvironment)
			{
				// Not OperatingSystem.IsBrowser() inverted for its own sake: the question is
				// whether this host has threads to run the maps on, and the browser is the one
				// agg platform that does not.
				RustParallel.Enabled = !OperatingSystem.IsBrowser();
			}
		}

		/// <summary>
		/// Runs the kernel's one-time process-global configuration if it has not run yet.
		/// </summary>
		/// <remarks>
		/// The static constructor above is the configuration; this only guarantees it has
		/// happened. A type initializer fires on first use of the type, so every path that
		/// reaches the kernel <em>through this class</em> is configured by construction -
		/// but <see cref="MeshRepair"/> holds the kernel's types directly and never touches
		/// <see cref="ManifoldKernel"/>, so a session that repaired a mesh before it ever
		/// ran a boolean got the kernel's own defaults (Exact, sequential) instead of this
		/// one's. That is not a wrong answer - the repair is engine-independent and Par.cs
		/// guarantees bit-identity either way - but it is a different amount of work than
		/// the same call makes after a boolean has run, which is exactly the kind of
		/// set-once ordering Par.cs asks hosts to pin rather than leave to whichever
		/// operation happened to be first.
		/// <para>
		/// Idempotent and cheap: after the first call it is a no-op the JIT can elide
		/// entirely, because the CLR guarantees a type initializer runs at most once.
		/// </para>
		/// </remarks>
		internal static void EnsureConfigured()
		{
			// Referencing any member of the type is enough to force the initializer; this
			// spelling says so out loud rather than relying on a side effect of the call
			// the caller was going to make anyway.
			System.Runtime.CompilerServices.RuntimeHelpers.RunClassConstructor(typeof(ManifoldKernel).TypeHandle);
		}

		/// <summary>
		/// Perform a boolean operation via the ManifoldSharp kernel. Every failure
		/// surfaces as a managed exception - including cancellation, as
		/// <see cref="OperationCanceledException"/> - and
		/// <see cref="BooleanProcessing.DoArray"/> passes them all straight to the caller.
		/// </summary>
		/// <remarks>
		/// Reached through <see cref="BooleanProcessing.DoArrayViaManifoldRust"/>, which is
		/// the name the tests use to watch the kernel reject an input directly;
		/// <see cref="BooleanProcessing.DoArray"/> is the entry point everything else uses.
		/// <para>
		/// A union whose operands are not all usable degrades rather than failing outright: the
		/// ones that imported are combined and the answer leaves as
		/// <see cref="PartialBooleanException"/>, which carries both the result and the list of
		/// operands that were left out. It is still a throw, so no caller loses a part quietly -
		/// see that type for why.
		/// </para>
		/// </remarks>
		/// <param name="windingRule">
		/// Which winding numbers the kernel counts as solid.
		/// <see cref="RustWindingRule.Nonzero"/> keeps inside-out shells as material
		/// instead of letting them cancel; it also forces the robust engine, because
		/// the rule has no meaning to the exact one.
		/// </param>
		/// <param name="repairOrientation">
		/// Rewind each operand's inside-out shells before combining. The alternative to
		/// <see cref="RustWindingRule.Nonzero"/>: it fixes the data once rather than
		/// changing what every later operation means by "solid".
		/// </param>
		internal static Mesh RunBoolean(
			IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			CancellationToken cancellationToken,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted,
			Color[] meshColors,
			RustWindingRule windingRule = RustWindingRule.Positive,
			bool repairOrientation = false)
		{
			var batch = new ManifoldOperandBatch(operation, meshColors, repairOrientation);

			foreach (var (mesh, matrix) in items)
			{
				if (!batch.TryAdd(mesh, matrix, cancellationToken))
				{
					return new Mesh();
				}
			}

			batch.ThrowIfNothingImported();

			var result = CombineAndRead(
				batch, operation, cancellationToken, windingRule, reporter, amountPerOperation, ratioCompleted);

			batch.ThrowIfAnySkipped(result);

			return result;
		}

		/// <summary>
		/// <see cref="RunBoolean"/> for a caller that can hand the UI its thread back: the same
		/// boolean, with a yield after every operand's import and around the combine.
		/// </summary>
		/// <remarks>
		/// The yields are what make a boolean's progress bar move at all on a host where the job and
		/// the UI share one thread. They are only ever between native calls: the n-ary batch (or one
		/// pairwise boolean of a winding-rule fold) is one uninterruptible call, so the frame it
		/// holds is still frozen for however long that call takes.
		/// <para>
		/// A <paramref name="reporter"/> nobody is watching - null, or one with no target - never
		/// yields - see <see cref="AnyoneWatching"/>.
		/// </para>
		/// </remarks>
		/// <inheritdoc cref="RunBoolean"/>
		internal static async Task<Mesh> RunBooleanAsync(
			IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			CancellationToken cancellationToken,
			ProgressReporter reporter,
			double amountPerOperation,
			double ratioCompleted,
			Color[] meshColors,
			RustWindingRule windingRule = RustWindingRule.Positive,
			bool repairOrientation = false)
		{
			var batch = new ManifoldOperandBatch(operation, meshColors, repairOrientation);

			foreach (var (mesh, matrix) in items)
			{
				if (!batch.TryAdd(mesh, matrix, cancellationToken))
				{
					return new Mesh();
				}

				// A mesh copy, a transform and an import each - seconds apiece on a large set,
				// and all of it before the boolean itself starts. The token is read with the
				// yield because the yield is what lets the user press Stop at all, so reading it
				// here is what makes the press land on this operand rather than after the import.
				cancellationToken.ThrowIfCancellationRequested();
				await (reporter?.YieldToUi() ?? default);
			}

			batch.ThrowIfNothingImported();

			var result = await CombineAndReadAsync(
				batch, operation, cancellationToken, windingRule, reporter, amountPerOperation, ratioCompleted);

			batch.ThrowIfAnySkipped(result);

			return result;
		}

		/// <summary>
		/// Runs the n-ary boolean over already-imported operands and reads the result
		/// back into a <see cref="Mesh"/>.
		/// </summary>
		private static Mesh CombineAndRead(
			ManifoldOperandBatch batch,
			CsgModes operation,
			CancellationToken cancellationToken,
			RustWindingRule windingRule,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted)
		{
			if (batch.Manifolds.Count == 0)
			{
				return new Mesh();
			}

			var operationType = OperationTypeOf(operation);

			RustManifold boolResult;
			if (batch.Manifolds.Count == 1)
			{
				// A single operand is its own answer; running a one-operand boolean would
				// only cost a copy.
				boolResult = batch.Manifolds[0];
			}
			else if (NeedsExplicitBoolean(windingRule))
			{
				boolResult = ManifoldPairwiseFold.CombinePairwise(
					batch.Manifolds, operationType, windingRule, cancellationToken, reporter, amountPerOperation, ratioCompleted);
			}
			else
			{
				var progress = BatchProgressFor(reporter, amountPerOperation, ratioCompleted);
				ManifoldKernelCallCounts.CountOperandCombine();
				boolResult = ManifoldCancellableBoolean.BatchBoolean(
					batch.Manifolds, operationType, ManifoldPairwiseFold.ProgressSinkFor(progress), cancellationToken);
				progress?.CompleteOperation(CombineCompletePhase);
			}

			return ManifoldResultReader.ReadResult(boolResult, batch);
		}

		/// <summary>
		/// <see cref="CombineAndRead"/> with a yield on either side of the batch, or between each
		/// pair of a winding-rule fold.
		/// </summary>
		/// <remarks>
		/// Only the combine differs: reading the result back is one export and a walk over managed
		/// collections, with no point inside it where handing the frame away would leave the
		/// kernel's data in a state anything else may look at.
		/// </remarks>
		private static async Task<Mesh> CombineAndReadAsync(
			ManifoldOperandBatch batch,
			CsgModes operation,
			CancellationToken cancellationToken,
			RustWindingRule windingRule,
			ProgressReporter reporter,
			double amountPerOperation,
			double ratioCompleted)
		{
			if (batch.Manifolds.Count == 0)
			{
				return new Mesh();
			}

			var operationType = OperationTypeOf(operation);

			RustManifold boolResult;
			if (batch.Manifolds.Count == 1)
			{
				boolResult = batch.Manifolds[0];
			}
			else if (NeedsExplicitBoolean(windingRule))
			{
				boolResult = await ManifoldPairwiseFold.CombinePairwiseAsync(
					batch.Manifolds, operationType, windingRule, cancellationToken, reporter, amountPerOperation, ratioCompleted);
			}
			else
			{
				// The batch is one kernel call: its booleans stream their phases into the bar from
				// inside it (on whatever thread runs them), but nothing inside it can yield. Yield on
				// both sides of it: before, so the "combining" report paints before the frame is
				// held; after, so the finished bar paints and a Stop pressed while the call ran lands
				// here rather than after the result is read back.
				var progress = BatchProgressFor(reporter, amountPerOperation, ratioCompleted);
				cancellationToken.ThrowIfCancellationRequested();
				await (reporter?.YieldToUi() ?? default);

				ManifoldKernelCallCounts.CountOperandCombine();
				boolResult = ManifoldCancellableBoolean.BatchBoolean(
					batch.Manifolds, operationType, ManifoldPairwiseFold.ProgressSinkFor(progress), cancellationToken);

				progress?.CompleteOperation(CombineCompletePhase);
				cancellationToken.ThrowIfCancellationRequested();
				await (reporter?.YieldToUi() ?? default);
			}

			return ManifoldResultReader.ReadResult(boolResult, batch);
		}

		/// <summary>
		/// The kernel's name for one of our operations.
		/// </summary>
		private static RustOpType OperationTypeOf(CsgModes operation)
		{
			if (operation == CsgModes.Subtract)
			{
				return RustOpType.Subtract;
			}

			if (operation == CsgModes.Intersect)
			{
				return RustOpType.Intersect;
			}

			return RustOpType.Add;
		}

		/// <summary>
		/// Whether the n-ary combine has to be spelled out as a pairwise fold rather than handed to
		/// the kernel whole.
		/// </summary>
		/// <remarks>
		/// BatchBoolean runs the kernel's CSG tree, which is what makes a large n-ary union
		/// tractable, but it takes no winding rule - it reads the process-global engine and the
		/// default rule - so only a non-default rule drops to a pairwise left fold over the
		/// explicit binary entry point. Progress is NOT a reason to fold: the fold re-runs a full
		/// binary boolean against the growing result for every operand, which turned a 20-operand
		/// union from seconds into minutes, and every boolean in the app is watched. A watched
		/// batch hands its reporter to the tree instead, which passes it to each boolean it runs
		/// (<see cref="BatchProgressFor"/>).
		/// </remarks>
		private static bool NeedsExplicitBoolean(RustWindingRule windingRule)
		{
			return windingRule != RustWindingRule.Positive;
		}

		/// <summary>
		/// The bar for a batch boolean: already told the combine has started, fed the kernel's
		/// phases through <see cref="ManifoldPairwiseFold.ProgressSinkFor"/> while it runs, and
		/// closed out by the caller with <see cref="BooleanProgressAdapter.CompleteOperation"/>
		/// once it returns. Null when nobody is watching.
		/// </summary>
		/// <remarks>
		/// The whole batch is one operation window, and every boolean the kernel's tree runs
		/// restarts its phases at fraction 0 inside it. The adapter's high-water mark keeps the
		/// bar from going backwards, so on a multi-boolean tree it climbs with the first boolean
		/// that gets far and then holds while later ones catch up; the phase name in the message
		/// is what keeps moving.
		/// </remarks>
		private static BooleanProgressAdapter BatchProgressFor(ProgressReporter reporter, double amountPerOperation, double ratioCompleted)
		{
			if (!AnyoneWatching(reporter))
			{
				return null;
			}

			var progress = new BooleanProgressAdapter(reporter, ratioCompleted, amountPerOperation, 1);
			progress.Report((CombineCompletePhase, null));
			return progress;
		}

		/// <summary>
		/// Whether anything is actually listening to a progress sink.
		/// </summary>
		/// <remarks>
		/// The question is never "is there a reporter object" but "is anyone watching", and the two
		/// differ: <see cref="ProgressReporter.Null"/> and <c>new ProgressReporter(null)</c> report
		/// nowhere, yet both convert to a NON-null <c>Action</c> - the conversion hands back the
		/// reporter's own <c>Report</c> method group - so a null check on either shape answers yes
		/// when the truth is no.
		/// <para>
		/// Getting that wrong costs a progress adapter, and a UI yield per operand on the async path,
		/// for a bar nobody can see. Same test <see cref="MinkowskiProcessing"/>'s morph makes.
		/// </para>
		/// <para>
		/// Takes a <see cref="ProgressReporter"/> so both entry points can share it: an
		/// <c>Action</c> converts implicitly, a null one becoming <see cref="ProgressReporter.Null"/>,
		/// and the conversion unwraps a reporter's own <c>Report</c> rather than wrapping it again -
		/// so a sync caller's action arrives here as the reporter it came from.
		/// </para>
		/// </remarks>
		internal static bool AnyoneWatching(ProgressReporter reporter)
		{
			return reporter != null && reporter.HasTarget;
		}

		/// <summary>
		/// Message phase for the edges of a combine - either end of the batch, or between two
		/// pairwise booleans - where the kernel itself has nothing to report.
		/// </summary>
		internal const string CombineCompletePhase = "combining";
	}
}
