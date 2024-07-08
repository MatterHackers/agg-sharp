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
using Agg.Tests.Agg;
using ClipperLib;
using DualContouring;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;


namespace MatterHackers.PolygonMesh.UnitTests
{
	using Polygons = List<List<IntPoint>>;
	using Polygon = List<IntPoint>;

	[TestFixture("Agg.PolygonMesh")]
	public class MeshTests
	{
		// [TestFixtureSetUp]
		// public void Setup()
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
        public void SdfDensityFunctions()
        {
            var cylinder = new Cylinder()
            {
                Height = 2,
                Radius = 0.5
            };

			Assert.True(cylinder.Bounds.Equals(new AxisAlignedBoundingBox(-.5, -.5, -1, .5, .5, 1), .001));
			Assert.Equal(1, cylinder.Sdf(new Vector3(0, 0, 2)));
			Assert.Equal(1, cylinder.Sdf(new Vector3(0, 0, -2)));
			Assert.Equal(.5, cylinder.Sdf(new Vector3(0, 1, 1)));
			Assert.Equal(.5, cylinder.Sdf(new Vector3(1, 0, 1)));
		}

		[Test]
        public void PolygonRequirements()
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
                Assert.True(intersections.Count() == 1);
                Assert.Equal(2, intersections.First().pointIndex);
                Assert.Equal(ClipperLib.Intersection.Intersect, intersections.First().intersection);
                Assert.Equal(new IntPoint(0, 0), intersections.First().position);
            }

            // touching the top point
            {
                var intersections = outerLoop[0].GetIntersections(new IntPoint(0, 700), new IntPoint(0, 1000));
                Assert.True(intersections.Count() == 2);
				foreach (var intersection in intersections)
				{
					Assert.Equal(ClipperLib.Intersection.Colinear, intersection.intersection);
					Assert.Equal(new IntPoint(0, 1000), intersection.position);
				}
            }

