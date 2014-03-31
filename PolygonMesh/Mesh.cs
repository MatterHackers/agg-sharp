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
//#define RUN_TIMING_TESTS

using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh
{
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

        MetaData data = new MetaData();
        public MetaData Data { get { return data; } }

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
                foreach (FaceEdge faceEdgeToAdd in face.FaceEdgeIterator())
                {
                    Vertex newVertex = CreateVertex(faceEdgeToAdd.vertex.Position, true);
                    faceVertices.Add(newVertex);
                }

                CreateFace(faceVertices.ToArray(), true);
            }
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
            foreach (MeshEdge meshEdeg in meshEdges)
            {
                totalDebug.Append(new string('\t', 1) + String.Format("MeshEdeg: {0}\n", meshEdeg.Data.ID));
                meshEdeg.AddDebugInfo(totalDebug, 2);
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
            foreach (MeshEdge meshEdeg in meshEdges)
            {
                meshEdeg.Validate();
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
            return vertices.ContainsVertex(vertexToLookFor);
        }

        public void SplitFace(Face faceToSplit, Vertex vertex1, Vertex vertex2, out MeshEdge meshEdgeCreatedDurringSplit, out Face faceCreatedDurringSplit)
        {
            if (!ContainsVertex(vertex1) || !ContainsVertex(vertex2))
            {
                throw new Exception("The mesh must contain the vertices you intend to split between.");
            }

            // we may want to be able to double up an edge for some operations (we'll have to see).
            if (FindMeshEdge(vertex1, vertex2) != null)
            {
                // this also ensures that the face is more than 2 sided.
                throw new Exception("You cannot split a face on an existing edge.");
            }

            // we need to make sure that the vertecies are in the order of the winding.
            FaceEdge faceEdge1 = null;
            FaceEdge faceEdge2 = null;
            foreach(FaceEdge faceEdge in faceToSplit.FaceEdgeIterator())
            {
                if(faceEdge.vertex == vertex1)
                {
                    faceEdge1 = faceEdge;
                    if(faceEdge2 != null) break; // stop if we are done
                }
                else if(faceEdge.vertex == vertex2)
                {
                    faceEdge2 = faceEdge;
                    if(faceEdge1 != null) break; // stop if we are done
                }
            }

            meshEdgeCreatedDurringSplit = CreateMeshEdge(vertex1, vertex2);
            faceCreatedDurringSplit = new Face(faceToSplit);

            faces.Add(faceCreatedDurringSplit);

            FaceEdge newFaceEdge1 = new FaceEdge(faceToSplit, meshEdgeCreatedDurringSplit, vertex1);
            FaceEdge newFaceEdge2 = new FaceEdge(faceCreatedDurringSplit, meshEdgeCreatedDurringSplit, vertex2);

            // get the new edges injected into the existing loop, spliting it in two.
            newFaceEdge1.prevFaceEdge = faceEdge2.prevFaceEdge;
            newFaceEdge2.prevFaceEdge = faceEdge1.prevFaceEdge;

            faceEdge1.prevFaceEdge.nextFaceEdge = newFaceEdge2;
            faceEdge2.prevFaceEdge.nextFaceEdge = newFaceEdge1;

            newFaceEdge1.nextFaceEdge = faceEdge1;
            newFaceEdge2.nextFaceEdge = faceEdge2;

            faceEdge1.prevFaceEdge = newFaceEdge1;
            faceEdge2.prevFaceEdge = newFaceEdge2;
            
            // find out which loop the original face holds
            bool faceEdge1InFaceToSplit = false;
            foreach(FaceEdge faceEdge in faceEdge1.NextFaceEdgeIterator())
            {
                if(faceEdge == faceToSplit.firstFaceEdge)
                {
                    faceEdge1InFaceToSplit = true;
                }
            }

            if (faceEdge1InFaceToSplit)
            {
                if (faceToSplit.firstFaceEdge.prevFaceEdge == faceEdge1)
                {
                    faceCreatedDurringSplit.firstFaceEdge = faceEdge2.prevFaceEdge;
                }
                else if (faceToSplit.firstFaceEdge.nextFaceEdge == faceEdge1)
                {
                    faceCreatedDurringSplit.firstFaceEdge = faceEdge2.nextFaceEdge;
                }
                else
                {
                    faceCreatedDurringSplit.firstFaceEdge = faceEdge2;
                }
            }
            else
            {
                if (faceToSplit.firstFaceEdge.prevFaceEdge == faceEdge2)
                {
                    faceCreatedDurringSplit.firstFaceEdge = faceEdge1.prevFaceEdge;
                }
                else if (faceToSplit.firstFaceEdge.nextFaceEdge == faceEdge2)
                {
                    faceCreatedDurringSplit.firstFaceEdge = faceEdge1.nextFaceEdge;
                }
                else
                {
                    faceCreatedDurringSplit.firstFaceEdge = faceEdge1;
                }
            }

            // make sure the FaceEdges of the new face all point to the new face.
            foreach (FaceEdge faceEdge in faceCreatedDurringSplit.firstFaceEdge.NextFaceEdgeIterator())
            {
                faceEdge.face = faceCreatedDurringSplit;
            }

            newFaceEdge1.AddToRadialLoop(meshEdgeCreatedDurringSplit);
            newFaceEdge2.AddToRadialLoop(meshEdgeCreatedDurringSplit);
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

            if (faceEdgeToDeleteOnFaceToKeep.vertex == faceEdgeToDeleteOnFaceToDelete.vertex)
            {
                throw new Exception("The faces have oposite windings and you cannot merge the edge");
            }

            faceEdgeToDeleteOnFaceToKeep.prevFaceEdge.nextFaceEdge = faceEdgeToDeleteOnFaceToDelete.nextFaceEdge;
            faceEdgeToDeleteOnFaceToDelete.nextFaceEdge.prevFaceEdge = faceEdgeToDeleteOnFaceToKeep.prevFaceEdge;

            faceEdgeToDeleteOnFaceToKeep.nextFaceEdge.prevFaceEdge = faceEdgeToDeleteOnFaceToDelete.prevFaceEdge;
            faceEdgeToDeleteOnFaceToDelete.prevFaceEdge.nextFaceEdge = faceEdgeToDeleteOnFaceToKeep.nextFaceEdge;

            if (faceToKeep.firstFaceEdge == faceEdgeToDeleteOnFaceToKeep)
            {
                faceToKeep.firstFaceEdge = faceToKeep.firstFaceEdge.nextFaceEdge;
            }

            // make sure the FaceEdges all point to the kept face.
            foreach (FaceEdge faceEdge in faceToKeep.firstFaceEdge.NextFaceEdgeIterator())
            {
                faceEdge.face = faceToKeep;
            }

            faceEdgeToDeleteOnFaceToKeep.meshEdge.vertex1.RemoveMeshEdgeFromMeshEdgeLinks(faceEdgeToDeleteOnFaceToKeep.meshEdge);
            faceEdgeToDeleteOnFaceToKeep.meshEdge.vertex2.RemoveMeshEdgeFromMeshEdgeLinks(faceEdgeToDeleteOnFaceToKeep.meshEdge);

            faceToDelete.firstFaceEdge = null;
            faces.Remove(faceToDelete);

            meshEdgeToDelete.firstFaceEdge = null;
            meshEdgeToDelete.vertex1 = null;
            meshEdgeToDelete.vertex1MeshEdgeLinks = null;
            meshEdgeToDelete.vertex2 = null;
            meshEdgeToDelete.vertex2MeshEdgeLinks = null;

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
        public Vertex CreateVertex(double x, double y, double z, bool allowDuplicate = false)
        {
            return CreateVertex(new Vector3(x, y, z), allowDuplicate);
        }

        public List<Vertex> FindVertices(Vector3 position)
        {
            return vertices.FindVertices(position, MaxDistanceToConsiderVertexAsSame);
        }

        public Vertex CreateVertex(Vector3 position, bool allowDuplicate = false)
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
            vertices.Add(createdVertex);
            return createdVertex;
        }

        public void DeleteVertex(Vertex vertex)
        {
            throw new NotImplementedException();
        }
        #endregion

        #region MeshEdge
        public MeshEdge FindMeshEdge(Vertex vertex1, Vertex vertex2)
        {
#if false
            if (!vertices.Contains(vertex1) || !vertices.Contains(vertex2))
            {
                return null;
            }
#endif

            if (vertex1 == vertex2)
            {
                return null;
            }

            return vertex1.GetMeshEdgeConnectedToVertex(vertex2);
        }

        public MeshEdge CreateMeshEdge(Vertex vertex1, Vertex vertex2, bool allowDuplicate = false)
        {
            if (false)//!vertices.Contains(vertex1) || !vertices.Contains(vertex2))
            {
                throw new ArgumentException("the two vertices must be in the vertices list before a mesh edge can be made between them.");
            }

            if (vertex1 == vertex2)
            {
                throw new ArgumentException("Your input vertices must not be the same vertex.");
            }

            if (!allowDuplicate)
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
                vertexCreatedDurringSplit = CreateVertex((meshEdgeToSplit.vertex1.Position + meshEdgeToSplit.vertex2.Position) / 2);
                meshEdgeCreatedDurringSplit = new MeshEdge();
            }

            // Set the new firstMeshEdge on the new Vertex
            vertexCreatedDurringSplit.firstMeshEdge = meshEdgeCreatedDurringSplit;

            // fix the Vertex references on the MeshEdges
            {
                // and set the edges to point to this new one
                meshEdgeCreatedDurringSplit.vertex2 = meshEdgeToSplit.vertex2;
                meshEdgeCreatedDurringSplit.vertex1 = vertexCreatedDurringSplit;
                meshEdgeToSplit.vertex2 = vertexCreatedDurringSplit;
            }

            // fix the MeshEdgeLinks on the MeshEdges
            {
                // set the created edge to be connected to the old edges other mesh edges
                meshEdgeCreatedDurringSplit.vertex2MeshEdgeLinks = meshEdgeToSplit.vertex2MeshEdgeLinks;
                // make their links point to eachother
                meshEdgeToSplit.vertex2MeshEdgeLinks = new MeshEdgeLinks(meshEdgeCreatedDurringSplit, meshEdgeCreatedDurringSplit);
                meshEdgeCreatedDurringSplit.vertex1MeshEdgeLinks = new MeshEdgeLinks(meshEdgeToSplit, meshEdgeToSplit);
            }

            // if the MeshEdge is part of a face than we have to fix the face up
            FaceEdge faceEdgeToSplit = meshEdgeToSplit.firstFaceEdge;
            if (faceEdgeToSplit != null)
            {
                foreach (FaceEdge faceEdge in meshEdgeToSplit.FaceEdgesSharingMeshEdgeIterator())
                {
                    Face currentFace = faceEdge.face;
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
            int edgeToJoinEndThatHasVertex;
            if (edgeToJoin.vertex1 == vertexToDelete)
            {
                edgeToJoinEndThatHasVertex = 1;
            }
            else if (edgeToJoin.vertex2 == vertexToDelete)
            {
                edgeToJoinEndThatHasVertex = 2;
            }
            else
            {
                throw new Exception("The edge that is being unsplit must be connected the vertexToDelete.");
            }
            
            MeshEdgeLinks linksForVertex = edgeToJoin.GetMeshEdgeLinksContainingVertex(vertexToDelete);
            if (linksForVertex.nextMeshEdge != linksForVertex.prevMeshEdge)
            {
                // make sure the edgeToJoin is a valid unsplit (only one connection)
                throw new Exception("The edge that is being unsplit must be connected to only one other MeshEdge across the vertexToDelete.");
            }
            MeshEdge edgeToDelete = linksForVertex.nextMeshEdge;
            MeshEdgeLinks linksOppositeVertexToDelete = edgeToDelete.GetMeshEdgeLinksOppositeVertex(vertexToDelete);

            // if the MeshEdge is part of any faces than we have to fix the faces.
            if (edgeToJoin.firstFaceEdge != null)
            {
                // The edge we split was part of one or more faces, we need to fix the FaceEdge loops
                foreach (FaceEdge faceEdge in edgeToJoin.FaceEdgesSharingMeshEdgeIterator())
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
                    if (faceEdge.face.firstFaceEdge == faceEdgeToDelete)
                    {
                        faceEdge.face.firstFaceEdge = faceEdge;
                    }

                    // and clear out the FaceEdge we are deleting to help debuging and other references to it.
                    faceEdgeToDelete.nextFaceEdge = null;
                    faceEdgeToDelete.prevFaceEdge = null;
                    faceEdgeToDelete.radialNextFaceEdge = null;
                    faceEdgeToDelete.radialPrevFaceEdge = null;
                    faceEdgeToDelete.meshEdge = null;
                    faceEdgeToDelete.face = null;
                    faceEdgeToDelete.vertex = null;
                }
            }

            // fix the MeshEdgeLinks on the edgeToJoin
            {
                if(edgeToJoinEndThatHasVertex == 1)
                {
                    edgeToJoin.vertex1 = edgeToDelete.GetOppositeVertex(vertexToDelete);
                    edgeToJoin.vertex1MeshEdgeLinks = linksOppositeVertexToDelete;
                }
                else
                {
                    edgeToJoin.vertex2 = edgeToDelete.GetOppositeVertex(vertexToDelete);
                    edgeToJoin.vertex2MeshEdgeLinks = linksOppositeVertexToDelete;
                }
            }

            // Clear all  the data on the deleted vertex and edge so we have less code that will work if it continues to use them.
            vertexToDelete.firstMeshEdge = null;
            edgeToDelete.vertex1 = null;
            edgeToDelete.vertex1MeshEdgeLinks = null;
            edgeToDelete.vertex2 = null;
            edgeToDelete.vertex2MeshEdgeLinks = null;
            edgeToDelete.firstFaceEdge = null;
        }
        #endregion // MeshEdge

        #region Face
        /// <summary>
        /// This version of CreateFace allows you to specify what MeshEdegs to use.  You would need
        /// this if you had allowed duplicate mesh edges and wanted to state which one to use.
        /// </summary>
        /// <param name="verticesToUse"></param>
        /// <param name="edges"></param>
        /// <param name="allowDuplicate"></param>
        /// <returns></returns>
        public Face CreateFace(Vertex[] verticesToUse, MeshEdge[] edges, bool allowDuplicate = false)
        {
            throw new NotImplementedException();
        }

#if RUN_TIMING_TESTS
        NamedExecutionTimer CreateFaceTimer = new NamedExecutionTimer("Mesh Create Face");
#endif
        public Face CreateFace(Vertex[] verticesToUse, bool allowDuplicate = false)
        {
#if RUN_TIMING_TESTS
            CreateFaceTimer.Start();
#endif
            if (verticesToUse.Length < 3)
            {
                throw new ArgumentException("A face cannot have less than 3 vertices.");
            }

            if (!allowDuplicate)
            {
                Face existingFace = FindFace(verticesToUse);
                if (existingFace != null)
                {
#if RUN_TIMING_TESTS
                    CreateFaceTimer.Stop();
#endif
                    return existingFace;
                }
            }

            // make sure all the mesh edges exist (by trying to create them).
            for (int i = 0; i < verticesToUse.Length - 1; i++)
            {
                CreateMeshEdge(verticesToUse[i], verticesToUse[i + 1]);
            }
            CreateMeshEdge(verticesToUse[verticesToUse.Length - 1], verticesToUse[0]);

            // make the face and set it's data
            Face createdFace = new Face();
            FaceEdge prevFaceEdge = null;
            for (int i = 0; i < verticesToUse.Length - 1; i++)
            {
                MeshEdge currentMeshEdge = FindMeshEdge(verticesToUse[i], verticesToUse[i + 1]);
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
                MeshEdge currentMeshEdge = FindMeshEdge(verticesToUse[verticesToUse.Length-1], verticesToUse[0]);
                FaceEdge currentFaceEdge = new FaceEdge(createdFace, currentMeshEdge, verticesToUse[verticesToUse.Length-1]);
                prevFaceEdge.nextFaceEdge = currentFaceEdge;
                currentFaceEdge.prevFaceEdge = prevFaceEdge;
                currentFaceEdge.nextFaceEdge = createdFace.firstFaceEdge;
                createdFace.firstFaceEdge.prevFaceEdge = currentFaceEdge;
                currentFaceEdge.AddToRadialLoop(currentMeshEdge);
            }

            createdFace.CalculateNormal();

            faces.Add(createdFace);

#if RUN_TIMING_TESTS
            CreateFaceTimer.Stop();
#endif
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
