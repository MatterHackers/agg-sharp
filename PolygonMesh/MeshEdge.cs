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
using System.Linq;
using System.Text;
using System.Diagnostics;

namespace MatterHackers.PolygonMesh
{
    [DebuggerDisplay("ID = {Data.ID}")]
    public class MeshEdge
    {
        MetaData data = new MetaData();
        public MetaData Data { get { return data; } }

        public Vertex[] attachedVertecies = new Vertex[2];
        public MeshEdge[] vertexMeshEdgeLoop = new MeshEdge[2];

        public FaceEdge firstFaceEdge;

        public MeshEdge()
        {
        }

        public MeshEdge(Vertex vertex1, Vertex vertex2)
        {
            this.attachedVertecies[0] = vertex1;
            this.attachedVertecies[1] = vertex2;

            vertexMeshEdgeLoop[0] = this; // start out with a circular reference to ourselves
            vertexMeshEdgeLoop[1] = this; // start out with a circular reference to ourselves

            AppendThisEdgeToEdgeLinksOfVertex(vertex1);
            AppendThisEdgeToEdgeLinksOfVertex(vertex2);
        }

        public void AddDebugInfo(StringBuilder totalDebug, int numTabs)
        {
            totalDebug.Append(new string('\t', numTabs) + String.Format("Vertex1: {0}\n", vertex1 != null ? vertex1.Data.ID.ToString() : "null"));
            if (vertex1MeshEdgeLinks != null)
            {
                vertex1MeshEdgeLinks.AddDebugInfo(totalDebug, numTabs + 1);
            }
            else
            {
                totalDebug.Append(new string('\t', numTabs) + "null\n");
            }
            totalDebug.Append(String.Format("Vertex1 Next MeshEdge: {0}\n", vertex1NextMeshEdge.Data.ID));

            totalDebug.Append(new string('\t', numTabs) + String.Format("Vertex2: {0}\n", vertex2 != null ? vertex2.Data.ID.ToString() : "null"));
            if (vertex2MeshEdgeLinks != null)
            {
                vertex2MeshEdgeLinks.AddDebugInfo(totalDebug, numTabs + 1);
            }
            else
            {
                totalDebug.Append(new string('\t', numTabs) + "null\n");
            }
            totalDebug.Append(String.Format("Vertex2 Next MeshEdge: {0}\n", vertex2NextMeshEdge.Data.ID));
            int firstFaceEdgeID = -1;
            if (firstFaceEdge != null)
            {
                firstFaceEdgeID = firstFaceEdge.Data.ID;
            }
            totalDebug.Append(new string('\t', numTabs) + String.Format("First FaceEdge: {0}\n", firstFaceEdgeID));
        }

        public int GetVertexIndex(Vertex vertexToGetIndexOf)
        {
            if (vertexToGetIndexOf == attachedVertecies[0])
            {
                return 0;
            }
            else
            {
                if (vertexToGetIndexOf == attachedVertecies[1])
                {
                    throw new Exception("You must only ask to get the edge links for a MeshEdge that is linked to the given vertex.");
                }
                return 1;
            }
        }

        public MeshEdge GetOtherVertexIndex(Vertex vertexToNotGetIndexOf)
        {
            if (vertexToNotGetIndexOf == attachedVertecies[0])
            {
                return 1;
            }
            else
            {
                if (vertexToNotGetIndexOf == attachedVertecies[1])
                {
                    throw new Exception("You must only ask to get the edge links for a MeshEdge that is linked to the given vertex.");
                }
                return 2;
            }
        }

        void AppendThisEdgeToEdgeLinksOfVertex(Vertex vertexToAppendTo)
        {
            int indexOfMeshEdgeForAddedVertex = GetVertexIndex(vertexToAppendTo);

            if (vertexToAppendTo.firstMeshEdge == null)
            {
                // the vertex is not currently part of any edge
                // we are the only edge for this vertex so set its links all to this.
                vertexToAppendTo.firstMeshEdge = this;
                vertexMeshEdgeLoop[indexOfMeshEdgeForAddedVertex] = this;
            }
            else // the vertex is already part of an edge (or many)
            {
                int indexInExistingMeshEdge = vertexToAppendTo.firstMeshEdge.GetVertexIndex(vertexToAppendTo);
#if true
                MeshEdge hold = vertexToAppendTo.firstMeshEdge.firstFaceEdge[indexInExistingMeshEdge];
                thisEdgeLinks.prevMeshEdge = firstMeshEdgeEdgeLinks.nextMeshEdge;
                firstMeshEdgeEdgeLinks.nextMeshEdge = hold;
#else
                MeshEdgeLinks edgeLinksForPrevFromExistingEdge = edgeLinksOnOtherMeshEdge.prevMeshEdge.GetMeshEdgeLinksContainingVertex(addedVertex);

                // point our links to the ones that are already there
		        edgeLinksOnThis.nextMeshEdge = addedVertex.firstMeshEdge; // we point our next at the one that is currently on the vertex
                edgeLinksOnThis.prevMeshEdge = edgeLinksOnOtherMeshEdge.prevMeshEdge; // we point our prev at the one that is currently the prev

                // and point the ones that are already there at this.
                edgeLinksOnOtherMeshEdge.prevMeshEdge = this; // point that old prev to us
                edgeLinksForPrevFromExistingEdge.nextMeshEdge = this; // and that prev next to us
#endif
            }
        }

        internal bool IsConnectedTo(Vertex vertexToCheck)
        {
            if (vertex1 == vertexToCheck || vertex2 == vertexToCheck)
            {
                return true;
            }

            return false;
        }

        public int GetNumFacesSharingEdge()
        {
            int numFacesSharingEdge = 0;

            foreach (Face face in FacesSharingMeshEdgeIterator())
            {
                numFacesSharingEdge++;
            }

            return numFacesSharingEdge;
        }

        public IEnumerable<FaceEdge> FaceEdgesSharingMeshEdgeIterator()
        {
            FaceEdge curFaceEdge = this.firstFaceEdge;
            if (curFaceEdge != null)
            {
                do
                {
                    yield return curFaceEdge;

                    curFaceEdge = curFaceEdge.radialNextFaceEdge;
                } while (curFaceEdge != this.firstFaceEdge);
            }
        }

        public IEnumerable<Face> FacesSharingMeshEdgeIterator()
        {
            foreach (FaceEdge faceEdge in FaceEdgesSharingMeshEdgeIterator())
            {
                yield return faceEdge.containingFace;
            }
        }

        internal Vertex GetOppositeVertex(Vertex vertexToGetOppositeFor)
        {
            if (vertexToGetOppositeFor == vertex1)
            {
                return vertex2;
            }
            else
            {
                if (vertex2 != vertexToGetOppositeFor)
                {
                    throw new Exception("You must only ask to get the opposite vertex on a MeshEdge that is linked to the given vertexToGetOppositeFor.");
                }
                return vertex1;
            }
        }

        internal FaceEdge GetFaceEdge(Face faceToFindFaceEdgeFor)
        {
            foreach (FaceEdge faceEdge in faceToFindFaceEdgeFor.FaceEdgeIterator())
            {
                if (faceEdge.containingFace == faceToFindFaceEdgeFor)
                {
                    return faceEdge;
                }
            }

            return null;
        }

        public void Validate()
        {
        }
    }
}
