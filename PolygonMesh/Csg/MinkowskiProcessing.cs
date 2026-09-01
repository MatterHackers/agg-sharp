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
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg;

// Same aliasing convention as the kernel itself (ManifoldKernel.cs): types that come from
// the boolean kernel are spelled with a Rust prefix - ManifoldSharp is the C# port of
// manifold-rust - so a use site says which library it means. Here it also keeps the kernel's
// own Minkowski class from being confused with this wrapper.
using RustCancelToken = ManifoldSharp.CancelToken;
using RustManifold = ManifoldSharp.Manifold;
using RustPhases = ManifoldSharp.Phases;
using RustProgressReporter = ManifoldSharp.ProgressReporter;
using RustStatus = ManifoldSharp.Error;

namespace MatterHackers.PolygonMesh.Csg
{
	/// <summary>
	/// The kernel's morphological operations over <see cref="Mesh"/>: dilation
	/// (<see cref="MinkowskiSum"/>) and erosion (<see cref="MinkowskiDifference"/>).
	/// </summary>
	/// <remarks>
	/// The public face of <see cref="ManifoldKernel"/> for Minkowski work, exactly as
	/// <see cref="BooleanProcessing"/> is for booleans: operands are validated by being offered
	/// to the kernel, and an operand it will not take is an exception the caller sees rather
	/// than geometry built by other rules.
	/// <para>
	/// <b>Cost model.</b> The kernel picks one of three algorithms, and they are orders of
	/// magnitude apart:
	/// </para>
	/// <list type="bullet">
	/// <item>convex &#8853; convex - one convex hull over the pairwise vertex sums. Milliseconds.</item>
	/// <item>nonconvex &#8853; convex (and every erosion the closed form below declines) - one
	/// convex hull per triangle of the nonconvex operand, batch-unioned 1000 at a time. Linear
	/// in that operand's triangle count, with a boolean's worth of work per triangle: seconds
	/// for a few hundred triangles, minutes for a few thousand.</item>
	/// <item>nonconvex &#8853; nonconvex - a hull per <em>pair</em> of faces. Quadratic, and only
	/// worth starting on toy meshes.</item>
	/// </list>
	/// <para>
	/// And a fourth this class routes to itself, ahead of all three: an erosion of a
	/// <em>convex</em> solid has a closed form - the intersection of the solid's face
	/// halfspaces with every plane pushed inward by the tool's reach in that direction - which
	/// the kernel offers as <c>Manifold.TryConvexErosion</c>. Two convex hulls, whatever the
	/// triangle count. Measured against the sweep it replaces: a 12-triangle box 0.01 ms
	/// against 16, a 436-triangle cylinder 0.07 ms against 184, a 2048-triangle sphere 29 ms
	/// against 2700. It declines for anything it cannot prove itself on - a non-convex operand
	/// first among them - and the sweep runs unchanged, so this is a routing decision and not
	/// a second answer.
	/// </para>
	/// <para>
	/// The two answers agree exactly on anything up to about a thousand faces - a box comes out
	/// whole-number identical either way - and to about 5e-7 relative above that, where the
	/// kernel's hull starts discarding near-coplanar points. Far inside the error the
	/// tessellated ball itself introduces, and documented at the kernel in
	/// <c>docs/RUST_DIVERGENCES.md</c> entry 5.
	/// </para>
	/// <para>
	/// So a caller controls its own running time by keeping the structuring element small and
	/// coarse: <see cref="SphereMesh"/> at 12 segments is 72 triangles and is what the rounding
	/// radius actually needs, while the same ball at 64 segments costs the same shape many times
	/// over.
	/// </para>
	/// <para>
	/// <b>Intended uses.</b> Morphological opening (<c>erode</c> then <c>dilate</c>) rounds every
	/// convex edge of a solid by the ball's radius and closing (<c>dilate</c> then <c>erode</c>)
	/// rounds every concave one, which is the uniform all-edges fillet, exactly; and because
	/// that answer is exact, it is also the oracle a selective bevel/fillet implementation is
	/// measured against on the edges it does round.
	/// </para>
	/// <para>
	/// <b>Progress and cancellation.</b> The kernel's Minkowski now takes both - a
	/// <c>ManifoldSharp.ProgressReporter</c> and a <c>CancelToken</c>, reporting one unit per
	/// per-face hull, per batch reduction and for the closing merge, and polling the flag
	/// between them. <see cref="MinkowskiSumAsync"/> and
	/// <see cref="MinkowskiDifferenceAsync"/> are the entry points that use them: the kernel
	/// call goes to a worker, the caller's <see cref="MatterHackers.Agg.ProgressReporter"/>
	/// sees a bar that only rises and ends at 1.0, and a cancel comes back as an
	/// <see cref="OperationCanceledException"/> within one hull rather than at the end of the
	/// operation. That is the same translation <see cref="BooleanProcessing"/> performs, and
	/// it matters more here: an erosion of a few thousand triangles is minutes of work that
	/// used to be uninterruptible.
	/// </para>
	/// <para>
	/// The synchronous entry points are unchanged and stay the right call for a job with
	/// nobody watching - they pass no reporter and no token, which is byte-for-byte the path
	/// that ran before either existed.
	/// </para>
	/// <para>
	/// What this unblocks is the uniform-fillet fast path: an opening or a closing is two of
	/// these calls, and until they could report and be stopped, offering one from the UI meant
	/// offering a frozen window with no way out of it.
	/// </para>
	/// </remarks>
	/// <summary>
	/// Which algorithm an erosion actually ran - the closed form or the sweep it falls back to.
	/// </summary>
	/// <remarks>
	/// <b>This is a cost signal, not a correctness one.</b> Both paths compute the same erosion,
	/// and the closed form stands down wherever it cannot prove that, so no caller should ever
	/// branch its <em>geometry</em> on this. It exists because the two paths are three orders of
	/// magnitude apart in cost, and a progress bar or a cost estimate that cannot tell them apart
	/// is wrong about one of them - see the cost model on <see cref="MinkowskiProcessing"/>.
	/// <para>
	/// It is reported <em>after</em> the erosion, because there is no way to know in advance: the
	/// closed form declines from inside its own algorithm as well as at the gate, so anything
	/// claiming to predict the routing would have to run it. A caller that wants to weight a bar
	/// it has already started therefore cannot use this to do it; what it can do is tell the user
	/// which regime they just paid for, and remember the answer for the next run on the same part.
	/// </para>
	/// </remarks>
	public enum ErosionPath
	{
		/// <summary>
		/// The halfspace-intersection closed form. Two convex hulls, whatever the triangle
		/// count - milliseconds.
		/// </summary>
		ClosedForm,

