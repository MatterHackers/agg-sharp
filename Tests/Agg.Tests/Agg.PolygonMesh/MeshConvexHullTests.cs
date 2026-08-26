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

using System.Diagnostics;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.PolygonMesh.UnitTests
{
	public class MeshConvexHullTests
	{
		[Test]
		public async Task SyncHullIsComputedAndCached()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var hull = mesh.GetConvexHull(false);

			await Assert.That(hull).IsNotNull();
			await Assert.That(hull.Faces.Count).IsGreaterThan(0);

			// the second call comes back from the cache on the mesh, not from a second hull build
			await Assert.That(mesh.GetConvexHull(false)).IsSameReferenceAs(hull);
		}

		[Test]
		public async Task SyncHullDoesNotWaitOnAnInFlightHull()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			// stand in for another thread that is part way through building the hull. The old
			// implementation polled with Thread.Sleep(1) for up to a second waiting for this to
			// clear; the sync contract is that we build our own hull instead of parking.
			var neverCompletes = new TaskCompletionSource<Mesh>();
			mesh.PropertyBag[MeshConvexHull.CreatingConvexHullMesh] = neverCompletes.Task;

			var timer = Stopwatch.StartNew();
			var hull = mesh.GetConvexHull(false);
			timer.Stop();

			await Assert.That(hull).IsNotNull();
			await Assert.That(timer.ElapsedMilliseconds).IsLessThan(500);

			neverCompletes.SetResult(null);
		}

		[Test]
		public async Task ChangingTheMeshDropsTheCachedHull()
		{
			var mesh = PlatonicSolids.CreateCube(10, 10, 10);

			var hull = mesh.GetConvexHull(false);
			await Assert.That(hull).IsNotNull();

			mesh.Vertices.Add(new Vector3Float(20, 20, 20));
			mesh.MarkAsChanged();

			await Assert.That(mesh.PropertyBag.ContainsKey(MeshConvexHull.ConvexHullMesh)).IsFalse();
		}
	}
}
