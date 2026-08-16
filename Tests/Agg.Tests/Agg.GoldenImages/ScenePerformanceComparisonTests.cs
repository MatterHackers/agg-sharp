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
using System.Diagnostics;
using System.Threading.Tasks;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The port plan's Phase 3 exit metric: frame time on the transparent scene, WebGPU against the
	/// classic D3D11 path, on the same machine in the same process.
	/// </summary>
	/// <remarks>
	/// <b>Explicit on purpose.</b> This measures rather than asserts, and a timing threshold in CI would
	/// either be so loose it proves nothing or so tight it fails on a busy machine. Run it by name when
	/// the number is wanted:
	/// <code>Agg.Tests.exe --treenode-filter "/*/*/ScenePerformanceComparisonTests/*"</code>
	/// <para>
	/// Both backends are measured the same way, per frame: draw the scene, end the frame, then read the
	/// colour target back. The readback is what makes the number mean anything - without it the CPU would
	/// be timed racing ahead of a GPU that has not finished, on either backend. It also puts a fixed
	/// per-frame cost (a 512x384 copy plus a map) into both totals, so the *difference* between the two is
	/// a fair comparison while the absolute numbers are pessimistic.
	/// </para>
	/// </remarks>
	[NotInParallel]
	[Explicit]
	public class ScenePerformanceComparisonTests
	{
		private const int WarmupFrames = 5;

		private const int MeasuredFrames = 60;

		/// <summary>Alpha of the measured scene: 120 peels, 255 does not.</summary>
		private static int sceneAlpha = 120;

		[Test]
		public async Task TransparentSceneFrameTime()
		{
			double classicMs = MeasureClassic(readBackEveryFrame: true);
			double webGpuMs = await MeasureWebGpuAsync(readBackEveryFrame: true);

			Console.WriteLine(
				$"Scene.Transparent frame time over {MeasuredFrames} frames (readback every frame): "
				+ $"classic D3D11 {classicMs:0.00} ms/frame, WebGPU {webGpuMs:0.00} ms/frame, "
				+ $"ratio {webGpuMs / classicMs:0.00}x.");

			// The number an interactive frame actually pays: draw and submit every frame, read back once at
			// the end so the GPU is still forced to finish everything but the per-frame map/copy is out of
			// the way. A window presents; it does not read its own backbuffer back.
			double classicRenderMs = MeasureClassic(readBackEveryFrame: false);
			double webGpuRenderMs = await MeasureWebGpuAsync(readBackEveryFrame: false);

			Console.WriteLine(
				$"Scene.Transparent frame time over {MeasuredFrames} frames (single readback at the end): "
				+ $"classic D3D11 {classicRenderMs:0.00} ms/frame, WebGPU {webGpuRenderMs:0.00} ms/frame, "
				+ $"ratio {webGpuRenderMs / classicRenderMs:0.00}x.");

			// The same scene opaque, which runs every pass except the peel. Whatever ratio survives here is
			// general per-draw overhead rather than anything the transparency reformulation costs.
			sceneAlpha = 255;
			try
			{
				double classicOpaqueMs = MeasureClassic(readBackEveryFrame: false);
				double webGpuOpaqueMs = await MeasureWebGpuAsync(readBackEveryFrame: false);

				Console.WriteLine(
					$"Scene.Opaque frame time over {MeasuredFrames} frames (single readback at the end): "
					+ $"classic D3D11 {classicOpaqueMs:0.00} ms/frame, WebGPU {webGpuOpaqueMs:0.00} ms/frame, "
					+ $"ratio {webGpuOpaqueMs / classicOpaqueMs:0.00}x.");
			}
			finally
			{
				sceneAlpha = 120;
			}
		}

		private static double MeasureClassic(bool readBackEveryFrame)
		{
			using var capture = D3D11OffscreenCapture.Create();
			var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);

			for (int frame = 0; frame < WarmupFrames; frame++)
			{
				RenderClassicFrame(capture, world, true);
			}

			var stopwatch = Stopwatch.StartNew();
			for (int frame = 0; frame < MeasuredFrames; frame++)
			{
				RenderClassicFrame(capture, world, readBackEveryFrame);
			}

			if (!readBackEveryFrame)
			{
				capture.Capture();
			}

			stopwatch.Stop();
			return stopwatch.Elapsed.TotalMilliseconds / MeasuredFrames;
		}

		private static void RenderClassicFrame(D3D11OffscreenCapture capture, WorldView world, bool readBack)
		{
			capture.ClearTo(Golden3DScenes.Background);
			capture.RenderScene(
				world,
				new LightingData(),
				() => Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, sceneAlpha));

			if (readBack)
			{
				capture.Capture();
			}
		}

		private static async Task<double> MeasureWebGpuAsync(bool readBackEveryFrame)
		{
			using var capture = WebGpuOffscreenCapture.Create();
			var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);

			for (int frame = 0; frame < WarmupFrames; frame++)
			{
				await RenderWebGpuFrameAsync(capture, world, true);
			}

			var stopwatch = Stopwatch.StartNew();
			for (int frame = 0; frame < MeasuredFrames; frame++)
			{
				await RenderWebGpuFrameAsync(capture, world, readBackEveryFrame);
			}

			if (!readBackEveryFrame)
			{
				await capture.CaptureAsync();
			}

			stopwatch.Stop();
			return stopwatch.Elapsed.TotalMilliseconds / MeasuredFrames;
		}

		private static async Task RenderWebGpuFrameAsync(WebGpuOffscreenCapture capture, WorldView world, bool readBack)
		{
			capture.ClearTo(Golden3DScenes.Background);
			capture.RenderScene(
				world,
				new LightingData(),
				() => Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, sceneAlpha));

			if (readBack)
			{
				await capture.CaptureAsync();
			}
		}
	}
}
