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

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// Turns the kernel's Minkowski progress - which counts work items - into a fraction of the
	/// expected running time, so a bar driven by it moves at a steady rate.
	/// </summary>
	/// <remarks>
	/// The kernel's nonconvex &#8853; convex sweep reports one unit per per-triangle hull, one per
	/// 1000-hull batch union and one for the closing merge, all weighted the same. They are not the
	/// same cost. Measured in Release on a sphere-minus-cube dilated by a 12-segment ball (730 to
	/// 7722 triangles): the hulls are about 5% of the run, the batch unions about 85% and the
	/// closing merge the rest. Counted as units the bar raced to the end of each batch's hulls and
	/// then sat through its union, and sat again at the very end through the merge. Weighted by
	/// those measured costs the bar tracks the clock to within the batch granularity: it still
	/// holds during a union (the kernel reports nothing inside one) but no longer lies about how
	/// much is left.
	/// <para>
	/// The unit layout mirrors <c>ManifoldSharp.Minkowski.WorkUnits</c>; if the kernel's batch
	/// size or reporting changes, this has to follow it.
	/// </para>
	/// </remarks>
	public static class MinkowskiProgressModel
	{
		/// <summary>The kernel's batch size (Minkowski.BatchSize).</summary>
		public const int BatchSize = 1000;

		// Relative cost per triangle of each piece of the sweep, from the Release measurements above.
		private const double HullWeight = 1;
		private const double BatchUnionWeight = 17;
		private const double MergeWeight = 2;

		/// <summary>
		/// The number of progress units the kernel's sweep reports for a solid of
		/// <paramref name="triangles"/> triangles: a hull each, a union per batch and the closing merge.
		/// </summary>
		public static long KernelUnits(int triangles)
		{
			long batches = (triangles + BatchSize - 1L) / BatchSize;
			return triangles + batches + 1;
		}

		/// <summary>
		/// Maps the kernel's unit fraction for a sweep over <paramref name="triangles"/> triangles to
		/// the fraction of the expected running time spent so far. Monotonic, 0 at 0 and 1 at 1.
		/// </summary>
		public static double TimeFraction(int triangles, double kernelFraction)
		{
			if (triangles <= 0 || kernelFraction >= 1)
			{
				return Math.Clamp(kernelFraction, 0, 1);
			}

			if (kernelFraction <= 0)
			{
				return 0;
			}

			// The kernel emits fraction = done / total; rounding recovers the whole unit count it had.
			long units = (long)Math.Round(kernelFraction * KernelUnits(triangles));
			double work = 0;
			for (int start = 0; start < triangles && units > 0; start += BatchSize)
			{
				int size = Math.Min(BatchSize, triangles - start);
				if (units > size)
				{
					// Every hull of this batch and its union are done.
					work += size * (HullWeight + BatchUnionWeight);
					units -= size + 1;
				}
				else
				{
					work += units * HullWeight;
					units = 0;
				}
			}

			if (units > 0)
			{
				work += triangles * MergeWeight;
			}

			return Math.Clamp(work / (triangles * (HullWeight + BatchUnionWeight + MergeWeight)), 0, 1);
		}

		/// <summary>The kernel's dilation-tree leaf size (ConvexDilation.LeafSize).</summary>
		public const int TreeLeafSize = 16;

		/// <summary>
		/// The share of a dilation tree's running time spent building and unioning the leaves.
		/// </summary>
		/// <remarks>
		/// From two Release runs of the G203 part (46854 triangles, a 12-segment ball): the leaves
		/// took 55-57 s of runs lasting 81 s and 120 s (the second on a loaded machine), so a bit over
		/// half. The pairwise levels and the closing pass share the rest, and in the slower run the
		/// top few levels alone held the bar for 40 s when the levels were counted by node - which
		/// is why the levels are weighted below rather than counted.
		/// </remarks>
		public const double TreeLeafTimeShare = 0.55;

		/// <summary>The share of a dilation tree's running time spent in the pairwise levels.</summary>
		public const double TreeLevelTimeShare = 0.37;

		/// <summary>
		/// How much more a union at one tree level costs than one at the level below. Each level's
		/// operands are about twice the size, but siblings only overlap along a thin seam, so the
		/// cost grows more slowly than the size. 1.5 puts the first two levels of the G203 tree at
		/// about a third of the levels' time, as measured (about 20 s of about 60 s).
		/// </summary>
		public const double TreeLevelCostGrowth = 1.5;

		/// <summary>
		/// The number of progress units the kernel's union tree reports for
		/// <paramref name="hullUnits"/> hulls: a unit per hull, one per leaf, one per tree node - a
		/// binary reduction of L leaves is L - 1 unions - and one for the closing pass. A dilation
		/// (<c>Manifold.TryDilateByConvex</c>) has the solid as leaf 0; an erosion
		/// (<c>Manifold.TryErodeByConvex</c>, <paramref name="erosion"/>) leaves it out of the tree
		/// and spends one more unit subtracting the union from it.
		/// </summary>
		/// <remarks>
		/// An erosion builds one hull per triangle, so its hull units are the triangle count. A
		/// dilation builds one hull per convex patch of up to 16 triangles (ConvexPatches.cs), a
		/// count only the kernel knows; <see cref="TreeHullUnits"/> recovers it from the phase
		/// total the kernel reports. Either way a solid with nested shells is first rebuilt by the
		/// kernel, which changes its triangle count, so the total is the one to trust.
		/// </remarks>
		public static long TreeUnits(long hullUnits, bool erosion = false)
		{
			long leaves = TreeLeaves(hullUnits, erosion);
			return hullUnits + leaves + (leaves - 1) + 1 + (erosion ? 1 : 0);
		}

		/// <summary>
		/// The hull count behind a union tree whose phase total is <paramref name="totalUnits"/>
		/// (<c>ProgressReporter.PhaseTotal</c>): the inverse of <see cref="TreeUnits"/>, which grows
		/// strictly with the hull count. 0 when no hull count produces that total.
		/// </summary>
		public static long TreeHullUnits(long totalUnits, bool erosion = false)
		{
			// TreeUnits is about hulls * 18 / 16, so start just under the answer and walk up.
			long hulls = Math.Max(0, ((totalUnits - 4) * 8 / 9) - 2);
			while (TreeUnits(hulls + 1, erosion) <= totalUnits)
			{
				hulls++;
			}

			return TreeUnits(hulls, erosion) == totalUnits ? hulls : 0;
		}

		/// <summary>
		/// Maps the kernel's unit fraction for a union tree over <paramref name="hullUnits"/>
		/// hulls to the fraction of the expected running time. Monotonic, 0 at 0 and 1 at 1.
		/// </summary>
		/// <remarks>
		/// Unlike the sweep, the tree's units are not reported in a fixed order: leaves run in
		/// parallel, each reporting its hulls and then itself, so the count only says how much is
		/// done, not which piece. That is enough, because the stages are sequential: every hull and
		/// leaf unit is spent before the first tree node, and each tree level finishes before the
		/// next starts, so a count of finished nodes names the level being worked on and how far
		/// through it the run is.
		/// <para>
		/// The leaf stage maps linearly onto its share - a leaf's sixteen hull units and its union
		/// unit arrive together, so the count is proportional to leaf work. The levels do not: the
		/// bottom one is half the nodes but many small unions spread over every core, while the top
		/// ones are a few large unions with cores idle. Each level is given a wall-time weight of
		/// its rounds of work (unions over <paramref name="parallelism"/>, rounded up) times a per-union
		/// cost growing by <see cref="TreeLevelCostGrowth"/> per level, and the bar moves through a
		/// level linearly by its own nodes. The closing pass - one kernel unit, spent only when the
		/// phase completes - is the remainder. The caller's high-water mark keeps the bar from
		/// stepping back when two workers report out of order.
		/// </para>
		/// <para>
		/// An erosion's closing subtraction - the solid minus the tree's union, one boolean on the
		/// two largest operands - is counted as one more level of one union above the tree's top,
		/// which is what it is: the dilation tree unions that same solid in as its extra leaf.
		/// </para>
		/// </remarks>
		/// <param name="hullUnits">The tree's hull count (<see cref="TreeHullUnits"/>).</param>
		/// <param name="kernelFraction">The kernel's reported fraction of its units.</param>
		/// <param name="parallelism">How many unions run at once; the core count when the kernel runs
		/// in parallel, 1 when it does not.</param>
		/// <param name="erosion">True for the erosion tree, false for the dilation tree.</param>
		public static double TreeTimeFraction(long hullUnits, double kernelFraction, int parallelism, bool erosion = false)
		{
			if (hullUnits <= 0 || kernelFraction >= 1)
			{
				return Math.Clamp(kernelFraction, 0, 1);
			}

			if (kernelFraction <= 0)
			{
				return 0;
			}

			long total = TreeUnits(hullUnits, erosion);
			long leaves = TreeLeaves(hullUnits, erosion);
			long leafStageUnits = hullUnits + leaves;
			double units = kernelFraction * total;
			if (units <= leafStageUnits)
			{
				return TreeLeafTimeShare * units / leafStageUnits;
			}

			// Walk the levels exactly as ConvexDilation reduces them: a level of n nodes performs
			// n / 2 unions and carries an odd one up unchanged.
			int cores = Math.Max(1, parallelism);
			var unionsPerLevel = new System.Collections.Generic.List<long>();
			var weights = new System.Collections.Generic.List<double>();
			double totalWeight = 0;
			double cost = 1;
			for (long nodes = leaves; nodes > 1; nodes = (nodes / 2) + (nodes & 1))
			{
				long unions = nodes / 2;
				double weight = cost * ((unions + cores - 1) / cores);
				unionsPerLevel.Add(unions);
				weights.Add(weight);
				totalWeight += weight;
				cost *= TreeLevelCostGrowth;
			}

			if (erosion)
			{
				// The closing subtraction: one union's worth at the next level up.
				unionsPerLevel.Add(1);
				weights.Add(cost);
				totalWeight += cost;
			}

			double doneNodes = units - leafStageUnits;
			double doneWeight = 0;
			for (int level = 0; level < unionsPerLevel.Count && doneNodes > 0; level++)
			{
				double through = Math.Min(doneNodes, unionsPerLevel[level]);
				doneWeight += weights[level] * through / unionsPerLevel[level];
				doneNodes -= through;
			}

			double levelsDone = totalWeight <= 0 ? 1 : doneWeight / totalWeight;
			return Math.Clamp(TreeLeafTimeShare + (TreeLevelTimeShare * levelsDone), 0, 1);
		}

		private static long TreeLeaves(long hullUnits, bool erosion)
		{
			// A dilation adds the solid as one more leaf; an erosion subtracts it at the end instead.
			return ((hullUnits + TreeLeafSize - 1L) / TreeLeafSize) + (erosion ? 0 : 1);
		}
	}
}
