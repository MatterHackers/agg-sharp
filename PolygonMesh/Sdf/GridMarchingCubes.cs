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

namespace MatterHackers.PolygonMesh.Sdf
{
	/// <summary>
	/// Marching cubes straight over a sampled distance field, for a drag preview's per-step budget.
	/// </summary>
	/// <remarks>
	/// g3's <see cref="MarchingCubes"/> samples an implicit at every corner of every cell and
	/// dedupes vertices through one locked dictionary, which measured 100-200 ms a step on a 128
	/// grid. Here the corners are the grid samples themselves. Every cell is tested: skipping cells
	/// whose corner is more than a diagonal from the iso assumes the field changes no faster than
	/// distance, and it does not across an open hole (a voxel under the hole reads -6 beside one
	/// above it at +6), where the skip dropped the crossing and left the surface torn.
	/// Vertices are keyed by the grid edge they sit on, so neighbouring cells share them without
	/// a lock and the result is closed. The key map is per slab of cells - the two node slices it
	/// spans, reused by each worker thread - so a step allocates no grid-sized array.
	/// Uses g3's tables and corner order, so the winding matches g3's (outward for a field
	/// negative inside).
	/// </remarks>
	internal static class GridMarchingCubes
	{
		// g3 corner order: offsets of the eight corners from the cell's lowest node.
		private static readonly int[,] CornerOffset =
		{
			{ 0, 0, 0 }, { 1, 0, 0 }, { 1, 0, 1 }, { 0, 0, 1 }, { 0, 1, 0 }, { 1, 1, 0 }, { 1, 1, 1 }, { 0, 1, 1 },
		};

