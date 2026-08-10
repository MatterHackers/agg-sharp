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

// Aliased the same way ManifoldKernel.cs aliases them: ManifoldRust.Manifold and
// MatterHackers.PolygonMesh.Mesh are both "the mesh type" at a glance, and the Rust
// prefix says which one a use site means.
using RustManifold = ManifoldRust.Manifold;
using RustMeshGL64 = ManifoldRust.MeshGL64;
using RustStatus = ManifoldRust.ManifoldStatus;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Mesh repairs performed by the ManifoldRust kernel, exposed to callers outside this
	/// assembly (the Repair design tool) that only want a repair and not a boolean.
	/// </summary>
	public static class MeshRepair
	{
		/// <summary>
		/// Rewinds inside-out shells so every body reads as solid material, using the
		/// ManifoldRust kernel's exact shell-level orientation repair.
		/// </summary>
		/// <remarks>
		/// <para>
		/// The kernel decides per shell with exact predicates: outermost shells end up wound
		/// outward, legitimate cavity shells end up wound inward, and coincident or doubled
		/// sheets are left alone. That last point is a real behavioural difference from
		/// geometry3Sharp's MeshRepairOrientation, which flood-fills a consistent winding
		/// across each connected component and then picks a global sign: the kernel is
		/// deliberately conservative and never removes or reverses material it has no
		/// evidence about, so a single shell whose triangles disagree with each other comes
		/// back unchanged rather than guessed at.
		/// </para>
		/// <para>
		/// Import is the robust one, so closed-but-non-manifold soup is welded rather than
		/// rejected. Geometry that is not closed at all cannot be imported, and that is the
		/// main reason this returns false - the caller is expected to fall back to a
		/// tolerant repair rather than treat it as an error.
		/// </para>
		/// <para>
		/// Vertices may be welded and reindexed by the round trip, so anything the caller
		/// keyed on vertex or face index (per-face colours, for instance) has to be carried
		/// across by position rather than by index.
		/// </para>
		/// </remarks>
		/// <param name="sourceMesh">The mesh to repair. Left unmodified.</param>
		/// <param name="repairedMesh">The repaired copy, or null when this returns false.</param>
		/// <returns>
		/// True when the kernel accepted the mesh and produced a repaired copy; false when
		/// it could not (empty input, an import or repair error status, or a native failure),
		/// in which case the caller should use its own fallback.
		/// </returns>
		public static bool TryRepairOrientation(Mesh sourceMesh, out Mesh repairedMesh)
		{
			repairedMesh = null;

			if (sourceMesh == null
				|| sourceMesh.Faces.Count == 0
				|| sourceMesh.Vertices.Count == 0)
			{
				return false;
			}

			try
			{
				var vertProperties = new double[sourceMesh.Vertices.Count * 3];
				for (int i = 0; i < sourceMesh.Vertices.Count; i++)
				{
					var vertex = sourceMesh.Vertices[i];
					vertProperties[(i * 3) + 0] = vertex.X;
					vertProperties[(i * 3) + 1] = vertex.Y;
					vertProperties[(i * 3) + 2] = vertex.Z;
				}

				// 64-bit indices because that is the width the robust import takes; the values
				// themselves are ordinary mesh vertex indices.
				var triVerts = new ulong[sourceMesh.Faces.Count * 3];
				for (int i = 0; i < sourceMesh.Faces.Count; i++)
				{
					var face = sourceMesh.Faces[i];
					triVerts[(i * 3) + 0] = (ulong)face.v0;
					triVerts[(i * 3) + 1] = (ulong)face.v1;
					triVerts[(i * 3) + 2] = (ulong)face.v2;
				}

				using (var imported = RustManifold.FromMesh64Robust(vertProperties, triVerts))
				{
					if (imported.Status != RustStatus.NoError)
					{
						return false;
					}

					using (var repaired = imported.RepairOrientation())
					{
						if (repaired.Status != RustStatus.NoError)
						{
							return false;
						}

						var result = ToMesh(repaired.GetMeshGL64());

						// An empty result from a non-empty input means the kernel lost the solid.
						// Handing that back would silently delete the user's model, so it counts as
						// a failure and the caller falls back instead.
						if (result == null || result.Faces.Count == 0)
						{
							return false;
						}

						repairedMesh = result;
						return true;
					}
				}
			}
			catch (Exception)
			{
				// Every kernel failure mode - a rejected mesh, a version handshake failure, a
				// missing native library - is a reason to use the caller's fallback, not a reason
				// to fail the repair the user asked for.
				return false;
			}
		}

		/// <summary>
		/// Reads a kernel export back into a <see cref="Mesh"/>.
		/// </summary>
		private static Mesh ToMesh(RustMeshGL64 exported)
		{
			var numProp = (int)exported.NumProp;
			var vertices = exported.VertProperties;
			var indices = exported.TriVerts;

			// The ABI promises at least x, y, z per vertex. Checked rather than assumed because
			// numProp is the loop stride below, and a zero would spin.
			if (numProp < 3)
			{
				return null;
			}

			var mesh = new Mesh();

			for (int i = 0; i + 2 < vertices.Length; i += numProp)
			{
				mesh.Vertices.Add(new VectorMath.Vector3(
					vertices[i],
					vertices[i + 1],
					vertices[i + 2]));
			}

			for (int i = 0; i + 2 < indices.Length; i += 3)
			{
				mesh.Faces.Add(new Face(
					(int)indices[i],
					(int)indices[i + 1],
					(int)indices[i + 2],
					mesh.Vertices));
			}

			return mesh;
		}
	}
}
