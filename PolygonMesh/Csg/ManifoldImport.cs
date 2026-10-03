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
using MatterHackers.VectorMath;

// Kernel types are aliased rather than imported, as ManifoldKernel.cs explains.
using RustManifold = ManifoldSharp.Manifold;
using RustMeshGL64 = ManifoldSharp.MeshGL64;
using RustStatus = ManifoldSharp.Error;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Getting a <see cref="Mesh"/> into the kernel: the robust import, the tolerance weld that
	/// gives a not-quite-closed mesh a second chance, the re-tag that lets a result attribute its
	/// triangles back to an operand, and the flattening into the kernel's own mesh data.
	/// </summary>
	internal static class ManifoldImport
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
		static ManifoldImport()
		{
			ManifoldKernel.EnsureConfigured();
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
		internal static Mesh WeldSeams(Mesh mesh)
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
		internal static RustManifold ImportAsOriginal(Mesh mesh, bool repairOrientation)
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
	}
}
