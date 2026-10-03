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

using System.Threading;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// How many boolean evaluations <see cref="ManifoldKernel"/> has run, by kind, so a test can pin
	/// that an n-ary boolean reached the kernel as ONE call rather than a pairwise fold. Process-global
	/// and never read by production code; a test resets it before the work it measures and must not
	/// run beside other boolean work (NotInParallel).
	/// </summary>
	internal static class ManifoldKernelCallCounts
	{
		private static int operandCombines;
		private static int pairwiseSteps;
		private static int colorGroupUnions;

		/// <summary>One n-ary CSG-tree evaluation over a boolean's operands (two or more of them).</summary>
		internal static int OperandCombines => Volatile.Read(ref operandCombines);

		/// <summary>One binary step of a winding-rule pairwise fold - n-1 of them per folded boolean.</summary>
		internal static int PairwiseSteps => Volatile.Read(ref pairwiseSteps);

		/// <summary>
		/// One union of a multi-coloured operand's colour groups at import, before the real operation runs.
		/// </summary>
		internal static int ColorGroupUnions => Volatile.Read(ref colorGroupUnions);

		internal static void Reset()
		{
			Interlocked.Exchange(ref operandCombines, 0);
			Interlocked.Exchange(ref pairwiseSteps, 0);
			Interlocked.Exchange(ref colorGroupUnions, 0);
		}

		internal static void CountOperandCombine() => Interlocked.Increment(ref operandCombines);

		internal static void CountPairwiseStep() => Interlocked.Increment(ref pairwiseSteps);

		internal static void CountColorGroupUnion() => Interlocked.Increment(ref colorGroupUnions);
	}
}
