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
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using MatterHackers.VectorMath;
using MatterHackers.PolygonMesh.Csg;

namespace MatterHackers.PolygonMesh.UnitTests
{
    [TestFixture]
    public class MeshUnitTests
    {
        [Test]
        public void CreateWireFrameTriangle()
        {
            Mesh testMesh = new Mesh();
            Vertex leftVertexBottom = testMesh.CreateVertex(-1, 0, 0);
            Vertex rightVertexBottom = testMesh.CreateVertex(1, 0, 0);
            Vertex centerVertexTop = testMesh.CreateVertex(0, 0, 2);

            // create the first mesh edge and check stuff
            MeshEdge meshEdge0 = testMesh.CreateMeshEdge(leftVertexBottom, rightVertexBottom);
            Assert.IsTrue(meshEdge0.VertexOnEnd[0] == leftVertexBottom);
            Assert.IsTrue(meshEdge0.firstFaceEdge == null);
            Assert.IsTrue(meshEdge0.NextMeshEdgeFromEnd[0] == meshEdge0.NextMeshEdgeFromEnd[1]);
            Assert.IsTrue(meshEdge0.NextMeshEdgeFromEnd[0] == meshEdge0);
            Assert.IsTrue(meshEdge0.VertexOnEnd[1] == rightVertexBottom);
            Assert.IsTrue(meshEdge0.NextMeshEdgeFromEnd[1] == meshEdge0);

            // create the second mesh edge and check stuff
            MeshEdge meshEdge1 = testMesh.CreateMeshEdge(rightVertexBottom, centerVertexTop);
            Assert.IsTrue(meshEdge0.VertexOnEnd[0] == leftVertexBottom);
            Assert.IsTrue(meshEdge0.firstFaceEdge == null);
            Assert.IsTrue(meshEdge0.NextMeshEdgeFromEnd[0] == meshEdge0);
            Assert.IsTrue(meshEdge0.VertexOnEnd[1] == rightVertexBottom);
            Assert.IsTrue(meshEdge0.NextMeshEdgeFromEnd[1] == meshEdge1);

            Assert.IsTrue(meshEdge1.VertexOnEnd[0] == rightVertexBottom);
            Assert.IsTrue(meshEdge1.firstFaceEdge == null);
            Assert.IsTrue(meshEdge1.NextMeshEdgeFromEnd[0] == meshEdge0);
            Assert.IsTrue(meshEdge1.VertexOnEnd[1] == centerVertexTop);
            Assert.IsTrue(meshEdge1.NextMeshEdgeFromEnd[1] == meshEdge1);

            // create the third mesh edge and test
            MeshEdge meshEdge2 = testMesh.CreateMeshEdge(centerVertexTop, leftVertexBottom);
            Assert.IsTrue(meshEdge0.VertexOnEnd[0] == leftVertexBottom);
            Assert.IsTrue(meshEdge0.VertexOnEnd[1] == rightVertexBottom);
            Assert.IsTrue(meshEdge0.firstFaceEdge == null);
            Assert.IsTrue(meshEdge0.NextMeshEdgeFromEnd[0] == meshEdge2);
            Assert.IsTrue(meshEdge0.NextMeshEdgeFromEnd[1] == meshEdge1);

            Assert.IsTrue(meshEdge1.VertexOnEnd[0] == rightVertexBottom);
            Assert.IsTrue(meshEdge1.VertexOnEnd[1] == centerVertexTop);
            Assert.IsTrue(meshEdge1.firstFaceEdge == null);
            Assert.IsTrue(meshEdge1.NextMeshEdgeFromEnd[0] == meshEdge0);
            Assert.IsTrue(meshEdge1.NextMeshEdgeFromEnd[1] == meshEdge2);

            Assert.IsTrue(meshEdge2.VertexOnEnd[0] == centerVertexTop);
            Assert.IsTrue(meshEdge2.VertexOnEnd[1] == leftVertexBottom);
            Assert.IsTrue(meshEdge2.firstFaceEdge == null);
            Assert.IsTrue(meshEdge2.NextMeshEdgeFromEnd[0] == meshEdge1);
            Assert.IsTrue(meshEdge2.NextMeshEdgeFromEnd[1] == meshEdge0);
        }

