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

namespace MatterHackers.PolygonMesh
{
	/// <summary>
	/// The unique edges of a mesh, and the faces adjacent to each edge, held as flat arrays.
	/// </summary>
	/// <remarks>
	/// This exists because the object-per-edge view (<see cref="MeshEdge.CreateMeshEdgeList"/>) is far too
	/// expensive for the outline render path, which walks every edge once and keeps nothing. Measured on a
	/// 5,113,368 face mesh (7,670,061 unique edges), the old dictionary-of-lists build allocated 2,279 MB,
	/// peaked at +1,906 MB, and retained 824 MB - one <see cref="System.Collections.Generic.List{T}"/> per
	/// edge in the dictionary, plus a second one inside every MeshEdge object.
	///
	/// Here the whole thing is four arrays and one in-place sort: three (edgeKey, faceIndex) slots per face
	/// sorted by key, then a walk over equal-key runs. For that same mesh it allocates about 275 MB
	/// transiently and retains about 153 MB (the sorted face indices double as the adjacency storage).
	///
	/// Adjacency is stored CSR style: the faces on edge e are faceIndices[faceStarts[e] .. faceStarts[e + 1]).
	/// </remarks>
	public class MeshEdgeGraph
	{
		private readonly int[] edgeVertex0;
		private readonly int[] edgeVertex1;

		// Length EdgeCount + 1, so the run for the last edge needs no special case.
		private readonly int[] faceStarts;

		private readonly int[] faceIndices;

		private MeshEdgeGraph(int[] edgeVertex0, int[] edgeVertex1, int[] faceStarts, int[] faceIndices)
		{
			this.edgeVertex0 = edgeVertex0;
			this.edgeVertex1 = edgeVertex1;
			this.faceStarts = faceStarts;
			this.faceIndices = faceIndices;
		}

		/// <summary>
		/// Gets the number of unique edges in the mesh.
		/// </summary>
		public int EdgeCount => edgeVertex0.Length;

		/// <summary>
		/// Gets the lower of the two vertex indices of the given edge.
		/// </summary>
		public int GetVertex0(int edgeIndex) => edgeVertex0[edgeIndex];

		/// <summary>
		/// Gets the higher of the two vertex indices of the given edge.
		/// </summary>
		public int GetVertex1(int edgeIndex) => edgeVertex1[edgeIndex];

		/// <summary>
		/// Gets how many faces share the given edge. 2 is manifold, 1 is a boundary edge, more is non-manifold.
		/// </summary>
		public int GetFaceCount(int edgeIndex) => faceStarts[edgeIndex + 1] - faceStarts[edgeIndex];

		/// <summary>
		/// Gets one of the faces sharing the given edge. Faces are in ascending face index order.
		/// </summary>
		/// <param name="edgeIndex">The edge to look up.</param>
		/// <param name="faceOffset">Which of that edge's faces to return, from 0 to GetFaceCount() - 1.</param>
		public int GetFace(int edgeIndex, int faceOffset) => faceIndices[faceStarts[edgeIndex] + faceOffset];

		/// <summary>
		/// Gets all the faces sharing the given edge, in ascending face index order, without copying.
		/// </summary>
		public ReadOnlySpan<int> GetFaces(int edgeIndex)
		{
			int start = faceStarts[edgeIndex];
			return new ReadOnlySpan<int>(faceIndices, start, faceStarts[edgeIndex + 1] - start);
		}

		/// <summary>
		/// Builds the edge graph for a mesh. O(n log n) in the face count, with no per-edge heap objects.
		/// </summary>
		public static MeshEdgeGraph Create(Mesh mesh)
		{
			int faceCount = mesh.Faces.Count;
			int slotCount = faceCount * 3;

			// One slot per face-edge. The key packs the edge's two vertex indices low-first into a single
			// ulong so a plain numeric sort brings every use of the same edge together.
			var keys = new ulong[slotCount];
			var faces = new int[slotCount];
			for (int faceIndex = 0; faceIndex < faceCount; faceIndex++)
			{
				var face = mesh.Faces[faceIndex];
				int slot = faceIndex * 3;

				keys[slot] = EdgeKey(face.v0, face.v1);
				keys[slot + 1] = EdgeKey(face.v1, face.v2);
				keys[slot + 2] = EdgeKey(face.v2, face.v0);

				faces[slot] = faceIndex;
				faces[slot + 1] = faceIndex;
				faces[slot + 2] = faceIndex;
			}

			// Sorts in place, carrying the face indices along, so the faces array comes out already grouped
			// by edge - it becomes the CSR adjacency storage with no second pass and no extra allocation.
			Array.Sort(keys, faces);

			int edgeCount = 0;
			for (int slot = 0; slot < slotCount; slot++)
			{
				if (slot == 0 || keys[slot] != keys[slot - 1])
				{
					edgeCount++;
				}
			}

			var edgeVertex0 = new int[edgeCount];
			var edgeVertex1 = new int[edgeCount];
			var faceStarts = new int[edgeCount + 1];
			faceStarts[edgeCount] = slotCount;

			int edgeIndexBeingFilled = -1;
			for (int slot = 0; slot < slotCount; slot++)
			{
				if (slot == 0 || keys[slot] != keys[slot - 1])
				{
					if (edgeIndexBeingFilled >= 0)
					{
						SortFacesInRun(faces, faceStarts[edgeIndexBeingFilled], slot);
					}

					edgeIndexBeingFilled++;
					edgeVertex0[edgeIndexBeingFilled] = (int)(keys[slot] >> 32);
					edgeVertex1[edgeIndexBeingFilled] = (int)(keys[slot] & 0xFFFFFFFF);
					faceStarts[edgeIndexBeingFilled] = slot;
				}
			}

			if (edgeIndexBeingFilled >= 0)
			{
				SortFacesInRun(faces, faceStarts[edgeIndexBeingFilled], slotCount);
			}

			return new MeshEdgeGraph(edgeVertex0, edgeVertex1, faceStarts, faces);
		}

		private static ulong EdgeKey(int vertexA, int vertexB)
		{
			return vertexA < vertexB
				? ((ulong)(uint)vertexA << 32) | (uint)vertexB
				: ((ulong)(uint)vertexB << 32) | (uint)vertexA;
		}

		/// <summary>
		/// Puts one edge's faces back into ascending order. Array.Sort is unstable, so a run's faces can come
		/// out shuffled, and callers (and the MeshEdge view built on this) have always seen ascending faces.
		/// Runs are almost always 2 long, so an insertion sort is the right shape here.
		/// </summary>
		private static void SortFacesInRun(int[] faces, int start, int end)
		{
			for (int i = start + 1; i < end; i++)
			{
				int face = faces[i];
				int j = i - 1;
				while (j >= start && faces[j] > face)
				{
					faces[j + 1] = faces[j];
					j--;
				}

				faces[j + 1] = face;
			}
		}
	}
}
