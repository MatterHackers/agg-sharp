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

using MatterHackers.DataConverters3D;
using MatterHackers.MeshVisualizer;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh")]
	public class SceneTests
	{
		[Test]
		public void SaveSimpleScene()
		{
			var scene = new InteractiveScene();
			scene.Children.Add(new Object3D
			{
				ItemType = Object3DTypes.Model
			});

			string tempPath = GetTempPath();
			string filePath = Path.Combine(tempPath, "some.mcx");

			scene.Save(filePath, tempPath);

			Assert.IsTrue(File.Exists(filePath));

			IObject3D loadedItem = Object3D.Load(filePath);
			Assert.IsTrue(loadedItem.Children.Count == 1);
		}

		[Test]
		public void CreatesAndLinksAmfsForUnsavedMeshes()
		{
			var scene = new InteractiveScene();
			scene.Children.Add(new Object3D
			{
				ItemType = Object3DTypes.Model,
				Mesh = PlatonicSolids.CreateCube(20, 20, 20)
			});

			string tempPath = GetTempPath();
			string filePath = Path.Combine(tempPath, "some.mcx");

			scene.Save(filePath, tempPath);

			Assert.IsTrue(File.Exists(filePath));

			IObject3D loadedItem = Object3D.Load(filePath);
			Assert.IsTrue(loadedItem.Children.Count == 1);

			IObject3D amfItem = loadedItem.Children.First();

			string meshPath = amfItem.MeshPath;
			Assert.IsTrue(!string.IsNullOrEmpty(meshPath));
			Assert.IsTrue(File.Exists(meshPath));

			Assert.IsTrue(amfItem.Children.Count == 1);
		}

		[Test]
		public void ResavedSceneRemainsConsistent()
		{
			var scene = new InteractiveScene();
			scene.Children.Add(new Object3D
			{
				ItemType = Object3DTypes.Model,
				Mesh = PlatonicSolids.CreateCube(20, 20, 20)
			});

			string tempPath = GetTempPath();
			string filePath = Path.Combine(tempPath, "some.mcx");

			scene.Save(filePath, tempPath);

			IObject3D loadedItem = Object3D.Load(filePath);

			var loadedJson = JsonConvert.SerializeObject(loadedItem);


			Assert.IsTrue(loadedItem.Children.Count == 1);

			IObject3D amfItem = loadedItem.Children.First();

			string meshPath = amfItem.MeshPath;
			Assert.IsTrue(!string.IsNullOrEmpty(meshPath));
			Assert.IsTrue(File.Exists(meshPath));

			Assert.IsTrue(amfItem.Children.Count == 1);
		}

		public static string GetTempPath()
		{
			string tempPath = Path.GetFullPath(Path.Combine("..", "..", "..", "..", "..", "..", "Tests", "temp", "scenetests"));
			Directory.CreateDirectory(tempPath);
			return tempPath;
		}

		private void SubtractWorks()
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
	}
}