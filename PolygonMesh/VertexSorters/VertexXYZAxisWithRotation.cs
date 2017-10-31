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
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh
{
	public class VertexXYZAxisWithRotation : VertexSorterBase
	{
		private static Matrix4X4 rotationToUse = Matrix4X4.CreateRotation(new Vector3(.224374, .805696, .383724));

		public VertexXYZAxisWithRotation()
		{
		}

		private static void TransformVector(Vector3 vec, ref Matrix4X4 mat, out Vector3 result)
		{
			result.X = vec.X * mat.Row0.X +
					   vec.Y * mat.Row1.X +
					   vec.Z * mat.Row2.X;

			result.Y = vec.X * mat.Row0.Y +
					   vec.Y * mat.Row1.Y +
					   vec.Z * mat.Row2.Y;

			result.Z = vec.X * mat.Row0.Z +
					   vec.Y * mat.Row1.Z +
					   vec.Z * mat.Row2.Z;
		}

		public override List<IVertex> FindVertices(List<IVertex> vertices, Vector3 position, double maxDistanceToConsiderVertexAsSame)
		{
			List<IVertex> foundVertices = new List<IVertex>();

			IVertex testPos = new Vertex(position);
			int index = vertices.BinarySearch(testPos, this);
			if (index < 0)
			{
				index = ~index;
			}
			// we have the starting index now get all the vertices that are close enough starting from here
			double maxDistanceToConsiderVertexAsSameSquared = maxDistanceToConsiderVertexAsSame * maxDistanceToConsiderVertexAsSame;
			Vector3 positionRotated;
			TransformVector(position, ref rotationToUse, out positionRotated);
			for (int i = index; i < vertices.Count; i++)
			{
				Vector3 positionToTest;
				TransformVector(vertices[i].Position, ref rotationToUse, out positionToTest);
				if (Math.Abs(positionToTest.X - positionRotated.X) > maxDistanceToConsiderVertexAsSame)
				{
					// we are too far away in x, we are done with this direction
					break;
				}
				AddToListIfSameEnough(vertices, position, foundVertices, maxDistanceToConsiderVertexAsSameSquared, i);
			}
			for (int i = index - 1; i >= 0; i--)
			{
				Vector3 positionToTest;
				TransformVector(vertices[i].Position, ref rotationToUse, out positionToTest);
				if (Math.Abs(positionToTest.X - positionRotated.X) > maxDistanceToConsiderVertexAsSame)
				{
					// we are too far away in x, we are done with this direction
					break;
				}
				AddToListIfSameEnough(vertices, position, foundVertices, maxDistanceToConsiderVertexAsSameSquared, i);
			}

			return foundVertices;
		}

		private void AddToListIfSameEnough(List<IVertex> vertices, Vector3 position, List<IVertex> findList, double maxDistanceToConsiderVertexAsSameSquared, int i)
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

		public override int Compare(IVertex aVertex, IVertex bVertex)
		{
			Vector3 a;
			TransformVector(aVertex.Position, ref rotationToUse, out a);
			Vector3 b;
			TransformVector(bVertex.Position, ref rotationToUse, out b);
			if (a.X < b.X)
			{
				return -1;
			}
			else if (a.X == b.X)
			{
				if (a.Y < b.Y)
				{
					return -1;
				}
				else if (a.Y == b.Y)
				{
					if (a.Z < b.Z)
					{
						return -1;
					}
					else if (a.Z == b.Z)
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
}