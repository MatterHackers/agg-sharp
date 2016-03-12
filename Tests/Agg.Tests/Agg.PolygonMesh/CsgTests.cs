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

using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System;
using System.Collections.Generic;


namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh")]
	public class MeshCsgTests
	{
		[Test]
		public void PlaneSubtractPlane()
		{
			Mesh meshA = new Mesh();
			Vertex[] verts = new Vertex[3];
			verts[0] = meshA.CreateVertex(new Vector3(-1, -1, 0));
			verts[1] = meshA.CreateVertex(new Vector3(1, -1, 0));
			verts[2] = meshA.CreateVertex(new Vector3(1, 1, 0));

			meshA.CreateFace(new Vertex[] { verts[0], verts[1], verts[2] });


			meshA.Translate(new Vector3(-2, -2, -2));
			Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
			meshB.Translate(new Vector3(2, 2, 2));

			Mesh meshIntersect = CsgOperations.Intersect(meshA, meshB);

			AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox intersect_aabb = meshIntersect.GetAxisAlignedBoundingBox();

			Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
			Assert.IsTrue(intersect_aabb.XSize == 6 && intersect_aabb.YSize == 6 && intersect_aabb.ZSize == 6);
		}

		[Test]
		public void SubtractWorks()
		{
			Vector3 centering = new Vector3(100, 100, 20);
			Mesh meshA = PlatonicSolids.CreateCube(40, 40, 40);
			meshA.Translate(centering);
			Mesh meshB = PlatonicSolids.CreateCube(40, 40, 40);

			Vector3 finalTransform = new Vector3(99.999927784394, 102.400700290798, 16.3588316937214);
			meshB.Translate(finalTransform);

			Mesh meshToAdd = CsgOperations.Subtract(meshA, meshB);

			AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox intersect_aabb = meshToAdd.GetAxisAlignedBoundingBox();

			Assert.IsTrue(a_aabb.XSize == 40 && a_aabb.YSize == 40 && a_aabb.ZSize == 40);
			Assert.IsTrue(intersect_aabb.XSize == 40 && intersect_aabb.YSize == 40 && intersect_aabb.ZSize == 40);
		}

		[Test]
		public void UnionExactlyOnWorks()
		{
			Mesh meshA = PlatonicSolids.CreateCube(40, 40, 40);
			Mesh meshB = PlatonicSolids.CreateCube(40, 40, 40);

			Mesh meshToAdd = CsgOperations.Union(meshA, meshB);

			AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox intersect_aabb = meshToAdd.GetAxisAlignedBoundingBox();

			Assert.IsTrue(a_aabb.XSize == 40 && a_aabb.YSize == 40 && a_aabb.ZSize == 40);
			Assert.IsTrue(intersect_aabb.XSize == 40 && intersect_aabb.YSize == 40 && intersect_aabb.ZSize == 40);
		}

		[Test]
		public void EnsureSimpleCubeIntersection()
		{
			// the intersection of 2 cubes
			{
				Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

				meshA.Translate(new Vector3(-2, -2, -2));
				Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
				meshB.Translate(new Vector3(2, 2, 2));

				Mesh meshIntersect = CsgOperations.Intersect(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = meshIntersect.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 6 && intersect_aabb.YSize == 6 && intersect_aabb.ZSize == 6);
			}

			// the intersection of 2 cubes that miss eachother
			{
				Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

				meshA.Translate(new Vector3(-5, -5, -5));
				Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
				meshB.Translate(new Vector3(5, 5, 5));

				Mesh meshIntersect = CsgOperations.Intersect(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = meshIntersect.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 0 && intersect_aabb.YSize == 0 && intersect_aabb.ZSize == 0);
			}
		}

		[Test]
		public void EnsureSimpleCubeUnion()
		{
			// the union of 2 cubes
			{
				Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

				meshA.Translate(new Vector3(-2, 0, 0));
				Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
				meshB.Translate(new Vector3(2, 0, 0));

				Mesh meshIntersect = CsgOperations.Union(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = meshIntersect.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 14 && intersect_aabb.YSize == 10 && intersect_aabb.ZSize == 10);
			}
		}

		[Test]
		public void EnsureSimpleCubeSubtraction()
		{
			// the subtraction of 2 cubes
			{
				Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

				meshA.Translate(new Vector3(-2, 0, 0));
				Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
				meshB.Translate(new Vector3(2, 0, 0));

				Mesh meshIntersect = CsgOperations.Subtract(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = meshIntersect.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 4 && intersect_aabb.YSize == 10 && intersect_aabb.ZSize == 10);
			}
		}
	}
}