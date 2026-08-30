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

// Aliased the same way ManifoldKernel.cs aliases them: ManifoldSharp.Manifold and
// MatterHackers.PolygonMesh.Mesh are both "the mesh type" at a glance, and the Rust
// prefix - ManifoldSharp is the C# port of manifold-rust - says which one a use site
// means.
using RustManifold = ManifoldSharp.Manifold;
using RustMeshGL64 = ManifoldSharp.MeshGL64;
using RustStatus = ManifoldSharp.Error;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Mesh repairs performed by the ManifoldSharp kernel, exposed to callers outside this
	/// assembly (the Repair design tool) that only want a repair and not a boolean.
	/// </summary>
	public static class MeshRepair
	{
		/// <summary>
		/// Rewinds inside-out shells so every body reads as solid material, using the
		/// ManifoldSharp kernel's exact shell-level orientation repair.
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
		/// it could not (empty input, an import or repair error status, or a kernel failure),
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

			// This is the one entry point into the kernel that does not go through
			// ManifoldKernel, so it is the one that has to ask for the kernel's process-global
			// configuration rather than getting it from a type initializer that has already
			// fired. Without it a Repair performed before any boolean in the session would run
			// on different settings than the same Repair performed after one - see
			// ManifoldKernel.EnsureConfigured for why that ordering is worth pinning.
			ManifoldKernel.EnsureConfigured();

			try
			{
				var vertProperties = new List<double>(sourceMesh.Vertices.Count * 3);
				for (int i = 0; i < sourceMesh.Vertices.Count; i++)
				{
					var vertex = sourceMesh.Vertices[i];
					vertProperties.Add(vertex.X);
					vertProperties.Add(vertex.Y);
					vertProperties.Add(vertex.Z);
				}

				// 64-bit indices because that is the width the robust import takes; the values
				// themselves are ordinary mesh vertex indices.
				var triVerts = new List<ulong>(sourceMesh.Faces.Count * 3);
				for (int i = 0; i < sourceMesh.Faces.Count; i++)
				{
					var face = sourceMesh.Faces[i];
					triVerts.Add((ulong)face.v0);
					triVerts.Add((ulong)face.v1);
					triVerts.Add((ulong)face.v2);
				}

				var imported = RustManifold.FromMeshGL64Robust(new RustMeshGL64
				{
					NumProp = 3,
					VertProperties = vertProperties,
					TriVerts = triVerts,
				});

				if (imported.Status() != RustStatus.NoError)
				{
					return false;
				}

				var repaired = imported.RepairOrientation();

				if (repaired.Status() != RustStatus.NoError)
				{
					return false;
				}

				// -1 is the export's "no property slot holds normals", which is what the retired
				// P/Invoke binding's parameterless GetMeshGL64 passed on every call.
				var result = ToMesh(repaired.GetMeshGL64(-1));

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
			catch (Exception)
			{
				// Every kernel failure mode is a reason to use the caller's fallback, not a
				// reason to fail the repair the user asked for. Narrower than it once was - the
				// kernel is managed now, so there is no library to be missing and no version
				// handshake to fail - but a rejected mesh still lands here.
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

			// The export promises at least x, y, z per vertex. Checked rather than assumed because
			// numProp is the loop stride below, and a zero would spin.
			if (numProp < 3)
			{
				return null;
			}

			var mesh = new Mesh();

			for (int i = 0; i + 2 < vertices.Count; i += numProp)
			{
				mesh.Vertices.Add(new VectorMath.Vector3(
					vertices[i],
					vertices[i + 1],
					vertices[i + 2]));
			}

			for (int i = 0; i + 2 < indices.Count; i += 3)
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
