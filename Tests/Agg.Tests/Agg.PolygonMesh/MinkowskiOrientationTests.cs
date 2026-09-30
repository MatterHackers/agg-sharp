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
using System.Threading;
using System.Threading.Tasks;
using ManifoldSharp;
using ManifoldSharp.Linalg;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// Dilate and Erode read a part saved inside out (every face pointing inward) as the solid
	/// it looks like, while a hollow part's correctly wound cavity stays a cavity.
	/// </summary>
	/// <remarks>
	/// Thingi10K 95430 is such a part: before the repair its Dilate came back at 0.947 from a
	/// 7.156 part, because the kernel read the inverted shell as a void and dilated that.
	/// </remarks>
	public class MinkowskiOrientationTests
	{
		private const double OuterSide = 10;

		private const double CavitySide = 4;

		/// <summary>
		/// The hollow box's Dilate volume by <see cref="Ball"/> as measured before the repair
		/// existed, pinned so the repair provably leaves a correctly wound cavity alone.
		/// </summary>
		private const double HollowDilatedVolume = 1679.7955980304591;

		[Test]
		public async Task AnInsideOutCubeDilatesAndErodesAsTheCubeItLooksLike()
		{
			var insideOut = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			insideOut.ReverseFaces();

			var dilated = MinkowskiProcessing.MinkowskiSum(insideOut, Ball());
			var eroded = MinkowskiProcessing.MinkowskiDifference(insideOut, Ball());

			double cube = OuterSide * OuterSide * OuterSide;
			await Assert.That(SignedVolume(dilated)).IsGreaterThan(cube)
				.Because("a Dilate never shrinks a part; the inverted cube is still a cube to the user");
			var correctEroded = MinkowskiProcessing.MinkowskiDifference(PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide), Ball());
			await Assert.That(SignedVolume(eroded)).IsEqualTo(SignedVolume(correctEroded)).Within(1e-6)
				.Because("eroding a 10 mm cube by 1 mm leaves an 8 mm cube, not nothing");
			await Assert.That(SignedVolume(eroded)).IsGreaterThan(0.0);
		}

		[Test]
		public async Task AnInsideOutToolWorksLikeTheCorrectTool()
		{
			var cube = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			var insideOutBall = Ball();
			insideOutBall.ReverseFaces();

			await Assert.That(SignedVolume(MinkowskiProcessing.MinkowskiSum(cube, insideOutBall)))
				.IsEqualTo(SignedVolume(MinkowskiProcessing.MinkowskiSum(cube, Ball()))).Within(1e-6);
		}

		[Test]
		public async Task OnlyTheInsideOutOneOfTwoSeparateSolidsIsRewound()
		{
			var mixed = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			var insideOut = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			insideOut.ReverseFaces();
			mixed.CopyAllFaces(insideOut, Matrix4X4.CreateTranslation(20, 0, 0));

			var correct = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			correct.CopyAllFaces(PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide), Matrix4X4.CreateTranslation(20, 0, 0));

			await Assert.That(SignedVolume(MinkowskiProcessing.MinkowskiSum(mixed, Ball())))
				.IsEqualTo(SignedVolume(MinkowskiProcessing.MinkowskiSum(correct, Ball()))).Within(1e-6);
		}

		[Test]
		public async Task AnEntirelyInsideOutThreeLevelNestIsTreatedAsTheNest()
		{
			var insideOut = ThreeLevelNest();
			insideOut.ReverseFaces();

			// Relative 1e-8 rather than bit-equal: rewound triangles list their corners in another
			// order, so the kernel's sums round differently (4e-6 of 9041 measured).
			await Assert.That(SignedVolume(MinkowskiProcessing.MinkowskiSum(insideOut, Ball())))
				.IsEqualTo(SignedVolume(MinkowskiProcessing.MinkowskiSum(ThreeLevelNest(), Ball()))).Within(1e-4);
			await Assert.That(SignedVolume(MinkowskiProcessing.MinkowskiDifference(insideOut, Ball())))
				.IsEqualTo(SignedVolume(MinkowskiProcessing.MinkowskiDifference(ThreeLevelNest(), Ball()))).Within(1e-4);
		}

		/// <summary>
		/// The repair is skipped exactly when no shell is inverted, which is the case in which
		/// it cannot change anything.
		/// </summary>
		[Test]
		public async Task TheRepairRunsOnlyWhenAShellIsInverted()
		{
			var insideOut = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			insideOut.ReverseFaces();

			await Assert.That(MinkowskiShellOrientation.MayHaveInvertedShell(ManifoldKernel.Import(PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide), false))).IsFalse();
			await Assert.That(MinkowskiShellOrientation.MayHaveInvertedShell(ManifoldKernel.Import(insideOut, false))).IsTrue();
			await Assert.That(MinkowskiShellOrientation.MayHaveInvertedShell(ManifoldKernel.Import(HollowBox(), false))).IsTrue()
				.Because("a cavity winds inward; the repair has to see it to judge its nesting");
		}

		/// <summary>
		/// A patched dilation's bar maps the kernel's units through the tree's real hull count:
		/// every leaf-stage report is a whole number of the kernel's leaf-stage units. Mapped
		/// through the triangle count instead, the reports fall between units.
		/// </summary>
		[Test]
		public async Task APatchedDilationsBarFollowsTheKernelsHullCount()
		{
			var solid = Manifold.Cube(new Vec3(20, 20, 20), true)
				- Manifold.Cube(new Vec3(10, 10, 10), false)
				- Manifold.Sphere(6, 24).Translate(new Vec3(-10, 0, 0));
			ulong phaseTotal = 0;
			ManifoldSharp.ProgressReporter kernelReporter = null;
			kernelReporter = new ManifoldSharp.ProgressReporter((phase, fraction) => phaseTotal = kernelReporter.PhaseTotal);
			await Assert.That(solid.TryDilateByConvex(Manifold.Sphere(1, 8), null, kernelReporter, out _)).IsTrue();
			long hulls = MinkowskiProgressModel.TreeHullUnits((long)phaseTotal);
			await Assert.That(hulls).IsLessThan((long)solid.NumTri());
			long leafStageUnits = hulls + (hulls + MinkowskiProgressModel.TreeLeafSize - 1) / MinkowskiProgressModel.TreeLeafSize + 1;

			var ratios = new List<double>();
			var bar = new MatterHackers.Agg.ProgressReporter((ratio, message) =>
			{
				lock (ratios)
				{
					ratios.Add(ratio);
				}
			});
			await MinkowskiProcessing.MinkowskiSumAsync(ManifoldKernel.ToMesh(solid, "solid"), MinkowskiProcessing.SphereMesh(1, 8), bar, CancellationToken.None);

			int leafReports = 0;
			foreach (double ratio in ratios)
			{
				if (ratio > 0 && ratio < MinkowskiProgressModel.TreeLeafTimeShare)
				{
					double units = ratio / MinkowskiProgressModel.TreeLeafTimeShare * leafStageUnits;
					await Assert.That(units).IsEqualTo(System.Math.Round(units)).Within(1e-6);
					leafReports++;
				}
			}

			await Assert.That(leafReports).IsGreaterThan(10);
		}

		[Test]
		public async Task AHollowBoxWithACorrectlyWoundCavityKeepsItsCavity()
		{
			var dilated = MinkowskiProcessing.MinkowskiSum(HollowBox(), Ball());

			await Assert.That(SignedVolume(dilated)).IsEqualTo(HollowDilatedVolume).Within(1e-6)
				.Because("an inward-wound shell inside a solid is a cavity, not a part saved inside out");
		}

		[Test]
		public async Task AnEntirelyInsideOutHollowBoxIsTreatedAsTheHollowBox()
		{
			var insideOut = HollowBox();
			insideOut.ReverseFaces();

			var dilated = MinkowskiProcessing.MinkowskiSum(insideOut, Ball());
			var eroded = MinkowskiProcessing.MinkowskiDifference(insideOut, Ball());
			var hollowEroded = MinkowskiProcessing.MinkowskiDifference(HollowBox(), Ball());

			await Assert.That(SignedVolume(dilated)).IsEqualTo(HollowDilatedVolume).Within(1e-6);
			await Assert.That(SignedVolume(eroded)).IsEqualTo(SignedVolume(hollowEroded)).Within(1e-6);
		}

		/// <summary>
		/// The public orientation returns the operand the way the kernel reads it: an inside-out body
		/// beside a correct one is turned, and a correctly wound cavity is left a cavity.
		/// </summary>
		[Test]
		public async Task OrientShellsAsSolidTurnsOnlyTheInsideOutShells()
		{
			var mixed = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			var insideOut = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			insideOut.ReverseFaces();
			mixed.CopyAllFaces(insideOut, Matrix4X4.CreateTranslation(20, 0, 0));
			await Assert.That(SignedVolume(MinkowskiProcessing.OrientShellsAsSolid(mixed, CancellationToken.None)))
				.IsEqualTo(2 * OuterSide * OuterSide * OuterSide).Within(1e-6);

			var hollow = HollowBox();
			await Assert.That(SignedVolume(MinkowskiProcessing.OrientShellsAsSolid(hollow, CancellationToken.None)))
				.IsEqualTo(SignedVolume(HollowBox())).Within(1e-6);
		}

		private static Mesh Ball()
		{
			return MinkowskiProcessing.SphereMesh(1.0, 8);
		}

		/// <summary>
		/// A 10 mm cube with a 4 mm cavity at its centre, the cavity's faces pointing into it.
		/// </summary>
		private static Mesh HollowBox()
		{
			var box = PlatonicSolids.CreateCube(OuterSide, OuterSide, OuterSide);
			var cavity = PlatonicSolids.CreateCube(CavitySide, CavitySide, CavitySide);
			cavity.ReverseFaces();
			box.CopyAllFaces(cavity, Matrix4X4.Identity);

			return box;
		}

		/// <summary>
		/// A 20 mm cube holding a 14 mm cavity holding a 4 mm cube, each wound as its nesting demands.
		/// </summary>
		private static Mesh ThreeLevelNest()
		{
			var nest = PlatonicSolids.CreateCube(20, 20, 20);
			var cavity = PlatonicSolids.CreateCube(14, 14, 14);
			cavity.ReverseFaces();
			nest.CopyAllFaces(cavity, Matrix4X4.Identity);
			nest.CopyAllFaces(PlatonicSolids.CreateCube(4, 4, 4), Matrix4X4.Identity);

			return nest;
		}

		/// <summary>
		/// The divergence-theorem volume of a closed mesh, positive when its faces wind outward.
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
	}
}
