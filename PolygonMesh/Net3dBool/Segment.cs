/*
The MIT License (MIT)

Copyright (c) 2014 Sebastian Loncar

Permission is hereby granted, free of charge, to any person obtaining a copy
of this software and associated documentation files (the "Software"), to deal
in the Software without restriction, including without limitation the rights
to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
copies of the Software, and to permit persons to whom the Software is
furnished to do so, subject to the following conditions:

The above copyright notice and this permission notice shall be included in all
copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
SOFTWARE.

See:
D. H. Laidlaw, W. B. Trumbore, and J. F. Hughes.
"Constructive Solid Geometry for Polyhedral Objects"
SIGGRAPH Proceedings, 1986, p.161.

original author: Danilo Balby Silva Castanheira (danbalby@yahoo.com)

Ported from Java to C# by Sebastian Loncar, Web: http://loncar.de
Optomized and refactored by: Lars Brubaker (larsbrubaker@matterhackers.com)
Project: https://github.com/MatterHackers/agg-sharp (an included library)
*/

using MatterHackers.VectorMath;
using System;
using System.Collections;
using System.Collections.Generic;

namespace Net3dBool
{
	public enum SegmentEnd { Vertex, Face, Edge };

	/// <summary>
	/// Represents a line segment resulting from a intersection of a face and a plane.
	/// </summary>
	public class Segment
	{
		/** line resulting from the two planes intersection */
		private Line line;
		/** shows how many ends were already defined */
		private int index;

		/** distance from the segment starting point to the point defining the plane */
		public double StartDistance { get; private set; }
		/** distance from the segment ending point to the point defining the plane */
		public double EndDistance { get; private set; }

		/** starting point status relative to the face */
		public SegmentEnd StartType { get; private set; }
		/** intermediate status relative to the face */
		public SegmentEnd MiddleType { get; private set; }
		/** ending point status relative to the face */
		public SegmentEnd EndType { get; private set; }

		/** nearest vertex from the starting point */
		public Vertex StartVertex { get; private set; }
		/** nearest vertex from the ending point */
		public Vertex EndVertex { get; private set; }

		/** start of the intersection point */
		public Vector3 StartPosition { get; private set; }
		/** end of the intersection point */
		public Vector3 EndPosition { get; private set; }

		/** tolerance value to test equalities */
		private static double TOL = 1e-10f;

		//---------------------------------CONSTRUCTORS---------------------------------//

		/**
     * Constructs a Segment based on elements obtained from the two planes relations 
     * 
     * @param line resulting from the two planes intersection
     * @param face face that intersects with the plane
     * @param sign1 position of the face vertex1 relative to the plane (-1 behind, 1 front, 0 on)
     * @param sign2 position of the face vertex1 relative to the plane (-1 behind, 1 front, 0 on)
     * @param sign3 position of the face vertex1 relative to the plane (-1 behind, 1 front, 0 on)  
     */
		public Segment(Line line, Face face, PlaneSide side1, PlaneSide side2, PlaneSide side3)
		{
			this.line = line;
			index = 0;

			//VERTEX is an end
			if (side1 == PlaneSide.On)
			{
				SetVertex(face.v1);
				//other vertices on the same side - VERTEX-VERTEX VERTEX
				if (side2 == side3)
				{
					SetVertex(face.v1);
				}
			}

			//VERTEX is an end
			if (side2 == PlaneSide.On)
			{
				SetVertex(face.v2);
				//other vertices on the same side - VERTEX-VERTEX VERTEX
				if (side1 == side3)
				{
					SetVertex(face.v2);
				}
			}

			//VERTEX is an end
			if (side3 == PlaneSide.On)
			{
				SetVertex(face.v3);
				//other vertices on the same side - VERTEX-VERTEX VERTEX
				if (side1 == side2)
				{
					SetVertex(face.v3);
				}
			}

			//There are undefined ends - one or more edges cut the planes intersection line
			if (GetNumEndsSet() != 2)
			{
				//EDGE is an end
				if ((side1 == PlaneSide.Front && side2 == PlaneSide.Back) || (side1 == PlaneSide.Back && side2 == PlaneSide.Front))
				{
					SetEdge(face.v1, face.v2);
				}
				//EDGE is an end
				if ((side2 == PlaneSide.Front && side3 == PlaneSide.Back) || (side2 == PlaneSide.Back && side3 == PlaneSide.Front))
				{
					SetEdge(face.v2, face.v3);
				}
				//EDGE is an end
				if ((side3 == PlaneSide.Front && side1 == PlaneSide.Back) || (side3 == PlaneSide.Back && side1 == PlaneSide.Front))
				{
					SetEdge(face.v3, face.v1);
				}
			}
		}

