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
	/// Data structure about a 3D solid to apply boolean operations on it.
	/// Typically, two 'Object3d' objects are created to apply boolean operation. The
	/// methods SplitFaces() and ClassifyFaces() are called in this sequence for both objects,
	/// always using the other one as parameter, then the faces from both objects are collected
	/// according their status.
	/// </summary>
	public class CsgObject3D
	{
		/// <summary>
		/// solid faces
		/// </summary>
		public Octree<CsgFace> Faces;

		/// <summary>
		/// tolerance value to test equalities
		/// </summary>
		private readonly static double EqualityTolerance = 1e-10f;

		private Dictionary<long, int> addedVertices = new Dictionary<long, int>();

		/// <summary>
		/// object representing the solid extremes
		/// </summary>
		private AxisAlignedBoundingBox Bounds { get; set; }

		/// <summary>
		/// solid vertices
		/// </summary>
		private List<Vertex> vertices;

		//BoundsOctree<int> octree = new BoundsOctree<int>();

		/// <summary>
		/// Constructs a Object3d object based on a solid file.
		/// </summary>
		/// <param name="solid">solid used to construct the Object3d object</param>
		public CsgObject3D(Solid solid)
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
				vertex = AddVertex(verticesPoints[i], FaceStatus.Unknown);
				totalBounds.ExpandToInclude(verticesPoints[i]);
				verticesTemp.Add(vertex);
			}

			//create faces
			totalBounds.Expand(1);
			Faces = new Octree<CsgFace>(5, totalBounds);
			for (int i = 0; i < indices.Length; i = i + 3)
			{
				v1 = verticesTemp[indices[i]];
				v2 = verticesTemp[indices[i + 1]];
				v3 = verticesTemp[indices[i + 2]];
				AddFace(v1, v2, v3);
			}

			//create bound
			Bounds = new AxisAlignedBoundingBox(verticesPoints);
		}

		/// <summary>
		/// Default constructor
		/// </summary>
		private CsgObject3D()
		{
		}

		/// <summary>
		/// Classify faces as being inside, outside or on boundary of other object
		/// </summary>
		/// <param name="otherObject">object 3d used for the comparison</param>
		public void ClassifyFaces(CsgObject3D otherObject, CancellationToken cancellationToken, Action<CsgFace> classifyFaces = null)
		{
			var otherBounds = otherObject.Bounds;
			foreach (var vertex in vertices)
			{
				cancellationToken.ThrowIfCancellationRequested();

				if (!otherBounds.Contains(vertex.Position)
					&& vertex.Status != FaceStatus.Outside)
				{
					vertex.Status = FaceStatus.Outside;
				}
			}

			//calculate adjacency information
			Faces.All();
			foreach (var face in Faces.QueryResults)
			{
				cancellationToken.ThrowIfCancellationRequested();

				face.v1.AddAdjacentVertex(face.v2);
				face.v1.AddAdjacentVertex(face.v3);
				face.v2.AddAdjacentVertex(face.v1);
				face.v2.AddAdjacentVertex(face.v3);
				face.v3.AddAdjacentVertex(face.v1);
				face.v3.AddAdjacentVertex(face.v2);
			}

			// for each face
			foreach (var face in Faces.QueryResults)
			{
				cancellationToken.ThrowIfCancellationRequested();

				// If the face vertices can't be classified with the simple classify
				if (face.SimpleClassify() == false)
				{
					//makes the ray trace classification
					face.RayTraceClassify(otherObject);

					//mark the vertices
					if (face.v1.Status == FaceStatus.Unknown)
					{
						face.v1.Mark(face.Status);
					}
					if (face.v2.Status == FaceStatus.Unknown)
					{
						face.v2.Mark(face.Status);
					}
					if (face.v3.Status == FaceStatus.Unknown)
					{
						face.v3.Mark(face.Status);
					}
				}

				classifyFaces?.Invoke(face);
			}
		}

		/// <summary>
		/// Gets the solid bound
		/// </summary>
		/// <returns>solid bound</returns>
		public AxisAlignedBoundingBox GetBound()
		{
			return Bounds;
		}

		/// <summary>
		/// Inverts faces classified as INSIDE, making its normals point outside. Usually used into the second solid when the difference is applied.
		/// </summary>
		public void InvertInsideFaces()
		{
			Faces.All();
			foreach (var face in Faces.QueryResults)
			{
				if (face.Status == FaceStatus.Inside)
				{
					face.Invert();
				}
			}
		}

		/// <summary>
		/// Split faces so that no face is intercepted by a face of other object
		/// </summary>
		/// <param name="compareObject">the other object 3d used to make the split</param>
		public void SplitFaces(CsgObject3D compareObject, CancellationToken cancellationToken,
			Action<CsgFace, CsgFace> splitFaces = null,
			Action<List<CsgFace>> results = null)
		{
			Stack<CsgFace> newFacesFromSplitting = new Stack<CsgFace>();

			int numFacesStart = this.Faces.Count;

			//if the objects bounds overlap...
			//for each object1 face...
			var bounds = compareObject.GetBound();
			Faces.SearchBounds(bounds);
			foreach (var thisFaceIn in Faces.QueryResults) // put it in an array as we will be adding new faces to it
			{
				newFacesFromSplitting.Push(thisFaceIn);
				// make sure we process every face that we have added during splitting before moving on to the next face
				while (newFacesFromSplitting.Count > 0)
				{
					var faceToSplit = newFacesFromSplitting.Pop();

					// stop processing if operation has been canceled
					cancellationToken.ThrowIfCancellationRequested();

					//if object1 face bound and object2 bound overlap ...
					//for each object2 face...
					compareObject.Faces.SearchBounds(faceToSplit.GetBound());
					foreach (var cuttingFace in compareObject.Faces.QueryResults)
					{
						//if object1 face bound and object2 face bound overlap...
						//PART I - DO TWO POLIGONS INTERSECT?
						//POSSIBLE RESULTS: INTERSECT, NOT_INTERSECT, COPLANAR

						//distance from the face1 vertices to the face2 plane
						double v1DistToCuttingFace = cuttingFace.DistanceFromPlane(faceToSplit.v1);
						double v2DistToCuttingFace = cuttingFace.DistanceFromPlane(faceToSplit.v2);
						double v3DistToCuttingFace = cuttingFace.DistanceFromPlane(faceToSplit.v3);

						//distances signs from the face1 vertices to the face2 plane
						PlaneSide sideFace1Vert1 = (v1DistToCuttingFace > EqualityTolerance ? PlaneSide.Front : (v1DistToCuttingFace < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
						PlaneSide sideFace1Vert2 = (v2DistToCuttingFace > EqualityTolerance ? PlaneSide.Front : (v2DistToCuttingFace < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
						PlaneSide sideFace1Vert3 = (v3DistToCuttingFace > EqualityTolerance ? PlaneSide.Front : (v3DistToCuttingFace < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));

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
							PlaneSide signFace2Vert1 = (faceToSplitTo1 > EqualityTolerance ? PlaneSide.Front : (faceToSplitTo1 < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
							PlaneSide signFace2Vert2 = (faceToSplitTo2 > EqualityTolerance ? PlaneSide.Front : (faceToSplitTo2 < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));
							PlaneSide signFace2Vert3 = (faceToSplitTo3 > EqualityTolerance ? PlaneSide.Front : (faceToSplitTo3 < -EqualityTolerance ? PlaneSide.Back : PlaneSide.On));

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
									Stack<CsgFace> facesFromSplit = new Stack<CsgFace>();

									if (this.SplitFace(faceToSplit, segment1, segment2, facesFromSplit)
										&& facesFromSplit.Count > 0
										&& !(facesFromSplit.Count == 1 && facesFromSplit.Peek().Equals(faceToSplit)))
									{
										foreach (var face in facesFromSplit)
										{
											newFacesFromSplitting.Push(face);
										}

										// send debugging information if registered
										splitFaces?.Invoke(faceToSplit, cuttingFace);
										results?.Invoke(facesFromSplit.ToList());

										break;
									}
								}
							}
						}
					}
				}
			}
		}

		private CsgFace AddFace(Vertex v1, Vertex v2, Vertex v3)
		{
			if (!(v1.Equals(v2) || v1.Equals(v3) || v2.Equals(v3)))
			{
				CsgFace face = new CsgFace(v1, v2, v3);
				if (face.GetArea() > EqualityTolerance)
				{
					Faces.Insert(face, face.GetBound());

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
		private void AddFaceFromSplit(Vertex v1, Vertex v2, Vertex v3,
			Stack<CsgFace> facesFromSplit)
		{
			if (!(v1.Equals(v2) || v1.Equals(v3) || v2.Equals(v3)))
			{
				CsgFace face = new CsgFace(v1, v2, v3);
				if (face.GetArea() > EqualityTolerance)
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
						Faces.Insert(face, face.GetBound());
						facesFromSplit.Push(face);
					}
				}
			}
		}

		/// <summary>
		/// Method used to add a vertex properly for internal methods
		/// </summary>
		/// <param name="pos">vertex position</param>
		/// <param name="status">vertex status</param>
		/// <returns>The vertex inserted (if a similar vertex already exists, this is returned).</returns>
		private Vertex AddVertex(Vector3 pos, FaceStatus status)
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
			vertex.Status = status;

			return vertex;
		}

		/// <summary>
		/// Face breaker for FACE-FACE-FACE
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="facePos1">new vertex position</param>
		/// <param name="facePos2">new vertex position</param>
		/// <param name="linedVertex">linedVertex what vertex is more lined with the intersection found</param>
		private bool BreakFaceInFive(CsgFace face, Vector3 facePos1, Vector3 facePos2, int linedVertex, Stack<CsgFace> facesFromSplit)
		{
			//       O
			//      - -
			//     -   -
			//    -     -
			//   -    X  -
			//  -  X      -
			// O-----------O
			Vertex faceVertex1 = AddVertex(facePos1, FaceStatus.Boundary);
			bool faceVertex1Exists = faceVertex1 != vertices[vertices.Count - 1];

			Vertex faceVertex2 = AddVertex(facePos2, FaceStatus.Boundary);
			bool faceVertex2Exists = faceVertex2 != vertices[vertices.Count - 1];

			if (faceVertex1Exists && faceVertex2Exists)
			{
				vertices.RemoveAt(vertices.Count - 1);
				vertices.RemoveAt(vertices.Count - 1);
				return false;
			}

			if (linedVertex == 1)
			{
				AddFaceFromSplit(face.v2, face.v3, faceVertex1, facesFromSplit);
				AddFaceFromSplit(face.v2, faceVertex1, faceVertex2, facesFromSplit);
				AddFaceFromSplit(face.v3, faceVertex2, faceVertex1, facesFromSplit);
				AddFaceFromSplit(face.v2, faceVertex2, face.v1, facesFromSplit);
				AddFaceFromSplit(face.v3, face.v1, faceVertex2, facesFromSplit);
			}
			else if (linedVertex == 2)
			{
				AddFaceFromSplit(face.v3, face.v1, faceVertex1, facesFromSplit);
				AddFaceFromSplit(face.v3, faceVertex1, faceVertex2, facesFromSplit);
				AddFaceFromSplit(face.v1, faceVertex2, faceVertex1, facesFromSplit);
				AddFaceFromSplit(face.v3, faceVertex2, face.v2, facesFromSplit);
				AddFaceFromSplit(face.v1, face.v2, faceVertex2, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v1, face.v2, faceVertex1, facesFromSplit);
				AddFaceFromSplit(face.v1, faceVertex1, faceVertex2, facesFromSplit);
				AddFaceFromSplit(face.v2, faceVertex2, faceVertex1, facesFromSplit);
				AddFaceFromSplit(face.v1, faceVertex2, face.v3, facesFromSplit);
				AddFaceFromSplit(face.v2, face.v3, faceVertex2, facesFromSplit);
			}

			Faces.Remove(face);

			return true;
		}

		/// <summary>
		/// Face breaker for EDGE-FACE-FACE / FACE-FACE-EDGE
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="edgePos">new vertex position</param>
		/// <param name="facePos">new vertex position</param>
		/// <param name="endVertex">vertex used for the split</param>
		private bool BreakFaceInFour(CsgFace face, Vector3 edgePos, Vector3 facePos, Vertex endVertex, Stack<CsgFace> facesFromSplit)
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
			Vertex edgeVertex = AddVertex(edgePos, FaceStatus.Boundary);
			bool edgeExists = edgeVertex != vertices[vertices.Count - 1];
			Vertex faceVertex = AddVertex(facePos, FaceStatus.Boundary);
			bool faceExists = faceVertex != vertices[vertices.Count - 1];

			if (faceExists && edgeExists)
			{
				vertices.RemoveAt(vertices.Count - 1);
				vertices.RemoveAt(vertices.Count - 1);
				return false;
			}

			// check that we are not adding back in the same face we are removing
			if (endVertex.Equals(face.v1))
			{
				AddFaceFromSplit(face.v1, edgeVertex, faceVertex, facesFromSplit);
				AddFaceFromSplit(edgeVertex, face.v2, faceVertex, facesFromSplit);
				AddFaceFromSplit(face.v2, face.v3, faceVertex, facesFromSplit);
				AddFaceFromSplit(face.v3, face.v1, faceVertex, facesFromSplit);
			}
			else if (endVertex.Equals(face.v2))
			{
				AddFaceFromSplit(face.v2, edgeVertex, faceVertex, facesFromSplit);
				AddFaceFromSplit(edgeVertex, face.v3, faceVertex, facesFromSplit);
				AddFaceFromSplit(face.v3, face.v1, faceVertex, facesFromSplit);
				AddFaceFromSplit(face.v1, face.v2, faceVertex, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v3, edgeVertex, faceVertex, facesFromSplit);
				AddFaceFromSplit(edgeVertex, face.v1, faceVertex, facesFromSplit);
				AddFaceFromSplit(face.v1, face.v2, faceVertex, facesFromSplit);
				AddFaceFromSplit(face.v2, face.v3, faceVertex, facesFromSplit);
			}

			Faces.Remove(face);

			return true;
		}

		/// <summary>
		/// Face breaker for EDGE-EDGE-EDGE
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos1">new vertex position</param>
		/// <param name="newPos2">new vertex position</param>
		/// <param name="splitEdge">edge that will be split</param>
		private bool BreakFaceInThree(CsgFace face, Vector3 newPos1, Vector3 newPos2, int splitEdge, Stack<CsgFace> facesFromSplit)
		{
			//       O
			//      - -
			//     -   X
			//    -  *  -
			//   - *   **X
			//  -* ****   -
			// X-----------O

			Vertex vertex1 = AddVertex(newPos1, FaceStatus.Boundary);
			Vertex vertex2 = AddVertex(newPos2, FaceStatus.Boundary);

			if (splitEdge == 1) // vertex 3
			{
				bool willMakeExistingFace = (vertex1 == face.v1 && vertex2 == face.v2) || (vertex1 == face.v2 || vertex2 == face.v1);
				if (!willMakeExistingFace)
				{
					AddFaceFromSplit(face.v1, vertex1, face.v3, facesFromSplit);
					AddFaceFromSplit(vertex1, vertex2, face.v3, facesFromSplit);
					AddFaceFromSplit(vertex2, face.v2, face.v3, facesFromSplit);
					Faces.Remove(face);

					return true;
				}
			}
			else if (splitEdge == 2) // vertex 1
			{
				bool willMakeExistingFace = (vertex1 == face.v2 && vertex2 == face.v3) || (vertex1 == face.v3 || vertex2 == face.v2);
				if (!willMakeExistingFace)
				{
					AddFaceFromSplit(face.v2, vertex1, face.v1, facesFromSplit);
					AddFaceFromSplit(vertex1, vertex2, face.v1, facesFromSplit);
					AddFaceFromSplit(vertex2, face.v3, face.v1, facesFromSplit);
					Faces.Remove(face);

					return true;
				}
			}
			else // vertex 2
			{
				bool willMakeExistingFace = (vertex1 == face.v1 && vertex2 == face.v3) || (vertex1 == face.v3 || vertex2 == face.v1);
				if (!willMakeExistingFace)
				{
					AddFaceFromSplit(face.v3, vertex1, face.v2, facesFromSplit);
					AddFaceFromSplit(vertex1, vertex2, face.v2, facesFromSplit);
					AddFaceFromSplit(vertex2, face.v1, face.v2, facesFromSplit);
					Faces.Remove(face);

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
		private bool BreakFaceInThree(CsgFace face, Vector3 newPos, Stack<CsgFace> facesFromSplit)
		{
			//       O
			//      -*-
			//     - * -
			//    -  *  -
			//   -   X   -
			//  -  *   *  -
			// O-*--------*O
			Vertex vertex = AddVertex(newPos, FaceStatus.Boundary);
			if (face.v1.Position == vertex.Position
				|| face.v2.Position == vertex.Position
				|| face.v2.Position == vertex.Position) // it is not new
			{
				// if the vertex we are adding is any of the existing vertices then don't add any
				return false;
			}

			AddFaceFromSplit(face.v1, face.v2, vertex, facesFromSplit);
			AddFaceFromSplit(face.v2, face.v3, vertex, facesFromSplit);
			AddFaceFromSplit(face.v3, face.v1, vertex, facesFromSplit);

			Faces.Remove(face);

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
		private bool BreakFaceInThree(CsgFace face, Vector3 newPos1, Vector3 newPos2, Vertex startVertex, Vertex endVertex, Stack<CsgFace> facesFromSplit)
		{
			//       O
			//      - -
			//     -   -
			//    -     -
			//   -       -
			//  -         -
			// O-----------O
			Vertex vertex1 = AddVertex(newPos1, FaceStatus.Boundary);
			Vertex vertex2 = AddVertex(newPos2, FaceStatus.Boundary);

			if (startVertex.Equals(face.v1) && endVertex.Equals(face.v2))
			{
				AddFaceFromSplit(face.v1, vertex1, vertex2, facesFromSplit);
				AddFaceFromSplit(face.v1, vertex2, face.v3, facesFromSplit);
				AddFaceFromSplit(vertex1, face.v2, vertex2, facesFromSplit);
			}
			else if (startVertex.Equals(face.v2) && endVertex.Equals(face.v1))
			{
				AddFaceFromSplit(face.v1, vertex2, vertex1, facesFromSplit);
				AddFaceFromSplit(face.v1, vertex1, face.v3, facesFromSplit);
				AddFaceFromSplit(vertex2, face.v2, vertex1, facesFromSplit);
			}
			else if (startVertex.Equals(face.v2) && endVertex.Equals(face.v3))
			{
				AddFaceFromSplit(face.v2, vertex1, vertex2, facesFromSplit);
				AddFaceFromSplit(face.v2, vertex2, face.v1, facesFromSplit);
				AddFaceFromSplit(vertex1, face.v3, vertex2, facesFromSplit);
			}
			else if (startVertex.Equals(face.v3) && endVertex.Equals(face.v2))
			{
				AddFaceFromSplit(face.v2, vertex2, vertex1, facesFromSplit);
				AddFaceFromSplit(face.v2, vertex1, face.v1, facesFromSplit);
				AddFaceFromSplit(vertex2, face.v3, vertex1, facesFromSplit);
			}
			else if (startVertex.Equals(face.v3) && endVertex.Equals(face.v1))
			{
				AddFaceFromSplit(face.v3, vertex1, vertex2, facesFromSplit);
				AddFaceFromSplit(face.v3, vertex2, face.v2, facesFromSplit);
				AddFaceFromSplit(vertex1, face.v1, vertex2, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v3, vertex2, vertex1, facesFromSplit);
				AddFaceFromSplit(face.v3, vertex1, face.v2, facesFromSplit);
				AddFaceFromSplit(vertex2, face.v1, vertex1, facesFromSplit);
			}

			Faces.Remove(face);

			return true;
		}

		/// <summary>
		/// Face breaker for VERTEX-EDGE-EDGE / EDGE-EDGE-VERTEX
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos">new vertex position</param>
		/// <param name="splitEdge">edge that will be split</param>
		private bool BreakFaceInTwo(CsgFace face, Vector3 newPos, int splitEdge, Stack<CsgFace> facesFromSplit)
		{
			//       O
			//      -*-
			//     - * -
			//    -  *  -
			//   -   *   -
			//  -    *    -
			// O-----X-----O
			Vertex vertex = AddVertex(newPos, FaceStatus.Boundary);

			if (vertex != vertices[vertices.Count - 1])
			{
				// The added vertex is one of the existing vertices. So we would only add the same face we are removing.
				return false;
			}

			if (splitEdge == 1)
			{
				AddFaceFromSplit(face.v1, vertex, face.v3, facesFromSplit);
				AddFaceFromSplit(vertex, face.v2, face.v3, facesFromSplit);
			}
			else if (splitEdge == 2)
			{
				if (!face.v3.Equals(vertex))
				{
					AddFaceFromSplit(face.v2, vertex, face.v1, facesFromSplit);
				}
				AddFaceFromSplit(vertex, face.v3, face.v1, facesFromSplit);
			}
			else
			{
				AddFaceFromSplit(face.v3, vertex, face.v2, facesFromSplit);
				AddFaceFromSplit(vertex, face.v1, face.v2, facesFromSplit);
			}

			Faces.Remove(face);

			return true;
		}

		/// <summary>
		/// Face breaker for VERTEX-FACE-EDGE / EDGE-FACE-VERTEX
		/// </summary>
		/// <param name="faceIndex">face index in the faces array</param>
		/// <param name="newPos">new vertex position</param>
		/// <param name="endVertex">vertex used for splitting</param>
		private bool BreakFaceInTwo(CsgFace face, Vector3 newPos, Vertex endVertex, Stack<CsgFace> facesFromSplit)
		{
			//       O
			//      - -
			//     -   -
			//    -     -
			//   -       -
			//  -         -
			// O-----------O

			// TODO: make sure we are not creating extra Vertices and not cleaning them up
			Vertex vertex = AddVertex(newPos, FaceStatus.Boundary);

			if (endVertex.Equals(face.v1))
			{
				// don't add it if it is the same as the face we have
				if (!face.v1.Equals(vertex)
					&& !face.v2.Equals(vertex))
				{
					AddFaceFromSplit(face.v1, vertex, face.v3, facesFromSplit);
					AddFaceFromSplit(vertex, face.v2, face.v3, facesFromSplit);
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
					AddFaceFromSplit(face.v2, vertex, face.v1, facesFromSplit);
					AddFaceFromSplit(vertex, face.v3, face.v1, facesFromSplit);
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
					AddFaceFromSplit(face.v3, vertex, face.v2, facesFromSplit);
					AddFaceFromSplit(vertex, face.v1, face.v2, facesFromSplit);
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
		private bool SplitFace(CsgFace face, Segment segment1, Segment segment2, Stack<CsgFace> facesFromSplit)
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
				startVertex.Status = FaceStatus.Boundary;
			}

			if (endType == SegmentEnd.Vertex)
			{
				//set vertex to BOUNDARY if it is end type
				endVertex.Status = FaceStatus.Boundary;
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
			else if (startType == SegmentEnd.Face
				&& endType == SegmentEnd.Face)
			{
				//FACE-FACE-FACE
				Vector3 segmentVector = new Vector3(startPos.X - endPos.X, startPos.Y - endPos.Y, startPos.Z - endPos.Z);

				//if the intersection segment is a point only...
				if (Math.Abs(segmentVector.X) < EqualityTolerance
					&& Math.Abs(segmentVector.Y) < EqualityTolerance
					&& Math.Abs(segmentVector.Z) < EqualityTolerance)
				{
					return BreakFaceInThree(face, startPos, facesFromSplit);
				}

				//gets the vertex more lined with the intersection segment
				double dot1 = Math.Abs(Vector3.Dot(segmentVector, (endPos - face.v1.Position).GetNormal()));
				double dot2 = Math.Abs(Vector3.Dot(segmentVector, (endPos - face.v2.Position).GetNormal()));
				double dot3 = Math.Abs(Vector3.Dot(segmentVector, (endPos - face.v3.Position).GetNormal()));

				int linedVertex;
				Vector3 linedVertexPos;
				if (dot1 > dot2
					&& dot1 > dot3)
				{
					linedVertex = 1;
					linedVertexPos = face.v1.Position;
				}
				else if (dot2 > dot3
					&& dot2 > dot1)
				{
					linedVertex = 2;
					linedVertexPos = face.v2.Position;
				}
				else
				{
					linedVertex = 3;
					linedVertexPos = face.v3.Position;
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