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
using System.Diagnostics;
using System.Text;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	[DebuggerDisplay("ID = {Data.ID}")]
	public class Face
	{
		public FaceEdge firstFaceEdge;
		public Vector3 Normal { get; set; }

		public Face(Mesh containingMesh)
		{
			this.ContainingMesh = containingMesh;
		}

		// number of boundaries
		// material
		public Face(Face faceToUseAsModel, Mesh containingMesh)
			: this(containingMesh)
		{
		}

		public Mesh ContainingMesh { get; }

		public int ID => Mesh.GetID(this);

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

		public void AddDebugInfo(StringBuilder totalDebug, int numTabs)
		{
			totalDebug.Append(new string('\t', numTabs) + String.Format("First FaceEdge: {0}\n", firstFaceEdge.ID));
			firstFaceEdge.AddDebugInfo(totalDebug, numTabs + 1);
		}

		public void CalculateNormal()
		{
			FaceEdge faceEdge0 = firstFaceEdge;
			FaceEdge faceEdge1 = faceEdge0.nextFaceEdge;
			Vector3 faceEdge1Minus0 = faceEdge1.FirstVertex.Position - faceEdge0.FirstVertex.Position;
			FaceEdge faceEdge2 = faceEdge1;
			bool collinear = false;
			do
			{
				faceEdge2 = faceEdge2.nextFaceEdge;
				collinear = Vector3.Collinear(faceEdge0.FirstVertex.Position, faceEdge1.FirstVertex.Position, faceEdge2.FirstVertex.Position);
			} while (collinear && faceEdge2 != faceEdge0);
			Vector3 face2Minus0 = faceEdge2.FirstVertex.Position - faceEdge0.FirstVertex.Position;
			Normal = Vector3.Cross(faceEdge1Minus0, face2Minus0).GetNormal();
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

		public IEnumerable<FaceEdge> FaceEdges()
		{
			return firstFaceEdge.NextFaceEdges();
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			AxisAlignedBoundingBox aabb = AxisAlignedBoundingBox.Empty;
			foreach (FaceEdge faceEdge in FaceEdges())
			{
				aabb = AxisAlignedBoundingBox.Union(aabb, faceEdge.FirstVertex.Position);
			}

			return aabb;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 matrix)
		{
			AxisAlignedBoundingBox aabb = AxisAlignedBoundingBox.Empty;
			foreach (FaceEdge faceEdge in FaceEdges())
			{
				aabb = AxisAlignedBoundingBox.Union(aabb, Vector3.Transform(faceEdge.FirstVertex.Position, matrix));
			}

			return aabb;
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
					accumulatedPosition = faceEdge.FirstVertex.Position;
					first = false;
				}
				else
				{
					accumulatedPosition += faceEdge.FirstVertex.Position;
				}
			}

			return accumulatedPosition / count;
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
					prevInFront = cutPlane.GetDistanceFromPlane(prevEdge.FirstVertex.Position) > 0;
					first = false;
					firstEdge = prevEdge;
					firstInFront = prevInFront;
				}
				else
				{
					FaceEdge curEdge = faceEdge;
					bool curInFront = cutPlane.GetDistanceFromPlane(curEdge.FirstVertex.Position) > 0;
					if (prevInFront != curInFront)
					{
						// we crossed over the cut line
						Vector3 directionNormal = (curEdge.FirstVertex.Position - prevEdge.FirstVertex.Position).GetNormal();
						Ray edgeRay = new Ray(prevEdge.FirstVertex.Position, directionNormal);
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
				Vector3 directionNormal = (firstEdge.FirstVertex.Position - prevEdge.FirstVertex.Position).GetNormal();
				Ray edgeRay = new Ray(prevEdge.FirstVertex.Position, directionNormal);
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

		public ImageBuffer GetTexture(int index)
		{
			ImageBuffer image;
			if (ContainingMesh.FaceTexture.TryGetValue((this, index), out image))
			{
				return image;
			}

			return null;
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

		public void SetTexture(int index, ImageBuffer image)
		{
			if (image == null)
			{
				if (ContainingMesh.FaceTexture.ContainsKey((this, index)))
				{
					ContainingMesh.FaceTexture.Remove((this, index));
				}
			}
			else
			{
				ContainingMesh.FaceTexture[(this, index)] = image;
			}
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

		public IEnumerable<IVertex> Vertices()
		{
			foreach (FaceEdge faceEdge in FaceEdges())
			{
				yield return faceEdge.FirstVertex;
			}
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

		private int GetMajorAxis()
		{
			if (firstFaceEdge?.FirstVertex != null
				&& firstFaceEdge?.nextFaceEdge?.FirstVertex != null)
			{
				Vector3 position0 = firstFaceEdge.FirstVertex.Position;
				Vector3 position1 = firstFaceEdge.nextFaceEdge.FirstVertex.Position;
				Vector3 delta = position1 - position0;
				delta.X = Math.Abs(delta.X);
				delta.Y = Math.Abs(delta.Y);
				delta.Z = Math.Abs(delta.Z);
				if (delta.X < delta.Y && delta.X < delta.Z)
				{
					// x smallest
					return 0;
				}
				else if (delta.Y < delta.X && delta.Y < delta.Z)
				{
					return 1;
				}
			}

			return 2;
		}

		private int GetQuadrant(Vector2 positionToGetQuadantFor, double x, double y)
		{
			if (positionToGetQuadantFor.X > x)
			{
				if (positionToGetQuadantFor.Y > y)
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
				if (positionToGetQuadantFor.Y > y)
				{
					return 1;
				}
				else
				{
					return 2;
				}
			}
		}

		private double GetXIntersept(Vector2 prevPosition, Vector2 position, double y)
		{
			return position.X - (position.Y - y) * (prevPosition.X - position.X) / (prevPosition.Y - position.Y);
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

		public IEnumerable<((Vector3 p, Vector2 uv) v0, (Vector3 p, Vector2 uv) v1, (Vector3 p, Vector2 uv) v2)> AsUvTriangles()
		{
			var uvs = ContainingMesh.TextureUV;

			bool first = true;
			int vertexIndex = 0;
			(Vector3 p, Vector2 uv) firstVertex = (Vector3.Zero, Vector2.Zero);
			(Vector3 p, Vector2 uv) lastVertex = (Vector3.Zero, Vector2.Zero);
			// for now we assume the polygon is- convex and can be rendered as a fan
			foreach (var faceEdge in FaceEdges())
			{
				Vector2 uv = Vector2.Zero;
				uvs.TryGetValue((faceEdge, 0), out uv);
				var vertex = (faceEdge.FirstVertex.Position, uv);
				if (first)
				{
					firstVertex = vertex;
					first = false;
				}

				if (vertexIndex >= 2)
				{
					yield return (firstVertex, lastVertex, vertex);
				}

				lastVertex = vertex;
				vertexIndex++;
			}
		}

		public IEnumerable<(Vector3 p0, Vector3 p1, Vector3 p2)> AsTriangles()
		{
			bool first = true;
			int vertexIndex = 0;
			Vector3 firstVertex = Vector3.Zero;
			Vector3 lastVertex = Vector3.Zero;
			// for now we assume the polygon is- convex and can be rendered as a fan
			foreach (var vertex in Vertices())
			{
				if (first)
				{
					firstVertex = vertex.Position;
					first = false;
				}

				if (vertexIndex >= 2)
				{
					yield return (firstVertex, lastVertex, vertex.Position);
				}

				lastVertex = vertex.Position;
				vertexIndex++;
			}
		}
	}
}