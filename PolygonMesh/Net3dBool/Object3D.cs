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
Optimized and refactored by: Lars Brubaker (larsbrubaker@matterhackers.com)
Project: https://github.com/MatterHackers/agg-sharp (an included library)
*/

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MatterHackers.VectorMath;

namespace Net3dBool
{
	public enum PlaneSide { Back, On, Front };
	/// <summary>
	/// Data structure about a 3d solid to apply boolean operations in it.
	/// Tipically, two 'Object3d' objects are created to apply boolean operation. The
	/// methods splitFaces() and classifyFaces() are called in this sequence for both objects,
	/// always using the other one as parameter.Then the faces from both objects are collected
	/// according their status.
	/// </summary>
	public class Object3D
	{
		/// <summary>
		/// solid faces
		/// </summary>
		public Octree<Face> Faces;

		/// <summary>
		/// tolerance value to test equalities
		/// </summary>
		private readonly static double EqualityTolerance = 1e-10f;

		private Dictionary<long, int> addedVertices = new Dictionary<long, int>();

		/// <summary>
		/// object representing the solid extremes
		/// </summary>
		private AxisAlignedBoundingBox bound;

		/// <summary>
		/// solid vertices
		/// </summary>
		private List<Vertex> vertices;

		//BoundsOctree<int> octree = new BoundsOctree<int>();

		/// <summary>
		/// Constructs a Object3d object based on a solid file.
		/// </summary>
		/// <param name="solid">solid used to construct the Object3d object</param>
		public Object3D(Solid solid)
		{
			Vertex v1, v2, v3, vertex;
			Vector3[] verticesPoints = solid.getVertices();
			int[] indices = solid.getIndices();
			var verticesTemp = new List<Vertex>();

			AxisAlignedBoundingBox totalBounds = new AxisAlignedBoundingBox(verticesPoints[0], verticesPoints[0]);
			//create vertices
			vertices = new List<Vertex>();
			for (int i = 0; i < verticesPoints.Length; i++)
			{
				vertex = AddVertex(verticesPoints[i], Status.UNKNOWN);
				totalBounds.ExpandToInclude(verticesPoints[i]);
				verticesTemp.Add(vertex);
			}

			//create faces
			totalBounds.Expand(1);
			Faces = new Octree<Face>(5, new Bounds(totalBounds));
			for (int i = 0; i < indices.Length; i = i + 3)
			{
				v1 = verticesTemp[indices[i]];
				v2 = verticesTemp[indices[i + 1]];
				v3 = verticesTemp[indices[i + 2]];
				AddFace(v1, v2, v3);
			}

			//create bound
			bound = new AxisAlignedBoundingBox(verticesPoints);
		}

		/// <summary>
		/// Default constructor
		/// </summary>
		private Object3D()
		{
		}

		/// <summary>
		/// Classify faces as being inside, outside or on boundary of other object
		/// </summary>
		/// <param name="otherObject">object 3d used for the comparison</param>
		public void ClassifyFaces(Object3D otherObject)
		{
			//calculate adjacency information
			foreach (Face face in Faces.AllObjects())
			{
				face.v1.AddAdjacentVertex(face.v2);
				face.v1.AddAdjacentVertex(face.v3);
				face.v2.AddAdjacentVertex(face.v1);
				face.v2.AddAdjacentVertex(face.v3);
				face.v3.AddAdjacentVertex(face.v1);
				face.v3.AddAdjacentVertex(face.v2);
			}

			//for each face
			foreach (Face face in Faces.AllObjects())
			{
				//if the face vertices aren't classified to make the simple classify
				if (face.SimpleClassify() == false)
				{
					//makes the ray trace classification
					face.RayTraceClassify(otherObject);

					//mark the vertices
					if (face.v1.GetStatus() == Status.UNKNOWN)
					{
						face.v1.Mark(face.GetStatus());
					}
					if (face.v2.GetStatus() == Status.UNKNOWN)
					{
						face.v2.Mark(face.GetStatus());
					}
					if (face.v3.GetStatus() == Status.UNKNOWN)
					{
						face.v3.Mark(face.GetStatus());
					}
				}
			}
		}

