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

using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
    public class FaceEdgeData : MetaData
    {
        public List<Vector2> TextureUV = new List<Vector2>();
    }

    public class FaceEdge
    {
        MetaData data = new MetaData();
        public MetaData Data { get { return data; } set { data = value; } }

        public Face containingFace;
        public Vertex firstVertex;
        public MeshEdge meshEdge;

        public FaceEdge nextFaceEdge;
        public FaceEdge prevFaceEdge;

        public FaceEdge radialNextFaceEdge;
        public FaceEdge radialPrevFaceEdge;

        public FaceEdge()
        {
        }

        public FaceEdge(Face face, MeshEdge meshEdge, Vertex vertex)
        {
            this.containingFace = face;
            this.meshEdge = meshEdge;
            this.firstVertex = vertex;

            nextFaceEdge = null;
            prevFaceEdge = null;

            radialNextFaceEdge = radialPrevFaceEdge = this;
        }

        public Vector2 GetUVs(int index)
        {
            FaceEdgeData faceEdgeData = Data as FaceEdgeData;
            if (faceEdgeData != null && index < faceEdgeData.TextureUV.Count)
            {
                return faceEdgeData.TextureUV[index];
            }

            return new Vector2();
        }

        public void AddDebugInfo(StringBuilder totalDebug, int numTabs, bool printRecursive = true)
        {
            totalDebug.Append(new string('\t', numTabs) + String.Format("Face: {0}\n", containingFace.Data.ID));
            totalDebug.Append(new string('\t', numTabs) + String.Format("MeshEdge: {0}\n", meshEdge.Data.ID));
            totalDebug.Append(new string('\t', numTabs) + String.Format("Vertex: {0}\n", firstVertex.Data.ID));

            if(printRecursive)
            {
                bool afterFirst = false;
                foreach (FaceEdge faceEdge in NextFaceEdgeIterator())
                {
                    if (afterFirst)
                    {
                        totalDebug.Append(new string('\t', numTabs) + String.Format("Next FaceEdge: {0}\n", faceEdge.Data.ID));
                        faceEdge.AddDebugInfo(totalDebug, numTabs + 1, false);
                    }
                    afterFirst = true;
                }
            }

            PrintFaceEdges(totalDebug, "Prev FaceEdge: ", numTabs, PrevFaceEdgeIterator());

            PrintFaceEdges(totalDebug, "Radial Next FaceEdge: ", numTabs, RadialNextFaceEdgeIterator());
            PrintFaceEdges(totalDebug, "Radial Prev FaceEdge: ", numTabs, RadialPrevFaceEdgeIterator());
        }

        private static void PrintFaceEdges(StringBuilder totalDebug, string title, int numTabs, IEnumerable<FaceEdge> iterator)
        {
            string first = null;
            totalDebug.Append(new string('\t', numTabs) + String.Format(title));
            foreach (FaceEdge faceEdge in iterator)
            {
                if (first == null)
                {
                    first = faceEdge.Data.ID.ToString();
                }
                else
                {
                    totalDebug.Append(faceEdge.Data.ID + ", ");
                }
            }
            // show the first one last as it is the this and we want it to print as last.
            totalDebug.Append(first + "\n");
        }

        public void AddToRadialLoop(MeshEdge currentMeshEdge)
        {
            if (currentMeshEdge.firstFaceEdge == null)
            {
                // This is the first face edge of this mesh edge.  Add it.
                currentMeshEdge.firstFaceEdge = this;
            }
            else
            {
                // There was a face on this mesh edge so add this one in.
                // First set the new face edge radias pointers
                this.radialPrevFaceEdge = currentMeshEdge.firstFaceEdge;
                this.radialNextFaceEdge = currentMeshEdge.firstFaceEdge.radialNextFaceEdge;

                // then fix the insertion point to point to this new face edge.
                this.radialPrevFaceEdge.radialNextFaceEdge = this;
                this.radialNextFaceEdge.radialPrevFaceEdge = this;
            }
        }

        public IEnumerable<FaceEdge> NextFaceEdgeIterator()
        {
            FaceEdge curFaceEdge = this;
            do
            {
                yield return curFaceEdge;

                curFaceEdge = curFaceEdge.nextFaceEdge;
            } while (curFaceEdge != this);
        }

        public IEnumerable<FaceEdge> PrevFaceEdgeIterator()
        {
            FaceEdge curFaceEdge = this;
            do
            {
                yield return curFaceEdge;

                curFaceEdge = curFaceEdge.prevFaceEdge;
            } while (curFaceEdge != this);
        }

        public IEnumerable<FaceEdge> RadialNextFaceEdgeIterator()
        {
            FaceEdge curFaceEdge = this;
            do
            {
                yield return curFaceEdge;

                curFaceEdge = curFaceEdge.radialNextFaceEdge;
            } while (curFaceEdge != this);
        }

        public IEnumerable<FaceEdge> RadialPrevFaceEdgeIterator()
        {
            FaceEdge curFaceEdge = this;
            do
            {
                yield return curFaceEdge;

                curFaceEdge = curFaceEdge.radialPrevFaceEdge;
            } while (curFaceEdge != this);
        }
    }
}
