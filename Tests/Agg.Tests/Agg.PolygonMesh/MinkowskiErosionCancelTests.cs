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
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Csg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// Cancellation and the throttled progress bar on both of the slow erosion paths: the
	/// kernel's union tree (<c>Manifold.TryErodeByConvex</c>), which a non-convex
	/// solid takes (several shells included), and the kernel's sweep, which a non-convex tool
	/// still takes.
	/// </summary>
	/// <remarks>
	/// Each test asserts which path ran through <see cref="MinkowskiProcessing.ErosionTreeRuns"/>,
	/// because <see cref="ErosionPath"/> reads Sweep for both: a fixture that drifted onto the
	/// other path would otherwise keep passing while no longer testing its subject. The closed
	/// form's own cancel contract is in <see cref="MinkowskiTests"/>.
	/// </remarks>
	// Alone: ErosionTreeRuns is process-wide, and any erosion running beside these would move it.
	[NotInParallel]
	public class MinkowskiErosionCancelTests
	{
		/// <summary>
		/// The sweep: a cancel lands within a hull or two of being asked for, rather than at the
		/// end of the operation, and surfaces as the <see cref="OperationCanceledException"/>
		/// every caller above this layer is written against.
		/// </summary>
		/// <remarks>
		/// The bound is a report count, not a stopwatch: work units are what cancellation latency
		/// is made of, and a wall-clock ratio would measure JIT and machine load. The token is
		/// tripped from the second progress report, the earliest in-kernel moment a test can reach.
		/// <para>
		/// The TOOL is dented, and has to be. The tree and the closed form both need a convex tool,
		/// so a non-convex one - a user eroding by any shape that is not a ball - is what still
		/// reaches the sweep, in its face-pair form: one unit per solid-face-by-tool-face hull plus
		/// one per solid face. The solid is dented too, because the kernel puts a convex operand
		/// second, which would make the tool the one being eroded.
		/// </para>
		/// </remarks>
		[Test]
		public async Task ACancelledSweepErosionStopsWithinAHullOrTwoOfBeingAsked()
		{
			var solid = MinkowskiTests.Dented(MinkowskiProcessing.SphereMesh(5, 16));

			long treeRunsBefore = MinkowskiProcessing.ErosionTreeRuns;
			var reports = await CancelOnSecondReport(solid, NonConvexTool());

			await Assert.That(MinkowskiProcessing.ErosionTreeRuns).IsEqualTo(treeRunsBefore)
				.Because("the tree declines a non-convex tool, so this has to have been the sweep");

			// 128 x 32 face-pair hulls + 128 faces + the closing merge is 4225 units, reported
			// every 42 - about 100 reports. Loose because the parallel hull map cannot recall
			// iterations already in flight, but far under a completed run, which is what "prompt" means.
			await Assert.That(reports).IsLessThan(40)
				.Because($"cancel was ignored for {reports} of the sweep's ~100 reports");
		}

		/// <summary>
		/// The tree: the same prompt cancel, on a dented ball the tree takes.
		/// </summary>
		/// <remarks>
		/// The tree polls the token between hulls inside each leaf and between levels, so a cancel
		/// is as prompt as the sweep's; the bound allows the leaves already in flight on every core.
		/// </remarks>
		[Test]
		public async Task ACancelledTreeErosionStopsWithinAHullOrTwoOfBeingAsked()
		{
			var solid = MinkowskiTests.Dented(MinkowskiProcessing.SphereMesh(5, 16));

			long treeRunsBefore = MinkowskiProcessing.ErosionTreeRuns;
			var reports = await CancelOnSecondReport(solid, BallTool());

			await Assert.That(MinkowskiProcessing.ErosionTreeRuns).IsGreaterThan(treeRunsBefore)
				.Because("a non-convex solid and a convex ball are what the tree takes");

			// 128 hulls + 8 leaves + 7 nodes + the subtraction + the closing pass is 145 units.
			await Assert.That(reports).IsLessThan(40)
				.Because($"cancel was ignored for {reports} of the tree's ~145 reports");
		}

		/// <summary>
		/// The sweep: the bar reaches 1.0 through the whole async stack on an erosion big enough
		/// for the kernel's progress throttle to start skipping reports.
		/// </summary>
		/// <remarks>
		/// The regression this pins was found here rather than in the kernel: the throttle emits
		/// every <c>total / 100</c> units, so a 290 unit erosion used to stop reporting at 288/290
		/// and the caller's bar stopped just short of full. Anything under 100 units lands on 1.0
		/// by luck, because the step is then 1. A non-convex tool for the reason the sweep cancel
		/// test gives.
		/// </remarks>
		[Test]
		public async Task AThrottledSweepErosionStillFillsTheBar()
		{
			var solid = MinkowskiTests.Dented(MinkowskiProcessing.SphereMesh(5, 8));
			var tool = NonConvexTool();

			await Assert.That(solid.Faces.Count * tool.Faces.Count).IsEqualTo(1024)
				.Because("1024 face-pair hulls + 32 solid faces + the closing merge is 1057 units, so the throttle's step is 10");

			long treeRunsBefore = MinkowskiProcessing.ErosionTreeRuns;
			var ratios = await ErodeRecordingTheBar(solid, tool);

			await Assert.That(MinkowskiProcessing.ErosionTreeRuns).IsEqualTo(treeRunsBefore)
				.Because("the tree declines a non-convex tool, so this has to have been the sweep");
			await AssertThrottledAndFull(ratios, 1057);
		}

		/// <summary>
		/// The tree: the same full bar under the throttle, on a dented ball.
		/// </summary>
		[Test]
		public async Task AThrottledTreeErosionStillFillsTheBar()
		{
			var solid = MinkowskiTests.Dented(MinkowskiProcessing.SphereMesh(5, 24));

			await Assert.That(MinkowskiProgressModel.TreeUnits(solid.Faces.Count, erosion: true)).IsEqualTo(325)
				.Because("288 hulls, 18 leaves, 17 nodes, the subtraction and the closing pass: the throttle's step is 3");

			long treeRunsBefore = MinkowskiProcessing.ErosionTreeRuns;
			var ratios = await ErodeRecordingTheBar(solid, BallTool());

			await Assert.That(MinkowskiProcessing.ErosionTreeRuns).IsGreaterThan(treeRunsBefore)
				.Because("a non-convex solid and a convex ball are what the tree takes");
			await AssertThrottledAndFull(ratios, 325);
		}

		private static async Task<int> CancelOnSecondReport(Mesh solid, Mesh tool)
		{
			using var source = new CancellationTokenSource();
			int reports = 0;
			var reporter = new ProgressReporter((ratio, message) =>
			{
				// Not the first: that one is the kernel opening the phase, before any hull has
				// run, so cancelling on it would only re-test the entry gate.
				if (Interlocked.Increment(ref reports) >= 2)
				{
					source.Cancel();
				}
			});

			OperationCanceledException caught = null;
			try
			{
				await MinkowskiProcessing.MinkowskiDifferenceAsync(solid, tool, reporter, source.Token);
			}
			catch (OperationCanceledException cancelled)
			{
				caught = cancelled;
			}

			await Assert.That(caught).IsNotNull()
				.Because("the kernel reports a cancelled run as a status; this layer owes the caller an exception");

			return Volatile.Read(ref reports);
		}

		private static async Task<List<double>> ErodeRecordingTheBar(Mesh solid, Mesh tool)
		{
			var ratios = new List<double>();
			var gate = new object();
			var reporter = new ProgressReporter((ratio, message) =>
			{
				lock (gate)
				{
					ratios.Add(ratio);
				}
			});

			var eroded = await MinkowskiProcessing.MinkowskiDifferenceAsync(solid, tool, reporter, CancellationToken.None);
			await Assert.That(eroded.Faces.Count).IsGreaterThan(0);

			return ratios;
		}

		private static async Task AssertThrottledAndFull(List<double> ratios, int units)
		{
			await Assert.That(ratios.Count).IsLessThan(units)
				.Because("fewer reports than units is what makes this the throttled regime the bug lived in");
			await Assert.That(ratios.Count).IsGreaterThan(50)
				.Because("and more than a handful is what says a slow path ran at all - two reports would be the closed form");
			await Assert.That(ratios[ratios.Count - 1]).IsEqualTo(1.0).Within(1e-9)
				.Because("a finished erosion has to leave the bar full");
		}

		/// <summary>The convex ball every fast path takes.</summary>
		private static Mesh BallTool() => MinkowskiProcessing.SphereMesh(0.5, 8);

		/// <summary>
		/// The same ball, dented: 32 triangles and not convex, which the tree and the closed form
		/// both decline.
		/// </summary>
		private static Mesh NonConvexTool() => MinkowskiTests.Dented(BallTool());
	}
}
