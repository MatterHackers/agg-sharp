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

using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Text;

namespace MatterHackers.PolygonMesh
{
	[DebuggerDisplay("ID = {Data.ID}")]
	public class Face
	{
		public MetaData Data
		{
			get
			{
				return MetaData.Get(this);
			}
		}

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
			FaceTextureData faceData = FaceTextureData.Get(this);
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

		private double GetXIntersept(Vector2 prevPosition, Vector2 position, double y)
		{
			return position.x - (position.y - y) * (prevPosition.x - position.x) / (prevPosition.y - position.y);
		}

		private int WrapQuadrantDelta(int delta, Vector2 prevPosition, Vector2 position, double x, double y)
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

		private int GetQuadrant(Vector2 positionToGetQuadantFor, double x, double y)
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

		public IEnumerable<IVertex> Vertices()
		{
			foreach (FaceEdge faceEdge in FaceEdges())
			{
				yield return faceEdge.firstVertex;
			}
		}

		public IEnumerable<FaceEdge> FaceEdges()
		{
			return firstFaceEdge.NextFaceEdges();
		}

		/// <summary>
		/// Check if a point is inside the face at a given position.
		/// </summary>
		/// <param name="polyPlaneIntersection"></param>
		/// <returns></returns>
		public bool PointInPoly(Vector3 polyPlaneIntersection)
		{
			int axisOfProjection = GetMajorAxis();

			int xIndex, yIndex;
			GetAxisIndices(axisOfProjection, out xIndex, out yIndex);

			// calculate the major axis of this face
			return PointInPoly(polyPlaneIntersection[xIndex], polyPlaneIntersection[yIndex], axisOfProjection);
        }

		private int GetMajorAxis()
		{
			if (firstFaceEdge?.firstVertex != null
                && firstFaceEdge?.nextFaceEdge?.firstVertex != null)
			{
				Vector3 position0 = firstFaceEdge.firstVertex.Position;
				Vector3 position1 = firstFaceEdge.nextFaceEdge.firstVertex.Position;
				Vector3 delta = position1 - position0;
				delta.x = Math.Abs(delta.x);
				delta.y = Math.Abs(delta.y);
				delta.z = Math.Abs(delta.z);
				if (delta.x < delta.y && delta.x < delta.z)
				{
					// x smallest
					return 0;
				}
				else if(delta.y < delta.x && delta.y < delta.z)
				{
					return 1;
				}
			}

			return 2;
        }

		/// <summary>
		/// Check if a point is inside the face at a given position in x, y or z
		/// </summary>
		/// <param name="x"></param>
		/// <param name="y"></param>
		/// <param name="axisOfProjection"></param>
		/// <returns></returns>
		public bool PointInPoly(double x, double y, int axisOfProjection)
		{
			int xIndex, yIndex;
			GetAxisIndices(axisOfProjection, out xIndex, out yIndex);

			int accumulatedQuadrantAngle = 0;
			int prevQuadrant = 0;
			Vector2 prevPosition = Vector2.Zero;
			bool foundFirst = false;
			Vector2 firstPosition = Vector2.Zero;
			int quadrant = 0;
            foreach (IVertex vertex in Vertices())
			{
				Vector2 position = new Vector2(vertex.Position[xIndex], vertex.Position[yIndex]);
				quadrant = GetQuadrant(position, x, y);

				if (foundFirst)
				{
					accumulatedQuadrantAngle += WrapQuadrantDelta(quadrant - prevQuadrant, prevPosition, position, x, y);
				}
				else
				{
					firstPosition = position;
					foundFirst = true;
				}

				prevPosition = position;
				prevQuadrant = quadrant;
			}

			quadrant = GetQuadrant(firstPosition, x, y);
			accumulatedQuadrantAngle += WrapQuadrantDelta(quadrant - prevQuadrant, prevPosition, firstPosition, x, y);

			// complete 360 degrees (angle of + 4 or -4 ) means inside
			if ((accumulatedQuadrantAngle == 4) || (accumulatedQuadrantAngle == -4))
			{
				return true;
			}

			return false;
		}

		private static void GetAxisIndices(int axisOfProjection, out int xIndex, out int yIndex)
		{
			// set the major axis of projection (defaults to z)
			xIndex = 0;
			yIndex = 1;
			if (axisOfProjection == 0) // x
			{
				xIndex = 1;
				yIndex = 2;
			}
			else if (axisOfProjection == 1) // y
			{
				xIndex = 0;
				yIndex = 2;
			}
		}

