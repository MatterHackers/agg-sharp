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
//#define VALIDATE_SEARCH
using System;

using MatterHackers.VectorMath;
using System.Collections;
using System.Collections.Generic;
using System.Text;
using MatterHackers.Agg;

namespace MatterHackers.PolygonMesh
{
    public class VertexXAxisSorter : IComparer<Vertex>
    {
        public VertexXAxisSorter()
        {
        }

        public int Compare(Vertex a, Vertex b)
        {
            return a.Position.x.CompareTo(b.Position.x);
        }
    }

    public class VertexDistanceFromPointSorter : IComparer<Vertex>
    {
        static Vector3 positionToMeasureFrom = new Vector3(.224374, .805696, .383724);
        public VertexDistanceFromPointSorter()
        {
        }

        public int Compare(Vertex a, Vertex b)
        {
            double distToASquared = (a.Position - positionToMeasureFrom).LengthSquared;
            double distToBSquared = (b.Position - positionToMeasureFrom).LengthSquared;
            return distToASquared.CompareTo(distToBSquared);
        }
    }

    public class VertexCollecton : IEnumerable
    {
        List<Vertex> vertices = new List<Vertex>();
        IComparer<Vertex> vertexSorter = new VertexDistanceFromPointSorter();

        bool isSorted = true;
        public bool IsSorted
        {
            get { return isSorted; }
            set { isSorted = value; }
        }

        public VertexCollecton()
        {
        }

        public Vertex this[int index]
        {
            get { return vertices[index]; }
        }

        public IEnumerator GetEnumerator()
        {
            return vertices.GetEnumerator();
        }

        public List<Vertex> FindVertices(Vector3 position, double maxDistanceToConsiderVertexAsSame)
        {
#if VALIDATE_SEARCH
            List<Vertex> testList = new List<Vertex>();
            foreach (Vertex vertex in vertices)
            {
                if ((vertex.Position - position).Length < maxDistanceToConsiderVertexAsSame)
                {
                    testList.Add(vertex);
                }
            }
#endif
            if (!IsSorted)
            {
                throw new Exception("You can't Find a vertex in an unsorted VertexCollection. Sort it first (or add the vertexes without preventing sorting).");
            }
            List<Vertex> foundVertexes = new List<Vertex>();

            Vertex testPos = new Vertex(position);
            int index = vertices.BinarySearch(testPos, vertexSorter);
            if (index < 0)
            {
                index = ~index;
            }
            // we have the starting index now get all the vertices that are close enough starting from here
            double maxDistanceToConsiderVertexAsSameSquared = maxDistanceToConsiderVertexAsSame * maxDistanceToConsiderVertexAsSame;
            for (int i = index; i < vertices.Count; i++)
            {
                if (Math.Abs(vertices[i].Position.x - position.x) > maxDistanceToConsiderVertexAsSame)
                {
                    // we are too far away in x, we are done with this direction
                    break;
                }
                AddToListIfSameEnough(position, foundVertexes, maxDistanceToConsiderVertexAsSameSquared, i);
            }
            for (int i = index - 1; i >= 0; i--)
            {
                if (Math.Abs(vertices[i].Position.x - position.x) > maxDistanceToConsiderVertexAsSame)
                {
                    // we are too far away in x, we are done with this direction
                    break;
                }
                AddToListIfSameEnough(position, foundVertexes, maxDistanceToConsiderVertexAsSameSquared, i);
            }

#if VALIDATE_SEARCH
            if (testList.Count != findList.Count)
            {
                throw new Exception("You missed some or got some wrong.");
            }
            foreach (Vertex vertex in testList)
            {
                if (!findList.Contains(vertex))
                {
                    throw new Exception("You missed some or got some wrong.");
                }
            }
#endif

            return foundVertexes;
        }

        private void AddToListIfSameEnough(Vector3 position, List<Vertex> findList, double maxDistanceToConsiderVertexAsSameSquared, int i)
        {
            if ((vertices[i].Flags & VertexFlags.MarkedForDeletion) != VertexFlags.MarkedForDeletion)
            {
                if (vertices[i].Position == position)
                {
                    findList.Add(vertices[i]);
                }
                else
                {
                    double distanceSquared = (vertices[i].Position - position).LengthSquared;
                    if (distanceSquared <= maxDistanceToConsiderVertexAsSameSquared)
                    {
                        findList.Add(vertices[i]);
                    }
                }
            }
        }

        public void Sort()
        {
            if (!IsSorted)
            {
                vertices.Sort(vertexSorter);
                isSorted = true;
            }
        }

        public void Remove(Vertex vertexToRemove)
        {
            if (!IsSorted)
            {
                vertices.Remove(vertexToRemove);
            }
            else
            {
                int index = vertices.BinarySearch(vertexToRemove, vertexSorter);
                if (index < 0)
                {
                    throw new Exception("This vertex is not in this collection.");
                }

                // we have to get back to the first vertex at this position
                while (index > 0 && vertices[index-1].Position == vertexToRemove.Position)
                {
                    index--;
                }

                while (index < vertices.Count && vertices[index].Position == vertexToRemove.Position)
                {
                    if (vertices[index] == vertexToRemove)
                    {
                        vertices.RemoveAt(index);
                        return;
                    }
                    index++;
                }

                throw new Exception("This vertex is not in this collection.");
            }
        }

        public void Add(Vertex vertexToAdd, bool willSortLater = false)
        {
            if (willSortLater)
            {
                vertices.Add(vertexToAdd);
                isSorted = false;
            }
            else
            {
                int index = vertices.BinarySearch(vertexToAdd, vertexSorter);
                if (index < 0)
                {
                    index = ~index;
                }
                vertices.Insert(index, vertexToAdd);
            }
        }

        public bool ContainsAVertexAtPosition(Vertex vertexToLookFor)
        {
            if (!IsSorted)
            {
                throw new Exception("You can't Find a vertex in an unsorted VertexCollection. Sort it first (or add the vertexes without preventing sorting).");
            }

            int index = vertices.BinarySearch(vertexToLookFor, vertexSorter);
            if (index < 0)
            {
                return false;
            }

            return true;
        }

        public bool ContainsVertex(Vertex vertexToLookFor)
        {
            if (!IsSorted)
            {
                throw new Exception("You can't Find a vertex in an unsorted VertexCollection. Sort it first (or add the vertexes without preventing sorting).");
            }

            int index = vertices.BinarySearch(vertexToLookFor, vertexSorter);
            if (index < 0)
            {
                return false;
            }

            // we have to get back to the first vertex at this position
            while (index > 0 && vertices[index - 1].Position == vertexToLookFor.Position)
            {
                index--;
            }

            while (index < vertices.Count && vertices[index].Position == vertexToLookFor.Position)
            {
                if (vertices[index] == vertexToLookFor
                    && ((vertices[index].Flags & VertexFlags.MarkedForDeletion) != VertexFlags.MarkedForDeletion))
                {
                    return true;
                }
                index++;
            }

            return false;
        }

        public int Count
        {
            get { return vertices.Count; }
        }
    }
}
