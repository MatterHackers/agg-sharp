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
    public class MeshEdgeLinks
    {
        public MeshEdge nextMeshEdge;
        public MeshEdge prevMeshEdge;

        public MeshEdgeLinks()
        {
        }

        public MeshEdgeLinks(MeshEdge nextMeshEdge, MeshEdge prevMeshEdge)
        {
            this.nextMeshEdge = nextMeshEdge;
            this.prevMeshEdge = prevMeshEdge;
        }

        public void AddDebugInfo(StringBuilder totalDebug, int numTabs)
        {
            totalDebug.Append(new string('\t', numTabs));
            if (nextMeshEdge != null)
            {
                 totalDebug.Append(String.Format("Next MeshEdge: {0}\n", nextMeshEdge.Data.ID));
            }
            else
            {
                totalDebug.Append("null\n");
            }

            totalDebug.Append(new string('\t', numTabs));
            if (prevMeshEdge != null)
            {
                totalDebug.Append(String.Format("Prev MeshEdge: {0}\n", prevMeshEdge.Data.ID));
            }
            else
            {
                totalDebug.Append("null\n");
             }
        }
    }

    [DebuggerDisplay("ID = {Data.ID}")]
    public class MeshEdge
    {
        MetaData data = new MetaData();
        public MetaData Data { get { return data; } }

        public Vertex vertex1;
        public MeshEdge vertex1NextMeshEdge;
        public MeshEdgeLinks vertex1MeshEdgeLinks = new MeshEdgeLinks();

        public Vertex vertex2;
        public MeshEdge vertex2NextMeshEdge;
        public MeshEdgeLinks vertex2MeshEdgeLinks = new MeshEdgeLinks();

        public FaceEdge firstFaceEdge;

        public MeshEdge()
        {
        }

        public MeshEdge(Vertex vertex1, Vertex vertex2)
        {
            this.vertex1 = vertex1;
            this.vertex2 = vertex2;

            vertex1NextMeshEdge = this; // start out with a circular reference to ourselves
            vertex1MeshEdgeLinks.nextMeshEdge = this;
            vertex1MeshEdgeLinks.prevMeshEdge = this;
            vertex2NextMeshEdge = this; // start out with a circular reference to ourselves
            vertex2MeshEdgeLinks.nextMeshEdge = this;
            vertex2MeshEdgeLinks.prevMeshEdge = this;

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

        public MeshEdgeLinks GetMeshEdgeLinksContainingVertex(Vertex vertexToGetLinksFor)
        {
            if (vertexToGetLinksFor == vertex1)
            {
                return vertex1MeshEdgeLinks;
            }
            else
            {
                if (vertex2 != vertexToGetLinksFor)
                {
                    throw new Exception("You must only ask to get the edge links for a MeshEdge that is linked to the given vertex.");
                }
                return vertex2MeshEdgeLinks;
            }
        }

        public MeshEdgeLinks GetMeshEdgeLinksOppositeVertex(Vertex vertexToGetLinksFor)
        {
            if (vertexToGetLinksFor == vertex1)
            {
                return vertex2MeshEdgeLinks;
            }
            else
            {
                if (vertex2 != vertexToGetLinksFor)
                {
                    throw new Exception("You must only ask to get the edge links for a MeshEdge that is linked to the given vertex.");
                }
                return vertex1MeshEdgeLinks;
            }
        }

        void AppendThisEdgeToEdgeLinksOfVertex(Vertex addedVertex)
        {
            if(addedVertex.firstMeshEdge == null)
            {
                // the vertex is not currently part of any edge
                MeshEdgeLinks edgeLinksForAddedVertex = GetMeshEdgeLinksContainingVertex(addedVertex);

                // we are the only edge for this vertex so set its links all to this.
                addedVertex.firstMeshEdge = this;
                edgeLinksForAddedVertex.nextMeshEdge = this;
                edgeLinksForAddedVertex.prevMeshEdge = this;
            }
            else // the vertex is already part of an edge (or many)
            {
                MeshEdgeLinks thisEdgeLinks = GetMeshEdgeLinksContainingVertex(addedVertex);
                MeshEdgeLinks firstMeshEdgeEdgeLinks = addedVertex.firstMeshEdge.GetMeshEdgeLinksContainingVertex(addedVertex);
#if true
                MeshEdge hold = thisEdgeLinks.prevMeshEdge;
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