        [Test]
        public void MeshFaceSplitAndUnspiltTests()
        {
            {
                //                              centerVertexTop (0, 0, 2)
                //
                //
                //
                //
                // leftVertexBottom (-1, 0, 0)  centerVertexBottom (0, 0, 0)  rightVertexBottom (1, 0, 0)

                Mesh testMesh = new Mesh();
                Vertex leftVertexBottom = testMesh.CreateVertex(-1, 0, 0);
                Vertex centerVertexBottom = testMesh.CreateVertex(0, 0, 0);
                Vertex rightVertexBottom = testMesh.CreateVertex(1, 0, 0);
                Vertex centerVertexTop = testMesh.CreateVertex(0, 0, 1);
                Face originalFace = testMesh.CreateFace(new Vertex[] { leftVertexBottom, centerVertexBottom, rightVertexBottom, centerVertexTop });
                DebugRenderToImage debugRender = new DebugRenderToImage(testMesh);
                debugRender.RenderToTga("debug face 1 pre-split.tga");
                //       * 
                //      / \ 
                //     /   \
                //    /     \
                //   /       \
                //  *----*----*

                Assert.IsTrue(originalFace.firstFaceEdge.meshEdge.Data.ID == 116);
                Assert.IsTrue(originalFace.NumVertices == 4, "The original face has 4 vertices.");
                MeshEdge edgeLeftCenter = testMesh.FindMeshEdge(leftVertexBottom, centerVertexBottom);
                MeshEdge edgeCenterRight = testMesh.FindMeshEdge(centerVertexBottom, rightVertexBottom);
                MeshEdge edgeTopLeft = testMesh.FindMeshEdge(centerVertexTop, leftVertexBottom);
                MeshEdge edgeRightTop = testMesh.FindMeshEdge(centerVertexTop, rightVertexBottom);

                Assert.IsTrue(edgeTopLeft.NextMeshEdgeFromEnd[0] == edgeRightTop);
                Assert.IsTrue(edgeTopLeft.NextMeshEdgeFromEnd[1] == edgeLeftCenter);

                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 2);

                string connectionInfoBeforeSplit = testMesh.GetConnectionInfoAsString();

                // split the face and test the result
                Face faceCreatedDurringSplit;
                MeshEdge edgeCreatedDurringSplit;
                testMesh.SplitFace(originalFace, centerVertexBottom, centerVertexTop, out edgeCreatedDurringSplit, out faceCreatedDurringSplit);
                debugRender.RenderToTga("debug face 2 post-split.tga");
                //       * 
                //      /|\ 
                //     / | \
                //    /  |  \
                //   /   |   \
                //  *----*----*

                testMesh.Validate();
                return;
                //Debug.Write(testMesh.GetConnectionInfoAsString());

                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[0] == centerVertexBottom);
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[1] == centerVertexTop);
                Assert.IsTrue(edgeLeftCenter.NextMeshEdgeFromEnd[1] == edgeCreatedDurringSplit);
                Assert.IsTrue(edgeTopLeft.NextMeshEdgeFromEnd[0] == edgeCreatedDurringSplit);
                Assert.IsTrue(edgeTopLeft.NextMeshEdgeFromEnd[1] == edgeLeftCenter);
                Assert.IsTrue(originalFace.firstFaceEdge.meshEdge == edgeLeftCenter);
                Assert.IsTrue(originalFace.firstFaceEdge.meshEdge.GetOppositeVertex(leftVertexBottom) == centerVertexBottom);
                Assert.IsTrue(originalFace.NumVertices == 3, "The original face now has 3 vertices.");
                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 3);
                Assert.IsTrue(edgeCreatedDurringSplit.GetNumFacesSharingEdge() == 2, "The edge we split on now has 2 faces attached to it.");
                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(centerVertexTop.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(leftVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(rightVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(faceCreatedDurringSplit.NumVertices == 3, "The created now has 3 vertices.");


                // Unsplit the faces keeping the original face, and test the result.
                testMesh.UnsplitFace(originalFace, faceCreatedDurringSplit, edgeCreatedDurringSplit);
                debugRender.RenderToTga("debug face 3 post-unsplit.tga");
                //       * 
                //      / \ 
                //     /   \
                //    /     \
                //   /       \
                //  *----*----*
                string connectionInfoAfterUnsplit = testMesh.GetConnectionInfoAsString();
                testMesh.Validate();
                //Debug.Write(testMesh.GetConnectionInfoAsString());
                Assert.IsTrue(originalFace.NumVertices == 4, "The original face is back to 4 vertices.");
                Assert.IsTrue(edgeCreatedDurringSplit.firstFaceEdge == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.NextMeshEdgeFromEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.NextMeshEdgeFromEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(faceCreatedDurringSplit.firstFaceEdge == null, "The data for the deleted face is all null to help debugging.");
                
                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
                Assert.IsTrue(centerVertexTop.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
                Assert.IsTrue(connectionInfoBeforeSplit == connectionInfoAfterUnsplit);
            }

            {
                Mesh testMesh = new Mesh();
                Vertex leftVertexBottom = testMesh.CreateVertex(-1, 0, 0);
                Vertex rightVertexBottom = testMesh.CreateVertex(1, 0, 0);
                Vertex leftVertexCenter = testMesh.CreateVertex(-1, 0, 1);
                Vertex rightVertexCenter = testMesh.CreateVertex(1, 0, 1);
                Vertex leftVertexTop = testMesh.CreateVertex(-1, 0, 2);
                Vertex rightVertexTop = testMesh.CreateVertex(1, 0, 2);
                Face originalFace = testMesh.CreateFace(new Vertex[] { rightVertexBottom, rightVertexCenter, rightVertexTop, leftVertexTop, leftVertexCenter, leftVertexBottom });
                // *-------*
                // |       |
                // *       *
                // |       |
                // *-------*

                MeshEdge bottomEdge = testMesh.FindMeshEdge(leftVertexBottom, rightVertexBottom);
                Assert.IsTrue(bottomEdge.GetNumFacesSharingEdge() == 1);
                Assert.IsTrue(originalFace.NumVertices == 6);

                // split the face and test the result
                Face faceCreatedDurringSplit;
                MeshEdge edgeCreatedDurringSplit;
                testMesh.SplitFace(originalFace, rightVertexCenter, leftVertexCenter, out edgeCreatedDurringSplit, out faceCreatedDurringSplit);
                // *-------*
                // |       |
                // *-------*
                // |       |
                // *-------*

                Assert.IsTrue(edgeCreatedDurringSplit.GetNumFacesSharingEdge() == 2, "The edge we split on now has 2 faces attached to it.");
                Assert.IsTrue(leftVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(rightVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(leftVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(leftVertexTop.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(rightVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(rightVertexTop.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(originalFace.NumVertices == 4, "The original face still has 4 vertices.");
                Assert.IsTrue(faceCreatedDurringSplit.NumVertices == 4, "The created face has 4 vertices.");

                // Unsplit the faces keeping the original face, and test the result.
                testMesh.UnsplitFace(originalFace, faceCreatedDurringSplit, edgeCreatedDurringSplit);
                // *-------*
                // |       |
                // *       *
                // |       |
                // *-------*
                Assert.IsTrue(originalFace.NumVertices == 4, "The original face still has 4 vertices.");
                Assert.IsTrue(edgeCreatedDurringSplit.firstFaceEdge == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.NextMeshEdgeFromEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDurringSplit.NextMeshEdgeFromEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(faceCreatedDurringSplit.firstFaceEdge == null, "The data for the deleted face is all null to help debugging.");
                Assert.IsTrue(leftVertexCenter.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
                Assert.IsTrue(rightVertexCenter.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");

                // split the face again and test the result
                testMesh.SplitFace(originalFace, rightVertexCenter, leftVertexCenter, out edgeCreatedDurringSplit, out faceCreatedDurringSplit);
                Assert.IsTrue(edgeCreatedDurringSplit.GetNumFacesSharingEdge() == 2, "The edge we split on now has 2 faces attached to it.");
                Assert.IsTrue(leftVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(rightVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(leftVertexCenter.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
                Assert.IsTrue(rightVertexCenter.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");

                // unsplite the faces keeping the face we created and test the result
                testMesh.UnsplitFace(faceCreatedDurringSplit, originalFace, edgeCreatedDurringSplit);
                Assert.IsTrue(faceCreatedDurringSplit.NumVertices == 4, "The created face has 4 vertices.");
            }
        }

        [Test]
        public void MeshEdgeSplitAndUnsplitTests()
        {
            // split edge and create vert (not part of a polygon, just a wire mesh)
            {
                Mesh testMesh = new Mesh();
                Vertex leftVertex = testMesh.CreateVertex(-1, 0, 0);
                Vertex rightVertex = testMesh.CreateVertex(1, 0, 0);
                MeshEdge edgeToSplit = testMesh.CreateMeshEdge(leftVertex, rightVertex);
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == leftVertex, "The edgeToSplit is connected the way we expect.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == rightVertex, "The edgeToSplit is connected the way we expect.");
                Assert.IsTrue(leftVertex.firstMeshEdge == edgeToSplit, "First edge of left vertex is the edge.");
                Assert.IsTrue(rightVertex.firstMeshEdge == edgeToSplit, "First edge of right vertex is the edge.");
                MeshEdge edgeCreatedDurringSplit;
                Vertex vertexCreatedDurringSplit;
                testMesh.SplitMeshEdge(edgeToSplit, out vertexCreatedDurringSplit, out edgeCreatedDurringSplit);

                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == vertexCreatedDurringSplit);
                Assert.IsTrue(edgeToSplit.NextMeshEdgeFromEnd[0] == edgeToSplit);
                Assert.IsTrue(edgeToSplit.NextMeshEdgeFromEnd[1] == edgeCreatedDurringSplit);
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[0] == vertexCreatedDurringSplit, "The edgeCreatedDurringSplit is connected the way we expect.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[1] == rightVertex, "The edgeCreatedDurringSplit is connected the way we expect.");

                Assert.IsTrue(vertexCreatedDurringSplit.firstMeshEdge == edgeCreatedDurringSplit, "First edge of new vertex is the edge we split.");
                Assert.IsTrue(edgeCreatedDurringSplit.NextMeshEdgeFromEnd[0] == edgeToSplit, "The next edge is the one we created.");
                Assert.IsTrue(edgeCreatedDurringSplit.NextMeshEdgeFromEnd[1] == edgeCreatedDurringSplit, "The other side is connected to itself.");

                testMesh.UnsplitMeshEdge(edgeToSplit, vertexCreatedDurringSplit);
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[0] == null && edgeCreatedDurringSplit.VertexOnEnd[1] == null, "The edgeCreatedDurringSplit is no longer connected to Vertices.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == leftVertex, "The unsplit edge is connected back the way it was.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == rightVertex, "The unsplit edge is connected back the way it was.");
            }

            // split a polygon's edge and create vert
            {
                Mesh testMesh = new Mesh();
                Vertex leftVertex = testMesh.CreateVertex(-1, -1, 0);
                Vertex rightVertex = testMesh.CreateVertex(1, -1, 0);
                Vertex topVertex = testMesh.CreateVertex(-1, 1, 0);
                Face newFace = testMesh.CreateFace(new Vertex[] { rightVertex, topVertex, leftVertex });

                Assert.IsTrue(newFace.normal == Vector3.UnitZ);

                Assert.IsTrue(newFace.NumVertices == 3, "We have a 3 vertex face.");
                Assert.IsTrue(newFace.FaceEdgeLoopIsGood());

                MeshEdge edgeToSplit = testMesh.FindMeshEdge(leftVertex, rightVertex);
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == leftVertex, "The edgeToSplit is connected the way we expect.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == rightVertex, "The edgeToSplit is connected the way we expect.");

                MeshEdge edgeCreatedDurringSplit;
                Vertex vertexCreatedDurringSplit;
                testMesh.SplitMeshEdge(edgeToSplit, out vertexCreatedDurringSplit, out edgeCreatedDurringSplit);

                Assert.IsTrue(newFace.NumVertices == 4, "After SplitEdge it is a 4 vertex face.");
                Assert.IsTrue(newFace.FaceEdgeLoopIsGood());

                testMesh.UnsplitMeshEdge(edgeToSplit, vertexCreatedDurringSplit);

                Assert.IsTrue(newFace.FaceEdgeLoopIsGood());

                Assert.IsTrue(newFace.NumVertices == 3, "Back to 3 after UnsplitEdge.");

                Assert.IsTrue(edgeCreatedDurringSplit.firstFaceEdge == null, "First face edge is disconnected.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[0] == null && edgeCreatedDurringSplit.VertexOnEnd[1] == null, "The edgeCreatedDurringSplit is no longer connected to Vertices.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == leftVertex, "The unsplit edge is connected back the way it was.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == rightVertex, "The unsplit edge is connected back the way it was.");

                // split again then unsplit the created edge rather than the original edge
               testMesh.SplitMeshEdge(edgeToSplit, out vertexCreatedDurringSplit, out edgeCreatedDurringSplit);

                testMesh.UnsplitMeshEdge(edgeCreatedDurringSplit, vertexCreatedDurringSplit);

                Assert.IsTrue(newFace.FaceEdgeLoopIsGood());

                Assert.IsTrue(newFace.NumVertices == 3, "Back to 3 after UnsplitEdge.");

                Assert.IsTrue(edgeToSplit.firstFaceEdge == null, "First face edge is disconnected.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == null && edgeToSplit.VertexOnEnd[1] == null, "The edgeToSplit is no longer connected to Vertices.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[0] == leftVertex, "The unsplit edge is connected back the way it was.");
                Assert.IsTrue(edgeCreatedDurringSplit.VertexOnEnd[1] == rightVertex, "The unsplit edge is connected back the way it was.");
            }

            // make sure that the data on FaceEdges is correct (split the center edge of an extruded plus).
            {
                // make an extruded pluss sign.
                Mesh testMesh = new Mesh();
                Vertex centerVertex = testMesh.CreateVertex(0, 0, 0);
                Vertex leftVertex = testMesh.CreateVertex(-1, 0, 0);
                Vertex rightVertex = testMesh.CreateVertex(1, 0, 0);
                Vertex frontVertex = testMesh.CreateVertex(0, -1, 0);
                Vertex backVertex = testMesh.CreateVertex(0, 1, 0);

                Vertex centerVertexTop = testMesh.CreateVertex(0, 0, 1);
                Vertex leftVertexTop = testMesh.CreateVertex(-1, 0, 1);
                Vertex rightVertexTop = testMesh.CreateVertex(1, 0, 1);
                Vertex frontVertexTop = testMesh.CreateVertex(0, -1, 1);
                Vertex backVertexTop = testMesh.CreateVertex(0, 1, 1);

                testMesh.CreateFace(new Vertex[] { centerVertex, centerVertexTop, frontVertexTop, frontVertex });
                MeshEdge centerEdge = testMesh.FindMeshEdge(centerVertex, centerVertexTop);
                Assert.IsTrue(centerEdge.GetNumFacesSharingEdge() == 1, "There should be 1 faces on this edge.");
                testMesh.CreateFace(new Vertex[] { centerVertex, centerVertexTop, rightVertexTop, rightVertex });
                Assert.IsTrue(centerEdge.GetNumFacesSharingEdge() == 2, "There should be 2 faces on this edge.");
                testMesh.CreateFace(new Vertex[] { centerVertex, centerVertexTop, backVertexTop, backVertex });
                Assert.IsTrue(centerEdge.GetNumFacesSharingEdge() == 3, "There should be 3 faces on this edge.");
                testMesh.CreateFace(new Vertex[] { centerVertex, centerVertexTop, leftVertexTop, leftVertex });
                Assert.IsTrue(centerEdge.GetNumFacesSharingEdge() == 4, "There should be 4 faces on this edge.");

                foreach (Face face in centerEdge.FacesSharingMeshEdgeIterator())
                {
                    Assert.IsTrue(face.NumVertices == 4, "The faces should all have 4 vertices.");
                }

                Vertex createdVertx;
                MeshEdge createdEdge;
                testMesh.SplitMeshEdge(centerEdge, out createdVertx, out createdEdge);
                Assert.IsTrue(centerEdge.GetNumFacesSharingEdge() == 4, "There should still be 4 faces on this edge.");
                Assert.IsTrue(createdEdge.GetNumFacesSharingEdge() == 4, "There should be 4 faces on this new edge.");
                foreach (Face face in centerEdge.FacesSharingMeshEdgeIterator())
                {
                    Assert.IsTrue(face.NumVertices == 5, "The faces should all now have 5 vertices.");
                }

                testMesh.UnsplitMeshEdge(centerEdge, createdVertx);
                Assert.IsTrue(centerEdge.GetNumFacesSharingEdge() == 4, "There should again be 4 faces on this edge.");
                foreach (Face face in centerEdge.FacesSharingMeshEdgeIterator())
                {
                    Assert.IsTrue(face.NumVertices == 4, "The faces should all finally have 4 vertices.");
                }
            }
        }

        internal void DetectAndRemoveTJunctions()
        {
            //throw new NotImplementedException();
        }
    }

    public static class UnitTests
    {
        static bool ranTests = false;

        public static bool RanTests { get { return ranTests; } }
        public static void Run()
        {
            if (!ranTests)
            {
                ranTests = true;
                VertexCollectonTests vertexCollectionTests = new VertexCollectonTests();
                vertexCollectionTests.PreventDuplicates();

                MeshUnitTests test = new MeshUnitTests();
                test.CreateWireFrameTriangle();
                test.MeshEdgeSplitAndUnsplitTests();
                test.MeshFaceSplitAndUnspiltTests();
                test.DetectAndRemoveTJunctions();

                CsgTests csgTests = new CsgTests();
                csgTests.EnsureSimpleCubeIntersection();
                csgTests.EnsureSimpleCubeUnion();
                csgTests.EnsureSimpleCubeSubtraction();
            }
        }
    }
}
