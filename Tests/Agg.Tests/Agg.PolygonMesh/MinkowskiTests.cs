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
using System.Threading.Tasks;
// The kernel's own status enum, aliased rather than reached through a namespace import:
// ManifoldSharp.Manifold would sit beside MatterHackers.PolygonMesh.Mesh in this file.
using ManifoldStatus = ManifoldSharp.Error;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The kernel's morphological operations through <see cref="MinkowskiProcessing"/>, the
	/// entry points a uniform fillet and the bevel feature's test oracle both go through.
	/// </summary>
	/// <remarks>
	/// Every volume here is bracketed rather than compared to one analytic number, and the
	/// bracket is a real one: the structuring ball is an inscribed polytope, so it contains the
	/// sphere of its own inradius and is contained by the sphere of its radius. Minkowski sums
	/// are monotone in either operand, so rounding done with the polytope has to land between
	/// the two analytic answers - a bound that stays true at any tessellation rather than a
	/// fudge factor fitted to this one.
	/// <para>
	/// The balls are deliberately coarse. An erosion costs one convex hull and a boolean per
	/// triangle of the solid being eroded (see the cost model on
	/// <see cref="MinkowskiProcessing"/>), so a finer ball makes a slower test without making
	/// any assertion here stronger.
	/// </para>
	/// </remarks>
	public class MinkowskiTests
	{
		/// <summary>
		/// The cube these tests round, as a centred 10mm box.
		/// </summary>
		private const double CubeSide = 10;

		/// <summary>
		/// Dilating a solid by a ball grows it by the ball's radius in every direction: a cube
		/// becomes a cube with rounded edges and corners, the same solid a rolling ball of that
		/// radius would leave behind.
		/// </summary>
		[Test]
		public async Task DilatingACubeByABallRoundsItAndGrowsTheBoundsByTheDiameter()
		{
			const double radius = 1.0;
			var ball = MinkowskiProcessing.SphereMesh(radius, 16);

			var dilated = Cleaned(MinkowskiProcessing.MinkowskiSum(
				PlatonicSolids.CreateCube(CubeSide, CubeSide, CubeSide),
				ball));

			await Assert.That(dilated.IsManifold()).IsTrue()
				.Because("a dilated solid is still one closed watertight body");

			var volume = SignedVolume(dilated);

			await Assert.That(volume).IsLessThanOrEqualTo(RoundedBoxVolume(CubeSide, radius))
				.Because("the tessellated ball is inside the true one, so the sum cannot exceed the analytic rounded cube");
			await Assert.That(volume).IsGreaterThanOrEqualTo(RoundedBoxVolume(CubeSide, InRadius(ball)))
				.Because("the ball contains the sphere of its own inradius, so the sum contains that rounding");

			// The ball is a subdivided octahedron, so its six poles sit exactly on the axes at
			// the radius - which is why this is an equality and not a bracket.
			var bounds = dilated.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(CubeSide + (2 * radius)).Within(0.0001);
			await Assert.That(bounds.YSize).IsEqualTo(CubeSide + (2 * radius)).Within(0.0001);
			await Assert.That(bounds.ZSize).IsEqualTo(CubeSide + (2 * radius)).Within(0.0001);
		}

		/// <summary>
		/// Closing - dilate then erode - gives a convex solid back unchanged. It is the property
		/// that makes the erosion trustworthy at all: the two operations have to be inverses on
		/// a shape with no concave feature small enough for the ball to bridge.
		/// </summary>
		/// <remarks>
		/// The coarsest usable ball, because this is the one test whose erosion runs over a
		/// rounded body's triangles rather than a cube's twelve, and it is a second of work even
		/// so.
		/// </remarks>
		[Test]
		public async Task ClosingAConvexSolidGivesItBackUnchanged()
		{
			const double radius = 1.0;
			var ball = MinkowskiProcessing.SphereMesh(radius, 8);
			var cube = PlatonicSolids.CreateCube(CubeSide, CubeSide, CubeSide);

			var dilated = Cleaned(MinkowskiProcessing.MinkowskiSum(cube, ball));
			var closed = Cleaned(MinkowskiProcessing.MinkowskiDifference(dilated, ball));

			await Assert.That(closed.IsManifold()).IsTrue();

			// Exactly the cube back, not approximately: eroding by the same ball puts the six
			// face planes back where the dilation found them. The tolerance is for the float
			// vertex storage, not for the arithmetic.
			await Assert.That(SignedVolume(closed)).IsEqualTo(CubeSide * CubeSide * CubeSide).Within(0.01);

			var bounds = closed.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(CubeSide).Within(0.0001);
			await Assert.That(bounds.YSize).IsEqualTo(CubeSide).Within(0.0001);
			await Assert.That(bounds.ZSize).IsEqualTo(CubeSide).Within(0.0001);
		}

		/// <summary>
		/// Opening - erode then dilate - rounds every convex edge and corner by the ball's
		/// radius while leaving the flat faces where they were. This is the uniform all-edges
		/// fillet, and the oracle a selective fillet is measured against.
		/// </summary>
		[Test]
		public async Task OpeningACubeRoundsItsEdgesWithoutMovingItsFaces()
		{
			const double radius = 1.0;
			var ball = MinkowskiProcessing.SphereMesh(radius, 16);
			var cube = PlatonicSolids.CreateCube(CubeSide, CubeSide, CubeSide);

			var eroded = Cleaned(MinkowskiProcessing.MinkowskiDifference(cube, ball));

			// The erosion of a cube by a centred ball is the concentric cube two radii smaller,
			// exactly - worth pinning here because the opening's bounds below are built on it.
			var erodedSide = CubeSide - (2 * radius);
			await Assert.That(SignedVolume(eroded)).IsEqualTo(erodedSide * erodedSide * erodedSide).Within(0.01);

			var opened = Cleaned(MinkowskiProcessing.MinkowskiSum(eroded, ball));

			await Assert.That(opened.IsManifold()).IsTrue();

			var volume = SignedVolume(opened);

			await Assert.That(volume).IsLessThan(CubeSide * CubeSide * CubeSide)
				.Because("rounding the edges of a solid can only take material away");
			await Assert.That(volume).IsLessThanOrEqualTo(RoundedBoxVolume(erodedSide, radius))
				.Because("the tessellated ball is inside the true one, so the rounding cannot exceed the analytic one");
			await Assert.That(volume).IsGreaterThanOrEqualTo(RoundedBoxVolume(erodedSide, InRadius(ball)))
				.Because("the ball contains the sphere of its own inradius, so the rounding contains that answer");

			// The faces themselves never moved: an opening is inscribed in what it opened.
			var bounds = opened.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(CubeSide).Within(0.0001);
			await Assert.That(bounds.YSize).IsEqualTo(CubeSide).Within(0.0001);
			await Assert.That(bounds.ZSize).IsEqualTo(CubeSide).Within(0.0001);
		}

		/// <summary>
		/// An operand the kernel will not take fails the same way it fails a boolean - the
		/// import's own complaint, naming the status - rather than crashing or quietly answering
		/// with the other operand.
		/// </summary>
		[Test]
		public async Task AnOpenMeshIsRefusedTheWayABooleanRefusesIt()
		{
			var ball = MinkowskiProcessing.SphereMesh(1.0, 8);

			var asSolid = Assert.Throws<InvalidOperationException>(
				() => MinkowskiProcessing.MinkowskiSum(OpenBox(8), ball));

			await Assert.That(asSolid.Message).Contains(ManifoldStatus.NotClosed.ToString())
				.Because("the status is what tells the user which operand was unusable");

			// Both sides are validated: a bad structuring element is as unusable as a bad solid,
			// and it must not be the thing that is silently ignored.
			var asTool = Assert.Throws<InvalidOperationException>(
				() => MinkowskiProcessing.MinkowskiDifference(
					PlatonicSolids.CreateCube(CubeSide, CubeSide, CubeSide),
					OpenBox(2)));

			await Assert.That(asTool.Message).Contains(ManifoldStatus.NotClosed.ToString());
		}

		/// <summary>
		/// An empty operand is refused rather than treated as a no-op. The kernel's own answer to
		/// one is the <em>other</em> operand unchanged, which would show up as a fillet that
		/// silently did nothing.
		/// </summary>
		[Test]
		public void AnEmptyOperandIsRefusedRatherThanQuietlyDoingNothing()
		{
			var cube = PlatonicSolids.CreateCube(CubeSide, CubeSide, CubeSide);

			Assert.Throws<ArgumentException>(() => MinkowskiProcessing.MinkowskiSum(cube, new Mesh()));
			Assert.Throws<ArgumentException>(() => MinkowskiProcessing.MinkowskiDifference(new Mesh(), cube));
		}

		/// <summary>
		/// An operand with triangles but no volume is refused too. It is the harder half of the
		/// same bug as the empty one: a zero-thickness shell is closed, so it passes every mesh
		/// check and imports with no error at all - and lands on the kernel as an empty manifold,
		/// whose early exit hands back the *other* operand's clone. A fillet that returned its
		/// input unchanged and reported success would be indistinguishable from one that worked.
		/// </summary>
		[Test]
		public void AnOperandWithNoVolumeIsRefusedRatherThanQuietlyReturningTheOtherOne()
		{
			var cube = PlatonicSolids.CreateCube(CubeSide, CubeSide, CubeSide);

			// Closed, 8 vertices and 12 faces, and every one of them degenerate: the kernel
			// welds the two coincident sides together and is left with nothing.
			var flat = PlatonicSolids.CreateCube(2, 2, 0);

			Assert.Throws<ArgumentException>(() => MinkowskiProcessing.MinkowskiSum(cube, flat));
			Assert.Throws<ArgumentException>(() => MinkowskiProcessing.MinkowskiDifference(flat, cube));
		}

		/// <summary>
		/// The structuring ball's own arguments are checked rather than absorbed: the kernel
		/// answers a bad radius with an empty manifold, and reads any non-positive segment count
		/// as "pick one for me" - so a radius or a segment count that came out of a calculation
		/// wrong would silently produce a ball nobody asked for.
		/// </summary>
		[Test]
		public void ASphereRefusesARadiusOrSegmentCountThatCannotHaveBeenMeant()
		{
			Assert.Throws<ArgumentOutOfRangeException>(() => MinkowskiProcessing.SphereMesh(0, 16));
			Assert.Throws<ArgumentOutOfRangeException>(() => MinkowskiProcessing.SphereMesh(-1, 16));
			Assert.Throws<ArgumentOutOfRangeException>(() => MinkowskiProcessing.SphereMesh(1, -8));
		}

		/// <summary>
		/// What a boolean result gets before anyone looks at its topology: the kernel's export
		/// splits vertices at run boundaries, so <see cref="MeshExtensionMethods.IsManifold"/>
		/// is only meaningful after the merge.
		/// </summary>
		private static Mesh Cleaned(Mesh mesh)
		{
			mesh.CleanAndMerge();
			mesh.RemoveUnusedVertices();

			return mesh;
		}

		/// <summary>
		/// The signed volume of a closed mesh, positive when its faces wind outward.
		/// Sums the tetrahedron each triangle forms with the origin, which is the
		/// standard divergence-theorem sum and needs no topology.
		/// </summary>
		private static double SignedVolume(Mesh mesh)
		{
			double total = 0;

			foreach (var face in mesh.Faces)
			{
				var a = new Vector3(mesh.Vertices[face.v0]);
				var b = new Vector3(mesh.Vertices[face.v1]);
				var c = new Vector3(mesh.Vertices[face.v2]);

				total += a.Dot(b.Cross(c)) / 6.0;
			}

			return total;
		}

		/// <summary>
		/// The largest sphere centred on the origin that fits inside a convex mesh, measured as
		/// the nearest face plane. For the tessellated ball this is the radius the rounding is
		/// guaranteed to achieve, which is the lower half of every bracket here.
		/// </summary>
		private static double InRadius(Mesh mesh)
		{
			double nearest = double.MaxValue;

			foreach (var face in mesh.Faces)
			{
				var a = new Vector3(mesh.Vertices[face.v0]);
				var b = new Vector3(mesh.Vertices[face.v1]);
				var c = new Vector3(mesh.Vertices[face.v2]);

				var normal = (b - a).Cross(c - a);
				var length = normal.Length;

				if (length <= 0)
				{
					// A degenerate triangle has no plane to measure to; the ball's other faces
					// still bound it.
					continue;
				}

				nearest = Math.Min(nearest, Math.Abs(a.Dot(normal / length)));
			}

			return nearest;
		}

		/// <summary>
		/// The volume of a cube of <paramref name="side"/> with every edge and corner rolled off
		/// by a ball of <paramref name="radius"/> - Steiner's formula for a box: the box itself,
		/// its six faces raised by the radius, twelve quarter-cylinders along its edges, and the
		/// eight corner octants that together make one whole sphere.
		/// </summary>
		private static double RoundedBoxVolume(double side, double radius)
		{
			return (side * side * side)
				+ (6 * side * side * radius)
				+ (12 * side * Math.PI * radius * radius / 4)
				+ (4.0 / 3.0 * Math.PI * radius * radius * radius);
		}

		/// <summary>
		/// A box missing its top face - not closed, so even the robust import rejects it
		/// (NotClosed).
		/// </summary>
		private static Mesh OpenBox(double size)
		{
			var mesh = PlatonicSolids.CreateCube(size, size, size);

			// Drop the two triangles of one face. Which two does not matter; any hole makes
			// the surface open.
			mesh.Faces.RemoveAt(mesh.Faces.Count - 1);
			mesh.Faces.RemoveAt(mesh.Faces.Count - 1);

			return mesh;
		}
	}
}
