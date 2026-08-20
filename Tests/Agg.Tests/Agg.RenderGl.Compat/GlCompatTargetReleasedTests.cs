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

using System;
using System.Threading.Tasks;
using MatterHackers.RenderCore;
using MatterHackers.RenderCore.Testing;
using MatterHackers.RenderGl.Compat;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// What happens when the frame's destination goes away while widgets are still drawing into it.
	/// <para>
	/// A GL host has one global framebuffer that outlives any single paint; the compat layer's target is
	/// handed to it per frame and can be taken back mid-paint - the swapchain texture is released by a
	/// present, freed by a resize, or the whole device is rebuilt after a loss. The widget tree has no
	/// way to hear about any of that and keeps drawing, so the layer has to absorb it: draws issued
	/// after the target is released are dropped, and the next frame - which sets a target again - draws
	/// normally.
	/// </para>
	/// </summary>
	public class GlCompatTargetReleasedTests
	{
		[Test]
		public async Task DrawingAfterTheTargetIsReleasedIsDroppedRatherThanThrowing()
		{
			var harness = GlCompatTestHarness.Create();

			// The frame ended: WebGpuControl.Present forgets the target so the next BeginFrame acquires
			// a new swapchain texture. A widget still in the paint draws anyway.
			harness.Context.SetRenderTarget(null, null);

			harness.DrawTriangle();

			await Assert.That(harness.Context.Passes.IsPassOpen).IsFalse();
			await Assert.That(harness.Context.Passes.PassOpenCount).IsEqualTo(0);
			await Assert.That(harness.Device.CommandsOf<BeginRenderPassCommand>().Count).IsEqualTo(0);
		}

		[Test]
		public async Task DisplayListPlaybackAfterTheTargetIsReleasedIsDropped()
		{
			// The reported crash came in through a display list: the tumble cube HUD replays its cached
			// arc geometry with GL.CallList, which submits without ever asking whether a target exists.
			var harness = GlCompatTestHarness.Create();

			int list = harness.Context.GenLists(1);
			harness.Context.NewList(list, null);
			harness.DrawTriangle();
			harness.Context.EndList();

			harness.Context.SetRenderTarget(null, null);
			harness.Device.ClearRecording();

			harness.Context.CallList(list);

			await Assert.That(harness.Device.CommandsOf<BeginRenderPassCommand>().Count).IsEqualTo(0);
		}

		[Test]
		public async Task DrawingIntoADisposedContextIsDroppedRatherThanThrowing()
		{
			// A device loss disposes the compat context and builds a new one, but the Graphics2D the
			// current paint is holding still points at the old one.
			var harness = GlCompatTestHarness.Create();

			harness.Context.Dispose();

			harness.DrawTriangle();

			await Assert.That(harness.Context.Passes.PassOpenCount).IsEqualTo(0);
		}

		[Test]
		public async Task SettingATargetAgainResumesDrawing()
		{
			// Dropping draws must not be sticky: the frame after the interruption has to render.
			var harness = GlCompatTestHarness.Create();

			harness.Context.SetRenderTarget(null, null);
			harness.DrawTriangle();

			harness.Context.SetRenderTarget(harness.Target, null);
			harness.DrawTriangle();
			harness.Context.FlushPass();

			await Assert.That(harness.Context.Passes.PassOpenCount).IsEqualTo(1);
			await Assert.That(harness.Device.CommandsOf<BeginRenderPassCommand>().Count).IsEqualTo(1);
		}

		[Test]
		public async Task DrawingBeforeAnyTargetHasEverBeenSetStillThrows()
		{
			// The diagnostic that catches a host which never wired itself up has to survive: a context
			// that has never had a target is a programming error, not an interrupted frame.
			var device = new RecordingRenderDevice();
			var context = new GlCompatContext(device);

			await Assert.That(() =>
				{
					context.Begin(MatterHackers.RenderGl.OpenGl.BeginMode.Triangles);
					context.Vertex2(0, 0);
					context.Vertex2(1, 0);
					context.Vertex2(1, 1);
					context.End();
				})
				.Throws<InvalidOperationException>();
		}
	}
}
