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

// Kernel types are aliased rather than imported, as ManifoldKernel.cs explains.
using RustManifold = ManifoldSharp.Manifold;
using RustOpType = ManifoldSharp.OpType;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// The operands of one boolean as the kernel holds them: the imported manifolds, the
	/// colour bookkeeping their result is painted from, and the ones the kernel would not
	/// take.
	/// </summary>
	/// <remarks>
	/// Its own type because <see cref="ManifoldKernel.RunBoolean"/> and <see cref="ManifoldKernel.RunBooleanAsync"/> both walk
	/// the same operand list, and what a refused or an empty operand means is subtle enough that
	/// two copies of it would drift. The loop is the only thing that differs between them - one
	/// of them can hand the UI its thread back between operands - so the loop is all either of
	/// them writes.
	/// </remarks>
	internal sealed class ManifoldOperandBatch
	{
		private readonly CsgModes operation;
		private readonly Color[] meshColors;
		private readonly bool repairOrientation;

		// A union is the one operation where leaving an operand out still has an answer:
		// the other operands' union. Subtract and Intersect are defined by every operand,
		// so dropping one of those would silently change what the operation means.
		private readonly bool skipRefusedOperands;

		internal ManifoldOperandBatch(CsgModes operation, Color[] meshColors, bool repairOrientation)
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
				return ManifoldImport.Import(meshCopy, repairOrientation);
			}

			// AsOriginal is what gives the input an OriginalId, and the run data
			// that carries colours back is keyed on that. Without colours the run
			// data is never read, so the extra copy would be pure waste.
			var manifold = ManifoldImport.ImportAsOriginal(meshCopy, repairOrientation);

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

					var subManifold = ManifoldImport.ImportAsOriginal(subMesh, repairOrientation);

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
				ManifoldKernelCallCounts.CountColorGroupUnion();
				return ManifoldCancellableBoolean.BatchBoolean(subManifolds, RustOpType.Add, cancellationToken);
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
	}
}
