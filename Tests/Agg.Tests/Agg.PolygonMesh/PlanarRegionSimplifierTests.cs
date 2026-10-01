/*
Copyright (c) 2026, Lars Brubaker
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
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using g3;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.PolygonMesh.Sdf;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The distance-field Dilate / Erode surface of a mechanical part is mostly grid-sized
	/// triangles on flat walls; merging them must shed most triangles and leave the surface,
	/// its closure and its volume exactly as they were.
	/// </summary>
	public class PlanarRegionSimplifierTests
	{
		[Test]
		[Arguments(1.0)]
		[Arguments(-1.0)]
		public async Task BlockWithHoleAndNotch(double iso)
		{
			var outline = new VertexStorage();
			outline.MoveTo(0, 0);
			outline.LineTo(12, 0);
			outline.LineTo(12, 5);
			outline.LineTo(18, 5);
			outline.LineTo(18, 0);
			outline.LineTo(30, 0);
			outline.LineTo(30, 20);
			outline.LineTo(0, 20);
			outline.ClosePolygon();
			AddCircle(outline, new Vector2(8, 11), 4, 48);
			await AssertSimplifies($"block {iso}", outline.Extrude(10), iso, 4);
		}

		[Test]
		public async Task GearLikeExtrusion()
		{
			var outline = new VertexStorage();
			const int Teeth = 12;
			for (int i = 0; i < Teeth * 4; i++)
			{
				double angle = i * Math.PI * 2 / (Teeth * 4);
				double radius = (i % 4) < 2 ? 15 : 12;
				var p = new Vector2(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
				if (i == 0)
				{
					outline.MoveTo(p);
				}
				else
				{
					outline.LineTo(p);
				}
			}

			outline.ClosePolygon();
			AddCircle(outline, Vector2.Zero, 4, 32);
			await AssertSimplifies("gear", outline.Extrude(6), 1, 2);
		}

		[Test]
		public async Task LBracket()
		{
			var outline = new VertexStorage();
			outline.MoveTo(0, 0);
			outline.LineTo(40, 0);
			outline.LineTo(40, 5);
			outline.LineTo(5, 5);
			outline.LineTo(5, 30);
			outline.LineTo(0, 30);
			outline.ClosePolygon();
			await AssertSimplifies("L bracket", outline.Extrude(20), 1, 4);
		}

		[Test]
		public async Task RoundPartIsLeftAlmostAsItWas()
		{
			// A near-point dilated far is a ball: no flat wall to merge.
			var sdf = new MeshSdfGrid(PlatonicSolids.CreateCube(0.01, 0.01, 0.01), 5, 64);
			var source = sdf.Extract(5);
			var simplified = PlanarRegionSimplifier.Simplify(source);
			Console.WriteLine($"sphere: {source.Faces.Count} -> {simplified.Faces.Count}");
			await AssertSameSolid(source, simplified);
			await Assert.That(simplified.Faces.Count).IsGreaterThan(source.Faces.Count * 9 / 10);
		}

		[Test]
		[Explicit]
		public async Task AngleProbe()
		{
			var outline = new VertexStorage();
			outline.MoveTo(0, 0);
			outline.LineTo(30, 0);
			outline.LineTo(30, 20);
			outline.LineTo(0, 20);
			outline.ClosePolygon();
			var source = new MeshSdfGrid(outline.Extrude(10), 1, 64).Extract(-1);
			foreach (double angle in new[] { 1e-4, 1e-3, 1e-2 })
			{
				foreach (double distance in new[] { 1e-6, 1e-5, 1e-4 })
				{
					Console.WriteLine($"probe {angle} {distance}: {source.Faces.Count} -> {PlanarRegionSimplifier.Simplify(source, angle, distance).Faces.Count}");
				}
			}

			await Task.CompletedTask;
		}

		[Test]
		[Explicit]
		public async Task PhilTiming()
		{
			string path = Path.Combine(Environment.GetEnvironmentVariable("HOME") ?? string.Empty, "Development/MatterCAD/StaticData/OEMSettings/SampleParts/Phil A Ment.stl");
			var part = StlProcessing.Load(path, CancellationToken.None);
			var clock = Stopwatch.StartNew();
			var sdf = new MeshSdfGrid(part, 1, MeshSdfGrid.MaxResolution);
			var source = sdf.Extract(1);
			double sdfSeconds = clock.Elapsed.TotalSeconds;
			clock.Restart();
			var simplified = PlanarRegionSimplifier.Simplify(source);
			double simplifySeconds = clock.Elapsed.TotalSeconds;
			Console.WriteLine($"phil: {source.Faces.Count} -> {simplified.Faces.Count}, sdf+extract {sdfSeconds:0.00} s, simplify {simplifySeconds:0.00} s");
			await Assert.That(simplified.IsManifold()).IsTrue();
		}

		private static void AddCircle(VertexStorage outline, Vector2 center, double radius, int sides)
		{
			for (int i = 0; i < sides; i++)
			{
				double angle = i * Math.PI * 2 / sides;
				var p = center + new Vector2(Math.Cos(angle) * radius, Math.Sin(angle) * radius);
				if (i == 0)
				{
					outline.MoveTo(p);
				}
				else
				{
					outline.LineTo(p);
				}
			}

			outline.ClosePolygon();
		}

		private static async Task AssertSimplifies(string name, Mesh part, double iso, int minimumReduction)
		{
			var source = new MeshSdfGrid(part, Math.Abs(iso)).Extract(iso);
			var simplified = PlanarRegionSimplifier.Simplify(source);
			Console.WriteLine($"{name}: {source.Faces.Count} -> {simplified.Faces.Count}");
			await AssertSameSolid(source, simplified);
			await Assert.That(simplified.Faces.Count * minimumReduction).IsLessThan(source.Faces.Count);
		}

		private static async Task AssertSameSolid(Mesh source, Mesh simplified)
		{
			await Assert.That(simplified.IsManifold()).IsTrue();
			await Assert.That(ClosedEdgeErrors(simplified)).IsEqualTo(0);
			double volume = source.GetVolume();
			await Assert.That(simplified.GetVolume()).IsEqualTo(volume).Within(1e-6 * Math.Abs(volume));

			// The kept vertices are the source's own; every dropped one must still lie on the surface.
			var target = simplified.ToDMesh3();
			var tree = new DMeshAABBTree3(target, true);
			double worst = 0;
			foreach (var vertex in source.Vertices)
			{
				var point = new Vector3d(vertex.X, vertex.Y, vertex.Z);
				int triangle = tree.FindNearestTriangle(point);
				worst = Math.Max(worst, Math.Sqrt(MeshQueries.TriDistanceSqr(target, triangle, point)));
			}

			Console.WriteLine($"  worst dropped-vertex distance {worst:E2}");
			await Assert.That(worst).IsLessThanOrEqualTo(1e-5);
		}

		/// <summary>Half-edges without exactly one opposite partner.</summary>
		private static int ClosedEdgeErrors(Mesh mesh)
		{
			var directed = new Dictionary<(int, int), int>();
			foreach (var face in mesh.Faces)
			{
				foreach (var edge in new[] { (face.v0, face.v1), (face.v1, face.v2), (face.v2, face.v0) })
				{
					directed[edge] = directed.GetValueOrDefault(edge) + 1;
				}
			}

			int bad = 0;
			foreach (var (edge, count) in directed)
			{
				if (count != 1 || directed.GetValueOrDefault((edge.Item2, edge.Item1)) != 1)
				{
					bad++;
				}
			}

			return bad;
		}
	}
}
