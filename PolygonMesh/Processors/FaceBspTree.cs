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
		public static BspNode Create(Mesh mesh, int maxFacesToTest = 10, bool tryToBalanceTree = false)
		{
			BspNode root = new BspNode();

			var faces = new List<Face>(mesh.Faces);

			CreateNoSplitingFast(mesh.Faces, root, faces, maxFacesToTest, tryToBalanceTree);

			return root;
		}

		internal class BCF
		{
			internal BspNode behind;
			internal BspNode current;
			internal BspNode infront;

			public BCF(BspNode behind, BspNode current, BspNode infront)
			{
				this.behind = behind;
				this.current = current;
				this.infront = infront;
			}
		}
		/// <summary>
		/// Get an ordered list of the faces to render based on the camera position.
		/// </summary>
		/// <param name="node"></param>
		/// <param name="meshToViewTransform"></param>
		/// <param name="invMeshToViewTransform"></param>
		/// <param name="faceRenderOrder"></param>
		public static IEnumerable<Face> GetFacesInVisibiltyOrder(List<Face> meshFaces, BspNode root, Matrix4X4 meshToViewTransform, Matrix4X4 invMeshToViewTransform)
		{
			var processedBack = new HashSet<BspNode>();
			var renderOrder = new Stack<BspNode>(new BspNode[] { root.RenderOrder(meshFaces, meshToViewTransform, invMeshToViewTransform) });

			do
			{
				var lastBack = renderOrder.Peek().BackNode;
				while (lastBack != null
					&& lastBack.Index != -1
					&& !processedBack.Contains(lastBack))
				{
					processedBack.Add(lastBack);
					renderOrder.Push(lastBack.RenderOrder(meshFaces, meshToViewTransform, invMeshToViewTransform));
					lastBack = renderOrder.Peek().BackNode;
				}

				var node = renderOrder.Pop();
				if (node.Index != -1)
				{
					yield return meshFaces[node.Index];
				}
				var lastFront = node.FrontNode;
				if (lastFront != null && lastFront.Index != -1)
				{
					renderOrder.Push(lastFront.RenderOrder(meshFaces, meshToViewTransform, invMeshToViewTransform));
				}
			} while (renderOrder.Any());
		}

		private static (double, int) CalculateCrosingArrea(int faceIndex, List<Face> faces, double smallestCrossingArrea)
		{
			double negativeDistance = 0;
			double positiveDistance = 0;
			int negativeSideCount = 0;
			int positiveSideCount = 0;

			Face checkFace = faces[faceIndex];
			var pointOnCheckFace = faces[faceIndex].firstFaceEdge.FirstVertex.Position;

			for (int i = 0; i < faces.Count; i++)
			{
				if (i < faces.Count && i != faceIndex)
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

							if (negativeDistance > smallestCrossingArrea
								&& positiveDistance > smallestCrossingArrea)
							{
								return (double.MaxValue, int.MaxValue);
							}
						}
					}

					if (negativeDistance > positiveDistance)
					{
						negativeSideCount++;
					}
					else
					{
						positiveSideCount++;
					}
				}
			}

			// return whatever side is small as our rating of badness (0 being good)
			return (Math.Min(negativeDistance, positiveDistance), Math.Abs(negativeSideCount - positiveSideCount));
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

		private static void CreateNoSplitingFast(List<Face> sourceFaces, BspNode node, List<Face> faces, int maxFacesToTest, bool tryToBalanceTree)
		{
			if (faces.Count == 0)
			{
				return;
			}

			int bestFaceIndex = -1;
			double smallestCrossingArrea = double.MaxValue;
			int bestBalance = int.MaxValue;

			// find the first face that does not split anything
			int step = Math.Max(1, faces.Count / maxFacesToTest);
			for (int i = 0; i < faces.Count; i += step)
			{
				// calculate how much of polygons cross this face
				(double crossingArrea, int balance) = CalculateCrosingArrea(i, faces, smallestCrossingArrea);
				// keep track of the best face so far
				if (crossingArrea < smallestCrossingArrea)
				{
					smallestCrossingArrea = crossingArrea;
					bestBalance = balance;
					bestFaceIndex = i;
					if (crossingArrea == 0
						&& !tryToBalanceTree)
					{
						break;
					}
				}
				else if (crossingArrea == smallestCrossingArrea
					&& balance < bestBalance)
				{
					// the crossing area is the same but the tree balance is better
					bestBalance = balance;
					bestFaceIndex = i;
				}
			}

			node.Index = sourceFaces.IndexOf(faces[bestFaceIndex]);

			// put the behind stuff in a list
			List<Face> backFaces = new List<Face>();
			List<Face> frontFaces = new List<Face>();
			CreateBackAndFrontFaceLists(bestFaceIndex, faces, backFaces, frontFaces);

			CreateNoSplitingFast(sourceFaces, node.BackNode = new BspNode(), backFaces, maxFacesToTest, tryToBalanceTree);
			CreateNoSplitingFast(sourceFaces, node.FrontNode = new BspNode(), frontFaces, maxFacesToTest, tryToBalanceTree);
		}
	}

	public class BspNode
	{
		public BspNode BackNode { get; internal set; }
		public BspNode FrontNode { get; internal set; }
		public int Index { get; internal set; } = -1;
	}

	public static class BspNodeExtensions
	{ 
		public static BspNode RenderOrder(this BspNode node, List<Face> meshFaces, Matrix4X4 meshToViewTransform, Matrix4X4 invMeshToViewTransform)
		{
			var faceNormalInViewSpace = Vector3.TransformNormalInverse(meshFaces[node.Index].Normal, invMeshToViewTransform);
			var pointOnFaceInViewSpace = Vector3.Transform(meshFaces[node.Index].firstFaceEdge.FirstVertex.Position, meshToViewTransform);
			var infrontOfFace = Vector3.Dot(faceNormalInViewSpace, pointOnFaceInViewSpace) < 0;

			if (infrontOfFace)
			{
				return new BspNode()
				{
					Index = node.Index,
					BackNode = node.BackNode,
					FrontNode = node.FrontNode
				};
			}
			else
			{
				return new BspNode()
				{
					Index = node.Index,
					BackNode = node.FrontNode,
					FrontNode = node.BackNode
				};
			}
		}
	}
}