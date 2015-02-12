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
    public class CsgTests
    {
        public CsgTests()
        {
        }

        [Test]
        public void EnsureSimpleCubeIntersection()
        {
            // the intersection of 2 cubes
            {
                Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

                List<CsgPolygon> polygonsA = CsgOperations.PolygonsFromMesh(meshA);
                Assert.IsTrue(polygonsA.Count == 6);

                meshA.Translate(new Vector3(-2, -2, -2));
                Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
                meshB.Translate(new Vector3(2, 2, 2));

				Mesh meshIntersect = CsgOperations.Intersect(meshA, meshB);
                Assert.IsTrue(meshIntersect.Faces.Count == 6);
                Assert.IsTrue(meshIntersect.Vertices.Count == 8);
                foreach (Face face in meshIntersect.Faces)
                {
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.x) == 3);
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.y) == 3);
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.z) == 3);
                }
            }

            // the intersection of 2 cubes that miss eachother
            {
                Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

                List<CsgPolygon> polygonsA = CsgOperations.PolygonsFromMesh(meshA);
                Assert.IsTrue(polygonsA.Count == 6);

                meshA.Translate(new Vector3(-5, -5, -5));
                Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
                meshB.Translate(new Vector3(5, 5, 5));

                Mesh meshIntersect = CsgOperations.Intersect(meshA, meshB);
                Assert.IsTrue(meshIntersect.Faces.Count == 0);
            }
        }

        [Test]
        public void EnsureSimpleCubeUnion()
        {
            // the union of 2 cubes
            {
                Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

                List<CsgPolygon> polygonsA = CsgOperations.PolygonsFromMesh(meshA);
                Assert.IsTrue(polygonsA.Count == 6);

                meshA.Translate(new Vector3(-2, 0, 0));
                Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
                meshB.Translate(new Vector3(2, 0, 0));

                Mesh meshIntersect = CsgOperations.Union(meshA, meshB);
                Assert.IsTrue(meshIntersect.Faces.Count == 13);
                Assert.IsTrue(meshIntersect.Vertices.Count == 16);
                foreach (Face face in meshIntersect.Faces)
                {
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.x) == 7 || Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.x) == 3);
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.y) == 5);
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.z) == 5);
                }
            }
        }

        [Test]
        public void EnsureSimpleCubeSubtraction()
        {
            // the subtraction of 2 cubes
            {
                Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

                List<CsgPolygon> polygonsA = CsgOperations.PolygonsFromMesh(meshA);
                Assert.IsTrue(polygonsA.Count == 6);

                meshA.Translate(new Vector3(-2, 0, 0));
                Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
                meshB.Translate(new Vector3(2, 0, 0));

                Mesh meshIntersect = CsgOperations.Subtract(meshA, meshB);
                Assert.IsTrue(meshIntersect.Faces.Count == 6);
                Assert.IsTrue(meshIntersect.Vertices.Count == 8);
                foreach (Face face in meshIntersect.Faces)
                {
                    Assert.IsTrue(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.x == -7 || face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.x == -3);
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.y) == 5);
                    Assert.IsTrue(Math.Abs(face.firstFaceEdge.meshEdge.VertexOnEnd[0].Position.z) == 5);
                }
            }
        }
    }
}
