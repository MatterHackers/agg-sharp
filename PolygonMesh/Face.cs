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
using MatterHackers.Agg.Image;

namespace MatterHackers.PolygonMesh
{
    public class FaceData : MetaData
    {
        public List<ImageBuffer> Textures = new List<ImageBuffer>();
    }

    public class Face
    {
        MetaData data = new MetaData();
        public MetaData Data { get { return data; } set { data = value; } }

        public FaceEdge firstFaceEdge;
        public Vector3 normal;

        // number of boundaries
        // matterial

        public Face()
        {
        }

        public Face(Face faceToUseAsModel)
        {
        }

        public ImageBuffer GetTexture(int index)
        {
            FaceData faceData = Data as FaceData;
            if (faceData != null && index < faceData.Textures.Count)
            {
                return faceData.Textures[index];
            }

            return null;
        }

        public void AddDebugInfo(StringBuilder totalDebug, int numTabs)
        {
            totalDebug.Append(new string('\t', numTabs) + String.Format("First FaceEdge: {0}\n", firstFaceEdge.Data.ID));
            firstFaceEdge.AddDebugInfo(totalDebug, numTabs + 1);
        }

        public int NumVertices
        {
            get
            {
                int numVertices = 1;
                FaceEdge currentFaceEdge = firstFaceEdge;
                while (currentFaceEdge.nextFaceEdge != firstFaceEdge)
                {
                    numVertices++;
                    currentFaceEdge = currentFaceEdge.nextFaceEdge;
                }
                return numVertices;
            }
        }

        double GetXIntersept(Vector2 prevPosition, Vector2 position, double y)
        {
            return position.x - (position.y - y) * (prevPosition.x - position.x) / (prevPosition.y - position.y);
        }

        int WrapQuadrantDelta(int delta, Vector2 prevPosition, Vector2 position, double x, double y)
        {
            switch (delta)
            {
                // make quadrant deltas wrap around
                case 3:
                    return -1;

                case -3:
                    return 1;

                // check if went around point cw or ccw
                case 2:
                case -2:
                    if (GetXIntersept(prevPosition, position, y) > x)
                    {
                        return -delta;
                    }
                    break;
            }

            return delta;
        }

        int GetQuadrant(Vector2 positionToGetQuadantFor, double x, double y)
        {
            if (positionToGetQuadantFor.x > x)
            {
                if (positionToGetQuadantFor.y > y)
                {
                    return 0;
                }
                else
                {
                    return 3;
                }
            }
            else
            {
                if (positionToGetQuadantFor.y > y)
                {
                    return 1;
                }
                else
                {
                    return 2;
                }
            }
        }

        public IEnumerable<Vertex> VertexIterator()
        {
            foreach (FaceEdge faceEdge in FaceEdgeIterator())
            {
                yield return faceEdge.vertex;
            }
        }

        public IEnumerable<FaceEdge> FaceEdgeIterator()
        {
            return firstFaceEdge.NextFaceEdgeIterator();
        }

        bool PointInPoly(double x, double y)
        {
            // set these for the major axis of projection.
            int xIndex = 0;
            int yIndex = 1;

            int accumulatedQuadrantAngle = 0;
            int prevQuadrant = 0;
            Vector2 prevPosition = Vector2.Zero;
            bool foundFirst = false;
            foreach (Vertex vertex in VertexIterator())
            {
                Vector2 position = new Vector2(vertex.Position[xIndex], vertex.Position[yIndex]);
                int quadrant = GetQuadrant(position, x, y);

                if(foundFirst)
                {
                    accumulatedQuadrantAngle += WrapQuadrantDelta(quadrant - prevQuadrant, prevPosition, position, x, y);
                }
                else
                {
                    foundFirst = true;
                }

                prevPosition = position;
                prevQuadrant = quadrant;
            }

            // complete 360 degrees (angle of + 4 or -4 ) means inside
            if ((accumulatedQuadrantAngle == 4) || (accumulatedQuadrantAngle == -4))
            {
                return true;
            }

            return false;
        }

        public void CalculateNormal()
        {
            FaceEdge faceEdge0 = firstFaceEdge;
            FaceEdge faceEdge1 = faceEdge0.nextFaceEdge;
            Vector3 faceEdge1Minus0 = faceEdge1.vertex.Position - faceEdge0.vertex.Position;
            FaceEdge faceEdge2 = faceEdge1;
            bool collinear = false;
            do
            {
                faceEdge2 = faceEdge2.nextFaceEdge;
                collinear = Vector3.Collinear(faceEdge0.vertex.Position, faceEdge1.vertex.Position, faceEdge2.vertex.Position);
            } while (collinear && faceEdge2 != faceEdge0);
            Vector3 face2Minus0 = faceEdge2.vertex.Position - faceEdge0.vertex.Position;
            normal = Vector3.Cross(faceEdge1Minus0, face2Minus0).GetNormal();
        }

        public bool FaceEdgeLoopIsGood()
        {
            foreach (FaceEdge faceEdge in FaceEdgeIterator())
            {
                if (faceEdge.nextFaceEdge.prevFaceEdge != faceEdge)
                {
                    return false;
                }
            }

            return true;
        }

        public void Validate()
        {
            List<FaceEdge> nextList = new List<FaceEdge>();
            foreach (FaceEdge faceEdge in firstFaceEdge.NextFaceEdgeIterator())
            {
                nextList.Add(faceEdge);
            }

            int index = nextList.Count;
            foreach (FaceEdge faceEdge in firstFaceEdge.PrevFaceEdgeIterator())
            {
                int validIndex = (index--) % nextList.Count;
                if (faceEdge != nextList[validIndex])
                {
                    throw new Exception("The next and prev sets must be mirrors.");
                }
            }

            nextList.Clear();
            foreach (FaceEdge faceEdge in firstFaceEdge.RadialNextFaceEdgeIterator())
            {
                nextList.Add(faceEdge);
            }

            index = nextList.Count;
            foreach (FaceEdge faceEdge in firstFaceEdge.RadialPrevFaceEdgeIterator())
            {
                int validIndex = (index--) % nextList.Count;
                if (faceEdge != nextList[validIndex])
                {
                    throw new Exception("The next and prev sets must be mirrors.");
                }
            }
        }
    }
}
