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
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.PolygonMesh;
using MatterHackers.RenderGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class NativeSceneRenderPlannerTests
	{
		/// <summary>
		/// The opaque pass draws in submission order, with repeat draws of one mesh pulled up beside its
		/// first draw. Where two meshes meet at exactly equal depth the later draw wins the pixel, so an
		/// order that changes from run to run is a picture that changes from run to run: sorting by object
		/// identity hash did exactly that, and flickered one pixel of Scene.Opaque on CI's rasterizer.
		/// </summary>
		[Test]
		public async Task OpaqueCommandsKeepSubmissionOrderGroupedByMesh()
		{
			var shared = new Mesh();
			var meshes = Enumerable.Range(0, 16).Select(_ => new Mesh()).ToArray();

			var commands = meshes
				.Select(mesh => new MeshRenderCommand { Mesh = mesh, Color = Color.Red })
				.ToList();

			// The shared mesh's two draws are submitted apart and must come out adjacent, first one first.
			var firstShared = new MeshRenderCommand { Mesh = shared, Color = Color.Green };
			var secondShared = new MeshRenderCommand { Mesh = shared, Color = Color.Blue };
			commands.Insert(3, firstShared);
			commands.Add(secondShared);

			var expected = commands.Take(4)
				.Append(secondShared)
				.Concat(commands.Skip(4).Take(commands.Count - 5))
				.ToList();

			var plan = new NativeSceneRenderPlanner().Build(commands);

			await Assert.That(plan.OpaqueCommands.Count).IsEqualTo(expected.Count);
			for (int i = 0; i < expected.Count; i++)
			{
				await Assert.That(plan.OpaqueCommands[i]).IsSameReferenceAs(expected[i]);
			}
		}
	}
}
