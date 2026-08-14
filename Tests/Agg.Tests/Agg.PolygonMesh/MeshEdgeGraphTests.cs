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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// Pins the edge topology that both <see cref="MeshEdgeGraph"/> (the flat, sort based extraction
	/// used by the outline render paths) and <see cref="MeshEdge.CreateMeshEdgeList"/> (the older object
	/// per edge view) must produce. The expectations here are written from the geometry, not from either
	/// implementation, so they are a real oracle for both.
	/// </summary>
	public class MeshEdgeGraphTests
	{
		[Test]
		public async Task ClosedSolidsHaveTwoFacesOnEveryEdge()
		{
			foreach (var mesh in new[]
			{
				PlatonicSolids.CreateTetrahedron(),
				PlatonicSolids.CreateCube(10, 10, 10),
				PlatonicSolids.CreateOctahedron(),
				PlatonicSolids.CreateIcosahedron(),
			})
			{
				var adjacency = EdgeAdjacencyFromGraph(mesh);

				// Euler's formula for a closed genus zero surface: V - E + F = 2.
				await Assert.That(adjacency.Count).IsEqualTo(mesh.Vertices.Count + mesh.Faces.Count - 2);

				foreach (var faces in adjacency.Values)
				{
					await Assert.That(faces.Count).IsEqualTo(2);
				}

				// Every face contributes exactly its own three edges.
				await Assert.That(adjacency.Values.Sum(f => f.Count)).IsEqualTo(mesh.Faces.Count * 3);
			}
		}

		[Test]
		public async Task BoundaryAndNonManifoldEdgesReportEveryAdjacentFace()
		{
			var mesh = CreateThreeFacesSharingOneEdge();

			var expected = new Dictionary<(int, int), List<int>>
			{
				[(0, 1)] = new List<int> { 0, 1, 2 }, // the non-manifold spine, three faces hinge on it
				[(1, 2)] = new List<int> { 0 },
				[(0, 2)] = new List<int> { 0 },
				[(0, 3)] = new List<int> { 1 },
				[(1, 3)] = new List<int> { 1 },
				[(1, 4)] = new List<int> { 2 },
				[(0, 4)] = new List<int> { 2 },
			};

			await AssertAdjacencyMatches(expected, EdgeAdjacencyFromGraph(mesh));
			await AssertAdjacencyMatches(expected, EdgeAdjacencyFromMeshEdgeList(mesh));
		}

		[Test]
		public async Task MeshEdgeListAgreesWithFlatGraph()
		{
			foreach (var mesh in new[]
			{
				PlatonicSolids.CreateTetrahedron(),
				PlatonicSolids.CreateCube(10, 10, 10),
				PlatonicSolids.CreateIcosahedron(),
				CreateThreeFacesSharingOneEdge(),
			})
			{
				await AssertAdjacencyMatches(EdgeAdjacencyFromMeshEdgeList(mesh), EdgeAdjacencyFromGraph(mesh));
			}
		}

		[Test]
		public async Task EmptyMeshProducesNoEdges()
		{
			var graph = new Mesh().GetMeshEdgeGraph();

			await Assert.That(graph.EdgeCount).IsEqualTo(0);
		}

		[Test]
		public async Task EdgeVerticesAreOrderedLowToHighAndAreUnique()
		{
			var graph = PlatonicSolids.CreateIcosahedron().GetMeshEdgeGraph();

			var seen = new HashSet<(int, int)>();
			for (int edgeIndex = 0; edgeIndex < graph.EdgeCount; edgeIndex++)
			{
				await Assert.That(graph.GetVertex0(edgeIndex)).IsLessThan(graph.GetVertex1(edgeIndex));
				await Assert.That(seen.Add((graph.GetVertex0(edgeIndex), graph.GetVertex1(edgeIndex)))).IsTrue();
			}
		}

		/// <summary>
		/// Three triangles hinged on the shared edge (0, 1). Gives one non-manifold edge with three faces
		/// and six boundary edges with one face each.
		/// </summary>
		private static Mesh CreateThreeFacesSharingOneEdge()
		{
			var mesh = new Mesh();
			mesh.Vertices.Add(new Vector3Float(0, 0, 0));
			mesh.Vertices.Add(new Vector3Float(1, 0, 0));
			mesh.Vertices.Add(new Vector3Float(0, 1, 0));
			mesh.Vertices.Add(new Vector3Float(0, -1, 0));
			mesh.Vertices.Add(new Vector3Float(0, 0, 1));

			mesh.Faces.Add(0, 1, 2, mesh.Vertices);
			mesh.Faces.Add(0, 3, 1, mesh.Vertices);
			mesh.Faces.Add(0, 1, 4, mesh.Vertices);

			return mesh;
		}

		private static Dictionary<(int, int), List<int>> EdgeAdjacencyFromGraph(Mesh mesh)
		{
			var graph = mesh.GetMeshEdgeGraph();
			var adjacency = new Dictionary<(int, int), List<int>>();
			for (int edgeIndex = 0; edgeIndex < graph.EdgeCount; edgeIndex++)
			{
				var faces = new List<int>();
				for (int face = 0; face < graph.GetFaceCount(edgeIndex); face++)
				{
					faces.Add(graph.GetFace(edgeIndex, face));
				}

				adjacency.Add((graph.GetVertex0(edgeIndex), graph.GetVertex1(edgeIndex)), faces);
			}

			return adjacency;
		}

		private static Dictionary<(int, int), List<int>> EdgeAdjacencyFromMeshEdgeList(Mesh mesh)
		{
			var adjacency = new Dictionary<(int, int), List<int>>();
			foreach (var meshEdge in mesh.GetMeshEdges())
			{
				adjacency.Add((meshEdge.Vertex0Index, meshEdge.Vertex1Index), meshEdge.Faces.ToList());
			}

			return adjacency;
		}

		private static async Task AssertAdjacencyMatches(Dictionary<(int, int), List<int>> expected, Dictionary<(int, int), List<int>> actual)
		{
			await Assert.That(actual.Count).IsEqualTo(expected.Count);
			foreach (var kvp in expected)
			{
				await Assert.That(actual.ContainsKey(kvp.Key)).IsTrue();

				// Face order within an edge is part of the contract - callers index Faces[0]/Faces[1].
				await Assert.That(string.Join(",", actual[kvp.Key])).IsEqualTo(string.Join(",", kvp.Value));
			}
		}
	}
}