		/// <summary>
		/// The per-triangle sweep: one convex hull and one boolean per triangle of the solid.
		/// Seconds for a few hundred triangles, minutes for a few thousand.
		/// </summary>
		Sweep,
	}

	public static class MinkowskiProcessing
	{
		/// <summary>
		/// Dilation: every point of <paramref name="solid"/> swept by <paramref name="tool"/>,
		/// which grows the solid by the tool's extent in every direction.
		/// </summary>
		/// <remarks>
		/// The tool's own origin is the sweep centre, so a ball built by
		/// <see cref="SphereMesh"/> - centred - grows the solid symmetrically, and one that is not
		/// centred also translates it.
		/// </remarks>
		/// <param name="solid">The shape being grown.</param>
		/// <param name="tool">The structuring element swept over it.</param>
		/// <returns>The dilated solid.</returns>
		/// <exception cref="ArgumentNullException">Either operand is null.</exception>
		/// <exception cref="ArgumentException">
		/// Either operand has no geometry, or encloses no volume - a zero-thickness shell imports
		/// cleanly and holds nothing, and the kernel would answer it with the other operand.
		/// </exception>
		/// <exception cref="InvalidOperationException">
		/// The kernel refused an operand - as a <c>MeshImportRejectedException</c> naming the
		/// status it objected to - or could not build the result. Same failure and same message
		/// shape a boolean produces for the same input.
		/// </exception>
		public static Mesh MinkowskiSum(Mesh solid, Mesh tool)
		{
			return Run(solid, tool, inset: false, reporter: null, cancellationToken: CancellationToken.None).Mesh;
		}

