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

using System.Collections.Generic;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Rewinds the triangles of a surface that disagree with the rest of their own connected
	/// surface, so the kernel import sees one consistent winding.
	/// </summary>
	/// <remarks>
	/// The kernel import balances directed edges and refuses any surface where two neighbours
	/// run their shared edge the same way, reporting it as <c>NotClosed</c>. A mesh can be a
	/// perfectly closed, orientable surface and still carry a backward patch - the reported
	/// case was a 37,120-triangle part with 320 triangles reversed - and there is exactly one
	/// consistent winding for such a surface up to its overall sign, so rewinding the patch is
	/// not a guess.
	/// <para>
	/// The overall sign of each connected surface is left as the majority of its triangles have
	/// it: only the minority is flipped. A whole shell wound inside out is therefore untouched,
	/// which keeps the import's <c>repairOrientation</c> switch the only thing that decides what
	/// an inside-out shell means.
	/// </para>
	/// </remarks>
	internal static class ConsistentWinding
	{
		/// <summary>
		/// A copy of <paramref name="mesh"/> with each connected surface's minority winding
		/// flipped to match its majority and exactly coincident vertices joined, or null when there
		/// is nothing to flip or no consistent winding exists. The faces keep their order, so per-face
		/// colours and textures come across with them - a boolean reads the colours off its operands.
		/// </summary>
		/// <remarks>
		/// Connectivity is by exact vertex <em>position</em>, because that is how the kernel import
		/// welds: a mesh whose seams are split (triangle soup, or a box with a vertex set per side)
		/// is one surface to it, and read by index a backward side bounded by split seams would be
		/// an island that never flips. Seams apart by a rounding step are the caller's to weld first
		/// (<see cref="ManifoldImport.WeldSeams"/>).
		/// <para>
		/// Only edges with exactly two faces link; one shared by more (or fewer) is not a place where
		/// two sides' windings can be compared. Faces that collapse onto a repeated position are
		/// skipped - the import drops them too - so a sliver (a, b, a), whose one edge is used only by
		/// itself, cannot read as a contradiction. A surface that genuinely contradicts itself (a
		/// Möbius-like strip) has no consistent winding and returns null, leaving the caller's
		/// refusal in place.
		/// </para>
		/// </remarks>
		/// <param name="mesh">The mesh to read. Left unmodified.</param>
		/// <returns>The rewound copy, or null.</returns>
		public static Mesh Rewind(Mesh mesh)
		{
			var faces = mesh.Faces;
			var (positions, welded) = WeldByPosition(mesh);

			// Each face's corners as welded ids; null for a face that collapses onto a repeated
			// position, which takes no part in the walk and is never flipped.
			var corners = new (int A, int B, int C)?[faces.Count];
			var edgeFaces = new Dictionary<(int, int), List<int>>();

			for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
			{
				var face = faces[faceIndex];
				int a = welded[face.v0];
				int b = welded[face.v1];
				int c = welded[face.v2];
				if (a == b || b == c || c == a)
				{
					continue;
				}

				corners[faceIndex] = (a, b, c);
				AddEdge(edgeFaces, a, b, faceIndex);
				AddEdge(edgeFaces, b, c, faceIndex);
				AddEdge(edgeFaces, c, a, faceIndex);
			}

			// -1 unvisited; 0 keeps the seed's winding; 1 is reversed relative to it.
			var parity = new int[faces.Count];
			for (int i = 0; i < parity.Length; i++)
			{
				parity[i] = -1;
			}

			var toFlip = new List<int>();
			var queue = new Queue<int>();
			var component = new List<int>();

			for (int seed = 0; seed < faces.Count; seed++)
			{
				if (parity[seed] >= 0 || corners[seed] == null)
				{
					continue;
				}

				parity[seed] = 0;
				queue.Enqueue(seed);
				component.Clear();
				int reversedCount = 0;

				while (queue.Count > 0)
				{
					int faceIndex = queue.Dequeue();
					component.Add(faceIndex);
					reversedCount += parity[faceIndex];

					var (a, b, c) = corners[faceIndex].Value;
					foreach (var (start, end) in new[] { (a, b), (b, c), (c, a) })
					{
						var sharing = edgeFaces[Key(start, end)];
						if (sharing.Count != 2)
						{
							continue;
						}

						int neighbour = sharing[0] == faceIndex ? sharing[1] : sharing[0];

						// Consistent neighbours run a shared edge in opposite directions, so a
						// neighbour running it the same way has the opposite parity.
						int wanted = RunsEdge(corners[neighbour].Value, start, end) ? 1 - parity[faceIndex] : parity[faceIndex];

						if (parity[neighbour] < 0)
						{
							parity[neighbour] = wanted;
							queue.Enqueue(neighbour);
						}
						else if (parity[neighbour] != wanted)
						{
							return null;
						}
					}
				}

				// Ties keep the seed's winding; either is as good, and it is deterministic.
				int flipParity = reversedCount * 2 > component.Count ? 0 : 1;
				foreach (int faceIndex in component)
				{
					if (parity[faceIndex] == flipParity)
					{
						toFlip.Add(faceIndex);
					}
				}
			}

			if (toFlip.Count == 0)
			{
				return null;
			}

			var flip = new bool[faces.Count];
			foreach (int faceIndex in toFlip)
			{
				flip[faceIndex] = true;
			}

			return SharedVertexCopy(mesh, positions, welded, flip);
		}

		/// <summary>
		/// A copy of <paramref name="mesh"/> with exactly coincident vertices joined into one, or
		/// null when no two vertices share a position.
		/// </summary>
		/// <remarks>
		/// The kernel's strict import pairs halfedges by vertex index, so a correctly wound solid
		/// whose seams are split (every triangle its own three vertices, as STL stores it) imports
		/// only as triangle soup - and a soup operand cannot take a Minkowski (NotManifold). Joined,
		/// the same triangles pair up. Nothing moves: only vertices at the identical position are
		/// joined. The faces keep their order, colours and textures.
		/// </remarks>
		/// <param name="mesh">The mesh to read. Left unmodified.</param>
		/// <returns>The joined copy, or null.</returns>
		public static Mesh JoinCoincidentVertices(Mesh mesh)
		{
			var (positions, welded) = WeldByPosition(mesh);

			return positions.Count == mesh.Vertices.Count
				? null
				: SharedVertexCopy(mesh, positions, welded, new bool[mesh.Faces.Count]);
		}

		/// <summary>
		/// One id per distinct vertex position, and each vertex's id. Float tuple equality folds -0
		/// onto 0, as the kernel's own weld does.
		/// </summary>
		private static (List<Vector3Float> Positions, int[] Welded) WeldByPosition(Mesh mesh)
		{
			var positionIds = new Dictionary<(float, float, float), int>();
			var positions = new List<Vector3Float>();
			var welded = new int[mesh.Vertices.Count];
			for (int i = 0; i < welded.Length; i++)
			{
				var vertex = mesh.Vertices[i];
				var key = (vertex.X, vertex.Y, vertex.Z);
				if (!positionIds.TryGetValue(key, out int id))
				{
					id = positions.Count;
					positionIds[key] = id;
					positions.Add(vertex);
				}

				welded[i] = id;
			}

			return (positions, welded);
		}

		/// <summary>
		/// The mesh rebuilt on the welded ids - one vertex per position, see
		/// <see cref="JoinCoincidentVertices"/> for why - with the flagged faces reversed. Faces stay in
		/// their order, so per-face colours copy straight across; a reversed face's texture corners are
		/// reversed with it, as <see cref="Mesh.ReverseFace"/> does.
		/// </summary>
		private static Mesh SharedVertexCopy(Mesh mesh, List<Vector3Float> positions, int[] welded, bool[] flip)
		{
			var faces = mesh.Faces;
			var rewound = new Mesh();
			rewound.Vertices.AddRange(positions);
			for (int faceIndex = 0; faceIndex < faces.Count; faceIndex++)
			{
				var face = faces[faceIndex];
				int a = welded[face.v0];
				int b = welded[face.v1];
				int c = welded[face.v2];
				if (flip[faceIndex])
				{
					rewound.Faces.Add(c, b, a, -face.normal);
				}
				else
				{
					rewound.Faces.Add(a, b, c, face.normal);
				}
			}

			if (mesh.FaceColors != null)
			{
				rewound.FaceColors = (MatterHackers.Agg.Color[])mesh.FaceColors.Clone();
			}

			if (mesh.FaceTextures != null)
			{
				foreach (var (faceIndex, texture) in mesh.FaceTextures)
				{
					rewound.FaceTextures[faceIndex] = flip[faceIndex]
						? new FaceTextureData(texture.image, texture.uv2, texture.uv1, texture.uv0)
						: texture;
				}
			}

			return rewound;
		}

		private static (int, int) Key(int a, int b) => a < b ? (a, b) : (b, a);

		private static void AddEdge(Dictionary<(int, int), List<int>> edgeFaces, int a, int b, int faceIndex)
		{
			var key = Key(a, b);
			if (!edgeFaces.TryGetValue(key, out var sharing))
			{
				sharing = new List<int>(2);
				edgeFaces[key] = sharing;
			}

			sharing.Add(faceIndex);
		}

		private static bool RunsEdge((int A, int B, int C) face, int start, int end)
		{
			return (face.A == start && face.B == end)
				|| (face.B == start && face.C == end)
				|| (face.C == start && face.A == end);
		}
	}
}
