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
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.RayTracer;
using MatterHackers.RayTracer.Light;
using MatterHackers.RayTracer.Traceable;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;
using AggParallel = MatterHackers.Agg.Parallel;
using RayTracerEngine = MatterHackers.RayTracer.RayTracer;

namespace MatterHackers.VectorMath.Tests
{
	/// <summary>
	/// Execution coverage for <see cref="MatterHackers.Agg.Parallel.Sequential"/>, the single-threaded-host
	/// (wasm) switch. Everything routed through the wrapper - the BVH builders, geometry3Sharp, the ray
	/// tracer - has a sequential branch that was previously only verified by reading it. These tests run
	/// real work both ways and compare the results.
	///
	/// Every test is a keyless <c>[NotInParallel]</c>: the flag is process-wide static state, so two of
	/// these running at once (or alongside anything else that traces or builds a BVH) would see each
	/// other's setting. Each test captures the incoming value and restores it in a finally rather than
	/// blindly clearing it, so a host that legitimately starts up Sequential is not silently un-set.
	/// </summary>
	[NotInParallel]
	public class ParallelSequentialEquivalenceTests
	{
		[Test]
		[NotInParallel]
		public async Task LocallyOrderedClusteringBvhMatchesAcrossSequentialFlag()
		{
			var parallelBuilt = BuildBvh(BvhCreationOptions.LocalOrderClustering, sequential: false);
			var sequentialBuilt = BuildBvh(BvhCreationOptions.LocalOrderClustering, sequential: true);

			await AssertSameBvh(parallelBuilt, sequentialBuilt);
		}

		[Test]
		[NotInParallel]
		public async Task ParallelBinnedSahBvhMatchesAcrossSequentialFlag()
		{
			var parallelBuilt = BuildBvh(BvhCreationOptions.ParallelBinnedSah, sequential: false);
			var sequentialBuilt = BuildBvh(BvhCreationOptions.ParallelBinnedSah, sequential: true);

			await AssertSameBvh(parallelBuilt, sequentialBuilt);
		}

		/// <summary>
		/// A whole (tiny) ray trace, which is where the wrapper's Parallel.For over scanlines lives.
		/// Every scanline writes only its own pixels, so this can be asserted byte-exact.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task RayTracedImageMatchesAcrossSequentialFlag()
		{
			var parallelPixels = TraceSmallScene(sequential: false);
			var sequentialPixels = TraceSmallScene(sequential: true);

			// A scene that rendered nothing would be equal both ways for the wrong reason.
			await Assert.That(parallelPixels.Distinct().Count()).IsGreaterThan(1);
			await Assert.That(sequentialPixels.Length).IsEqualTo(parallelPixels.Length);

			// Byte-exact means position by position: IsEquivalentTo would only compare the two buffers as
			// bags, which for pixels is a histogram and would pass on a scrambled image.
			await Assert.That(FirstDifference(parallelPixels, sequentialPixels)).IsEqualTo(-1);
		}

		[Test]
		[NotInParallel]
		public async Task InvokeRunsActionsInOrderWhenSequential()
		{
			var wasSequential = AggParallel.Sequential;
			try
			{
				var order = new List<int>();

				AggParallel.Sequential = true;
				AggParallel.Invoke(
					() => order.Add(0),
					() => order.Add(1),
					() => order.Add(2),
					() => order.Add(3));

				// Joined rather than compared as collections: IsEquivalentTo ignores order, which is the one
				// thing this test exists to pin.
				await Assert.That(string.Join(",", order)).IsEqualTo("0,1,2,3");

				// The parallel path makes no ordering promise, only that every action ran - so this leg is
				// deliberately order-insensitive, and says so by sorting before the same strict compare.
				var parallelOrder = new List<int>();
				var addLock = new object();
				AggParallel.Sequential = false;
				AggParallel.Invoke(
					() => { lock (addLock) { parallelOrder.Add(0); } },
					() => { lock (addLock) { parallelOrder.Add(1); } },
					() => { lock (addLock) { parallelOrder.Add(2); } },
					() => { lock (addLock) { parallelOrder.Add(3); } });

				await Assert.That(string.Join(",", parallelOrder.OrderBy(ran => ran))).IsEqualTo("0,1,2,3");
			}
			finally
			{
				AggParallel.Sequential = wasSequential;
			}
		}