		public void CalculateNormal()
		{
			FaceEdge faceEdge0 = firstFaceEdge;
			FaceEdge faceEdge1 = faceEdge0.nextFaceEdge;
			Vector3 faceEdge1Minus0 = faceEdge1.firstVertex.Position - faceEdge0.firstVertex.Position;
			FaceEdge faceEdge2 = faceEdge1;
			bool collinear = false;
			do
			{
				faceEdge2 = faceEdge2.nextFaceEdge;
				collinear = Vector3.Collinear(faceEdge0.firstVertex.Position, faceEdge1.firstVertex.Position, faceEdge2.firstVertex.Position);
			} while (collinear && faceEdge2 != faceEdge0);
			Vector3 face2Minus0 = faceEdge2.firstVertex.Position - faceEdge0.firstVertex.Position;
			normal = Vector3.Cross(faceEdge1Minus0, face2Minus0).GetNormal();
		}

		public bool FaceEdgeLoopIsGood()
		{
			foreach (FaceEdge faceEdge in FaceEdges())
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
			foreach (FaceEdge faceEdge in firstFaceEdge.NextFaceEdges())
			{
				nextList.Add(faceEdge);
			}

			int index = nextList.Count;
			foreach (FaceEdge faceEdge in firstFaceEdge.PrevFaceEdges())
			{
				int validIndex = (index--) % nextList.Count;
				if (faceEdge != nextList[validIndex])
				{
					throw new Exception("The next and prev sets must be mirrors.");
				}
			}

			nextList.Clear();
			foreach (FaceEdge faceEdge in firstFaceEdge.RadialNextFaceEdges())
			{
				nextList.Add(faceEdge);
			}

			index = nextList.Count;
			foreach (FaceEdge faceEdge in firstFaceEdge.RadialPrevFaceEdges())
			{
				int validIndex = (index--) % nextList.Count;
				if (faceEdge != nextList[validIndex])
				{
					throw new Exception("The next and prev sets must be mirrors.");
				}
			}
		}

		public bool GetCutLine(Plane cutPlane, out Vector3 start, out Vector3 end)
		{
			start = new Vector3();
			end = new Vector3();
			int splitCount = 0;
			FaceEdge prevEdge = null;
			bool prevInFront = false;
			bool first = true;
			FaceEdge firstEdge = null;
			bool firstInFront = false;
			foreach (FaceEdge faceEdge in FaceEdges())
			{
				if (first)
				{
					prevEdge = faceEdge;
					prevInFront = cutPlane.GetDistanceFromPlane(prevEdge.firstVertex.Position) > 0;
					first = false;
					firstEdge = prevEdge;
					firstInFront = prevInFront;
				}
				else
				{
					FaceEdge curEdge = faceEdge;
					bool curInFront = cutPlane.GetDistanceFromPlane(curEdge.firstVertex.Position) > 0;
					if (prevInFront != curInFront)
					{
						// we crossed over the cut line
						Vector3 directionNormal = (curEdge.firstVertex.Position - prevEdge.firstVertex.Position).GetNormal();
						Ray edgeRay = new Ray(prevEdge.firstVertex.Position, directionNormal);
						double distanceToHit;
						bool hitFrontOfPlane;
						if (cutPlane.RayHitPlane(edgeRay, out distanceToHit, out hitFrontOfPlane))
						{
							splitCount++;
							if (splitCount == 1)
							{
								start = edgeRay.origin + edgeRay.directionNormal * distanceToHit;
							}
							else
							{
								end = edgeRay.origin + edgeRay.directionNormal * distanceToHit;
							}
						}
					}

					prevEdge = curEdge;
					prevInFront = curInFront;
					if (splitCount == 2)
					{
						break;
					}
				}
			}

			if (splitCount == 1
				&& prevInFront != firstInFront)
			{
				// we crossed over the cut line
				Vector3 directionNormal = (firstEdge.firstVertex.Position - prevEdge.firstVertex.Position).GetNormal();
				Ray edgeRay = new Ray(prevEdge.firstVertex.Position, directionNormal);
				double distanceToHit;
				bool hitFrontOfPlane;
				if (cutPlane.RayHitPlane(edgeRay, out distanceToHit, out hitFrontOfPlane))
				{
					splitCount++;
					end = edgeRay.origin + edgeRay.directionNormal * distanceToHit;
				}
			}

			if (splitCount == 2)
			{
				return true;
			}

			return false;
		}

		public Vector3 GetCenter()
		{
			bool first = true;
			Vector3 accumulatedPosition = Vector3.Zero;
			int count = 0;
			foreach (FaceEdge faceEdge in FaceEdges())
			{
				count++;
				if (first)
				{
					accumulatedPosition = faceEdge.firstVertex.Position;
					first = false;
				}
				else
				{
					accumulatedPosition += faceEdge.firstVertex.Position;
				}
			}

			return accumulatedPosition / count;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			AxisAlignedBoundingBox aabb = AxisAlignedBoundingBox.Empty;
			foreach (FaceEdge faceEdge in FaceEdges())
			{
				aabb = AxisAlignedBoundingBox.Union(aabb, faceEdge.firstVertex.Position);
            }

			return aabb;
		}
	}
}