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
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using ManifoldRust;
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The ManifoldRust boolean backend - the only boolean engine there is - exercised
	/// through the same public <see cref="BooleanProcessing"/> entry points the
	/// application uses.
	/// </summary>
	/// <remarks>
	/// There is no managed fallback behind these entry points any more, so a kernel failure
	/// is an exception the caller sees rather than a quietly different mesh. That is what
	/// makes the geometry assertions here meaningful without also asserting which engine ran.
	/// </remarks>
	public class ManifoldRustBackendTests
	{
		private static Mesh UnionSubtractIntersect(CsgModes operation, Mesh a, Matrix4X4 matrixA, Mesh b, Matrix4X4 matrixB)
		{
			var result = BooleanProcessing.DoArray(
				new[] { (a, matrixA), (b, matrixB) },
				operation,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None);

			// What CombineParticipants does to a boolean result before anyone looks at it.
			// The raw MeshGL output splits vertices at run boundaries, so topology questions
			// are only meaningful after the merge.
			result.CleanAndMerge();
			result.RemoveUnusedVertices();

			return result;
		}

		[Test]
		public async Task UnionOfOverlappingCubesIsClosedSolid()
		{
			var result = UnionSubtractIntersect(
				CsgModes.Union,
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(-3, 0, 0),
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0));

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();

			// The union of the two boxes is one box spanning both.
			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(16.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(10.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(10.0).Within(0.001);
		}

		[Test]
		public async Task SubtractOfOverlappingCubesIsClosedSolid()
		{
			var result = UnionSubtractIntersect(
				CsgModes.Subtract,
				PlatonicSolids.CreateCube(20, 20, 20), Matrix4X4.Identity,
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(10, 10, 10));

			// A corner bite out of a cube: still one closed solid, and more faces than the
			// 12 the cube started with.
			await Assert.That(result.Faces.Count).IsGreaterThan(12);
			await Assert.That(result.IsManifold()).IsTrue();

			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(20.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(20.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(20.0).Within(0.001);
		}

		[Test]
		public async Task IntersectOfOverlappingCubesIsTheOverlapBox()
		{
			var result = UnionSubtractIntersect(
				CsgModes.Intersect,
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(-3, 0, 0),
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0));

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();

			// The overlap of the two boxes is a 4 x 10 x 10 box.
			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(4.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(10.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(10.0).Within(0.001);
		}

		/// <summary>
		/// Closed but non-manifold geometry is the kernel's job: the robust import accepts
		/// it and the Auto engine switches to the robust boolean for it. With no fallback
		/// left, returning at all is the assertion - anything the kernel refuses throws.
		/// </summary>
		[Test]
		public async Task ClosedNonManifoldInputStillRunsOnTheKernel()
		{
			var pinched = TwoCubesSharingOneEdge();

			// The whole point of the test is the input the old IsManifold pre-gate would
			// have diverted: non-manifold, but with no boundary edges to be open at.
			await Assert.That(pinched.IsManifold()).IsFalse();
			await Assert.That(pinched.GetNonManifoldEdges().All(e => e.Faces.Count() == 4)).IsTrue()
				.Because("a closed surface has no edge with fewer than two faces");

			var result = BooleanProcessing.DoArray(
				new[]
				{
					(pinched, Matrix4X4.Identity),
					// Straddles the pinch, so the union is one honest solid rather than
					// two lobes still touching along a single edge.
					(PlatonicSolids.CreateCube(10, 10, 20), Matrix4X4.CreateTranslation(5, 5, 0)),
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None);

			result.CleanAndMerge();
			result.RemoveUnusedVertices();

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();
		}

		/// <summary>
		/// Colour tracking must not turn a soup operand into a failure. A soup handle cannot
		/// be re-tagged as an original, so <see cref="BooleanProcessing"/> keeps the plain
		/// import and that operand's faces arrive under a run it does not own - its colours
		/// degrade. Degrading is fine; throwing, or handing back a FaceColors array that does
		/// not line up with the faces, is not. Asking for colours must not be what makes the
		/// kernel refuse an operand.
		/// </summary>
		[Test]
		public async Task SoupOperandWithColorsStillRunsOnTheKernel()
		{
			var pinched = TwoCubesSharingOneEdge();

			var result = BooleanProcessing.DoArray(
				new[]
				{
					(pinched, Matrix4X4.Identity),
					// Straddles the pinch, so the union is one honest solid rather than
					// two lobes still touching along a single edge.
					(PlatonicSolids.CreateCube(10, 10, 20), Matrix4X4.CreateTranslation(5, 5, 0)),
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None,
				meshColors: new[] { Color.Red, Color.Blue });

			result.CleanAndMerge();
			result.RemoveUnusedVertices();

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();

			// Colours may be lost for the soup operand, but whatever comes back has to be a
			// usable parallel array - a mismatched length is what corrupts rendering later.
			if (result.FaceColors != null)
			{
				await Assert.That(result.FaceColors.Length).IsEqualTo(result.Faces.Count);
			}
		}

		/// <summary>
		/// An input the kernel cannot accept reaches the caller as an exception. There is no
		/// managed fallback to rewrite it into a mesh built by different rules, so the honest
		/// answer is the failure itself. An open surface is such an input: even the robust
		/// import rejects it, because it is not closed.
		/// </summary>
		[Test]
		public async Task OpenMeshThrowsRatherThanSilentlyProducingOtherGeometry()
		{
			var thrown = Assert.Throws<InvalidOperationException>(() => BooleanProcessing.DoArray(
				new[]
				{
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
					(OpenBox(8), Matrix4X4.Identity),
				},
				CsgModes.Subtract,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None));

			await Assert.That(thrown.Message).Contains(ManifoldStatus.NotClosed.ToString())
				.Because("the status is what tells the user which operand was unusable");

			// A subtraction is defined by every operand, so there is nothing to degrade to -
			// this has to be the plain refusal, not a partial result.
			await Assert.That(thrown is PartialBooleanException).IsFalse();
		}

		/// <summary>
		/// A union does have something to degrade to - the union of the operands that worked -
		/// so one unusable operand no longer costs the whole combine. What it must never cost is
		/// the report: the refused operand is named in the exception that carries the partial
		/// result out, so a caller that ignores it still fails loudly rather than quietly
		/// dropping a part.
		/// </summary>
		[Test]
		public async Task AUnionKeepsTheOperandsThatWorkedAndNamesTheOneThatDidNot()
		{
			var partial = Assert.Throws<PartialBooleanException>(() => BooleanProcessing.DoArray(
				new[]
				{
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
					(OpenBox(8), Matrix4X4.CreateTranslation(20, 0, 0)),
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None));

			await Assert.That(partial.SkippedOperands.Count).IsEqualTo(1);
			await Assert.That(partial.SkippedOperands[0].Index).IsEqualTo(1)
				.Because("the caller names the part from its position in the list it handed in");
			await Assert.That(partial.Message).Contains(ManifoldStatus.NotClosed.ToString())
				.Because("a caller that only logs the message must still learn what was left out");

			var result = partial.PartialResult;
			result.CleanAndMerge();
			result.RemoveUnusedVertices();

			await Assert.That(result.IsManifold()).IsTrue();

			// The good operand alone, untouched by the one that could not be used.
			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(10.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(10.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(10.0).Within(0.001);
		}

		/// <summary>
		/// A union in which the kernel refuses every operand still degrades rather than failing
		/// outright. The union of nothing is not geometry, but the caller's answer is not "the
		/// whole build failed" either: it is "these parts could not be used", with the parts
		/// listed, so a caller combining several touching sets keeps the sets that worked and can
		/// keep the refused parts visible itself.
		/// </summary>
		[Test]
		public async Task AUnionWhoseOperandsAreAllRefusedStillNamesThemAllPartially()
		{
			var partial = Assert.Throws<PartialBooleanException>(() => BooleanProcessing.DoArray(
				new[]
				{
					(OpenBox(8), Matrix4X4.Identity),
					(OpenBox(8), Matrix4X4.CreateTranslation(20, 0, 0)),
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None));

			await Assert.That(partial.SkippedOperands.Count).IsEqualTo(2)
				.Because("every operand was refused, so every one of them has to be named");

			await Assert.That(partial.SkippedOperands.Select(i => i.Index)).IsEquivalentTo(new[] { 0, 1 });

			await Assert.That(partial.Message).Contains(ManifoldStatus.NotClosed.ToString());

			await Assert.That(partial.PartialResult).IsNotNull()
				.Because("the caller copies the refused operands into the partial result, so it needs a mesh to copy into");

			await Assert.That(partial.PartialResult.Faces.Count).IsEqualTo(0)
				.Because("no operand contributed geometry - the answer is empty, not wrong");
		}

		/// <summary>
		/// A solid whose seams came apart in the last digits still unions. The kernel welds by
		/// exact position and has no tolerance of its own, so such a mesh reads as NotClosed to
		/// it even though nothing about it looks open; the import retries with a tolerance-welded
		/// copy rather than refusing the part.
		/// </summary>
		[Test]
		public async Task ASeamSplitByRoundingStillUnions()
		{
			var split = CubeWithSplitSeams(10, 1e-6f);

			// Every triangle owns its own corners, so every edge is a boundary edge - which is
			// exactly what a seam that lost its shared vertices to rounding looks like.
			await Assert.That(split.IsManifold()).IsFalse()
				.Because("the input under test has to be one the kernel would refuse untouched");

			var result = UnionSubtractIntersect(
				CsgModes.Union,
				split, Matrix4X4.CreateTranslation(-3, 0, 0),
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0));

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();

			// The welded operand still has to be the same 10mm cube it looked like.
			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(16.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(10.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(10.0).Within(0.001);
		}

		/// <summary>
		/// The same split-seam solid, out where the coordinates are large, still unions. Positions
		/// are stored as <see cref="Vector3Float"/>, so the rounding that splits a seam scales with
		/// distance from the origin rather than with the part: at x = 5000mm consecutive floats are
		/// ~4.9e-4mm apart, which is several times a weld tolerance scaled only to a 10mm part's
		/// diagonal. A part is no less weldable for having been moved across the bed.
		/// </summary>
		[Test]
		public async Task ASeamSplitByRoundingStillUnionsFarFromTheOrigin()
		{
			const double FarFromOrigin = 5000;

			// Split by nothing but the float grid itself, so the size of the seam gaps is decided
			// by where the part is rather than by a number chosen here. Two steps of that grid is
			// under 1e-3mm out here and welds at the origin without complaint; it is only large
			// against a 10mm part's diagonal, which is what the tolerance used to be scaled to.
			var split = CubeWithSeamsSplitByFloatSpacing(10, new Vector3(FarFromOrigin, 0, 0), ulps: 2);

			await Assert.That(split.IsManifold()).IsFalse()
				.Because("the input under test has to be one the kernel would refuse untouched");

			var result = UnionSubtractIntersect(
				CsgModes.Union,
				split, Matrix4X4.Identity,
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(FarFromOrigin + 6, 0, 0));

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();

			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(16.0).Within(0.01);
			await Assert.That(bounds.YSize).IsEqualTo(10.0).Within(0.01);
			await Assert.That(bounds.ZSize).IsEqualTo(10.0).Within(0.01);
		}

		/// <summary>
		/// A geometry the kernel refuses is refused per operand in a union - whatever it objected
		/// to. A NaN coordinate is as much a property of that one part's geometry as a hole in it
		/// is, and the answer for the user is the same: the other parts combine, and this one is
		/// named. Only a failure that is not the kernel judging geometry - a load or binding
		/// failure, say - is allowed to take the whole operation down, because "run Repair on it"
		/// would be a lie about what went wrong.
		/// </summary>
		[Test]
		public async Task ANonFiniteOperandIsSkippedPerOperandLikeAnyOtherRefusedGeometry()
		{
			var partial = Assert.Throws<PartialBooleanException>(() => BooleanProcessing.DoArray(
				new[]
				{
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
					(CubeWithANonFiniteVertex(), Matrix4X4.CreateTranslation(20, 0, 0)),
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None));

			await Assert.That(partial.SkippedOperands.Count).IsEqualTo(1);
			await Assert.That(partial.SkippedOperands[0].Index).IsEqualTo(1);
			await Assert.That(partial.SkippedOperands[0].Reason).Contains(ManifoldStatus.NonFiniteVertex.ToString())
				.Because("the status is the whole diagnostic value of refusing the input");

			var bounds = partial.PartialResult.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(10.0).Within(0.001)
				.Because("the good operand is the answer, unaffected by the one that was refused");
		}

		/// <summary>
		/// An operand the kernel rejects is thrown on rather than absorbed. A boolean
		/// silently treats an error-status operand as empty geometry and still reports
		/// success, so absorbing it would show up as a part missing from the model with
		/// nothing logged.
		/// </summary>
		[Test]
		public async Task AnInputTheKernelRejectsThrowsRatherThanGoingMissing()
		{
			var items = new[]
			{
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
				(CubeWithANonFiniteVertex(), Matrix4X4.Identity),
			};

			// A NaN coordinate leaves the surface perfectly manifold, so nothing about the
			// topology is wrong and even the robust import has no reason to be lenient - it
			// is the kernel's geometric validation that objects.
			await Assert.That(items[1].Item1.IsManifold()).IsTrue()
				.Because("the rejection under test has to be geometric, not topological");

			var thrown = Assert.Throws<InvalidOperationException>(() => BooleanProcessing.DoArrayViaManifoldRust(
				items,
				CsgModes.Union,
				CancellationToken.None,
				null,
				1,
				0,
				null));

			await Assert.That(thrown.Message).Contains(ManifoldStatus.NonFiniteVertex.ToString())
				.Because("the status is the whole diagnostic value of refusing the input");

			// And the public entry point passes it straight through - the same rejection, not
			// a different mesh built by a second engine.
			var throughDoArray = Assert.Throws<InvalidOperationException>(() => BooleanProcessing.DoArray(
				items,
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None));

			await Assert.That(throughDoArray.Message).Contains(ManifoldStatus.NonFiniteVertex.ToString());
		}

		/// <summary>
		/// Cancellation has to reach the caller as a cancellation, not as some other failure:
		/// the rebuild machinery distinguishes "the user stopped this" from "this broke".
		/// </summary>
		[Test]
		public async Task CancelledTokenPropagatesAsCancellation()
		{
			using var cancelled = new CancellationTokenSource();
			cancelled.Cancel();

			await Assert.That(() => BooleanProcessing.DoArray(
				new[]
				{
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(-3, 0, 0)),
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0)),
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				cancelled.Token)).Throws<OperationCanceledException>();
		}

		/// <summary>
		/// A reporter must see the kernel's progress, and attaching one must not change
		/// the geometry: the progress path runs a pairwise fold rather than BatchBoolean,
		/// and for two operands those have to be the same boolean.
		/// </summary>
		[Test]
		public async Task ReporterSeesProgressWithoutChangingTheResult()
		{
			const double RatioCompleted = 0.25;
			const double AmountPerOperation = 0.5;

			var reported = new List<(double Ratio, string Message)>();

			(Mesh, Matrix4X4)[] Operands() => new[]
			{
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(-3, 0, 0)),
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0)),
			};

			var withReporter = BooleanProcessing.DoArray(
				Operands(),
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				(ratio, message) =>
				{
					// The kernel's callback can arrive on a native worker thread, so the list
					// it lands in has to be guarded even though it is never re-entered.
					lock (reported)
					{
						reported.Add((ratio, message));
					}
				},
				CancellationToken.None,
				AmountPerOperation,
				RatioCompleted);

			await Assert.That(reported.Count).IsGreaterThan(0)
				.Because("a boolean that reports nothing leaves the progress bar frozen");

			double previous = RatioCompleted;
			foreach (var (ratio, message) in reported)
			{
				await Assert.That(ratio).IsGreaterThanOrEqualTo(RatioCompleted);
				await Assert.That(ratio).IsLessThanOrEqualTo(RatioCompleted + AmountPerOperation);

				// A bar that goes backwards is worse than no bar; the kernel's fraction
				// restarts at every phase, so the adapter has to enforce this.
				await Assert.That(ratio).IsGreaterThanOrEqualTo(previous);
				previous = ratio;

				await Assert.That(string.IsNullOrEmpty(message)).IsFalse();
			}

			var withoutReporter = BooleanProcessing.DoArray(
				Operands(),
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None,
				AmountPerOperation,
				RatioCompleted);

			await Assert.That(withReporter.Vertices.Count).IsEqualTo(withoutReporter.Vertices.Count);
			await Assert.That(withReporter.Faces.Count).IsEqualTo(withoutReporter.Faces.Count);
			await Assert.That(SignedVolume(withReporter)).IsEqualTo(SignedVolume(withoutReporter)).Within(1e-9);
		}

		/// <summary>
		/// The winding rule has to reach the kernel. An operand carrying an inside-out
		/// shell loses that shell's material under Positive - it winds to -1 - and keeps
		/// it under Nonzero.
		/// </summary>
		[Test]
		public async Task NonzeroWindingKeepsInvertedMaterialThatPositiveDiscards()
		{
			// Two operands, not one: a single operand is its own answer and no boolean -
			// and so no winding rule - ever runs.
			(Mesh, Matrix4X4)[] Operands() => new[]
			{
				(OutwardCubeWithAnInvertedNeighbour(), Matrix4X4.Identity),
				(PlatonicSolids.CreateCube(4, 4, 4), Matrix4X4.CreateTranslation(40, 0, 0)),
			};

			double VolumeUnder(WindingRule rule)
			{
				var result = BooleanProcessing.DoArray(
					Operands(),
					CsgModes.Union,
					ProcessingModes.Polygons,
					ProcessingResolution._64,
					ProcessingResolution._64,
					null,
					CancellationToken.None,
					windingRule: rule);

				return SignedVolume(result);
			}

			var positive = VolumeUnder(WindingRule.Positive);
			var nonzero = VolumeUnder(WindingRule.Nonzero);

			// Positive keeps only the outward cube less the region the inverted one cancels;
			// Nonzero keeps the inverted cube's own body as material too.
			await Assert.That(positive).IsGreaterThan(0.0);
			await Assert.That(nonzero).IsGreaterThan(positive);
		}

		/// <summary>
		/// Repairing orientation is the other answer to inside-out input: it rewinds the
		/// operand once, so the default Positive rule keeps working and the union is the
		/// one the correctly wound operand would have produced.
		/// </summary>
		[Test]
		public async Task RepairOrientationMakesAnInvertedOperandUnionLikeACorrectOne()
		{
			var inverted = PlatonicSolids.CreateCube(10, 10, 10);
			inverted.ReverseFaces();

			double UnionVolume(Mesh first, bool repairOrientation)
			{
				var result = BooleanProcessing.DoArray(
					new[]
					{
						(first, Matrix4X4.CreateTranslation(-3, 0, 0)),
						(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0)),
					},
					CsgModes.Union,
					ProcessingModes.Polygons,
					ProcessingResolution._64,
					ProcessingResolution._64,
					null,
					CancellationToken.None,
					repairOrientation: repairOrientation);

				return SignedVolume(result);
			}

			var repaired = UnionVolume(inverted, repairOrientation: true);
			var correct = UnionVolume(PlatonicSolids.CreateCube(10, 10, 10), repairOrientation: false);

			// 16 x 10 x 10 either way - the repair is the whole difference between the
			// inverted operand contributing its body and contributing nothing.
			await Assert.That(correct).IsEqualTo(1600.0).Within(0.001);
			await Assert.That(repaired).IsEqualTo(correct).Within(0.001);
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
		/// One mesh holding two cubes that overlap, the second of them wound inside out.
		/// Self-intersecting, so it imports as soup and runs on the robust engine - which
		/// is the only engine the winding rule means anything to.
		/// </summary>
		private static Mesh OutwardCubeWithAnInvertedNeighbour()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var inverted = PlatonicSolids.CreateCube(10, 10, 10);
			inverted.Transform(Matrix4X4.CreateTranslation(5, 5, 5));
			inverted.ReverseFaces();

			int offset = mesh.Vertices.Count;
			foreach (var vertex in inverted.Vertices)
			{
				mesh.Vertices.Add(vertex);
			}

			foreach (var face in inverted.Faces)
			{
				mesh.Faces.Add(new Face(face.v0 + offset, face.v1 + offset, face.v2 + offset, mesh.Vertices));
			}

			return mesh;
		}

		/// <summary>
		/// Two cubes welded into one mesh so that they meet along exactly one shared edge -
		/// four faces on that edge, so not manifold, but no boundary edges, so still closed
		/// and orientable. Precisely the shape the robust import exists for.
		/// </summary>
		private static Mesh TwoCubesSharingOneEdge()
		{
			var mesh = new Mesh();
			var vertexMap = new Dictionary<Vector3Float, int>();

			void Append(Matrix4X4 matrix)
			{
				var cube = PlatonicSolids.CreateCube(10, 10, 10);
				cube.Transform(matrix);

				int Weld(int sourceIndex)
				{
					var position = cube.Vertices[sourceIndex];
					if (!vertexMap.TryGetValue(position, out int index))
					{
						index = mesh.Vertices.Count;
						mesh.Vertices.Add(position);
						vertexMap[position] = index;
					}

					return index;
				}

				foreach (var face in cube.Faces)
				{
					mesh.Faces.Add(new Face(Weld(face.v0), Weld(face.v1), Weld(face.v2), mesh.Vertices));
				}
			}

			// [-5, 5] and [5, 15] in both x and y: the two cubes touch only along the
			// vertical line x = 5, y = 5, and welding makes that one shared mesh edge.
			Append(Matrix4X4.Identity);
			Append(Matrix4X4.CreateTranslation(10, 10, 0));

			return mesh;
		}

		/// <summary>
		/// A box missing its top face - not closed, so even the robust import rejects it
		/// (NotClosed) and DoArray throws.
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

		/// <summary>
		/// A cube whose every triangle carries its own copy of its corners, each nudged by up to
		/// <paramref name="jitter"/>. Geometrically the same cube; topologically 36 loose
		/// triangles, which is what a seam looks like after float storage and a transform or two
		/// have split the shared vertices apart.
		/// </summary>
		private static Mesh CubeWithSplitSeams(double size, float jitter)
		{
			var cube = PlatonicSolids.CreateCube(size, size, size);
			var split = new Mesh();

			// Deterministic, so a failure here is always the same failure.
			var random = new Random(12345);

			float Nudge() => (float)((random.NextDouble() - 0.5) * 2 * jitter);

			Vector3Float Perturbed(Vector3Float position)
			{
				return new Vector3Float(position.X + Nudge(), position.Y + Nudge(), position.Z + Nudge());
			}

			foreach (var face in cube.Faces)
			{
				int start = split.Vertices.Count;
				split.Vertices.Add(Perturbed(cube.Vertices[face.v0]));
				split.Vertices.Add(Perturbed(cube.Vertices[face.v1]));
				split.Vertices.Add(Perturbed(cube.Vertices[face.v2]));
				split.Faces.Add(new Face(start, start + 1, start + 2, split.Vertices));
			}

			return split;
		}

		/// <summary>
		/// A cube at <paramref name="at"/> whose every triangle carries its own copy of its corners,
		/// each corner nudged <paramref name="ulps"/> steps along the <c>float</c> grid. The seams
		/// are split by exactly the error the storage introduces and nothing else, so how far apart
		/// the copies land follows the distance from the origin: a rounding error at the origin,
		/// ~4.9e-4mm per step out at x = 5000mm, whatever the part's own size is.
		/// </summary>
		private static Mesh CubeWithSeamsSplitByFloatSpacing(double size, Vector3 at, int ulps = 1)
		{
			var cube = PlatonicSolids.CreateCube(size, size, size);
			cube.Transform(Matrix4X4.CreateTranslation(at));

			var split = new Mesh();

			// Deterministic, so a failure here is always the same failure.
			var random = new Random(12345);

			float Nudged(float value)
			{
				switch (random.Next(3))
				{
					case 0:
						for (int i = 0; i < ulps; i++)
						{
							value = MathF.BitDecrement(value);
						}

						return value;

					case 1:
						for (int i = 0; i < ulps; i++)
						{
							value = MathF.BitIncrement(value);
						}

						return value;

					default:
						return value;
				}
			}

			Vector3Float Perturbed(Vector3Float position)
			{
				return new Vector3Float(Nudged(position.X), Nudged(position.Y), Nudged(position.Z));
			}

			foreach (var face in cube.Faces)
			{
				int start = split.Vertices.Count;
				split.Vertices.Add(Perturbed(cube.Vertices[face.v0]));
				split.Vertices.Add(Perturbed(cube.Vertices[face.v1]));
				split.Vertices.Add(Perturbed(cube.Vertices[face.v2]));
				split.Faces.Add(new Face(start, start + 1, start + 2, split.Vertices));
			}

			return split;
		}

		/// <summary>
		/// A cube whose surface is still closed but one of whose corners is not a number.
		/// Topologically perfect, geometrically meaningless - which is how it slips past
		/// <see cref="MeshExtensionMethods.IsManifold"/> and lands on the kernel.
		/// </summary>
		private static Mesh CubeWithANonFiniteVertex()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var corner = mesh.Vertices[0];
			mesh.Vertices[0] = new Vector3Float(float.NaN, corner.Y, corner.Z);

			return mesh;
		}
	}
}