		[Test]
		[NotInParallel]
		public async Task ForAndForEachCoverEveryIndexWhenSequential()
		{
			var wasSequential = AggParallel.Sequential;
			try
			{
				AggParallel.Sequential = true;

				var visited = new List<int>();
				AggParallel.For(3, 9, i => visited.Add(i));
				await Assert.That(visited).IsEquivalentTo(new List<int> { 3, 4, 5, 6, 7, 8 });

				var seen = new List<string>();
				AggParallel.ForEach(new[] { "a", "b", "c" }, s => seen.Add(s));
				await Assert.That(seen).IsEquivalentTo(new List<string> { "a", "b", "c" });
			}
			finally
			{
				AggParallel.Sequential = wasSequential;
			}
		}

		/// <summary>
		/// Index of the first element that differs, or -1 when the two sequences match element for element.
		/// TUnit's <c>IsEquivalentTo</c> compares collections without regard to order, so any assertion that
		/// really means "same values in the same places" has to be spelled out - and reporting the index
		/// keeps the failure as readable as the collection assertion would have been.
		/// </summary>
		private static int FirstDifference<T>(IReadOnlyList<T> expected, IReadOnlyList<T> actual)
		{
			int shared = Math.Min(expected.Count, actual.Count);
			for (int i = 0; i < shared; i++)
			{
				if (!EqualityComparer<T>.Default.Equals(expected[i], actual[i]))
				{
					return i;
				}
			}

			return expected.Count == actual.Count ? -1 : shared;
		}

		private static ITraceable BuildBvh(BvhCreationOptions options, bool sequential)
		{
			var wasSequential = AggParallel.Sequential;
			try
			{
				AggParallel.Sequential = sequential;
				return BoundingVolumeHierarchy.CreateNewHierarchy(CreateTestTriangles(), options);
			}
			finally
			{
				AggParallel.Sequential = wasSequential;
			}
		}

		private static async Task AssertSameBvh(ITraceable expected, ITraceable actual)
		{
			var expectedShape = DescribeBvh(expected);
			var actualShape = DescribeBvh(actual);

			// Guards against a vacuous pass: if the builder ever returned a single flat node, or the test rays
			// all missed, the comparisons below would hold for the wrong reason.
			await Assert.That(expectedShape.Count).IsGreaterThan(100);
			await Assert.That(actualShape.Count).IsEqualTo(expectedShape.Count);

			// Walk order matters: IsEquivalentTo would compare the two walks as bags, so a tree holding the
			// same nodes in a different arrangement would pass.
			await Assert.That(FirstDifference(expectedShape, actualShape)).IsEqualTo(-1);

			int hitCount = 0;
			foreach (var ray in TestRays())
			{
				var expectedHit = expected.GetClosestIntersection(ray);
				var actualHit = actual.GetClosestIntersection(ray);

				await Assert.That(actualHit == null).IsEqualTo(expectedHit == null);
				if (expectedHit != null)
				{
					await Assert.That(actualHit.HitType).IsEqualTo(expectedHit.HitType);
					await Assert.That(actualHit.DistanceToHit).IsEqualTo(expectedHit.DistanceToHit);
					await Assert.That(actualHit.HitPosition).IsEqualTo(expectedHit.HitPosition);
					if (expectedHit.HitType != IntersectionType.None)
					{
						hitCount++;
					}
				}
			}

			await Assert.That(hitCount).IsGreaterThan(0);
		}

		/// <summary>
		/// Flattens the hierarchy into one string per node, in walk order: depth, node type, and bounds.
		/// Comparing the whole sequence in order asserts identical structure, not just identical totals - a
		/// differently balanced tree with the same node count would not match.
		/// </summary>
		/// <remarks>
		/// The bounds go through <c>Vector3.ToString</c>, which formats to about 1e-4, so this comparison is
		/// a structural one at display precision rather than a bit-exact numeric one. The exactness claim is
		/// carried by the ray-hit asserts in <see cref="AssertSameBvh"/>, which compare distances and
		/// positions as exact doubles.
		/// </remarks>
		private static List<string> DescribeBvh(ITraceable root)
		{
			var description = new List<string>();
			foreach (var item in new BvhIterator(root))
			{
				var bounds = item.Bvh.GetAxisAlignedBoundingBox();
				description.Add($"{item.Depth}|{item.Bvh.GetType().Name}|{bounds.MinXYZ}|{bounds.MaxXYZ}");
			}

			return description;
		}

