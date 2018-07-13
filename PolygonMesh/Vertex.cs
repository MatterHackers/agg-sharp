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
using System.Diagnostics;
using System.Text;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	[DebuggerDisplay("ID = {ID} | XYZ = {Position}")]
	public class Vertex : IVertex
	{
		public int ID { get; } = Mesh.GetID();

#if false
        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }
#else

		// this is to save memory on each vertex (12 bytes per position and 12 per normal rather than 24 and 24)
		private Vector3Float position;

		public Vector3 Position
		{
			get
			{
				return new Vector3(position.x, position.y, position.z);
			}
			set
			{
				position.x = (float)value.X;
				position.y = (float)value.Y;
				position.z = (float)value.Z;
			}
		}

		private Vector3Float normal;

		public Vector3 Normal
		{
			get
			{
				return new Vector3(normal.x, normal.y, normal.z);
			}
			set
			{
				normal.x = (float)value.X;
				normal.y = (float)value.Y;
				normal.z = (float)value.Z;
			}
		}

#endif

		public MeshEdge FirstMeshEdge { get; set; }

		public Vertex()
		{
		}

		public Vertex(Vector3 position)
		{
			this.Position = position;
		}

		public virtual IVertex CreateInterpolated(IVertex dest, double ratioToDest)
		{
			Vertex interpolatedVertex = new Vertex(Vector3.Lerp(this.Position, dest.Position, ratioToDest));
			interpolatedVertex.Normal = Vector3.Lerp(this.Normal, dest.Normal, ratioToDest).GetNormal();
			return interpolatedVertex;
		}

		public void AddDebugInfo(StringBuilder totalDebug, int numTabs)
		{
			int firstMeshEdgeID = -1;
			if (FirstMeshEdge != null)
			{
				firstMeshEdgeID = FirstMeshEdge.ID;
			}
			totalDebug.Append(new string('\t', numTabs) + String.Format("First MeshEdge: {0}\n", firstMeshEdgeID));
			if (FirstMeshEdge != null)
			{
				FirstMeshEdge.AddDebugInfo(totalDebug, numTabs + 1);
			}
		}

		public IEnumerable<Face> ConnectedFaces()
		{
			HashSet<Face> allFacesOfThisEdge = new HashSet<Face>();
			foreach (MeshEdge meshEdge in ConnectedMeshEdges())
			{
				foreach (Face face in meshEdge.FacesSharingMeshEdge())
				{
					allFacesOfThisEdge.Add(face);
				}
			}

			foreach (Face face in allFacesOfThisEdge)
			{
				yield return face;
			}
		}

		public List<MeshEdge> GetConnectedMeshEdges()
		{
			List<MeshEdge> meshEdgeList = new List<MeshEdge>();
			foreach (MeshEdge meshEdge in ConnectedMeshEdges())
			{
				meshEdgeList.Add(meshEdge);
			}

			return meshEdgeList;
		}

		public IEnumerable<MeshEdge> ConnectedMeshEdges()
		{
			if (this.FirstMeshEdge != null)
			{
				MeshEdge curMeshEdge = this.FirstMeshEdge;
				do
				{
					yield return curMeshEdge;

					curMeshEdge = curMeshEdge.GetNextMeshEdgeConnectedTo(this);
				} while (curMeshEdge != this.FirstMeshEdge);
			}
		}

		public MeshEdge GetMeshEdgeConnectedToVertex(IVertex vertexToFindConnectionTo)
		{
			if (this.FirstMeshEdge == null)
			{
				return null;
			}

			foreach (MeshEdge meshEdge in ConnectedMeshEdges())
			{
				if (meshEdge.IsConnectedTo(vertexToFindConnectionTo))
				{
					return meshEdge;
				}
			}

			return null;
		}

		public int GetConnectedMeshEdgesCount()
		{
			int numConnectedMeshEdges = 0;
			foreach (MeshEdge edge in ConnectedMeshEdges())
			{
				numConnectedMeshEdges++;
			}

			return numConnectedMeshEdges;
		}

		public void Validate()
		{
			if (FirstMeshEdge != null)
			{
				HashSet<MeshEdge> foundEdges = new HashSet<MeshEdge>();

				foreach (MeshEdge meshEdge in this.ConnectedMeshEdges())
				{
					if (foundEdges.Contains(meshEdge))
					{
						// TODO: this should really not be happening. We should only ever try to iterate to any mesh edge once.
						// We can get an infinite recursion with this and it needs to be debugged.
						throw new Exception("Bad ConnectedMeshEdges");
					}

					foundEdges.Add(meshEdge);
				}
			}
		}

		public override string ToString()
		{
			return Position.ToString();
		}
	}
}