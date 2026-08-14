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

namespace MatterHackers.PolygonMesh
{
	/// <summary>
	/// One unique edge of a mesh, together with the faces that share it.
	/// </summary>
	/// <remarks>
	/// This is the object-per-edge view of a mesh's edges. It costs a heap object and an array per edge, so
	/// code that only walks the edges once (render paths especially) should use <see cref="MeshEdgeGraph"/>
	/// instead, which holds the same information in flat arrays.
	/// </remarks>
	public class MeshEdge
	{
		private readonly int[] _faces;

		public MeshEdge(int vertex0Index, int vertex1Index)
			: this(vertex0Index, vertex1Index, Array.Empty<int>())
		{
		}

		internal MeshEdge(int vertex0Index, int vertex1Index, int[] faces)
		{
			Vertex0Index = vertex0Index;
			Vertex1Index = vertex1Index;
			_faces = faces;
		}

		/// <summary>
		/// Gets the indices of all the faces that share this edge.
		/// </summary>
		public IReadOnlyList<int> Faces => _faces;

		public int Vertex0Index { get; private set; }

		public int Vertex1Index { get; private set; }

		/// <summary>
		/// Builds a MeshEdge for every unique edge of the mesh.
		/// </summary>
		/// <remarks>
		/// Built on <see cref="MeshEdgeGraph"/>, so the expensive dictionary-of-lists intermediate is gone,
		/// but the returned list still costs an object and an array per edge. Prefer the graph directly when
		/// you do not need the objects.
		/// </remarks>
		public static IReadOnlyList<MeshEdge> CreateMeshEdgeList(Mesh mesh)
		{
			var graph = MeshEdgeGraph.Create(mesh);

			var meshEdges = new List<MeshEdge>(graph.EdgeCount);
			for (int edgeIndex = 0; edgeIndex < graph.EdgeCount; edgeIndex++)
			{
				meshEdges.Add(new MeshEdge(
					graph.GetVertex0(edgeIndex),
					graph.GetVertex1(edgeIndex),
					graph.GetFaces(edgeIndex).ToArray()));
			}

			return meshEdges;
		}
	}
}