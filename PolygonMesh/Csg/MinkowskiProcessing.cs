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
using RustParallel = ManifoldSharp.ManifoldParallel;
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
	/// for a few hundred triangles, minutes for a few thousand. A <em>dilation</em> of this kind
	/// never reaches the sweep: this class routes it to the kernel's
	/// <c>Manifold.TryDilateByConvex</c>, which builds the same hulls and unions them as a
	/// balanced tree across every core - 5-8x faster in Release on 6000-12000-triangle parts,
	/// with the same volume and genus (manifold-sharp divergence ledger entry 6). An erosion the
	/// closed form below declines goes to <c>Manifold.TryErodeByConvex</c> next: the same tree
	/// without the solid leaf, then one solid-minus-union, about 2x the sweep's speed. Both trees
	/// take solids with several shells, nested or overlapping; only a non-convex tool (or an
	/// empty, soup or errored operand) still sweeps either way.</item>
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
		/// <remarks>
		/// Also reported when the kernel's erosion tree (<c>Manifold.TryErodeByConvex</c>) reduced
		/// those same hulls instead: it is the same cost regime - a hull and a boolean's worth of
		/// work per triangle, about 2x faster, not three orders - so a caller weighting a bar by
		/// this signal weights it correctly. <see cref="MinkowskiProcessing.ErosionTreeRuns"/>
		/// tells the two apart for tests.
		/// </remarks>
		Sweep,
	}

	public static class MinkowskiProcessing
	{
		private static long dilationTreeRuns;

		/// <summary>
		/// How many dilations in this process took the kernel's parallel union tree rather than the
		/// sweep. A path signal for tests - a caller cannot see which algorithm answered otherwise,
		/// since both give the same solid - in the spirit of <see cref="ErosionPath"/>.
		/// </summary>
		public static long DilationTreeRuns => Interlocked.Read(ref dilationTreeRuns);

		private static long erosionTreeRuns;

		/// <summary>
		/// How many erosions in this process took the kernel's parallel union tree rather than the
		/// sweep - the erosion counterpart of <see cref="DilationTreeRuns"/>. Both report
		/// <see cref="ErosionPath.Sweep"/>, so this is the only way to tell them apart.
		/// </summary>
		public static long ErosionTreeRuns => Interlocked.Read(ref erosionTreeRuns);

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
		/// The solid exactly as Dilate and Erode read it: imported the way their operands are (a backward
		/// patch rewound, split seams joined) and every shell whose winding contradicts its nesting turned
		/// the right way, so an inside-out part - or one inside-out body beside a correct one - comes back as
		/// the solid it looks like, while a correctly wound cavity stays a cavity.
		/// </summary>
		/// <remarks>
		/// For a caller that combines an operand with its own erosion or dilation (Hollow Out appends the
		/// erosion inside out) and needs both halves to agree on which way every shell faces.
		/// </remarks>
		/// <param name="solid">The shape to orient; must not be empty.</param>
		/// <param name="cancellationToken">Checked between shells of the repair.</param>
		/// <returns>The oriented solid as a mesh.</returns>
		public static Mesh OrientShellsAsSolid(Mesh solid, CancellationToken cancellationToken)
		{
			ArgumentNullException.ThrowIfNull(solid);
			ThrowIfEmpty(solid, nameof(solid));
			var oriented = MinkowskiShellOrientation.Repair(ImportOperand(solid), cancellationToken);
			return ManifoldKernel.ToMesh(oriented, "shell orientation");
		}

		/// <summary>
		/// Imports both operands, runs the kernel's Minkowski and reads the result back.
		/// </summary>
		/// <remarks>
		/// The import is <see cref="ImportOperand"/>: the boolean operand import, weld retry included,
		/// plus one more retry that rewinds a backward patch. So every mesh usable as a boolean operand
		/// is usable here, and so is a closed surface with triangles wound against their neighbours,
		/// which a boolean refuses as NotClosed. Anything else fails the same way a boolean does, with
		/// the same message. Then every shell whose winding contradicts its nesting is rewound
		/// (<see cref="MinkowskiShellOrientation.Repair"/>), automatically and without a warning: a part saved
		/// inside out dilates and erodes as the solid it looks like, because a Dilate that shrinks a
		/// part is never the intended result. Booleans do not do this - there it stays the opt-in
		/// KeepInsideOutGeometry / RepairWindingOrientation choice, since a boolean has legitimate
		/// uses for a negative shell.
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

			var solidManifold = MinkowskiShellOrientation.Repair(ImportOperand(solid), cancellationToken);
			var toolManifold = MinkowskiShellOrientation.Repair(ImportOperand(tool), cancellationToken);

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
		/// <see cref="ManifoldKernel.Import"/>, with one more chance for a closed surface that has a
		/// patch of triangles wound against the rest of it, and one for a solid whose seams are split.
		/// </summary>
		/// <remarks>
		/// The import balances directed edges, so a backward patch reads to it as
		/// <see cref="RustStatus.NotClosed"/> even though every edge has its two faces. The reported
		/// part (a 37,120-triangle mouse body with 320 triangles reversed) failed Dilate that way.
		/// Such a surface has exactly one consistent winding up to its overall sign, and
		/// <see cref="ConsistentWinding"/> keeps each surface's majority sign, so an inside-out shell
		/// still means what it meant before - the shell-level repair is a separate step (see Run).
		/// <para>
		/// Here rather than in <see cref="ManifoldKernel.Import"/>: booleans and the bevel's output
		/// gates classify meshes through that import, and a gate that quietly accepted a backward
		/// patch would publish it. A Minkowski result is rebuilt from scratch by the kernel, so the
		/// operand's winding never reaches the output.
		/// </para>
		/// <para>
		/// A split-seam solid (every triangle its own three vertices, as STL stores it) imports
		/// cleanly but only as triangle soup, because the import pairs edges by vertex index - and
		/// the kernel's Minkowski refuses a soup operand as NotManifold. So a soup import is retried
		/// with its exactly coincident vertices joined, and kept only if that pairs up. A mesh that
		/// is soup for a real reason (an edge shared by four faces) stays soup and fails as before.
		/// </para>
		/// </remarks>
		private static RustManifold ImportOperand(Mesh mesh)
		{
			var imported = ImportRewindingABackwardPatch(mesh);

			if (imported.AsImpl().IsSoup)
			{
				var joined = ConsistentWinding.JoinCoincidentVertices(mesh);

				if (joined != null
					&& TryImport(joined, out var paired)
					&& !paired.AsImpl().IsSoup)
				{
					return paired;
				}
			}

			return imported;
		}

		/// <summary>
		/// The import with the backward-patch retry described on <see cref="ImportOperand"/>.
		/// </summary>
		private static RustManifold ImportRewindingABackwardPatch(Mesh mesh)
		{
			try
			{
				return ManifoldKernel.Import(mesh, repairOrientation: false);
			}
			catch (MeshImportRejectedException refused) when (refused.Status == RustStatus.NotClosed)
			{
				// Exact positions first, as the kernel welds. Then the tolerance-welded copy, for a
				// backward patch that only joins the rest across seams apart by a rounding step -
				// read unwelded, such a patch is a surface of its own and has nothing to disagree with.
				var rewound = ConsistentWinding.Rewind(mesh);
				if (rewound != null
					&& TryImport(rewound, out var imported))
				{
					return imported;
				}

				var welded = ManifoldKernel.WeldSeams(mesh);
				var weldedRewound = welded == null ? null : ConsistentWinding.Rewind(welded);
				if (weldedRewound != null
					&& TryImport(weldedRewound, out imported))
				{
					return imported;
				}

				// The original complaint, not one about a copy the caller never handed in.
				throw;
			}
		}

		private static bool TryImport(Mesh mesh, out RustManifold imported)
		{
			try
			{
				imported = ManifoldKernel.Import(mesh, repairOrientation: false);
				return true;
			}
			catch (MeshImportRejectedException)
			{
				imported = null;
				return false;
			}
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
			// ErosionPath names erosion algorithms only. Nothing public surfaces a path for a sum -
			// MinkowskiSum has no such overload - so on that leg this only gives the out parameter
			// a defined value.
			erosionPath = ErosionPath.Sweep;

			// HasTarget rather than a null check: ProgressReporter.Null and any reporter built
			// around a null action are both "nobody is watching", and the second would slip
			// past a reference comparison and then hand the adapter a null action.
			var adapter = reporter == null || !reporter.HasTarget
				? null
				: new BooleanProgressAdapter(reporter, 0, 1, 1, inset ? "Erode" : "Dilate");

			// The adapter, not the raw reporter: it swallows a throwing sink, which matters
			// because this callback is invoked from inside the kernel's hull loop, where an
			// escaping exception would lose geometry that had otherwise been computed. The kernel counts
			// work items; MinkowskiProgressModel reweights them by their measured cost so the bar tracks
			// the clock (and so a time-remaining estimate built on it is honest).
			int solidTriangles = solid.NumTri();
			var progress = adapter == null
				? null
				: new RustProgressReporter((phase, fraction) => adapter.Report((
					RustPhases.Name(phase),
					fraction.HasValue ? MinkowskiProgressModel.TimeFraction(solidTriangles, fraction.Value) : null)));

			// The union tree (dilation and erosion alike) counts different units (hull, leaf, tree
			// node, and for an erosion the closing subtraction) and reports them from worker threads,
			// so two reports can arrive out of order; it gets its own time mapping, and the adapter's
			// high-water mark keeps the bar from stepping back. Its hull count is not the triangle
			// count - a dilation builds one hull per convex patch, and nested shells are rebuilt
			// first - so it is recovered from the phase total the kernel set.
			int treeParallelism = RustParallel.Enabled ? Environment.ProcessorCount : 1;
			RustProgressReporter treeProgress = null;
			treeProgress = adapter == null
				? null
				: new RustProgressReporter((phase, fraction) => adapter.Report((
					RustPhases.Name(phase),
					fraction.HasValue
						? MinkowskiProgressModel.TreeTimeFraction(
							MinkowskiProgressModel.TreeHullUnits((long)treeProgress.PhaseTotal, inset), fraction.Value, treeParallelism, inset)
						: null)));

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
			else if (!inset && solid.TryDilateByConvex(tool, token, treeProgress, out RustManifold treeSum))
			{
				// Nonconvex (+) convex through the kernel's parallel union tree: the same hulls,
				// unioned as a balanced tree instead of serial 1000-hull batches. Same volume and
				// genus as the sweep, different triangles. It declines, having reported nothing,
				// for any other pair of operands, and the sweep below runs as before; a cancelled
				// run comes back applied, as the closed form's does.
				result = treeSum;
				Interlocked.Increment(ref dilationTreeRuns);
			}
			else if (inset && solid.TryErodeByConvex(tool, token, treeProgress, out RustManifold treeDifference))
			{
				// The erosion the closed form declined, through the same tree: the same hulls
				// reduced without the solid, then the solid minus their union once. Same volume
				// and genus as the sweep, nested shells included; it declines, having reported
				// nothing, for a non-convex tool or an empty, soup or errored operand, and the
				// sweep below runs as before. Reported as Sweep - the same cost regime (see
				// ErosionPath.Sweep).
				result = treeDifference;
				Interlocked.Increment(ref erosionTreeRuns);
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
