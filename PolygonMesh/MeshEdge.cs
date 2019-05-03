/*
Copyright (c) 2014, Lars Brubaker
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
	public class MeshEdge
	{
		private readonly List<int> _faces = new List<int>();

		public MeshEdge(int vertex0Index, int vertex1Index)
		{
			Vertex0Index = vertex0Index;
			Vertex1Index = vertex1Index;
		}

		/// <summary>
		/// Gets the indices of all the faces that share this edge.
		/// </summary>
		public IReadOnlyList<int> Faces => _faces;

		public int Vertex0Index { get; private set; }

		public int Vertex1Index { get; private set; }

		public static IReadOnlyList<MeshEdge> CreateMeshEdgeList(Mesh mesh)
		{
			// make a list of every face edge (faceIndex, vertex0Index, vertex1Index)
			var faceEdges = new List<(int face, int start, int end)>(mesh.Faces.Count * 3);
			for (int i = 0; i < mesh.Faces.Count; i++)
			{
				var face = mesh.Faces[i];

				// sort them so the start index is always the smaller index
				faceEdges.Add((i, Math.Min(face.v0, face.v1), Math.Max(face.v0, face.v1)));
				faceEdges.Add((i, Math.Min(face.v1, face.v2), Math.Max(face.v1, face.v2)));
				faceEdges.Add((i, Math.Min(face.v2, face.v0), Math.Max(face.v2, face.v0)));
			}

			// make a dictionary, keyed on edge of faces
			var faceEdgesThatShareStartIndex = new Dictionary<(int start, int end), List<int>>();
			for (int i = 0; i < faceEdges.Count; i++)
			{
				var (face, start, end) = faceEdges[i];
				if (!faceEdgesThatShareStartIndex.ContainsKey((start, end)))
				{
					faceEdgesThatShareStartIndex.Add((start, end), new List<int>());
				}

				faceEdgesThatShareStartIndex[(start, end)].Add(face);
			}

			// now that we have a dictionary of all the face edges by start index
			// we can make the list of mesh edges
			var meshEdges = new List<MeshEdge>();

			foreach (var kvp in faceEdgesThatShareStartIndex)
			{
				var meshEdge = new MeshEdge(kvp.Key.start, kvp.Key.end);
				foreach (var face in kvp.Value)
				{
					meshEdge._faces.Add(face);
				}

				meshEdges.Add(meshEdge);
			}

			return meshEdges;
		}
	}
}