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
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Display list record and replay. The invariant these exist to protect is the one the port plan
	/// calls out explicitly: a list bakes <em>geometry only</em>, and everything else - pipeline, bind
	/// group, matrices - is resolved from live state at replay time, because that is what
	/// <c>glCallList</c> does today.
	/// </summary>
	public class GlCompatDisplayListTests
	{
		[Test]
		public async Task TwoReplaysUnderDifferentBlendStatesShareGeometryButNotPipelines()
		{
			var harness = GlCompatTestHarness.Create();

			int list = harness.Context.GenLists(1);
			harness.Context.NewList(list, null);
			harness.DrawTriangle();
			harness.Context.EndList();

			// Recording draws nothing and creates nothing: the batch went into the list.
			await Assert.That(harness.Device.CommandsOf<DrawCommand>().Count).IsEqualTo(0);
			await Assert.That(harness.VertexBufferCreations().Count).IsEqualTo(0);

			harness.Context.CallList(list);

			harness.Context.Enable((int)EnableCap.Blend);
			harness.Context.BlendFunc((int)BlendingFactorSrc.SrcAlpha, (int)BlendingFactorDest.OneMinusSrcAlpha);
			harness.Context.CallList(list);
			harness.Context.FlushPass();

			// Baked once, on the first replay, and reused by the second.
			await Assert.That(harness.VertexBufferCreations().Count).IsEqualTo(1);

			var vertexBuffers = harness.BoundVertexBuffers();
			await Assert.That(vertexBuffers.Count).IsEqualTo(2);
			await Assert.That(ReferenceEquals(vertexBuffers[0], vertexBuffers[1])).IsTrue();

			// The pipeline is not baked, so the second replay picks up the live blend state.
			var pipelines = harness.BoundPipelines();
			await Assert.That(ReferenceEquals(pipelines[0], pipelines[1])).IsFalse();
			await Assert.That(pipelines[0].Descriptor.ColorTargets[0].BlendEnabled).IsFalse();
			await Assert.That(pipelines[1].Descriptor.ColorTargets[0].BlendEnabled).IsTrue();
		}

		[Test]
		public async Task AReplayPicksUpTheLiveMatrixStack()
		{
			var harness = GlCompatTestHarness.Create();

			int list = harness.Context.GenLists(1);
			harness.Context.NewList(list, null);
			harness.DrawTriangle();
			harness.Context.EndList();

			// This is exactly how the 2D glyph cache draws: translate, call the cached list, translate
			// back. If the list baked its transform, the second placement would land on the first.
			harness.Context.Translate(20, 30, 0);
			harness.Context.CallList(list);
			harness.Context.Translate(-20, -30, 0);
			harness.Context.CallList(list);

			// Submit rather than FlushPass: per-draw uniform blocks are staged and pushed in one write
			// just before the device submit, so nothing is readable until then.
			harness.Context.Submit();

			await Assert.That(harness.UniformModelView(0).Row3.X).IsEqualTo(20.0).Within(1e-5);
			await Assert.That(harness.UniformModelView(1).Row3.X).IsEqualTo(0.0).Within(1e-5);
		}

		[Test]
		public async Task FlatShadingIsBakedIntoTheGeometrySoEachShadingModeGetsItsOwnBuffer()
		{
			// GL's provoking vertex rule is applied while interleaving, so it does reach the vertex
			// bytes even though nothing else about the pipeline does. Keyed baking is how the two modes
			// stay correct without re-baking on every replay.
			var harness = GlCompatTestHarness.Create();

			int list = harness.Context.GenLists(1);
			harness.Context.NewList(list, null);
			harness.DrawTriangle();
			harness.Context.EndList();

			harness.Context.CallList(list);
			harness.Context.ShadeModel(ShadingModel.Flat);
			harness.Context.CallList(list);
			harness.Context.ShadeModel(ShadingModel.Smooth);
			harness.Context.CallList(list);
			harness.Context.FlushPass();

			await Assert.That(harness.VertexBufferCreations().Count).IsEqualTo(2);

			var vertexBuffers = harness.BoundVertexBuffers();
			await Assert.That(ReferenceEquals(vertexBuffers[0], vertexBuffers[1])).IsFalse();
			await Assert.That(ReferenceEquals(vertexBuffers[0], vertexBuffers[2])).IsTrue();
		}

		[Test]
		public async Task RecordingIntoAListAgainReplacesWhatItHeld()
		{
			var harness = GlCompatTestHarness.Create();

			int list = harness.Context.GenLists(1);
			harness.Context.NewList(list, null);
			harness.DrawTriangle();
			harness.DrawTriangle();
			harness.Context.EndList();

			harness.Context.NewList(list, null);
			harness.DrawTriangle();
			harness.Context.EndList();

			harness.Context.CallList(list);
			harness.Context.FlushPass();

			await Assert.That(harness.Device.CommandsOf<DrawCommand>().Count).IsEqualTo(1);
		}

		[Test]
		public async Task DeletedListsReplayNothing()
		{
			var harness = GlCompatTestHarness.Create();

			int list = harness.Context.GenLists(1);
			harness.Context.NewList(list, null);
			harness.DrawTriangle();
			harness.Context.EndList();
			harness.Context.DeleteLists(list, 1);

			harness.Context.CallList(list);

			await Assert.That(harness.Device.CommandsOf<DrawCommand>().Count).IsEqualTo(0);
			await Assert.That(harness.Context.Passes.PassOpenCount).IsEqualTo(0);
		}
	}
}
