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
using ManifoldRust;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The ManifoldRust boolean backend - the only native boolean engine - exercised
	/// through the same public <see cref="BooleanProcessing"/> entry points the
	/// application uses.
	/// </summary>
	/// <remarks>
	/// <see cref="BooleanProcessing.LastBackendUsed"/> is a process-wide static that every
	/// boolean overwrites, so the tests here that assert on it run in the
	/// <see cref="ParallelKey"/> group. <see cref="MeshCsgTests"/> and
	/// <see cref="FaceColorTests"/> are in that group too - not because they assert on it,
	/// but because they run booleans, and one landing between a DoArray call here and the
	/// assertion that follows it would clobber the value being asserted.
	/// </remarks>
	[NotInParallel(ParallelKey)]
	public class ManifoldRustBackendTests
	{
		/// <summary>
		/// Serializes every test that asserts on, or would overwrite, the process-wide
		/// <see cref="BooleanProcessing.LastBackendUsed"/>. Shared with
		/// <see cref="MeshCsgTests"/> and <see cref="FaceColorTests"/>.
		/// </summary>
		public const string ParallelKey = "BooleanEngineStatics";

		private static Mesh UnionSubtractIntersect(CsgModes operation, Mesh a, Matrix4X4 matrixA, Mesh b, Matrix4X4 matrixB)
		{
			var result = BooleanProcessing.DoArray(
				new[] { (a, matrixA), (b, matrixB) },
				operation,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None);

			// What CombineParticipants does to a boolean result before anyone looks at it.
			// The raw MeshGL output splits vertices at run boundaries, so topology questions
			// are only meaningful after the merge.
			result.CleanAndMerge();
			result.RemoveUnusedVertices();

			return result;
		}

		[Test]
		public async Task UnionOfOverlappingCubesIsClosedSolid()
		{
			var result = UnionSubtractIntersect(
				CsgModes.Union,
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(-3, 0, 0),
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0));

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();

			// Without this the test passes just as well when the Rust engine throws and
			// CsgBySlicing quietly produces the same box.
			await Assert.That(BooleanProcessing.LastBackendUsed).IsEqualTo(BooleanProcessing.BackendManifoldRust);

			// The union of the two boxes is one box spanning both.
			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(16.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(10.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(10.0).Within(0.001);
		}

		[Test]
		public async Task SubtractOfOverlappingCubesIsClosedSolid()
		{
			var result = UnionSubtractIntersect(
				CsgModes.Subtract,
				PlatonicSolids.CreateCube(20, 20, 20), Matrix4X4.Identity,
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(10, 10, 10));

			// A corner bite out of a cube: still one closed solid, and more faces than the
			// 12 the cube started with.
			await Assert.That(result.Faces.Count).IsGreaterThan(12);
			await Assert.That(result.IsManifold()).IsTrue();

			// Without this the test passes just as well when the Rust engine throws and
			// CsgBySlicing quietly produces the same box.
			await Assert.That(BooleanProcessing.LastBackendUsed).IsEqualTo(BooleanProcessing.BackendManifoldRust);

			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(20.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(20.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(20.0).Within(0.001);
		}

		[Test]
		public async Task IntersectOfOverlappingCubesIsTheOverlapBox()
		{
			var result = UnionSubtractIntersect(
				CsgModes.Intersect,
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(-3, 0, 0),
				PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0));

			await Assert.That(result.Faces.Count).IsGreaterThan(0);
			await Assert.That(result.IsManifold()).IsTrue();

			// Without this the test passes just as well when the Rust engine throws and
			// CsgBySlicing quietly produces the same box.
			await Assert.That(BooleanProcessing.LastBackendUsed).IsEqualTo(BooleanProcessing.BackendManifoldRust);

			// The overlap of the two boxes is a 4 x 10 x 10 box.
			var bounds = result.GetAxisAlignedBoundingBox();
			await Assert.That(bounds.XSize).IsEqualTo(4.0).Within(0.001);
			await Assert.That(bounds.YSize).IsEqualTo(10.0).Within(0.001);
			await Assert.That(bounds.ZSize).IsEqualTo(10.0).Within(0.001);
		}

		/// <summary>
		/// An input the kernel cannot accept must come back as geometry from the managed
		/// CsgBySlicing fallback, not as an exception.
		/// </summary>
		[Test]
		public async Task NonManifoldInputFallsBackToCsgBySlicing()
		{
			var result = BooleanProcessing.DoArray(
				new[]
				{
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
					(OpenBox(8), Matrix4X4.Identity),
				},
				CsgModes.Subtract,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None);

			await Assert.That(result).IsNotNull();
			await Assert.That(BooleanProcessing.LastBackendUsed).IsEqualTo(BooleanProcessing.BackendCsgBySlicing)
				.Because("an open surface never reaches the kernel - DoArray's IsManifold gate diverts it");
		}

		/// <summary>
		/// An operand the kernel rejects is thrown on rather than absorbed. A boolean
		/// silently treats an error-status operand as empty geometry and still reports
		/// success, so absorbing it would show up as a part missing from the model with
		/// nothing logged.
		/// </summary>
		[Test]
		public async Task AnInputTheKernelRejectsThrowsRatherThanGoingMissing()
		{
			var items = new[]
			{
				(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.Identity),
				(CubeWithANonFiniteVertex(), Matrix4X4.Identity),
			};

			// A NaN coordinate leaves the surface closed, so DoArray's topological gate
			// waves it through and it is the kernel that objects - which is exactly the
			// case this path exists for.
			await Assert.That(items[1].Item1.IsManifold()).IsTrue()
				.Because("the test is only meaningful if the mesh gets past DoArray's IsManifold gate");

			var thrown = Assert.Throws<InvalidOperationException>(() => BooleanProcessing.DoArrayViaManifoldRust(
				items,
				CsgModes.Union,
				CancellationToken.None,
				null,
				1,
				0,
				null));

			await Assert.That(thrown.Message).Contains(ManifoldStatus.NonFiniteVertex.ToString())
				.Because("the status is the whole diagnostic value of refusing the input");

			// And through the public entry point the same rejection is a fallback, not a
			// failure the caller ever sees.
			var result = BooleanProcessing.DoArray(
				items,
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				CancellationToken.None);

			await Assert.That(result).IsNotNull();
			await Assert.That(BooleanProcessing.LastBackendUsed).IsEqualTo(BooleanProcessing.BackendCsgBySlicing);
		}

		/// <summary>
		/// Cancellation has to reach the caller. The general catch in
		/// <see cref="BooleanProcessing.DoArray"/> exists to fall back to CsgBySlicing when
		/// the native engine fails; a cancelled operation is not a failure, and re-running
		/// it in managed code would only spend the same abandoned time again.
		/// </summary>
		[Test]
		public async Task CancelledTokenPropagatesRatherThanFallingBack()
		{
			using var cancelled = new CancellationTokenSource();
			cancelled.Cancel();

			await Assert.That(() => BooleanProcessing.DoArray(
				new[]
				{
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(-3, 0, 0)),
					(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(3, 0, 0)),
				},
				CsgModes.Union,
				ProcessingModes.Polygons,
				ProcessingResolution._64,
				ProcessingResolution._64,
				null,
				cancelled.Token)).Throws<OperationCanceledException>();
		}

		/// <summary>
		/// A box missing its top face - edge manifold nowhere, so
		/// <see cref="MeshExtensionMethods.IsManifold"/> rejects it and DoArray never reaches
		/// the kernel.
		/// </summary>
		private static Mesh OpenBox(double size)
		{
			var mesh = PlatonicSolids.CreateCube(size, size, size);

			// Drop the two triangles of one face. Which two does not matter; any hole makes
			// the surface open.
			mesh.Faces.RemoveAt(mesh.Faces.Count - 1);
			mesh.Faces.RemoveAt(mesh.Faces.Count - 1);

			return mesh;
		}

		/// <summary>
		/// A cube whose surface is still closed but one of whose corners is not a number.
		/// Topologically perfect, geometrically meaningless - which is how it slips past
		/// <see cref="MeshExtensionMethods.IsManifold"/> and lands on the kernel.
		/// </summary>
		private static Mesh CubeWithANonFiniteVertex()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var corner = mesh.Vertices[0];
			mesh.Vertices[0] = new Vector3Float(float.NaN, corner.Y, corner.Z);

			return mesh;
		}
	}
}
