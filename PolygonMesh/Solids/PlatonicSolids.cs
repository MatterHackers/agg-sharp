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

using MatterHackers.VectorMath;
using System;

namespace MatterHackers.PolygonMesh
{
	public static class PlatonicSolids
	{
		public static Mesh CreateTetrahedron()
		{
			Mesh tetrahedron = new Mesh();
			Vector2 basePoint = new Vector2(1, 0);
			double baseOffsetZ = -Math.Sin(MathHelper.DegreesToRadians(30));
			Vertex[] verts = new Vertex[4];
			verts[0] = tetrahedron.CreateVertex(new Vector3(basePoint, baseOffsetZ));
			verts[1] = tetrahedron.CreateVertex(new Vector3(Vector2.Rotate(basePoint, MathHelper.Tau / 3), baseOffsetZ));
			verts[2] = tetrahedron.CreateVertex(new Vector3(Vector2.Rotate(basePoint, 2 * MathHelper.Tau / 3), baseOffsetZ));
			verts[3] = tetrahedron.CreateVertex(new Vector3(0, 0, 1));

			tetrahedron.CreateFace(new Vertex[] { verts[0], verts[2], verts[1] }); // add reversed because we want to see the bottom.
			tetrahedron.CreateFace(new Vertex[] { verts[0], verts[1], verts[3] });
			tetrahedron.CreateFace(new Vertex[] { verts[1], verts[2], verts[3] });
			tetrahedron.CreateFace(new Vertex[] { verts[2], verts[0], verts[3] });

			return tetrahedron;
		}

		public static Mesh CreateCube(double xScale = 1, double yScale = 1, double zScale = 1)
		{
			return CreateCube(new Vector3(xScale, yScale, zScale));
		}

		public static Mesh CreateCube(Vector3 scale)
		{
			scale *= .5; // the cube is -1 to 1 and I want it to be -.5 to .5 so it is a unit cube.
			Mesh cube = new Mesh();
			Vertex[] verts = new Vertex[8];
			verts[0] = cube.CreateVertex(new Vector3(-1, -1, 1) * scale);
			verts[1] = cube.CreateVertex(new Vector3(1, -1, 1) * scale);
			verts[2] = cube.CreateVertex(new Vector3(1, 1, 1) * scale);
			verts[3] = cube.CreateVertex(new Vector3(-1, 1, 1) * scale);
			verts[4] = cube.CreateVertex(new Vector3(-1, -1, -1) * scale);
			verts[5] = cube.CreateVertex(new Vector3(1, -1, -1) * scale);
			verts[6] = cube.CreateVertex(new Vector3(1, 1, -1) * scale);
			verts[7] = cube.CreateVertex(new Vector3(-1, 1, -1) * scale);

			// front
			cube.CreateFace(new Vertex[] { verts[0], verts[1], verts[2], verts[3] });
			// left
			cube.CreateFace(new Vertex[] { verts[4], verts[0], verts[3], verts[7] });
			// right
			cube.CreateFace(new Vertex[] { verts[1], verts[5], verts[6], verts[2] });
			// back
			cube.CreateFace(new Vertex[] { verts[4], verts[7], verts[6], verts[5] });
			// top
			cube.CreateFace(new Vertex[] { verts[3], verts[2], verts[6], verts[7] });
			// bottom
			cube.CreateFace(new Vertex[] { verts[4], verts[5], verts[1], verts[0] });

			return cube;
		}

		public static Mesh CreateIcosahedron(double scale = 1)
		{
			Mesh icosahedron = new Mesh();
			double[] icosahedronVertices =
            {
                0, -0.525731, 0.850651,
                0.850651, 0, 0.525731,
                0.850651, 0, -0.525731,
                -0.850651, 0, -0.525731,
                -0.850651, 0, 0.525731,
                -0.525731, 0.850651, 0,
                0.525731, 0.850651, 0,
                0.525731, -0.850651, 0,
                -0.525731, -0.850651, 0,
                0, -0.525731, -0.850651,
                0, 0.525731, -0.850651,
                0, 0.525731, 0.850651
            };

			int[] icosahedronIndicies =
            {
                1, 2, 6,
                1, 7, 2,
                3, 4, 5,
                4, 3, 8,
                6, 5, 11,
                5, 6, 10,
                9, 10, 2,
                10, 9, 3,
                7, 8, 9,
                8, 7, 0,
                11, 0, 1,
                0, 11, 4,
                6, 2, 10,
                1, 6, 11,
                3, 5, 10,
                5, 4, 11,
                2, 7, 9,
                7, 1, 0,
                3, 9, 8,
                4, 8, 0,
            };

			Vertex[] verts = new Vertex[icosahedronVertices.Length / 3];
			for (int i = 0; i < icosahedronVertices.Length / 3; i++)
			{
				verts[i] = icosahedron.CreateVertex(new Vector3(icosahedronVertices[i * 3 + 0], icosahedronVertices[i * 3 + 1], icosahedronVertices[i * 3 + 2]));
			}

			for (int i = 0; i < icosahedronIndicies.Length / 3; i++)
			{
				Vertex[] triangleVertices = new Vertex[]
                {
                    verts[icosahedronIndicies[i * 3 + 0]],
                    verts[icosahedronIndicies[i * 3 + 1]],
                    verts[icosahedronIndicies[i * 3 + 2]],
                };
				icosahedron.CreateFace(triangleVertices);
			}

			icosahedron.Transform(Matrix4X4.CreateScale(scale));

			return icosahedron;
		}
	}
}