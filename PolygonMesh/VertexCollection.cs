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
        List<Vertex> vertecies = new List<Vertex>();
        //IComparer<Vertex> vertexSorter = new VertexXAxisSorter();
        IComparer<Vertex> vertexSorter = new VertexDistanceFromPointSorter();

        public VertexCollecton()
        {
        }

        public Vertex this[int index]
        {
            get { return vertecies[index]; }
        }

        public IEnumerator GetEnumerator()
        {
            return vertecies.GetEnumerator();
        }

        public List<Vertex> FindVertices(Vector3 position, double maxDistanceToConsiderVertexAsSame)
        {
#if VALIDATE_SEARCH
            List<Vertex> testList = new List<Vertex>();
            foreach (Vertex vertex in vertecies)
            {
                if ((vertex.Position - position).Length < maxDistanceToConsiderVertexAsSame)
                {
                    testList.Add(vertex);
                }
            }
#endif

            List<Vertex> findList = new List<Vertex>();

            Vertex testPos = new Vertex(position);
            int index = vertecies.BinarySearch(testPos, vertexSorter);
            if (index < 0)
            {
                index = ~index;
            }
            // we have the starting index now get all the vertices that are close enough starting from here
            double maxDistanceToConsiderVertexAsSameSquared = maxDistanceToConsiderVertexAsSame * maxDistanceToConsiderVertexAsSame;
            for (int i = index; i < vertecies.Count; i++)
            {
                if (Math.Abs(vertecies[i].Position.x - position.x) > maxDistanceToConsiderVertexAsSame)
                {
                    // we are too far away in x, we are done with this direction
                    break;
                }
                position = FindIfSameEnough(position, findList, maxDistanceToConsiderVertexAsSameSquared, i);
            }
            for (int i = index - 1; i >= 0; i--)
            {
                if (Math.Abs(vertecies[i].Position.x - position.x) > maxDistanceToConsiderVertexAsSame)
                {
                    // we are too far away in x, we are done with this direction
                    break;
                }
                position = FindIfSameEnough(position, findList, maxDistanceToConsiderVertexAsSameSquared, i);
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

            return findList;
        }

        private Vector3 FindIfSameEnough(Vector3 position, List<Vertex> findList, double maxDistanceToConsiderVertexAsSameSquared, int i)
        {
            if (vertecies[i].Position == position)
            {
                findList.Add(vertecies[i]);
            }
            else
            {
                double distanceSquared = (vertecies[i].Position - position).LengthSquared;
                if (distanceSquared <= maxDistanceToConsiderVertexAsSameSquared)
                {
                    findList.Add(vertecies[i]);
                }
            }
            return position;
        }

        internal void Add(Vertex vertexToAdd)
        {
            int index = vertecies.BinarySearch(vertexToAdd, vertexSorter);
            if (index < 0)
            {
                index = ~index;
            }
            vertecies.Insert(index, vertexToAdd);
        }

        internal bool ContainsVertex(Vertex vertexToLookFor)
        {
            int index = vertecies.BinarySearch(vertexToLookFor, vertexSorter);
            if (index < 0)
            {
                return false;
            }

            return true;
        }
 
        public int Count
        {
            get { return vertecies.Count; }
        }
    }
}
