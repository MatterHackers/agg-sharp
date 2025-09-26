/*
Copyright (c) 2025, Lars Brubaker
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

using ClipperLib;
using DualContouring;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;


namespace MatterHackers.PolygonMesh.UnitTests
{
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class MeshTests
	{
		// [TestFixtureSetUp]
		// public async Task Setup()
		// {
		//    string relativePath = "../../../../../agg-sharp/PlatformWin32/bin/Debug/agg_platform_win32.dll";
		//    if(Path.DirectorySeparatorChar != '/')
		//    {
		//        relativePath = relativePath.Replace('/', Path.DirectorySeparatorChar);
		//    }
		//    File.Copy(relativePath, Path.GetFileName(relativePath), true);
		// }
		private static readonly int meshSaveIndex = 0;

		public void SaveDebugInfo(Mesh mesh, string testHint = "")
		{
#if DEBUG_INTO_TGAS
			throw new NotImplementedException();
			// DebugRenderToImage debugRender = new DebugRenderToImage(mesh);
			// if (testHint != "")
			// {
			// 	debugRender.RenderToPng("debug face {0} - {1}.png".FormatWith(meshSaveIndex++, testHint));
			// }
			// else
			// {
			// 	debugRender.RenderToPng("debug face {0}.png".FormatWith(meshSaveIndex++));
			// }
#endif
		}

		[Test]
		public async Task SdfDensityFunctions()
		{
			var cylinder = new Cylinder()
			{
				Height = 2,
				Radius = 0.5
			};

			await Assert.That(cylinder.Bounds.Equals(new AxisAlignedBoundingBox(-.5, -.5, -1, .5, .5, 1), .001)).IsTrue();
			await Assert.That(cylinder.Sdf(new Vector3(0, 0, 2))).IsEqualTo(1);
			await Assert.That(cylinder.Sdf(new Vector3(0, 0, -2))).IsEqualTo(1);
			await Assert.That(cylinder.Sdf(new Vector3(0, 1, 1))).IsEqualTo(.5);
			await Assert.That(cylinder.Sdf(new Vector3(1, 0, 1))).IsEqualTo(.5);
		}

		[Test]
		public async Task PolygonRequirements()
		{
			//       /\1
			//      /  \
			//     /    \
			//    /      \
			//   /        \
			//  /          \
			// /____________\
			// 2             0

			var outerLoop = PolygonsExtensions.CreateFromString("x:1000, y:0,x:0, y:1000,x:-1000, y:0,|");

			// crossing the bottom
			{
				var intersections = outerLoop[0].GetIntersections(new IntPoint(0, -10), new IntPoint(0, 10));
				await Assert.That(intersections.Count() == 1).IsTrue();
				await Assert.That(intersections.First().pointIndex).IsEqualTo(2);
				await Assert.That(intersections.First().intersection).IsEqualTo(ClipperLib.Intersection.Intersect);
				await Assert.That(intersections.First().position).IsEqualTo(new IntPoint(0, 0));
			}

			// touching the top point
			{
				var intersections = outerLoop[0].GetIntersections(new IntPoint(0, 700), new IntPoint(0, 1000));
                await Assert.That(intersections.Count() == 2).IsTrue();
				foreach (var intersection in intersections)
				{
					await Assert.That(intersection.intersection).IsEqualTo(ClipperLib.Intersection.Colinear);
					await Assert.That(intersection.position).IsEqualTo(new IntPoint(0, 1000));
				}
			}

			// touching the bottom line
			{
				var intersections = outerLoop[0].GetIntersections(new IntPoint(0, -10), new IntPoint(0, 0));
                await Assert.That(intersections.Count() == 1).IsTrue();
				await Assert.That(intersections.First().pointIndex).IsEqualTo(2);
				await Assert.That(intersections.First().intersection).IsEqualTo(ClipperLib.Intersection.Colinear);
				await Assert.That(intersections.First().position).IsEqualTo(new IntPoint(0, 0));
			}
		}

		[Test]
		public async Task EnsureCorrectStitchOrder()
		{
            // You can see it in EnsureCorrectStitchOrder.html

            //       /\1
            //      /1 \
            //     / /\ \
            //    / /  \0\
            //   / 2\  /  \
            //  /    \/3   \
            // /____________\
            // 2             0

            // If the advance is on the 0 (outside) polygon, create [outside prev, outside new, inside]
            // If the advance is on the 1 (inside) polygon, creat [outside, inside new, inside prev]

            var outerLoop = PolygonsExtensions.CreateFromString("x:1000, y:0,x:0, y:1000,x:-1000, y:0,|")[0];
			var innerLoop = PolygonsExtensions.CreateFromString("x:400, y:500,x:0, y:750,x:-400, y:500,x:0, y:250,|")[0];

			var (outerStart, innerStart) = PathStitcher.BestStartIndices(outerLoop, innerLoop);

			await Assert.That(outerStart).IsEqualTo(1);
			await Assert.That(innerStart).IsEqualTo(1);

			var expected = new List<(int outerIndex, int innerIndex, int polyIndex)>()
			{
				(1,1,1), // the point on outer, the point on inner, the polygon to advance on
				(1,2,0),
				(2,2,1),
				(2,3,0),
				(0,3,1),
				(0,0,0),
				(1,0,1),
			};
			for (var i = 0; i < expected.Count; i++)
			{
				var data = expected[i];
				var polygonToAndvanceOn = PathStitcher.GetPolygonToAdvance(outerLoop, data.outerIndex, innerLoop, data.innerIndex);
                await Assert.That(polygonToAndvanceOn).IsEqualTo(data.polyIndex);
			}
		}

		[Test]
		public async Task FaceCutWoundCorrectly()
		{
			var vertices = new List<Vector3Float>()
			{
				new Vector3Float(new Vector2(1, 0).GetRotated(MathHelper.Tau / 3 * 0), 0),
				new Vector3Float(new Vector2(1, 0).GetRotated(MathHelper.Tau / 3 * 1), 0),
				new Vector3Float(new Vector2(1, 0).GetRotated(MathHelper.Tau / 3 * 2), 0),
			};

			var face = new Face(0, 1, 2, new Vector3Float(0, 0, 1));

			async Task CheckAngle(double angle, double distance)
			{
				var normal = new Vector3(new Vector2(1, 0).GetRotated(angle), 0);
				face.GetCutLine(vertices,
					new Plane(normal, distance),
					out Vector3 start,
					out Vector3 end);

				var direction = end - start;
				var yDirection = new Vector2(direction.X, direction.Y).GetRotated(-angle);
                await Assert.That(yDirection.Y < 0).IsTrue();
			}

			await CheckAngle(MathHelper.Tau / 3 * 0, .5);
			await CheckAngle(MathHelper.Tau / 3 * 1, .5);
			await CheckAngle(MathHelper.Tau / 3 * 2, .5);
		}

		public static void DebugSegments(IEnumerable<(Vector2 start, Vector2 end)> segments, string path = "temp.png")
		{
			ImageBuffer image = new ImageBuffer(512, 512);
			var minX = segments.Select(i => Math.Min(i.start.X, i.end.X)).Min();
			var maxX = segments.Select(i => Math.Max(i.start.X, i.end.X)).Max();
			var minY = segments.Select(i => Math.Min(i.start.Y, i.end.Y)).Min();
			var maxY = segments.Select(i => Math.Max(i.start.Y, i.end.Y)).Max();



			ImageIO.SaveImageData(path, image);
		}

		[Test]
		public async Task CutsRespectWindingOrder()
		{
			var cube = PlatonicSolids.CreateCube(10, 10, 10);
			cube.Translate(0, 0, 5); // move bottom to z=0
			var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 5));

			// StlProcessing.Save(cube, "c:\\temp\\cube.stl", CancellationToken.None, new MeshOutputSettings() { OutputTypeSetting = MeshOutputSettings.OutputType.Ascii });

			async Task CheckFace(int faceIndex)
			{
				var face = cube.Faces[faceIndex];
				if (face.normal.Z == 0)
				{
					Vector3 start, end;
                    await Assert.That(face.GetCutLine(cube.Vertices, cutPlane, out start, out end)).IsTrue();
					if (face.normal.X < 0)
					{
                        await Assert.That(start.Y > end.Y).IsTrue();
					}
					else if (face.normal.Y < 0)
					{
                        await Assert.That(start.X < end.X).IsTrue();
					}
					else if (face.normal.X > 0)
					{
                        await Assert.That(start.Y < end.Y).IsTrue();
					}
					else if (face.normal.Y > 0)
					{
                        await Assert.That(start.X > end.X).IsTrue();
					}
				}
			}

			for (var faceIndex = 0; faceIndex < cube.Faces.Count; faceIndex++)
			{
				await CheckFace(faceIndex);
			}
		}

		[Test]
		public async Task GetSliceLoop()
		{
			{
				var tetrahedron = PlatonicSolids.CreateTetrahedron(10);
				tetrahedron.Translate(new Vector3(0, 0, -tetrahedron.GetAxisAlignedBoundingBox().MinXYZ.Z));
				var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 3));
				var slice = SliceLayer.CreateSlice(tetrahedron, cutPlane);
				await Assert.That(slice.Count()).IsEqualTo(1);
				await Assert.That(slice[0].Count).IsEqualTo(3);
			}

			{
				var cube = PlatonicSolids.CreateCube(10, 10, 10);
				var cutPlane = new Plane(Vector3.UnitX, new Vector3(3, 0, 0));
				var slice = SliceLayer.CreateSlice(cube, cutPlane);
				await Assert.That(slice.Count()).IsEqualTo(1);
				await Assert.That(slice[0].Count).IsEqualTo(4);
			}

			{
				var cube = PlatonicSolids.CreateCube(10, 10, 10);
				cube.Translate(0, 0, 5); // move bottom to z=0
				var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 5));
				var unorderedSegments = SliceLayer.GetUnorderdSegments(cube, cutPlane);
				await Assert.That(unorderedSegments.Count).IsEqualTo(8);
				var fastLookups = SliceLayer.CreateFastIndexLookup(unorderedSegments);
				await Assert.That(fastLookups.Count).IsEqualTo(8);
				var closedLoops = SliceLayer.FindClosedPolygons(unorderedSegments);
				await Assert.That(closedLoops.Count()).IsEqualTo(1);
			}

			{
				var cube = PlatonicSolids.CreateCube(10, 10, 10);
				cube.Translate(0, 0, 5); // move bottom to z=0
				var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 5));
				var unorderedSegments = SliceLayer.GetUnorderdSegments(cube, cutPlane);
				await Assert.That(unorderedSegments.Count).IsEqualTo(8);
				var fastLookups = SliceLayer.CreateFastIndexLookup(unorderedSegments);
				await Assert.That(fastLookups.Count).IsEqualTo(8);
				var closedLoops = SliceLayer.FindClosedPolygons(unorderedSegments);
				await Assert.That(closedLoops.Count()).IsEqualTo(1);
			}

			{
				var cube1 = PlatonicSolids.CreateCube(10, 10, 10);
				cube1.Translate(0, 0, 5); // move bottom to z=0
				var cube2 = PlatonicSolids.CreateCube(10, 10, 10);
				cube2.Translate(3, 3, 5);
				var cubes = new Mesh();
				cubes.CopyFaces(cube1);
				cubes.CopyFaces(cube2);
				var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 5));
				var unorderedSegments = SliceLayer.GetUnorderdSegments(cubes, cutPlane);
				await Assert.That(unorderedSegments.Count).IsEqualTo(16);
				var fastLookups = SliceLayer.CreateFastIndexLookup(unorderedSegments);
				await Assert.That(fastLookups.Count).IsEqualTo(16);//, "There should be two loops of 8 segments that all have unique starts");
				var closedLoops = SliceLayer.FindClosedPolygons(unorderedSegments);
				await Assert.That(closedLoops.Count).IsEqualTo(2);
				var union = SliceLayer.UnionClosedPolygons(closedLoops);
				await Assert.That(union.Count()).IsEqualTo(1);
			}
		}

		[Test]
		public async Task SingleLoopStiching()
		{
			return;
			// only a CCW bottom
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 100,0, 100,100, 0,100");
				var top = PolygonsExtensions.CreateFromString("");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a bottom face, no walls
				await Assert.That(mesh.Faces.Count).IsEqualTo(2);
				foreach (var vertex in mesh.Vertices)
				{
					await Assert.That(vertex.Z).IsEqualTo(0);
				}
			}

			// only a CCW top
			{
				var bottom = PolygonsExtensions.CreateFromString("");
				var top = PolygonsExtensions.CreateFromString("0,0, 100,0, 100,100, 0,100");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a top face, no walls
				// only a bottom face, no walls
				await Assert.That(mesh.Faces.Count).IsEqualTo(2);
				foreach (var vertex in mesh.Vertices)
				{
					await Assert.That(vertex.Z).IsEqualTo(10);
				}
			}

			// a simple skirt wound ccw
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 100,0, 100,100, 0,100");
				var top = PolygonsExtensions.CreateFromString("0,0 ,100,0, 100,100, 0,100");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				await Assert.That(mesh.Faces.Count).IsEqualTo(8);
			}

			// a simple skirt wound CW (error condition)
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var top = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				await Assert.That(mesh.Faces.Count).IsEqualTo(0);
			}

			// only a CW bottom (error condition)
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var top = PolygonsExtensions.CreateFromString("");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a bottom face, no walls
				await Assert.That(mesh.Faces.Count).IsEqualTo(0);
			}

			// only a CW top (error condition)
			{
				var bottom = PolygonsExtensions.CreateFromString("");
				var top = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a top face, no walls
				// only a bottom face, no walls
				await Assert.That(mesh.Faces.Count).IsEqualTo(0);
			}
		}

		public async Task DetectAndRemoveTJunctions()
		{
			// throw new NotImplementedException();
		}

		[Test]
		public async Task SplitFaceEdgeEdge()
		{
			async Task TestPositions(int p0, int p1, int p2)
			{
				var positions = new Vector3[] { default(Vector3), new Vector3(10, 0, 0), new Vector3(5, 20, 0) };
				var mesh = new Mesh();
				// .     |
				// .    /|\
				// .   / | \
				// .  /  |  \
				// . /___|___\
				// .     |
				mesh.CreateFace(positions[p0], positions[p1], positions[p2]);
				await Assert.That(mesh.Faces.Count()).IsEqualTo(1);
				await Assert.That(mesh.Vertices.Count).IsEqualTo(3);

                // we find a split
                await Assert.That(mesh.SplitFace(0, new Plane(new Vector3(1, 0, 0), 5))).IsTrue();
				// we now have 2 faces
				await Assert.That(mesh.Faces.Count).IsEqualTo(2);
				// we now have 5 verts
				// the have all the expected x values
				await Assert.That(mesh.Vertices.Count).IsEqualTo(4);
				await Assert.That(mesh.Vertices.Where(v => v.X == 0).Count()).IsEqualTo(1);
				await Assert.That(mesh.Vertices.Where(v => v.X == 5).Count()).IsEqualTo(2);
				await Assert.That(mesh.Vertices.Where(v => v.X == 10).Count()).IsEqualTo(1);
				// no face crosses the split line
				await Assert.That(mesh.Faces.Where(f =>
				{
					// all face vertices are less than the split line or greater than the split line
					return (mesh.Vertices[f.v0].X <= 5 && mesh.Vertices[f.v1].X <= 5 && mesh.Vertices[f.v2].X <= 5)
						|| (mesh.Vertices[f.v0].X >= 5 && mesh.Vertices[f.v1].X >= 5 && mesh.Vertices[f.v2].X >= 5);
				}).Count()).IsEqualTo(2);
			}

			// test every vertex orientation
			await TestPositions(0, 1, 2);
			await TestPositions(0, 2, 1);
			await TestPositions(1, 0, 2);
			await TestPositions(1, 2, 0);
			await TestPositions(2, 0, 1);
			await TestPositions(2, 1, 0);
		}

		[Test]
		public async Task SplitFaceTwoEdges()
		{
			async Task TestPositions(int p0, int p1, int p2)
			{
				var positions = new Vector3[] { new Vector3(0, 5, 0), default(Vector3), new Vector3(10, 0, 0) };
				var mesh = new Mesh();
				// split the face along the vertical line
				// . 0 _    |
				// .  |   __|
				// .  |     | __
				// .  |_____|_______
				// . 1      |       2
				mesh.CreateFace(positions[p0], positions[p1], positions[p2]);
				await Assert.That(mesh.Faces.Count()).IsEqualTo(1);
				await Assert.That(mesh.Vertices.Count).IsEqualTo(3);

                // we find a split
                await Assert.That(mesh.SplitFace(0, new Plane(new Vector3(1, 0, 0), 5))).IsTrue();
				// we now have 3 faces
				await Assert.That(mesh.Faces.Count).IsEqualTo(3);
				// we now have 5 verts
				// the have all the expected x values
				await Assert.That(mesh.Vertices.Count).IsEqualTo(5);
				await Assert.That(mesh.Vertices.Where(v => v.X == 0).Count()).IsEqualTo(2);
				await Assert.That(mesh.Vertices.Where(v => v.X == 5).Count()).IsEqualTo(2);
				await Assert.That(mesh.Vertices.Where(v => v.X == 10).Count()).IsEqualTo(1);
				// no face crosses the split line
				await Assert.That(mesh.Faces.Where(f =>
				{
					// all face vertices are less than the split line or greater than the split line
					return (mesh.Vertices[f.v0].X <= 5 && mesh.Vertices[f.v1].X <= 5 && mesh.Vertices[f.v2].X <= 5)
						|| (mesh.Vertices[f.v0].X >= 5 && mesh.Vertices[f.v1].X >= 5 && mesh.Vertices[f.v2].X >= 5);
				}).Count()).IsEqualTo(3);
			}

			// test every vertex orientation
			await TestPositions(0, 1, 2);
			await TestPositions(0, 2, 1);
			await TestPositions(1, 0, 2);
			await TestPositions(1, 2, 0);
			await TestPositions(2, 0, 1);
			await TestPositions(2, 1, 0);
		}

		[Test]
		public async Task CreateBspFaceTrees()
		{
			// a simple list of 3 faces
			//
			// Index 1 ^------------------- z = 3
			//
			// Index 0 ^------------------- z = 2
			//
			// Index 2 ^------------------- z = 1

			var testMesh = new Mesh();

			testMesh.CreateFace(
				new Vector3(0, 0, 2),
				new Vector3(10, 0, 2),
				new Vector3(5, 5, 2)
			);

			testMesh.CreateFace(
				new Vector3(0, 0, 3),
				new Vector3(10, 0, 3),
				new Vector3(5, 5, 3)
			);
			testMesh.CreateFace(
				new Vector3(0, 0, 1),
				new Vector3(10, 0, 1),
				new Vector3(5, 5, 1)
			);

			// test they are in the right order
			{
				var root = FaceBspTree.Create(testMesh);

                await Assert.That(root.Index == 1).IsTrue();
                await Assert.That(root.BackNode.Index == 0).IsTrue();
                await Assert.That(root.BackNode.BackNode.Index == 2).IsTrue();

				var renderOredrList = FaceBspTree.GetFacesInVisibiltyOrder(testMesh, root, Matrix4X4.Identity, Matrix4X4.Identity).ToList();
                await Assert.That(renderOredrList[0] == 1).IsTrue();
                await Assert.That(renderOredrList[1] == 0).IsTrue();
                await Assert.That(renderOredrList[2] == 2).IsTrue();
			}
		}

		[Test]
		public async Task CreateDualContouringCube()
		{
			foreach (var size in new[] { 1, 15, 200 })
				foreach (var iterations in new[] { 2, 3, 4, 5, 6, 7 })
				{
					// apply dual contouring to a box shape
					// and validate that the generated mesh is a cube

					var box = new DualContouring.Box()
					{
						Size = new Vector3(size, size, size)
					};

					var bounds = box.Bounds;
					bounds.Expand(.1);

					var octree = DualContouring.Octree.BuildOctree(box.Sdf, bounds.MinXYZ, bounds.Size, iterations, threshold: .001);
					var mesh = DualContouring.Octree.GenerateMeshFromOctree(octree);

					await Assert.That(mesh.Faces.Count).IsEqualTo(12);
					await Assert.That(mesh.Vertices.Count).IsEqualTo(8);

					var expectedVertices = PlatonicSolids.CreateCube(size, size, size).Vertices
								.OrderBy(v => v.X)
								.ThenBy(v => v.Y)
								.ThenBy(v => v.Z);

					var actualVertices = mesh.Vertices
								.OrderBy(v => v.X)
								.ThenBy(v => v.Y)
								.ThenBy(v => v.Z);

					foreach (var (expected, actual) in expectedVertices.Zip(actualVertices))
					{
                        await Assert.That((expected - actual).Length < 1e-6).IsTrue();
					}
				}
		}
	}
}