		/// <summary>
		/// <see cref="MinkowskiSum"/> for a caller that can hand the UI its thread back and
		/// wants to be able to stop.
		/// </summary>
		/// <remarks>
		/// The whole operation runs on a worker; the yields are before and after it, not
		/// inside, because the kernel's progress callback arrives part way through a hull loop
		/// on whichever thread got there - the same rule
		/// <see cref="BooleanProgressAdapter"/> is written under, and the reason a boolean's
		/// yields live in the managed loop around the kernel rather than in the callback.
		/// <para>
		/// Both meshes are read from that worker, so a caller must not mutate them until the
		/// task completes.
		/// </para>
		/// </remarks>
		/// <param name="solid">The shape being grown.</param>
		/// <param name="tool">The structuring element swept over it.</param>
		/// <param name="reporter">Where to report progress, or null for nobody watching.</param>
		/// <param name="cancellationToken">Stops the operation between hulls.</param>
		/// <returns>The dilated solid.</returns>
		/// <exception cref="OperationCanceledException">
		/// <paramref name="cancellationToken"/> was signalled and the kernel observed it. The
		/// kernel reports a cancelled run as an empty result with a status; this is the same
		/// translation <c>ManifoldKernel</c> performs for a boolean, completion included -
		/// a run that finished before it saw the flag returns its result.
		/// </exception>
		/// <inheritdoc cref="MinkowskiSum"/>
		public static async Task<Mesh> MinkowskiSumAsync(
			Mesh solid,
			Mesh tool,
			ProgressReporter reporter,
			CancellationToken cancellationToken)
		{
			return (await RunAsync(solid, tool, inset: false, reporter, cancellationToken)).Mesh;
		}

		/// <summary>
		/// Erosion: the points of <paramref name="solid"/> that <paramref name="tool"/> still fits
		/// inside when centred on them, which shrinks the solid by the tool's extent.
		/// </summary>
		/// <remarks>
		/// The expensive half of an opening or a closing, unless the solid is convex - then it
		/// takes the closed form instead and costs about as little as the dilation does. Both
		/// compute the same erosion, and the closed form stands down wherever it cannot prove
		/// that, so the routing never changes the answer - only the clock. A caller that needs to
		/// know which one it paid for, to size a progress bar or an estimate, takes the
		/// <see cref="ErosionPath"/> overload. See the cost model on
		/// <see cref="MinkowskiProcessing"/>.
		/// <para>
		/// A tool too large for the solid erodes it away entirely, which comes back as an empty
		/// mesh rather than as a failure: nothing fits is a real answer.
		/// </para>
		/// </remarks>
		/// <param name="solid">The shape being shrunk.</param>
		/// <param name="tool">The structuring element that has to fit inside it.</param>
		/// <returns>The eroded solid.</returns>
		/// <inheritdoc cref="MinkowskiSum"/>
		public static Mesh MinkowskiDifference(Mesh solid, Mesh tool)
		{
			return Run(solid, tool, inset: true, reporter: null, cancellationToken: CancellationToken.None).Mesh;
		}

		/// <summary>
		/// <see cref="MinkowskiDifference(Mesh, Mesh)"/>, also reporting which of the two
		/// algorithms ran.
		/// </summary>
		/// <remarks>
		/// The same erosion and the same answer as the overload without the out parameter; see
		/// <see cref="ErosionPath"/> for what the signal is and is not good for.
		/// </remarks>
		/// <param name="solid">The shape being shrunk.</param>
		/// <param name="tool">The structuring element that has to fit inside it.</param>
		/// <param name="erosionPath">Which algorithm computed the result.</param>
		/// <returns>The eroded solid.</returns>
		/// <inheritdoc cref="MinkowskiSum"/>
		public static Mesh MinkowskiDifference(Mesh solid, Mesh tool, out ErosionPath erosionPath)
		{
			var run = Run(solid, tool, inset: true, reporter: null, cancellationToken: CancellationToken.None);

			erosionPath = run.Path;

			return run.Mesh;
		}

