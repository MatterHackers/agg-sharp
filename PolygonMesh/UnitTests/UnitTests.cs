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
#define DEBUG_INTO_TGAS

using System;
using System.Diagnostics;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using NUnit.Framework;

using MatterHackers.Agg;
using MatterHackers.VectorMath;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;

namespace MatterHackers.PolygonMesh.UnitTests
{
    [TestFixture]
    public class MeshUnitTests
    {
        static int meshSaveIndex = 0;
        public void SaveDebugInfo(Mesh mesh, string testHint = "")
        {
#if DEBUG_INTO_TGAS
            DebugRenderToImage debugRender = new DebugRenderToImage(mesh);
            if (testHint != "")
            {
                debugRender.RenderToPng("debug face {0} - {1}.png".FormatWith(meshSaveIndex++, testHint));
            }
            else
            {
                debugRender.RenderToPng("debug face {0}.png".FormatWith(meshSaveIndex++));
            }
#endif
        }

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

            SaveDebugInfo(testMesh);
        }

        [Test]
        public void MergeVertices()
        {
            {
                Mesh testMesh = new Mesh();
                Vertex leftVertexBottom = testMesh.CreateVertex(-1, 0, 0);
                Vertex rightVertexBottom = testMesh.CreateVertex(1, 0, 0);
                Vertex centerVertexMiddle1 = testMesh.CreateVertex(0, 0, 1);

                MeshEdge meshEdgeBottomLeftRight = testMesh.CreateMeshEdge(leftVertexBottom, rightVertexBottom);
                MeshEdge meshEdgeBottomRightCenter = testMesh.CreateMeshEdge(rightVertexBottom, centerVertexMiddle1);
                MeshEdge meshEdgeBottomCenterLeft = testMesh.CreateMeshEdge(centerVertexMiddle1, leftVertexBottom);

                Vertex leftVertexTop = testMesh.CreateVertex(-1, 0, 2);
                Vertex centerVertexMiddle2 = testMesh.CreateVertex(0, 0, 1, CreateOption.CreateNew);
                Vertex rightVertexTop = testMesh.CreateVertex(1, 0, 2);

                MeshEdge meshEdgeTopLeftCenter = testMesh.CreateMeshEdge(leftVertexTop, centerVertexMiddle2);
                MeshEdge meshEdgeTopCenterRight = testMesh.CreateMeshEdge(centerVertexMiddle2, rightVertexTop);
                MeshEdge meshEdgeTopRightLeft = testMesh.CreateMeshEdge(rightVertexTop, leftVertexTop);

                Assert.IsTrue(meshEdgeBottomRightCenter.VertexOnEnd[1] == centerVertexMiddle1);
                Assert.IsTrue(meshEdgeTopLeftCenter.VertexOnEnd[1] == centerVertexMiddle2);

                SaveDebugInfo(testMesh);

                testMesh.MergeVertices(centerVertexMiddle1, centerVertexMiddle2);

                Assert.IsTrue(!testMesh.Vertices.ContainsVertex(centerVertexMiddle2));

                Assert.IsTrue(meshEdgeBottomRightCenter.VertexOnEnd[1] == centerVertexMiddle1);
                Assert.IsTrue(meshEdgeBottomCenterLeft.VertexOnEnd[0] == centerVertexMiddle1);
                Assert.IsTrue(meshEdgeTopLeftCenter.VertexOnEnd[1] == centerVertexMiddle1);
                Assert.IsTrue(meshEdgeTopCenterRight.VertexOnEnd[0] == centerVertexMiddle1);

                int connectedCount = 0;
                foreach (MeshEdge meshEdge in centerVertexMiddle1.ConnectedMeshEdges())
                {
                    connectedCount++;
                }
                Assert.IsTrue(connectedCount == 4);

                SaveDebugInfo(testMesh);
            }

            {
                Mesh testMesh = new Mesh();
                Vertex leftVertexBottom = testMesh.CreateVertex(-1, 0, 0);
                Vertex rightVertexBottom = testMesh.CreateVertex(1, 0, 0);
                Vertex centerVertexMiddle1 = testMesh.CreateVertex(0, 0, 1);

                MeshEdge meshEdgeBottomLeftRight = testMesh.CreateMeshEdge(leftVertexBottom, rightVertexBottom);
                MeshEdge meshEdgeBottomRightCenter = testMesh.CreateMeshEdge(rightVertexBottom, centerVertexMiddle1);
                MeshEdge meshEdgeBottomCenterLeft = testMesh.CreateMeshEdge(centerVertexMiddle1, leftVertexBottom);

                Vertex leftVertexTop = testMesh.CreateVertex(-1, 0, 2);
                Vertex centerVertexMiddle2 = testMesh.CreateVertex(0, 0, 1, CreateOption.CreateNew);
                Vertex rightVertexTop = testMesh.CreateVertex(1, 0, 2);

                MeshEdge meshEdgeTopLeftCenter = testMesh.CreateMeshEdge(leftVertexTop, centerVertexMiddle2);
                MeshEdge meshEdgeTopCenterRight = testMesh.CreateMeshEdge(centerVertexMiddle2, rightVertexTop);
                MeshEdge meshEdgeTopRightLeft = testMesh.CreateMeshEdge(rightVertexTop, leftVertexTop);

                Assert.IsTrue(meshEdgeBottomRightCenter.VertexOnEnd[1] == centerVertexMiddle1);
                Assert.IsTrue(meshEdgeTopLeftCenter.VertexOnEnd[1] == centerVertexMiddle2);

                SaveDebugInfo(testMesh);

                testMesh.MergeVertices(centerVertexMiddle2, centerVertexMiddle1);

                Assert.IsTrue(testMesh.Vertices.ContainsVertex(centerVertexMiddle2));

                Assert.IsTrue(meshEdgeBottomRightCenter.VertexOnEnd[1] == centerVertexMiddle2);
                Assert.IsTrue(meshEdgeBottomCenterLeft.VertexOnEnd[0] == centerVertexMiddle2);
                Assert.IsTrue(meshEdgeTopLeftCenter.VertexOnEnd[1] == centerVertexMiddle2);
                Assert.IsTrue(meshEdgeTopCenterRight.VertexOnEnd[0] == centerVertexMiddle2);

                int connectedCount = 0;
                foreach (MeshEdge meshEdge in centerVertexMiddle2.ConnectedMeshEdges())
                {
                    connectedCount++;
                }
                Assert.IsTrue(connectedCount == 4);

                SaveDebugInfo(testMesh);
            }

            {
                Mesh testMesh = new Mesh();
                Vertex leftVertexBottom = testMesh.CreateVertex(-1, 0, 0);
                Vertex rightVertexBottom = testMesh.CreateVertex(1, 0, 0);
                Vertex centerVertexMiddle1 = testMesh.CreateVertex(0, 0, 1);

                Face bottomFace = testMesh.CreateFace(new Vertex[] { leftVertexBottom, rightVertexBottom, centerVertexMiddle1 });

                Vertex leftVertexTop = testMesh.CreateVertex(-1, 0, 2);
                Vertex centerVertexMiddle2 = testMesh.CreateVertex(0, 0, 1, CreateOption.CreateNew);
                Vertex rightVertexTop = testMesh.CreateVertex(1, 0, 2);

                Face top = testMesh.CreateFace(new Vertex[] { leftVertexTop, centerVertexMiddle2, rightVertexTop });

                MeshEdge meshEdgeBottomRightCenter = testMesh.FindMeshEdges(leftVertexBottom, centerVertexMiddle1)[0];
                MeshEdge meshEdgeTopLeftCenter = testMesh.FindMeshEdges(leftVertexTop, centerVertexMiddle2)[0];
                Assert.IsTrue(meshEdgeBottomRightCenter.VertexOnEnd[0] == centerVertexMiddle1);
                Assert.IsTrue(meshEdgeTopLeftCenter.VertexOnEnd[1] == centerVertexMiddle2);

                SaveDebugInfo(testMesh);

                testMesh.MergeVertices();

                SaveDebugInfo(testMesh);
            }
        }

