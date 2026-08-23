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
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// Cutting a closed solid with a plane has to leave it closed. The two faces that share an edge
	/// walk that edge in opposite directions, so each of them used to compute the plane crossing
	/// from its own end of the edge. In float those two answers differ by an ulp or two, and
	/// <see cref="Mesh.CleanAndMerge"/> welds only bit identical positions - so every cut used to
	/// leave a seam of boundary edges behind, one per crossed edge.
	/// </summary>
	public class MeshSplitManifoldTests
	{
		[Test]
		public async Task SplittingAClosedSolidOnAnAngledPlaneLeavesItClosed()
		{
			// An icosahedron has no axis aligned edges, so every crossing lands on an arbitrary
			// fraction of an edge - the case where the two ends disagree.
			var mesh = PlatonicSolids.CreateIcosahedron(10);

			await Assert.That(mesh.IsManifold()).IsTrue()
				.Because("the solid under test has to start closed");

			mesh.Split(new Plane(new Vector3(0.3, 0.5, 0.81).GetNormal(), 1.37));

			await Assert.That(BoundaryEdgeCount(mesh)).IsEqualTo(0);
			await Assert.That(mesh.IsManifold()).IsTrue();
		}

		[Test]
		public async Task SplittingOnManySerialPlanesLeavesTheSolidClosed()
		{
			var mesh = PlatonicSolids.CreateIcosahedron(10);

			for (int i = 0; i < 8; i++)
			{
				mesh.Split(new Plane(new Vector3(0.3, 0.5, 0.81).GetNormal(), -3.5 + i * 0.91));
			}

			await Assert.That(BoundaryEdgeCount(mesh)).IsEqualTo(0);
			await Assert.That(mesh.IsManifold()).IsTrue();
		}

		/// <summary>
		/// The pattern the Curve tool needs: slice an elongated part into many strips along x before
		/// moving the vertices onto an arc. If the slicing opens seams, everything downstream sees a
		/// non manifold part no matter how good the bend is.
		/// </summary>
		[Test]
		public async Task SlicingAnElongatedSolidIntoStripsLikeTheCurveToolLeavesItClosed()
		{
			var mesh = ElongatedTippedIcosahedron();

			var bounds = mesh.GetAxisAlignedBoundingBox();
			const int CutCount = 40;
			foreach (var cut in CutsAcrossX(bounds, CutCount))
			{
				mesh.Split(new Plane(Vector3.UnitX, cut), bounds.XSize / CutCount / 8);
			}

			await Assert.That(BoundaryEdgeCount(mesh)).IsEqualTo(0);
			await Assert.That(mesh.IsManifold()).IsTrue();
		}

		/// <summary>
		/// The same slicing through the entry point the Curve tool actually calls. Every face crossing
		/// a plane has to be cut in the same sweep as its neighbours: SplitOnPlanes used to cut a face
		/// at a plane only when two of its corners were on the negative side (a test that depends on
		/// the third, unshared, corner) and pick up the neighbours in a later pass, by which time other
		/// planes had subdivided the shared edge, so the two sides interpolated their crossing from
		/// different endpoints and landed on near - but not bit - identical points.
		/// </summary>
		[Test]
		public async Task SplitOnPlanesLeavesTheSolidClosed()
		{
			var mesh = ElongatedTippedIcosahedron();

			var bounds = mesh.GetAxisAlignedBoundingBox();
			const int CutCount = 40;
			mesh.SplitOnPlanes(Vector3.UnitX, CutsAcrossX(bounds, CutCount), bounds.XSize / CutCount / 8);

			await Assert.That(BoundaryEdgeCount(mesh)).IsEqualTo(0);
			await Assert.That(mesh.IsManifold()).IsTrue();
		}

		/// <summary>
		/// SplitOnPlanes slices each face against every plane at once, so a face crossed by k planes has
		/// to come out as 2k+1 triangles. Cutting plane by plane instead re-triangulates the fragments of
		/// the earlier cuts and bloats that to O(k^2) - a triangle spanning 63 planes came out as 3,041
		/// faces instead of 127, and the customer part it came from went from 48k faces to 118k.
		/// </summary>
		[Test]
		public async Task SplitOnPlanesTriangulatesEachFaceOnce()
		{
			// A tetrahedron, stretched along x so every one of its 4 faces spans all the cut planes.
			var mesh = PlatonicSolids.CreateTetrahedron(10);
			mesh.Transform(Matrix4X4.CreateScale(20, 1, 1) * Matrix4X4.CreateRotationZ(0.37));

			var bounds = mesh.GetAxisAlignedBoundingBox();
			const int CutCount = 40;
			var cuts = CutsAcrossX(bounds, CutCount);
			var sourceFaceCount = mesh.Faces.Count;

			mesh.SplitOnPlanes(Vector3.UnitX, cuts, bounds.XSize / CutCount / 8);

			// Each source face is crossed by at most CutCount - 1 planes, so 2k+1 caps out here. The
			// bound is deliberately loose about the exact fan shape and tight enough to catch a cascade.
			var maxPerFace = (2 * cuts.Count) + 2;
			await Assert.That(mesh.Faces.Count).IsLessThanOrEqualTo(sourceFaceCount * maxPerFace);

			await Assert.That(BoundaryEdgeCount(mesh)).IsEqualTo(0);
			await Assert.That(mesh.IsManifold()).IsTrue();
		}

		/// <summary>
		/// onPlaneDistance is a tolerance, not a free parameter: once it reaches the spacing between two
		/// planes, the crossing inserted for the upper plane also reads as "on" the lower one, so it ends
		/// up in both the below and the above polygon and the two fans overlap (589 boundary edges when
		/// this was measured). SplitOnPlanes clamps the tolerance to a fraction of the smallest plane gap,
		/// so even an absurd caller value has to come out closed.
		/// </summary>
		[Test]
		public async Task SplitOnPlanesClampsAnOversizedToleranceAndStaysClosed()
		{
			var mesh = ElongatedTippedIcosahedron();

			var bounds = mesh.GetAxisAlignedBoundingBox();
			const int CutCount = 40;
			var spacing = bounds.XSize / CutCount;

			// Twice the plane spacing - far past the point where the slicer used to open up.
			mesh.SplitOnPlanes(Vector3.UnitX, CutsAcrossX(bounds, CutCount), spacing * 2);

			await Assert.That(BoundaryEdgeCount(mesh)).IsEqualTo(0);
			await Assert.That(mesh.IsManifold()).IsTrue();
		}

		/// <summary>
		/// The T junction case. A plane passes within onPlaneDistance of one face's third corner while
		/// cleanly crossing the edge that face shares with its neighbour, so the plane crosses two edges
		/// of the neighbour but only one edge of this face. If a face were allowed to decide "this plane
		/// does not cross me" and skip the point, the neighbour would still put a vertex on the shared
		/// edge and the two would no longer match - a T junction and an open seam.
		/// </summary>
		[Test]
		public async Task APlaneGrazingAVertexStillCutsTheSharedEdgeOnBothFaces()
		{
			var mesh = new Mesh();
			// A square pyramid. The apex sits just barely past the cut plane, so the plane grazes it.
			const double OnPlaneDistance = 0.05;
			var cut = 4.0;
			var apex = new Vector3(0, 0, 10);
			var corners = new[]
			{
				new Vector3(cut + 2, -5, 0),
				new Vector3(cut + 2, 5, 0),
				new Vector3(cut - 6, 5, 0),
				new Vector3(cut - 6, -5, 0),
			};

			for (int i = 0; i < 4; i++)
			{
				mesh.CreateFace(corners[i], corners[(i + 1) % 4], apex);
			}

			mesh.CreateFace(corners[0], corners[2], corners[1]);
			mesh.CreateFace(corners[0], corners[3], corners[2]);
			mesh.CleanAndMerge();

			await Assert.That(mesh.IsManifold()).IsTrue()
				.Because("the solid under test has to start closed");

			// The plane sits a hair off the apex's x (which is 0), well inside onPlaneDistance, while
			// cleanly crossing the base edges either side of it.
			mesh.SplitOnPlanes(Vector3.UnitX, new List<double> { 0.02, cut }, OnPlaneDistance);

			await Assert.That(BoundaryEdgeCount(mesh)).IsEqualTo(0);
			await Assert.That(mesh.IsManifold()).IsTrue();
		}

		/// <summary>
		/// A mesh with no faces at all is vacuously manifold - it has no edges to fail the two-faces-per-edge
		/// test - but it is exactly the case that still needs the weld, because dropping every face is what
		/// orphans the vertices. CameraFittingUtil's orthographic path clips a cube away to nothing and then
		/// reads Vertices.Any() to decide whether it has geometry left, so stale corners surviving the clip
		/// make it compute depths from a solid that is no longer in the view volume.
		/// </summary>
		[Test]
		public async Task CleanAndMergeDropsVerticesLeftOverWhenEveryFaceIsClipped()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			// Clip against a plane the whole cube is behind, the way CameraFittingUtil clips to the
			// view volume - every face is discarded and the corners are left behind.
			mesh.Split(new Plane(Vector3.UnitX, 100), cleanAndMerge: false, discardFacesOnNegativeSide: true);

			await Assert.That(mesh.Faces.Count).IsEqualTo(0)
				.Because("the clip plane leaves nothing of the cube");

			mesh.CleanAndMerge();

			await Assert.That(mesh.Vertices.Count).IsEqualTo(0)
				.Because("a mesh with no faces has no vertices worth keeping");
		}

		/// <summary>
		/// Split rebuilds Faces and appends to Vertices, so it has to invalidate the caches keyed off them
		/// (cachedAABB, the transformed AABB cache) itself rather than relying on the MarkAsChanged inside
		/// CleanAndMerge - which callers can switch off, and which no longer runs for an already manifold
		/// result. CameraFittingUtil is the caller that passes cleanAndMerge: false.
		/// </summary>
		[Test]
		public async Task SplitMarksTheMeshAsChangedEvenWhenNotMerging()
		{
			var mesh = PlatonicSolids.CreateIcosahedron(10);

			// Prime the AABB cache so a missed invalidation is a stale bounds, not only an unbumped counter.
			_ = mesh.GetAxisAlignedBoundingBox();
			var changedCountBeforeSplit = mesh.ChangedCount;
			var faceCountBeforeSplit = mesh.Faces.Count;

			mesh.Split(new Plane(Vector3.UnitX, 2.5), cleanAndMerge: false);

			await Assert.That(mesh.Faces.Count).IsNotEqualTo(faceCountBeforeSplit)
				.Because("the plane has to actually cut the solid for this to test anything");

			await Assert.That(mesh.ChangedCount).IsGreaterThan(changedCountBeforeSplit)
				.Because("Split rebuilt Faces and appended Vertices, so anything caching them is stale");
		}

		/// <summary>
		/// The same cache invalidation contract for the single face entry point.
		/// </summary>
		[Test]
		public async Task SplitFaceMarksTheMeshAsChanged()
		{
			var mesh = PlatonicSolids.CreateIcosahedron(10);

			_ = mesh.GetAxisAlignedBoundingBox();
			var changedCountBeforeSplit = mesh.ChangedCount;

			// Which face the plane happens to cross is an accident of how the solid is built, so take
			// the first one it does cross rather than hard coding an index.
			var plane = new Plane(Vector3.UnitX, 2.5);
			var didSplit = false;
			for (int i = 0; i < mesh.Faces.Count && !didSplit; i++)
			{
				didSplit = mesh.SplitFace(i, plane);
			}

			await Assert.That(didSplit).IsTrue()
				.Because("the plane has to actually cross a face for this to test anything");

			await Assert.That(mesh.ChangedCount).IsGreaterThan(changedCountBeforeSplit)
				.Because("SplitFace rebuilt Faces and appended Vertices");
		}

		/// <summary>
		/// A closed solid stretched along x and tipped, so the x cut planes meet its faces at
		/// arbitrary angles and no crossing lands on a tidy fraction of an edge.
		/// </summary>
		private static Mesh ElongatedTippedIcosahedron()
		{
			var mesh = PlatonicSolids.CreateIcosahedron(10);
			mesh.Transform(Matrix4X4.CreateScale(6, 1, 1) * Matrix4X4.CreateRotationZ(0.37) * Matrix4X4.CreateRotationX(0.21));
			return mesh;
		}

		private static List<double> CutsAcrossX(AxisAlignedBoundingBox bounds, int cutCount)
		{
			var cuts = new List<double>();
			for (int i = 1; i < cutCount; i++)
			{
				cuts.Add(bounds.MinXYZ.X + (bounds.XSize * i / cutCount));
			}

			return cuts;
		}

		/// <summary>
		/// How many edges have a single face on them - the shape of the seam left by a cut that
		/// failed to weld, and a far more useful failure message than a bare false.
		/// </summary>
		private static int BoundaryEdgeCount(Mesh mesh)
		{
			var graph = mesh.GetMeshEdgeGraph();
			var count = 0;
			for (int edgeIndex = 0; edgeIndex < graph.EdgeCount; edgeIndex++)
			{
				if (graph.GetFaceCount(edgeIndex) != 2)
				{
					count++;
				}
			}

			return count;
		}
	}
}