		/// <summary>
		/// <see cref="MinkowskiDifference"/> for a caller that can hand the UI its thread back
		/// and wants to be able to stop. This is the half worth cancelling: an erosion is one
		/// hull and one boolean per triangle of the solid.
		/// </summary>
		/// <param name="solid">The shape being shrunk.</param>
		/// <param name="tool">The structuring element that has to fit inside it.</param>
		/// <param name="reporter">Where to report progress, or null for nobody watching.</param>
		/// <param name="cancellationToken">Stops the operation between hulls.</param>
		/// <returns>The eroded solid.</returns>
		/// <inheritdoc cref="MinkowskiSumAsync"/>
		public static async Task<Mesh> MinkowskiDifferenceAsync(
			Mesh solid,
			Mesh tool,
			ProgressReporter reporter,
			CancellationToken cancellationToken)
		{
			return (await RunAsync(solid, tool, inset: true, reporter, cancellationToken)).Mesh;
		}

		/// <summary>
		/// <see cref="MinkowskiDifferenceAsync"/>, also reporting which of the two algorithms ran.
		/// </summary>
		/// <remarks>
		/// The same erosion and the same answer as <see cref="MinkowskiDifferenceAsync"/>; see
		/// <see cref="ErosionPath"/> for what the signal is and is not good for. In particular the
		/// path arrives with the result, which is after the bar this call drove has already been
		/// spent - it can size the <em>next</em> estimate, not this one.
		/// </remarks>
		/// <param name="solid">The shape being shrunk.</param>
		/// <param name="tool">The structuring element that has to fit inside it.</param>
		/// <param name="reporter">Where to report progress, or null for nobody watching.</param>
		/// <param name="cancellationToken">Stops the operation between hulls.</param>
		/// <returns>The eroded solid and the algorithm that produced it.</returns>
		/// <inheritdoc cref="MinkowskiSumAsync"/>
		public static Task<(Mesh Eroded, ErosionPath Path)> MinkowskiDifferenceWithPathAsync(
			Mesh solid,
			Mesh tool,
			ProgressReporter reporter,
			CancellationToken cancellationToken)
		{
			return RunAsync(solid, tool, inset: true, reporter, cancellationToken);
		}

		/// <summary>
		/// A sphere of <paramref name="radius"/> centred on the origin - the structuring element
		/// morphological rounding is done with.
		/// </summary>
		/// <remarks>
		/// The kernel's own sphere rather than one built here, for two reasons: it is a subdivided
		/// octahedron, so it is convex and its poles sit exactly on the axes at
		/// <paramref name="radius"/> (which is what makes a dilated bounding box grow by exactly
		/// twice the radius), and it comes back through the same import the operands do.
		/// <para>
		/// Note it is <em>inscribed</em>: every vertex is exactly on the sphere and every face is
		/// inside it, so a rounding done with it is a little tighter than the analytic radius, by
		/// the chord error of the tessellation. Segments buy back that error at a price the cost
		/// model above spells out.
		/// </para>
		/// </remarks>
		/// <param name="radius">The sphere's radius; must be positive.</param>
		/// <param name="segments">
		/// Sides around the full circle. The kernel rounds up to a multiple of four (it subdivides
		/// each octahedron edge into <c>(segments + 3) / 4</c> parts), and 0 asks it to pick a
		/// count from the radius. Negative is refused rather than read as that request.
		/// </param>
		/// <returns>The sphere as a mesh.</returns>
		public static Mesh SphereMesh(double radius, int segments)
		{
			if (!(radius > 0))
			{
				// The kernel answers a non-positive radius with an empty InvalidConstruction
				// manifold, which would surface here as an unexplained empty mesh.
				throw new ArgumentOutOfRangeException(nameof(radius), radius, "A sphere needs a positive radius.");
			}

			if (segments < 0)
			{
				// The kernel reads any non-positive count as "pick one from the radius", so a
				// segment count that came out of a calculation negative would silently produce a
				// ball at some other tessellation than the caller believes it asked for. Zero is
				// left alone because zero is how a caller spells that request on purpose.
				throw new ArgumentOutOfRangeException(nameof(segments), segments, "A sphere needs a non-negative segment count; 0 asks the kernel to choose.");
			}

			return ManifoldKernel.ToMesh(RustManifold.Sphere(radius, segments), "sphere");
		}

