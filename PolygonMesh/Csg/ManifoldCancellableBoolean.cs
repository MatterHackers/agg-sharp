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

// Kernel types are aliased rather than imported, as ManifoldKernel.cs explains.
using RustBooleanEngine = ManifoldSharp.BooleanEngine;
using RustCancelToken = ManifoldSharp.CancelToken;
using RustCsgLeaf = ManifoldSharp.CsgLeaf;
using RustCsgNode = ManifoldSharp.CsgNode;
using RustCsgOp = ManifoldSharp.CsgOp;
using RustManifold = ManifoldSharp.Manifold;
using RustOpType = ManifoldSharp.OpType;
using RustProgressReporter = ManifoldSharp.ProgressReporter;
using RustStatus = ManifoldSharp.Error;
using RustWindingRule = ManifoldSharp.WindingRule;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// The kernel's two boolean entry points - one binary boolean and the n-ary CSG tree - with
	/// the caller's <see cref="System.Threading.CancellationToken"/> bridged into the kernel's own
	/// cancellation flag, and a cancelled run turned back into a thrown
	/// <see cref="System.OperationCanceledException"/>.
	/// </summary>
	internal static class ManifoldCancellableBoolean
	{
		/// <summary>
		/// Configures the kernel before the first call that reaches it through this class.
		/// </summary>
		/// <remarks>
		/// <see cref="ManifoldKernel"/>'s type initializer is the configuration, so its own
		/// members configure the kernel by construction; this class is reached directly too, so it
		/// asks for the same thing. A caller that gets here before any boolean has run still gets
		/// the engine and parallelism a boolean would - see <see cref="ManifoldKernel.EnsureConfigured"/>.
		/// </remarks>
		static ManifoldCancellableBoolean()
		{
			ManifoldKernel.EnsureConfigured();
		}

		/// <summary>
		/// One binary boolean, with the caller's <see cref="CancellationToken"/> bridged into
		/// the kernel's own cancellation flag.
		/// </summary>
		/// <remarks>
		/// The kernel reports cancellation as <see cref="RustStatus.Cancelled"/> on an empty
		/// result; the kernel wrapper - and every caller above it - is written against a
		/// thrown <see cref="OperationCanceledException"/>, which is what the P/Invoke binding
		/// translated it into. This is that translation, unchanged, including its two
		/// deliberate properties: a token that can never be signalled allocates nothing and
		/// takes the uncancellable path, and <b>completion wins</b> - a kernel that finished
		/// before it observed the flag returns its result rather than throwing, even if the
		/// token is signalled by the time this returns.
		/// </remarks>
		internal static RustManifold Boolean(
			RustManifold a,
			RustManifold b,
			RustOpType operationType,
			RustBooleanEngine engine,
			RustWindingRule windingRule,
			RustProgressReporter progress,
			CancellationToken cancellationToken)
		{
			ManifoldKernelCallCounts.CountPairwiseStep();
			if (!cancellationToken.CanBeCanceled)
			{
				return a.BooleanWithEngineRuleAndProgress(b, operationType, engine, windingRule, null, progress);
			}

			// One token per operation, as CancelToken's own remarks require: it registers on
			// the caller's source and is never unregistered, so a token that outlived the call
			// would stay rooted in that source's callback list.
			var token = new RustCancelToken(cancellationToken);

			var result = a.BooleanWithEngineRuleAndProgress(b, operationType, engine, windingRule, token, progress);

			if (result.Status() == RustStatus.Cancelled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			return result;
		}

		/// <summary>
		/// The n-ary boolean over already-imported operands: the kernel's CSG tree, which is
		/// what makes a large union tractable, with the same cancellation translation
		/// <see cref="Boolean"/> performs.
		/// </summary>
		/// <remarks>
		/// Spelled out here rather than called, because <c>Manifold.BatchBoolean</c> is not the
		/// same operation: that one is a pairwise left fold, while the batch entry point this
		/// code was written against - manifold-ffi's <c>manifold_rs_batch_boolean_ct</c>, the
		/// one the P/Invoke binding exposed as <c>BatchBoolean</c> - builds an n-ary CSG node
		/// and evaluates it. The tree is the whole point of the batch path (see
		/// <see cref="ManifoldKernel.NeedsExplicitBoolean"/>), so this reproduces the FFI's definition line for
		/// line: leaves over cloned implementations, one <c>CsgOp</c>, evaluate with the token.
		/// <para>
		/// <paramref name="progress"/> goes to every binary boolean the tree runs (ManifoldSharp's
		/// C#-only <c>EvaluateWithToken(token, progress)</c>), so each one streams its own kernel
		/// phases from the start; observation only, the result is the same bits either way.
		/// </para>
		/// </remarks>
		internal static RustManifold BatchBoolean(
			List<RustManifold> manifolds,
			RustOpType operationType,
			RustProgressReporter progress,
			CancellationToken cancellationToken)
		{
			if (!cancellationToken.CanBeCanceled)
			{
				return BatchBooleanCore(manifolds, operationType, null, progress);
			}

			var token = new RustCancelToken(cancellationToken);

			var result = BatchBooleanCore(manifolds, operationType, token, progress);

			if (result.Status() == RustStatus.Cancelled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			return result;
		}

		/// <summary>
		/// <see cref="BatchBoolean"/> against the kernel's own cancellation flag, which reports
		/// a cancelled run as a status rather than by throwing.
		/// </summary>
		private static RustManifold BatchBooleanCore(
			List<RustManifold> manifolds,
			RustOpType operationType,
			RustCancelToken token,
			RustProgressReporter progress)
		{
			// A single operand has nothing to combine with and every operation is identity on
			// it - but an already-cancelled token still wins, so a caller that only polls the
			// status never sees a one-operand call report success after a cancel.
			if (manifolds.Count == 1)
			{
				return token != null && token.IsCancelled
					? RustManifold.MakeEmpty(RustStatus.Cancelled)
					: manifolds[0].Clone();
			}

			var leaves = new List<RustCsgNode>(manifolds.Count);

			foreach (var manifold in manifolds)
			{
				// Cloned, not aliased: the tree takes ownership of what it evaluates, and the
				// operands stay the caller's to use again afterwards.
				leaves.Add(new RustCsgLeaf(manifold.AsImpl().Clone()));
			}

			return RustManifold.FromImpl(new RustCsgOp(operationType, leaves).EvaluateWithToken(token, progress));
		}
	}
}