		/// <summary>
		/// Gets the solid bound
		/// </summary>
		/// <returns>solid bound</returns>
		public AxisAlignedBoundingBox GetBound()
		{
			return bound;
		}

		/// <summary>
		/// Inverts faces classified as INSIDE, making its normals point outside. Usually used into the second solid when the difference is applied.
		/// </summary>
		public void InvertInsideFaces()
		{
			foreach (Face face in Faces.AllObjects())
			{
				if (face.GetStatus() == Status.INSIDE)
				{
					face.Invert();
				}
			}
		}

		/// <summary>
		/// Split faces so that no face is intercepted by a face of other object
		/// </summary>
		/// <param name="compareObject">the other object 3d used to make the split</param>
		public void SplitFaces(Object3D compareObject, CancellationToken cancellationToken,
			Action<Vector3[], Vector3[]> splitFaces = null,
			Action<List<Vector3[]>> results = null)
		{
			Stack<Face> newFacesFromSplitting = new Stack<Face>();

			PlaneSide sideFace1Vert1, sideFace1Vert2, sideFace1Vert3, signFace2Vert1, signFace2Vert2, signFace2Vert3;
			int numFacesStart = this.Faces.Count;

			//if the objects bounds overlap...
			//for each object1 face...
			var bounds = new Bounds(compareObject.GetBound());
			foreach (Face thisFaceIn in Faces.SearchBounds(bounds).ToArray()) // put it in an array as we will be adding new faces to it
			{
				newFacesFromSplitting.Push(thisFaceIn);
				// make sure we processe every face that we have added durring splitting befor moving on to the next face
				while (newFacesFromSplitting.Count > 0)
				{
					var faceToSplit = newFacesFromSplitting.Pop();

					cancellationToken.ThrowIfCancellationRequested();

					//if object1 face bound and object2 bound overlap ...
					//for each object2 face...
					foreach (Face cuttingFace in compareObject.Faces.SearchBounds(new Bounds(faceToSplit.GetBound())))
					{
						//if object1 face bound and object2 face bound overlap...
						//PART I - DO TWO POLIGONS INTERSECT?
						//POSSIBLE RESULTS: INTERSECT, NOT_INTERSECT, COPLANAR

						//distance from the face1 vertices to the face2 plane
						double v1DistToCuttingFace = cuttingFace.DistanceFromPlane(faceToSplit.v1);
						double v2DistToCuttingFace = cuttingFace.DistanceFromPlane(faceToSplit.v2);
						double v3DistToCuttingFace = cuttingFace.DistanceFromPlane(faceToSplit.v3);

						//distances signs from the face1 vertices to the face2 plane
						sideFace1Vert1 = (v1DistToCuttingFace > EqualityTolerance ? PlaneSide.Front : (v1DistToCuttingFace < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
						sideFace1Vert2 = (v2DistToCuttingFace > EqualityTolerance ? PlaneSide.Front : (v2DistToCuttingFace < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
						sideFace1Vert3 = (v3DistToCuttingFace > EqualityTolerance ? PlaneSide.Front : (v3DistToCuttingFace < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));

						//if all the signs are zero, the planes are coplanar
						//if all the signs are positive or negative, the planes do not intersect
						//if the signs are not equal...
						if (!(sideFace1Vert1 == sideFace1Vert2 && sideFace1Vert2 == sideFace1Vert3))
						{
							//distance from the face2 vertices to the face1 plane
							double faceToSplitTo1 = faceToSplit.DistanceFromPlane(cuttingFace.v1);
							double faceToSplitTo2 = faceToSplit.DistanceFromPlane(cuttingFace.v2);
							double faceToSplitTo3 = faceToSplit.DistanceFromPlane(cuttingFace.v3);

							//distances signs from the face2 vertices to the face1 plane
							signFace2Vert1 = (faceToSplitTo1 > EqualityTolerance ? PlaneSide.Front : (faceToSplitTo1 < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
							signFace2Vert2 = (faceToSplitTo2 > EqualityTolerance ? PlaneSide.Front : (faceToSplitTo2 < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
							signFace2Vert3 = (faceToSplitTo3 > EqualityTolerance ? PlaneSide.Front : (faceToSplitTo3 < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));

							//if the signs are not equal...
							if (!(signFace2Vert1 == signFace2Vert2 && signFace2Vert2 == signFace2Vert3))
							{
								var line = new Line(faceToSplit, cuttingFace);

								//intersection of the face1 and the plane of face2
								var segment1 = new Segment(line, faceToSplit, sideFace1Vert1, sideFace1Vert2, sideFace1Vert3);

								//intersection of the face2 and the plane of face1
								var segment2 = new Segment(line, cuttingFace, signFace2Vert1, signFace2Vert2, signFace2Vert3);

								//if the two segments intersect...
								if (segment1.Intersect(segment2))
								{
									//PART II - SUBDIVIDING NON-COPLANAR POLYGONS
									Stack<Face> facesFromSplit = new Stack<Face>();

									if (this.SplitFace(faceToSplit, segment1, segment2, facesFromSplit))
									{
										if (facesFromSplit.Count > 0)
										{
											foreach (var face in facesFromSplit)
											{
												newFacesFromSplitting.Push(face);
											}

											splitFaces?.Invoke(faceToSplit.Positions(), cuttingFace.Positions());

											results?.Invoke(facesFromSplit.Select((f) => f.Positions()).ToList());

											//prevent from infinite loop (with a loss of faces...)
											if (Faces.Count > numFacesStart * 100)
											{
												//System.out.println("possible infinite loop situation: terminating faces split");
												//return;
											}

											break;
										}
									}
								}
							}
						}
					}
				}
			}
		}

		private Face AddFace(Vertex v1, Vertex v2, Vertex v3)
		{
			if (!(v1.Equals(v2) || v1.Equals(v3) || v2.Equals(v3)))
			{
				Face face = new Face(v1, v2, v3);
				if (face.GetArea() > EqualityTolerance)
				{
					Faces.Insert(face, new Bounds(face.GetBound()));

					return face;
				}
			}

			return null;
		}

		/// <summary>
		/// Method used to add a face properly for internal methods
		/// </summary>
		/// <param name="v1">a face vertex</param>
		/// <param name="v2">a face vertex</param>
		/// <param name="v3">a face vertex</param>
		/// <returns></returns>
		private Face AddFaceFromSplit(Vertex v1, Vertex v2, Vertex v3, Face faceBeingSplit, Stack<Face> facesFromSplit)
		{
			if (!(v1.Equals(v2) || v1.Equals(v3) || v2.Equals(v3)))
			{
				Face face = new Face(v1, v2, v3);
				if (face.GetArea() > EqualityTolerance)
				{
					if (!faceBeingSplit.Equals(face))
					{
						bool exists = false;
						foreach (var test in facesFromSplit)
						{
							if (test.Equals(face))
							{
								exists = true;
								break;
							}
						}
						if (!exists)
						{
							Faces.Insert(face, new Bounds(face.GetBound()));
							facesFromSplit.Push(face);
						}
					}

					return face;
				}
			}

			return null;
		}

		/// <summary>
		/// Method used to add a vertex properly for internal methods
		/// </summary>
		/// <param name="pos">vertex position</param>
		/// <param name="status">vertex status</param>
		/// <returns>The vertex inserted (if a similar vertex already exists, this is returned).</returns>
		private Vertex AddVertex(Vector3 pos, Status status)
		{
			Vertex vertex;

			// if the vertex is already there, it is not inserted
			if (!addedVertices.TryGetValue(pos.GetLongHashCode(), out int position))
			{
				position = vertices.Count;
				addedVertices.Add(pos.GetLongHashCode(), position);
				vertex = new Vertex(pos, status);
				vertices.Add(vertex);
			}

			vertex = vertices[position];
			vertex.SetStatus(status);

			return vertex;
		}

		/// <summary>
		/// Face breaker for FACE-FACE-FACE
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="facePos1">new vertex position</param>
		/// <param name="facePos2">new vertex position</param>
		/// <param name="linedVertex">linedVertex what vertex is more lined with the interersection found</param>
		private bool BreakFaceInFive(Face face, Vector3 facePos1, Vector3 facePos2, int linedVertex, Stack<Face> facesFromSplit)
		{
			//       O
			//      - -
			//     -   -
			//    -     -
			//   -    X  -
			//  -  X      -
			// O-----------O
			Faces.Remove(face);

			Vertex faceVertex1 = AddVertex(facePos1, Status.BOUNDARY);
			bool faceVertex1Exists = faceVertex1 != vertices[vertices.Count - 1];

			Vertex faceVertex2 = AddVertex(facePos2, Status.BOUNDARY);
			bool faceVertex2Exists = faceVertex2 != vertices[vertices.Count - 1];

			if (faceVertex1Exists && faceVertex2Exists)
			{
				vertices.RemoveAt(vertices.Count - 1);
				vertices.RemoveAt(vertices.Count - 1);
				return false;
			}

			if (linedVertex == 1)
			{
				AddFaceFromSplit(face.v2, face.v3, faceVertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v2, faceVertex1, faceVertex2, face, facesFromSplit);
				AddFaceFromSplit(face.v3, faceVertex2, faceVertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v2, faceVertex2, face.v1, face, facesFromSplit);
				AddFaceFromSplit(face.v3, face.v1, faceVertex2, face, facesFromSplit);
			}
			else if (linedVertex == 2)
			{
				AddFaceFromSplit(face.v3, face.v1, faceVertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v3, faceVertex1, faceVertex2, face, facesFromSplit);
				AddFaceFromSplit(face.v1, faceVertex2, faceVertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v3, faceVertex2, face.v2, face, facesFromSplit);
				AddFaceFromSplit(face.v1, face.v2, faceVertex2, face, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v1, face.v2, faceVertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v1, faceVertex1, faceVertex2, face, facesFromSplit);
				AddFaceFromSplit(face.v2, faceVertex2, faceVertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v1, faceVertex2, face.v3, face, facesFromSplit);
				AddFaceFromSplit(face.v2, face.v3, faceVertex2, face, facesFromSplit);
			}

			return true;
		}

		/// <summary>
		/// Face breaker for EDGE-FACE-FACE / FACE-FACE-EDGE
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="edgePos">new vertex position</param>
		/// <param name="facePos">new vertex position</param>
		/// <param name="endVertex">vertex used for the split</param>
		private bool BreakFaceInFour(Face face, Vector3 edgePos, Vector3 facePos, Vertex endVertex, Stack<Face> facesFromSplit)
		{
			//         2
			//        -*-
			//       - * -
			//      -  *  E
			//     -   * * -
			//    -    F    -
			//   -   *   *   -
			//  - *         * -
			// 3---------------1
			Vertex edgeVertex = AddVertex(edgePos, Status.BOUNDARY);
			bool edgeExists = edgeVertex != vertices[vertices.Count - 1];
			Vertex faceVertex = AddVertex(facePos, Status.BOUNDARY);
			bool faceExists = faceVertex != vertices[vertices.Count - 1];

			if (faceExists && edgeExists)
			{
				vertices.RemoveAt(vertices.Count - 1);
				vertices.RemoveAt(vertices.Count - 1);
				return false;
			}

			Faces.Remove(face);
			// check that we are not adding back in the same face we are removing
			if (endVertex.Equals(face.v1))
			{
				AddFaceFromSplit(face.v1, edgeVertex, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(edgeVertex, face.v2, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(face.v2, face.v3, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(face.v3, face.v1, faceVertex, face, facesFromSplit);
			}
			else if (endVertex.Equals(face.v2))
			{
				AddFaceFromSplit(face.v2, edgeVertex, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(edgeVertex, face.v3, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(face.v3, face.v1, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(face.v1, face.v2, faceVertex, face, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v3, edgeVertex, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(edgeVertex, face.v1, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(face.v1, face.v2, faceVertex, face, facesFromSplit);
				AddFaceFromSplit(face.v2, face.v3, faceVertex, face, facesFromSplit);
			}

			return true;
		}

		/// <summary>
		/// Face breaker for EDGE-EDGE-EDGE
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos1">new vertex position</param>
		/// <param name="newPos2">new vertex position</param>
		/// <param name="splitEdge">edge that will be split</param>
		private bool BreakFaceInThree(Face face, Vector3 newPos1, Vector3 newPos2, int splitEdge, Stack<Face> facesFromSplit)
		{
			//       O
			//      - -
			//     -   X
			//    -  *  -
			//   - *   **X
			//  -* ****   -
			// X-----------O

			Vertex vertex1 = AddVertex(newPos1, Status.BOUNDARY);
			Vertex vertex2 = AddVertex(newPos2, Status.BOUNDARY);

			if (splitEdge == 1) // vertex 3
			{
				bool willMakeExistingFace = (vertex1 == face.v1 && vertex2 == face.v2) || (vertex1 == face.v2 || vertex2 == face.v1);
				if (!willMakeExistingFace)
				{
					Faces.Remove(face);
					AddFaceFromSplit(face.v1, vertex1, face.v3, face, facesFromSplit);
					AddFaceFromSplit(vertex1, vertex2, face.v3, face, facesFromSplit);
					AddFaceFromSplit(vertex2, face.v2, face.v3, face, facesFromSplit);
					return true;
				}
			}
			else if (splitEdge == 2) // vertex 1
			{
				bool willMakeExistingFace = (vertex1 == face.v2 && vertex2 == face.v3) || (vertex1 == face.v3 || vertex2 == face.v2);
				if (!willMakeExistingFace)
				{
					Faces.Remove(face);
					AddFaceFromSplit(face.v2, vertex1, face.v1, face, facesFromSplit);
					AddFaceFromSplit(vertex1, vertex2, face.v1, face, facesFromSplit);
					AddFaceFromSplit(vertex2, face.v3, face.v1, face, facesFromSplit);
					return true;
				}
			}
			else // vertex 2
			{
				bool willMakeExistingFace = (vertex1 == face.v1 && vertex2 == face.v3) || (vertex1 == face.v3 || vertex2 == face.v1);
				if (!willMakeExistingFace)
				{
					Faces.Remove(face);
					AddFaceFromSplit(face.v3, vertex1, face.v2, face, facesFromSplit);
					AddFaceFromSplit(vertex1, vertex2, face.v2, face, facesFromSplit);
					AddFaceFromSplit(vertex2, face.v1, face.v2, face, facesFromSplit);
					return true;
				}
			}

			if (vertex2 == vertices[vertices.Count - 1])
				vertices.RemoveAt(vertices.Count - 1);
			if (vertex1 == vertices[vertices.Count - 1])
				vertices.RemoveAt(vertices.Count - 1);
			return false;
		}

		/// <summary>
		/// Face breaker for VERTEX-FACE-FACE / FACE-FACE-VERTEX
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos">new vertex position</param>
		/// <param name="endVertex">vertex used for the split</param>
		private bool BreakFaceInThree(Face face, Vector3 newPos, Stack<Face> facesFromSplit)
		{
			//       O
			//      -*-
			//     - * -
			//    -  *  -
			//   -   X   -
			//  -  *   *  -
			// O-*--------*O
			Vertex vertex = AddVertex(newPos, Status.BOUNDARY);
			if (face.v1.Position == vertex.Position
				|| face.v2.Position == vertex.Position
				|| face.v2.Position == vertex.Position) // it is not new
			{
				// if the vertex we are adding is any of the existing vertices then don't add any
				return false;
			}

			Faces.Remove(face);

			AddFaceFromSplit(face.v1, face.v2, vertex, face, facesFromSplit);
			AddFaceFromSplit(face.v2, face.v3, vertex, face, facesFromSplit);
			AddFaceFromSplit(face.v3, face.v1, vertex, face, facesFromSplit);

			return true;
		}

		/// <summary>
		/// Face breaker for EDGE-FACE-EDGE
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos1">new vertex position</param>
		/// <param name="newPos2">new vertex position</param>
		/// <param name="startVertex">vertex used for the new faces creation</param>
		/// <param name="endVertex">vertex used for the new faces creation</param>
		private bool BreakFaceInThree(Face face, Vector3 newPos1, Vector3 newPos2, Vertex startVertex, Vertex endVertex, Stack<Face> facesFromSplit)
		{
			//       O
			//      - -
			//     -   -
			//    -     -
			//   -       -
			//  -         -
			// O-----------O
			Faces.Remove(face);

			Vertex vertex1 = AddVertex(newPos1, Status.BOUNDARY);
			Vertex vertex2 = AddVertex(newPos2, Status.BOUNDARY);

			if (startVertex.Equals(face.v1) && endVertex.Equals(face.v2))
			{
				AddFaceFromSplit(face.v1, vertex1, vertex2, face, facesFromSplit);
				AddFaceFromSplit(face.v1, vertex2, face.v3, face, facesFromSplit);
				AddFaceFromSplit(vertex1, face.v2, vertex2, face, facesFromSplit);
			}
			else if (startVertex.Equals(face.v2) && endVertex.Equals(face.v1))
			{
				AddFaceFromSplit(face.v1, vertex2, vertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v1, vertex1, face.v3, face, facesFromSplit);
				AddFaceFromSplit(vertex2, face.v2, vertex1, face, facesFromSplit);
			}
			else if (startVertex.Equals(face.v2) && endVertex.Equals(face.v3))
			{
				AddFaceFromSplit(face.v2, vertex1, vertex2, face, facesFromSplit);
				AddFaceFromSplit(face.v2, vertex2, face.v1, face, facesFromSplit);
				AddFaceFromSplit(vertex1, face.v3, vertex2, face, facesFromSplit);
			}
			else if (startVertex.Equals(face.v3) && endVertex.Equals(face.v2))
			{
				AddFaceFromSplit(face.v2, vertex2, vertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v2, vertex1, face.v1, face, facesFromSplit);
				AddFaceFromSplit(vertex2, face.v3, vertex1, face, facesFromSplit);
			}
			else if (startVertex.Equals(face.v3) && endVertex.Equals(face.v1))
			{
				AddFaceFromSplit(face.v3, vertex1, vertex2, face, facesFromSplit);
				AddFaceFromSplit(face.v3, vertex2, face.v2, face, facesFromSplit);
				AddFaceFromSplit(vertex1, face.v1, vertex2, face, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v3, vertex2, vertex1, face, facesFromSplit);
				AddFaceFromSplit(face.v3, vertex1, face.v2, face, facesFromSplit);
				AddFaceFromSplit(vertex2, face.v1, vertex1, face, facesFromSplit);
			}

			return true;
		}

		/// <summary>
		/// Face breaker for VERTEX-EDGE-EDGE / EDGE-EDGE-VERTEX
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos">new vertex position</param>
		/// <param name="splitEdge">edge that will be split</param>
		private bool BreakFaceInTwo(Face face, Vector3 newPos, int splitEdge, Stack<Face> facesFromSplit)
		{
			//       O
			//      -*-
			//     - * -
			//    -  *  -
			//   -   *   -
			//  -    *    -
			// O-----X-----O
			Vertex vertex = AddVertex(newPos, Status.BOUNDARY);

			if (vertex != vertices[vertices.Count - 1])
			{
				// The added vertex is one of the existing vertices. So we would only add the same face we are removing.
				return false;
			}

			Faces.Remove(face);

			if (splitEdge == 1)
			{
				AddFaceFromSplit(face.v1, vertex, face.v3, face, facesFromSplit);
				AddFaceFromSplit(vertex, face.v2, face.v3, face, facesFromSplit);
			}
			else if (splitEdge == 2)
			{
				if (!face.v3.Equals(vertex))
				{
					AddFaceFromSplit(face.v2, vertex, face.v1, face, facesFromSplit);
				}
				AddFaceFromSplit(vertex, face.v3, face.v1, face, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v3, vertex, face.v2, face, facesFromSplit);
				AddFaceFromSplit(vertex, face.v1, face.v2, face, facesFromSplit);
			}

			return true;
		}

		/// <summary>
		/// Face breaker for VERTEX-FACE-EDGE / EDGE-FACE-VERTEX
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos">new vertex position</param>
		/// <param name="endVertex">vertex used for splitting</param>
		private bool BreakFaceInTwo(Face face, Vector3 newPos, Vertex endVertex, Stack<Face> facesFromSplit)
		{
			//       O
			//      - -
			//     -   -
			//    -     -
			//   -       -
			//  -         -
			// O-----------O

			// TODO: make sure we are not creating extra Vertices and not cleaning them up
			Vertex vertex = AddVertex(newPos, Status.BOUNDARY);

			if (endVertex.Equals(face.v1))
			{
				// don't add it if it is the same as the face we have
				if (!face.v1.Equals(vertex)
					&& !face.v2.Equals(vertex))
				{
					AddFaceFromSplit(face.v1, vertex, face.v3, face, facesFromSplit);
					AddFaceFromSplit(vertex, face.v2, face.v3, face, facesFromSplit);
					Faces.Remove(face);
					return true;
				}
			}
			else if (endVertex.Equals(face.v2))
			{
				// don't add it if it is the same as the face we have
				if (!face.v2.Equals(vertex)
					&& !face.v3.Equals(vertex))
				{
					AddFaceFromSplit(face.v2, vertex, face.v1, face, facesFromSplit);
					AddFaceFromSplit(vertex, face.v3, face.v1, face, facesFromSplit);
					Faces.Remove(face);
					return true;
				}
			}
			else
			{
				// don't add it if it is the same as the face we have
				if (!face.v1.Equals(vertex)
					&& !face.v3.Equals(vertex))
				{
					AddFaceFromSplit(face.v3, vertex, face.v2, face, facesFromSplit);
					AddFaceFromSplit(vertex, face.v1, face.v2, face, facesFromSplit);
					Faces.Remove(face);
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Split an individual face
		/// </summary>
		/// <param name="faceIndex">face index in the array of faces</param>
		/// <param name="segment1">segment representing the intersection of the face with the plane</param>
		/// <param name="segment2">segment representing the intersection of other face with the plane of the current face plane</param>
		private bool SplitFace(Face face, Segment segment1, Segment segment2, Stack<Face> facesFromSplit)
		{
			Vector3 startPos, endPos;
			SegmentEnd startType, endType, middleType;
			double startDist, endDist;

			Vertex startVertex = segment1.StartVertex;
			Vertex endVertex = segment1.EndVertex;

			//starting point: deeper starting point
			if (segment2.StartDistance > segment1.StartDistance + EqualityTolerance)
			{
				startDist = segment2.StartDistance;
				startType = segment1.MiddleType;
				startPos = segment2.StartPosition;
			}
			else
			{
				startDist = segment1.StartDistance;
				startType = segment1.StartType;
				startPos = segment1.StartPosition;
			}

			//ending point: deepest ending point
			if (segment2.EndDistance < segment1.EndDistance - EqualityTolerance)
			{
				endDist = segment2.EndDistance;
				endType = segment1.MiddleType;
				endPos = segment2.EndPosition;
			}
			else
			{
				endDist = segment1.EndDistance;
				endType = segment1.EndType;
				endPos = segment1.EndPosition;
			}
			middleType = segment1.MiddleType;

			if (startType == SegmentEnd.Vertex)
			{
				//set vertex to BOUNDARY if it is start type
				startVertex.SetStatus(Status.BOUNDARY);
			}

			if (endType == SegmentEnd.Vertex)
			{
				//set vertex to BOUNDARY if it is end type
				endVertex.SetStatus(Status.BOUNDARY);
			}

			if (startType == SegmentEnd.Vertex && endType == SegmentEnd.Vertex)
			{
				//VERTEX-_______-VERTEX
				return false;
			}
			else if (middleType == SegmentEnd.Edge)
			{
				//______-EDGE-______
				//gets the edge
				int splitEdge;
				if ((startVertex == face.v1 && endVertex == face.v2) || (startVertex == face.v2 && endVertex == face.v1))
				{
					splitEdge = 1;
				}
				else if ((startVertex == face.v2 && endVertex == face.v3) || (startVertex == face.v3 && endVertex == face.v2))
				{
					splitEdge = 2;
				}
				else
				{
					splitEdge = 3;
				}

				if (startType == SegmentEnd.Vertex)
				{
					//VERTEX-EDGE-EDGE
					return BreakFaceInTwo(face, endPos, splitEdge, facesFromSplit);
				}
				else if (endType == SegmentEnd.Vertex)
				{
					//EDGE-EDGE-VERTEX
					return BreakFaceInTwo(face, startPos, splitEdge, facesFromSplit);
				}
				else if (startDist == endDist)
				{
					// EDGE-EDGE-EDGE
					return BreakFaceInTwo(face, endPos, splitEdge, facesFromSplit);
				}
				else
				{
					if ((startVertex == face.v1 && endVertex == face.v2) || (startVertex == face.v2 && endVertex == face.v3) || (startVertex == face.v3 && endVertex == face.v1))
					{
						return BreakFaceInThree(face, startPos, endPos, splitEdge, facesFromSplit);
					}
					else
					{
						return BreakFaceInThree(face, endPos, startPos, splitEdge, facesFromSplit);
					}
				}
			}
			//______-FACE-______
			else if (startType == SegmentEnd.Vertex && endType == SegmentEnd.Edge)
			{
				//VERTEX-FACE-EDGE
				return BreakFaceInTwo(face, endPos, endVertex, facesFromSplit);
			}
			else if (startType == SegmentEnd.Edge && endType == SegmentEnd.Vertex)
			{
				//EDGE-FACE-VERTEX
				return BreakFaceInTwo(face, startPos, startVertex, facesFromSplit);
			}
			else if (startType == SegmentEnd.Vertex && endType == SegmentEnd.Face)
			{
				//VERTEX-FACE-FACE
				return BreakFaceInThree(face, endPos, facesFromSplit);
			}
			else if (startType == SegmentEnd.Face && endType == SegmentEnd.Vertex)
			{
				//FACE-FACE-VERTEX
				return BreakFaceInThree(face, startPos, facesFromSplit);
			}
			else if (startType == SegmentEnd.Edge && endType == SegmentEnd.Edge)
			{
				//EDGE-FACE-EDGE
				return BreakFaceInThree(face, startPos, endPos, startVertex, endVertex, facesFromSplit);
			}
			else if (startType == SegmentEnd.Edge && endType == SegmentEnd.Face)
			{
				//EDGE-FACE-FACE
				return BreakFaceInFour(face, startPos, endPos, startVertex, facesFromSplit);
			}
			else if (startType == SegmentEnd.Face && endType == SegmentEnd.Edge)
			{
				//FACE-FACE-EDGE
				return BreakFaceInFour(face, endPos, startPos, endVertex, facesFromSplit);
			}
			else if (startType == SegmentEnd.Face && endType == SegmentEnd.Face)
			{
				//FACE-FACE-FACE
				Vector3 segmentVector = new Vector3(startPos.X - endPos.X, startPos.Y - endPos.Y, startPos.Z - endPos.Z);

				//if the intersection segment is a point only...
				if (Math.Abs(segmentVector.X) < EqualityTolerance && Math.Abs(segmentVector.Y) < EqualityTolerance && Math.Abs(segmentVector.Z) < EqualityTolerance)
				{
					return BreakFaceInThree(face, startPos, facesFromSplit);
				}

				//gets the vertex more lined with the intersection segment
				int linedVertex;
				Vector3 linedVertexPos;
				Vector3 vertexVector = new Vector3(endPos.X - face.v1.Position.X, endPos.Y - face.v1.Position.Y, endPos.Z - face.v1.Position.Z);
				vertexVector.Normalize();
				double dot1 = Math.Abs(Vector3.Dot(segmentVector, vertexVector));
				vertexVector = new Vector3(endPos.X - face.v2.Position.X, endPos.Y - face.v2.Position.Y, endPos.Z - face.v2.Position.Z);
				vertexVector.Normalize();
				double dot2 = Math.Abs(Vector3.Dot(segmentVector, vertexVector));
				vertexVector = new Vector3(endPos.X - face.v3.Position.X, endPos.Y - face.v3.Position.Y, endPos.Z - face.v3.Position.Z);
				vertexVector.Normalize();
				double dot3 = Math.Abs(Vector3.Dot(segmentVector, vertexVector));
				if (dot1 > dot2 && dot1 > dot3)
				{
					linedVertex = 1;
					linedVertexPos = face.v1.GetPosition();
				}
				else if (dot2 > dot3 && dot2 > dot1)
				{
					linedVertex = 2;
					linedVertexPos = face.v2.GetPosition();
				}
				else
				{
					linedVertex = 3;
					linedVertexPos = face.v3.GetPosition();
				}

				// Now find which of the intersection endpoints is nearest to that vertex.
				if ((linedVertexPos - startPos).Length > (linedVertexPos - endPos).Length)
				{
					return BreakFaceInFive(face, startPos, endPos, linedVertex, facesFromSplit);
				}
				else
				{
					return BreakFaceInFive(face, endPos, startPos, linedVertex, facesFromSplit);
				}
			}

			return false;
		}
	}
}