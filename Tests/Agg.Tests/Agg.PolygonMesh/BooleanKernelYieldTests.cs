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
using MatterHackers.Agg;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The awaitable half of the boolean kernel: an n-ary union has to hand the UI its thread back
	/// between operands, or in the browser - where the job and the UI share one thread - the progress
	/// bar cannot paint until the whole boolean is over.
	/// </summary>
	/// <remarks>
	/// <see cref="ProgressReporter.UiYield"/> is process global state, so every test here installs its
	/// hook inside a try/finally that restores it and the class is <c>[NotInParallel]</c>.
	/// </remarks>
	[NotInParallel]
	public class BooleanKernelYieldTests
	{
		/// <summary>
		/// What the job did, in the order it did it: every report, and how many reports had happened at
		/// each point the UI was handed the thread. The second list is what tells a yield BETWEEN two
		/// operations from one before the work started or after it finished.
		/// </summary>
		private sealed class ProgressLog
		{
			private readonly object recordLock = new object();

			public ProgressLog()
			{
				// One reporter for the whole job: the yield throttle is per-reporter state.
				this.Reporter = new ProgressReporter((ratio, message) =>
				{
					lock (this.recordLock)
					{
						this.Reports.Add((ratio, message));
					}
				});
			}

			public List<(double ratio, string message)> Reports { get; } = new List<(double, string)>();

			public List<int> ReportsBeforeEachYield { get; } = new List<int>();

			public ProgressReporter Reporter { get; }

			/// <summary>
			/// The hook a browser host would install, plus enough of a wait to clear
			/// <see cref="ProgressReporter.YieldThrottleMs"/>.
			/// </summary>
			/// <remarks>
			/// The delay is what makes the count deterministic. Without it a fixture this small finishes
			/// inside one throttle window, so only the first yield of the run would get through and
			/// "between two operands" could not be observed at all. A real browser frame costs about
			/// this much anyway.
			/// </remarks>
			public async Task RecordYield()
			{
				lock (this.recordLock)
				{
					this.ReportsBeforeEachYield.Add(this.Reports.Count);
				}

				await Task.Delay((int)ProgressReporter.YieldThrottleMs + 10);
			}
		}

		/// <summary>
		/// Three cubes in a row, each overlapping the next, so every pair the kernel folds is a real
		/// boolean rather than two disjoint solids.
		/// </summary>
		private static List<(Mesh mesh, Matrix4X4 matrix)> ThreeOverlappingCubes()
		{
			return new List<(Mesh, Matrix4X4)>
			{
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(6, 0, 0)),
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(12, 0, 0)),
			};
		}

		private static Task<Mesh> UnionAsync(ProgressReporter reporter)
		{
			return BooleanProcessing.DoArrayAsync(
				ThreeOverlappingCubes(),
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				reporter,
				CancellationToken.None);
		}

		[Test]
		public async Task AnNaryUnionHandsTheUiItsThreadBackBetweenOperations()
		{
			var previousHook = ProgressReporter.UiYield;
			var log = new ProgressLog();
			ProgressReporter.UiYield = log.RecordYield;

			Mesh result;

			try
			{
				result = await UnionAsync(log.Reporter);
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}

			await Assert.That(log.ReportsBeforeEachYield).IsNotEmpty()
				.Because("a boolean that never yields is a frozen frame in the browser for as long as it runs");

			// Reports on both sides of a yield is what "between two operations" means: the fold had
			// already told the bar where it was, handed the thread over, and then went on folding.
			await Assert.That(log.ReportsBeforeEachYield.Any(reports => reports > 0 && reports < log.Reports.Count))
				.IsTrue()
				.Because("the yield has to land between two pairwise booleans, not only before the first");

			await Assert.That(log.Reports.All(report => report.ratio >= 0 && report.ratio <= 1)).IsTrue();

			// and the union still did its job: one solid spanning all three cubes
			result.CleanAndMerge();
			await Assert.That(result.GetAxisAlignedBoundingBox().XSize).IsEqualTo(22.0).Within(0.001);
		}

		[Test]
		public async Task AUnionNobodyIsWatchingNeverYields()
		{
			// The hook is installed, so the only reason not to hop the event loop is that there is no
			// reporter. Getting this wrong costs every non-UI caller a UI hop per operand - and a null
			// reporter is also what lets the kernel keep its n-ary batch path.
			var previousHook = ProgressReporter.UiYield;
			var log = new ProgressLog();
			ProgressReporter.UiYield = log.RecordYield;

			try
			{
				await UnionAsync(null);
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}

			await Assert.That(log.ReportsBeforeEachYield).IsEmpty()
				.Because("a boolean with no reporter has no progress to paint");
		}

		[Test]
		public async Task YieldingDoesNotChangeTheSolid()
		{
			var previousHook = ProgressReporter.UiYield;
			var log = new ProgressLog();
			ProgressReporter.UiYield = log.RecordYield;

			Mesh yielding;

			try
			{
				yielding = await UnionAsync(log.Reporter);
			}
			finally
			{
				ProgressReporter.UiYield = previousHook;
			}

			// A reporter that does nothing, rather than none at all: a null reporter would put the
			// kernel on its batch path and this would be comparing two folds instead of comparing one
			// fold with and without the yields.
			var plain = BooleanProcessing.DoArray(
				ThreeOverlappingCubes(),
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				(ratio, message) => { },
				CancellationToken.None);

			yielding.CleanAndMerge();
			plain.CleanAndMerge();

			await Assert.That(yielding.Faces.Count).IsEqualTo(plain.Faces.Count)
				.Because("handing the UI a frame in the middle of a boolean must not change the solid it builds");
			await Assert.That(yielding.Vertices.Count).IsEqualTo(plain.Vertices.Count);
		}
	}
}