            // touching the bottom line
            {
                var intersections = outerLoop[0].GetIntersections(new IntPoint(0, -10), new IntPoint(0, 0));
                Assert.True(intersections.Count() == 1);
                Assert.Equal(2, intersections.First().pointIndex);
                Assert.Equal(ClipperLib.Intersection.Colinear, intersections.First().intersection);
                Assert.Equal(new IntPoint(0, 0), intersections.First().position);
            }
        }

        [Test]
        public void EnsureCorrectStitchOrder()
		{
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

            // head, move, created polygon
            // [1,1] 0, starting points 0-1, 1-3 (outside to inside)
            // [1,0] 1,    [0-1, 1-0, 1-1] 
            // [2,0] 0,    [0-1, 0-2, 1-0] - [0-1, 1-0, 1-3] polygon crosses a line [1-0, 1-3]
            // [2,3] 1,    [0-2, 1-3, 1-0]
            // [0,3] 0,    [0-2, 0-0, 1-0]
            // [0,2] 1,    [0-0, 1-2, 1-3]
            // [1,2] 0,    [0-0, 0-1, 1-2]
            // [1,1] 1,    [0-1, 1-1, 1-2]
            // back to start, done

            var outerLoop = PolygonsExtensions.CreateFromString("x:1000, y:0,x:0, y:1000,x:-1000, y:0,|");
            var innerLoop = PolygonsExtensions.CreateFromString("x:4000, y:500,x:0, y:750,x:-400, y:500,x:0, y:250,|");

			var (outerStart, innerStart) = PathStitcher.BestStartIndices(outerLoop[0], innerLoop[0]);

			Assert.Equal(1, outerStart);
			Assert.Equal(1, innerStart);

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
                Assert.Equal(data.polyIndex, PathStitcher.GetPolygonToAdvance(outerLoop[0], data.outerIndex, innerLoop[0], data.innerIndex));//, "Validate Advance");
            }
        }

        [Test]
		public void FaceCutWoundCorrectly()
		{
			var vertices = new List<Vector3Float>()
			{
				new Vector3Float(new Vector2(1, 0).GetRotated(MathHelper.Tau / 3 * 0), 0),
				new Vector3Float(new Vector2(1, 0).GetRotated(MathHelper.Tau / 3 * 1), 0),
				new Vector3Float(new Vector2(1, 0).GetRotated(MathHelper.Tau / 3 * 2), 0),
			};

			var face = new Face(0, 1, 2, new Vector3Float(0, 0, 1));

			void CheckAngle(double angle, double distance)
			{
				var normal = new Vector3(new Vector2(1, 0).GetRotated(angle), 0);
				face.GetCutLine(vertices,
					new Plane(normal, distance),
					out Vector3 start,
					out Vector3 end);

				var direction = end - start;
				var yDirection = new Vector2(direction.X, direction.Y).GetRotated(-angle);
				Assert.True(yDirection.Y < 0);
			}

			CheckAngle(MathHelper.Tau / 3 * 0, .5);
			CheckAngle(MathHelper.Tau / 3 * 1, .5);
			CheckAngle(MathHelper.Tau / 3 * 2, .5);
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
		public void CutsRespectWindingOrder()
		{
			var cube = PlatonicSolids.CreateCube(10, 10, 10);
			cube.Translate(0, 0, 5); // move bottom to z=0
			var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 5));

			// StlProcessing.Save(cube, "c:\\temp\\cube.stl", CancellationToken.None, new MeshOutputSettings() { OutputTypeSetting = MeshOutputSettings.OutputType.Ascii });

			void CheckFace(int faceIndex)
			{
				var face = cube.Faces[faceIndex];
				if (face.normal.Z == 0)
				{
					Vector3 start, end;
					Assert.True(face.GetCutLine(cube.Vertices, cutPlane, out start, out end));
					if (face.normal.X < 0)
					{
						Assert.True(start.Y > end.Y);
					}
					else if (face.normal.Y < 0)
					{
						Assert.True(start.X < end.X);
					}
					else if (face.normal.X > 0)
					{
						Assert.True(start.Y < end.Y);
					}
					else if (face.normal.Y > 0)
					{
						Assert.True(start.X > end.X);
					}
				}
			}

			for (var faceIndex = 0; faceIndex < cube.Faces.Count; faceIndex++)
			{
				CheckFace(faceIndex);
			}
		}

		[Test]
		public void GetSliceLoop()
		{
			{
				var tetrahedron = PlatonicSolids.CreateTetrahedron(10);
				tetrahedron.Translate(new Vector3(0, 0, -tetrahedron.GetAxisAlignedBoundingBox().MinXYZ.Z));
				var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 3));
				var slice = SliceLayer.CreateSlice(tetrahedron, cutPlane);
				Assert.Single(slice);
				Assert.Equal(3, slice[0].Count);
			}

			{
				var cube = PlatonicSolids.CreateCube(10, 10, 10);
				var cutPlane = new Plane(Vector3.UnitX, new Vector3(3, 0, 0));
				var slice = SliceLayer.CreateSlice(cube, cutPlane);
				Assert.Single(slice);
				Assert.Equal(4, slice[0].Count);
			}

			{
				var cube = PlatonicSolids.CreateCube(10, 10, 10);
				cube.Translate(0, 0, 5); // move bottom to z=0
				var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 5));
				var unorderedSegments = SliceLayer.GetUnorderdSegments(cube, cutPlane);
				Assert.Equal(8, unorderedSegments.Count);
				var fastLookups = SliceLayer.CreateFastIndexLookup(unorderedSegments);
				Assert.Equal(8, fastLookups.Count);
				var closedLoops = SliceLayer.FindClosedPolygons(unorderedSegments);
				Assert.Single(closedLoops);
			}

			{
				var cube = PlatonicSolids.CreateCube(10, 10, 10);
				cube.Translate(0, 0, 5); // move bottom to z=0
				var cutPlane = new Plane(Vector3.UnitZ, new Vector3(0, 0, 5));
				var unorderedSegments = SliceLayer.GetUnorderdSegments(cube, cutPlane);
				Assert.Equal(8, unorderedSegments.Count);
				var fastLookups = SliceLayer.CreateFastIndexLookup(unorderedSegments);
				Assert.Equal(8, fastLookups.Count);
				var closedLoops = SliceLayer.FindClosedPolygons(unorderedSegments);
				Assert.Single(closedLoops);
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
				Assert.Equal(16, unorderedSegments.Count);
				var fastLookups = SliceLayer.CreateFastIndexLookup(unorderedSegments);
				Assert.Equal(16, fastLookups.Count);//, "There should be two loops of 8 segments that all have unique starts");
                var closedLoops = SliceLayer.FindClosedPolygons(unorderedSegments);
				Assert.Equal(2, closedLoops.Count);
				var union = SliceLayer.UnionClosedPolygons(closedLoops);
				Assert.Single(union);
			}
		}

		[Test]
		public void SingleLoopStiching()
		{
			return;
			// only a CCW bottom
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 100,0, 100,100, 0,100");
				var top = PolygonsExtensions.CreateFromString("");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a bottom face, no walls
				Assert.Equal(2, mesh.Faces.Count);
				foreach (var vertex in mesh.Vertices)
				{
					Assert.Equal(0, vertex.Z);
				}
			}

			// only a CCW top
			{
				var bottom = PolygonsExtensions.CreateFromString("");
				var top = PolygonsExtensions.CreateFromString("0,0, 100,0, 100,100, 0,100");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a top face, no walls
				// only a bottom face, no walls
				Assert.Equal(2, mesh.Faces.Count);
				foreach (var vertex in mesh.Vertices)
				{
					Assert.Equal(10, vertex.Z);
				}
			}

			// a simple skirt wound ccw
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 100,0, 100,100, 0,100");
				var top = PolygonsExtensions.CreateFromString("0,0 ,100,0, 100,100, 0,100");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				Assert.Equal(8, mesh.Faces.Count);
			}

			// a simple skirt wound CW (error condition)
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var top = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				Assert.Empty(mesh.Faces);
			}

			// only a CW bottom (error condition)
			{
				var bottom = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var top = PolygonsExtensions.CreateFromString("");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a bottom face, no walls
				Assert.Empty(mesh.Faces);
			}

			// only a CW top (error condition)
			{
				var bottom = PolygonsExtensions.CreateFromString("");
				var top = PolygonsExtensions.CreateFromString("0,0, 0,100, 100,100, 100,0");
				var mesh = PathStitcher.Stitch(bottom, 0, top, 10);
				// only a top face, no walls
				// only a bottom face, no walls
				Assert.Empty(mesh.Faces);
			}
		}

		public void DetectAndRemoveTJunctions()
		{
			// throw new NotImplementedException();
		}

		[Test]
		public void SplitFaceEdgeEdge()
		{
			void TestPositions(int p0, int p1, int p2)
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
				Assert.Single(mesh.Faces);
				Assert.Equal(3, mesh.Vertices.Count);

				// we find a split
				Assert.True(mesh.SplitFace(0, new Plane(new Vector3(1, 0, 0), 5)));
				// we now have 2 faces
				Assert.Equal(2, mesh.Faces.Count);
				// we now have 5 verts
				// the have all the expected x values
				Assert.Equal(4, mesh.Vertices.Count);
				Assert.Single(mesh.Vertices.Where(v => v.X == 0));
				Assert.Equal(2, mesh.Vertices.Where(v => v.X == 5).Count());
				Assert.Single(mesh.Vertices.Where(v => v.X == 10));
				// no face crosses the split line
				Assert.Equal(2, mesh.Faces.Where(f =>
				{
					// all face vertices are less than the split line or greater than the split line
					return (mesh.Vertices[f.v0].X <= 5 && mesh.Vertices[f.v1].X <= 5 && mesh.Vertices[f.v2].X <= 5)
						|| (mesh.Vertices[f.v0].X >= 5 && mesh.Vertices[f.v1].X >= 5 && mesh.Vertices[f.v2].X >= 5);
				}).Count());
			}

			// test every vertex orientation
			TestPositions(0, 1, 2);
			TestPositions(0, 2, 1);
			TestPositions(1, 0, 2);
			TestPositions(1, 2, 0);
			TestPositions(2, 0, 1);
			TestPositions(2, 1, 0);
		}

		[Test]
		public void SplitFaceTwoEdges()
		{
			void TestPositions(int p0, int p1, int p2)
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
				Assert.Single(mesh.Faces);
				Assert.Equal(3, mesh.Vertices.Count);

				// we find a split
				Assert.True(mesh.SplitFace(0, new Plane(new Vector3(1, 0, 0), 5)));
				// we now have 3 faces
				Assert.Equal(3, mesh.Faces.Count);
				// we now have 5 verts
				// the have all the expected x values
				Assert.Equal(5, mesh.Vertices.Count);
				Assert.Equal(2, mesh.Vertices.Where(v => v.X == 0).Count());
				Assert.Equal(2, mesh.Vertices.Where(v => v.X == 5).Count());
				Assert.Single(mesh.Vertices.Where(v => v.X == 10));
				// no face crosses the split line
				Assert.Equal(3, mesh.Faces.Where(f =>
				{
					// all face vertices are less than the split line or greater than the split line
					return (mesh.Vertices[f.v0].X <= 5 && mesh.Vertices[f.v1].X <= 5 && mesh.Vertices[f.v2].X <= 5)
						|| (mesh.Vertices[f.v0].X >= 5 && mesh.Vertices[f.v1].X >= 5 && mesh.Vertices[f.v2].X >= 5);
				}).Count());
			}

			// test every vertex orientation
			TestPositions(0, 1, 2);
			TestPositions(0, 2, 1);
			TestPositions(1, 0, 2);
			TestPositions(1, 2, 0);
			TestPositions(2, 0, 1);
			TestPositions(2, 1, 0);
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

				Assert.True(root.Index == 1);
				Assert.True(root.BackNode.Index == 0);
				Assert.True(root.BackNode.BackNode.Index == 2);

				var renderOredrList = FaceBspTree.GetFacesInVisibiltyOrder(testMesh, root, Matrix4X4.Identity, Matrix4X4.Identity).ToList();
				Assert.True(renderOredrList[0] == 1);
				Assert.True(renderOredrList[1] == 0);
				Assert.True(renderOredrList[2] == 2);
			}
		}

		[Test]
		public void CreateDualContouringCube()
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

				Assert.Equal(12, mesh.Faces.Count);
				Assert.Equal(8, mesh.Vertices.Count);

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
					Assert.True((expected - actual).Length < 1e-6);
				}
			}
		}
	}
}