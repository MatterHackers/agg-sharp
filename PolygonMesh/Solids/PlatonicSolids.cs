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
        public static Mesh CreateTetrahedron(double scale = 1)
        {
            Mesh tetrahedron = new Mesh();
            Vector2 basePoint = new Vector2(1, 0);
            double baseOffsetZ = -Math.Sin(MathHelper.DegreesToRadians(30));
            tetrahedron.Vertices.Add(new Vector3(basePoint, baseOffsetZ));
            tetrahedron.Vertices.Add(new Vector3(Vector2.Rotate(basePoint, MathHelper.Tau / 3), baseOffsetZ));
            tetrahedron.Vertices.Add(new Vector3(Vector2.Rotate(basePoint, 2 * MathHelper.Tau / 3), baseOffsetZ));
            tetrahedron.Vertices.Add(new Vector3(0, 0, 1));

            tetrahedron.Faces.Add(0, 2, 1, tetrahedron.Vertices); // add reversed because we want to see the bottom.
            tetrahedron.Faces.Add(0, 1, 3, tetrahedron.Vertices);
            tetrahedron.Faces.Add(1, 2, 3, tetrahedron.Vertices);
            tetrahedron.Faces.Add(2, 0, 3, tetrahedron.Vertices);

            if (scale != 1)
            {
                tetrahedron.Transform(Matrix4X4.CreateScale(scale));
            }

            return tetrahedron;
        }

        public static Mesh CreateCube(double xScale = 1, double yScale = 1, double zScale = 1)
        {
            return CreateCube(new Vector3(xScale, yScale, zScale));
        }

        public static Mesh CreateCube(Vector3 scale)
        {
            scale *= .5; // the cube is -1 to 1 and we want it to be -.5 to .5 so it is a unit cube.
            Mesh cube = new Mesh();
            cube.Vertices.Add(new Vector3(-1, -1, 1) * scale);
            cube.Vertices.Add(new Vector3(1, -1, 1) * scale);
            cube.Vertices.Add(new Vector3(1, 1, 1) * scale);
            cube.Vertices.Add(new Vector3(-1, 1, 1) * scale);
            cube.Vertices.Add(new Vector3(-1, -1, -1) * scale);
            cube.Vertices.Add(new Vector3(1, -1, -1) * scale);
            cube.Vertices.Add(new Vector3(1, 1, -1) * scale);
            cube.Vertices.Add(new Vector3(-1, 1, -1) * scale);

            // front
            cube.Faces.Add(0, 1, 2, cube.Vertices);
            cube.Faces.Add(0, 2, 3, cube.Vertices);
            // left
            cube.Faces.Add(4, 0, 3, cube.Vertices);
            cube.Faces.Add(4, 3, 7, cube.Vertices);
            // right
            cube.Faces.Add(1, 5, 6, cube.Vertices);
            cube.Faces.Add(1, 6, 2, cube.Vertices);
            // back
            cube.Faces.Add(4, 7, 6, cube.Vertices);
            cube.Faces.Add(4, 6, 5, cube.Vertices);
            // top
            cube.Faces.Add(3, 2, 6, cube.Vertices);
            cube.Faces.Add(3, 6, 7, cube.Vertices);
            // bottom
            cube.Faces.Add(4, 5, 1, cube.Vertices);
            cube.Faces.Add(4, 1, 0, cube.Vertices);

            return cube;
        }

        public static Mesh CreateOctahedron(double scale = 1)
        {
            Mesh octahedron = new Mesh();

            // Add the 6 vertices
            octahedron.Vertices.Add(new Vector3(1, 0, 0));
            octahedron.Vertices.Add(new Vector3(-1, 0, 0));
            octahedron.Vertices.Add(new Vector3(0, 1, 0));
            octahedron.Vertices.Add(new Vector3(0, -1, 0));
            octahedron.Vertices.Add(new Vector3(0, 0, 1));
            octahedron.Vertices.Add(new Vector3(0, 0, -1));

            // Add the 8 triangular faces
            octahedron.Faces.Add(4, 0, 2, octahedron.Vertices);
            octahedron.Faces.Add(4, 2, 1, octahedron.Vertices);
            octahedron.Faces.Add(4, 1, 3, octahedron.Vertices);
            octahedron.Faces.Add(4, 3, 0, octahedron.Vertices);
            octahedron.Faces.Add(5, 2, 0, octahedron.Vertices);
            octahedron.Faces.Add(5, 1, 2, octahedron.Vertices);
            octahedron.Faces.Add(5, 3, 1, octahedron.Vertices);
            octahedron.Faces.Add(5, 0, 3, octahedron.Vertices);

            if (scale != 1)
            {
                octahedron.Transform(Matrix4X4.CreateScale(scale));
            }

            return octahedron;
        }

        public static Mesh CreateDodecahedron(double scale = 1)
        {
            Mesh dodecahedron = new Mesh();

            // Golden ratio for vertex calculations
            double phi = (1 + Math.Sqrt(5)) / 2;
            double invPhi = 1 / phi;

            // Create the 20 vertices
            // Cube vertices
            for (int i = -1; i <= 1; i += 2)
                for (int j = -1; j <= 1; j += 2)
                    for (int k = -1; k <= 1; k += 2)
                        dodecahedron.Vertices.Add(new Vector3(i, j, k));

            // Rectangle vertices
            for (int i = -1; i <= 1; i += 2)
            {
                dodecahedron.Vertices.Add(new Vector3(0, i * phi, i * invPhi));
                dodecahedron.Vertices.Add(new Vector3(i * invPhi, 0, i * phi));
                dodecahedron.Vertices.Add(new Vector3(i * phi, i * invPhi, 0));
            }

            // Add the 12 pentagonal faces
            int[][] faces =
            [
                [0, 8, 9, 4, 16],
                [0, 16, 17, 2, 10],
                [0, 10, 11, 1, 8],
                [1, 11, 13, 5, 18],
                [1, 18, 19, 3, 9],
                [2, 12, 13, 11, 10],
                [2, 17, 15, 6, 12],
                [3, 14, 15, 17, 16],
                [3, 19, 7, 14, 15],
                [4, 9, 3, 15, 6],
                [4, 6, 12, 13, 5],
                [4, 5, 18, 19, 7]
            ];

            foreach (int[] face in faces)
            {
                for (int i = 2; i < face.Length; i++)
                {
                    dodecahedron.Faces.Add(face[0], face[i - 1], face[i], dodecahedron.Vertices);
                }
            }

            if (scale != 1)
            {
                dodecahedron.Transform(Matrix4X4.CreateScale(scale));
            }

            return dodecahedron;
        }

        public static Mesh CreateIcosahedron(double scale = 1)
        {
            Mesh icosahedron = new Mesh();

            // Golden ratio for icosahedron construction
            double phi = (1 + Math.Sqrt(5)) / 2;

            // Create the 12 vertices of the icosahedron
            // These form three orthogonal rectangles
            icosahedron.Vertices.Add(new Vector3(-1, phi, 0));
            icosahedron.Vertices.Add(new Vector3(1, phi, 0));
            icosahedron.Vertices.Add(new Vector3(-1, -phi, 0));
            icosahedron.Vertices.Add(new Vector3(1, -phi, 0));

            icosahedron.Vertices.Add(new Vector3(0, -1, phi));
            icosahedron.Vertices.Add(new Vector3(0, 1, phi));
            icosahedron.Vertices.Add(new Vector3(0, -1, -phi));
            icosahedron.Vertices.Add(new Vector3(0, 1, -phi));

            icosahedron.Vertices.Add(new Vector3(phi, 0, -1));
            icosahedron.Vertices.Add(new Vector3(phi, 0, 1));
            icosahedron.Vertices.Add(new Vector3(-phi, 0, -1));
            icosahedron.Vertices.Add(new Vector3(-phi, 0, 1));

            // Create the 20 triangular faces
            // 5 faces around vertex 0
            icosahedron.Faces.Add(0, 11, 5, icosahedron.Vertices);
            icosahedron.Faces.Add(0, 5, 1, icosahedron.Vertices);
            icosahedron.Faces.Add(0, 1, 7, icosahedron.Vertices);
            icosahedron.Faces.Add(0, 7, 10, icosahedron.Vertices);
            icosahedron.Faces.Add(0, 10, 11, icosahedron.Vertices);

            // 5 adjacent faces
            icosahedron.Faces.Add(1, 5, 9, icosahedron.Vertices);
            icosahedron.Faces.Add(5, 11, 4, icosahedron.Vertices);
            icosahedron.Faces.Add(11, 10, 2, icosahedron.Vertices);
            icosahedron.Faces.Add(10, 7, 6, icosahedron.Vertices);
            icosahedron.Faces.Add(7, 1, 8, icosahedron.Vertices);

            // 5 faces around vertex 3
            icosahedron.Faces.Add(3, 9, 4, icosahedron.Vertices);
            icosahedron.Faces.Add(3, 4, 2, icosahedron.Vertices);
            icosahedron.Faces.Add(3, 2, 6, icosahedron.Vertices);
            icosahedron.Faces.Add(3, 6, 8, icosahedron.Vertices);
            icosahedron.Faces.Add(3, 8, 9, icosahedron.Vertices);

            // 5 adjacent faces
            icosahedron.Faces.Add(4, 9, 5, icosahedron.Vertices);
            icosahedron.Faces.Add(2, 4, 11, icosahedron.Vertices);
            icosahedron.Faces.Add(6, 2, 10, icosahedron.Vertices);
            icosahedron.Faces.Add(8, 6, 7, icosahedron.Vertices);
            icosahedron.Faces.Add(9, 8, 1, icosahedron.Vertices);

            // Apply scaling if needed
            if (scale != 1)
            {
                icosahedron.Transform(Matrix4X4.CreateScale(scale));
            }

            return icosahedron;
        }
    }
}