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
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh")]
	public class VertexCollectonTests
	{
		[Test]
		public void PreventDuplicates()
		{
			throw new NotImplementedException();
			//VertexCollecton collection = new VertexCollecton();
			//Vector3 position1 = new Vector3(10, 11, 12);
			//IVertex vertex1 = new Vertex(position1);
			//collection.Add(vertex1);
			//Assert.IsTrue(collection.ContainsAVertexAtPosition(vertex1) == true);
			//List<IVertex> found = collection.FindVertices(position1, .001);
			//Assert.IsTrue(found.Count == 1);
			//Assert.IsTrue(found[0] == vertex1);

			//Vector3 position2 = new Vector3(20, 21, 22);
			//IVertex vertex2 = new Vertex(position2);
			//collection.Add(vertex2);
			//Assert.IsTrue(collection.ContainsAVertexAtPosition(vertex1) == true);
			//found = collection.FindVertices(position1, .001);
			//Assert.IsTrue(found.Count == 1);
			//Assert.IsTrue(found[0] == vertex1);

			//Vector3 position3 = new Vector3(10, 21, 22);
			//IVertex vertex3 = new Vertex(position3);
			//collection.Add(vertex3);
			//Assert.IsTrue(collection.ContainsAVertexAtPosition(vertex1) == true);
			//found = collection.FindVertices(position1, .001);
			//Assert.IsTrue(found.Count == 1);
			//Assert.IsTrue(found[0] == vertex1);
		}

		[Test]
		public void SortAndQueryWork()
		{
			throw new NotImplementedException();
			//double size = 10;
			//double increment = 1;
			//Random positionRand = new Random();
			//Mesh mesh = new Mesh();
			//List<Vector3Float> positions = new List<Vector3Float>();
			//int numPosition = 50;
			//for (int i = 0; i < numPosition; i++)
			//{
			//	positions.Add(new Vector3Float(positionRand.NextDouble() * size, positionRand.NextDouble() * size, positionRand.NextDouble() * size));
			//	mesh.CreateVertex(new Vector3(positions[i].x, positions[i].y, positions[i].z), CreateOption.CreateNew, SortOption.WillSortLater);
			//}

			//mesh.SortVertices();

			//for (double distance = 0; distance < size; distance += increment)
			//{
			//	for (int i = 0; i < numPosition; i++)
			//	{
			//		List<IVertex> found1 = mesh.FindVertices(new Vector3(positions[i].x, positions[i].y, positions[i].z), distance);
			//		List<Vector3Float> found2 = FindWithinDist(positions, positions[i], distance);

			//		Assert.IsTrue(found1.Count == found2.Count);
			//	}
			//}
		}

		private List<Vector3Float> FindWithinDist(List<Vector3Float> positions, Vector3Float position, double distance)
		{
			List<Vector3Float> found = new List<Vector3Float>();
			for (int i = 0; i < positions.Count; i++)
			{
				if ((positions[i] - position).Length <= distance)
				{
					found.Add(positions[i]);
				}
			}
			return found;
		}
	}
}