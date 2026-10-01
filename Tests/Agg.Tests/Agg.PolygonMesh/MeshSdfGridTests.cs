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
using System.Diagnostics;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh.Csg;
using MatterHackers.PolygonMesh.Sdf;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	/// <summary>
	/// The drag preview's distance field: offsets of a 20 mm cube land on the exact rounded box and
	/// the shrunk cube, an inside-out cube reads as the same solid, and one field serves every step.
	/// </summary>
	public class MeshSdfGridTests
	{
		private const double Side = 20;
		private const double Radius = 2;

		[Test]
		public async Task DilatedCubeMatchesRoundedBox()
		{
			var sdf = new MeshSdfGrid(PlatonicSolids.CreateCube(Side, Side, Side), Radius);
			double volume = await ClosedVolume(sdf.Extract(Radius));

			await Assert.That(volume).IsEqualTo(RoundedBoxVolume(Side, Radius)).Within(Tolerance(Side + (2 * Radius), sdf.CellSize));
		}

		[Test]
		public async Task ErodedCubeMatchesShrunkCube()
		{
			var sdf = new MeshSdfGrid(PlatonicSolids.CreateCube(Side, Side, Side), Radius);
			double shrunk = Side - (2 * Radius);

			await Assert.That(await ClosedVolume(sdf.Extract(-Radius))).IsEqualTo(shrunk * shrunk * shrunk).Within(Tolerance(shrunk, sdf.CellSize));
		}

		[Test]
		public async Task InsideOutCubeGivesTheSameOffsets()
		{
			var insideOut = PlatonicSolids.CreateCube(Side, Side, Side);
			insideOut.ReverseFaces();
			var sdf = new MeshSdfGrid(insideOut, Radius);
			double shrunk = Side - (2 * Radius);

			await Assert.That(await ClosedVolume(sdf.Extract(Radius))).IsEqualTo(RoundedBoxVolume(Side, Radius)).Within(Tolerance(Side + (2 * Radius), sdf.CellSize));
			await Assert.That(await ClosedVolume(sdf.Extract(-Radius))).IsEqualTo(shrunk * shrunk * shrunk).Within(Tolerance(shrunk, sdf.CellSize));
		}

		[Test]
		public async Task FieldIsComputedOnceForEveryStep()
		{
			var sdf = new MeshSdfGrid(PlatonicSolids.CreateCube(Side, Side, Side), Radius, 48);
			for (double iso = -Radius; iso <= Radius; iso += 0.5)
			{
				sdf.Extract(iso);
			}

			await Assert.That(sdf.GridBuilds).IsEqualTo(1);
		}

		[Test]
		public async Task SeparateBodiesOneInsideOutOffsetLikeBothRightWayOut()
		{
			var part = PlatonicSolids.CreateCube(10, 10, 10);
			var insideOut = PlatonicSolids.CreateCube(10, 10, 10);
			insideOut.ReverseFaces();
			part.CopyAllFaces(insideOut, Matrix4X4.CreateTranslation(30, 0, 0));
			var rightWayOut = PlatonicSolids.CreateCube(10, 10, 10);
			rightWayOut.CopyAllFaces(PlatonicSolids.CreateCube(10, 10, 10), Matrix4X4.CreateTranslation(30, 0, 0));
			var sdf = new MeshSdfGrid(part, 1);
			var reference = new MeshSdfGrid(rightWayOut, 1);

			foreach (var iso in new[] { 1.0, -1.0 })
			{
				await Assert.That(await ClosedVolume(sdf.Extract(iso))).IsEqualTo(await ClosedVolume(reference.Extract(iso))).Within(1e-6);
			}

			// And both land on the analytic offsets. Two 10 mm cubes 30 mm apart make a coarse
			// grid (0.37 mm cells), and marching cubes bevels the eroded cubes' sharp edges by a
			// fraction of a cell, which dominates here: 1% of 1024 measured.
			await Assert.That(await ClosedVolume(sdf.Extract(1))).IsEqualTo(2 * RoundedBoxVolume(10, 1)).Within(0.02 * 2 * RoundedBoxVolume(10, 1));
			await Assert.That(await ClosedVolume(sdf.Extract(-1))).IsEqualTo(2 * 8.0 * 8 * 8).Within(0.02 * 2 * 8.0 * 8 * 8);
		}

		[Test]
		public async Task CavityGrowsWhenErodedAndClosesWhenDilated()
		{
			var part = PlatonicSolids.CreateCube(Side, Side, Side);
			var cavity = PlatonicSolids.CreateCube(8, 8, 8);
			cavity.ReverseFaces();
			part.CopyAllFaces(cavity, Matrix4X4.Identity);
			var sdf = new MeshSdfGrid(part, 5);

			// Eroded by 1: the outside shrinks to 18 and the cavity grows to a rounded 10.
			double eroded = (18.0 * 18 * 18) - RoundedBoxVolume(8, 1);
			await Assert.That(await ClosedVolume(sdf.Extract(-1))).IsEqualTo(eroded).Within(Tolerance(18, sdf.CellSize) + Tolerance(10, sdf.CellSize));

			// Dilated by 5: the cavity's 8 mm shrinks past nothing and the part is solid.
			await Assert.That(await ClosedVolume(sdf.Extract(5))).IsEqualTo(RoundedBoxVolume(Side, 5)).Within(Tolerance(Side + 10, sdf.CellSize));
		}

		[Test]
		public async Task SmallOpenHoleStillOffsetsLikeTheClosedPart()
		{
			var closed = PlatonicSolids.CreateCube(Side, Side, Side);
			var holed = PlatonicSolids.CreateCube(Side, Side, Side);
			holed.Faces.RemoveAt(0);
			var closedSdf = new MeshSdfGrid(closed, Radius);
			var holedSdf = new MeshSdfGrid(holed, Radius);

			// Every voxel keeps its sign (the vote outruns the one wrong axis through the hole),
			// but the distance near the hole is to its rim, not to a face, so both offsets bend
			// into it: grown sags about 3.7%, shrunk rises about 4.4%. That is the true offset
			// of an open part. Marching cubes skipping cells far from the iso once tore the
			// surface here (the field jumps from -6 to +6 across the hole): shrunk came out 7% low.
			holedSdf.Build();
			await Assert.That(WrongSignVoxels(holedSdf)).IsEqualTo(0);

			double shrunk = await ClosedVolume(closedSdf.Extract(-Radius));
			await Assert.That(await ClosedVolume(holedSdf.Extract(-Radius))).IsEqualTo(shrunk).Within(0.05 * shrunk);

			double grown = await ClosedVolume(closedSdf.Extract(Radius));
			await Assert.That(await ClosedVolume(holedSdf.Extract(Radius))).IsEqualTo(grown).Within(0.05 * grown);
		}

		[Test]
		public async Task GridOnTheFacesLeavesNoWrongSignVoxel()
		{
			// With a 2 mm range at 52 voxels the cell is exactly 0.5 mm and the grid origin sits
			// 3 mm (the range plus two cells) below the cube's minimum, so grid lines run along its faces, its edges and
			// the diagonals splitting each face into triangles - every parity graze at once.
			var sdf = new MeshSdfGrid(PlatonicSolids.CreateCube(Side, Side, Side), Radius, 52);
			sdf.Build();
			await Assert.That(sdf.CellSize).IsEqualTo(0.5);

			await Assert.That(WrongSignVoxels(sdf)).IsEqualTo(0);
		}

		[Test]
		public async Task CancelledBuildStopsPromptly()
		{
			// A 256 grid around an 8k-triangle ball: the band flood processes millions of voxels
			// in passes of a few hundred thousand, so a cancel at 20000 lands mid-pass.
			const long CancelAt = 20000;
			var ball = MinkowskiProcessing.SphereMesh(10, 128);
			var sdf = new MeshSdfGrid(ball, 5, MeshSdfGrid.MaxResolution);
			using var cancel = new CancellationTokenSource();
			sdf.FloodProgress = processed =>
			{
				if (processed == CancelAt)
				{
					cancel.Cancel();
				}
			};

			var clock = Stopwatch.StartNew();
			await Assert.That(() => sdf.Build(cancel.Token)).Throws<OperationCanceledException>();

			// Work, not time. The flood counts a voxel only after reading the abort flag, and
			// polls the token at every 64th count: so after the cancel at CancelAt, the next
			// multiple of 64 (at most 64 counts later) sets the flag, and past that each worker
			// thread can add at most the one voxel it counted between its flag read and the
			// write landing. Workers are pool threads, so the pool's size bounds them.
			long workers = Math.Max(ThreadPool.ThreadCount, Environment.ProcessorCount);
			await Assert.That(sdf.FloodWork - CancelAt).IsLessThanOrEqualTo(64 + workers);
			await Assert.That(sdf.GridBuilds).IsEqualTo(0);

			// A hang guard only: a full build is seconds, a stuck one never ends.
			await Assert.That(clock.Elapsed.TotalSeconds).IsLessThan(60.0);
		}

		/// <summary>
		/// The mesh's signed volume, after asserting it is a closed, consistently wound surface:
		/// every directed edge appears once and its reverse once.
		/// </summary>
		private static async Task<double> ClosedVolume(Mesh mesh)
		{
			var directed = new Dictionary<(int, int), int>();
			foreach (var face in mesh.Faces)
			{
				foreach (var edge in new[] { (face.v0, face.v1), (face.v1, face.v2), (face.v2, face.v0) })
				{
					directed[edge] = directed.GetValueOrDefault(edge) + 1;
				}
			}

			int bad = 0;
			foreach (var (edge, count) in directed)
			{
				if (count != 1 || directed.GetValueOrDefault((edge.Item2, edge.Item1)) != 1)
				{
					bad++;
				}
			}

			await Assert.That(bad).IsEqualTo(0);
			return SignedVolume(mesh);
		}

		/// <summary>
		/// Samples of a centred cube's field whose sign disagrees with the cube, ignoring the ones
		/// on its faces, where either sign is right.
		/// </summary>
		private static int WrongSignVoxels(MeshSdfGrid sdf)
		{
			var field = sdf.Field;
			int wrong = 0;
			for (int k = 0; k < field.nk; k++)
			{
				for (int j = 0; j < field.nj; j++)
				{
					for (int i = 0; i < field.ni; i++)
					{
						var p = sdf.FieldOrigin + (sdf.CellSize * new g3.Vector3d(i, j, k));
						double outside = Math.Max(Math.Abs(p.x), Math.Max(Math.Abs(p.y), Math.Abs(p.z))) - (Side / 2);
						if (Math.Abs(outside) > 1e-6 && (outside < 0) != (field[i, j, k] < 0))
						{
							wrong++;
						}
					}
				}
			}

			return wrong;
		}

		/// <summary>
		/// A cube of side a grown by r: the cube, six slabs, twelve quarter cylinders and eight
		/// sphere octants.
		/// </summary>
		internal static double RoundedBoxVolume(double side, double radius)
		{
			return (side * side * side) + (6 * side * side * radius) + (3 * Math.PI * side * radius * radius) + (4.0 / 3.0 * Math.PI * radius * radius * radius);
		}

		/// <summary>
		/// A fiftieth of a voxel of drift over the surface of a box of this side. The flat faces
		/// interpolate exactly; the error is where the surface bends between samples, and measured
		/// about a tenth of this at 128 voxels (2.3 mm3 of 13587 grown, 0.4 of 4096 shrunk).
		/// </summary>
		private static double Tolerance(double side, double cellSize)
		{
			return 0.02 * 6 * side * side * cellSize;
		}

		internal static double SignedVolume(Mesh mesh)
		{
			double total = 0;
			foreach (var face in mesh.Faces)
			{
				var a = mesh.Vertices[face.v0];
				var b = mesh.Vertices[face.v1];
				var c = mesh.Vertices[face.v2];
				total += ((a.X * ((b.Y * c.Z) - (b.Z * c.Y))) - (a.Y * ((b.X * c.Z) - (b.Z * c.X))) + (a.Z * ((b.X * c.Y) - (b.Y * c.X)))) / 6.0;
			}

			return total;
		}
	}
}