		/// <summary>
		/// Imports both operands, runs the kernel's Minkowski and reads the result back.
		/// </summary>
		/// <remarks>
		/// The import is <see cref="ManifoldKernel.Import"/> - the same one a boolean operand goes
		/// through, weld retry included - so a mesh usable as a boolean operand is usable here and
		/// one that is not fails the same way, with the same message. No orientation repair: a
		/// caller that wants it can hand in a repaired mesh, and doing it silently here would make
		/// the two entry points disagree about what an inside-out shell means.
		/// </remarks>
		private static (Mesh Mesh, ErosionPath Path) Run(Mesh solid, Mesh tool, bool inset, ProgressReporter reporter, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(solid);
			ArgumentNullException.ThrowIfNull(tool);

			// Empty is not a degenerate case the kernel answers usefully: Minkowski with an empty
			// operand is empty, but the kernel hands back the *other* operand unchanged. That is a
			// silent no-op on a fillet, so it is refused here instead.
			ThrowIfEmpty(solid, nameof(solid));
			ThrowIfEmpty(tool, nameof(tool));

			var solidManifold = ManifoldKernel.Import(solid, repairOrientation: false);
			var toolManifold = ManifoldKernel.Import(tool, repairOrientation: false);

			// The check above is not the same check. A mesh can carry triangles and still import
			// to nothing: a zero-thickness shell - PlatonicSolids.CreateCube(2, 2, 0), or any
			// flattened solid - is closed, passes every mesh check, and imports with no error at
			// all, because the kernel welds its two coincident sides together and is left with an
			// empty manifold. That is the input the kernel's own early exit answers with the other
			// operand's clone, so it has to be caught after the import or not at all.
			ThrowIfNoVolume(solidManifold, nameof(solid));
			ThrowIfNoVolume(toolManifold, nameof(tool));

			var result = Morph(solidManifold, toolManifold, inset, reporter, cancellationToken, out ErosionPath erosionPath);

			return (ManifoldKernel.ToMesh(result, inset ? "minkowski difference" : "minkowski sum"), erosionPath);
		}

		/// <summary>
		/// <see cref="Run"/> on a worker, with a yield to the UI on either side of it.
		/// </summary>
		/// <remarks>
		/// Everything is inside the worker, the import included: uploading a large mesh is
		/// seconds of work on its own, so leaving it on the caller's thread would freeze the
		/// frame before the bar had moved once.
		/// </remarks>
		private static async Task<(Mesh Mesh, ErosionPath Path)> RunAsync(
			Mesh solid,
			Mesh tool,
			bool inset,
			ProgressReporter reporter,
			CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(solid);
			ArgumentNullException.ThrowIfNull(tool);

			// Before the work, so a host that shares one thread between the job and the UI
			// paints the bar at zero rather than showing it for the first time once the
			// operation has already finished.
			await (reporter?.YieldToUi() ?? default);

			var result = await Task.Run(
				() => Run(solid, tool, inset, reporter, cancellationToken),
				cancellationToken);

			await (reporter?.YieldToUi() ?? default);

			return result;
		}

