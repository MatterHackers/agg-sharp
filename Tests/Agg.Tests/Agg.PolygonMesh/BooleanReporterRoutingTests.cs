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
	/// Which n-ary route the boolean kernel takes has to be decided by whether anyone is actually
	/// watching, not by whether a reporter object exists.
	/// </summary>
	/// <remarks>
	/// The two routes - the kernel's CSG tree and the explicit pairwise left fold - are two
	/// evaluation orders over the same operands, so they are free to build the same solid out of
	/// different vertices. That is fine as long as a caller cannot pick between them by accident.
	/// It could: <see cref="ProgressReporter.Null"/> and <c>new ProgressReporter(null)</c> report
	/// nowhere, but both convert to a NON-null <c>Action</c> (the conversion hands back the
	/// reporter's own <c>Report</c> method group), so a null check on the sink said "somebody is
	/// watching" and routed the boolean down the fold. These tests pin that a do-nothing reporter
	/// gets exactly what null gets.
	/// </remarks>
	public class BooleanReporterRoutingTests
	{
		/// <summary>
		/// Four solids, no two of them the same size and none aligned to another, each overlapping
		/// its neighbour.
		/// </summary>
		/// <remarks>
		/// Asymmetric on purpose: a left fold and an n-ary tree combine different pairs first, and
		/// on a symmetric chain of identical cubes the two orders can easily land on the same
		/// answer and hide the divergence. The rotations put the intersection curves at
		/// coordinates that are not shared between any two pairings.
		/// </remarks>
		private static List<(Mesh mesh, Matrix4X4 matrix)> AsymmetricOperands()
		{
			return new List<(Mesh, Matrix4X4)>
			{
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
				(PlatonicSolids.CreateCube(7, 12, 9), Matrix4X4.CreateRotationZ(0.3) * Matrix4X4.CreateTranslation(5.5, 1.25, 0)),
				(PlatonicSolids.CreateCube(9, 6, 11), Matrix4X4.CreateRotationX(0.45) * Matrix4X4.CreateTranslation(10.75, -0.5, 1.5)),
				(PlatonicSolids.CreateCube(6, 8, 7), Matrix4X4.CreateRotationY(0.2) * Matrix4X4.CreateTranslation(15.25, 2, -1)),
			};
		}

		private static Mesh Union(Action<double, string> reporter)
		{
			return BooleanProcessing.DoArray(
				AsymmetricOperands(),
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				reporter,
				CancellationToken.None);
		}

		private static Task<Mesh> UnionAsync(ProgressReporter reporter)
		{
			return BooleanProcessing.DoArrayAsync(
				AsymmetricOperands(),
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				reporter,
				CancellationToken.None);
		}

		/// <summary>
		/// Vertex for vertex and index for index, with no tolerance: the point is that the two
		/// calls ran the same arithmetic in the same order, not that they agree about the shape.
		/// </summary>
		private static async Task AssertSameMesh(Mesh expected, Mesh actual, string because)
		{
			await Assert.That(actual.Vertices.Count).IsEqualTo(expected.Vertices.Count).Because(because);
			await Assert.That(actual.Faces.Count).IsEqualTo(expected.Faces.Count).Because(because);

			for (int i = 0; i < expected.Vertices.Count; i++)
			{
				await Assert.That(actual.Vertices[i]).IsEqualTo(expected.Vertices[i]).Because(because);
			}

			for (int i = 0; i < expected.Faces.Count; i++)
			{
				await Assert.That((actual.Faces[i].v0, actual.Faces[i].v1, actual.Faces[i].v2))
					.IsEqualTo((expected.Faces[i].v0, expected.Faces[i].v1, expected.Faces[i].v2))
					.Because(because);
			}
		}

		[Test]
		public async Task ATargetlessReporterBuildsTheSameSolidAsNone()
		{
			var withNoReporter = Union(null);

			// ProgressReporter.Null converts to a non-null Action, so this is the shape that used
			// to buy the pairwise fold while the line above got the CSG tree.
			var withTargetlessReporter = Union(ProgressReporter.Null);

			await AssertSameMesh(
				withNoReporter,
				withTargetlessReporter,
				"a reporter nobody is watching must not change which n-ary route the kernel takes");
		}

		[Test]
		public async Task AReporterBuiltAroundANullActionBuildsTheSameSolidAsNone()
		{
			// Not the Null singleton: a reference comparison would catch that one and still miss
			// this, which is just as unwatched.
			var withNoReporter = Union(null);
			var withTargetlessReporter = Union(new ProgressReporter(null));

			await AssertSameMesh(
				withNoReporter,
				withTargetlessReporter,
				"nobody-is-watching arrives in more than one shape and every shape has to route alike");
		}

		[Test]
		public async Task ATargetlessReporterBuildsTheSameSolidAsNoneOnTheAsyncPath()
		{
			var withNoReporter = await UnionAsync(null);
			var withTargetlessReporter = await UnionAsync(ProgressReporter.Null);

			await AssertSameMesh(
				withNoReporter,
				withTargetlessReporter,
				"the async entry point routes by the same rule as the synchronous one");
		}

		[Test]
		public async Task ATargetedReporterStillGetsProgressForEveryPair()
		{
			var reports = new List<(double ratio, string message)>();

			Union((ratio, message) => reports.Add((ratio, message)));

			// One "combining" report closes out each pairwise boolean of the fold, so a four
			// operand union has three: the fold is what a watched boolean is routed to, and this
			// is the observable that says it still is.
			int operandCount = AsymmetricOperands().Count;
			await Assert.That(reports.Count(report => report.message.EndsWith("combining", StringComparison.Ordinal)))
				.IsEqualTo(operandCount - 1)
				.Because("a watched n-ary boolean reports the completion of every pair it folds");

			await Assert.That(reports.All(report => report.ratio >= 0 && report.ratio <= 1)).IsTrue();
		}
	}
}
