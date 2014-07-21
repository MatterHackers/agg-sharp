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
using System.ComponentModel;
using System.Linq;
using System.Text;
using System.Diagnostics;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
    public delegate bool ReportProgress(double progress0To1, string processingState);

    [DebuggerDisplay("ID = {Data.ID}")]
    public class Mesh
    {
        static public readonly double DefaultMaxDistanceToConsiderVertexAsSame = .0000001;

        double maxDistanceToConsiderVertexAsSame = DefaultMaxDistanceToConsiderVertexAsSame;
        public double MaxDistanceToConsiderVertexAsSame
        {
            get
            {
                return maxDistanceToConsiderVertexAsSame;
            }

            set
            {
                maxDistanceToConsiderVertexAsSame = value;
            }
        }

        int changedCount = 0;
        public int ChangedCount { get { return changedCount; } }

        public void MarkAsChanged()
        {
            // mark this unchecked as we don't want to throw an exception if this rolls over.
            unchecked
            {
                changedCount++;
            }
        }

        public MetaData Data
        {
            get
            {
                return MetaData.Get(this);
            }
        }

        VertexCollecton vertices = new VertexCollecton();
        public VertexCollecton Vertices
        {
            get
            {
                return vertices;
            }
        }

        public List<MeshEdge> meshEdges = new List<MeshEdge>();
        List<Face> faces = new List<Face>();

        public Mesh()
        {
        }

        public Mesh(Mesh meshToCopy)
        {
            foreach (Face face in meshToCopy.Faces)
            {
                List<Vertex> faceVertices = new List<Vertex>();
                foreach (FaceEdge faceEdgeToAdd in face.FaceEdges())
                {
                    Vertex newVertex = CreateVertex(faceEdgeToAdd.firstVertex.Position, true, true);
                    faceVertices.Add(newVertex);
                }

                CreateFace(faceVertices.ToArray(), true);
            }

            CleanAndMergMesh();
        }

        public void CleanAndMergMesh(ReportProgress reportProgress = null)
        {
            SortVertices((double progress0To1, string processingState) => { return reportProgress(progress0To1 * .41, processingState); });
            MergeVertices((double progress0To1, string processingState) => { return reportProgress(progress0To1 * .23 + .41, processingState); });
            MergeMeshEdges((double progress0To1, string processingState) => { return reportProgress(progress0To1 * .36 + .64, processingState); });
        }

        public List<Face> Faces
        {
            get
            {
                return faces;
            }
        }

        #region Public Members
        #region Debug
        public string GetConnectionInfoAsString()
        {
            StringBuilder totalDebug = new StringBuilder();
            totalDebug.Append(String.Format("Mesh: {0}\n", Data.ID));
            foreach (Vertex vertex in vertices)
            {
                totalDebug.Append(new string('\t', 1) + String.Format("Vertex: {0}\n", vertex.Data.ID));
                vertex.AddDebugInfo(totalDebug, 2);
            }
            foreach (MeshEdge meshEdge in meshEdges)
            {
                totalDebug.Append(new string('\t', 1) + String.Format("MeshEdge: {0}\n", meshEdge.Data.ID));
                meshEdge.AddDebugInfo(totalDebug, 2);
            }
            foreach (Face face in faces)
            {
                totalDebug.Append(new string('\t', 1) + String.Format("Face: {0}\n", face.Data.ID));
                face.AddDebugInfo(totalDebug, 2);
            }

            return totalDebug.ToString();
        }

        public void Validate()
        {
            foreach (Vertex vertex in vertices)
            {
                vertex.Validate();
            }
            foreach (MeshEdge meshEdge in meshEdges)
            {
                meshEdge.Validate();
            }
            foreach (Face face in faces)
            {
                face.Validate();
            }
        }
        #endregion // Debug

        #region Operations
        public bool DeleteVertexFromMeshEdge(MeshEdge meshEdgeDeleteVertexFrom, Vertex vertexToDelete)
        {
            throw new NotImplementedException();
        }

        public bool ContainsVertex(Vertex vertexToLookFor)
        {
            return vertices.ContainsAVertexAtPosition(vertexToLookFor);
        }

        public void SplitFace(Face faceToSplit, Vertex splitStartVertex, Vertex splitEndVertex, out MeshEdge meshEdgeCreatedDurringSplit, out Face faceCreatedDurringSplit)
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
            foreach(FaceEdge faceEdge in faceToSplit.FaceEdges())
            {
                if(faceEdge.firstVertex == splitStartVertex)
                {
                    faceEdgeAfterSplitStart = faceEdge;
                    count++;
                }
                else if(faceEdge.firstVertex == splitEndVertex)
                {
                    faceEdgeAfterSplitEnd = faceEdge;
                    count++;
                }
                if (count==2)
                {
                    break; // stop if we found both face edges
                }
            }

            meshEdgeCreatedDurringSplit = CreateMeshEdge(splitStartVertex, splitEndVertex);
            faceCreatedDurringSplit = new Face(faceToSplit);

            faces.Add(faceCreatedDurringSplit);

            FaceEdge newFaceEdgeExistingFace = new FaceEdge(faceToSplit, meshEdgeCreatedDurringSplit, splitStartVertex);
            FaceEdge newFaceEdgeForNewFace = new FaceEdge(faceCreatedDurringSplit, meshEdgeCreatedDurringSplit, splitEndVertex);

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
            faceCreatedDurringSplit.firstFaceEdge = newFaceEdgeForNewFace;

            // make sure the FaceEdges of the new face all point to the new face.
            foreach (FaceEdge faceEdge in faceCreatedDurringSplit.firstFaceEdge.NextFaceEdges())
            {
                faceEdge.containingFace = faceCreatedDurringSplit;
            }

            newFaceEdgeExistingFace.AddToRadialLoop(meshEdgeCreatedDurringSplit);
            newFaceEdgeForNewFace.AddToRadialLoop(meshEdgeCreatedDurringSplit);
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

            if (faceEdgeToDeleteOnFaceToKeep.firstVertex == faceEdgeToDeleteOnFaceToDelete.firstVertex)
            {
                throw new Exception("The faces have oposite windings and you cannot merge the edge");
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
                faceEdge.containingFace = faceToKeep;
            }

            // make sure we take the mesh edge out of the neighbors pointers
            meshEdgeToDelete.RemoveFromMeshEdgeLinksOfVertex(meshEdgeToDelete.VertexOnEnd[0]);
            meshEdgeToDelete.RemoveFromMeshEdgeLinksOfVertex(meshEdgeToDelete.VertexOnEnd[1]);

            // clear the data on the deleted face edge to help with debugging
            faceEdgeToDeleteOnFaceToKeep.meshEdge.VertexOnEnd[0] = null;
            faceEdgeToDeleteOnFaceToKeep.meshEdge.VertexOnEnd[1] = null;
            faceToDelete.firstFaceEdge = null;
            // take the face out of the face list
            faces.Remove(faceToDelete);

            // clear the data on the deleted mesh edge to help with debugging
            meshEdgeToDelete.firstFaceEdge = null;
            meshEdgeToDelete.VertexOnEnd[0] = null;
            meshEdgeToDelete.NextMeshEdgeFromEnd[0] = null;
            meshEdgeToDelete.VertexOnEnd[1] = null;
            meshEdgeToDelete.NextMeshEdgeFromEnd[1] = null;

            meshEdges.Remove(meshEdgeToDelete);
        }

        public bool DeleteMeshEdgeFromFace(Face faceToDeleteEdgeFrom, MeshEdge meshEdgeToDelete)
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

        #endregion // Operations

        #region Vertex
        public Vertex CreateVertex(double x, double y, double z, bool allowDuplicate = false, bool willSortLater = false)
        {
            return CreateVertex(new Vector3(x, y, z), allowDuplicate, willSortLater);
        }

        public List<Vertex> FindVertices(Vector3 position)
        {
            return vertices.FindVertices(position, MaxDistanceToConsiderVertexAsSame);
        }

        public Vertex CreateVertex(Vector3 position, bool allowDuplicate = false, bool willSortLater = false)
        {
            if (!allowDuplicate)
            {
                List<Vertex> existingVertices = FindVertices(position);
                if (existingVertices != null && existingVertices.Count > 0)
                {
                    return existingVertices[0];
                }
            }

            Vertex createdVertex = new Vertex(position);
            vertices.Add(createdVertex, willSortLater);
            return createdVertex;
        }

        public void DeleteVertex(Vertex vertex)
        {
            throw new NotImplementedException();
        }

        public void SortVertices(ReportProgress reportProgress = null)
        {
            if (reportProgress != null)
            {
                reportProgress(0, "Sorting Vertices");
            }
            Vertices.Sort();
            if (reportProgress != null)
            {
                reportProgress(1, "Sorting Vertices");
            }
        }

        public void MergeVertices(ReportProgress reportProgress = null, double maxDistanceToConsiderVertexAsSame = 0)
        {
            HashSet<Vertex> markedForDeletion = new HashSet<Vertex>();
            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();

            for (int i = 0; i < Vertices.Count; i++)
            {
                Vertex vertexToCheck = Vertices[i];
                if (!markedForDeletion.Contains(vertexToCheck))
                {
                    List<Vertex> samePosition = Vertices.FindVertices(vertexToCheck.Position, maxDistanceToConsiderVertexAsSame);
                    foreach (Vertex vertexToDelete in samePosition)
                    {
                        if (vertexToDelete != vertexToCheck)
                        {
                            if (!markedForDeletion.Contains(vertexToDelete))
                            {
                                MergeVertices(vertexToCheck, vertexToDelete, false);
                                markedForDeletion.Add(vertexToDelete);
                            }
                        }
                    }

                    if (reportProgress != null)
                    {
                        if (maxProgressReport.ElapsedMilliseconds > 200)
                        {
                            reportProgress(i / (double)Vertices.Count, "Merging Vertices");
                            maxProgressReport.Restart();
                        }
                    }
                }
            }

            if (reportProgress != null)
            {
                reportProgress(1, "Deleting Unused Vertices");
            }
            RemoveVerticesMarkedForDeletion(markedForDeletion);
        }

        private void RemoveVerticesMarkedForDeletion(HashSet<Vertex> markedForDeletion)
        {
            VertexCollecton NonDeleteVertices = new VertexCollecton();
            for (int i = 0; i < Vertices.Count; i++)
            {
                Vertex vertexToCheck = Vertices[i];
                if (!markedForDeletion.Contains(vertexToCheck))
                {
                    NonDeleteVertices.Add(vertexToCheck, true);
                }
            }

            // we put them in in the same order they were in, so we keep the state
            NonDeleteVertices.IsSorted = vertices.IsSorted;
            vertices = NonDeleteVertices;
        }

        public void MergeVertices(Vertex vertexToKeep, Vertex vertexToDelete, bool doActualDeletion = true)
        {
#if false // this check is relatively slow
            if (!Vertices.ContainsAVertexAtPosition(vertexToKeep) || !Vertices.ContainsAVertexAtPosition(vertexToDelete))
            {
                throw new Exception("Both vertexes have to be part of this mesh to be merged.");
            }
#endif
            // fix up the mesh edges
            List<MeshEdge> connectedMeshEdges = vertexToDelete.GetConnectedMeshEdges();
            foreach (MeshEdge meshEdgeToFix in connectedMeshEdges)
            {
                // fix up the face edges
                foreach (FaceEdge faceEdge in meshEdgeToFix.FaceEdgesSharingMeshEdge())
                {
                    if (faceEdge.firstVertex == vertexToDelete)
                    {
                        faceEdge.firstVertex = vertexToKeep;
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
        #endregion

        #region MeshEdge
        public List<MeshEdge> FindMeshEdges(Vertex vertex1, Vertex vertex2)
        {
            List<MeshEdge> meshEdges = new List<MeshEdge>();

            foreach (MeshEdge meshEdge in vertex1.ConnectedMeshEdges())
            {
                if(meshEdge.IsConnectedTo(vertex2))
                {
                    meshEdges.Add(meshEdge);
                }
            }

            return meshEdges;
        }

        public MeshEdge CreateMeshEdge(Vertex vertex1, Vertex vertex2, bool createEvenIfExists = false)
        {
            if (false)//!vertices.Contains(vertex1) || !vertices.Contains(vertex2))
            {
                throw new ArgumentException("the two vertices must be in the vertices list before a mesh edge can be made between them.");
            }

            if (vertex1 == vertex2)
            {
                throw new ArgumentException("Your input vertices must not be the same vertex.");
            }

            if (!createEvenIfExists)
            {
                MeshEdge existingMeshEdge = vertex1.GetMeshEdgeConnectedToVertex(vertex2);
                if (existingMeshEdge != null)
                {
                    return existingMeshEdge;
                }
            }

            MeshEdge createdMeshEdge = new MeshEdge(vertex1, vertex2);

            meshEdges.Add(createdMeshEdge);

            return createdMeshEdge;
        }

        public void DeleteMeshEdge(MeshEdge meshEdgeToDelete)
        {
        }

        public void SplitMeshEdge(MeshEdge meshEdgeToSplit, out Vertex vertexCreatedDurringSplit, out MeshEdge meshEdgeCreatedDurringSplit)
        {
            // create our new Vertex and MeshEdge
            {
                // make a new vertex between the existing ones

                // TODO: make this create an interpolated vertex, check if it exits and add it or use the right one.
                //vertexCreatedDurringSplit = meshEdgeToSplit.edgeEndVertex[0].CreateInterpolated(meshEdgeToSplit.edgeEndVertex[1], .5);
                vertexCreatedDurringSplit = CreateVertex((meshEdgeToSplit.VertexOnEnd[0].Position + meshEdgeToSplit.VertexOnEnd[1].Position) / 2);
                // TODO: check if the mesh edge exits and use the existing one (or not)
                meshEdgeCreatedDurringSplit = new MeshEdge();
            }

            // Set the new firstMeshEdge on the new Vertex
            vertexCreatedDurringSplit.firstMeshEdge = meshEdgeCreatedDurringSplit;

            // fix the Vertex references on the MeshEdges
            {
                // and set the edges to point to this new one
                meshEdgeCreatedDurringSplit.VertexOnEnd[0] = vertexCreatedDurringSplit;
                meshEdgeCreatedDurringSplit.VertexOnEnd[1] = meshEdgeToSplit.VertexOnEnd[1];
                meshEdgeToSplit.VertexOnEnd[1] = vertexCreatedDurringSplit;
            }

            // fix the MeshEdgeLinks on the MeshEdges
            {
                // set the created edge to be connected to the old edges other mesh edges
                meshEdgeCreatedDurringSplit.NextMeshEdgeFromEnd[0] = meshEdgeToSplit;
                // make their links point to eachother
                meshEdgeToSplit.NextMeshEdgeFromEnd[1] = meshEdgeCreatedDurringSplit;
            }

            // if the MeshEdge is part of a face than we have to fix the face up
            FaceEdge faceEdgeToSplit = meshEdgeToSplit.firstFaceEdge;
            if (faceEdgeToSplit != null)
            {
                foreach (FaceEdge faceEdge in meshEdgeToSplit.FaceEdgesSharingMeshEdge())
                {
                    Face currentFace = faceEdge.containingFace;
                    FaceEdge newFaceEdge = new FaceEdge(currentFace, meshEdgeCreatedDurringSplit, vertexCreatedDurringSplit);
                    newFaceEdge.AddToRadialLoop(meshEdgeCreatedDurringSplit);
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
        public void UnsplitMeshEdge(MeshEdge edgeToJoin, Vertex vertexToDelete)
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
                    if (faceEdge.containingFace.firstFaceEdge == faceEdgeToDelete)
                    {
                        faceEdge.containingFace.firstFaceEdge = faceEdge;
                    }

                    // and clear out the FaceEdge we are deleting to help debugging and other references to it.
                    faceEdgeToDelete.nextFaceEdge = null;
                    faceEdgeToDelete.prevFaceEdge = null;
                    faceEdgeToDelete.radialNextFaceEdge = null;
                    faceEdgeToDelete.radialPrevFaceEdge = null;
                    faceEdgeToDelete.meshEdge = null;
                    faceEdgeToDelete.containingFace = null;
                    faceEdgeToDelete.firstVertex = null;
                }
            }

            // fix the MeshEdgeLinks on the edgeToJoin
            {
                edgeToJoin.VertexOnEnd[endToJoinIndex] = edgeToDelete.VertexOnEnd[otherEndOfEdgeToDelete];
                edgeToJoin.NextMeshEdgeFromEnd[endToJoinIndex] = edgeToDelete.NextMeshEdgeFromEnd[otherEndOfEdgeToDelete];
            }

            // Clear all  the data on the deleted vertex and edge so we have less code that will work if it continues to use them.
            vertexToDelete.firstMeshEdge = null;
            edgeToDelete.firstFaceEdge = null;
            edgeToDelete.VertexOnEnd[0] = null;
            edgeToDelete.NextMeshEdgeFromEnd[0] = null;
            edgeToDelete.VertexOnEnd[1] = null;
            edgeToDelete.NextMeshEdgeFromEnd[1] = null;
        }

        public void MergeMeshEdges(ReportProgress reportProgress = null)
        {
            HashSet<MeshEdge> markedForDeletion = new HashSet<MeshEdge>();
            Stopwatch maxProgressReport = new Stopwatch();
            maxProgressReport.Start();

            for (int i = 0; i < meshEdges.Count; i++)
            {
                MeshEdge currentMeshEdge = meshEdges[i];
                if (!markedForDeletion.Contains(currentMeshEdge))
                {
                    Vertex vertex0 = currentMeshEdge.VertexOnEnd[0];
                    Vertex vertex1 = currentMeshEdge.VertexOnEnd[1];

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
                        reportProgress(i / (double)meshEdges.Count, "Merging Mesh Edges");
                        maxProgressReport.Restart();
                    }
                }
            }

            RemoveMeshEdgesMarkedForDeletion(markedForDeletion);
        }

        private void RemoveMeshEdgesMarkedForDeletion(HashSet<MeshEdge> markedForDeletion)
        {
            List<MeshEdge> NonDeleteMeshEdges = new List<MeshEdge>();
            for (int i = 0; i < meshEdges.Count; i++)
            {
                MeshEdge meshEdgeToCheck = meshEdges[i];
                if (!markedForDeletion.Contains(meshEdgeToCheck))
                {
                    NonDeleteMeshEdges.Add(meshEdgeToCheck);
                }
            }

            meshEdges = NonDeleteMeshEdges;
        }

        public void MergeMeshEdges(MeshEdge edgeToKeep, MeshEdge edgeToDelete, bool doActualDeletion = true)
        {
            // make sure they sare vertexes (or they can't be merged)
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
                meshEdges.Remove(edgeToDelete);
            }
        }

        #endregion // MeshEdge

        #region Face
        public Face CreateFace(Vertex[] verticesToUse, bool createMeshEdgesEvenIfExist = false)
        {
            if (verticesToUse.Length < 3)
            {
                throw new ArgumentException("A face cannot have less than 3 vertices.");
            }

            List<MeshEdge> edgesToUse = new List<MeshEdge>();
            for (int i = 0; i < verticesToUse.Length - 1; i++)
            {
                edgesToUse.Add(CreateMeshEdge(verticesToUse[i], verticesToUse[i + 1], createMeshEdgesEvenIfExist));
            }
            edgesToUse.Add(CreateMeshEdge(verticesToUse[verticesToUse.Length - 1], verticesToUse[0], createMeshEdgesEvenIfExist));

            // make the face and set it's data
            Face createdFace = new Face();
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
                MeshEdge currentMeshEdge = edgesToUse[verticesToUse.Length-1];
                FaceEdge currentFaceEdge = new FaceEdge(createdFace, currentMeshEdge, verticesToUse[verticesToUse.Length-1]);
                prevFaceEdge.nextFaceEdge = currentFaceEdge;
                currentFaceEdge.prevFaceEdge = prevFaceEdge;
                currentFaceEdge.nextFaceEdge = createdFace.firstFaceEdge;
                createdFace.firstFaceEdge.prevFaceEdge = currentFaceEdge;
                currentFaceEdge.AddToRadialLoop(currentMeshEdge);
            }

            createdFace.CalculateNormal();

            faces.Add(createdFace);

            return createdFace;
        }

        public List<MeshEdge> GetNonManifoldEdges()
        {
            List<MeshEdge> nonManifoldEdges = new List<MeshEdge>();

            foreach (MeshEdge meshEdge in meshEdges)
            {
                int numFacesSharingEdge = meshEdge.GetNumFacesSharingEdge();
                if (numFacesSharingEdge != 2)
                {
                    nonManifoldEdges.Add(meshEdge);
                }
            }

            return nonManifoldEdges;
        }

        public Face FindFace(Vertex[] vertices)
        {
            //throw new NotImplementedException();
            return null;
        }

        #endregion // Face

        public AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
        {
            Vector3 minXYZ = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
            Vector3 maxXYZ = new Vector3(double.MinValue, double.MinValue, double.MinValue);

            foreach (Vertex vertex in vertices)
            {
                minXYZ.x = Math.Min(minXYZ.x, vertex.Position.x);
                minXYZ.y = Math.Min(minXYZ.y, vertex.Position.y);
                minXYZ.z = Math.Min(minXYZ.z, vertex.Position.z);

                maxXYZ.x = Math.Max(maxXYZ.x, vertex.Position.x);
                maxXYZ.y = Math.Max(maxXYZ.y, vertex.Position.y);
                maxXYZ.z = Math.Max(maxXYZ.z, vertex.Position.z);
            }

            return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
        }

        public AxisAlignedBoundingBox GetAxisAlignedBoundingBox(Matrix4X4 transform)
        {
            Vector3 minXYZ = new Vector3(double.MaxValue, double.MaxValue, double.MaxValue);
            Vector3 maxXYZ = new Vector3(double.MinValue, double.MinValue, double.MinValue);

            foreach (Vertex vertex in vertices)
            {
                Vector3 position = Vector3.Transform(vertex.Position, transform);
                minXYZ.x = Math.Min(minXYZ.x, position.x);
                minXYZ.y = Math.Min(minXYZ.y, position.y);
                minXYZ.z = Math.Min(minXYZ.z, position.z);

                maxXYZ.x = Math.Max(maxXYZ.x, position.x);
                maxXYZ.y = Math.Max(maxXYZ.y, position.y);
                maxXYZ.z = Math.Max(maxXYZ.z, position.z);
            }

            return new AxisAlignedBoundingBox(minXYZ, maxXYZ);
        }

        #endregion // Public Members

        #region Private Members
        #endregion

        public void Translate(Vector3 offset)
        {
            if (offset != Vector3.Zero)
            {
                foreach (Vertex vertex in Vertices)
                {
                    vertex.Position += offset;
                }
                MarkAsChanged();
            }
        }

        public void Transform(Matrix4X4 matrix)
        {
            if (matrix != Matrix4X4.Identity)
            {
                foreach (Vertex vertex in Vertices)
                {
                    vertex.Position = Vector3.Transform(vertex.Position, matrix);
                }
                foreach(Face face in Faces)
                {
                    face.CalculateNormal();
                }
                MarkAsChanged();
            }
        }
    }
}
