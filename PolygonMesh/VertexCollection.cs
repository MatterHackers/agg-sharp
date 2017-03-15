using MatterHackers.VectorMath;

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
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh
{
	public class VertexCollecton : IEnumerable<Vertex>
	{
		private List<Vertex> vertices = new List<Vertex>();

		//VertexSorterBase vertexSorter = new VertexXAxisSorter();
		//VertexSorterBase vertexSorter = new VertexDistanceFromPointSorter();
		private VertexSorterBase vertexSorter = new VertexXYZAxisWithRotation();

		private bool isSorted = true;

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

		public IEnumerator<Vertex> GetEnumerator()
		{
			return vertices.GetEnumerator();
		}

		IEnumerator IEnumerable.GetEnumerator()
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

			List<Vertex> foundVertexes = vertexSorter.FindVertices(vertices, position, maxDistanceToConsiderVertexAsSame);

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
				int index = IndexOf(vertexToRemove);
				if (index != -1)
				{
					vertices.RemoveAt(index);
				}
			}
		}

		public void Add(Vertex vertexToAdd, SortOption sortOption = SortOption.SortNow)
		{
			if (sortOption == SortOption.WillSortLater)
			{
				vertices.Add(vertexToAdd);
				isSorted = false;
			}
			else
			{
				int index = IndexOf(vertexToAdd);
				if (index < 0)
				{
					index = ~index;
				}
				vertices.Insert(index, vertexToAdd);
			}
		}

		public int IndexOf(Vertex vertexToLookFor)
		{
			if (IsSorted)
			{
				int index = vertices.BinarySearch(vertexToLookFor, vertexSorter);
				if (index < 0)
				{
					return index;
				}

				// we have to get back to the first vertex at this position
				while (index > 0 && vertexSorter.Compare(vertices[index - 1], vertexToLookFor) == 0)
				{
					index--;
				}

				bool found = false;
				while (!found
					&& index < vertices.Count
					&& vertexSorter.Compare(vertices[index], vertexToLookFor) == 0)
				{
					if (vertices[index] == vertexToLookFor)
					{
						found = true;
						break;
					}
					index++;
				}

				if (!found)
				{
					// this is the insertion position
					return index;
				}

#if false
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

			int index = IndexOf(vertexToLookFor);
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

			int index = IndexOf(vertexToLookFor);
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