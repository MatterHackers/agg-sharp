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

using MatterHackers.VectorMath;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using System.Diagnostics;

namespace MatterHackers.PolygonMesh
{
    [DebuggerDisplay("ID = {Data.ID}")]
    public class Vertex
    {
        MetaData data = new MetaData();
        public MetaData Data { get { return data; } }

        public Vector3 Position { get; set; }
        public Vector3 Normal { get; set; }

        public MeshEdge firstMeshEdge;

        public Vertex(Vector3 position)
        {
            this.Position = position;
        }

        public virtual Vertex CreateInterpolated(Vertex dest, double ratioToDest)
        {
            Vertex interpolatedVertex = new Vertex(Vector3.Lerp(this.Position, dest.Position, ratioToDest));
            interpolatedVertex.Normal = Vector3.Lerp(this.Normal, dest.Normal, ratioToDest).GetNormal();
            return interpolatedVertex;
        }

        public void AddDebugInfo(StringBuilder totalDebug, int numTabs)
        {
            int firstMeshEdgeID = -1;
            if (firstMeshEdge != null)
            {
                firstMeshEdgeID = firstMeshEdge.Data.ID;
            }
            totalDebug.Append(new string('\t', numTabs) + String.Format("First MeshEdge: {0}\n", firstMeshEdgeID));
            if (firstMeshEdge != null)
            {
                firstMeshEdge.AddDebugInfo(totalDebug, numTabs + 1);
            }
        }

        public IEnumerable<Face> ConnectedFacesIterator()
        {
            HashSet<Face> allFacesOfThisEdge = new HashSet<Face>();
            foreach (MeshEdge meshEdge in ConnectedMeshEdgesIterator())
            {
                foreach (Face face in meshEdge.FacesSharingMeshEdgeIterator())
                {
                    allFacesOfThisEdge.Add(face);
                }
            }

            foreach (Face face in allFacesOfThisEdge)
            {
                yield return face;
            }
        }

        public IEnumerable<MeshEdge> ConnectedMeshEdgesIterator()
        {
            MeshEdge curMeshEdge = this.firstMeshEdge;
            if (curMeshEdge != null)
            {
                do
                {
                    yield return curMeshEdge;

                    MeshEdgeLinks nextEdgeLink = curMeshEdge.GetMeshEdgeLinksContainingVertex(this);
                    if (nextEdgeLink.nextMeshEdge == curMeshEdge)
                    {
                        curMeshEdge = nextEdgeLink.prevMeshEdge;
                    }
                    else
                    {
                        curMeshEdge = nextEdgeLink.nextMeshEdge;
                    }
                } while (curMeshEdge != this.firstMeshEdge);
            }
        }

        public MeshEdge GetMeshEdgeConnectedToVertex(Vertex vertexToFindConnectionTo)
        {
            if (this.firstMeshEdge == null)
            {
                return null;
            }

            foreach (MeshEdge meshEdge in ConnectedMeshEdgesIterator())
            {
                if (meshEdge.IsConnectedTo(vertexToFindConnectionTo))
                {
                    return meshEdge;
                }
            }

            return null;
        }

        public int GetNumConnectedMeshEdges()
        {
            int numConnectedEdges = 0;
            foreach (MeshEdge edge in ConnectedMeshEdgesIterator())
            {
                numConnectedEdges++;
            }

            return numConnectedEdges;
        }

        public void RemoveMeshEdgeFromMeshEdgeLinks(MeshEdge meshEdgeToRemove)
        {
            MeshEdgeLinks edgeLinksRemoveFrom1 = meshEdgeToRemove.GetMeshEdgeLinksContainingVertex(this);

            if (edgeLinksRemoveFrom1.prevMeshEdge != null)
            {
                MeshEdgeLinks hold = edgeLinksRemoveFrom1.prevMeshEdge.GetMeshEdgeLinksContainingVertex(this);
                hold.nextMeshEdge = edgeLinksRemoveFrom1.nextMeshEdge;
            }

            if (edgeLinksRemoveFrom1.nextMeshEdge != null)
            {
                MeshEdgeLinks hold = edgeLinksRemoveFrom1.nextMeshEdge.GetMeshEdgeLinksContainingVertex(this);
                hold.prevMeshEdge = edgeLinksRemoveFrom1.prevMeshEdge;
            }

            if (firstMeshEdge == meshEdgeToRemove)
            {
                if (edgeLinksRemoveFrom1.nextMeshEdge == meshEdgeToRemove)
                {
                    firstMeshEdge = null;
                }
                else
                {
                    firstMeshEdge = edgeLinksRemoveFrom1.nextMeshEdge;
                }
            }

            edgeLinksRemoveFrom1.nextMeshEdge = edgeLinksRemoveFrom1.prevMeshEdge = null;
        }

        public void Validate()
        {
        }

        public override string ToString()
        {
            return Position.ToString();
        }
    }
}
