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
	///
	/// Ambiguous faces: a cell face whose corners alternate inside / outside can be crossed by its
	/// two contour segments either way, and the 256-case table picks by case number, which cut a
	/// thin ridge running diagonally across the grid (an eroded gear tooth's tip) into floating
	/// one-voxel shells. A cell with such a face is built here from its face segments instead of
	/// the table: every face's segments are chosen by the asymptotic decider - the inside corners
	/// join when the face's bilinear saddle is inside - and chained into loops through the cell.
	/// The choice reads only the shared face's four values, in a form that is deterministic and
	/// symmetric in corner order (the products round, but identically in both cells), so both cells sharing the face draw the same segments, in opposite
	/// directions, and the mesh stays closed and manifold. Faces with no ambiguity have only one
	/// possible segment, which is also the table's, so table cells and loop cells meet cleanly.
	/// A loop of three or four crossings is fanned (a four-loop's diagonal joins crossings on no
	/// common face, so no other cell can draw it); a longer loop gets a centre vertex, since a
	/// fan diagonal could lie in a face and be drawn by the neighbour too.
	/// The cell-interior ambiguity (whether two loops in one cell join through a tunnel) is not
	/// resolved: loops are never joined. That can only lose a sub-cell tunnel, never tear the mesh.
	/// </remarks>
	internal static class GridMarchingCubes
	{
		// g3 corner order: offsets of the eight corners from the cell's lowest node.
		private static readonly int[,] CornerOffset =
		{
			{ 0, 0, 0 }, { 1, 0, 0 }, { 1, 0, 1 }, { 0, 0, 1 }, { 0, 1, 0 }, { 1, 1, 0 }, { 1, 1, 1 }, { 0, 1, 1 },
		};

		// Each face's corners counter-clockwise seen from outside the cell.
		private static readonly int[,] FaceCorners =
		{
			{ 0, 1, 2, 3 }, { 4, 7, 6, 5 }, { 0, 3, 7, 4 }, { 1, 5, 6, 2 }, { 0, 4, 5, 1 }, { 3, 2, 6, 7 },
		};

		// FaceEdges[face, i]: the cell edge from FaceCorners[face, i] to the next corner.
		private static readonly int[,] FaceEdges = BuildFaceEdges();

		// Cases with at least one face whose corners alternate inside / outside.
		private static readonly bool[] HasAmbiguousFace = BuildAmbiguousCases();

		private static int[,] BuildFaceEdges()
		{
			var faceEdges = new int[6, 4];
			for (int face = 0; face < 6; face++)
			{
				for (int i = 0; i < 4; i++)
				{
					int a = FaceCorners[face, i], b = FaceCorners[face, (i + 1) % 4];
					for (int e = 0; e < 12; e++)
					{
						int ea = MarchingCubes.edge_indices[e, 0], eb = MarchingCubes.edge_indices[e, 1];
						if ((ea == a && eb == b) || (ea == b && eb == a))
						{
							faceEdges[face, i] = e;
						}
					}
				}
			}

			return faceEdges;
		}

		private static bool[] BuildAmbiguousCases()
		{
			var ambiguous = new bool[256];
			for (int cubeIndex = 0; cubeIndex < 256; cubeIndex++)
			{
				for (int face = 0; face < 6; face++)
				{
					bool s0 = (cubeIndex & (1 << FaceCorners[face, 0])) != 0, s1 = (cubeIndex & (1 << FaceCorners[face, 1])) != 0;
					bool s2 = (cubeIndex & (1 << FaceCorners[face, 2])) != 0, s3 = (cubeIndex & (1 << FaceCorners[face, 3])) != 0;
					if (s0 == s2 && s1 == s3 && s0 != s1)
					{
						ambiguous[cubeIndex] = true;
					}
				}
			}

			return ambiguous;
		}

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
			var slabCentres = new List<double>[nk];
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
					var centres = new List<double>();
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
							if (HasAmbiguousFace[cubeIndex])
							{
								AddCellLoops(node, key, cubeIndex, edgeVertex, triangles, centres);
								continue;
							}

							for (int t = 0; MarchingCubes.triTable[cubeIndex, t] != -1; t += 3)
							{
								triangles.Add(edgeVertex[key + edgeOffset[MarchingCubes.triTable[cubeIndex, t]]]);
								triangles.Add(edgeVertex[key + edgeOffset[MarchingCubes.triTable[cubeIndex, t + 1]]]);
								triangles.Add(edgeVertex[key + edgeOffset[MarchingCubes.triTable[cubeIndex, t + 2]]]);
							}
						}
					}

					slabTriangles[k] = triangles;
					slabCentres[k] = centres;
					return edgeVertex;
				},
				_ => { });

			// Centre vertices were numbered per slab as -(n + 1); they go after the edge vertices.
			int centreCount = 0;
			for (int k = 0; k < nk - 1; k++)
			{
				centreCount += slabCentres[k].Count / 3;
			}

			if (centreCount > 0)
			{
				Array.Resize(ref vertices, vertices.Length + (centreCount * 3));
			}

			var faces = new List<int>();
			int nextCentre = vertexCount;
			for (int k = 0; k < nk - 1; k++)
			{
				int first = nextCentre;
				slabCentres[k].CopyTo(vertices, first * 3);
				nextCentre += slabCentres[k].Count / 3;
				foreach (int index in slabTriangles[k])
				{
					faces.Add(index >= 0 ? index : first - index - 1);
				}
			}

			return new Mesh(vertices, faces.ToArray());

			// A cell with an ambiguous face: its contour loops chained from per-face segments.
			void AddCellLoops(int node, int key, int cubeIndex, int[] edgeVertex, List<int> triangles, List<double> centres)
			{
				Span<int> next = stackalloc int[12];
				next.Fill(-1);
				for (int face = 0; face < 6; face++)
				{
					// Seen from outside, going round the face: an exit edge runs inside -> outside,
					// an entry edge outside -> inside. A segment runs from an exit to an entry.
					int crossings = 0;
					for (int i = 0; i < 4; i++)
					{
						bool here = (cubeIndex & (1 << FaceCorners[face, i])) != 0;
						bool there = (cubeIndex & (1 << FaceCorners[face, (i + 1) % 4])) != 0;
						if (here != there)
						{
							crossings++;
						}
					}

					if (crossings == 0)
					{
						continue;
					}

					bool join = false;
					if (crossings == 4)
					{
						// Asymptotic decider: the bilinear saddle (ac - bd) / (a + c - b - d) is inside
						// (negative) when the inside corners connect. a and c share a sign, b and d
						// the other, so the denominator has a's sign and only ac against bd is compared.
						// The products round, but a*c and b*d are the same doubles whichever corner
						// starts the face or which way round it goes, so the test is deterministic and
						// symmetric: both cells sharing the face reach the same answer.
						double a = values[node + cornerOffset[FaceCorners[face, 0]]] - (double)isoF;
						double b = values[node + cornerOffset[FaceCorners[face, 1]]] - (double)isoF;
						double c = values[node + cornerOffset[FaceCorners[face, 2]]] - (double)isoF;
						double d = values[node + cornerOffset[FaceCorners[face, 3]]] - (double)isoF;
						double ac = a * c, bd = b * d;
						join = a < 0 ? ac > bd : ac < bd;
					}

					for (int i = 0; i < 4; i++)
					{
						bool here = (cubeIndex & (1 << FaceCorners[face, i])) != 0;
						bool there = (cubeIndex & (1 << FaceCorners[face, (i + 1) % 4])) != 0;
						if (!here || there)
						{
							continue;
						}

						// An exit. Separated inside corners: the entry just before it closes its
						// corner off. Joined: the next entry round the face.
						int step = join ? 1 : 3;
						int entry = (i + step) % 4;
						while (((cubeIndex & (1 << FaceCorners[face, entry])) != 0) == ((cubeIndex & (1 << FaceCorners[face, (entry + 1) % 4])) != 0))
						{
							entry = (entry + step) % 4;
						}

						next[FaceEdges[face, i]] = FaceEdges[face, entry];
					}
				}

				Span<int> loop = stackalloc int[12];
				int visited = 0;
				for (int start = 0; start < 12; start++)
				{
					if (next[start] < 0 || (visited & (1 << start)) != 0)
					{
						continue;
					}

					int count = 0;
					for (int e = start; (visited & (1 << e)) == 0; e = next[e])
					{
						visited |= 1 << e;
						loop[count++] = edgeVertex[key + edgeOffset[e]];
					}

					if (count <= 4)
					{
						for (int n = 1; n + 1 < count; n++)
						{
							AddTriangle(loop[0], loop[n], loop[n + 1]);
						}
					}
					else
					{
						double x = 0, y = 0, z = 0;
						for (int n = 0; n < count; n++)
						{
							x += vertices[loop[n] * 3];
							y += vertices[(loop[n] * 3) + 1];
							z += vertices[(loop[n] * 3) + 2];
						}

						centres.Add(x / count);
						centres.Add(y / count);
						centres.Add(z / count);
						int centre = -(centres.Count / 3);
						for (int n = 0; n < count; n++)
						{
							AddTriangle(centre, loop[n], loop[(n + 1) % count]);
						}
					}
				}

				// Loops run with the inside on their left seen from outside each face, which is
				// clockwise seen from the outside of the surface: wind the other way, as g3 does.
				void AddTriangle(int v0, int v1, int v2)
				{
					triangles.Add(v0);
					triangles.Add(v2);
					triangles.Add(v1);
				}
			}
		}
	}
}
