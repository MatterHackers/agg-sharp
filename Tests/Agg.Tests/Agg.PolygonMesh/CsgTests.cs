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

using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using MatterHackers.Csg;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using MatterHackers.RenderOpenGl;
using System.Linq;
using MatterHackers.DataConverters3D;
using System.IO;
using System.Threading;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh.CSG")]
	public class MeshCsgTests
	{
		[Test]
		public void SubtractWorks()
		{
			Vector3 centering = new Vector3(100, 100, 20);
			Mesh meshA = PlatonicSolids.CreateCube(40, 40, 40);
			meshA.Translate(centering);
			Mesh meshB = PlatonicSolids.CreateCube(40, 40, 40);

			Vector3 finalTransform = new Vector3(99.999927784394, 102.400700290798, 16.3588316937214);
			meshB.Translate(finalTransform);

			Mesh result = CsgOperations.Subtract(meshA, meshB);

			AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox intersect_aabb = result.GetAxisAlignedBoundingBox();

			Assert.IsTrue(a_aabb.XSize == 40 && a_aabb.YSize == 40 && a_aabb.ZSize == 40);
			Assert.IsTrue(intersect_aabb.XSize == 40 && intersect_aabb.YSize == 40 && intersect_aabb.ZSize == 40);

			// Todo: turn this on
			//Assert.IsTrue(result.IsManifold());
		}

		[Test, Ignore("TODO: Get this test passing")]
		public void TopIsSolid()
		{
			int sides = 3;
			CsgObject keep = new Cylinder(20, 20, sides);
			var keepMesh = CsgToMesh.Convert(keep, true);
			CsgObject subtract = new Cylinder(10, 21, sides);
			subtract = new SetCenter(subtract, keep.GetCenter());
			var subtractMesh = CsgToMesh.Convert(subtract, true);
			CsgObject result = keep - subtract;
			var resultMesh = CsgToMesh.Convert(result, true);

			Assert.AreEqual(0, keepMesh.GetNonManifoldEdges().Count, "All faces should be 2 manifold");
			Assert.AreEqual(0, subtractMesh.GetNonManifoldEdges().Count, "All faces should be 2 manifold");
			//Assert.AreEqual(0, resultMesh.GetNonManifoldEdges().Count, "All faces of this subtract should be 2 manifold");
		}

		[Test]
		public void SubtractHasAllFaces()
		{
			double XOffset = -.4;
			CsgObject keep = new Box(10, 10, 10);
			var keepMesh = CsgToMesh.Convert(keep, true);
			var subtract = new Translate(new Box(10, 10, 10), XOffset, -3, 2);
			var subtractMesh = CsgToMesh.Convert(subtract, true);
			CsgObject result = keep - subtract;
			var resultMesh =  CsgToMesh.Convert(result);
			//mesh.Save("C:/Temp/TempCsgMesh.stl");
			var bottomOfSubtractFaces = resultMesh.Faces.Where((f) =>
				AreEqual(f.Normal.Z, 1)
				&&
				FaceAtHeight(f, -3)
				).ToArray();

			HashSet<IVertex> allVertices = new HashSet<IVertex>();
			foreach(var face in bottomOfSubtractFaces)
			{
				foreach(var vertex in face.Vertices())
				{
					if (!allVertices.Contains(vertex))
					{
						allVertices.Add(vertex);
					}
				}
			}

			// back right
			Assert.IsTrue(HasPosition(allVertices, new Vector3(4.6, 2, -3)));
			// front right
			Assert.IsTrue(HasPosition(allVertices, new Vector3(4.6, -5, -3)));
			// back left
			Assert.IsTrue(HasPosition(allVertices, new Vector3(-5, 2, -3)));
			// front left
			Assert.IsTrue(HasPosition(allVertices, new Vector3(-5, -5, -3)), "Must have front left corner point");

			Assert.AreEqual(0, keepMesh.GetNonManifoldEdges().Count, "All faces should be 2 manifold");
			Assert.AreEqual(0, subtractMesh.GetNonManifoldEdges().Count, "All faces should be 2 manifold");
			//Assert.AreEqual(0, resultMesh.GetNonManifoldEdges().Count, "All faces of this subtract should be 2 manifold");
		}

		private bool HasPosition(HashSet<IVertex> allVertices, Vector3 position)
		{
			foreach(var vertex in allVertices)
			{
				if(vertex.Position.Equals(position, .0001))
				{
					return true;
				}
			}

			return false;
		}

		bool FaceAtHeight(Face face, double height)
		{
			foreach (var vertex in face.Vertices())
			{
				if(!AreEqual(vertex.Position.Z, height))
				{
					return false;
				}
			}

			return true;
		}

		bool AreEqual(double a, double b, double errorRange = .001)
		{
			if(a < b + errorRange
				&& a > b - errorRange)
			{
				return true;
			}

			return false;
		}

		[Test, Ignore("Crashes NUnit with an unrecoverable StackOverflow error, ending test passes on build servers")]
		public void SubtractIcosahedronsWorks()
		{
			Vector3 centering = new Vector3(100, 100, 20);
			Mesh meshA = PlatonicSolids.CreateIcosahedron(35);
			meshA.Translate(centering);
			Mesh meshB = PlatonicSolids.CreateIcosahedron(35);

			Vector3 finalTransform = new Vector3(105.240172225344, 92.9716306394062, 18.4619570261172);
			Vector3 rotCurrent = new Vector3(4.56890223673623, -2.67874102322035, 1.02768848238523);
			Vector3 scaleCurrent = new Vector3(1.07853517569753, 0.964980885267323, 1.09290934544604);
			Matrix4X4 transformB = Matrix4X4.CreateScale(scaleCurrent) * Matrix4X4.CreateRotation(rotCurrent) * Matrix4X4.CreateTranslation(finalTransform);
			meshB.Transform(transformB);

			Mesh result = CsgOperations.Subtract(meshA, meshB);

			AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox intersect_aabb = result.GetAxisAlignedBoundingBox();

			Assert.IsTrue(a_aabb.XSize == 40 && a_aabb.YSize == 40 && a_aabb.ZSize == 40);
			Assert.IsTrue(intersect_aabb.XSize == 40 && intersect_aabb.YSize == 40 && intersect_aabb.ZSize == 40);

			// Todo: turn this on
			//Assert.IsTrue(result.IsManifold());
		}

		[Test]
		public void UnionExactlyOnWorks()
		{
			Mesh meshA = PlatonicSolids.CreateCube(40, 40, 40);
			Mesh meshB = PlatonicSolids.CreateCube(40, 40, 40);

			Mesh result = CsgOperations.Union(meshA, meshB);

			AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
			AxisAlignedBoundingBox intersect_aabb = result.GetAxisAlignedBoundingBox();

			Assert.IsTrue(a_aabb.XSize == 40 && a_aabb.YSize == 40 && a_aabb.ZSize == 40);
			Assert.IsTrue(intersect_aabb.XSize == 40 && intersect_aabb.YSize == 40 && intersect_aabb.ZSize == 40);

			// Todo: turn this on
			//Assert.IsTrue(result.IsManifold());
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

				Mesh result = CsgOperations.Intersect(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = result.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 6 && intersect_aabb.YSize == 6 && intersect_aabb.ZSize == 6);

				// Todo: turn this on
				//Assert.IsTrue(result.IsManifold());
			}

			// the intersection of 2 cubes that miss eachother
			{
				Mesh meshA = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));

				meshA.Translate(new Vector3(-5, -5, -5));
				Mesh meshB = PlatonicSolids.CreateCube(new Vector3(10, 10, 10));
				meshB.Translate(new Vector3(5, 5, 5));

				Mesh result = CsgOperations.Intersect(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = result.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 0 && intersect_aabb.YSize == 0 && intersect_aabb.ZSize == 0);

				// Todo: turn this on
				//Assert.IsTrue(result.IsManifold());
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

				Mesh result = CsgOperations.Union(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = result.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 14 && intersect_aabb.YSize == 10 && intersect_aabb.ZSize == 10);

				// Todo: turn this on
				//Assert.IsTrue(result.IsManifold());
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

				Mesh result = CsgOperations.Subtract(meshA, meshB);

				AxisAlignedBoundingBox a_aabb = meshA.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox b_aabb = meshB.GetAxisAlignedBoundingBox();
				AxisAlignedBoundingBox intersect_aabb = result.GetAxisAlignedBoundingBox();

				Assert.IsTrue(a_aabb.XSize == 10 && a_aabb.YSize == 10 && a_aabb.ZSize == 10);
				Assert.IsTrue(intersect_aabb.XSize == 4 && intersect_aabb.YSize == 10 && intersect_aabb.ZSize == 10);

				// Todo: turn this on
				//Assert.IsTrue(result.IsManifold());
			}
		}

		[Test, Ignore("Work in progress")]
		public void SubtractionMakesClosedSolid()
		{
			double XOffset = -.4;

			MatterHackers.Csg.CsgObject boxCombine = new MatterHackers.Csg.Solids.Box(10, 10, 10);
			boxCombine -= new MatterHackers.Csg.Transform.Translate(new MatterHackers.Csg.Solids.Box(10, 10, 10), XOffset, -3, 2);
			Mesh result = RenderOpenGl.CsgToMesh.Convert(boxCombine);

			Assert.IsTrue(result.IsManifold());
		}
	}
}