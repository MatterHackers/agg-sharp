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
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The wire, non manifold and overhang caches hold pure cpu data, so unlike the texture and
	/// triangle caches they do not have to be keyed by GL context. What they do share with those
	/// caches is the threads that reach them: RenderHelper.Render runs on the ui thread and on the
	/// D3D thumbnail workers at the same time, over the same <see cref="Mesh"/>, and all three of
	/// these caches used to live in Mesh.PropertyBag - one plain Dictionary that both threads did
	/// Remove/Add on with no lock, which corrupts its bucket chains. These tests hammer the caches
	/// from two threads and pin the instance reuse contract that the render loop depends on.
	/// </summary>
	[NotInParallel]
	public class MeshCacheConcurrencyTests
	{
		// Two vertices per edge, three edges per face - what MeshWirePlugin emits with no angle filter.
		private const int WireVerticesPerFace = 6;

		/// <summary>
		/// Runs two threads over the same mesh and collects whatever they throw. The threads each get
		/// a GL context of their own, which is how the ui thread and a thumbnail worker meet.
		/// </summary>
		private static List<Exception> RunOnTwoThreads(Action<GL, bool> fetchRepeatedly)
		{
			var failures = new List<Exception>();

			void RunOne(GL gl, bool alsoChangeTheMesh)
			{
				try
				{
					fetchRepeatedly(gl, alsoChangeTheMesh);
				}
				catch (Exception e)
				{
					lock (failures)
					{
						failures.Add(e);
					}
				}
			}

			var glA = new GL(new RecordingGpuContext(idBase: 40000));
			var glB = new GL(new RecordingGpuContext(idBase: 41000));

			var threadA = new Thread(() => RunOne(glA, true));
			var threadB = new Thread(() => RunOne(glB, false));

			threadA.Start();
			threadB.Start();
			threadA.Join();
			threadB.Join();

			return failures;
		}

		private static string Describe(List<Exception> failures)
		{
			return string.Join(", ", failures.Select(e => e.GetType().Name + ": " + e.Message));
		}

		[Test]
		public async Task ConcurrentMeshWirePluginFetchesStayConsistent()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			int expectedEdgeVertices = mesh.Faces.Count * WireVerticesPerFace;

			// Both threads edit the mesh: the ui thread moves the part while a thumbnail worker draws
			// its outline, so both of them are the one that finds the cache stale and republishes.
			var failures = RunOnTwoThreads((gl, alsoChangeTheMesh) =>
			{
				for (int i = 0; i < 200; i++)
				{
					// No angle filter, so the edge list is built synchronously and must be complete
					// the moment the plugin is handed out.
					var plugin = MeshWirePlugin.Get(mesh, Color.White);

					if (plugin.EdgeLines == null || plugin.EdgeLines.Count != expectedEdgeVertices)
					{
						throw new Exception("a wire plugin was handed out before its edge list was built: "
							+ (plugin.EdgeLines?.Count.ToString() ?? "null"));
					}

					if ((i % 5) == 0)
					{
						mesh.MarkAsChanged();
					}
				}
			});

			await Assert.That(failures.Count)
				.IsEqualTo(0)
				.Because("two threads fetching the wire lines for the same changing mesh must not corrupt the cache: "
					+ Describe(failures));
		}

		[Test]
		public async Task MeshWirePluginReusesInstanceUntilTheMeshChanges()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var first = MeshWirePlugin.Get(mesh, Color.White);
			var edgeCountBeforeChange = first.EdgeLines.Count;

			await Assert.That(ReferenceEquals(first, MeshWirePlugin.Get(mesh, Color.White)))
				.IsTrue()
				.Because("an unchanged mesh must not have its edge list rebuilt on every frame");

			mesh.MarkAsChanged();

			var second = MeshWirePlugin.Get(mesh, Color.White);

			await Assert.That(ReferenceEquals(first, second))
				.IsFalse()
				.Because("a changed mesh has to produce a new plugin");

			// A rebuild replaces the cache entry rather than mutating the instance in place, because
			// the other thread may be part way through drawing from the instance it already fetched.
			await Assert.That(first.EdgeLines.Count)
				.IsEqualTo(edgeCountBeforeChange)
				.Because("the replaced plugin must still hold the edge list its holder is drawing from");
			await Assert.That(second.EdgeLines.Count).IsEqualTo(edgeCountBeforeChange);
		}

		[Test]
		public async Task MeshWirePluginRebuildsWhenTheAngleFilterChanges()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			// RenderHelper asks for a filtered list for Outlines and an unfiltered one for Wireframe,
			// so the angle is part of the cache identity even though the mesh has not changed.
			var unfiltered = MeshWirePlugin.Get(mesh, Color.White);
			var filtered = MeshWirePlugin.Get(mesh, Color.White, MathHelper.Tau / 8);

			await Assert.That(ReferenceEquals(unfiltered, filtered))
				.IsFalse()
				.Because("a different non planar angle needs a differently filtered edge list");

			await Assert.That(ReferenceEquals(filtered, MeshWirePlugin.Get(mesh, Color.White, MathHelper.Tau / 8)))
				.IsTrue()
				.Because("asking again for the same angle must reuse the cached plugin");
		}

		[Test]
		public async Task MeshWirePluginKeepsBothAngleListsCachedAtOnce()
		{
			// The regression test for making the angle part of the cache key rather than only part of the
			// freshness check. With one entry per mesh the two angles evicted each other, so every fetch
			// found the entry stale and re-walked the whole mesh.
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var wireframe = MeshWirePlugin.Get(mesh, Color.White);
			var outlines = MeshWirePlugin.Get(mesh, Color.White, MathHelper.Tau / 8);

			for (int i = 0; i < 5; i++)
			{
				await Assert.That(ReferenceEquals(wireframe, MeshWirePlugin.Get(mesh, Color.White)))
					.IsTrue()
					.Because("a ui viewport drawing this mesh in wireframe while a thumbnail worker draws its "
						+ "outlines must keep its unfiltered list cached, not have the outline fetch evict it");

				await Assert.That(ReferenceEquals(outlines, MeshWirePlugin.Get(mesh, Color.White, MathHelper.Tau / 8)))
					.IsTrue()
					.Because("and the thumbnail worker's filtered list has to survive the viewport's fetch too - "
						+ "otherwise both threads re-walk the unchanged mesh on every single frame");
			}
		}

		[Test]
		public async Task MeshWirePluginSwapsInTheFilteredListWhenTheBackgroundPassFinishes()
		{
			// A cube: every edge is a 90 degree crease, so the whole mesh survives the filter.
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			// The meshChanged callback is the plugin's own completion signal - it fires on the background
			// thread immediately after the filtered list is swapped in - so the test waits on the event
			// rather than on a timer.
			var filteredPassFinished = new TaskCompletionSource<bool>(TaskCreationOptions.RunContinuationsAsynchronously);

			var plugin = MeshWirePlugin.Get(
				mesh,
				Color.White,
				MathHelper.Tau / 8,
				() => filteredPassFinished.TrySetResult(true));

			// The filtered path publishes the plugin with an empty list and fills it in later, so this is
			// what a caller drawing on the very next frame can legitimately see.
			await filteredPassFinished.Task;

			// Reading EdgeLines from this thread after the background thread stored it is exactly the
			// hand off the volatile field exists for. This exercises that publication rather than proving
			// it: a plain field would almost always be read correctly here too, since there is no way to
			// force the reordering it guards against.
			var filteredEdgeLines = plugin.EdgeLines;

			await Assert.That(filteredEdgeLines).IsNotNull();
			await Assert.That(filteredEdgeLines.Count)
				.IsGreaterThan(0)
				.Because("the background pass has to have swapped its finished list in, not left the empty one");
			await Assert.That(filteredEdgeLines.Count)
				.IsLessThan(mesh.Faces.Count * WireVerticesPerFace)
				.Because("the filter has to have dropped the coplanar edges - the diagonal splitting each "
					+ "square face into two triangles is a zero degree crease");

			// And the swapped in list has to be reachable through the cache, not just through the
			// reference the first caller happened to keep.
			await Assert.That(ReferenceEquals(MeshWirePlugin.Get(mesh, Color.White, MathHelper.Tau / 8).EdgeLines, filteredEdgeLines))
				.IsTrue()
				.Because("the cached plugin is the one whose list was swapped, so a later frame draws the filtered edges");
		}

		[Test]
		public async Task ConcurrentMeshNonManifoldPluginFetchesStayConsistent()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var failures = RunOnTwoThreads((gl, alsoChangeTheMesh) =>
			{
				for (int i = 0; i < 200; i++)
				{
					var plugin = MeshNonManifoldPlugin.Get(mesh, Color.White);

					// The unfiltered list is built before the plugin is published; the background pass
					// only ever swaps in another fully built list, so this is never empty.
					if (plugin.EdgeLines == null || plugin.EdgeLines.Count == 0)
					{
						throw new Exception("a non manifold plugin was handed out before its edge list was built");
					}

					if ((i % 5) == 0)
					{
						mesh.MarkAsChanged();
					}
				}
			});

			await Assert.That(failures.Count)
				.IsEqualTo(0)
				.Because("two threads fetching the non manifold lines for the same changing mesh must not corrupt the cache: "
					+ Describe(failures));
		}

		[Test]
		public async Task MeshNonManifoldPluginReusesInstanceUntilTheMeshChanges()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var first = MeshNonManifoldPlugin.Get(mesh, Color.White);

			await Assert.That(ReferenceEquals(first, MeshNonManifoldPlugin.Get(mesh, Color.White)))
				.IsTrue()
				.Because("an unchanged mesh must not have its edge list rebuilt on every frame");

			mesh.MarkAsChanged();

			var second = MeshNonManifoldPlugin.Get(mesh, Color.White);

			await Assert.That(ReferenceEquals(first, second))
				.IsFalse()
				.Because("a changed mesh has to produce a new plugin");
			// The count is not pinned here the way it is for the wire plugin: this plugin's background
			// pass swaps in a differently filtered list. What must hold is that the replaced instance
			// still has a complete list for whoever is drawing from it.
			await Assert.That(first.EdgeLines.Count)
				.IsGreaterThan(0)
				.Because("the replaced plugin must still hold an edge list for its holder to draw from");
			await Assert.That(second.EdgeLines.Count)
				.IsGreaterThan(0);
		}

		[Test]
		public async Task ConcurrentOverhangEnsureUpdatedStaysConsistent()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var failures = RunOnTwoThreads((gl, alsoChangeTheMesh) =>
			{
				for (int i = 0; i < 200; i++)
				{
					OverhangRender.EnsureUpdated(gl, mesh, Matrix4X4.Identity);

					if ((i % 5) == 0)
					{
						mesh.MarkAsChanged();
					}
				}
			});

			await Assert.That(failures.Count)
				.IsEqualTo(0)
				.Because("two contexts rendering overhangs for the same mesh must not corrupt the cache: "
					+ Describe(failures));
		}

		[Test]
		public async Task OverhangEnsureUpdatedStopsMarkingTheMeshOnceTheNormalIsRecorded()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			var gl = new GL(new RecordingGpuContext(idBase: 42000));

			// The first pass records face 0's world z and marks the mesh so the colored render data is
			// rebuilt. Every later pass with the same transform has to be a no-op, otherwise the
			// overhang view would re-tesselate the mesh on every single frame.
			OverhangRender.EnsureUpdated(gl, mesh, Matrix4X4.Identity);

			var changedCountAfterFirstPass = mesh.ChangedCount;

			OverhangRender.EnsureUpdated(gl, mesh, Matrix4X4.Identity);
			OverhangRender.EnsureUpdated(gl, mesh, Matrix4X4.Identity);

			await Assert.That(mesh.ChangedCount)
				.IsEqualTo(changedCountAfterFirstPass)
				.Because("the recorded normal has to be found again rather than re-recorded every pass");
		}

		[Test]
		public async Task ConcurrentWireAndOverhangFetchesStayConsistent()
		{
			// This is the shape of the real failure: the ui thread renders overhangs while a thumbnail
			// worker renders outlines, so two different caches for one mesh are written at once. When
			// they shared Mesh.PropertyBag that was two unguarded writers on one Dictionary.
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);
			int expectedEdgeVertices = mesh.Faces.Count * WireVerticesPerFace;

			var failures = RunOnTwoThreads((gl, renderOverhangs) =>
			{
				for (int i = 0; i < 200; i++)
				{
					if (renderOverhangs)
					{
						OverhangRender.EnsureUpdated(gl, mesh, Matrix4X4.Identity);
					}
					else
					{
						var plugin = MeshWirePlugin.Get(mesh, Color.White);
						if (plugin.EdgeLines == null || plugin.EdgeLines.Count != expectedEdgeVertices)
						{
							throw new Exception("a wire plugin was handed out before its edge list was built: "
								+ (plugin.EdgeLines?.Count.ToString() ?? "null"));
						}
					}

					if ((i % 5) == 0)
					{
						mesh.MarkAsChanged();
					}
				}
			});

			await Assert.That(failures.Count)
				.IsEqualTo(0)
				.Because("the overhang and wire caches for one mesh must not corrupt each other: "
					+ Describe(failures));
		}
	}
}