		public static Mesh Extract(DenseGrid3f grid, Vector3d origin, double cellSize, double iso, CancellationToken cancellationToken)
		{
			int ni = grid.ni, nj = grid.nj, nk = grid.nk;
			float[] values = grid.Buffer;
			int sliceSize = ni * nj;
			float isoF = (float)iso;
			var options = new ParallelOptions { CancellationToken = cancellationToken };

			// Pass 1: every grid edge that crosses the iso gets a vertex, gathered per node slice
			// and keyed within its slice as (node in slice) * 3 + axis.
			var sliceVertices = new List<double>[nk];
			var sliceEdges = new List<int>[nk];
			Parallel.For(0, nk, options, k =>
			{
				var positions = new List<double>();
				var edges = new List<int>();
				for (int j = 0; j < nj; j++)
				{
					for (int i = 0; i < ni; i++)
					{
						int inSlice = (j * ni) + i;
						int node = (k * sliceSize) + inSlice;
						float f = values[node] - isoF;
						AddCrossing(i + 1 < ni, node + 1, 0);
						AddCrossing(j + 1 < nj, node + ni, 1);
						AddCrossing(k + 1 < nk, node + sliceSize, 2);

						void AddCrossing(bool exists, int other, int axis)
						{
							if (!exists)
							{
								return;
							}

							float g = values[other] - isoF;
							if ((f < 0) == (g < 0))
							{
								return;
							}

							// A node exactly on the iso would give t = 0 (or 1), and each of its
							// crossing edges a vertex at the same point, so the triangles between
							// them would have no area - wrong in a final result such as Hollow
							// Out's cavity. Keeping t a thousandth of a cell off the node keeps
							// those vertices apart, so every triangle has area, and moves the
							// surface by no more than that; the topology is untouched, so the mesh
							// stays closed and manifold. Welding them instead could pinch it.
							double t = Math.Clamp(f / (double)(f - g), 1e-3, 1 - 1e-3);
							edges.Add((inSlice * 3) + axis);
							positions.Add(origin.x + ((i + (axis == 0 ? t : 0)) * cellSize));
							positions.Add(origin.y + ((j + (axis == 1 ? t : 0)) * cellSize));
							positions.Add(origin.z + ((k + (axis == 2 ? t : 0)) * cellSize));
						}
					}
				}

				sliceVertices[k] = positions;
				sliceEdges[k] = edges;
			});

			var sliceBase = new int[nk];
			int vertexCount = 0;
			for (int k = 0; k < nk; k++)
			{
				sliceBase[k] = vertexCount;
				vertexCount += sliceEdges[k].Count;
			}

			var vertices = new double[vertexCount * 3];
			for (int k = 0; k < nk; k++)
			{
				sliceVertices[k].CopyTo(vertices, sliceBase[k] * 3);
			}

			// For each of the 12 cell edges, its key in a slab's two-slice map, relative to the
			// cell's lowest node: the upper slice's keys follow the lower's.
			var edgeOffset = new int[12];
			for (int e = 0; e < 12; e++)
			{
				int a = MarchingCubes.edge_indices[e, 0], b = MarchingCubes.edge_indices[e, 1];
				int low = CornerOffset[a, 0] + CornerOffset[a, 1] + CornerOffset[a, 2] <= CornerOffset[b, 0] + CornerOffset[b, 1] + CornerOffset[b, 2] ? a : b;
				int axis = CornerOffset[a, 0] != CornerOffset[b, 0] ? 0 : (CornerOffset[a, 1] != CornerOffset[b, 1] ? 1 : 2);
				edgeOffset[e] = ((CornerOffset[low, 0] + (CornerOffset[low, 1] * ni) + (CornerOffset[low, 2] * sliceSize)) * 3) + axis;
			}

			var cornerOffset = new int[8];
			for (int c = 0; c < 8; c++)
			{
				cornerOffset[c] = CornerOffset[c, 0] + (CornerOffset[c, 1] * ni) + (CornerOffset[c, 2] * sliceSize);
			}

			// Pass 2: triangles, per slab of cells between node slices k and k + 1. The map is
			// never cleared: a cell only reads the keys of edges that cross, and those were just
			// written from this slab's two slices.
			var slabTriangles = new List<int>[nk];
			Parallel.For(
				0,
				nk - 1,
				options,
				() => new int[2 * 3 * sliceSize],
				(k, state, edgeVertex) =>
				{
					for (int half = 0; half < 2; half++)
					{
						var edges = sliceEdges[k + half];
						int first = sliceBase[k + half];
						int shift = half * 3 * sliceSize;
						for (int n = 0; n < edges.Count; n++)
						{
							edgeVertex[shift + edges[n]] = first + n;
						}
					}

					var triangles = new List<int>();
					for (int j = 0; j < nj - 1; j++)
					{
						for (int i = 0; i < ni - 1; i++)
						{
							int inSlice = (j * ni) + i;
							int node = (k * sliceSize) + inSlice;
							int cubeIndex = 0;
							for (int c = 0; c < 8; c++)
							{
								if (values[node + cornerOffset[c]] < isoF)
								{
									cubeIndex |= 1 << c;
								}
							}

							if (MarchingCubes.edgeTable[cubeIndex] == 0)
							{
								continue;
							}

							int key = inSlice * 3;
							for (int t = 0; MarchingCubes.triTable[cubeIndex, t] != -1; t += 3)
							{
								triangles.Add(edgeVertex[key + edgeOffset[MarchingCubes.triTable[cubeIndex, t]]]);
								triangles.Add(edgeVertex[key + edgeOffset[MarchingCubes.triTable[cubeIndex, t + 1]]]);
								triangles.Add(edgeVertex[key + edgeOffset[MarchingCubes.triTable[cubeIndex, t + 2]]]);
							}
						}
					}

					slabTriangles[k] = triangles;
					return edgeVertex;
				},
				_ => { });

			var faces = new List<int>();
			for (int k = 0; k < nk - 1; k++)
			{
				faces.AddRange(slabTriangles[k]);
			}

			return new Mesh(vertices, faces.ToArray());
		}
	}
}
