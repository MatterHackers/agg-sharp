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
using RustManifold = ManifoldRust.Manifold;
using RustMeshGL64 = ManifoldRust.MeshGL64;
using RustOpType = ManifoldRust.ManifoldOpType;
using RustStatus = ManifoldRust.ManifoldStatus;

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
		/// Perform a boolean operation via the ManifoldRust native library. Every failure
		/// surfaces as a managed exception so the caller can fall back to CsgBySlicing,
		/// except cancellation, which the caller deliberately lets through.
		/// </summary>
		/// <remarks>
		/// Internal rather than private only so the tests can watch it reject an input
		/// directly. <see cref="DoArray"/> is the entry point everything else uses.
		/// </remarks>
		internal static Mesh DoArrayViaManifoldRust(
			IEnumerable<(Mesh mesh, Matrix4X4 matrix)> items,
			CsgModes operation,
			CancellationToken cancellationToken,
			Action<double, string> reporter,
			double amountPerOperation,
			double ratioCompleted,
			Color[] meshColors)
		{
			// Claimed on entry rather than on success: if this throws, the caller's fallback
			// overwrites it, so the value always names whichever engine actually returned.
			LastBackendUsed = BackendManifoldRust;

			bool trackColors = meshColors != null;

			var manifolds = new List<RustManifold>();
			var originalIdToColor = new Dictionary<int, Color>();
			var originalIdToSpatialColors = new Dictionary<int, List<(Vector3, Color)>>();

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

					RustManifold manifold = null;

					if (trackColors && meshCopy.FaceColors != null)
					{
						manifold = TrySplitByFaceColorsRust(meshCopy, originalIdToColor, cancellationToken);
					}

					if (manifold == null)
					{
						if (trackColors)
						{
							// AsOriginal is what gives the input an OriginalId, and the run data
							// that carries colours back is keyed on that. Without colours the run
							// data is never read, so the extra native copy would be pure waste.
							manifold = ImportAsOriginal(meshCopy);

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
						}
						else
						{
							manifold = Import(meshCopy);
						}
					}

					manifolds.Add(manifold);
					meshIndex++;
				}

				return CombineAndRead(
					manifolds,
					operation,
					cancellationToken,
					trackColors,
					originalIdToColor,
					originalIdToSpatialColors);
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
		/// Runs the n-ary boolean over already-imported operands and reads the result
		/// back into a <see cref="Mesh"/>.
		/// </summary>
		private static Mesh CombineAndRead(
			List<RustManifold> manifolds,
			CsgModes operation,
			CancellationToken cancellationToken,
			bool trackColors,
			Dictionary<int, Color> originalIdToColor,
			Dictionary<int, List<(Vector3, Color)>> originalIdToSpatialColors)
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
			RustManifold boolResult = ownsResult
				? RustManifold.BatchBoolean(manifolds, operationType, cancellationToken)
				: manifolds[0];

			try
			{
				if (boolResult.Status != RustStatus.NoError)
				{
					// Exporting an error manifold is safe here - unlike the C++ engine, which
					// could fault the CLR doing it - but an error status still means the kernel
					// could not build the solid, and CsgBySlicing may yet manage it.
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
		/// Uploads a mesh, rejecting one the kernel could not accept.
		/// </summary>
		/// <exception cref="InvalidOperationException">
		/// The mesh failed the kernel's validation. Left to surface rather than absorbed:
		/// a boolean swallows an error operand as empty geometry and still reports
		/// success, which would show up as a part silently missing from the output.
		/// </exception>
		private static RustManifold Import(Mesh mesh)
		{
			var (vertProperties, triVerts) = ToRustMeshData(mesh);

			var imported = RustManifold.FromMesh64(vertProperties, triVerts);
			try
			{
				if (imported.Status != RustStatus.NoError)
				{
					throw new InvalidOperationException(
						$"Manifold input has error status: {imported.Status} ({mesh.Vertices.Count} vertices, {mesh.Faces.Count} faces)");
				}

				return imported;
			}
			catch
			{
				imported.Dispose();
				throw;
			}
		}

		/// <summary>
		/// <see cref="Import"/>, re-tagged as an original so results derived from it report
		/// its <see cref="RustManifold.OriginalId"/> in the run data.
		/// </summary>
		/// <inheritdoc cref="Import"/>
		private static RustManifold ImportAsOriginal(Mesh mesh)
		{
			// AsOriginal returns a new manifold, so the import it was called on is a
			// separate handle that still has to be released.
			using (var imported = Import(mesh))
			{
				return imported.AsOriginal();
			}
		}

		/// <summary>
		/// Flattens a mesh into the interleaved position array and triangle index array
		/// the kernel takes. Positions widen to <c>double</c>, with none of the <c>float</c>
		/// narrowing the old C++ boundary imposed.
		/// </summary>
		private static (double[] vertProperties, uint[] triVerts) ToRustMeshData(Mesh mesh)
		{
			var vertProperties = new double[mesh.Vertices.Count * 3];
			for (int i = 0; i < mesh.Vertices.Count; i++)
			{
				var vertex = mesh.Vertices[i];
				vertProperties[(i * 3) + 0] = vertex.X;
				vertProperties[(i * 3) + 1] = vertex.Y;
				vertProperties[(i * 3) + 2] = vertex.Z;
			}

			var triVerts = new uint[mesh.Faces.Count * 3];
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				var face = mesh.Faces[i];
				triVerts[(i * 3) + 0] = (uint)face.v0;
				triVerts[(i * 3) + 1] = (uint)face.v1;
				triVerts[(i * 3) + 2] = (uint)face.v2;
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
			CancellationToken cancellationToken)
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

					var subManifold = ImportAsOriginal(subMesh);
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
