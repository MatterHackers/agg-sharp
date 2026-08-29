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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using MatterHackers.PolygonMesh.Processors;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// Revolve turns a 2D profile into a solid, and every primitive that spins a profile around an axis
	/// (tubes, cylinders, tori, spheres, the Revolve operation) reaches the same code. Two things about
	/// the profile it is handed are not under its control: the profile can be degenerate while a user is
	/// mid-edit, and it may or may not have material on the negative side of the axis.
	/// </summary>
	public class RevolveTests
	{
		/// <summary>
		/// A rectangular profile from (left, 0) to (right, height), authored the way the ring and cylinder
		/// primitives author theirs - four line segments, left implicitly closed.
		/// </summary>
		private static VertexStorage RectangleProfile(double left, double right, double height)
		{
			var path = new VertexStorage();
			path.MoveTo(left, 0);
			path.LineTo(right, 0);
			path.LineTo(right, height);
			path.LineTo(left, height);

			return path;
		}

		[Test]
		public async Task AZeroAreaProfileRevolvesToAnEmptyMesh()
		{
			// A zero height profile encloses no area, so it converts to no clipper polygons at all. Asking
			// an empty polygon set for its bounds answers with the +/- double extremes, and feeding those
			// to clipper threw ClipperException("Coordinate outside allowed range") - on the UI thread,
			// during an ordinary keystroke in a height field, with nothing above it to catch.
			var mesh = RectangleProfile(5, 15, 0).Revolve(30);

			await Assert.That(mesh).IsNotNull()
				.Because("nothing to revolve is an empty solid, not a crash");
			await Assert.That(mesh.Faces.Count).IsEqualTo(0);
			await Assert.That(mesh.Vertices.Count).IsEqualTo(0);
		}

		[Test]
		public async Task AProfileRightOfTheAxisRevolvesUnchanged()
		{
			// The counts are the ones the pre-fix code produced for this profile. The clip-and-mirror branch
			// used to run for every profile (the guard asked whether the polygon list had any polygons, not
			// whether any point was left of the axis), so skipping it now has to leave the solid identical.
			var mesh = RectangleProfile(5, 15, 10).Revolve(30);

			await Assert.That(mesh.Faces.Count).IsEqualTo(240);
			await Assert.That(mesh.Vertices.Count).IsEqualTo(120);

			var bounds = mesh.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(30).Within(.001);
			await Assert.That(bounds.ZSize).IsEqualTo(10).Within(.001);
		}

		/// <summary>
		/// The same rectangle authored the other way around - up the left edge first, so the profile winds
		/// clockwise. Callers author profiles in whatever order is natural for them, so Revolve is the one
		/// place winding gets normalized.
		/// </summary>
		private static VertexStorage ClockwiseRectangleProfile(double left, double right, double height)
		{
			var path = new VertexStorage();
			path.MoveTo(left, 0);
			path.LineTo(left, height);
			path.LineTo(right, height);
			path.LineTo(right, 0);

			return path;
		}

		[Test]
		public async Task AClockwiseProfileRightOfTheAxisRevolvesRightSideOut()
		{
			// Revolve is what makes profile winding not matter to its callers - RevolveObject3D hands it
			// paths straight from the user's sketch and counts on coming back with an outward-wound solid.
			// Winding correction used to be reached by every profile because it sat inside a branch whose
			// guard was always true; once the guard actually tested for material left of the axis, profiles
			// on the right skipped it and came back inside out (negative volume).
			var clockwise = ClockwiseRectangleProfile(10, 20, 5).Revolve(30);
			var counterClockwise = RectangleProfile(10, 20, 5).Revolve(30);

			await Assert.That(SignedVolume(counterClockwise)).IsGreaterThan(0)
				.Because("a counter clockwise profile has always revolved right side out");
			await Assert.That(SignedVolume(clockwise)).IsEqualTo(SignedVolume(counterClockwise)).Within(.001)
				.Because("Revolve normalizes profile winding, so both orders make the same solid");
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

		[Test]
		public async Task APartialRevolveWeldsItsEndCapsToTheWall()
		{
			// The end caps used to be placed by chaining a quarter turn about X onto the Z rotation, while
			// the wall strips built the same points by hand and rotated them once. The two arithmetic paths
			// land about 1e-7 apart, and CleanAndMerge welds on exact float equality, so every cap corner
			// stayed its own vertex and the solid came back open along both end seams.
			const int angleSteps = 8;
			var mesh = RectangleProfile(5, 15, 10).Revolve(angleSteps, 0, MathHelper.Tau / 4);

			// four profile corners at each of the nine ring angles and nothing else - the cap corners are
			// corners of the first and last ring, not points of their own
			await Assert.That(mesh.Vertices.Count).IsEqualTo(4 * (angleSteps + 1));
			await Assert.That(mesh.IsManifold()).IsTrue()
				.Because("a partial revolve is a closed solid, so every edge has exactly two faces");

			// The faceted quarter wedge is eight prisms of chord width: 8 * height * sin(step) * (R^2 - r^2) / 2.
			// Welding alone would not notice a cap placed at the wrong angle or wound inward, but volume does.
			var stepAngle = MathHelper.Tau / 4 / angleSteps;
			var expectedVolume = angleSteps * 10 * Math.Sin(stepAngle) * ((15 * 15) - (5 * 5)) / 2;
			await Assert.That(SignedVolume(mesh)).IsEqualTo(expectedVolume).Within(.001)
				.Because("both caps close the wedge, facing outward");
		}

		[Test]
		public async Task AFullRevolveWeldsItsWrapAroundSeam()
		{
			// A full turn has no end caps, and the closing strip ends on the exact angle the first strip
			// started from, so its seam has always welded. Pinned so the cap fix cannot regress it.
			const int angleSteps = 8;
			var mesh = RectangleProfile(5, 15, 10).Revolve(angleSteps);

			await Assert.That(mesh.Vertices.Count).IsEqualTo(4 * angleSteps);
			await Assert.That(mesh.IsManifold()).IsTrue()
				.Because("a full revolve closes on itself");
		}

		[Test]
		public async Task AProfileLeftOfTheAxisIsMirroredOntoTheRight()
		{
			// The clip-and-mirror branch still has to run when there really is material left of the axis -
			// the same rectangle authored on the negative side must revolve into the same solid.
			var mirrored = RectangleProfile(-15, -5, 10).Revolve(30);
			var direct = RectangleProfile(5, 15, 10).Revolve(30);

			await Assert.That(mirrored.Faces.Count).IsEqualTo(direct.Faces.Count);

			var mirroredBounds = mirrored.GetAxisAlignedBoundingBox();
			var directBounds = direct.GetAxisAlignedBoundingBox();
			await Assert.That(mirroredBounds.XSize).IsEqualTo(directBounds.XSize).Within(.001);
			await Assert.That(mirroredBounds.ZSize).IsEqualTo(directBounds.ZSize).Within(.001);
		}
	}
}
