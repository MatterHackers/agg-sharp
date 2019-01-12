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
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.PolygonMesh.UnitTests
{
	[TestFixture, Category("Agg.PolygonMesh")]
	public class MeshTests
	{
		//[TestFixtureSetUp]
		//public void Setup()
		//{
		//    string relativePath = "../../../../../agg-sharp/PlatformWin32/bin/Debug/agg_platform_win32.dll";
		//    if(Path.DirectorySeparatorChar != '/')
		//    {
		//        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
		//    }

		//    File.Copy(relativePath, Path.GetFileName(relativePath), true);
		//}
		private static int meshSaveIndex = 0;

		public void SaveDebugInfo(Mesh mesh, string testHint = "")
		{
#if DEBUG_INTO_TGAS
			throw new NotImplementedException();
			//DebugRenderToImage debugRender = new DebugRenderToImage(mesh);
			//if (testHint != "")
			//{
			//	debugRender.RenderToPng("debug face {0} - {1}.png".FormatWith(meshSaveIndex++, testHint));
			//}
			//else
			//{
			//	debugRender.RenderToPng("debug face {0}.png".FormatWith(meshSaveIndex++));
			//}
#endif
		}

		//[Test]
		public void GetSliceLoop()
		{
			var cube = MeshHelper.CreatePlane(10, 10);
			var cutPlane = new Plane(Vector3.UnitX, new Vector3(3, 0, 0));
			var slice = new SliceLayer(cutPlane);
			slice.CreateSlice(cube);
			//Assert.AreEqual(1, slice.ClosedPolygons.Count);
		}

		public void DetectAndRemoveTJunctions()
		{
			//throw new NotImplementedException();
		}

		[Test]
		public void CreateBspFaceTrees()
		{
			// a simple list of 3 faces
			//
			// Index 1 ^------------------- z = 3
			//
			// Index 0 ^------------------- z = 2
			//
			// Index 2 ^------------------- z = 1

			Mesh testMesh = new Mesh();

			testMesh.CreateFace(new Vector3[]
			{ 
				new Vector3(0, 0, 2),
				new Vector3(10, 0, 2),
				new Vector3(5, 5, 2)
			});
			testMesh.CreateFace(new Vector3[]
			{
				new Vector3(0, 0, 3),
				new Vector3(10, 0, 3),
				new Vector3(5, 5, 3)
			});
			testMesh.CreateFace(new Vector3[]
			{
				new Vector3(0, 0, 1),
				new Vector3(10, 0, 1),
				new Vector3(5, 5, 1)
			});

			// test they are in the right order
			{
				var root = FaceBspTree.Create(testMesh);

				Assert.IsTrue(root.Index == 1);
				Assert.IsTrue(root.BackNode.Index == 0);
				Assert.IsTrue(root.BackNode.BackNode.Index == 2);

				List<int> renderOredrList = FaceBspTree.GetFacesInVisibiltyOrder(testMesh, root, Matrix4X4.Identity, Matrix4X4.Identity).ToList();
				Assert.IsTrue(renderOredrList[0] == 1);
				Assert.IsTrue(renderOredrList[1] == 0);
				Assert.IsTrue(renderOredrList[2] == 2);
			}
		}
	}
}