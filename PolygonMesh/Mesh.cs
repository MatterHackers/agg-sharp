﻿/*
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
using System.Linq;
using System.Text;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
	public enum CreateOption { CreateNew, ReuseExisting };

	public enum SortOption { SortNow, WillSortLater };

	public class Mesh
	{
		public Dictionary<(Face, int), ImageBuffer> FaceTexture = new Dictionary<(Face, int), ImageBuffer>();
		public Dictionary<(FaceEdge, int), Vector2> TextureUV = new Dictionary<(FaceEdge, int), Vector2>();

		private static Dictionary<object, int> Ids = new Dictionary<object, int>(ReferenceEqualityComparer.Default);

		private static int nextIdToUse = 0;
		public BspNode FaceBspTree { get; set; } = null;
		public AxisAlignedBoundingBox cachedAABB = null;

		TransformedAabbCache transformedAabbCache = new TransformedAabbCache();

		public Dictionary<string, object> PropertyBag = new Dictionary<string, object>();

		public Mesh()
		{
		}

		public int ChangedCount { get; private set; } = 0;

		public List<Face> Faces { get; } = new List<Face>();

		public int ID => Mesh.GetID(this);

		public List<MeshEdge> MeshEdges { get; private set; } = new List<MeshEdge>();

		public VertexCollecton Vertices { get; private set; } = new VertexCollecton();

		public static Mesh Copy(Mesh meshToCopy, CancellationToken cancellationToken, Action<double, string> progress = null, bool allowFastCopy = true)
		{
			Mesh newMesh = new Mesh();

			if (allowFastCopy 
				&& meshToCopy.Vertices.IsSorted
				&& !meshToCopy.Vertices.Where((v) => v.FirstMeshEdge == null).Any())
			{
				Dictionary<int, int> vertexIndexDictionary = GetVertexToIndexDictionary(meshToCopy, newMesh);
				Dictionary<int, int> meshEdgeIndexDictionary = GetMeshEdgeToIndexDictionary(meshToCopy, newMesh);

				for (int faceIndex = 0; faceIndex < meshToCopy.Faces.Count; faceIndex++)
				{
					Face faceToCopy = meshToCopy.Faces[faceIndex];
					newMesh.Faces.Add(new Face(newMesh));
				}

				// now set all the data for the new mesh
				newMesh.Vertices.Capacity = meshToCopy.Vertices.Capacity;
				for (int vertexIndex = 0; vertexIndex < meshToCopy.Vertices.Count; vertexIndex++)
				{
					IVertex vertexToCopy = meshToCopy.Vertices[vertexIndex];
					// !!!! ON ERROR !!!!! If this throws an error, you likely need to CleanAndMergMesh the mesh before copying
					int indexOfFirstMeshEdge = meshEdgeIndexDictionary[vertexToCopy.FirstMeshEdge.ID];
					IVertex newVertex = newMesh.Vertices[vertexIndex];
					newVertex.FirstMeshEdge = newMesh.MeshEdges[indexOfFirstMeshEdge];
					newVertex.Normal = vertexToCopy.Normal;
				}

				newMesh.MeshEdges.Capacity = meshToCopy.MeshEdges.Capacity;
				for (int meshEdgeIndex = 0; meshEdgeIndex < meshToCopy.MeshEdges.Count; meshEdgeIndex++)
				{
					MeshEdge meshEdgeToCopy = meshToCopy.MeshEdges[meshEdgeIndex];
					MeshEdge newMeshEdge = newMesh.MeshEdges[meshEdgeIndex];

					newMeshEdge.NextMeshEdgeFromEnd[0] = newMesh.MeshEdges[meshEdgeIndexDictionary[meshEdgeToCopy.NextMeshEdgeFromEnd[0].ID]];
					newMeshEdge.NextMeshEdgeFromEnd[1] = newMesh.MeshEdges[meshEdgeIndexDictionary[meshEdgeToCopy.NextMeshEdgeFromEnd[1].ID]];

					newMeshEdge.VertexOnEnd[0] = newMesh.Vertices[vertexIndexDictionary[meshEdgeToCopy.VertexOnEnd[0].ID]];
					newMeshEdge.VertexOnEnd[1] = newMesh.Vertices[vertexIndexDictionary[meshEdgeToCopy.VertexOnEnd[1].ID]];

					// This will get hooked up when we create radial loops with the face edges below
					//newMeshEdge.firstFaceEdge;
					//newMesh.MeshEdges.Add(newMeshEdge);
				}

				newMesh.Faces.Capacity = meshToCopy.Faces.Capacity;
				for (int faceIndex = 0; faceIndex < meshToCopy.Faces.Count; faceIndex++)
				{
					Face faceToCopy = meshToCopy.Faces[faceIndex];
					Face newface = newMesh.Faces[faceIndex];

					newface.Normal = faceToCopy.Normal;

					// hook up the face edges
					//public FaceEdge firstFaceEdge;
					List<IVertex> verticesFromCopy = new List<IVertex>();
					List<IVertex> verticesForNew = new List<IVertex>();
					foreach (IVertex vertex in faceToCopy.Vertices())
					{
						verticesFromCopy.Add(vertex);
						verticesForNew.Add(newMesh.Vertices[vertexIndexDictionary[vertex.ID]]);
					}

					List<MeshEdge> edgesFromCopy = new List<MeshEdge>();
					List<MeshEdge> edgesForNew = new List<MeshEdge>();
					for (int i = 0; i < verticesForNew.Count - 1; i++)
					{
						MeshEdge meshEdgeFromCopy = verticesFromCopy[i].GetMeshEdgeConnectedToVertex(verticesFromCopy[i + 1]);
						edgesFromCopy.Add(meshEdgeFromCopy);
						edgesForNew.Add(newMesh.MeshEdges[meshEdgeIndexDictionary[meshEdgeFromCopy.ID]]);
					}
					MeshEdge lastMeshEdgeFromCopy = verticesFromCopy[verticesFromCopy.Count - 1].GetMeshEdgeConnectedToVertex(verticesFromCopy[0]);
					edgesFromCopy.Add(lastMeshEdgeFromCopy);
					edgesForNew.Add(newMesh.MeshEdges[meshEdgeIndexDictionary[lastMeshEdgeFromCopy.ID]]);

					CreateFaceEdges(verticesForNew.ToArray(), edgesForNew, newface);
				}
			}
			else
			{
				foreach (Face face in meshToCopy.Faces)
				{
					List<IVertex> faceVertices = new List<IVertex>();
					foreach (FaceEdge faceEdgeToAdd in face.FaceEdges())
					{
						IVertex newVertex = newMesh.CreateVertex(faceEdgeToAdd.FirstVertex.Position, CreateOption.CreateNew, SortOption.WillSortLater);
						faceVertices.Add(newVertex);
					}

					newMesh.CreateFace(faceVertices.ToArray(), CreateOption.CreateNew);
				}
			}

			return newMesh;
		}

		public static int GetID(object item)
		{
			int id;
			if (!Ids.TryGetValue(item, out id))
			{
				id = nextIdToUse++;
				Ids.Add(item, id);
			}

			return id;
		}

		public void CleanAndMergeMesh(CancellationToken cancellationToken, double maxDistanceToConsiderVertexAsSame = 0, Action<double, string> reportProgress = null)
		{
			if (reportProgress != null)
			{
				SortVertices((double progress0To1, string processingState) =>
				{
					reportProgress(progress0To1 * .41, processingState);
				});

				if (!cancellationToken.IsCancellationRequested)
				{
					MergeVertices(cancellationToken, maxDistanceToConsiderVertexAsSame, (double progress0To1, string processingState) =>
					{
						reportProgress(progress0To1 * .23 + .41, processingState);
					});
				}

				if (!cancellationToken.IsCancellationRequested)
				{
					MergeMeshEdges(cancellationToken, (double progress0To1, string processingState) =>
					{
						reportProgress(progress0To1 * .36 + .64, processingState);
					});
				}
			}
			else
			{
				SortVertices();
				MergeVertices(cancellationToken, maxDistanceToConsiderVertexAsSame);
				MergeMeshEdges(cancellationToken);
			}

			MarkAsChanged();
		}

		public override bool Equals(object obj)
		{
			if (!(obj is Mesh))
				return false;

			return this.Equals((Mesh)obj);
		}

		public bool Equals(Mesh other)
		{
			if (this.Vertices.Count == other.Vertices.Count
				&& this.MeshEdges.Count == other.MeshEdges.Count
				&& this.Faces.Count == other.Faces.Count)
			{
				foreach (IVertex vertex in Vertices)
				{
					List<IVertex> foundVertices = other.FindVertices(vertex.Position);
					if (foundVertices.Count < 1)
					{
						return false;
					}
				}

				foreach (MeshEdge meshEdge in MeshEdges)
				{
					List<MeshEdge> foundEdges = other.FindMeshEdges(meshEdge.VertexOnEnd[0], meshEdge.VertexOnEnd[1]);
					if (foundEdges.Count < 1)
					{
						return false;
					}
				}

				foreach (Face face in Faces)
				{
					List<IVertex> faceVerices = new List<IVertex>();
					foreach (FaceEdge faceEdge in face.FaceEdges())
					{
						faceVerices.Add(faceEdge.FirstVertex);
					}

					List<Face> foundFaces = other.FindFacesAtPosition(faceVerices.ToArray());
					if (foundFaces.Count < 1)
					{
						return false;
					}
				}

				return true;
			}

			return false;
		}

		public long GetLongHashCode()
		{
			unchecked
			{
				long hash = 19;

				hash = hash * 31 + MeshEdges.Count;
				hash = hash * 31 + Faces.Count;
				hash = hash * 31 + Vertices.Count;

				int step = Math.Max(1, Vertices.Count / 16);
				for (int i = 0; i < Vertices.Count; i += step)
				{
					var vertex = Vertices[i];
					hash = hash * 31 + vertex.Position.GetLongHashCode();
				}

				return hash;
			}
		}

		public void MarkAsChanged()
		{
			// mark this unchecked as we don't want to throw an exception if this rolls over.
			unchecked
			{
				transformedAabbCache.Changed();
				cachedAABB = null;
				ChangedCount++;
			}
		}

		public void Transform(Matrix4X4 matrix)
		{
			if (matrix != Matrix4X4.Identity)
			{
				bool wasSorted = Vertices.IsSorted;
				Vertices.IsSorted = false;
				foreach (IVertex vertex in Vertices)
				{
					vertex.Position = Vector3.Transform(vertex.Position, matrix);
				}
				foreach (Face face in Faces)
				{
					face.CalculateNormal();
				}
				if (wasSorted)
				{
					SortVertices();
				}
				MarkAsChanged();
			}
		}

		public void CalculateNormals()
		{
			foreach(var face in Faces)
			{
				face.CalculateNormal();
			}
		}

		public void Translate(Vector3 offset)
		{
			if (offset != Vector3.Zero)
			{
				foreach (IVertex vertex in Vertices)
				{
					vertex.Position += offset;
				}
				MarkAsChanged();
			}
		}

		public void Triangulate()
		{
			List<Face> tempFaceList = new List<Face>(Faces);
			foreach (Face face in tempFaceList)
			{
				if (face.NumVertices != 3)
				{
					List<IVertex> positionsCCW = new List<IVertex>();
					foreach (FaceEdge faceEdge in face.FaceEdges())
					{
						positionsCCW.Add(faceEdge.FirstVertex);
					}

					for (int splitIndex = 2; splitIndex < positionsCCW.Count - 1; splitIndex++)
					{
						MeshEdge createdEdge;
						Face createdFace;
						this.SplitFace(face, positionsCCW[0], positionsCCW[splitIndex], out createdEdge, out createdFace);
					}
				}
			}
		}

		private static Dictionary<int, int> GetMeshEdgeToIndexDictionary(Mesh meshToCopy, Mesh newMesh)
		{
			Dictionary<int, int> meshEdgeIndexDictionary = new Dictionary<int, int>(meshToCopy.MeshEdges.Count);
			for (int edgeIndex = 0; edgeIndex < meshToCopy.MeshEdges.Count; edgeIndex++)
			{
				MeshEdge edgeToCopy = meshToCopy.MeshEdges[edgeIndex];
				meshEdgeIndexDictionary.Add(edgeToCopy.ID, edgeIndex);
				newMesh.MeshEdges.Add(new MeshEdge());
			}
			return meshEdgeIndexDictionary;
		}

		private static Dictionary<int, int> GetVertexToIndexDictionary(Mesh meshToCopy, Mesh newMesh)
		{
			Dictionary<int, int> vertexIndexMapping = new Dictionary<int, int>(meshToCopy.Vertices.Count);
			for (int vertexIndex = 0; vertexIndex < meshToCopy.Vertices.Count; vertexIndex++)
			{
				IVertex vertexToCopy = meshToCopy.Vertices[vertexIndex];
				vertexIndexMapping.Add(vertexToCopy.ID, vertexIndex);
				newMesh.Vertices.Add(vertexToCopy.CreateInterpolated(vertexToCopy, 0));
			}
			return vertexIndexMapping;
		}

		#region Public Members

		#region Debug

		public string GetConnectionInfoAsString()
		{
			StringBuilder totalDebug = new StringBuilder();
			totalDebug.Append(String.Format("Mesh: {0}\n", ID));
			foreach (IVertex vertex in Vertices)
			{
				totalDebug.Append(new string('\t', 1) + String.Format("Vertex: {0}\n", vertex.ID));
				vertex.AddDebugInfo(totalDebug, 2);
			}
			foreach (MeshEdge meshEdge in MeshEdges)
			{
				totalDebug.Append(new string('\t', 1) + String.Format("MeshEdge: {0}\n", meshEdge.ID));
				meshEdge.AddDebugInfo(totalDebug, 2);
			}
			foreach (Face face in Faces)
			{
				totalDebug.Append(new string('\t', 1) + String.Format("Face: {0}\n", face.ID));
				face.AddDebugInfo(totalDebug, 2);
			}

			return totalDebug.ToString();
		}

		public void Validate(HashSet<IVertex> verticesToSkip = null)
		{
			if (verticesToSkip != null)
			{
				foreach (IVertex vertex in Vertices)
				{
					if (!verticesToSkip.Contains(vertex))
					{
						vertex.Validate();
					}
				}
			}
			else
			{
				foreach (IVertex vertex in Vertices)
				{
					vertex.Validate();
				}
			}
			foreach (MeshEdge meshEdge in MeshEdges)
			{
				meshEdge.Validate();
			}
			foreach (Face face in Faces)
			{
				face.Validate();
			}
		}

		#endregion Debug

		#region Operations

		public bool ContainsVertex(IVertex vertexToLookFor)
		{
			return Vertices.ContainsAVertexAtPosition(vertexToLookFor);
		}

		public bool DeleteVertexFromMeshEdge(MeshEdge meshEdgeDeleteVertexFrom, IVertex vertexToDelete)
		{
			throw new NotImplementedException();
		}

		public void ReverseFaceEdges()
		{
			foreach (Face face in Faces)
			{
				ReverseFaceEdges(face);
			}
		}

		public void ReverseFaceEdges(Face faceToReverse)
		{
			FaceEdge temp = null;
			FaceEdge current = faceToReverse.firstFaceEdge;

			// swap next and prev for all nodes of
			// doubly linked list
			do
			{
				temp = current.prevFaceEdge;
				current.prevFaceEdge = current.nextFaceEdge;
				current.nextFaceEdge = temp;
				current = current.prevFaceEdge; // go to the next
			} while (current != faceToReverse.firstFaceEdge);

			faceToReverse.CalculateNormal();
		}

		public void SplitFace(Face faceToSplit, IVertex splitStartVertex, IVertex splitEndVertex, out MeshEdge meshEdgeCreatedDuringSplit, out Face faceCreatedDuringSplit)
		{
			if (!ContainsVertex(splitStartVertex) || !ContainsVertex(splitEndVertex))
			{
				throw new Exception("The mesh must contain the vertices you intend to split between.");
			}

			// we may want to be able to double up an edge for some operations (we'll have to see).
			if (FindMeshEdges(splitStartVertex, splitEndVertex).Count > 0)
			{
				// this also ensures that the face is more than 2 sided.
				throw new Exception("You cannot split a face on an existing edge.");
			}

			FaceEdge faceEdgeAfterSplitStart = null;
			FaceEdge faceEdgeAfterSplitEnd = null;
			int count = 0;
			foreach (FaceEdge faceEdge in faceToSplit.FaceEdges())
			{
				if (faceEdge.FirstVertex == splitStartVertex)
				{
					faceEdgeAfterSplitStart = faceEdge;
					count++;
				}
				else if (faceEdge.FirstVertex == splitEndVertex)
				{
					faceEdgeAfterSplitEnd = faceEdge;
					count++;
				}
				if (count == 2)
				{
					break; // stop if we found both face edges
				}
			}

			meshEdgeCreatedDuringSplit = CreateMeshEdge(splitStartVertex, splitEndVertex);
			faceCreatedDuringSplit = new Face(faceToSplit, this);

			Faces.Add(faceCreatedDuringSplit);

			FaceEdge newFaceEdgeExistingFace = new FaceEdge(faceToSplit, meshEdgeCreatedDuringSplit, splitStartVertex);
			FaceEdge newFaceEdgeForNewFace = new FaceEdge(faceCreatedDuringSplit, meshEdgeCreatedDuringSplit, splitEndVertex);

			// get the new edges injected into the existing loop, spliting it in two.
			newFaceEdgeExistingFace.prevFaceEdge = faceEdgeAfterSplitStart.prevFaceEdge;
			newFaceEdgeForNewFace.prevFaceEdge = faceEdgeAfterSplitEnd.prevFaceEdge;

			faceEdgeAfterSplitStart.prevFaceEdge.nextFaceEdge = newFaceEdgeExistingFace;
			faceEdgeAfterSplitEnd.prevFaceEdge.nextFaceEdge = newFaceEdgeForNewFace;

			newFaceEdgeExistingFace.nextFaceEdge = faceEdgeAfterSplitEnd;
			newFaceEdgeForNewFace.nextFaceEdge = faceEdgeAfterSplitStart;

			faceEdgeAfterSplitStart.prevFaceEdge = newFaceEdgeForNewFace;
			faceEdgeAfterSplitEnd.prevFaceEdge = newFaceEdgeExistingFace;

			// make sure the first face edge of each face is valid
			faceToSplit.firstFaceEdge = newFaceEdgeExistingFace;
			faceCreatedDuringSplit.firstFaceEdge = newFaceEdgeForNewFace;

			// make sure the FaceEdges of the new face all point to the new face.
			foreach (FaceEdge faceEdge in faceCreatedDuringSplit.firstFaceEdge.NextFaceEdges())
			{
				faceEdge.ContainingFace = faceCreatedDuringSplit;
			}

			newFaceEdgeExistingFace.AddToRadialLoop(meshEdgeCreatedDuringSplit);
			newFaceEdgeForNewFace.AddToRadialLoop(meshEdgeCreatedDuringSplit);
		}

		public void UnsplitFace(Face faceToKeep, Face faceToDelete, MeshEdge meshEdgeToDelete)
		{
			if (faceToKeep == faceToDelete)
			{
				throw new Exception("Can't join face to itself");
			}

			// validate the edgeToDelete is in both faces, edgeToDelete is only in these two faces, the two faces only share this one edge and no other edges

			FaceEdge faceEdgeToDeleteOnFaceToKeep = meshEdgeToDelete.GetFaceEdge(faceToKeep);
			FaceEdge faceEdgeToDeleteOnFaceToDelete = meshEdgeToDelete.GetFaceEdge(faceToDelete);

			if (faceEdgeToDeleteOnFaceToKeep.FirstVertex == faceEdgeToDeleteOnFaceToDelete.FirstVertex)
			{
				throw new Exception("The faces have opposite windings and you cannot merge the edge");
			}

			faceEdgeToDeleteOnFaceToKeep.prevFaceEdge.nextFaceEdge = faceEdgeToDeleteOnFaceToDelete.nextFaceEdge;
			faceEdgeToDeleteOnFaceToDelete.nextFaceEdge.prevFaceEdge = faceEdgeToDeleteOnFaceToKeep.prevFaceEdge;

			faceEdgeToDeleteOnFaceToKeep.nextFaceEdge.prevFaceEdge = faceEdgeToDeleteOnFaceToDelete.prevFaceEdge;
			faceEdgeToDeleteOnFaceToDelete.prevFaceEdge.nextFaceEdge = faceEdgeToDeleteOnFaceToKeep.nextFaceEdge;

			// if the face we are deleting is the one that the face to keep was looking at as its starting face edge, move it to the next face edge
			if (faceToKeep.firstFaceEdge == faceEdgeToDeleteOnFaceToKeep)
			{
				faceToKeep.firstFaceEdge = faceToKeep.firstFaceEdge.nextFaceEdge;
			}

			// make sure the FaceEdges all point to the kept face.
			foreach (FaceEdge faceEdge in faceToKeep.firstFaceEdge.NextFaceEdges())
			{
				faceEdge.ContainingFace = faceToKeep;
			}

			DeleteMeshEdge(meshEdgeToDelete);

			// clear the data on the deleted face edge to help with debugging
			faceEdgeToDeleteOnFaceToKeep.meshEdge.VertexOnEnd[0] = null;
			faceEdgeToDeleteOnFaceToKeep.meshEdge.VertexOnEnd[1] = null;
			faceToDelete.firstFaceEdge = null;
			// take the face out of the face list
			Faces.Remove(faceToDelete);
		}

		#endregion Operations

		#region Vertex

		public IVertex CreateVertex(double x, double y, double z, CreateOption createOption = CreateOption.ReuseExisting, SortOption sortOption = SortOption.SortNow)
		{
			return CreateVertex(new Vector3(x, y, z), createOption, sortOption);
		}

		public IVertex CreateVertex(Vector3 position, CreateOption createOption = CreateOption.ReuseExisting, SortOption sortOption = SortOption.SortNow, double maxDistanceToConsiderVertexAsSame = 0)
		{
			if (createOption == CreateOption.ReuseExisting)
			{
				List<IVertex> existingVertices = FindVertices(position, maxDistanceToConsiderVertexAsSame);
				if (existingVertices != null && existingVertices.Count > 0)
				{
					return existingVertices[0];
				}
			}

			IVertex createdVertex = new Vertex(position);
			Vertices.Add(createdVertex, sortOption);
			return createdVertex;
		}

		public void DeleteVertex(IVertex vertex)
		{
			throw new NotImplementedException();
		}

		public List<IVertex> FindVertices(Vector3 position, double maxDistanceToConsiderVertexAsSame = 0)
		{
			return Vertices.FindVertices(position, maxDistanceToConsiderVertexAsSame);
		}

		public void MergeVertices(CancellationToken cancellationToken, double maxDistanceToConsiderVertexAsSame = 0, Action<double, string> reportProgress = null)
		{
			HashSet<IVertex> markedForDeletion = new HashSet<IVertex>();
			Stopwatch maxProgressReport = new Stopwatch();
			maxProgressReport.Start();

			for (int i = 0; i < Vertices.Count; i++)
			{
				IVertex vertexToKeep = Vertices[i];
				if (!markedForDeletion.Contains(vertexToKeep))
				{
					List<IVertex> samePosition = Vertices.FindVertices(vertexToKeep.Position, maxDistanceToConsiderVertexAsSame);
					foreach (IVertex vertexToDelete in samePosition)
					{
						if (vertexToDelete != vertexToKeep)
						{
							if (!markedForDeletion.Contains(vertexToDelete))
							{
								//Validate(markedForDeletion);
								MergeVertices(vertexToKeep, vertexToDelete, false);
								markedForDeletion.Add(vertexToDelete);
								//Validate(markedForDeletion);
							}
						}
					}

					if (reportProgress != null
						&& maxProgressReport.ElapsedMilliseconds > 200)
					{
						reportProgress(i / (double)Vertices.Count, "Merging Vertices");
						if (cancellationToken.IsCancellationRequested)
						{
							return;
						}
						maxProgressReport.Restart();
					}
				}
			}

			//Validate(markedForDeletion);
			reportProgress?.Invoke(1, "Deleting Unused Vertices");

			RemoveVerticesMarkedForDeletion(markedForDeletion);
		}

		public void MergeVertices(IVertex vertexToKeep, IVertex vertexToDelete, bool doActualDeletion = true)
		{
			/* this check is relatively slow
						if (!Vertices.ContainsAVertexAtPosition(vertexToKeep) || !Vertices.ContainsAVertexAtPosition(vertexToDelete))
						{
							throw new Exception("Both vertexes have to be part of this mesh to be merged.");
						}
			*/
			// fix up the mesh edges
			List<MeshEdge> connectedMeshEdges = vertexToDelete.GetConnectedMeshEdges();
			foreach (MeshEdge meshEdgeToFix in connectedMeshEdges)
			{
				// fix up the face edges
				foreach (FaceEdge faceEdge in meshEdgeToFix.FaceEdgesSharingMeshEdge())
				{
					if (faceEdge.FirstVertex == vertexToDelete)
					{
						faceEdge.FirstVertex = vertexToKeep;
					}
				}

				// fix up the mesh edge
				if (meshEdgeToFix.VertexOnEnd[0] == vertexToDelete)
				{
					meshEdgeToFix.VertexOnEnd[0] = vertexToKeep;
				}
				else if (meshEdgeToFix.VertexOnEnd[1] == vertexToDelete)
				{
					meshEdgeToFix.VertexOnEnd[1] = vertexToKeep;
				}

				// make sure it is in the vertex edge loop
				meshEdgeToFix.AddToMeshEdgeLinksOfVertex(vertexToKeep);
			}

			// delete the vertex
			if (doActualDeletion)
			{
				Vertices.Remove(vertexToDelete);
			}
		}

		public void SortVertices(Action<double, string> reportProgress = null)
		{
			reportProgress?.Invoke(0, "Sorting Vertices");

			Vertices.Sort();

			reportProgress?.Invoke(1, "Sorting Vertices");
		}

		private void RemoveVerticesMarkedForDeletion(HashSet<IVertex> markedForDeletion)
		{
			VertexCollecton NonDeleteVertices = new VertexCollecton();
			for (int i = 0; i < Vertices.Count; i++)
			{
				IVertex vertexToCheck = Vertices[i];
				if (!markedForDeletion.Contains(vertexToCheck))
				{
					NonDeleteVertices.Add(vertexToCheck, SortOption.WillSortLater);
				}
			}

			// we put them in in the same order they were in, so we keep the state
			NonDeleteVertices.IsSorted = Vertices.IsSorted;
			Vertices = NonDeleteVertices;
		}

		#endregion Vertex

		#region MeshEdge

		public MeshEdge CreateMeshEdge(IVertex vertex1, IVertex vertex2, CreateOption createOption = CreateOption.ReuseExisting)
		{
			if (false)//!vertices.Contains(vertex1) || !vertices.Contains(vertex2))
			{
				throw new ArgumentException("the two vertices must be in the vertices list before a mesh edge can be made between them.");
			}

			if (vertex1 == vertex2)
			{
				throw new ArgumentException("Your input vertices must not be the same vertex.");
			}

			if (createOption == CreateOption.ReuseExisting)
			{
				MeshEdge existingMeshEdge = vertex1.GetMeshEdgeConnectedToVertex(vertex2);
				if (existingMeshEdge != null)
				{
					return existingMeshEdge;
				}
			}

			MeshEdge createdMeshEdge = new MeshEdge(vertex1, vertex2);

			MeshEdges.Add(createdMeshEdge);

			return createdMeshEdge;
		}

		public void DeleteMeshEdge(MeshEdge meshEdgeToDelete)
		{
			// make sure we take the mesh edge out of the neighbors pointers
			meshEdgeToDelete.RemoveFromMeshEdgeLinksOfVertex(meshEdgeToDelete.VertexOnEnd[0]);
			meshEdgeToDelete.RemoveFromMeshEdgeLinksOfVertex(meshEdgeToDelete.VertexOnEnd[1]);

			// clear the data on the deleted mesh edge to help with debugging
			meshEdgeToDelete.firstFaceEdge = null;
			meshEdgeToDelete.VertexOnEnd[0] = null;
			meshEdgeToDelete.NextMeshEdgeFromEnd[0] = null;
			meshEdgeToDelete.VertexOnEnd[1] = null;
			meshEdgeToDelete.NextMeshEdgeFromEnd[1] = null;

			MeshEdges.Remove(meshEdgeToDelete);
		}

		public List<MeshEdge> FindMeshEdges(IVertex vertex1, IVertex vertex2)
		{
			List<MeshEdge> meshEdges = new List<MeshEdge>();

			foreach (MeshEdge meshEdge in vertex1.ConnectedMeshEdges())
			{
				if (meshEdge.IsConnectedTo(vertex2))
				{
					meshEdges.Add(meshEdge);
				}
			}

			return meshEdges;
		}

		public void MergeMeshEdges(CancellationToken cancellationToken, Action<double, string> reportProgress = null)
		{
			HashSet<MeshEdge> markedForDeletion = new HashSet<MeshEdge>();
			Stopwatch maxProgressReport = new Stopwatch();
			maxProgressReport.Start();

			for (int i = 0; i < MeshEdges.Count; i++)
			{
				MeshEdge currentMeshEdge = MeshEdges[i];
				if (!markedForDeletion.Contains(currentMeshEdge))
				{
					IVertex vertex0 = currentMeshEdge.VertexOnEnd[0];
					IVertex vertex1 = currentMeshEdge.VertexOnEnd[1];

					// find out if there is another edge attached to the same vertexes
					List<MeshEdge> meshEdgesToDelete = FindMeshEdges(vertex0, vertex1);

					if (meshEdgesToDelete.Count > 1)
					{
						foreach (MeshEdge meshEdgeToDelete in meshEdgesToDelete)
						{
							if (meshEdgeToDelete != currentMeshEdge)
							{
								if (!markedForDeletion.Contains(meshEdgeToDelete))
								{
									MergeMeshEdges(currentMeshEdge, meshEdgeToDelete, false);
									markedForDeletion.Add(meshEdgeToDelete);
								}
							}
						}
					}
				}

				if (reportProgress != null)
				{
					if (maxProgressReport.ElapsedMilliseconds > 200)
					{
						reportProgress(i / (double)MeshEdges.Count, "Merging Mesh Edges");
						maxProgressReport.Restart();
						if (cancellationToken.IsCancellationRequested)
						{
							return;
						}
					}
				}
			}

			RemoveMeshEdgesMarkedForDeletion(markedForDeletion);
		}

		public void MergeMeshEdges(MeshEdge edgeToKeep, MeshEdge edgeToDelete, bool doActualDeletion = true)
		{
			// make sure they share vertexes (or they can't be merged)
			if (!edgeToDelete.IsConnectedTo(edgeToKeep.VertexOnEnd[0])
				|| !edgeToDelete.IsConnectedTo(edgeToKeep.VertexOnEnd[1]))
			{
				throw new Exception("These mesh edges do not share vertexes and can't be merged.");
			}

			edgeToDelete.RemoveFromMeshEdgeLinksOfVertex(edgeToKeep.VertexOnEnd[0]);
			edgeToDelete.RemoveFromMeshEdgeLinksOfVertex(edgeToKeep.VertexOnEnd[1]);

			// fix any face edges that are referencing the edgeToDelete
			foreach (FaceEdge attachedFaceEdge in edgeToDelete.firstFaceEdge.RadialNextFaceEdges())
			{
				attachedFaceEdge.meshEdge = edgeToKeep;
			}

			List<FaceEdge> radialLoopToMove = new List<FaceEdge>();
			foreach (FaceEdge faceEdge in edgeToDelete.firstFaceEdge.RadialNextFaceEdges())
			{
				radialLoopToMove.Add(faceEdge);
			}

			foreach (FaceEdge faceEdge in radialLoopToMove)
			{
				faceEdge.AddToRadialLoop(edgeToKeep);
			}

			if (doActualDeletion)
			{
				MeshEdges.Remove(edgeToDelete);
			}
		}

		public void SplitMeshEdge(MeshEdge meshEdgeToSplit, out IVertex vertexCreatedDuringSplit, out MeshEdge meshEdgeCreatedDuringSplit)
		{
			// create our new Vertex and MeshEdge
			{
				// make a new vertex between the existing ones

				// TODO: make this create an interpolated vertex, check if it exits and add it or use the right one.
				//vertexCreatedDuringSplit = meshEdgeToSplit.edgeEndVertex[0].CreateInterpolated(meshEdgeToSplit.edgeEndVertex[1], .5);
				vertexCreatedDuringSplit = CreateVertex((meshEdgeToSplit.VertexOnEnd[0].Position + meshEdgeToSplit.VertexOnEnd[1].Position) / 2);
				// TODO: check if the mesh edge exits and use the existing one (or not)
				meshEdgeCreatedDuringSplit = new MeshEdge();
			}

			// Set the new firstMeshEdge on the new Vertex
			vertexCreatedDuringSplit.FirstMeshEdge = meshEdgeCreatedDuringSplit;

			IVertex existingVertexToConectTo = meshEdgeToSplit.VertexOnEnd[1];
			// fix the Vertex references on the MeshEdges
			{
				// and set the edges to point to this new one
				meshEdgeCreatedDuringSplit.VertexOnEnd[0] = vertexCreatedDuringSplit;
				meshEdgeCreatedDuringSplit.VertexOnEnd[1] = existingVertexToConectTo;
				meshEdgeToSplit.VertexOnEnd[1] = vertexCreatedDuringSplit;
			}

			// fix the MeshEdgeLinks on the MeshEdges
			{
				// set the created edge to be connected to the old edges other mesh edges
				meshEdgeCreatedDuringSplit.NextMeshEdgeFromEnd[0] = meshEdgeToSplit;

				// make anything that pointed to the split edge point to the new mesh edge

				meshEdgeToSplit.NextMeshEdgeFromEnd[1] = meshEdgeCreatedDuringSplit;
			}

			// if the MeshEdge is part of a face than we have to fix the face up
			FaceEdge faceEdgeToSplit = meshEdgeToSplit.firstFaceEdge;
			if (faceEdgeToSplit != null)
			{
				foreach (FaceEdge faceEdge in meshEdgeToSplit.FaceEdgesSharingMeshEdge())
				{
					Face currentFace = faceEdge.ContainingFace;
					FaceEdge newFaceEdge = new FaceEdge(currentFace, meshEdgeCreatedDuringSplit, vertexCreatedDuringSplit);
					newFaceEdge.AddToRadialLoop(meshEdgeCreatedDuringSplit);
					// and inject it into the face loop for this face
					newFaceEdge.prevFaceEdge = faceEdge;
					newFaceEdge.nextFaceEdge = faceEdge.nextFaceEdge;
					faceEdge.nextFaceEdge.prevFaceEdge = newFaceEdge;
					faceEdge.nextFaceEdge = newFaceEdge;
				}
			}
		}

		/// <summary>
		/// Unsplit (merge) the edgeToJoin and the edge that it is connected to through vertexToDelete.
		/// Only unsplit the edge if we are reversing what would have been a split (a single vertex connecting only two edges).
		/// </summary>
		/// <param name="edgeToJoin"></param>
		/// <param name="vertexToDelete"></param>
		/// <returns></returns>
		public void UnsplitMeshEdge(MeshEdge edgeToJoin, IVertex vertexToDelete)
		{
			int endToJoinIndex = edgeToJoin.GetVertexEndIndex(vertexToDelete);

			MeshEdge edgeToDelete = edgeToJoin.GetNextMeshEdgeConnectedTo(vertexToDelete);
			if (edgeToDelete.GetNextMeshEdgeConnectedTo(vertexToDelete) != edgeToJoin)
			{
				// make sure the edgeToJoin is a valid unsplit (only one connection)
				throw new Exception("The edge that is being unsplit must be connected to only one other MeshEdge across the vertexToDelete.");
			}

			int otherEndOfEdgeToDelete = edgeToDelete.GetOpositeVertexEndIndex(vertexToDelete);
			MeshEdge edgeToJoinTo = edgeToDelete.NextMeshEdgeFromEnd[otherEndOfEdgeToDelete];

			// if the MeshEdge is part of any faces than we have to fix the faces.
			if (edgeToJoin.firstFaceEdge != null)
			{
				// The edge we split was part of one or more faces, we need to fix the FaceEdge loops
				foreach (FaceEdge faceEdge in edgeToJoin.FaceEdgesSharingMeshEdge())
				{
					FaceEdge faceEdgeToDelete = null;
					if (faceEdge.nextFaceEdge.meshEdge == edgeToDelete)
					{
						faceEdgeToDelete = faceEdge.nextFaceEdge;
						FaceEdge newNextFaceEdge = faceEdgeToDelete.nextFaceEdge;
						newNextFaceEdge.prevFaceEdge = faceEdge;
						faceEdge.nextFaceEdge = newNextFaceEdge;
					}
					else if (faceEdge.prevFaceEdge.meshEdge == edgeToDelete)
					{
						faceEdgeToDelete = faceEdge.prevFaceEdge;
						FaceEdge newPrevFaceEdge = faceEdgeToDelete.prevFaceEdge;
						newPrevFaceEdge.nextFaceEdge = faceEdge;
						faceEdge.prevFaceEdge = newPrevFaceEdge;
					}
					else
					{
						throw new Exception("Either the next or prev edge must be the same as the edge to delete.");
					}

					// if the FaceEdge we are deleting is the one that the face was using as its firstFaceEdge, change it.
					if (faceEdge.ContainingFace.firstFaceEdge == faceEdgeToDelete)
					{
						faceEdge.ContainingFace.firstFaceEdge = faceEdge;
					}

					// and clear out the FaceEdge we are deleting to help debugging and other references to it.
					faceEdgeToDelete.nextFaceEdge = null;
					faceEdgeToDelete.prevFaceEdge = null;
					faceEdgeToDelete.radialNextFaceEdge = null;
					faceEdgeToDelete.radialPrevFaceEdge = null;
					faceEdgeToDelete.meshEdge = null;
					faceEdgeToDelete.ContainingFace = null;
					faceEdgeToDelete.FirstVertex = null;
				}
			}

			// fix the MeshEdgeLinks on the edgeToJoin
			{
				edgeToJoin.VertexOnEnd[endToJoinIndex] = edgeToDelete.VertexOnEnd[otherEndOfEdgeToDelete];
				edgeToJoin.NextMeshEdgeFromEnd[endToJoinIndex] = edgeToDelete.NextMeshEdgeFromEnd[otherEndOfEdgeToDelete];
			}

			// Clear all  the data on the deleted vertex and edge so we have less code that will work if it continues to use them.
			vertexToDelete.FirstMeshEdge = null;
			edgeToDelete.firstFaceEdge = null;
			edgeToDelete.VertexOnEnd[0] = null;
			edgeToDelete.NextMeshEdgeFromEnd[0] = null;
			edgeToDelete.VertexOnEnd[1] = null;
			edgeToDelete.NextMeshEdgeFromEnd[1] = null;
		}

		private void RemoveMeshEdgesMarkedForDeletion(HashSet<MeshEdge> markedForDeletion)
		{
			List<MeshEdge> NonDeleteMeshEdges = new List<MeshEdge>();
			for (int i = 0; i < MeshEdges.Count; i++)
			{
				MeshEdge meshEdgeToCheck = MeshEdges[i];
				if (!markedForDeletion.Contains(meshEdgeToCheck))
				{
					NonDeleteMeshEdges.Add(meshEdgeToCheck);
				}
			}

			MeshEdges = NonDeleteMeshEdges;
		}

		#endregion MeshEdge

		#region Face

		public Face CreateFace(int[] vertexIndexList, CreateOption createOption = CreateOption.ReuseExisting)
		{
			List<IVertex> vertexList = new List<IVertex>();
			foreach (var index in vertexIndexList)
			{
				vertexList.Add(Vertices[index]);
			}
			return CreateFace(vertexList.ToArray(), createOption);
		}

		public Face CreateFace(IVertex[] verticesToUse, CreateOption createOption = CreateOption.ReuseExisting)
		{
			List<IVertex> nonRepeatingSet = new List<IVertex>(verticesToUse);
			for (int i = nonRepeatingSet.Count - 1; i > 0; i--)
			{
				if (nonRepeatingSet[i] == nonRepeatingSet[i - 1]
					|| nonRepeatingSet[i].Position == nonRepeatingSet[i - 1].Position)
				{
					nonRepeatingSet.RemoveAt(i);
				}
			}

			if (nonRepeatingSet.Count < 3
				|| (nonRepeatingSet.Count == 3
				&& (nonRepeatingSet[0].Position == nonRepeatingSet[1].Position
				|| nonRepeatingSet[1].Position == nonRepeatingSet[2].Position
				|| nonRepeatingSet[2].Position == nonRepeatingSet[0].Position)))
			{
				return null;
			}

			List<MeshEdge> edgesToUse = new List<MeshEdge>();
			for (int i = 0; i < nonRepeatingSet.Count - 1; i++)
			{
				edgesToUse.Add(CreateMeshEdge(nonRepeatingSet[i], nonRepeatingSet[i + 1], createOption));
			}
			edgesToUse.Add(CreateMeshEdge(nonRepeatingSet[nonRepeatingSet.Count - 1], nonRepeatingSet[0], createOption));

			// make the face and set it's data
			Face createdFace = new Face(this);

			CreateFaceEdges(nonRepeatingSet.ToArray(), edgesToUse, createdFace);

			createdFace.CalculateNormal();

			Faces.Add(createdFace);

			return createdFace;
		}

		public void DeleteFace(Face faceToDelete)
		{
			// fix the radial face edges and the mesh edeges
			List<FaceEdge> faceEdgesToDelete = new List<FaceEdge>(faceToDelete.FaceEdges());
			foreach (var faceEdgeToDelete in faceEdgesToDelete)
			{
				if (faceEdgeToDelete.meshEdge.firstFaceEdge == faceEdgeToDelete)
				{
					// make sure the mesh edge is not pointing to this face edeg
					if (faceEdgeToDelete.radialNextFaceEdge == faceEdgeToDelete)
					{
						// it point to itself, so the edge will point to nothing
						faceEdgeToDelete.meshEdge.firstFaceEdge = null;
					}
					else
					{
						faceEdgeToDelete.meshEdge.firstFaceEdge = faceEdgeToDelete.radialNextFaceEdge;
					}
				}
				FaceEdge temp = faceEdgeToDelete.radialNextFaceEdge.radialPrevFaceEdge;
				faceEdgeToDelete.radialPrevFaceEdge.radialNextFaceEdge = faceEdgeToDelete.radialPrevFaceEdge;
				faceEdgeToDelete.radialNextFaceEdge.radialPrevFaceEdge = faceEdgeToDelete.nextFaceEdge;
			}

			// clear the data on the deleted face edge to help with debugging
			faceToDelete.firstFaceEdge = null;
			// take the face out of the face list
			Faces.Remove(faceToDelete);
		}

		public List<Face> FindFacesAtPosition(IVertex[] vertices)
		{
			if (vertices.Length > 0)
			{
				List<Vector3> positions = new List<Vector3>();
				foreach (IVertex vertex in vertices)
				{
					positions.Add(vertex.Position);
				}

				List<Face> sharedFaces = new List<Face>();
				List<IVertex> sharedVertices = FindVertices(vertices[0].Position);
				if (sharedVertices.Count > 0)
				{
					// we have found 1 or more shared vertexes (with the first vertex)
					// let's get all the faces that also share the rest of the vertices
					foreach (IVertex sharedVertex in sharedVertices)
					{
						foreach (Face connectedFace in sharedVertex.ConnectedFaces())
						{
							bool allShared = true;
							foreach (IVertex checkVertex in connectedFace.Vertices())
							{
								if (!positions.Contains(checkVertex.Position))
								{
									allShared = false;
									break;
								}
							}

							if (allShared)
							{
								sharedFaces.Add(connectedFace);
							}
						}
					}

					return sharedFaces;
				}
			}

			return null;
		}

		public List<MeshEdge> GetNonManifoldEdges()
		{
			List<MeshEdge> nonManifoldEdges = new List<MeshEdge>();

			foreach (MeshEdge meshEdge in MeshEdges)
			{
				int numFacesSharingEdge = meshEdge.GetNumFacesSharingEdge();
				if (numFacesSharingEdge != 2)
				{
					nonManifoldEdges.Add(meshEdge);
				}
			}

			return nonManifoldEdges;
		}

		private static void CreateFaceEdges(IVertex[] verticesToUse, List<MeshEdge> edgesToUse, Face createdFace)
		{
			FaceEdge prevFaceEdge = null;
			for (int i = 0; i < verticesToUse.Length - 1; i++)
			{
				MeshEdge currentMeshEdge = edgesToUse[i];
				FaceEdge currentFaceEdge = new FaceEdge(createdFace, currentMeshEdge, verticesToUse[i]);
				if (i == 0)
				{
					createdFace.firstFaceEdge = currentFaceEdge;
				}
				else
				{
					prevFaceEdge.nextFaceEdge = currentFaceEdge;
					currentFaceEdge.prevFaceEdge = prevFaceEdge;
				}
				currentFaceEdge.AddToRadialLoop(currentMeshEdge);
				prevFaceEdge = currentFaceEdge;
			}
			// make the last FaceEdge
			{
				MeshEdge currentMeshEdge = edgesToUse[verticesToUse.Length - 1];
				FaceEdge currentFaceEdge = new FaceEdge(createdFace, currentMeshEdge, verticesToUse[verticesToUse.Length - 1]);
				prevFaceEdge.nextFaceEdge = currentFaceEdge;
				currentFaceEdge.prevFaceEdge = prevFaceEdge;
				currentFaceEdge.nextFaceEdge = createdFace.firstFaceEdge;
				createdFace.firstFaceEdge.prevFaceEdge = currentFaceEdge;
				currentFaceEdge.AddToRadialLoop(currentMeshEdge);
			}
		}

		private static void DeleteFaceEdge(FaceEdge faceEdgeToDelete)
		{
		}

		#endregion Face

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
		{
			if (Vertices.Count == 0)
			{
				return new AxisAlignedBoundingBox(Vector3.Zero, Vector3.Zero);
			}

			if (cachedAABB == null)
			{
				Vector3 minXYZ = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
				Vector3 maxXYZ = new Vector3(double.MinValue, double.MinValue, double.MinValue);

				foreach (IVertex vertex in Vertices)
				{
					minXYZ.X = Math.Min(minXYZ.X, vertex.Position.X);
					minXYZ.Y = Math.Min(minXYZ.Y, vertex.Position.Y);
					minXYZ.Z = Math.Min(minXYZ.Z, vertex.Position.Z);

					maxXYZ.X = Math.Max(maxXYZ.X, vertex.Position.X);
					maxXYZ.Y = Math.Max(maxXYZ.Y, vertex.Position.Y);
					maxXYZ.Z = Math.Max(maxXYZ.Z, vertex.Position.Z);
				}

				cachedAABB = new AxisAlignedBoundingBox(minXYZ, maxXYZ);
			}

			return cachedAABB;
		}

		public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 transform)
		{
			return transformedAabbCache.GetAxisAlignedBoundingBox(this, GetAxisAlignedBoundingBox(), transform);
		}

		public override string ToString()
		{
			return $"ID = {ID}, Faces = {Faces.Count}";
		}

		public override int GetHashCode()
		{
			return (int)GetLongHashCode();
		}

		#endregion Public Members
	}
}