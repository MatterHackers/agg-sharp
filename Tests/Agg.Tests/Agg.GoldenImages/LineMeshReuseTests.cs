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

using System.Threading.Tasks;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Scene;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The 3D line helpers draw the gizmos, and they draw them every frame. This is the test that they do
	/// not mint a GPU vertex buffer every frame while doing it: the scene renderer retains buffers per mesh
	/// identity, so a fresh <c>Mesh</c> per line per frame was a hundred buffer creations a frame.
	/// </summary>
	[NotInParallel]
	public class LineMeshReuseTests
	{
		private static void DrawLines(WebGpuOffscreenCapture capture, WorldView world)
		{
			var frustum = world.GetClippingFrustum();

			// A spread of the shapes the gizmos actually draw: plain lines, an arrow headed line, and the
			// helpers that fan out into many lines of their own.
			for (int index = 0; index < 8; index++)
			{
				world.Render3DLineNoPrep(
					capture.Gl,
					frustum,
					new Vector3(-40 + (index * 10), -30, 0),
					new Vector3(-40 + (index * 10), 30, 20),
					Color.Blue,
					width: 1);
			}

			world.Render3DLine(capture.Gl, frustum, new Vector3(-30, 0, -40), new Vector3(-30, 0, 74), Color.Red, doDepthTest: true, width: 2, endArrow: true);
			world.RenderAabb(capture.Gl, new AxisAlignedBoundingBox(new Vector3(-65, -35, -35), new Vector3(5, 35, 35)), Matrix4X4.Identity, Color.Green, lineWidth: 1);
			world.RenderRing(capture.Gl, Matrix4X4.Identity, new Vector3(-30, 0, 0), 96, 40, Color.Yellow, lineWidth: 1.4);
		}

		[Test]
		public async Task UnchangedLinesReuseTheirVertexBuffers()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			capture.ClearTo(Golden3DScenes.Background);

			var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);
			var lighting = new LightingData();
			var renderer = (WebGpuSceneRenderer)capture.SceneRenderer;

			capture.RenderScene(world, lighting, () => DrawLines(capture, world));
			int afterFirstFrame = renderer.VertexBufferCreateCount;

			// The first frame has to build them, or the second frame proves nothing.
			await Assert.That(afterFirstFrame).IsGreaterThan(0);

			// Three more identical frames. Same camera, same lines, so the same meshes - and therefore the
			// same buffers, with nothing new created.
			for (int frame = 0; frame < 3; frame++)
			{
				capture.RenderScene(world, lighting, () => DrawLines(capture, world));
			}

			await Assert.That(renderer.VertexBufferCreateCount).IsEqualTo(afterFirstFrame)
				.Because("redrawing the same 3D lines must not mint new vertex buffers");

			await Assert.That(capture.Device.LastUncapturedError).IsNull();
		}

		[Test]
		public async Task MovingTheCameraRebuildsLineMeshes()
		{
			using var capture = WebGpuOffscreenCapture.Create();
			capture.ClearTo(Golden3DScenes.Background);

			var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);
			var lighting = new LightingData();
			var renderer = (WebGpuSceneRenderer)capture.SceneRenderer;

			capture.RenderScene(world, lighting, () => DrawLines(capture, world));
			int afterFirstFrame = renderer.VertexBufferCreateCount;

			// The cache key carries the per-pixel world scale at each end, so a camera move must miss it:
			// the line geometry is measured in screen pixels and genuinely changed.
			world.Rotate(Quaternion.FromEulerAngles(new Vector3(0, 0, 0.3)));
			capture.RenderScene(world, lighting, () => DrawLines(capture, world));

			await Assert.That(renderer.VertexBufferCreateCount).IsGreaterThan(afterFirstFrame)
				.Because("a moved camera changes the lines' world-space geometry, so the cache must miss");
		}
	}
}
