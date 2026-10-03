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
using MatterHackers.Agg;
using MatterHackers.VectorMath;

// Kernel types are aliased rather than imported, as ManifoldKernel.cs explains.
using RustBooleanEngine = ManifoldSharp.BooleanEngine;
using RustManifold = ManifoldSharp.Manifold;
using RustMeshGL64 = ManifoldSharp.MeshGL64;
using RustStatus = ManifoldSharp.Error;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Getting a finished kernel result back out as a <see cref="Mesh"/>: refusing an errored
	/// one, rebuilding the vertex and face lists from the export, and - for a boolean whose
	/// caller tracks colours - painting each face from the run data.
	/// </summary>
	internal static class ManifoldResultReader
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
		static ManifoldResultReader()
		{
			ManifoldKernel.EnsureConfigured();
		}

		/// <summary>
		/// Reads a finished boolean back out of the kernel as a <see cref="Mesh"/>, painting its
		/// faces from the run data when the caller asked for colour tracking.
		/// </summary>
		internal static Mesh ReadResult(RustManifold boolResult, ManifoldOperandBatch batch)
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
