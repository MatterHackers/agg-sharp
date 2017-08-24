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
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public static class FaceBspTree
	{
		private static readonly double considerCoplaner = .1;

		/// <summary>
		/// This function will search for the first face that produces no polygon cuts
		/// and split the tree on it. If it can't find a non-cutting face,
		/// it will split on the face that minimizes the area that it devides.
		/// </summary>
		/// <param name="mesh"></param>
		/// <returns></returns>
		public static FaceBspNode Create(Mesh mesh)
		{
			FaceBspNode root = new FaceBspNode();

			var faces = new List<Face>(mesh.Faces);

			CreateNoSplitingFast(root, faces);

			return root;
		}

		public static IEnumerable<Face> GetFacesInVisibiltyOrder(FaceBspNode root, WorldView worldView)
		{
			// return all the back faces
			foreach (var face in GetFacesInVisibiltyOrder(root.BackNode, worldView))
			{
				yield return face;
			}

			// return this face
			yield return root.Face;

			// return all the front faces
			foreach (var face in GetFacesInVisibiltyOrder(root.BackNode, worldView))
			{
				yield return face;
			}
		}

		private static double CalculateCrosingArrea(int faceIndex, List<Face> faces, double smallestCrossingArrea)
		{
			double negativeDistance = 0;
			double positiveDistance = 0;

			Face checkFace = faces[faceIndex];
			var pointOnCheckFace = faces[faceIndex].Vertices().FirstOrDefault().Position;
			int step = faces.Count / 10;
			if(step < 10)
			{
				step = 1;
			}
			for (int i = 0; i < faces.Count; i += step)
			{
				if (i != faceIndex)
				{
					foreach (var vertex in faces[i].Vertices())
					{
						double distanceToPlan = Vector3.Dot(checkFace.Normal, vertex.Position - pointOnCheckFace);
						if (Math.Abs(distanceToPlan) > considerCoplaner)
						{
							if (distanceToPlan < 0)
							{
								// Take the square of thi distance to penalize far away points
								negativeDistance += (distanceToPlan * distanceToPlan);
							}
							else
							{
								positiveDistance += (distanceToPlan * distanceToPlan);
							}

							if(negativeDistance > smallestCrossingArrea 
								&& positiveDistance > smallestCrossingArrea)
							{
								return negativeDistance;
							}
						}
					}
				}
			}

			// return whatever side is small as our rating of badness (0 being good)
			return Math.Min(negativeDistance, positiveDistance);
		}

		private static void CreateBackAndFrontFaceLists(int faceIndex, List<Face> faces, List<Face> backFaces, List<Face> frontFaces)
		{
			Face checkFace = faces[faceIndex];
			var pointOnCheckFace = faces[faceIndex].Vertices().FirstOrDefault().Position;
			for (int i = 0; i < faces.Count; i++)
			{
				if (i != faceIndex)
				{
					bool backFace = true;
					foreach (var vertex in faces[i].Vertices())
					{
						double distanceToPlan = Vector3.Dot(checkFace.Normal, vertex.Position - pointOnCheckFace);
						if (Math.Abs(distanceToPlan) > considerCoplaner)
						{
							if (distanceToPlan > 0)
							{
								backFace = false;
							}
						}
					}

					if (backFace)
					{
						// it is a back face
						backFaces.Add(faces[i]);
					}
					else
					{
						// it is a front face
						frontFaces.Add(faces[i]);
					}
				}
			}
		}

		private static void CreateNoSplitingFast(FaceBspNode node, List<Face> faces)
		{
			if (faces.Count == 0)
			{
				return;
			}

			int bestFaceIndex = -1;
			double smallestCrossingArrea = double.MaxValue;

			// find the first face that does not split anything
			for (int i = 0; i < faces.Count; i++)
			{
				// calculate how much of polygons cross this face
				double crossingArrea = CalculateCrosingArrea(i, faces, smallestCrossingArrea);
				// keep track of the best face so far
				if (crossingArrea < smallestCrossingArrea)
				{
					smallestCrossingArrea = crossingArrea;
					bestFaceIndex = i;
					if (crossingArrea == 0)
					{
						// this face is good enough (not balanced but not crossing anything)
						break;
					}
				}
			}

			node.Face = faces[bestFaceIndex];

			// put the behind stuff in a list
			List<Face> backFaces = new List<Face>();
			List<Face> frontFaces = new List<Face>();
			CreateBackAndFrontFaceLists(bestFaceIndex, faces, backFaces, frontFaces);

			CreateNoSplitingFast(node.BackNode = new FaceBspNode(), backFaces);
			CreateNoSplitingFast(node.FrontNode = new FaceBspNode(), frontFaces);
		}
	}

	public class FaceBspNode
	{
		public Face Face;
		public FaceBspNode BackNode { get; set; }
		public FaceBspNode FrontNode { get; set; }
	}
}