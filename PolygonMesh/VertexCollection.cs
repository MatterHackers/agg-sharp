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

    public class VertexXYZAxisWithRotation : IComparer<Vertex>
    {
        static Matrix4X4 rotationToUse = Matrix4X4.CreateRotation(new Vector3(.224374, .805696, .383724));

        public VertexXYZAxisWithRotation()
        {
        }

        static void TransformVector(Vector3 vec, ref Matrix4X4 mat, out Vector3 result)
        {
            result.x = vec.x * mat.Row0.x +
                       vec.y * mat.Row1.x +
                       vec.z * mat.Row2.x;

            result.y = vec.x * mat.Row0.y +
                       vec.y * mat.Row1.y +
                       vec.z * mat.Row2.y;

            result.z = vec.x * mat.Row0.z +
                       vec.y * mat.Row1.z +
                       vec.z * mat.Row2.z;
        }

        public int Compare(Vertex aVertex, Vertex bVertex)
        {
            Vector3 a;
            TransformVector(aVertex.Position, ref rotationToUse, out a);
            Vector3 b;
            TransformVector(bVertex.Position, ref rotationToUse, out b);
            if (a.x < b.x)
            {
                return -1;
            }
            else if (a.x == b.x)
            {
                if (a.y < b.y)
                {
                    return -1;
                }
                else if (a.y == b.y)
                {
                    if (a.z < b.z)
                    {
                        return -1;
                    }
                    else if (a.z == b.z)
                    {
                        return 0;
                    }
                    else
                    {
                        return 1;
                    }
                }
                else
                {
                    return 1;
                }
            }
            else
            {
                return 1;
            }
        }
    }

    public class VertexCollecton : IEnumerable
    {
        List<Vertex> vertices = new List<Vertex>();
        IComparer<Vertex> vertexSorter = new VertexXYZAxisWithRotation();

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

        public int Capacity
        {
            get { return vertices.Capacity; }
            set { vertices.Capacity = value; }
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

        public int IndexOf(Vertex vertexToLookFor)
        {
            if(IsSorted)
            {
                int index = vertices.BinarySearch(vertexToLookFor, vertexSorter);
#if DEBUG
                int indexCheck = vertices.IndexOf(vertexToLookFor);
                if (index != indexCheck)
                {
                    throw new Exception("Bad index from sort");
                }
#endif
                return index;
            }
            else
            {
                return vertices.IndexOf(vertexToLookFor);
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
                if (vertices[index] == vertexToLookFor)
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