        [Test]
        public void MergeMeshEdges()
        {
            {
                Mesh testMesh = new Mesh();
                Vertex leftVertexBottom = testMesh.CreateVertex(-1, 0, 0);
                Vertex centerVertexBottom = testMesh.CreateVertex(0, 0, 0);
                Vertex centerVertexTop = testMesh.CreateVertex(0, 0, 1);
                Face leftFace = testMesh.CreateFace(new Vertex[] { leftVertexBottom, centerVertexBottom, centerVertexTop });
                SaveDebugInfo(testMesh);

                Vertex rightVertexBottom = testMesh.CreateVertex(1, 0, 0);
                Face rightFace = testMesh.CreateFace(new Vertex[] { centerVertexBottom, rightVertexBottom, centerVertexTop }, CreateOption.CreateNew);

                SaveDebugInfo(testMesh);

                foreach (MeshEdge meshEdge in testMesh.MeshEdges)
                {
                    Assert.IsTrue(meshEdge.firstFaceEdge != null);
                }

                Assert.IsTrue(testMesh.MeshEdges.Count == 6);

                Assert.IsTrue(testMesh.FindMeshEdges(centerVertexTop, centerVertexBottom).Count == 2);
                testMesh.MergeMeshEdges();
                SaveDebugInfo(testMesh);
                Assert.IsTrue(testMesh.MeshEdges.Count == 5);
            }
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
                
                SaveDebugInfo(testMesh);
                //       * 
                //      / \ 
                //     /   \
                //    /     \
                //   /       \
                //  *----*----*

                Assert.IsTrue(testMesh.FindMeshEdges(leftVertexBottom, centerVertexBottom).Count == 1);
                MeshEdge firstFaceEdgeMeshEdge = testMesh.FindMeshEdges(leftVertexBottom, centerVertexBottom)[0];
                Assert.IsTrue(originalFace.firstFaceEdge.meshEdge == firstFaceEdgeMeshEdge);
                Assert.IsTrue(originalFace.NumVertices == 4, "The original face has 4 vertices.");
                MeshEdge edgeLeftCenter = testMesh.FindMeshEdges(leftVertexBottom, centerVertexBottom)[0];
                MeshEdge edgeCenterRight = testMesh.FindMeshEdges(centerVertexBottom, rightVertexBottom)[0];
                MeshEdge edgeTopLeft = testMesh.FindMeshEdges(centerVertexTop, leftVertexBottom)[0];
                MeshEdge edgeRightTop = testMesh.FindMeshEdges(centerVertexTop, rightVertexBottom)[0];

                Assert.IsTrue(edgeTopLeft.NextMeshEdgeFromEnd[0] == edgeRightTop);
                Assert.IsTrue(edgeTopLeft.NextMeshEdgeFromEnd[1] == edgeLeftCenter);

                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 2);

