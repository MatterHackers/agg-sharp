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
using MatterHackers.Agg;
using MatterHackers.VectorMath;

// Every type from the package is aliased rather than reached through its namespace:
// ManifoldRust.Manifold and MatterHackers.PolygonMesh.Mesh are both "the mesh type"
// at a glance, and the Rust prefix says which one a use site means.
using RustBooleanEngine = ManifoldRust.BooleanEngine;
using RustManifold = ManifoldRust.Manifold;
using RustMeshGL64 = ManifoldRust.MeshGL64;
using RustOpType = ManifoldRust.ManifoldOpType;
using RustStatus = ManifoldRust.ManifoldStatus;
using RustWindingRule = ManifoldRust.WindingRule;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// The ManifoldRust boolean backend: the native kernel
	/// <see cref="BooleanProcessing.DoArray"/> runs every polygon-mode boolean through.
	/// </summary>
	/// <remarks>
	/// Kept in its own file only to keep each half of the partial class a readable
	/// length; there is no second native engine to sit beside any more.
	/// <para>
	/// Three things it does that the C++ ManifoldNET engine it replaced could not, and
	/// which are why the migration happened: coordinates upload as <c>double</c> rather
	/// than being narrowed to <c>float</c> at the boundary, the run data needed for face
	/// colours comes back as ordinary managed arrays (no raw P/Invoke and no reflection
	/// into a private handle field), and the caller's <see cref="CancellationToken"/>
	/// actually reaches the kernel.
	/// </para>
	/// </remarks>
	public static partial class BooleanProcessing
	{
		private static readonly Color DefaultFaceColor = new Color(200, 200, 200, 255);

		/// <summary>
		/// Selects the kernel's boolean engine, once, before the first boolean runs.
		/// </summary>
		/// <remarks>
		/// The setting is process-global on the native side, so it is done in a static
		/// constructor rather than per call. <see cref="RustBooleanEngine.Auto"/> keeps the
		/// fast exact pipeline for strictly manifold operands and only pays for the slower
		/// rational-arithmetic engine when an operand came in through the robust import as
		/// non-manifold soup.
		/// </remarks>
		static BooleanProcessing()
		{
			try
			{
				RustManifold.DefaultBooleanEngine = RustBooleanEngine.Auto;
			}
			catch
			{
				// A static constructor that throws poisons the whole type: every later member
				// access - including the implicit-surface path, which never touches the kernel -
				// would get a cached TypeInitializationException naming the wrong problem. If the
				// native library cannot load or the version handshake fails, the per-call Import
				// throws instead, which says what actually went wrong. A failed engine selection
				// simply leaves the kernel's default Exact behaviour in place.
			}
		}

		/// <summary>
		/// Perform a boolean operation via the ManifoldRust native library. Every failure
		/// surfaces as a managed exception - including cancellation, as
		/// <see cref="OperationCanceledException"/> - and <see cref="DoArray"/> passes them
		/// all straight to the caller.
		/// </summary>
		/// <remarks>
		/// Internal rather than private only so the tests can watch it reject an input
		/// directly. <see cref="DoArray"/> is the entry point everything else uses.
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
		internal static Mesh DoArrayViaManifoldRust(
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
			bool trackColors = meshColors != null;

			var manifolds = new List<RustManifold>();
			var originalIdToColor = new Dictionary<int, Color>();
			var originalIdToSpatialColors = new Dictionary<int, List<(Vector3, Color)>>();

			// A union is the one operation where leaving an operand out still has an answer:
			// the other operands' union. Subtract and Intersect are defined by every operand,
			// so dropping one of those would silently change what the operation means.
			bool skipRefusedOperands = operation == CsgModes.Union;
			var skipped = new List<SkippedBooleanOperand>();

			try
			{
				int meshIndex = 0;
				foreach (var (mesh, matrix) in items)
				{
					// Before the copy and the upload, not just before the boolean: importing a
					// large set is itself seconds of work, and an already-cancelled caller should
					// not pay for N mesh copies and N native imports it is going to throw away.
					cancellationToken.ThrowIfCancellationRequested();

					if (mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
					{
						if (operation == CsgModes.Intersect)
						{
							return new Mesh();
						}

						if (meshIndex == 0 && operation == CsgModes.Subtract)
						{
							return new Mesh();
						}

						meshIndex++;
						continue;
					}

					var meshCopy = mesh.Copy(CancellationToken.None);
					meshCopy.Transform(matrix);

					RustManifold manifold;

					try
					{
						manifold = ImportOperand(
							meshCopy, meshIndex, trackColors, meshColors, originalIdToColor, originalIdToSpatialColors, cancellationToken, repairOrientation);
					}
					catch (MeshImportRejectedException refused) when (skipRefusedOperands)
					{
						// Only the kernel's verdict on this operand's geometry is skippable. A
						// failure from anywhere else in the import - the native library, a handle -
						// propagates, because degrading on it would tell the user to Repair a part
						// that has nothing wrong with it.
						// Not swallowed: the throw below - or the exception the partial result is
						// carried out on - names every operand that landed here.
						skipped.Add(new SkippedBooleanOperand(meshIndex, refused.Message));
						meshIndex++;
						continue;
					}

					manifolds.Add(manifold);
					meshIndex++;
				}

				if (skipped.Count > 0 && manifolds.Count == 0)
				{
					// Every operand refused. The union of nothing is not geometry, but it is still
					// the partial answer rather than a different kind of failure: a caller combining
					// several touching sets has to be able to keep the sets that worked and keep
					// these parts visible, and a plain InvalidOperationException here would take the
					// whole build down with them. Callers that do not handle the partial case see
					// the same InvalidOperationException they always did, naming every operand.
					throw new PartialBooleanException(DescribeSkipped(skipped, meshIndex), new Mesh(), skipped);
				}

				var result = CombineAndRead(
					manifolds,
					operation,
					cancellationToken,
					trackColors,
					originalIdToColor,
					originalIdToSpatialColors,
					windingRule,
					reporter,
					amountPerOperation,
					ratioCompleted);

				if (skipped.Count > 0)
				{
					throw new PartialBooleanException(DescribeSkipped(skipped, meshIndex), result, skipped);
				}

				return result;
			}
			finally
			{
				// Deterministic rather than left to the finalizer: a boolean over large
				// geometry can hold a lot of native memory, and the next one may start
				// before a collection would have run.
				foreach (var manifold in manifolds)
				{
					manifold.Dispose();
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
			// data is never read, so the extra native copy would be pure waste.
			var manifold = ImportAsOriginal(meshCopy, repairOrientation);

			if (meshCopy.FaceColors != null)
			{
				originalIdToSpatialColors[manifold.OriginalId] = meshCopy.SaveFaceCentroidColors();
			}
			else
			{
				var color = meshIndex < meshColors.Length
					? meshColors[meshIndex]
					: DefaultFaceColor;
				originalIdToColor[manifold.OriginalId] = color;
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
			List<RustManifold> manifolds,
			CsgModes operation,
			CancellationToken cancellationToken,
			bool trackColors,
			Dictionary<int, Color> originalIdToColor,
			Dictionary<int, List<(Vector3, Color)>> originalIdToSpatialColors,
			RustWindingRule windingRule,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted)
		{
			var resultMesh = new Mesh();

			if (manifolds.Count == 0)
			{
				return resultMesh;
			}

			var operationType = RustOpType.Add;

			if (operation == CsgModes.Subtract)
			{
				operationType = RustOpType.Subtract;
			}
			else if (operation == CsgModes.Intersect)
			{
				operationType = RustOpType.Intersect;
			}

			// A single operand is its own answer; running a one-operand boolean would
			// only cost a copy. It is also the one case where the result is borrowed from
			// the operand list, so it must not be disposed here.
			bool ownsResult = manifolds.Count > 1;

			// BatchBoolean runs the kernel's CSG tree, which is what makes a large n-ary
			// union tractable, but it reports no progress and takes no winding rule -
			// it reads the process-global engine and the default rule. So it stays the
			// path whenever neither is asked for, and asking for either drops to a
			// pairwise left fold over the explicit binary entry point.
			bool needsExplicitBoolean = reporter != null || windingRule != RustWindingRule.Positive;

			RustManifold boolResult;
			if (!ownsResult)
			{
				boolResult = manifolds[0];
			}
			else if (needsExplicitBoolean)
			{
				boolResult = CombinePairwise(
					manifolds, operationType, windingRule, cancellationToken, reporter, amountPerOperation, ratioCompleted);
			}
			else
			{
				boolResult = RustManifold.BatchBoolean(manifolds, operationType, cancellationToken);
			}

			try
			{
				if (boolResult.Status != RustStatus.NoError)
				{
					// Exporting an error manifold is safe here - unlike the C++ engine, which
					// could fault the CLR doing it - but an error status still means the kernel
					// could not build the solid, and a half-built one is worse than a failure.
					throw new InvalidOperationException($"Manifold boolean result has error status: {boolResult.Status}");
				}

				var result = boolResult.GetMeshGL64();
				var resultNumProp = (int)result.NumProp;
				var vertices = result.VertProperties;
				var indices = result.TriVerts;

				// The ABI promises at least x, y, z per vertex. Checked rather than assumed
				// because resultNumProp is the loop stride below, and a zero would spin.
				if (resultNumProp < 3)
				{
					throw new InvalidOperationException($"Manifold boolean result has {resultNumProp} properties per vertex, expected at least 3");
				}

				for (int i = 0; i + 2 < vertices.Length; i += resultNumProp)
				{
					resultMesh.Vertices.Add(new Vector3(
						vertices[i],
						vertices[i + 1],
						vertices[i + 2]));
				}

				for (int i = 0; i + 2 < indices.Length; i += 3)
				{
					resultMesh.Faces.Add(new Face(
						(int)indices[i],
						(int)indices[i + 1],
						(int)indices[i + 2],
						resultMesh.Vertices));
				}

				if (trackColors && resultMesh.Faces.Count > 0)
				{
					var faceColors = ExtractFaceColorsFromRuns(
						result, resultMesh, originalIdToColor, originalIdToSpatialColors);
					if (faceColors != null)
					{
						resultMesh.FaceColors = faceColors;
					}
				}

				return resultMesh;
			}
			finally
			{
				if (ownsResult)
				{
					boolResult.Dispose();
				}
			}
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

			// Only ever holds an intermediate this method created; manifolds[0] is the
			// caller's and is never assigned here, so the catch below cannot free it.
			RustManifold accumulated = null;

			try
			{
				for (int i = 1; i < manifolds.Count; i++)
				{
					var next = RustManifold.Boolean(
						accumulated ?? manifolds[0],
						manifolds[i],
						operationType,
						RustBooleanEngine.Auto,
						windingRule,
						progress,
						cancellationToken);

					accumulated?.Dispose();
					accumulated = next;

					progress?.CompleteOperation(CombineCompletePhase);
				}

				return accumulated;
			}
			catch
			{
				accumulated?.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Message phase for the boundary between two pairwise booleans, where the
		/// kernel itself has nothing to report because no operation is running.
		/// </summary>
		private const string CombineCompletePhase = "combining";

		/// <summary>
		/// Uploads a mesh, rejecting one the kernel could not accept.
		/// </summary>
		/// <remarks>
		/// Always the robust import. For strictly manifold input it is the plain import -
		/// same result, and the handle is not marked soup, so the Auto engine still picks
		/// the fast exact pipeline. Closed but non-manifold input is welded into a soup
		/// handle instead of being rejected. Only geometry that is not even closed still
		/// fails, as <see cref="RustStatus.NotClosed"/> - and that one gets a second chance
		/// through <see cref="WeldSeams"/> before it is refused.
		/// </remarks>
		/// <exception cref="MeshImportRejectedException">
		/// The mesh failed the kernel's validation. Left to surface rather than absorbed:
		/// a boolean swallows an error operand as empty geometry and still reports
		/// success, which would show up as a part silently missing from the output.
		/// </exception>
		private static RustManifold Import(Mesh mesh, bool repairOrientation)
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
		/// What the kernel makes of a mesh offered to it as a boolean operand: whether it would
		/// take it at all, and if so whether the solid intersects itself.
		/// </summary>
		/// <remarks>
		/// Both halves of the answer come from one import, because both are properties of the same
		/// upload and a caller that asked them separately would pay for the mesh twice.
		/// <para>
		/// Exposed because self-intersecting operands are the difference between a union that
		/// finishes and one that does not: they force the kernel off its exact pipeline onto the
		/// robust one, and on a set of large hole-filled meshes that has been measured in tens of
		/// minutes with no end. A caller that has a choice about which operands to hand over needs
		/// to be able to ask first.
		/// </para>
		/// <para>
		/// The import is the same one <see cref="DoArrayViaManifoldRust"/> performs - transform
		/// first, weld retry included - so the verdict is about the manifold the boolean would
		/// actually see and not about some other reading of the same triangles.
		/// </para>
		/// </remarks>
		/// <param name="matrix">
		/// The operand's transform, applied before the import exactly as the boolean applies it. It
		/// can change the answer on its own: a mirroring matrix turns every triangle inside out.
		/// </param>
		/// <param name="repairOrientation">
		/// Import with the same shell-orientation repair the boolean would use, since that changes
		/// which triangles the kernel ends up scanning.
		/// </param>
		public static BooleanOperandVerdict ClassifyBooleanOperand(Mesh mesh, Matrix4X4 matrix, bool repairOrientation = false)
		{
			if (mesh == null
				|| mesh.Faces.Count == 0
				|| mesh.Vertices.Count == 0)
			{
				return BooleanOperandVerdict.Refused;
			}

			try
			{
				var meshCopy = mesh.Copy(CancellationToken.None);
				meshCopy.Transform(matrix);

				using (var imported = Import(meshCopy, repairOrientation))
				{
					return imported.HasSelfIntersections
						? BooleanOperandVerdict.SelfIntersecting
						: BooleanOperandVerdict.Clean;
				}
			}
			catch (Exception)
			{
				// Including the native failures: a mesh the kernel cannot be made to hold is one it
				// would refuse as an operand too, and that is the only thing the caller is asking.
				return BooleanOperandVerdict.Refused;
			}
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
			var (vertProperties, triVerts) = ToRustMeshData(mesh);

			var imported = RustManifold.FromMesh64Robust(vertProperties, triVerts);
			try
			{
				if (imported.Status != RustStatus.NoError)
				{
					status = imported.Status;
					failureMessage = $"Manifold input has error status: {status} ({mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces)";
					imported.Dispose();
					return null;
				}

				if (!repairOrientation)
				{
					status = RustStatus.NoError;
					failureMessage = null;
					return imported;
				}

				// On the handle rather than the mesh: the repair is a kernel operation over
				// the imported shells, and doing it here means every caller - colour split
				// included - gets it without repeating the check. A mesh that needs no
				// repair comes back as a plain copy, so this is safe unconditionally.
				var repaired = imported.RepairOrientation();

				try
				{
					if (repaired.Status != RustStatus.NoError)
					{
						status = repaired.Status;
						failureMessage = $"Manifold orientation repair has error status: {status} ({mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces)";
						repaired.Dispose();
						imported.Dispose();
						return null;
					}
				}
				catch
				{
					repaired.Dispose();
					throw;
				}

				imported.Dispose();
				status = RustStatus.NoError;
				failureMessage = null;
				return repaired;
			}
			catch
			{
				imported.Dispose();
				throw;
			}
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
		/// its <see cref="RustManifold.OriginalId"/> in the run data.
		/// </summary>
		/// <inheritdoc cref="Import"/>
		private static RustManifold ImportAsOriginal(Mesh mesh, bool repairOrientation)
		{
			var imported = Import(mesh, repairOrientation);

			try
			{
				var asOriginal = imported.AsOriginal();

				try
				{
					// Status itself can throw, so the new handle needs its own guard - without it
					// asOriginal would be left to the finalizer on that path.
					if (asOriginal.Status != RustStatus.NoError)
					{
						// A soup handle - closed but non-manifold input - cannot be re-tagged as an
						// original; AsOriginal hands back an empty NotManifold manifold. Keep the
						// import instead. Its triangles then arrive in the result under whatever run
						// they inherit rather than one this operand owns, so it loses its face
						// colours - much better than losing the boolean.
						asOriginal.Dispose();
						return imported;
					}
				}
				catch
				{
					asOriginal.Dispose();
					throw;
				}

				// AsOriginal returns a new manifold, so the import it was called on is a
				// separate handle that still has to be released.
				imported.Dispose();
				return asOriginal;
			}
			catch
			{
				imported.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Flattens a mesh into the interleaved position array and triangle index array
		/// the kernel takes. Positions widen to <c>double</c>, with none of the <c>float</c>
		/// narrowing the old C++ boundary imposed.
		/// </summary>
		private static (double[] vertProperties, ulong[] triVerts) ToRustMeshData(Mesh mesh)
		{
			var vertProperties = new double[mesh.Vertices.Count * 3];
			for (int i = 0; i < mesh.Vertices.Count; i++)
			{
				var vertex = mesh.Vertices[i];
				vertProperties[(i * 3) + 0] = vertex.X;
				vertProperties[(i * 3) + 1] = vertex.Y;
				vertProperties[(i * 3) + 2] = vertex.Z;
			}

			// 64-bit indices because that is the width the robust import takes; the values
			// themselves are ordinary mesh vertex indices.
			var triVerts = new ulong[mesh.Faces.Count * 3];
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				var face = mesh.Faces[i];
				triVerts[(i * 3) + 0] = (ulong)face.v0;
				triVerts[(i * 3) + 1] = (ulong)face.v1;
				triVerts[(i * 3) + 2] = (ulong)face.v2;
			}

			return (vertProperties, triVerts);
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
			bool keepSubManifolds = false;

			try
			{
				var colorGroups = new Dictionary<Color, List<int>>();
				for (int faceIdx = 0; faceIdx < meshCopy.Faces.Count; faceIdx++)
				{
					var faceColor = faceIdx < meshCopy.FaceColors.Length
						? meshCopy.FaceColors[faceIdx]
						: DefaultFaceColor;
					if (!colorGroups.TryGetValue(faceColor, out var faceList))
					{
						faceList = new List<int>();
						colorGroups[faceColor] = faceList;
					}

					faceList.Add(faceIdx);
				}

				foreach (var (color, faceIndices) in colorGroups)
				{
					// One sub-mesh build and one native import per colour group, so this loop is
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
					originalIdToColor[subManifold.OriginalId] = color;
					subManifolds.Add(subManifold);
				}

				if (subManifolds.Count == 0)
				{
					return null;
				}

				if (subManifolds.Count == 1)
				{
					keepSubManifolds = true;
					return subManifolds[0];
				}

				// Not AsOriginal: the union has to keep the sub-manifolds' boolean history,
				// because that history is what carries each colour group's OriginalId into
				// the run data of whatever this is later combined with.
				//
				// Cancellable, and on a heavily coloured model this union is most of the wall
				// time - one boolean per colour group before the real operation even starts.
				return RustManifold.BatchBoolean(subManifolds, RustOpType.Add, cancellationToken);
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
			finally
			{
				if (!keepSubManifolds)
				{
					foreach (var subManifold in subManifolds)
					{
						subManifold.Dispose();
					}
				}
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
		/// reach these two arrays; here they are plain managed arrays on
		/// <see cref="RustMeshGL64"/>.
		/// </remarks>
		private static Color[] ExtractFaceColorsFromRuns(
			RustMeshGL64 resultMeshGl,
			Mesh resultMesh,
			Dictionary<int, Color> originalIdToColor,
			Dictionary<int, List<(Vector3 centroid, Color color)>> originalIdToSpatialColors)
		{
			var faceCount = resultMesh.Faces.Count;
			var runIndex = resultMeshGl.RunIndex;
			var runOriginalId = resultMeshGl.RunOriginalId;

			// RunIndex carries a trailing end sentinel, so a usable one is at least a
			// start and an end for a single run.
			if (runIndex.Length < 2 || runOriginalId.Length < 1)
			{
				return null;
			}

			var faceColors = new Color[faceCount];

			for (int runIdx = 0; runIdx < runOriginalId.Length; runIdx++)
			{
				int startTri = (int)(runIndex[runIdx] / 3);
				int endTri = (runIdx + 1 < runIndex.Length) ? (int)(runIndex[runIdx + 1] / 3) : faceCount;

				// OriginalId is signed on the manifold and unsigned in the run data; the
				// values are the same small non-negative IDs either way.
				int origId = unchecked((int)runOriginalId[runIdx]);

				// Check if this OriginalID has spatial face colors
				List<(Vector3 centroid, Color color)> spatialColors = null;
				originalIdToSpatialColors?.TryGetValue(origId, out spatialColors);

				if (spatialColors != null)
				{
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
					var color = originalIdToColor.TryGetValue(origId, out var c) ? c : DefaultFaceColor;
					for (int tri = startTri; tri < endTri && tri < faceCount; tri++)
					{
						faceColors[tri] = color;
					}
				}
			}

			return faceColors;
		}
	}
}