		/// <summary>
		/// The kernel call itself, with the caller's <see cref="CancellationToken"/> bridged
		/// into the kernel's own cancellation flag and its phase callback adapted to the
		/// pipeline's reporter.
		/// </summary>
		/// <remarks>
		/// Line for line the shape of <c>ManifoldKernel.Boolean</c>, and for the same reasons:
		/// a token that can never be signalled allocates nothing and takes the uncancellable
		/// path, and <b>completion wins</b> - a kernel that finished before it observed the
		/// flag returns its result rather than throwing. The kernel reports cancellation as
		/// <see cref="RustStatus.Cancelled"/> on an empty result; every caller above here is
		/// written against a thrown <see cref="OperationCanceledException"/>.
		/// </remarks>
		private static RustManifold Morph(
			RustManifold solid,
			RustManifold tool,
			bool inset,
			ProgressReporter reporter,
			CancellationToken cancellationToken,
			out ErosionPath erosionPath)
		{
			// A dilation has no closed form to decline, so it is always the sweep. Nothing public
			// surfaces the path for a sum - MinkowskiSum has no such overload - so this is only
			// here to give the out parameter a defined value on that leg.
			erosionPath = ErosionPath.Sweep;

			// HasTarget rather than a null check: ProgressReporter.Null and any reporter built
			// around a null action are both "nobody is watching", and the second would slip
			// past a reference comparison and then hand the adapter a null action.
			var adapter = reporter == null || !reporter.HasTarget
				? null
				: new BooleanProgressAdapter(reporter, 0, 1, 1, inset ? "Erode" : "Dilate");

			// The adapter, not the raw reporter: it swallows a throwing sink, which matters
			// because this callback is invoked from inside the kernel's hull loop, where an
			// escaping exception would lose geometry that had otherwise been computed.
			var progress = adapter == null
				? null
				: new RustProgressReporter((phase, fraction) => adapter.Report((RustPhases.Name(phase), fraction)));

			// One token per operation, as CancelToken's own remarks require: it registers on
			// the caller's source and is never unregistered, so a token that outlived the call
			// would stay rooted in that source's callback list. A token that can never be
			// signalled allocates nothing and leaves the kernel on its uncancellable path,
			// which is byte-for-byte what ran before cancellation existed.
			var token = cancellationToken.CanBeCanceled
				? new RustCancelToken(cancellationToken)
				: null;

			RustManifold result;

			// The closed form first, and only for an erosion: it is the same answer two
			// convex hulls instead of one hull and one boolean per triangle. It declines -
			// answers false, having reported nothing - for anything it cannot prove itself
			// on, a non-convex operand first among them, and the line below is then exactly
			// the call that ran before it existed. A cancelled run comes back as *applied*
			// with a Cancelled status, so a cancel cannot fall through into the slow path it
			// was cancelled out of.
			if (inset && solid.TryConvexErosion(tool, token, progress, out RustManifold closedForm))
			{
				result = closedForm;
				erosionPath = ErosionPath.ClosedForm;
			}
			else
			{
				result = inset
					? solid.MinkowskiDifference(tool, token, progress)
					: solid.MinkowskiSum(tool, token, progress);
			}

			if (token != null && result.Status() == RustStatus.Cancelled)
			{
				throw new OperationCanceledException(cancellationToken);
			}

			return result;
		}

		private static void ThrowIfEmpty(Mesh mesh, string parameterName)
		{
			if (mesh.Vertices.Count == 0 || mesh.Faces.Count == 0)
			{
				throw new ArgumentException("A Minkowski operand needs geometry; this mesh has none.", parameterName);
			}
		}

		/// <summary>
		/// Refuses an operand that imported cleanly and holds nothing.
		/// </summary>
		private static void ThrowIfNoVolume(RustManifold manifold, string parameterName)
		{
			if (manifold.IsEmpty())
			{
				throw new ArgumentException(
					"A Minkowski operand needs volume; this mesh is closed but encloses nothing, so the kernel holds it as empty.",
					parameterName);
			}
		}
	}
}