                string connectionInfoBeforeSplit = testMesh.GetConnectionInfoAsString();

                // split the face and test the result
                Face faceCreatedDuringSplit;
                MeshEdge meshEdgeCreatedDuringSplit;
                testMesh.SplitFace(originalFace, centerVertexBottom, centerVertexTop, out meshEdgeCreatedDuringSplit, out faceCreatedDuringSplit);
                SaveDebugInfo(testMesh);
                //       * 
                //      /|\ 
                //     / | \
                //    /  |  \
                //   /   |   \
                //  *----*----*

                testMesh.Validate();
                //Debug.Write(testMesh.GetConnectionInfoAsString());

                Assert.IsTrue(meshEdgeCreatedDuringSplit.VertexOnEnd[0] == centerVertexBottom);
                Assert.IsTrue(meshEdgeCreatedDuringSplit.VertexOnEnd[1] == centerVertexTop);
                Assert.IsTrue(edgeLeftCenter.NextMeshEdgeFromEnd[1] == meshEdgeCreatedDuringSplit);
                Assert.IsTrue(edgeTopLeft.NextMeshEdgeFromEnd[1] == edgeLeftCenter);
                Assert.IsTrue(originalFace.firstFaceEdge.meshEdge == meshEdgeCreatedDuringSplit);
                Assert.IsTrue(originalFace.NumVertices == 3, "The original face now has 3 vertices.");
                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 3);
                Assert.IsTrue(meshEdgeCreatedDuringSplit.GetNumFacesSharingEdge() == 2, "The edge we split on now has 2 faces attached to it.");
                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(centerVertexTop.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(leftVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(rightVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(faceCreatedDuringSplit.NumVertices == 3, "The created now has 3 vertices.");


                // Unsplit the faces keeping the original face, and test the result.
                testMesh.UnsplitFace(originalFace, faceCreatedDuringSplit, meshEdgeCreatedDuringSplit);
                SaveDebugInfo(testMesh);
                //       * 
                //      / \ 
                //     /   \
                //    /     \
                //   /       \
                //  *----*----*
                string connectionInfoAfterUnsplit = testMesh.GetConnectionInfoAsString();
                testMesh.Validate();
                foreach (FaceEdge faceEdge in originalFace.FaceEdges())
                {
                    // make sure none of them are connected to the deleted MeshEdge
                    Assert.IsTrue(faceEdge.meshEdge != meshEdgeCreatedDuringSplit);
                    Assert.IsTrue(faceEdge.meshEdge.NextMeshEdgeFromEnd[0] != meshEdgeCreatedDuringSplit);
                    Assert.IsTrue(faceEdge.meshEdge.NextMeshEdgeFromEnd[1] != meshEdgeCreatedDuringSplit);
                }
                //Debug.Write(testMesh.GetConnectionInfoAsString());
                Assert.IsTrue(originalFace.NumVertices == 4, "The original face is back to 4 vertices.");
                Assert.IsTrue(meshEdgeCreatedDuringSplit.firstFaceEdge == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(meshEdgeCreatedDuringSplit.VertexOnEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(meshEdgeCreatedDuringSplit.NextMeshEdgeFromEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(meshEdgeCreatedDuringSplit.VertexOnEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(meshEdgeCreatedDuringSplit.NextMeshEdgeFromEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(faceCreatedDuringSplit.firstFaceEdge == null, "The data for the deleted face is all null to help debugging.");
                
                Assert.IsTrue(centerVertexBottom.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
                Assert.IsTrue(centerVertexTop.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
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
                SaveDebugInfo(testMesh);
                // *-------*
                // |       |
                // *       *
                // |       |
                // *-------*

                MeshEdge bottomEdge = testMesh.FindMeshEdges(leftVertexBottom, rightVertexBottom)[0];
                Assert.IsTrue(bottomEdge.GetNumFacesSharingEdge() == 1);
                Assert.IsTrue(originalFace.NumVertices == 6);

                // split the face and test the result
                Face faceCreatedDuringSplit;
                MeshEdge edgeCreatedDuringSplit;
                testMesh.SplitFace(originalFace, rightVertexCenter, leftVertexCenter, out edgeCreatedDuringSplit, out faceCreatedDuringSplit);
                SaveDebugInfo(testMesh);
                // *-------*
                // |       |
                // *-------*
                // |       |
                // *-------*

                Assert.IsTrue(edgeCreatedDuringSplit.GetNumFacesSharingEdge() == 2, "The edge we split on now has 2 faces attached to it.");
                Assert.IsTrue(leftVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(rightVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(leftVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(leftVertexTop.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(rightVertexBottom.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(rightVertexTop.GetConnectedMeshEdgesCount() == 2, "The original vertices should still have 2 mesh edges attached to them.");
                Assert.IsTrue(originalFace.NumVertices == 4, "The original face still has 4 vertices.");
                Assert.IsTrue(faceCreatedDuringSplit.NumVertices == 4, "The created face has 4 vertices.");

                // Unsplit the faces keeping the original face, and test the result.
                testMesh.UnsplitFace(originalFace, faceCreatedDuringSplit, edgeCreatedDuringSplit);
                SaveDebugInfo(testMesh);
                // *-------*
                // |       |
                // *       *
                // |       |
                // *-------*
                Assert.IsTrue(originalFace.NumVertices == 6, "The original face still has 4 vertices.");
                Assert.IsTrue(edgeCreatedDuringSplit.firstFaceEdge == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDuringSplit.NextMeshEdgeFromEnd[0] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(edgeCreatedDuringSplit.NextMeshEdgeFromEnd[1] == null, "The data for the deleted edge is all null to help debugging.");
                Assert.IsTrue(faceCreatedDuringSplit.firstFaceEdge == null, "The data for the deleted face is all null to help debugging.");
                Assert.IsTrue(leftVertexCenter.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
                Assert.IsTrue(rightVertexCenter.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");

                // split the face again and test the result
                testMesh.SplitFace(originalFace, rightVertexCenter, leftVertexCenter, out edgeCreatedDuringSplit, out faceCreatedDuringSplit);
                Assert.IsTrue(edgeCreatedDuringSplit.GetNumFacesSharingEdge() == 2, "The edge we split on now has 2 faces attached to it.");
                Assert.IsTrue(leftVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(rightVertexCenter.GetConnectedMeshEdgesCount() == 3, "The vertex we split on should now have 3 mesh edges attached to it.");
                Assert.IsTrue(leftVertexBottom.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");
                Assert.IsTrue(rightVertexBottom.GetConnectedMeshEdgesCount() == 2, "The vertex we split on should now have 2 mesh edges attached to it.");

                // unsplite the faces keeping the face we created and test the result
                testMesh.UnsplitFace(faceCreatedDuringSplit, originalFace, edgeCreatedDuringSplit);
                Assert.IsTrue(faceCreatedDuringSplit.NumVertices == 6, "The created face has 4 vertices.");
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
                MeshEdge edgeCreatedDuringSplit;
                Vertex vertexCreatedDuringSplit;
                testMesh.SplitMeshEdge(edgeToSplit, out vertexCreatedDuringSplit, out edgeCreatedDuringSplit);

                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == vertexCreatedDuringSplit);
                Assert.IsTrue(edgeToSplit.NextMeshEdgeFromEnd[0] == edgeToSplit);
                Assert.IsTrue(edgeToSplit.NextMeshEdgeFromEnd[1] == edgeCreatedDuringSplit);
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[0] == vertexCreatedDuringSplit, "The edgeCreatedDuringSplit is connected the way we expect.");
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[1] == rightVertex, "The edgeCreatedDuringSplit is connected the way we expect.");

                Assert.IsTrue(vertexCreatedDuringSplit.firstMeshEdge == edgeCreatedDuringSplit, "First edge of new vertex is the edge we split.");
                Assert.IsTrue(edgeCreatedDuringSplit.NextMeshEdgeFromEnd[0] == edgeToSplit, "The next edge is the one we created.");
                Assert.IsTrue(edgeCreatedDuringSplit.NextMeshEdgeFromEnd[1] == edgeCreatedDuringSplit, "The other side is connected to itself.");

                testMesh.UnsplitMeshEdge(edgeToSplit, vertexCreatedDuringSplit);
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[0] == null && edgeCreatedDuringSplit.VertexOnEnd[1] == null, "The edgeCreatedDuringSplit is no longer connected to Vertices.");
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

                MeshEdge edgeToSplit = testMesh.FindMeshEdges(leftVertex, rightVertex)[0];
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == leftVertex, "The edgeToSplit is connected the way we expect.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == rightVertex, "The edgeToSplit is connected the way we expect.");

                MeshEdge edgeCreatedDuringSplit;
                Vertex vertexCreatedDuringSplit;
                testMesh.SplitMeshEdge(edgeToSplit, out vertexCreatedDuringSplit, out edgeCreatedDuringSplit);

                Assert.IsTrue(newFace.NumVertices == 4, "After SplitEdge it is a 4 vertex face.");
                Assert.IsTrue(newFace.FaceEdgeLoopIsGood());

                testMesh.UnsplitMeshEdge(edgeToSplit, vertexCreatedDuringSplit);

                Assert.IsTrue(newFace.FaceEdgeLoopIsGood());

                Assert.IsTrue(newFace.NumVertices == 3, "Back to 3 after UnsplitEdge.");

                Assert.IsTrue(edgeCreatedDuringSplit.firstFaceEdge == null, "First face edge is disconnected.");
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[0] == null && edgeCreatedDuringSplit.VertexOnEnd[1] == null, "The edgeCreatedDuringSplit is no longer connected to Vertices.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == leftVertex, "The unsplit edge is connected back the way it was.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[1] == rightVertex, "The unsplit edge is connected back the way it was.");

                // split again then unsplit the created edge rather than the original edge
               testMesh.SplitMeshEdge(edgeToSplit, out vertexCreatedDuringSplit, out edgeCreatedDuringSplit);

                testMesh.UnsplitMeshEdge(edgeCreatedDuringSplit, vertexCreatedDuringSplit);

                Assert.IsTrue(newFace.FaceEdgeLoopIsGood());

                Assert.IsTrue(newFace.NumVertices == 3, "Back to 3 after UnsplitEdge.");

                Assert.IsTrue(edgeToSplit.firstFaceEdge == null, "First face edge is disconnected.");
                Assert.IsTrue(edgeToSplit.VertexOnEnd[0] == null && edgeToSplit.VertexOnEnd[1] == null, "The edgeToSplit is no longer connected to Vertices.");
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[0] == leftVertex, "The unsplit edge is connected back the way it was.");
                Assert.IsTrue(edgeCreatedDuringSplit.VertexOnEnd[1] == rightVertex, "The unsplit edge is connected back the way it was.");
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
                MeshEdge centerEdge = testMesh.FindMeshEdges(centerVertex, centerVertexTop)[0];
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

        public void DetectAndRemoveTJunctions()
        {
            //throw new NotImplementedException();
        }

        [Test]
        internal void MeshCopy()
        {
            {
                Mesh testMesh = new Mesh();
                Vertex left = testMesh.CreateVertex(-1, -1, 0);
                Vertex middle = testMesh.CreateVertex(0, 1, 0);
                Vertex right = testMesh.CreateVertex(1, -1, 0);

                Vertex top = testMesh.CreateVertex(0, 0, 1);

                testMesh.CreateFace(new Vertex[] { left, top, middle });
                testMesh.CreateFace(new Vertex[] { left, right, top });
                testMesh.CreateFace(new Vertex[] { right, middle, top });
                testMesh.CreateFace(new Vertex[] { left, middle, right });

                testMesh.MergeVertices();

                MeshFileIo.Save(testMesh, "control.stl", new MeshOutputSettings(MeshOutputSettings.OutputType.Ascii));
                SaveDebugInfo(testMesh, "MeshCopy (orig)");

                Mesh copyMesh = Mesh.Copy(testMesh);

                MeshFileIo.Save(testMesh, "test.stl", new MeshOutputSettings(MeshOutputSettings.OutputType.Ascii));
                SaveDebugInfo(copyMesh, "MeshCopy (copy)");

                Mesh copyMesh2 = Mesh.Copy(copyMesh);

                MeshFileIo.Save(copyMesh2, "test2.stl", new MeshOutputSettings(MeshOutputSettings.OutputType.Ascii));
                SaveDebugInfo(copyMesh, "MeshCopy (copy2)");
            }
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
                vertexCollectionTests.SortAndQueryWork();

                MeshUnitTests test = new MeshUnitTests();
                test.CreateWireFrameTriangle();
                test.MergeVertices();
                test.MeshEdgeSplitAndUnsplitTests();
                test.MeshFaceSplitAndUnspiltTests();
                test.MergeMeshEdges();
                test.DetectAndRemoveTJunctions();
                test.MeshCopy();

                CsgTests csgTests = new CsgTests();
                csgTests.EnsureSimpleCubeIntersection();
                csgTests.EnsureSimpleCubeUnion();
                csgTests.EnsureSimpleCubeSubtraction();
            }
        }
    }
}
