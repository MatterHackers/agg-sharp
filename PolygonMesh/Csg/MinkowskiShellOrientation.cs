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
using System.Threading;
using RustCancelToken = ManifoldSharp.CancelToken;
using RustManifold = ManifoldSharp.Manifold;
using RustStatus = ManifoldSharp.Error;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Rewinds a Minkowski operand's shells whose winding contradicts their nesting, so a part
	/// saved inside out dilates and erodes as the solid it looks like.
	/// </summary>
	internal static class MinkowskiShellOrientation
	{
		/// <summary>
		/// The operand with every mis-wound shell rewound, or the operand itself when no shell
		/// can be.
		/// </summary>
		/// <remarks>
		/// The kernel's repair (<c>Repair.PlanRepair</c>) flips an outer shell wound inward, and a
		/// shell inside it only when the whole nesting stack arrived mirrored. A correctly wound
		/// cavity - an inward shell inside an outward one - is what its nesting demands and is left
		/// alone, as is an outward shell nested in a correct solid (it may be a deliberate second
		/// body). After the import rather than on the mesh so every import path, soup join
		/// included, gets it.
		/// <para>
		/// The repair costs about shells x triangles and reports no progress, so it runs only when
		/// a shell is inverted (a cavity counts): 0.4-1.2 s on a 245k-triangle sphere otherwise,
		/// with the bar at 0. It polls the token once per shell, so Cancel still works.
		/// </para>
		/// </remarks>
		public static RustManifold Repair(RustManifold imported, CancellationToken cancellationToken)
		{
			if (!MayHaveInvertedShell(imported))
			{
				return imported;
			}

			// A token that can never be signalled allocates nothing, as in Morph: CancelToken
			// registers on the caller's source for good, so it is made per operation.
			var token = cancellationToken.CanBeCanceled
				? new RustCancelToken(cancellationToken)
				: null;
			var repaired = imported.RepairOrientationWithToken(token);
			if (repaired.Status() == RustStatus.Cancelled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			// The repair only flips triangles, so it has no reason to fail on an import that
			// succeeded; if it ever does, the operand as imported is the better answer than none.
			return repaired.Status() == RustStatus.NoError ? repaired : imported;
		}

		/// <summary>
		/// False only when every edge-connected shell of <paramref name="manifold"/> encloses a
		/// positive volume - the case in which the repair provably changes nothing. O(triangles).
		/// </summary>
		/// <remarks>
		/// PlanRepair flips a shell in exactly two cases: its sign is -1 at even depth, or it is +1
		/// at odd depth under a container whose own sign is -1 at even depth. Both need a shell
		/// whose sign is -1, so a mesh with none is a no-op. A shell's sign is its exact
		/// ray-cast orientation, which for a closed shell that does not cross itself is the sign of
		/// its enclosed volume. Shells here are joined through paired halfedges, never coarser than
		/// PlanRepair's position-welded edges, and a sum of positive volumes is positive, so a
		/// merged shell there is positive too. A soup import has no pairing to walk and always
		/// takes the repair.
		/// </remarks>
		internal static bool MayHaveInvertedShell(RustManifold manifold)
		{
			var impl = manifold.AsImpl();
			if (impl.IsSoup)
			{
				return true;
			}

			var halfedges = impl.Halfedge;
			var positions = impl.VertPos;
			int triangles = halfedges.Count / 3;
			var parent = new int[triangles];
			for (int t = 0; t < triangles; t++)
			{
				parent[t] = t;
			}

			int Find(int t)
			{
				while (parent[t] != t)
				{
					parent[t] = parent[parent[t]];
					t = parent[t];
				}

				return t;
			}

			for (int h = 0; h < halfedges.Count; h++)
			{
				int pair = halfedges[h].PairedHalfedge;
				if (pair < 0)
				{
					// A removed or unpaired halfedge: no pairing to trust.
					return true;
				}

				int a = Find(h / 3);
				int b = Find(pair / 3);
				if (a != b)
				{
					parent[a] = b;
				}
			}

			// Six times the volume per shell: the divergence-theorem sum of each triangle's
			// tetrahedron with the origin.
			var volume = new double[triangles];
			for (int t = 0; t < triangles; t++)
			{
				var p0 = positions[halfedges[3 * t].StartVert];
				var p1 = positions[halfedges[(3 * t) + 1].StartVert];
				var p2 = positions[halfedges[(3 * t) + 2].StartVert];
				volume[Find(t)] += (p0.X * ((p1.Y * p2.Z) - (p1.Z * p2.Y)))
					- (p0.Y * ((p1.X * p2.Z) - (p1.Z * p2.X)))
					+ (p0.Z * ((p1.X * p2.Y) - (p1.Y * p2.X)));
			}

			for (int t = 0; t < triangles; t++)
			{
				if (parent[t] == t && !(volume[t] > 0))
				{
					return true;
				}
			}

			return false;
		}
	}
}
