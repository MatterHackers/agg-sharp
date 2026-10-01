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
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Sdf;
using MatterHackers.VectorMath;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The go/no-go numbers for the SDF drag preview against the exact Dilate. Explicit: run it in
	/// Release with --treenode-filter "/*/*/MeshSdfGridBenchmark/*"; it prints a table.
	/// </summary>
	/// <remarks>
	/// dVol is against the kernel's default ball (segments 0), which is coarse at a small radius:
	/// on 37283 (r 0.41) it gave 610.9 mm3 against 630.4 with a 64-segment ball, and the preview
	/// read 629.3 / 629.9 / 630.3 / 630.4 at 96 / 128 / 192 / 256 voxels - so that part's 3% is
	/// the reference's facets, not the grid or the sign.
	/// A 12-triangle cube builds slower than a 4k part (~145 ms warm, ~360 cold): g3's flood fill
	/// seeds one voxel per vertex and grows the band a sequential pass per layer, and from 8
	/// corners that is many passes. Splitting long edges to seed more measured far slower.
	/// </remarks>
	public class MeshSdfGridBenchmark
	{
		// A Thingi10K meshes folder (the one holding <id>.stl.zip), e.g.
		// .../Thingi10K/meshes/Thingi10K-meshes-1/meshes.
		private static readonly string ThingiRoot = Environment.GetEnvironmentVariable("THINGI10K_ROOT");

		[Test]
		[Explicit]
		public Task PreviewAgainstExactDilate()
		{
			if (string.IsNullOrEmpty(ThingiRoot))
			{
				Skip.Test("Set THINGI10K_ROOT to a Thingi10K meshes folder to run the SDF preview benchmark.");
			}

			var cube = PlatonicSolids.CreateCube(20, 20, 20);
			var armA = PlatonicSolids.CreateCube(30, 10, 10);
			var armB = PlatonicSolids.CreateCube(10, 30, 10);
			armB.Transform(Matrix4X4.CreateTranslation(-10, 10, 0));
			var lPrism = CsgOperations.Union(armA, armB);

			// Twice: the first run pays the JIT for everything.
			Report("cube 20 (cold)", cube);
			Report("cube 20", cube);
			Report("L prism", lPrism);
			// Thingi10K parts of about 4k and 50k triangles, the first of each list the kernel
			// accepts as closed (counted after merging, which drops duplicates).
			ReportFirstClosed(new[] { "37221", "37283", "37304", "37746", "37747", "37748", "37766", "37887", "37964", "38290", "39166", "39929", "39930", "39950", "40118", "40631", "42629", "43385", "43394", "43582", "44900", "45408", "45512", "46262", "47568", "47877", "49163", "49315", "51808", "53126" }, 3500, 4500);
			Report("thingi 37283", ReadZippedStl(Path.Combine(ThingiRoot, "37283.stl.zip")));
			ReportFirstClosed(new[] { "38559", "39381", "39498", "39879" }, 45000, 55000);

			return Task.CompletedTask;
		}

		private static void ReportFirstClosed(string[] ids, int minTriangles, int maxTriangles)
		{
			foreach (var id in ids)
			{
				string path = Path.Combine(ThingiRoot, id + ".stl.zip");
				try
				{
					var part = ReadZippedStl(path);
					if (part.Faces.Count < minTriangles || part.Faces.Count > maxTriangles)
					{
						continue;
					}

					Report("thingi " + id, part);
					return;
				}
				catch (Exception ex) when (ex is InvalidOperationException || ex is ArgumentException)
				{
					Console.WriteLine($"thingi {id} skipped: {ex.Message}");
				}
			}
		}

		private static void Report(string name, Mesh part)
		{
			var bounds = part.GetAxisAlignedBoundingBox();
			double radius = 0.02 * (bounds.MaxXYZ - bounds.MinXYZ).Length;

			var clock = Stopwatch.StartNew();
			var exact = MinkowskiProcessing.MinkowskiSum(part, MinkowskiProcessing.SphereMesh(radius, 0));
			double exactMs = clock.Elapsed.TotalMilliseconds;
			double exactVolume = MeshSdfGridTests.SignedVolume(exact);

			string line = $"{name}: tris {part.Faces.Count}, r {radius:0.###}, exact {exactMs:0} ms";
			foreach (int resolution in new[] { 96, 128 })
			{
				var sdf = new MeshSdfGrid(part, radius * 1.5, resolution);
				clock.Restart();
				sdf.Build();
				double buildMs = clock.Elapsed.TotalMilliseconds;

				clock.Restart();
				var preview = sdf.Extract(radius);
				double firstMs = clock.Elapsed.TotalMilliseconds;
				clock.Restart();
				const int steps = 5;
				for (int step = 1; step <= steps; step++)
				{
					sdf.Extract(radius * (1 - (0.05 * step)));
				}

				double stepMs = clock.Elapsed.TotalMilliseconds / steps;
				double diff = (MeshSdfGridTests.SignedVolume(preview) - exactVolume) / exactVolume;
				line += $" | N{resolution}: build {buildMs:0} ms, extract {firstMs:0} ms, step {stepMs:0} ms, dVol {100 * diff:0.00}%";
			}

			Console.WriteLine(line);
		}

		private static Mesh ReadZippedStl(string path)
		{
			using var zip = ZipFile.OpenRead(path);
			using var stream = zip.Entries[0].Open();
			using var memory = new MemoryStream();
			stream.CopyTo(memory);
			var bytes = memory.ToArray();
			var mesh = new Mesh();

			// Binary STL: an 80 byte header, a count, then 50 bytes per triangle; anything else is
			// read as ASCII, one "vertex x y z" line per corner.
			int count = bytes.Length >= 84 ? BitConverter.ToInt32(bytes, 80) : -1;
			if (84 + (50 * (long)count) != bytes.Length)
			{
				var corners = new List<Vector3>();
				foreach (var raw in System.Text.Encoding.ASCII.GetString(bytes).Split('\n'))
				{
					var parts = raw.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
					if (parts.Length == 4 && parts[0] == "vertex")
					{
						corners.Add(new Vector3(
							double.Parse(parts[1], CultureInfo.InvariantCulture),
							double.Parse(parts[2], CultureInfo.InvariantCulture),
							double.Parse(parts[3], CultureInfo.InvariantCulture)));
						if (corners.Count == 3)
						{
							mesh.CreateFace(corners.ToArray());
							corners.Clear();
						}
					}
				}

				mesh.CleanAndMerge();
				return mesh;
			}

			for (int t = 0; t < count; t++)
			{
				int at = 84 + (50 * t) + 12;
				var corners = new Vector3[3];
				for (int v = 0; v < 3; v++)
				{
					corners[v] = new Vector3(
						BitConverter.ToSingle(bytes, at + (12 * v)),
						BitConverter.ToSingle(bytes, at + (12 * v) + 4),
						BitConverter.ToSingle(bytes, at + (12 * v) + 8));
				}

				mesh.CreateFace(corners);
			}

			mesh.CleanAndMerge();
			return mesh;
		}
	}
}
