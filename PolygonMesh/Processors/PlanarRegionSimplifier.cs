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
using ManifoldSharp;
using ManifoldSharp.Linalg;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonMesh.Processors
{
	/// <summary>
	/// Merges connected coplanar triangles into one region each and retriangulates the region from
	/// its boundary vertices alone - the marching-cubes surface of a distance field over a mechanical
	/// part is mostly flat walls tiled with grid-sized triangles, which this collapses to a few
	/// large ones without moving the surface.
	/// </summary>
	/// <remarks>
	/// Every boundary vertex of a region is kept, so each edge a region shares with its neighbour
	/// stays exactly as it was: the mesh stays closed with no T-junctions, and non-planar regions are
	/// untouched. Only interior vertices go, and each lies within the distance tolerance of its
	/// region's plane. The retriangulation is Manifold's ear clipper (built for retriangulating
	/// coplanar faces, holes included, never adding a vertex). A region is left as it was when its
	/// boundary pinches (one vertex starts two boundary edges), when the new triangles do not tile
	/// the old area exactly, or when a new diagonal duplicates an edge elsewhere in the mesh.
	/// </remarks>
	public static class PlanarRegionSimplifier
	{
		/// <summary>
		/// Returns a copy of <paramref name="mesh"/> with every coplanar region retriangulated, or the
		/// mesh's own faces where nothing could be merged.
		/// </summary>
		/// <param name="mesh">A welded mesh (faces share vertex indices).</param>
		/// <param name="angleTolerance">Radians a face normal may differ from its region's.</param>
		/// <param name="relativeDistanceTolerance">How far, as a fraction of the largest bounding box
		/// extent, a vertex may sit off its region's plane.</param>
		public static Mesh Simplify(Mesh mesh, double angleTolerance = 1e-4, double relativeDistanceTolerance = 1e-7)
		{
			ArgumentNullException.ThrowIfNull(mesh);
			int faceCount = mesh.Faces.Count;
			var positions = new Vector3[mesh.Vertices.Count];
			for (int i = 0; i < positions.Length; i++)
			{
				positions[i] = new Vector3(mesh.Vertices[i]);
			}

			var bounds = mesh.GetAxisAlignedBoundingBox();
			double distanceTolerance = relativeDistanceTolerance * Math.Max(bounds.XSize, Math.Max(bounds.YSize, bounds.ZSize));
			double cosTolerance = Math.Cos(angleTolerance);

			var faceVerts = new int[faceCount * 3];
			var normals = new Vector3[faceCount];
			var areas = new double[faceCount];
			for (int f = 0; f < faceCount; f++)
			{
				var face = mesh.Faces[f];
				faceVerts[3 * f] = face.v0;
				faceVerts[(3 * f) + 1] = face.v1;
				faceVerts[(3 * f) + 2] = face.v2;
				var cross = (positions[face.v1] - positions[face.v0]).Cross(positions[face.v2] - positions[face.v0]);
				areas[f] = cross.Length / 2;
				normals[f] = areas[f] > 0 ? cross / (2 * areas[f]) : Vector3.Zero;
			}

			// Directed half-edge -> its index (face * 3 + corner). A repeated directed edge is a
			// non-manifold spot; its faces are kept out of every region.
			var halfEdges = new Dictionary<long, int>(faceCount * 3);
			var eligible = new bool[faceCount];
			Array.Fill(eligible, true);
			for (int h = 0; h < faceCount * 3; h++)
			{
				long key = EdgeKey(faceVerts[h], faceVerts[Next(h)]);
				if (!halfEdges.TryAdd(key, h))
				{
					eligible[h / 3] = false;
					eligible[halfEdges[key] / 3] = false;
				}
			}

			// Seed from the largest faces first: their normals carry the least rounding noise.
			var order = new int[faceCount];
			for (int i = 0; i < faceCount; i++)
			{
				order[i] = i;
			}

			Array.Sort(order, (a, b) => areas[b].CompareTo(areas[a]));

			// Degenerate slivers inside a flat wall have no normal to compare; they join on distance.
			double degenerateArea = distanceTolerance * distanceTolerance;
			var regionOf = new int[faceCount];
			Array.Fill(regionOf, -1);
			var regions = new List<Region>();
			var stack = new Stack<int>();
			foreach (int seed in order)
			{
				if (regionOf[seed] != -1 || !eligible[seed] || areas[seed] <= degenerateArea)
				{
					continue;
				}

				var region = new Region { Normal = normals[seed], Offset = normals[seed].Dot(positions[faceVerts[3 * seed]]) };
				int regionIndex = regions.Count;
				regionOf[seed] = regionIndex;
				stack.Push(seed);
				while (stack.Count > 0)
				{
					int f = stack.Pop();
					region.Faces.Add(f);
					for (int corner = 0; corner < 3; corner++)
					{
						int h = (3 * f) + corner;
						if (!halfEdges.TryGetValue(EdgeKey(faceVerts[Next(h)], faceVerts[h]), out int opposite))
						{
							continue;
						}

						int g = opposite / 3;
						if (regionOf[g] == -1 && eligible[g] && Joins(g, region))
						{
							regionOf[g] = regionIndex;
							stack.Push(g);
						}
					}
				}

				regions.Add(region);
			}

			bool Joins(int f, Region region)
			{
				for (int corner = 0; corner < 3; corner++)
				{
					if (Math.Abs(region.Normal.Dot(positions[faceVerts[(3 * f) + corner]]) - region.Offset) > distanceTolerance)
					{
						return false;
					}
				}

				return areas[f] <= degenerateArea || normals[f].Dot(region.Normal) >= cosTolerance;
			}

			// A region with a pinched boundary can not be traced; it keeps its faces, as do lone faces.
			var active = new bool[regions.Count];
			for (int r = 0; r < regions.Count; r++)
			{
				active[r] = regions[r].Faces.Count >= 2 && TraceBoundary(regions[r], r, faceVerts, regionOf, halfEdges);
			}

			// Retriangulate every active region; a region that fails keeps its faces and the vertex
			// drops are worked out again, since its neighbours may no longer drop the vertices they
			// share with it. A new diagonal can also join two boundary vertices a neighbour already
			// joins, giving an edge four faces; the regions that made such an edge are reverted too.
			var replacements = new List<int[]>[regions.Count];
			while (true)
			{
				var dropped = FindDroppedVertices(regions, active, regionOf, faceVerts, positions, distanceTolerance);
				bool failed = false;
				for (int r = 0; r < regions.Count; r++)
				{
					replacements[r] = active[r] ? Retriangulate(regions[r], dropped, faceVerts, positions, distanceTolerance) : null;
					if (active[r] && replacements[r] == null)
					{
						active[r] = false;
						failed = true;
					}
				}

				if (failed)
				{
					continue;
				}

				var result = Assemble(mesh.Vertices, faceCount, faceVerts, regionOf, replacements, out var badEdges);
				var reverted = false;
				foreach (var (_, owners) in badEdges)
				{
					foreach (var owner in owners)
					{
						foreach (int r in owner)
						{
							if (r >= 0 && active[r])
							{
								active[r] = false;
								reverted = true;
							}
						}
					}
				}

				if (!reverted)
				{
					return result;
				}
			}
		}

		private static int Next(int h) => (h % 3) == 2 ? h - 2 : h + 1;

		private static long EdgeKey(int a, int b) => ((long)a << 32) | (uint)b;

		/// <summary>
		/// Traces the region's boundary loops into <see cref="Region.Loops"/>, in the faces' winding;
		/// false when one vertex starts two boundary edges (a pinch), which would make it ambiguous.
		/// </summary>
		private static bool TraceBoundary(Region region, int regionIndex, int[] faceVerts, int[] regionOf, Dictionary<long, int> halfEdges)
		{
			var nextOnBoundary = new Dictionary<int, int>();
			foreach (int f in region.Faces)
			{
				for (int corner = 0; corner < 3; corner++)
				{
					int h = (3 * f) + corner;
					int a = faceVerts[h];
					int b = faceVerts[Next(h)];
					if (halfEdges.TryGetValue(EdgeKey(b, a), out int opposite) && regionOf[opposite / 3] == regionIndex)
					{
						continue;
					}

					if (!nextOnBoundary.TryAdd(a, b))
					{
						return false;
					}
				}
			}

			var visited = new HashSet<int>();
			foreach (int start in nextOnBoundary.Keys)
			{
				if (!visited.Add(start))
				{
					continue;
				}

				var loop = new List<int>();
				int current = start;
				do
				{
					loop.Add(current);
					if (!nextOnBoundary.TryGetValue(current, out current))
					{
						return false;
					}
				}
				while (current != start && visited.Add(current));

				if (current != start)
				{
					return false;
				}

				region.Loops.Add(loop);
			}

			return true;
		}

		/// <summary>
		/// The boundary vertices two merged regions may both leave out: a vertex touched only by the
		/// faces of exactly two active regions, on the straight run of boundary they share. A run is
		/// dropped only when all of it lies on the segment between the kept vertices at its ends, and
		/// both regions see the same run between the same ends, so they drop the same vertices.
		/// </summary>
		private static bool[] FindDroppedVertices(List<Region> regions, bool[] active, int[] regionOf, int[] faceVerts, Vector3[] positions, double distanceTolerance)
		{
			int vertexCount = positions.Length;
			var first = new int[vertexCount];
			var second = new int[vertexCount];
			var keep = new bool[vertexCount];
			Array.Fill(first, -1);
			Array.Fill(second, -1);
			for (int h = 0; h < faceVerts.Length; h++)
			{
				int vertex = faceVerts[h];
				int r = regionOf[h / 3];
				if (r < 0 || !active[r])
				{
					keep[vertex] = true;
				}
				else if (first[vertex] == -1)
				{
					first[vertex] = r;
				}
				else if (first[vertex] != r)
				{
					if (second[vertex] == -1)
					{
						second[vertex] = r;
					}
					else if (second[vertex] != r)
					{
						keep[vertex] = true;
					}
				}
			}

			var dropped = new bool[vertexCount];
			for (int vertex = 0; vertex < vertexCount; vertex++)
			{
				dropped[vertex] = !keep[vertex] && second[vertex] != -1;
			}

			// First each candidate against its own two neighbours, which both regions share.
			for (int r = 0; r < regions.Count; r++)
			{
				if (!active[r])
				{
					continue;
				}

				foreach (var loop in regions[r].Loops)
				{
					for (int i = 0; i < loop.Count; i++)
					{
						int vertex = loop[i];
						if (dropped[vertex]
							&& DistanceToSegment(positions[vertex], positions[loop[(i + loop.Count - 1) % loop.Count]], positions[loop[(i + 1) % loop.Count]]) > distanceTolerance)
						{
							dropped[vertex] = false;
						}
					}
				}
			}

			// Then each whole run against the segment that will replace it, so a gentle curve can not
			// be flattened a vertex at a time.
			for (int r = 0; r < regions.Count; r++)
			{
				if (!active[r])
				{
					continue;
				}

				foreach (var loop in regions[r].Loops)
				{
					int anchor = loop.FindIndex(vertex => !dropped[vertex]);
					if (anchor < 0)
					{
						foreach (int vertex in loop)
						{
							dropped[vertex] = false;
						}

						continue;
					}

					for (int step = 0; step < loop.Count;)
					{
						int start = (anchor + step) % loop.Count;
						int length = 1;
						while (dropped[loop[(start + length) % loop.Count]])
						{
							length++;
						}

						if (length > 1)
						{
							var a = positions[loop[start]];
							var b = positions[loop[(start + length) % loop.Count]];
							bool straight = true;
							for (int i = 1; i < length && straight; i++)
							{
								straight = DistanceToSegment(positions[loop[(start + i) % loop.Count]], a, b) <= distanceTolerance;
							}

							if (!straight)
							{
								for (int i = 1; i < length; i++)
								{
									dropped[loop[(start + i) % loop.Count]] = false;
								}
							}
						}

						step += length;
					}
				}
			}

			return dropped;
		}

		private static double DistanceToSegment(Vector3 point, Vector3 a, Vector3 b)
		{
			var ab = b - a;
			double lengthSquared = ab.LengthSquared;
			double t = lengthSquared > 0 ? Math.Clamp((point - a).Dot(ab) / lengthSquared, 0, 1) : 0;
			return (point - (a + (ab * t))).Length;
		}

		private static List<int[]> Retriangulate(Region region, bool[] dropped, int[] faceVerts, Vector3[] positions, double distanceTolerance)
		{
			// Project onto the plane; the faces wind counter-clockwise about the normal, so the
			// outer loop comes out counter-clockwise and the holes clockwise, as the ear clipper wants.
			var normal = region.Normal;
			var u = normal.Cross(Math.Abs(normal.X) < 0.9 ? Vector3.UnitX : Vector3.UnitY).GetNormal();
			var v = normal.Cross(u);
			var loops = new List<List<PolyVert>>();
			int boundaryCount = 0;
			foreach (var loop in region.Loops)
			{
				var kept = new List<PolyVert>(loop.Count);
				foreach (int vertex in loop)
				{
					if (!dropped[vertex])
					{
						var p = positions[vertex];
						kept.Add(new PolyVert(new Vec2(p.Dot(u), p.Dot(v)), vertex));
					}
				}

				boundaryCount += kept.Count;
				loops.Add(kept);
			}

			// A disk with h holes and V boundary vertices takes V + 2h - 2 triangles.
			int expected = boundaryCount + (2 * (loops.Count - 1)) - 2;
			if (expected > region.Faces.Count)
			{
				return null;
			}

			List<IVec3> triangles;
			try
			{
				triangles = ManifoldSharp.Polygon.TriangulateIdx(loops, distanceTolerance, true);
			}
			catch (Exception)
			{
				return null;
			}

			if (triangles.Count != expected)
			{
				return null;
			}

			// The new triangles must all face the region's way and cover exactly its old area - with
			// every one positive, an equal total rules out overlaps and gaps.
			double oldArea = 0;
			foreach (int f in region.Faces)
			{
				oldArea += ProjectedArea(positions[faceVerts[3 * f]], positions[faceVerts[(3 * f) + 1]], positions[faceVerts[(3 * f) + 2]], normal);
			}

			double newArea = 0;
			var result = new List<int[]>(triangles.Count);
			foreach (var triangle in triangles)
			{
				double area = ProjectedArea(positions[triangle.X], positions[triangle.Y], positions[triangle.Z], normal);
				if (!(area > 0))
				{
					return null;
				}

				newArea += area;
				result.Add(new[] { triangle.X, triangle.Y, triangle.Z });
			}

			if (Math.Abs(newArea - oldArea) > 1e-9 * Math.Max(1, oldArea) + (distanceTolerance * distanceTolerance))
			{
				return null;
			}

			return result;
		}

		private static double ProjectedArea(Vector3 a, Vector3 b, Vector3 c, Vector3 normal)
		{
			return (b - a).Cross(c - a).Dot(normal) / 2;
		}

		/// <summary>
		/// Builds the output mesh and reports, per undirected edge, which regions (-1 for an unmerged
		/// face) contribute each of its half-edges, so the caller can find edges that lost manifoldness.
		/// </summary>
		private static Mesh Assemble(List<Vector3Float> vertices, int faceCount, int[] faceVerts, int[] regionOf, List<int[]>[] replacements, out List<(long Edge, List<List<int>> Owners)> badEdges)
		{
			var triangles = new List<(int A, int B, int C, int Region)>();
			for (int f = 0; f < faceCount; f++)
			{
				int r = regionOf[f];
				if (r < 0 || replacements[r] == null)
				{
					triangles.Add((faceVerts[3 * f], faceVerts[(3 * f) + 1], faceVerts[(3 * f) + 2], -1));
				}
			}

			for (int r = 0; r < replacements.Length; r++)
			{
				if (replacements[r] != null)
				{
					foreach (var t in replacements[r])
					{
						triangles.Add((t[0], t[1], t[2], r));
					}
				}
			}

			// An edge is good when each direction is used exactly once; on a bad one, every region
			// that used it is a suspect.
			var directed = new Dictionary<long, List<int>>(triangles.Count * 3);
			foreach (var (a, b, c, region) in triangles)
			{
				AddDirected(directed, a, b, region);
				AddDirected(directed, b, c, region);
				AddDirected(directed, c, a, region);
			}

			badEdges = new List<(long, List<List<int>>)>();
			foreach (var (key, owners) in directed)
			{
				int a = (int)(key >> 32);
				int b = (int)(uint)key;
				directed.TryGetValue(EdgeKey(b, a), out var reverse);
				bool introduced = owners.Exists(r => r >= 0) || (reverse != null && reverse.Exists(r => r >= 0));
				if (introduced && (owners.Count != 1 || reverse == null || reverse.Count != 1))
				{
					badEdges.Add((key, new List<List<int>> { owners, reverse ?? new List<int>() }));
				}
			}

			return BuildMesh(vertices, triangles);
		}

		private static void AddDirected(Dictionary<long, List<int>> directed, int a, int b, int region)
		{
			long key = EdgeKey(a, b);
			if (!directed.TryGetValue(key, out var owners))
			{
				owners = new List<int>(1);
				directed[key] = owners;
			}

			owners.Add(region);
		}

		/// <summary>Builds the mesh from the kept vertices only, renumbered in their old order.</summary>
		private static Mesh BuildMesh(List<Vector3Float> vertices, List<(int A, int B, int C, int Region)> triangles)
		{
			var newIndex = new int[vertices.Count];
			Array.Fill(newIndex, -1);
			var keptVertices = new List<Vector3Float>();
			foreach (var (a, b, c, _) in triangles)
			{
				foreach (int vertex in new[] { a, b, c })
				{
					if (newIndex[vertex] == -1)
					{
						newIndex[vertex] = keptVertices.Count;
						keptVertices.Add(vertices[vertex]);
					}
				}
			}

			var result = new Mesh();
			result.Vertices = keptVertices;
			foreach (var (a, b, c, _) in triangles)
			{
				result.Faces.Add(newIndex[a], newIndex[b], newIndex[c], keptVertices);
			}

			return result;
		}

		private sealed class Region
		{
			public List<int> Faces { get; } = new List<int>();

			public Vector3 Normal { get; set; }

			public double Offset { get; set; }

			/// <summary>Gets the boundary loops, outer and holes, in the faces' winding.</summary>
			public List<List<int>> Loops { get; } = new List<List<int>>();
		}
	}
}
