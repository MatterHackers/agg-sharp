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
*/

using System;
using MatterHackers.RenderCore;
using MatterHackers.WebGpu;
using System;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using g3;
using MatterHackers.Agg.Tests.TestingInfrastructure;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.PolygonMesh.Sdf;
using MatterHackers.RenderCore.Testing;
using MatterHackers.VectorMath;
using MatterHackers.WebGpuRender;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>The GPU distance grid against MeshSdfGrid's CPU flood, on a real device.</summary>
	[NotInParallel]
	public class GpuMeshSdfTests
	{
		[Test]
		public async Task GpuGridMatchesCpuGrid()
		{
			using (GpuTestGate.Acquire(nameof(GpuMeshSdfTests)))
			using (var device = CreateDevice())
			using (var gpu = new GpuMeshSdf(device))
			{
				foreach (var (name, part) in new[] { ("cube", PlatonicSolids.CreateCube(20, 20, 20)), ("torus", Torus(40, 20)), ("L", LPrism()) })
				{
					var (maxDiff, signMismatch, cell) = Compare(part, 2, 64, gpu);
					Console.WriteLine($"{name}: max |gpu - cpu| {maxDiff:E2} (cell {cell:0.###}, {maxDiff / cell:E2} cells), sign mismatches {signMismatch}");
					await Assert.That(signMismatch).IsEqualTo(0);
					await Assert.That(maxDiff).IsLessThanOrEqualTo((1e-4 * cell) + 1e-5);
				}

				// Phil A Ment (172k triangles) when PHIL_STL points at it; MatterCAD ships it, agg does not.
				string phil = Environment.GetEnvironmentVariable("PHIL_STL");
				if (!string.IsNullOrEmpty(phil))
				{
					var (maxDiff, signMismatch, cell) = Compare(ReadBinaryStl(phil), 2, 128, gpu);
					Console.WriteLine($"Phil: max |gpu - cpu| {maxDiff:E2} (cell {cell:0.###}), sign mismatches {signMismatch}");
					await Assert.That(signMismatch).IsEqualTo(0);
					await Assert.That(maxDiff).IsLessThanOrEqualTo((1e-4 * cell) + 1e-5);
				}

				await Assert.That(device.LastUncapturedError).IsNull();
			}
		}

		[Test]
		[Explicit]
		public Task GpuAgainstCpuTimings()
		{
			string phil = Environment.GetEnvironmentVariable("PHIL_STL");
			using (GpuTestGate.Acquire(nameof(GpuMeshSdfTests)))
			using (var device = CreateDevice())
			using (var gpu = new GpuMeshSdf(device))
			{
				// Warm both paths (JIT, pipeline) before timing.
				new MeshSdfGrid(Torus(40, 20), 2, 64).Build();
				new MeshSdfGrid(Torus(40, 20), 2, 64, gpu.ComputeDistances).Build();
				foreach (var (name, part) in new[] { ("torus 1600", Torus(40, 20)), ("Phil", string.IsNullOrEmpty(phil) ? null : ReadBinaryStl(phil)) })
				{
					if (part == null)
					{
						continue;
					}

					foreach (int resolution in new[] { 128, 256 })
					{
						// The median of three builds of each: a shared machine swings wall time ~2x.
						var cpuRuns = new double[3];
						var gpuRuns = new double[3];
						MeshSdfGrid cpu = null, onGpu = null;
						for (int run = 0; run < 3; run++)
						{
							cpu = new MeshSdfGrid(part, 2, resolution);
							var clock = Stopwatch.StartNew();
							cpu.Build();
							cpuRuns[run] = clock.Elapsed.TotalMilliseconds;
							onGpu = new MeshSdfGrid(part, 2, resolution, gpu.ComputeDistances);
							clock.Restart();
							onGpu.Build();
							gpuRuns[run] = clock.Elapsed.TotalMilliseconds;
						}

						Array.Sort(cpuRuns);
						Array.Sort(gpuRuns);
						var t = gpu.LastTimings;
						double wallCpu = MinWall(part, cpu.Extract(-2));
						double wallGpu = MinWall(part, onGpu.Extract(-2));
						Console.WriteLine($"{name} tris {part.Faces.Count} N{resolution}: cpu {cpuRuns[1]:0} ms | gpu {gpuRuns[1]:0} ms (last: bucket {t.Bucket:0}, upload {t.Upload:0}, dispatch {t.Dispatch:0}, readback {t.Readback:0}, entries {t.Entries:N0}) | {cpuRuns[1] / gpuRuns[1]:0.00}x | repairs {cpu.OrientRepairs} | hollow 2 min wall cpu {wallCpu:0.0000} gpu {wallGpu:0.0000}");
					}
				}
			}

			return Task.CompletedTask;
		}

		private static (double MaxDiff, int SignMismatch, double Cell) Compare(Mesh part, double radius, int resolution, GpuMeshSdf gpu)
		{
			var cpu = new MeshSdfGrid(part, radius, resolution);
			cpu.Build();
			var onGpu = new MeshSdfGrid(part, radius, resolution, gpu.ComputeDistances);
			onGpu.Build();
			if (cpu.Field.size != onGpu.Field.size || cpu.FieldOrigin != onGpu.FieldOrigin)
			{
				throw new InvalidOperationException("The GPU grid's layout differs from the CPU grid's.");
			}

			double band = radius + (2 * cpu.CellSize);
			double maxDiff = 0;
			int signMismatch = 0;
			(float Cpu, float Gpu) worst = default;
			for (int i = 0; i < cpu.Field.size; i++)
			{
				float a = cpu.Field.Buffer[i], b = onGpu.Field.Buffer[i];
				if (Math.Abs(a) < band && Math.Abs(b) < band)
				{
					if (Math.Abs(a - b) > maxDiff)
					{
						maxDiff = Math.Abs(a - b);
						worst = (a, b);
					}
				}

				// Samples on the surface read 0 on the CPU and f32 noise on the GPU; their sign carries nothing.
				if (Math.Sign(a) != Math.Sign(b) && Math.Abs(a) > 1e-5 && Math.Abs(b) > 1e-5)
				{
					if (signMismatch++ == 0)
					{
						Console.WriteLine($"first sign mismatch at {i}: cpu {a} gpu {b}");
					}
				}
			}

			return (maxDiff, signMismatch, cpu.CellSize);
		}

		/// <summary>The least distance from the hollow's inner surface to the part: its thinnest wall.</summary>
		private static double MinWall(Mesh part, Mesh inner)
		{
			var dmesh = part.ToDMesh3();
			var tree = new DMeshAABBTree3(dmesh, autoBuild: true);
			double min = double.MaxValue;
			foreach (var v in inner.Vertices)
			{
				var p = new Vector3d(v.X, v.Y, v.Z);
				int tid = tree.FindNearestTriangle(p);
				var tri = new Triangle3d();
				dmesh.GetTriVertices(tid, ref tri.V0, ref tri.V1, ref tri.V2);
				min = Math.Min(min, Math.Sqrt(DistPoint3Triangle3.DistanceSqr(ref p, ref tri, out _, out _)));
			}

			return min;
		}

		private static Mesh Torus(int around, int tube)
		{
			const double R = 15, r = 5;
			var mesh = new Mesh();
			Vector3 At(int u, int v)
			{
				double a = 2 * Math.PI * u / around, b = 2 * Math.PI * v / tube;
				return new Vector3((R + (r * Math.Cos(b))) * Math.Cos(a), (R + (r * Math.Cos(b))) * Math.Sin(a), r * Math.Sin(b));
			}

			for (int u = 0; u < around; u++)
			{
				for (int v = 0; v < tube; v++)
				{
					mesh.CreateFace(new[] { At(u, v), At(u + 1, v), At(u + 1, v + 1) });
					mesh.CreateFace(new[] { At(u, v), At(u + 1, v + 1), At(u, v + 1) });
				}
			}

			mesh.CleanAndMerge();
			return mesh;
		}

		private static Mesh LPrism()
		{
			var armA = PlatonicSolids.CreateCube(30, 10, 10);
			var armB = PlatonicSolids.CreateCube(10, 30, 10);
			armB.Transform(Matrix4X4.CreateTranslation(-10, 10, 0));
			return CsgOperations.Union(armA, armB);
		}

		private static Mesh ReadBinaryStl(string path)
		{
			var bytes = File.ReadAllBytes(path);
			int count = BitConverter.ToInt32(bytes, 80);
			var mesh = new Mesh();
			for (int t = 0; t < count; t++)
			{
				int at = 84 + (50 * t) + 12;
				var corners = new Vector3[3];
				for (int v = 0; v < 3; v++)
				{
					corners[v] = new Vector3(BitConverter.ToSingle(bytes, at + (12 * v)), BitConverter.ToSingle(bytes, at + (12 * v) + 4), BitConverter.ToSingle(bytes, at + (12 * v) + 8));
				}

				mesh.CreateFace(corners);
			}

			mesh.CleanAndMerge();
			return mesh;
		}

		private static WebGpuRenderDevice CreateDevice()
			=> new WebGpuRenderDevice(false, TestRenderBackend.Native, nameof(GpuMeshSdfTests), raiseComputeLimits: true);
	}
}