		/// <summary>
		/// 800 deterministic triangles: enough to push the locally ordered clustering builder past its
		/// BatchSize * 4 threshold (128 * 4) so the batched parallel paths, not just the small-input ones,
		/// are the code under test. The x/y footprint of each triangle is fixed and only the corner z values
		/// vary, so <see cref="TestRays"/> can aim straight down z at a point that is guaranteed to be inside
		/// the triangle - random triangles in a 100 unit cube are missed by essentially every random ray, and
		/// an all-miss comparison proves nothing about the tree.
		/// </summary>
		private static List<ITraceable> CreateTestTriangles()
		{
			var material = new SolidMaterial(ColorF.Cyan, 0, 0, 0);
			var random = new DeterministicNumbers(seed: 12345);
			var triangles = new List<ITraceable>(TriangleCount);
			for (int i = 0; i < TriangleCount; i++)
			{
				var corner = TriangleCorner(random);
				triangles.Add(new TriangleShape(
					corner,
					corner + new Vector3(TriangleSize, 0, random.Next()),
					corner + new Vector3(0, TriangleSize, random.Next()),
					material));
			}

			return triangles;
		}

		/// <summary>
		/// One ray per sampled triangle, fired down -z through the triangle's centroid in x/y. Regenerating
		/// the corners from the same seed keeps the targets in step with <see cref="CreateTestTriangles"/>.
		/// </summary>
		private static IEnumerable<Ray> TestRays()
		{
			var random = new DeterministicNumbers(seed: 12345);
			for (int i = 0; i < TriangleCount; i++)
			{
				var corner = TriangleCorner(random);

				// Consume the same two values the triangle build consumes for its z offsets.
				random.Next();
				random.Next();

				if (i % 13 != 0)
				{
					continue;
				}

				var target = corner + new Vector3(TriangleSize / 3.0, TriangleSize / 3.0, 0);
				yield return new Ray(new Vector3(target.X, target.Y, 1000), -Vector3.UnitZ);
			}
		}

		private const int TriangleCount = 800;

		private const double TriangleSize = 3;

		private static Vector3 TriangleCorner(DeterministicNumbers random)
		{
			return new Vector3(random.Next() * 100, random.Next() * 100, random.Next() * 100);
		}

		private static byte[] TraceSmallScene(bool sequential)
		{
			var wasSequential = AggParallel.Sequential;
			try
			{
				AggParallel.Sequential = sequential;

				var camera = new SimpleCamera(32, 32, MathHelper.DegreesToRadians(40))
				{
					axisToWorld = Matrix4X4.Identity
				};
				// SimpleCamera looks down -Z, so the camera has to sit on the +Z side of the geometry.
				camera.Origin = new Vector3(0, 0, 8);

				var scene = new Scene(camera);
				scene.shapes.Add(new SphereShape(new Vector3(0, 0, 0), 2, new SolidMaterial(ColorF.Red, 0, 0, 0)));
				scene.shapes.Add(new BoxShape(new Vector3(-3, -3, -3), new Vector3(3, -1.5, -1), new SolidMaterial(ColorF.Blue, 0, 0, 0)));
				scene.lights.Add(new PointLight(new Vector3(50, 50, 50), new ColorF(0.8, 0.8, 0.8)));

				var tracer = new RayTracerEngine
				{
					AntiAliasing = AntiAliasing.None,
					MultiThreaded = true,
				};

				var viewport = new RectangleInt(0, 0, 32, 32);
				tracer.RayTraceScene(viewport, scene);

				var image = new ImageBuffer(32, 32, 32, new BlenderBGRA());
				tracer.CopyColorBufferToImage(image, viewport);

				return image.GetBuffer().ToArray();
			}
			finally
			{
				AggParallel.Sequential = wasSequential;
			}
		}

		/// <summary>
		/// A fixed linear congruential generator, so the same geometry and rays are produced on every run and
		/// on every platform. System.Random's sequence is not contractually stable across runtimes.
		/// </summary>
		private class DeterministicNumbers
		{
			private ulong state;

			public DeterministicNumbers(ulong seed)
			{
				state = seed;
			}

			/// <summary>Returns the next value in [0, 1).</summary>
			public double Next()
			{
				state = (state * 6364136223846793005UL) + 1442695040888963407UL;
				return (state >> 11) / (double)(1UL << 53);
			}
		}
	}
}
