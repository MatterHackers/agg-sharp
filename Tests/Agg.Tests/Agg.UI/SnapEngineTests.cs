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
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// Port of agg-gui's snap/tests.rs, inputs and expectations unchanged. Rectangles are written as agg-gui
	/// writes them, (x, y, width, height) with y up. Pure-spacing tests call <see cref="SnapSpacing"/>
	/// directly so an incidental edge alignment in the scene can't suppress the spacing under test.
	/// </summary>
	public class SnapEngineTests
	{
		private static RectangleDouble Rect(double x, double y, double w, double h) => new RectangleDouble(x, y, x + w, y + h);

		private static (SnapId, RectangleDouble) Target(ulong id, double x, double y, double w, double h) => (new SnapId(id), Rect(x, y, w, h));

		private static List<RectangleDouble> RectsOnly(params (SnapId, RectangleDouble)[] targets) => targets.Select(t => t.Item2).ToList();

		private static SnapResult Snap(RectangleDouble moving, double threshold, SnapMode mode, params (SnapId, RectangleDouble)[] targets)
		{
			return SnapEngine.ComputeSnap(moving, new SnapId(1), targets, threshold, mode);
		}

		private static int Count(IEnumerable<SnapGuide> guides, SnapGuideKind kind) => guides.Count(g => g.Kind == kind);

		[Test]
		public async Task LeftEdgeSnapsToTargetLeftWithinThreshold()
		{
			// Moving at x=103 (3 px right of the target's x=100, threshold 8).
			var result = Snap(Rect(103, 50, 60, 40), 8, SnapMode.Move, Target(2, 100, 200, 80, 40));
			await Assert.That(Math.Abs(result.Bounds.Left - 100) < 1e-9).IsTrue();
			await Assert.That(result.Guides.Any(g => g.Kind == SnapGuideKind.VLine && Math.Abs(g.At - 100) < 1e-9)).IsTrue();
		}

		[Test]
		public async Task OutsideThresholdDoesNotSnap()
		{
			// Every edge / centre pair is far more than 8 px apart.
			var result = Snap(Rect(120, 50, 60, 40), 8, SnapMode.Move, Target(2, 300, 200, 60, 40));
			await Assert.That(result.Bounds.Left).IsEqualTo(120.0);
			await Assert.That(result.Guides.Count).IsEqualTo(0);
		}

		[Test]
		public async Task SelfIdExcludedFromTargets()
		{
			// Callers commonly pass the whole scene, dragger included; it must not snap to itself.
			var moving = Rect(103, 50, 60, 40);
			var result = Snap(moving, 8, SnapMode.Move, (new SnapId(1), moving));
			await Assert.That(result.Bounds.Left).IsEqualTo(103.0);
			await Assert.That(result.Guides.Count).IsEqualTo(0);
		}

		[Test]
		public async Task CenterXAlignsToTargetCenter()
		{
			// Moving cx 130, target 82..182 (cx 132): only centre-to-centre (+2) is within 8 px.
			var result = Snap(Rect(100, 50, 60, 40), 8, SnapMode.Move, Target(2, 82, 200, 100, 40));
			double newCenter = result.Bounds.Left + result.Bounds.Width * 0.5;
			await Assert.That(Math.Abs(newCenter - 132) < 1e-9).IsTrue();
		}

		[Test]
		public async Task TopEdgeSnapsInYUpCoordinates()
		{
			// Y-up: top = y + height. Moving top 97 snaps to target top 100.
			var result = Snap(Rect(0, 47, 50, 50), 8, SnapMode.Move, Target(2, 200, 50, 50, 50));
			await Assert.That(Math.Abs(result.Bounds.Top - 100) < 1e-9).IsTrue();
		}

		[Test]
		public async Task HorizontalEqualSpacingCentersBetweenNeighbours()
		{
			// L right = 100, R left = 200; a 40-wide rect centres at left 130.
			var moving = Rect(132, 55, 40, 30);
			var m = SnapSpacing.HorizontalEqualSpacing(moving, RectsOnly(Target(2, 40, 50, 60, 40), Target(3, 200, 50, 60, 40)), 8);
			await Assert.That(m).IsNotNull();
			await Assert.That(Math.Abs(moving.Left + m.Delta - 130) < 1e-9).IsTrue();
			await Assert.That(Count(m.Guides, SnapGuideKind.HSpacing) > 0).IsTrue();
		}

		[Test]
		public async Task VerticalEqualSpacingCentersBetweenNeighbours()
		{
			// Bottom neighbour top at 80, top neighbour bottom at 200; 2 px off the symmetric bottom 125.
			var moving = Rect(55, 127, 60, 30);
			var m = SnapSpacing.VerticalEqualSpacing(moving, RectsOnly(Target(2, 50, 40, 60, 40), Target(3, 50, 200, 60, 40)), 8);
			await Assert.That(m).IsNotNull();
			await Assert.That(Math.Abs(moving.Bottom + m.Delta - 125) < 1e-9).IsTrue();
			await Assert.That(Count(m.Guides, SnapGuideKind.VSpacing) > 0).IsTrue();
		}

		[Test]
		public async Task ResizeEastOnlySnapsRightEdge()
		{
			// The left edge is in range but resize-East must not snap it; the right edge (163 vs 180) is not.
			var moving = Rect(103, 50, 60, 40);
			var result = Snap(moving, 8, SnapMode.Resize(ResizeEdge.East), Target(2, 100, 200, 80, 40));
			await Assert.That(result.Bounds == moving).IsTrue();
		}

		[Test]
		public async Task ResizeEastSnapsRightEdgeToTargetRight()
		{
			// Right 178 grows to the target's right 180; the left edge stays put.
			var result = Snap(Rect(100, 50, 78, 40), 8, SnapMode.Resize(ResizeEdge.East), Target(2, 40, 200, 140, 40));
			await Assert.That(Math.Abs(result.Bounds.Left - 100) < 1e-9).IsTrue();
			await Assert.That(Math.Abs(result.Bounds.Right - 180) < 1e-9).IsTrue();
		}

		[Test]
		public async Task ResizeEastSpacingMatchesReferenceGap()
		{
			// P 0..60, Q 100..160 (gap 40), R 260..320. Moving 180..217 dragged east: gap to R of 40 wants
			// right = 220 (delta +3). Rows offset in y so no edge snap suppresses the spacing.
			var result = Snap(
				Rect(180, 80, 37, 30),
				8,
				SnapMode.Resize(ResizeEdge.East),
				Target(2, 0, 0, 60, 200),
				Target(3, 100, 0, 60, 200),
				Target(4, 260, 0, 60, 200));
			await Assert.That(Math.Abs(result.Bounds.Right - 220) < 1e-6).IsTrue();
			await Assert.That(Math.Abs(result.Bounds.Left - 180) < 1e-6).IsTrue();
			await Assert.That(Count(result.Guides, SnapGuideKind.HSpacing)).IsEqualTo(2);
		}

		[Test]
		public async Task SandwichSpacingEmitsTwoFlankingGuides()
		{
			// One line from L.right to R.left would cross the moving rect; there must be one per gap.
			var moving = Rect(132, 55, 40, 30);
			var m = SnapSpacing.HorizontalEqualSpacing(moving, RectsOnly(Target(2, 40, 50, 60, 40), Target(3, 200, 50, 60, 40)), 8);
			await Assert.That(m).IsNotNull();
			var hGuides = m.Guides.Where(g => g.Kind == SnapGuideKind.HSpacing).ToList();
			await Assert.That(hGuides.Count).IsEqualTo(2);
			// Snapped left = 130: the gaps are 100..130 and 170..200.
			await Assert.That(hGuides.Any(g => Math.Abs(g.From - 100) < 1e-6 && Math.Abs(g.To - 130) < 1e-6)).IsTrue();
			await Assert.That(hGuides.Any(g => Math.Abs(g.From - 170) < 1e-6 && Math.Abs(g.To - 200) < 1e-6)).IsTrue();
		}

		[Test]
		public async Task EmptyTargetsReturnsInputRectUnchanged()
		{
			var moving = Rect(10, 20, 30, 40);
			var result = Snap(moving, 8, SnapMode.Move);
			await Assert.That(result.Bounds == moving).IsTrue();
			await Assert.That(result.Guides.Count).IsEqualTo(0);
		}

		[Test]
		public async Task SmallestDeltaWinsWhenMultipleTargetsInThreshold()
		{
			// Far left = 98 (delta -5), near left = 104 (delta +1).
			var result = Snap(Rect(103, 50, 60, 40), 8, SnapMode.Move, Target(2, 98, 200, 60, 40), Target(3, 104, 200, 60, 40));
			await Assert.That(Math.Abs(result.Bounds.Left - 104) < 1e-9).IsTrue();
		}

		[Test]
		public async Task HorizontalChainExtensionMatchesReferenceGapRightward()
		{
			// B 0..60, A 100..160 (gap 40): moving right of A wants left = 200.
			var moving = Rect(203, 55, 40, 30);
			var m = SnapSpacing.HorizontalEqualSpacing(moving, RectsOnly(Target(2, 0, 50, 60, 40), Target(3, 100, 50, 60, 40)), 8);
			await Assert.That(m).IsNotNull();
			await Assert.That(Math.Abs(moving.Left + m.Delta - 200) < 1e-9).IsTrue();
			await Assert.That(Count(m.Guides, SnapGuideKind.HSpacing)).IsEqualTo(2);
		}

		[Test]
		public async Task HorizontalChainExtensionMatchesReferenceGapLeftward()
		{
			// A 100..160, B 200..260 (gap 40): moving left of A wants right = 60.
			var moving = Rect(15, 55, 40, 30);
			var m = SnapSpacing.HorizontalEqualSpacing(moving, RectsOnly(Target(2, 100, 50, 60, 40), Target(3, 200, 50, 60, 40)), 8);
			await Assert.That(m).IsNotNull();
			double snappedRight = moving.Left + m.Delta + moving.Width;
			await Assert.That(Math.Abs(snappedRight - 60) < 1e-9).IsTrue();
		}

		[Test]
		public async Task VerticalChainExtensionMatchesReferenceGapUpward()
		{
			// B top 40, A 80..120 (gap 40): moving above A wants bottom = 160.
			var moving = Rect(55, 163, 50, 30);
			var m = SnapSpacing.VerticalEqualSpacing(moving, RectsOnly(Target(2, 50, 0, 60, 40), Target(3, 50, 80, 60, 40)), 8);
			await Assert.That(m).IsNotNull();
			await Assert.That(Math.Abs(moving.Bottom + m.Delta - 160) < 1e-9).IsTrue();
		}

		[Test]
		public async Task VerticalCrossPairReferenceGapMatches()
		{
			// The right column's gap (400 - 300 = 100) is matched below left-top, in a different column.
			var moving = Rect(0, 203, 200, 100);
			var m = SnapSpacing.VerticalEqualSpacing(
				moving,
				RectsOnly(Target(2, 0, 400, 200, 100), Target(3, 300, 400, 200, 100), Target(4, 300, 200, 200, 100)),
				8);
			await Assert.That(m).IsNotNull();
			await Assert.That(Math.Abs(moving.Bottom + m.Delta - 200) < 1e-9).IsTrue();
			await Assert.That(Count(m.Guides, SnapGuideKind.VSpacing)).IsEqualTo(2);
		}

		[Test]
		public async Task CloserReferencePairWinsWhenMultipleMatch()
		{
			// A far reference pair (y ~ 820) and a near one (y ~ 70) both fit; the near one must be shown.
			var moving = Rect(303, 60, 40, 30);
			var m = SnapSpacing.HorizontalEqualSpacing(
				moving,
				RectsOnly(
					Target(2, 0, 800, 60, 40),
					Target(3, 100, 800, 60, 40),
					Target(4, 0, 50, 60, 40),
					Target(5, 100, 50, 60, 40),
					Target(6, 200, 55, 60, 40)),
				8);
			await Assert.That(m).IsNotNull();
			await Assert.That(Math.Abs(moving.Left + m.Delta - 300) < 1e-9).IsTrue();
			var reference = m.Guides.First(g => g.Kind == SnapGuideKind.HSpacing);
			await Assert.That(reference.At < 200).IsTrue();
		}

		[Test]
		public async Task DegenerateChainExtensionDoesNotEmitOverlappingGuides()
		{
			// P 0..50, A 200..250 is moving's right neighbour: ref gap 150 wants right = 50 = P.right, so the
			// matched gap 50..200 is the reference gap itself and must not engage.
			var result = Snap(Rect(13, 55, 40, 30), 8, SnapMode.Move, Target(2, 0, 50, 50, 40), Target(3, 200, 50, 50, 40));
			await Assert.That(Count(result.Guides, SnapGuideKind.HSpacing)).IsEqualTo(0);
		}

		[Test]
		public async Task XEdgeDoesNotSuppressYSpacing()
		{
			// Moving's right (403) is 3 px from right_align's left (400): an x edge snap. bot/top in moving's
			// column make a vertical gap whose symmetric bottom is 125, 2 px from moving's 127.
			var result = Snap(
				Rect(343, 127, 60, 30),
				4,
				SnapMode.Move,
				Target(2, 400, 0, 60, 600),
				Target(3, 360, 40, 60, 40),
				Target(4, 360, 200, 60, 40));
			await Assert.That(Math.Abs(result.Bounds.Right - 400) < 1e-9).IsTrue();
			await Assert.That(Math.Abs(result.Bounds.Bottom - 125) < 1e-9).IsTrue();
			await Assert.That(Count(result.Guides, SnapGuideKind.VLine) > 0).IsTrue();
			await Assert.That(Count(result.Guides, SnapGuideKind.VSpacing) > 0).IsTrue();
			await Assert.That(Count(result.Guides, SnapGuideKind.HSpacing)).IsEqualTo(0);
		}

		[Test]
		public async Task EdgeAlignmentSuppressesSpacingGuides()
		{
			// Moving is 3 px off left_top's left and top, and a chain match would also be in range; the edge
			// snaps must win and no spacing guide may appear.
			var result = Snap(
				Rect(3, 503, 200, 100),
				8,
				SnapMode.Move,
				Target(2, 0, 400, 200, 100),
				Target(3, 300, 400, 200, 100),
				Target(4, 300, 200, 200, 100));
			await Assert.That(result.Guides.Any(g => g.Kind == SnapGuideKind.VLine || g.Kind == SnapGuideKind.HLine)).IsTrue();
			await Assert.That(result.Guides.Any(g => g.Kind == SnapGuideKind.HSpacing || g.Kind == SnapGuideKind.VSpacing)).IsFalse();
		}

		[Test]
		public async Task ThresholdZeroDisablesSnapping()
		{
			var moving = Rect(103, 50, 60, 40);
			var result = Snap(moving, 0, SnapMode.Move, Target(2, 100, 200, 80, 40));
			await Assert.That(result.Bounds == moving).IsTrue();
			await Assert.That(result.Guides.Count).IsEqualTo(0);
		}
	}
}
