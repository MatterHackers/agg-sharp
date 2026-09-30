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

using System.Threading.Tasks;
using MatterHackers.PolygonMesh.Csg;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	public class MinkowskiProgressModelTests
	{
		[Test]
		public async Task TheTimeFractionRisesThroughEveryKernelUnitFromZeroToOne()
		{
			const int triangles = 2500;
			long total = MinkowskiProgressModel.KernelUnits(triangles);
			await Assert.That(total).IsEqualTo(2500 + 3 + 1);

			double previous = -1;
			for (long unit = 0; unit <= total; unit++)
			{
				double fraction = MinkowskiProgressModel.TimeFraction(triangles, unit / (double)total);
				await Assert.That(fraction).IsGreaterThanOrEqualTo(previous);
				previous = fraction;
			}

			await Assert.That(MinkowskiProgressModel.TimeFraction(triangles, 0)).IsEqualTo(0.0);
			await Assert.That(MinkowskiProgressModel.TimeFraction(triangles, 1)).IsEqualTo(1.0);
		}

		[Test]
		public async Task HullsAreCheapAndBatchUnionsCarryTheTime()
		{
			const int triangles = 3000;
			long total = MinkowskiProgressModel.KernelUnits(triangles);

			// Every hull of the first batch done, its union not: the bar has barely moved,
			// because the union that follows is where the batch's time goes.
			double hullsDone = MinkowskiProgressModel.TimeFraction(triangles, 1000 / (double)total);
			await Assert.That(hullsDone).IsLessThan(0.05);

			// One of three batches unioned: close to a third of the time, less its share of the merge.
			double oneBatch = MinkowskiProgressModel.TimeFraction(triangles, 1001 / (double)total);
			await Assert.That(oneBatch).IsEqualTo(0.3).Within(0.001);

			// Every batch unioned, the closing merge still to run: its share is left.
			double allBatches = MinkowskiProgressModel.TimeFraction(triangles, (total - 1) / (double)total);
			await Assert.That(allBatches).IsEqualTo(0.9).Within(0.001);
		}

		[Test]
		public async Task TheDilationTreeFractionRisesLinearlyThroughTheLeavesThenTheTree()
		{
			const int triangles = 1600;
			long total = MinkowskiProgressModel.TreeUnits(triangles);

			// 1600 hulls, 101 leaves (the solid is one), 100 tree nodes and the closing pass.
			await Assert.That(total).IsEqualTo(1600 + 101 + 100 + 1);

			double previous = -1;
			for (long unit = 0; unit <= total; unit++)
			{
				double fraction = MinkowskiProgressModel.TreeTimeFraction(triangles, unit / (double)total, 10);
				await Assert.That(fraction).IsGreaterThanOrEqualTo(previous);
				previous = fraction;
			}

			await Assert.That(MinkowskiProgressModel.TreeTimeFraction(triangles, 0, 10)).IsEqualTo(0.0);
			await Assert.That(MinkowskiProgressModel.TreeTimeFraction(triangles, 1, 10)).IsEqualTo(1.0);

			// Half the leaf work done is half the leaves' share of the clock.
			double halfLeaves = MinkowskiProgressModel.TreeTimeFraction(triangles, (1701 / 2.0) / total, 10);
			await Assert.That(halfLeaves).IsEqualTo(MinkowskiProgressModel.TreeLeafTimeShare / 2).Within(1e-9);

			// The bottom level is half the tree's unions but many small ones on every core, so it
			// takes well under half the levels' share; the few big unions at the top get the rest,
			// which is what stops the bar holding at the very end.
			double bottomLevelDone = MinkowskiProgressModel.TreeTimeFraction(triangles, (1701 + 50) / (double)total, 10);
			double levelsShareAfterBottom = (bottomLevelDone - MinkowskiProgressModel.TreeLeafTimeShare) / MinkowskiProgressModel.TreeLevelTimeShare;
			await Assert.That(levelsShareAfterBottom).IsLessThan(0.3);

			// Everything but the closing pass done: the leaves and levels, and not the last stretch.
			double allNodes = MinkowskiProgressModel.TreeTimeFraction(triangles, (total - 1) / (double)total, 10);
			await Assert.That(allNodes).IsEqualTo(MinkowskiProgressModel.TreeLeafTimeShare + MinkowskiProgressModel.TreeLevelTimeShare).Within(1e-9);
		}

		[Test]
		public async Task TheErosionTreeCountsItsClosingSubtractionAsATopLevel()
		{
			const int triangles = 1600;
			long total = MinkowskiProgressModel.TreeUnits(triangles, erosion: true);

			// 1600 hulls, 100 leaves (the solid is not one), 99 tree nodes, the subtraction and the closing pass.
			await Assert.That(total).IsEqualTo(1600 + 100 + 99 + 1 + 1);

			double previous = -1;
			for (long unit = 0; unit <= total; unit++)
			{
				double fraction = MinkowskiProgressModel.TreeTimeFraction(triangles, unit / (double)total, 10, erosion: true);
				await Assert.That(fraction).IsGreaterThanOrEqualTo(previous);
				previous = fraction;
			}

			await Assert.That(MinkowskiProgressModel.TreeTimeFraction(triangles, 1, 10, erosion: true)).IsEqualTo(1.0);

			// Every leaf done is the leaves' whole share.
			double leavesDone = MinkowskiProgressModel.TreeTimeFraction(triangles, 1700 / (double)total, 10, erosion: true);
			await Assert.That(leavesDone).IsEqualTo(MinkowskiProgressModel.TreeLeafTimeShare).Within(1e-9);

			// Every tree node done but not the subtraction: the subtraction still holds a real part
			// of the levels' share - it is the biggest boolean of the run.
			double nodesDone = MinkowskiProgressModel.TreeTimeFraction(triangles, (1700 + 99) / (double)total, 10, erosion: true);
			await Assert.That(nodesDone).IsLessThan(MinkowskiProgressModel.TreeLeafTimeShare + (0.9 * MinkowskiProgressModel.TreeLevelTimeShare));

			// The subtraction done, the closing pass not: the leaves and all the levels.
			double subtracted = MinkowskiProgressModel.TreeTimeFraction(triangles, (total - 1) / (double)total, 10, erosion: true);
			await Assert.That(subtracted).IsEqualTo(MinkowskiProgressModel.TreeLeafTimeShare + MinkowskiProgressModel.TreeLevelTimeShare).Within(1e-9);
		}
	}
}