		private Segment()
		{
		}

		/**
     * Gets the number of ends already set
     *
     * @return number of ends already set
     */
		public int GetNumEndsSet()
		{
			return index;
		}

		//------------------------------------OTHERS------------------------------------//

		/**
     * Checks if two segments intersect
     * 
     * @param segment the other segment to check the intesection
     * @return true if the segments intersect, false otherwise
     */
		public bool Intersect(Segment segment)
		{
			if (EndDistance < segment.StartDistance + TOL || segment.EndDistance < StartDistance + TOL)
			{
				return false;
			}
			else
			{
				return true;
			}
		}

		//---------------------------------PRIVATES-------------------------------------//

		/**
     * Sets an end as vertex (starting point if none end were defined, ending point otherwise)
     * 
     * @param vertex the vertex that is an segment end 
     * @return false if all the ends were already defined, true otherwise
     */
		private bool SetVertex(Vertex vertex)
		{
			//none end were defined - define starting point as VERTEX
			if (index == 0)
			{
				StartVertex = vertex;
				StartType = SegmentEnd.Vertex;
				StartDistance = line.ComputePointToPointDistance(vertex.GetPosition());
				StartPosition = StartVertex.GetPosition();
				index++;
				return true;
			}
			//starting point were defined - define ending point as VERTEX
			if (index == 1)
			{
				EndVertex = vertex;
				EndType = SegmentEnd.Vertex;
				EndDistance = line.ComputePointToPointDistance(vertex.GetPosition());
				EndPosition = EndVertex.GetPosition();
				index++;

				//defining middle based on the starting point
				//VERTEX-VERTEX-VERTEX
				if (StartVertex.Equals(EndVertex))
				{
					MiddleType = SegmentEnd.Vertex;
				}
				//VERTEX-EDGE-VERTEX
				else if (StartType == SegmentEnd.Vertex)
				{
					MiddleType = SegmentEnd.Edge;
				}

				//the ending point distance should be smaller than  starting point distance 
				if (StartDistance > EndDistance)
				{
					SwapEnds();
				}

				return true;
			}
			else
			{
				return false;
			}
		}

		/**
     * Sets an end as edge (starting point if none end were defined, ending point otherwise)
     * 
     * @param vertex1 one of the vertices of the intercepted edge 
     * @param vertex2 one of the vertices of the intercepted edge
     * @return false if all ends were already defined, true otherwise
     */
		private bool SetEdge(Vertex vertex1, Vertex vertex2)
		{
			Vector3 point1 = vertex1.GetPosition();
			Vector3 point2 = vertex2.GetPosition();
			Vector3 edgeDirection = new Vector3(point2.X - point1.X, point2.Y - point1.Y, point2.Z - point1.Z);
			Line edgeLine = new Line(edgeDirection, point1);

			if (index == 0)
			{
				StartVertex = vertex1;
				StartType = SegmentEnd.Edge;
				StartPosition = line.ComputeLineIntersection(edgeLine);
				StartDistance = line.ComputePointToPointDistance(StartPosition);
				MiddleType = SegmentEnd.Face;
				index++;
				return true;
			}
			else if (index == 1)
			{
				EndVertex = vertex1;
				EndType = SegmentEnd.Edge;
				EndPosition = line.ComputeLineIntersection(edgeLine);
				EndDistance = line.ComputePointToPointDistance(EndPosition);
				MiddleType = SegmentEnd.Face;
				index++;

				//the ending point distance should be smaller than  starting point distance 
				if (StartDistance > EndDistance)
				{
					SwapEnds();
				}

				return true;
			}
			else
			{
				return false;
			}
		}

		/** Swaps the starting point and the ending point */
		private void SwapEnds()
		{
			double distTemp = StartDistance;
			StartDistance = EndDistance;
			EndDistance = distTemp;

			SegmentEnd typeTemp = StartType;
			StartType = EndType;
			EndType = typeTemp;

			Vertex vertexTemp = StartVertex;
			StartVertex = EndVertex;
			EndVertex = vertexTemp;

			Vector3 posTemp = StartPosition;
			StartPosition = EndPosition;
			EndPosition = posTemp;
		}
	}
}

