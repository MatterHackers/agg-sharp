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
using System.Threading;
using System.Threading.Tasks;
using g3;
using MatterHackers.PolygonMesh.Sdf;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// Marching cubes over random fields: whatever the corner values, the surface is closed and
	/// manifold - the guard on the ambiguous-face loops agreeing with the table and with each
	/// other across every shared face.
	/// </summary>
	public class GridMarchingCubesTests
	{
		/// <summary>
		/// Seeded 8x8x8 grids, border outside so every surface closes: smooth random values,
		/// small integers (many nodes on the iso and exact saddle ties), wildly mixed magnitudes,
		/// and a field biased inside. Every directed edge is used once with its reverse present,
		/// no triangle repeats a vertex, and the triangles round each vertex form one fan.
		/// </summary>
		[Test]
		public async Task RandomFieldsGiveClosedManifoldSurfaces()
		{
			const int n = 8;
			var failures = new List<string>();
			for (int mode = 0; mode < 4; mode++)
			{
				for (int seed = 0; seed < 75; seed++)
				{
					var random = new Random((seed * 7) + mode);
					var grid = new DenseGrid3f(n, n, n, 1f);
					for (int k = 1; k < n - 1; k++)
					{
						for (int j = 1; j < n - 1; j++)
						{
							for (int i = 1; i < n - 1; i++)
							{
								grid.Buffer[i + (j * n) + (k * n * n)] = mode switch
								{
									0 => (float)((random.NextDouble() * 2) - 1),
									1 => random.Next(-2, 3),
									2 => (float)(random.NextDouble() < 0.5 ? -1e-7 * random.NextDouble() : 1e3 * random.NextDouble()),
									_ => (float)((random.NextDouble() * 2) - 1.2),
								};
							}
						}
					}

					var mesh = GridMarchingCubes.Extract(grid, Vector3d.Zero, 1.0, 0.0, CancellationToken.None);
					string problem = SurfaceProblem(mesh);
					if (problem != null)
					{
						failures.Add($"mode {mode} seed {seed}: {problem}");
					}
				}
			}

			await Assert.That(string.Join("; ", failures)).IsEqualTo(string.Empty);
		}

		/// <summary>Why the mesh is not a closed, consistently wound manifold, or null.</summary>
		private static string SurfaceProblem(Mesh mesh)
		{
			var directed = new Dictionary<(int, int), int>();
			foreach (var face in mesh.Faces)
			{
				foreach (var (a, b) in new[] { (face.v0, face.v1), (face.v1, face.v2), (face.v2, face.v0) })
				{
					if (a == b)
					{
						return "degenerate triangle";
					}

					directed[(a, b)] = directed.GetValueOrDefault((a, b)) + 1;
				}
			}

			foreach (var (edge, count) in directed)
			{
				if (count != 1)
				{
					return $"directed edge used {count} times";
				}

				if (!directed.ContainsKey((edge.Item2, edge.Item1)))
				{
					return "open or flipped edge";
				}
			}

			// Round each vertex, the edge after a triangle's outgoing edge leads to the next
			// triangle; one fan means one cycle visits them all.
			var fans = new Dictionary<int, Dictionary<int, int>>();
			foreach (var face in mesh.Faces)
			{
				foreach (var (a, b, c) in new[] { (face.v0, face.v1, face.v2), (face.v1, face.v2, face.v0), (face.v2, face.v0, face.v1) })
				{
					if (!fans.TryGetValue(a, out var fan))
					{
						fans[a] = fan = new Dictionary<int, int>();
					}

					fan[b] = c;
				}
			}

			foreach (var (vertex, fan) in fans)
			{
				int start = -1;
				foreach (int key in fan.Keys)
				{
					start = key;
					break;
				}

				int at = start, steps = 0;
				do
				{
					if (!fan.TryGetValue(at, out at))
					{
						return $"vertex {vertex} fan is open";
					}

					steps++;
				}
				while (at != start && steps <= fan.Count);

				if (steps != fan.Count)
				{
					return $"vertex {vertex} has {fan.Count} triangles in more than one fan";
				}
			}

			return null;
		}
	}
}
