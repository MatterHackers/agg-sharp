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
using RustCancelToken = ManifoldSharp.CancelToken;
using RustCsgLeaf = ManifoldSharp.CsgLeaf;
using RustCsgNode = ManifoldSharp.CsgNode;
using RustCsgOp = ManifoldSharp.CsgOp;
using RustManifold = ManifoldSharp.Manifold;
using RustMeshGL64 = ManifoldSharp.MeshGL64;
using RustOpType = ManifoldSharp.OpType;
using RustParallel = ManifoldSharp.ManifoldParallel;
using RustPhases = ManifoldSharp.Phases;
using RustProgressReporter = ManifoldSharp.ProgressReporter;
using RustStatus = ManifoldSharp.Error;
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
	/// not load. What it did not change is the shape of the calls - the entry points below
	/// reproduce the FFI's own definitions of the two composites the binding exposed (the
	/// n-ary <c>BatchBoolean</c> as a CSG tree, and cancellation as a thrown
	/// <see cref="OperationCanceledException"/>), so the geometry and the failure modes
	/// are the ones this code was written against.
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
			var batch = new OperandBatch(operation, meshColors, repairOrientation);

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
		/// boolean, with a yield after every operand's import and between every pair of the n-ary
		/// fold.
		/// </summary>
		/// <remarks>
		/// The yields are what make a boolean's progress bar move at all on a host where the job and
		/// the UI share one thread. They are only ever between operations: a single pairwise boolean
		/// is one uninterruptible call, so the frame it holds is still frozen for however long
		/// that call takes.
		/// <para>
		/// A null <paramref name="reporter"/> never yields and keeps the kernel's n-ary batch path,
		/// exactly as in the synchronous entry point - see <see cref="CombineAndRead"/>.
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
			var batch = new OperandBatch(operation, meshColors, repairOrientation);

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
		/// The operands of one boolean as the kernel holds them: the imported manifolds, the
		/// colour bookkeeping their result is painted from, and the ones the kernel would not
		/// take.
		/// </summary>
		/// <remarks>
		/// Its own type because <see cref="RunBoolean"/> and <see cref="RunBooleanAsync"/> both walk
		/// the same operand list, and what a refused or an empty operand means is subtle enough that
		/// two copies of it would drift. The loop is the only thing that differs between them - one
		/// of them can hand the UI its thread back between operands - so the loop is all either of
		/// them writes.
		/// </remarks>
		private sealed class OperandBatch
		{
			private readonly CsgModes operation;
			private readonly Color[] meshColors;
			private readonly bool repairOrientation;

			// A union is the one operation where leaving an operand out still has an answer:
			// the other operands' union. Subtract and Intersect are defined by every operand,
			// so dropping one of those would silently change what the operation means.
			private readonly bool skipRefusedOperands;

			internal OperandBatch(CsgModes operation, Color[] meshColors, bool repairOrientation)
			{
				this.operation = operation;
				this.meshColors = meshColors;
				this.repairOrientation = repairOrientation;
				this.skipRefusedOperands = operation == CsgModes.Union;
			}

			internal List<RustManifold> Manifolds { get; } = new List<RustManifold>();

			internal List<SkippedBooleanOperand> Skipped { get; } = new List<SkippedBooleanOperand>();

			internal Dictionary<int, Color> OriginalIdToColor { get; } = new Dictionary<int, Color>();

			internal Dictionary<int, List<(Vector3, Color)>> OriginalIdToSpatialColors { get; } = new Dictionary<int, List<(Vector3, Color)>>();

			/// <summary>
			/// The per-operand colours the caller supplied, or null when it asked for no colour
			/// tracking at all.
			/// </summary>
			internal Color[] MeshColors => this.meshColors;

			internal bool TrackColors => this.meshColors != null;

			/// <summary>
			/// How many operands have been offered, the refused and the empty ones included - the
			/// count a partial result's message is read against.
			/// </summary>
			internal int OperandCount { get; private set; }

			/// <summary>
			/// Uploads one operand, recording a refusal rather than throwing when a union may go on
			/// without it.
			/// </summary>
			/// <returns>
			/// False when this operand makes the whole boolean empty - an empty mesh in an Intersect,
			/// or as the first operand of a Subtract - which the caller answers with an empty result.
			/// </returns>
			internal bool TryAdd(Mesh mesh, Matrix4X4 matrix, CancellationToken cancellationToken)
			{
				// Before the copy and the upload, not just before the boolean: importing a
				// large set is itself seconds of work, and an already-cancelled caller should
				// not pay for N mesh copies and N imports it is going to throw away.
				cancellationToken.ThrowIfCancellationRequested();

				int meshIndex = this.OperandCount;

				if (mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
				{
					if (this.operation == CsgModes.Intersect)
					{
						return false;
					}

					if (meshIndex == 0 && this.operation == CsgModes.Subtract)
					{
						return false;
					}

					this.OperandCount++;
					return true;
				}

				var meshCopy = mesh.Copy(CancellationToken.None);
				meshCopy.Transform(matrix);

				try
				{
					this.Manifolds.Add(ImportOperand(
						meshCopy,
						meshIndex,
						this.TrackColors,
						this.meshColors,
						this.OriginalIdToColor,
						this.OriginalIdToSpatialColors,
						cancellationToken,
						this.repairOrientation));
				}
				catch (MeshImportRejectedException refused) when (this.skipRefusedOperands)
				{
					// Only the kernel's verdict on this operand's geometry is skippable. A
					// failure from anywhere else in the import propagates, because degrading on it
					// would tell the user to Repair a part that has nothing wrong with it.
					// Not swallowed: the throw from ThrowIfNothingImported or ThrowIfAnySkipped
					// names every operand that landed here.
					this.Skipped.Add(new SkippedBooleanOperand(meshIndex, refused.Message));
				}

				this.OperandCount++;
				return true;
			}

			/// <summary>
			/// Refuses to run a boolean the kernel took no operand for.
			/// </summary>
			/// <remarks>
			/// The union of nothing is not geometry, but it is still the partial answer rather than a
			/// different kind of failure: a caller combining several touching sets has to be able to
			/// keep the sets that worked and keep these parts visible, and a plain
			/// InvalidOperationException here would take the whole build down with them. Callers that
			/// do not handle the partial case see the same InvalidOperationException they always did,
			/// naming every operand.
			/// </remarks>
			internal void ThrowIfNothingImported()
			{
				if (this.Skipped.Count > 0 && this.Manifolds.Count == 0)
				{
					throw new PartialBooleanException(DescribeSkipped(this.Skipped, this.OperandCount), new Mesh(), this.Skipped);
				}
			}

			/// <summary>
			/// Carries a finished result out as a partial one when the kernel refused any operand, so
			/// no caller loses a part quietly.
			/// </summary>
			internal void ThrowIfAnySkipped(Mesh result)
			{
				if (this.Skipped.Count > 0)
				{
					throw new PartialBooleanException(DescribeSkipped(this.Skipped, this.OperandCount), result, this.Skipped);
				}
			}
		}

		/// <summary>
		/// Uploads one already-transformed operand, recording whatever the colour machinery
		/// needs to paint that operand's triangles in the result.
		/// </summary>
		/// <remarks>
		/// Its own method so the caller can wrap exactly the import in a catch: a refused
		/// operand has to be told apart from a failure anywhere else in the loop.
		/// </remarks>
		private static RustManifold ImportOperand(
			Mesh meshCopy,
			int meshIndex,
			bool trackColors,
			Color[] meshColors,
			Dictionary<int, Color> originalIdToColor,
			Dictionary<int, List<(Vector3, Color)>> originalIdToSpatialColors,
			CancellationToken cancellationToken,
			bool repairOrientation)
		{
			if (trackColors && meshCopy.FaceColors != null)
			{
				var split = TrySplitByFaceColorsRust(meshCopy, originalIdToColor, cancellationToken, repairOrientation);

				if (split != null)
				{
					return split;
				}
			}

			if (!trackColors)
			{
				return Import(meshCopy, repairOrientation);
			}

			// AsOriginal is what gives the input an OriginalId, and the run data
			// that carries colours back is keyed on that. Without colours the run
			// data is never read, so the extra copy would be pure waste.
			var manifold = ImportAsOriginal(meshCopy, repairOrientation);

			// -1 is what OriginalId reports when the re-tag did not take, which is what happens to a
			// soup manifold. It is not an ID: every such operand would register under the same key and
			// the last one would silently answer for all of them. Registering nothing instead leaves
			// this operand's triangles unattributed, which is what they honestly are.
			if (manifold.OriginalId() >= 0)
			{
				if (meshCopy.FaceColors != null)
				{
					originalIdToSpatialColors[manifold.OriginalId()] = meshCopy.SaveFaceCentroidColors();
				}
				// Nothing registered past the end of meshColors: the caller supplied no colour for
				// this operand, and inventing one would make its triangles count as attributed and
				// come back wearing a colour from nowhere. Unreachable from this repository - every
				// caller builds the array in lockstep with the operand list - but BooleanProcessing
				// is public API, so a short array has to mean "no colour known" rather than "grey".
				else if (meshIndex < meshColors.Length)
				{
					originalIdToColor[manifold.OriginalId()] = meshColors[meshIndex];
				}
			}

			return manifold;
		}

		/// <summary>
		/// The message a partially completed union carries out, naming every operand the
		/// kernel would not take and repeating its complaint.
		/// </summary>
		private static string DescribeSkipped(List<SkippedBooleanOperand> skipped, int operandCount)
		{
			var described = new List<string>();

			foreach (var operand in skipped)
			{
				// One-based: these numbers are read by a human against a list of parts.
				described.Add($"operand {operand.Index + 1} - {operand.Reason}");
			}

			return $"Manifold could not use {skipped.Count} of {operandCount} operands: {string.Join("; ", described)}";
		}

		/// <summary>
		/// Runs the n-ary boolean over already-imported operands and reads the result
		/// back into a <see cref="Mesh"/>.
		/// </summary>
		private static Mesh CombineAndRead(
			OperandBatch batch,
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
			else if (NeedsExplicitBoolean(reporter, windingRule))
			{
				boolResult = CombinePairwise(
					batch.Manifolds, operationType, windingRule, cancellationToken, reporter, amountPerOperation, ratioCompleted);
			}
			else
			{
				boolResult = BatchBoolean(batch.Manifolds, operationType, cancellationToken);
			}

			return ReadResult(boolResult, batch);
		}

		/// <summary>
		/// <see cref="CombineAndRead"/> with a yield between each pair of the n-ary fold.
		/// </summary>
		/// <remarks>
		/// Only the fold differs: reading the result back is one export and a walk over managed
		/// collections, with no point inside it where handing the frame away would leave the
		/// kernel's data in a state anything else may look at.
		/// </remarks>
		private static async Task<Mesh> CombineAndReadAsync(
			OperandBatch batch,
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
			else if (NeedsExplicitBoolean(reporter, windingRule))
			{
				boolResult = await CombinePairwiseAsync(
					batch.Manifolds, operationType, windingRule, cancellationToken, reporter, amountPerOperation, ratioCompleted);
			}
			else
			{
				boolResult = BatchBoolean(batch.Manifolds, operationType, cancellationToken);
			}

			return ReadResult(boolResult, batch);
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
		/// tractable, but it reports no progress and takes no winding rule - it reads the
		/// process-global engine and the default rule. So it stays the path whenever neither is
		/// asked for, and asking for either drops to a pairwise left fold over the explicit binary
		/// entry point.
		/// </remarks>
		/// <param name="reporter">
		/// Whatever the caller's progress sink is - an <c>Action</c> or a
		/// <see cref="ProgressReporter"/>. Only whether it exists is read here, and it costs the
		/// batch path either way, which is why callers with nobody watching pass null rather than a
		/// do-nothing reporter.
		/// </param>
		private static bool NeedsExplicitBoolean(object reporter, RustWindingRule windingRule)
		{
			return reporter != null || windingRule != RustWindingRule.Positive;
		}

		/// <summary>
		/// Reads a finished boolean back out of the kernel as a <see cref="Mesh"/>, painting its
		/// faces from the run data when the caller asked for colour tracking.
		/// </summary>
		private static Mesh ReadResult(RustManifold boolResult, OperandBatch batch)
		{
			ThrowIfErrored(boolResult, "boolean");

			var result = Export(boolResult);
			var resultMesh = BuildMesh(result, "boolean");

			if (batch.TrackColors && resultMesh.Faces.Count > 0)
			{
				var faceColors = ExtractFaceColorsFromRuns(
					result, resultMesh, batch.OriginalIdToColor, batch.OriginalIdToSpatialColors, batch.MeshColors);
				if (faceColors != null)
				{
					resultMesh.FaceColors = faceColors;
				}
			}

			return resultMesh;
		}

		/// <summary>
		/// Reads a finished kernel result back out as a <see cref="Mesh"/>, refusing one the
		/// kernel could not build.
		/// </summary>
		/// <remarks>
		/// The colourless half of <see cref="ReadResult"/>, shared with the operations that
		/// have no run data to paint from - see <see cref="MinkowskiProcessing"/>. Face
		/// colours only mean something when the caller supplied per-operand colours, which
		/// only the boolean path does.
		/// </remarks>
		/// <param name="operationName">
		/// What to call the operation in a failure message, so a caller reading the log knows
		/// which kernel call refused rather than only that one did.
		/// </param>
		internal static Mesh ToMesh(RustManifold result, string operationName)
		{
			ThrowIfErrored(result, operationName);

			return BuildMesh(Export(result), operationName);
		}

		/// <summary>
		/// Refuses a result the kernel finished in an error state.
		/// </summary>
		/// <remarks>
		/// Exporting an error manifold is safe here - unlike the C++ engine, which could fault
		/// the CLR doing it - but an error status still means the kernel could not build the
		/// solid, and a half-built one is worse than a failure.
		/// </remarks>
		private static void ThrowIfErrored(RustManifold result, string operationName)
		{
			if (result.Status() != RustStatus.NoError)
			{
				throw new InvalidOperationException($"Manifold {operationName} result has error status: {result.Status()}");
			}
		}

		/// <summary>
		/// The kernel's export of a finished manifold: positions, indices and the run data a
		/// caller may want to attribute triangles with.
		/// </summary>
		private static RustMeshGL64 Export(RustManifold result)
		{
			// -1 is the export's "no property slot holds normals", which is the normal index
			// the retired binding's parameterless GetMeshGL64 passed on every call.
			return result.GetMeshGL64(-1);
		}

		/// <summary>
		/// Rebuilds a <see cref="Mesh"/> from the kernel's flat vertex and index lists.
		/// </summary>
		private static Mesh BuildMesh(RustMeshGL64 exported, string operationName)
		{
			var resultMesh = new Mesh();

			var resultNumProp = (int)exported.NumProp;
			var vertices = exported.VertProperties;
			var indices = exported.TriVerts;

			// The export promises at least x, y, z per vertex. Checked rather than assumed
			// because resultNumProp is the loop stride below, and a zero would spin.
			if (resultNumProp < 3)
			{
				throw new InvalidOperationException($"Manifold {operationName} result has {resultNumProp} properties per vertex, expected at least 3");
			}

			for (int i = 0; i + 2 < vertices.Count; i += resultNumProp)
			{
				resultMesh.Vertices.Add(new Vector3(
					vertices[i],
					vertices[i + 1],
					vertices[i + 2]));
			}

			for (int i = 0; i + 2 < indices.Count; i += 3)
			{
				resultMesh.Faces.Add(new Face(
					(int)indices[i],
					(int)indices[i + 1],
					(int)indices[i + 2],
					resultMesh.Vertices));
			}

			return resultMesh;
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
		/// <see cref="RustBooleanEngine.Auto"/> the static constructor installs; note
		/// that Auto resolves to the robust engine for
		/// <see cref="RustWindingRule.Nonzero"/>, which is the point of asking for it.
		/// <para>
		/// The fold builds one <see cref="RustCancelToken"/> per pair, and each of those
		/// registers a callback on <paramref name="cancellationToken"/>'s source that is
		/// never unregistered (see <see cref="Boolean"/> for why the registration is not
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
		private static RustManifold CombinePairwise(
			List<RustManifold> manifolds,
			RustOpType operationType,
			RustWindingRule windingRule,
			CancellationToken cancellationToken,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted)
		{
			var progress = reporter == null
				? null
				: new BooleanProgressAdapter(reporter, ratioCompleted, amountPerOperation, manifolds.Count - 1);
			var progressSink = ProgressSinkFor(progress);

			// Only ever holds an intermediate this method created; manifolds[0] is the
			// caller's and is never assigned here.
			RustManifold accumulated = null;

			for (int i = 1; i < manifolds.Count; i++)
			{
				accumulated = Boolean(
					accumulated ?? manifolds[0],
					manifolds[i],
					operationType,
					RustBooleanEngine.Auto,
					windingRule,
					progressSink,
					cancellationToken);

				progress?.CompleteOperation(CombineCompletePhase);
			}

			return accumulated;
		}

		/// <summary>
		/// <see cref="CombinePairwise"/> with the UI given its thread back between two operands.
		/// </summary>
		/// <remarks>
		/// Between, and only between: a single pairwise <see cref="Boolean"/> is one call that
		/// cannot hand anything back part way through, so this is the whole of what an n-ary union
		/// can offer a host where the job and the UI share a thread. The yield is placed after the
		/// step's own completion report so the bar has somewhere new to move to before it paints.
		/// </remarks>
		private static async Task<RustManifold> CombinePairwiseAsync(
			List<RustManifold> manifolds,
			RustOpType operationType,
			RustWindingRule windingRule,
			CancellationToken cancellationToken,
			ProgressReporter reporter,
			double amountPerOperation,
			double ratioCompleted)
		{
			var progress = reporter == null
				? null
				: new BooleanProgressAdapter(reporter, ratioCompleted, amountPerOperation, manifolds.Count - 1);
			var progressSink = ProgressSinkFor(progress);

			RustManifold accumulated = null;

			for (int i = 1; i < manifolds.Count; i++)
			{
				accumulated = Boolean(
					accumulated ?? manifolds[0],
					manifolds[i],
					operationType,
					RustBooleanEngine.Auto,
					windingRule,
					progressSink,
					cancellationToken);

				progress?.CompleteOperation(CombineCompletePhase);

				// The token is read with the yield: the yield is what lets the user press Stop at
				// all, so reading it here is what makes the press land on this pair rather than
				// after the whole fold.
				cancellationToken.ThrowIfCancellationRequested();
				await (reporter?.YieldToUi() ?? default);
			}

			return accumulated;
		}

		/// <summary>
		/// Message phase for the boundary between two pairwise booleans, where the
		/// kernel itself has nothing to report because no operation is running.
		/// </summary>
		private const string CombineCompletePhase = "combining";

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
		private static RustProgressReporter ProgressSinkFor(BooleanProgressAdapter adapter)
		{
			return adapter == null
				? null
				: new RustProgressReporter((phase, fraction) => adapter.Report((RustPhases.Name(phase), fraction)));
		}

		/// <summary>
		/// One binary boolean, with the caller's <see cref="CancellationToken"/> bridged into
		/// the kernel's own cancellation flag.
		/// </summary>
		/// <remarks>
		/// The kernel reports cancellation as <see cref="RustStatus.Cancelled"/> on an empty
		/// result; the rest of this file - and every caller above it - is written against a
		/// thrown <see cref="OperationCanceledException"/>, which is what the P/Invoke binding
		/// translated it into. This is that translation, unchanged, including its two
		/// deliberate properties: a token that can never be signalled allocates nothing and
		/// takes the uncancellable path, and <b>completion wins</b> - a kernel that finished
		/// before it observed the flag returns its result rather than throwing, even if the
		/// token is signalled by the time this returns.
		/// </remarks>
		private static RustManifold Boolean(
			RustManifold a,
			RustManifold b,
			RustOpType operationType,
			RustBooleanEngine engine,
			RustWindingRule windingRule,
			RustProgressReporter progress,
			CancellationToken cancellationToken)
		{
			if (!cancellationToken.CanBeCanceled)
			{
				return a.BooleanWithEngineRuleAndProgress(b, operationType, engine, windingRule, null, progress);
			}

			// One token per operation, as CancelToken's own remarks require: it registers on
			// the caller's source and is never unregistered, so a token that outlived the call
			// would stay rooted in that source's callback list.
			var token = new RustCancelToken(cancellationToken);

			var result = a.BooleanWithEngineRuleAndProgress(b, operationType, engine, windingRule, token, progress);

			if (result.Status() == RustStatus.Cancelled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			return result;
		}

		/// <summary>
		/// The n-ary boolean over already-imported operands: the kernel's CSG tree, which is
		/// what makes a large union tractable, with the same cancellation translation
		/// <see cref="Boolean"/> performs.
		/// </summary>
		/// <remarks>
		/// Spelled out here rather than called, because <c>Manifold.BatchBoolean</c> is not the
		/// same operation: that one is a pairwise left fold, while the batch entry point this
		/// code was written against - manifold-ffi's <c>manifold_rs_batch_boolean_ct</c>, the
		/// one the P/Invoke binding exposed as <c>BatchBoolean</c> - builds an n-ary CSG node
		/// and evaluates it. The tree is the whole point of the batch path (see
		/// <see cref="NeedsExplicitBoolean"/>), so this reproduces the FFI's definition line for
		/// line: leaves over cloned implementations, one <c>CsgOp</c>, evaluate with the token.
		/// </remarks>
		private static RustManifold BatchBoolean(
			List<RustManifold> manifolds,
			RustOpType operationType,
			CancellationToken cancellationToken)
		{
			if (!cancellationToken.CanBeCanceled)
			{
				return BatchBooleanCore(manifolds, operationType, null);
			}

			var token = new RustCancelToken(cancellationToken);

			var result = BatchBooleanCore(manifolds, operationType, token);

			if (result.Status() == RustStatus.Cancelled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			return result;
		}

		/// <summary>
		/// <see cref="BatchBoolean"/> against the kernel's own cancellation flag, which reports
		/// a cancelled run as a status rather than by throwing.
		/// </summary>
		private static RustManifold BatchBooleanCore(
			List<RustManifold> manifolds,
			RustOpType operationType,
			RustCancelToken token)
		{
			// A single operand has nothing to combine with and every operation is identity on
			// it - but an already-cancelled token still wins, so a caller that only polls the
			// status never sees a one-operand call report success after a cancel.
			if (manifolds.Count == 1)
			{
				return token != null && token.IsCancelled
					? RustManifold.MakeEmpty(RustStatus.Cancelled)
					: manifolds[0].Clone();
			}

			var leaves = new List<RustCsgNode>(manifolds.Count);

			foreach (var manifold in manifolds)
			{
				// Cloned, not aliased: the tree takes ownership of what it evaluates, and the
				// operands stay the caller's to use again afterwards.
				leaves.Add(new RustCsgLeaf(manifold.AsImpl().Clone()));
			}

			return RustManifold.FromImpl(new RustCsgOp(operationType, leaves).EvaluateWithToken(token));
		}

		/// <summary>
		/// Uploads a mesh, rejecting one the kernel could not accept.
		/// </summary>
		/// <remarks>
		/// Always the robust import. For strictly manifold input it is the plain import -
		/// same result, and the manifold is not marked soup, so the Auto engine still picks
		/// the fast exact pipeline. Closed but non-manifold input is welded into a soup
		/// manifold instead of being rejected. Only geometry that is not even closed still
		/// fails, as <see cref="RustStatus.NotClosed"/> - and that one gets a second chance
		/// through <see cref="WeldSeams"/> before it is refused.
		/// </remarks>
		/// <exception cref="MeshImportRejectedException">
		/// The mesh failed the kernel's validation. Left to surface rather than absorbed:
		/// a boolean swallows an error operand as empty geometry and still reports
		/// success, which would show up as a part silently missing from the output.
		/// </exception>
		internal static RustManifold Import(Mesh mesh, bool repairOrientation)
		{
			var imported = TryImport(mesh, repairOrientation, out var status, out var failureMessage);

			if (imported != null)
			{
				return imported;
			}

			if (status == RustStatus.NotClosed)
			{
				var welded = WeldSeams(mesh);

				if (welded != null)
				{
					var retried = TryImport(welded, repairOrientation, out _, out _);

					if (retried != null)
					{
						return retried;
					}
				}
			}

			throw new MeshImportRejectedException(failureMessage, status);
		}

		/// <summary>
		/// Uploads a mesh, handing back null and the status that explains it rather than
		/// throwing, so <see cref="Import"/> can decide whether the failure is worth a retry.
		/// </summary>
		/// <param name="status">
		/// <see cref="RustStatus.NoError"/> when the import succeeded, otherwise whatever
		/// the kernel objected to.
		/// </param>
		/// <param name="failureMessage">The message to throw with, or null on success.</param>
		private static RustManifold TryImport(Mesh mesh, bool repairOrientation, out RustStatus status, out string failureMessage)
		{
			var imported = RustManifold.FromMeshGL64Robust(ToRustMeshData(mesh));

			if (imported.Status() != RustStatus.NoError)
			{
				status = imported.Status();
				failureMessage = $"Manifold input has error status: {status} ({mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces)";
				return null;
			}

			if (!repairOrientation)
			{
				status = RustStatus.NoError;
				failureMessage = null;
				return imported;
			}

			// On the imported manifold rather than the mesh: the repair is a kernel operation
			// over the imported shells, and doing it here means every caller - colour split
			// included - gets it without repeating the check. A mesh that needs no repair
			// comes back as a plain copy, so this is safe unconditionally.
			var repaired = imported.RepairOrientation();

			if (repaired.Status() != RustStatus.NoError)
			{
				status = repaired.Status();
				failureMessage = $"Manifold orientation repair has error status: {status} ({mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces)";
				return null;
			}

			status = RustStatus.NoError;
			failureMessage = null;
			return repaired;
		}

		/// <summary>
		/// A tolerance-welded copy of a mesh, or null when there is no sane scale to weld at.
		/// </summary>
		/// <remarks>
		/// The kernel welds vertices by exact <c>f64</c> position and has no tolerance of its
		/// own, so a seam whose two sides differ in the last bits is a pair of boundary edges
		/// to it and a visually closed solid reports <see cref="RustStatus.NotClosed"/>.
		/// Meshes arrive that way routinely rather than exceptionally: positions are stored as
		/// <see cref="VectorMath.Vector3Float"/> and every transform on the way here re-rounds
		/// them, so a seam that was shared on disk can come apart in the last digit. Welding
		/// with a tolerance taken from the bounding box - both its size and how far it sits from
		/// the origin, so the tolerance means the same thing for a 1mm part and a 300mm one, and
		/// for a part at the origin and the same part moved across the bed - closes those seams
		/// without moving anything a user could see. Only ever called on the failure path, so a good mesh pays nothing for it.
		/// </remarks>
		private static Mesh WeldSeams(Mesh mesh)
		{
			var aabb = mesh.GetAxisAlignedBoundingBox();
			var diagonal = aabb.Size.Length;

			if (!(diagonal > 0) || double.IsInfinity(diagonal))
			{
				// No extent to scale a tolerance against. A non-finite vertex lands here too,
				// and welding is not the answer to that one anyway.
				return null;
			}

			// The part's own size is only half of what sets the scale of a seam gap. Positions are
			// stored as Vector3Float, so the rounding that splits a seam is a step of the float grid
			// at that absolute coordinate, not at the part's size: out at x = 5000mm consecutive
			// floats are ~4.9e-4mm apart, several times a tolerance scaled to a 10mm part's 17mm
			// diagonal - so the same part welds at the origin and is refused after being moved
			// across the bed. Whichever of the two is larger sets the tolerance.
			var distanceFromOrigin = Math.Max(MaxAbsComponent(aabb.MinXYZ), MaxAbsComponent(aabb.MaxXYZ));

			var tolerance = Math.Max(diagonal, distanceFromOrigin) * 1e-5;

			// Area rather than length, and well under the tolerance squared: this only drops
			// triangles the weld itself collapsed, not thin ones the model meant to have.
			var minFaceArea = tolerance * tolerance / 10;

			var welded = mesh.Copy(CancellationToken.None);
			welded.MergeVertices(tolerance, minFaceArea);
			welded.RemoveDegenerateFaces(minFaceArea);
			welded.RemoveUnusedVertices();

			return welded;
		}

		/// <summary>
		/// How far the furthest of a corner's three coordinates is from zero.
		/// </summary>
		private static double MaxAbsComponent(Vector3 corner)
		{
			return Math.Max(Math.Abs(corner.X), Math.Max(Math.Abs(corner.Y), Math.Abs(corner.Z)));
		}

		/// <summary>
		/// <see cref="Import"/>, re-tagged as an original so results derived from it report
		/// its <see cref="RustManifold.OriginalId()"/> in the run data.
		/// </summary>
		/// <inheritdoc cref="Import"/>
		private static RustManifold ImportAsOriginal(Mesh mesh, bool repairOrientation)
		{
			var imported = Import(mesh, repairOrientation);

			var asOriginal = imported.AsOriginal();

			if (asOriginal.Status() != RustStatus.NoError)
			{
				// A soup manifold - closed but non-manifold input - cannot be re-tagged as an
				// original; AsOriginal hands back an empty NotManifold manifold. Keep the
				// import instead. Its triangles then arrive in the result under whatever run
				// they inherit rather than one this operand owns, so it loses its face
				// colours - much better than losing the boolean.
				return imported;
			}

			return asOriginal;
		}

		/// <summary>
		/// Flattens a mesh into the interleaved position list and triangle index list
		/// the kernel takes. Positions widen to <c>double</c>, with none of the <c>float</c>
		/// narrowing the old C++ boundary imposed.
		/// </summary>
		/// <remarks>
		/// Three properties per vertex and nothing else set, which is what the retired
		/// binding's <c>FromMesh64Robust(vertProperties, triVerts)</c> built on the far side
		/// of the ABI: position only, no merge vectors, no runs, no tangents.
		/// </remarks>
		private static RustMeshGL64 ToRustMeshData(Mesh mesh)
		{
			var vertProperties = new List<double>(mesh.Vertices.Count * 3);
			for (int i = 0; i < mesh.Vertices.Count; i++)
			{
				var vertex = mesh.Vertices[i];
				vertProperties.Add(vertex.X);
				vertProperties.Add(vertex.Y);
				vertProperties.Add(vertex.Z);
			}

			// 64-bit indices because that is the width the robust import takes; the values
			// themselves are ordinary mesh vertex indices.
			var triVerts = new List<ulong>(mesh.Faces.Count * 3);
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				var face = mesh.Faces[i];
				triVerts.Add((ulong)face.v0);
				triVerts.Add((ulong)face.v1);
				triVerts.Add((ulong)face.v2);
			}

			return new RustMeshGL64
			{
				NumProp = 3,
				VertProperties = vertProperties,
				TriVerts = triVerts,
			};
		}

		/// <summary>
		/// Try to split a mesh with FaceColors into sub-manifolds by color group.
		/// Returns a single manifold (union of sub-manifolds) on success, or null if
		/// any color group doesn't form a valid manifold (e.g., from boolean results
		/// where color groups share boundaries).
		/// </summary>
		private static RustManifold TrySplitByFaceColorsRust(
			Mesh meshCopy,
			Dictionary<int, Color> originalIdToColor,
			CancellationToken cancellationToken,
			bool repairOrientation)
		{
			var subManifolds = new List<RustManifold>();

			try
			{
				var colorGroups = new Dictionary<Color, List<int>>();
				for (int faceIdx = 0; faceIdx < meshCopy.Faces.Count; faceIdx++)
				{
					var faceColor = faceIdx < meshCopy.FaceColors.Length
						? meshCopy.FaceColors[faceIdx]
						: Mesh.UnknownFaceColor;
					if (!colorGroups.TryGetValue(faceColor, out var faceList))
					{
						faceList = new List<int>();
						colorGroups[faceColor] = faceList;
					}

					faceList.Add(faceIdx);
				}

				foreach (var (color, faceIndices) in colorGroups)
				{
					// One sub-mesh build and one import per colour group, so this loop is
					// worth interrupting on its own rather than only at the union below.
					cancellationToken.ThrowIfCancellationRequested();

					var subMesh = new Mesh();
					var vertexMap = new Dictionary<int, int>();

					foreach (var faceIdx in faceIndices)
					{
						var face = meshCopy.Faces[faceIdx];
						int GetOrAddVertex(int origIdx)
						{
							if (!vertexMap.TryGetValue(origIdx, out int newIdx))
							{
								newIdx = subMesh.Vertices.Count;
								subMesh.Vertices.Add(meshCopy.Vertices[origIdx]);
								vertexMap[origIdx] = newIdx;
							}

							return newIdx;
						}

						subMesh.Faces.Add(new Face(
							GetOrAddVertex(face.v0),
							GetOrAddVertex(face.v1),
							GetOrAddVertex(face.v2),
							subMesh.Vertices));
					}

					// Check if sub-mesh is manifold before trying to create a Manifold
					if (!subMesh.IsManifold())
					{
						return null;
					}

					var subManifold = ImportAsOriginal(subMesh, repairOrientation);

					// Same -1 guard as ImportOperand: a colour group the re-tag would not take has no
					// ID to be keyed on, and letting every such group share the key -1 would hand one
					// group's colour to all of them.
					if (subManifold.OriginalId() >= 0)
					{
						originalIdToColor[subManifold.OriginalId()] = color;
					}

					subManifolds.Add(subManifold);
				}

				if (subManifolds.Count == 0)
				{
					return null;
				}

				if (subManifolds.Count == 1)
				{
					return subManifolds[0];
				}

				// Not AsOriginal: the union has to keep the sub-manifolds' boolean history,
				// because that history is what carries each colour group's OriginalId into
				// the run data of whatever this is later combined with.
				//
				// Cancellable, and on a heavily coloured model this union is most of the wall
				// time - one boolean per colour group before the real operation even starts.
				return BatchBoolean(subManifolds, RustOpType.Add, cancellationToken);
			}
			catch (OperationCanceledException)
			{
				// Above the general catch on purpose. Swallowing this would turn "the user
				// cancelled" into "the colours would not split", and the caller would go on to
				// re-import and re-combine the whole mesh against a token that is already
				// signalled.
				throw;
			}
			catch
			{
				return null;
			}
		}

		/// <summary>
		/// Extract per-face colors from a boolean result using its run data.
		/// Each run is a contiguous span of result triangles that came from one source
		/// mesh; the run's OriginalId says which. A source that had a single color paints
		/// its whole run, and one that had per-face colors is matched face by face
		/// through the nearest saved centroid.
		/// </summary>
		/// <remarks>
		/// The C++ engine needed raw P/Invoke and reflection into a private handle to
		/// reach these two arrays; here they are plain managed lists on
		/// <see cref="RustMeshGL64"/>.
		/// <para>
		/// Not every run can be traced back to an operand, and that is not a corner case.
		/// The robust engine - which <see cref="RustBooleanEngine.Auto"/> picks whenever an
		/// operand is non-manifold or self-intersecting, so for most scanned or downloaded
		/// parts - does not carry the operands' mesh relations through. What it produces
		/// arrives under a mesh ID that belongs to none of the operands.
		/// </para>
		/// <para>
		/// Such a run must never be painted <see cref="Mesh.UnknownFaceColor"/>. That grey is a
		/// colour nothing in the scene is wearing, so it does not read as "unknown" to the
		/// user - it reads as the part having turned grey. Instead: when nothing at all
		/// could be attributed the method returns null, leaving the mesh unpainted so the
		/// object's own colour shows, which is what a boolean looked like before per-face
		/// colours existed. When only some runs are unattributed the array still has to be
		/// filled, so those runs take the first operand's colour - the base being cut or
		/// unioned into, and the colour most of the body already has.
		/// </para>
		/// </remarks>
		/// <param name="meshColors">
		/// The per-operand colours the caller supplied, used only for the first-operand
		/// fallback above; null or empty when the caller had none.
		/// </param>
		private static Color[] ExtractFaceColorsFromRuns(
			RustMeshGL64 resultMeshGl,
			Mesh resultMesh,
			Dictionary<int, Color> originalIdToColor,
			Dictionary<int, List<(Vector3 centroid, Color color)>> originalIdToSpatialColors,
			Color[] meshColors)
		{
			var faceCount = resultMesh.Faces.Count;
			var runIndex = resultMeshGl.RunIndex;
			var runOriginalId = resultMeshGl.RunOriginalId;

			// RunIndex carries a trailing end sentinel, so a usable one is at least a
			// start and an end for a single run.
			if (runIndex.Count < 2 || runOriginalId.Count < 1)
			{
				return null;
			}

			var faceColors = new Color[faceCount];

			// A result none of whose runs name an operand carries no colour information at
			// all, and must not be painted as if it did - see the remarks above.
			bool anyRunAttributed = false;

			// What an unattributed run is painted when other runs did attribute. The first
			// operand rather than the kernel's grey: it is the base of the operation, so it
			// is both a colour the scene actually contains and the likeliest right answer.
			// The grey is only left for a caller that asked for colour tracking and then
			// supplied no colours at all - a contradiction no caller here can produce.
			var unattributedColor = meshColors?.Length > 0
				? meshColors[0]
				: Mesh.UnknownFaceColor;

			for (int runIdx = 0; runIdx < runOriginalId.Count; runIdx++)
			{
				int startTri = (int)(runIndex[runIdx] / 3);
				int endTri = (runIdx + 1 < runIndex.Count) ? (int)(runIndex[runIdx + 1] / 3) : faceCount;

				// OriginalId is signed on the manifold and unsigned in the run data; the
				// values are the same small non-negative IDs either way.
				int origId = unchecked((int)runOriginalId[runIdx]);

				// Check if this OriginalID has spatial face colors
				List<(Vector3 centroid, Color color)> spatialColors = null;
				originalIdToSpatialColors?.TryGetValue(origId, out spatialColors);

				if (spatialColors != null)
				{
					anyRunAttributed = true;

					// Match each result face to the nearest source face by centroid
					for (int tri = startTri; tri < endTri && tri < faceCount; tri++)
					{
						var face = resultMesh.Faces[tri];
						var centroid = new Vector3(
							(resultMesh.Vertices[face.v0]
							+ resultMesh.Vertices[face.v1]
							+ resultMesh.Vertices[face.v2]) / 3f);
						faceColors[tri] = Mesh.FindNearestCentroidColor(centroid, spatialColors);
					}
				}
				else
				{
					// Single color for this OriginalID
					bool known = originalIdToColor.TryGetValue(origId, out var color);
					anyRunAttributed |= known;

					if (!known)
					{
						color = unattributedColor;
					}

					for (int tri = startTri; tri < endTri && tri < faceCount; tri++)
					{
						faceColors[tri] = color;
					}
				}
			}

			return anyRunAttributed ? faceColors : null;
		}
	}
}
