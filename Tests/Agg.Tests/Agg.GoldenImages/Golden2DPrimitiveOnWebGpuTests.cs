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
using MatterHackers.Agg.Font;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The cross-backend half of the 2D suite: the scenes in <see cref="Golden2DScenes"/> rendered through
	/// <see cref="GlCompatContext"/> on <c>WebGpuRenderDevice</c> and compared against the <b>same</b> PNGs
	/// the classic D3D11 path captured. This is the Phase 2 exit measurement.
	/// </summary>
	/// <remarks>
	/// Tolerance is zero, per O4: the stated goal is 1:1 pixel identity, and a suite that starts permissive
	/// can never be tightened because nobody knows afterwards which differences were real. Where a
	/// difference turns out to be a genuine cross-API rasterization artifact rather than a port bug, the
	/// tolerance is raised <i>at the individual call site</i> with the evidence written next to it.
	/// </remarks>
	[NotInParallel]
	public class Golden2DPrimitiveOnWebGpuTests
	{
		private static async Task Check(string goldenName, Action<Graphics2DGpu, GL> draw)
		{
			bool wasLcdEnabled = LcdRenderSettings.Enabled;
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				LcdRenderSettings.Enabled = false;
				TypeFacePrinter.SnapBaselinesToWholePixels = true;

				using var capture = WebGpuOffscreenCapture.Create();
				var graphics = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));

				draw(graphics, capture.Gl);

				var rendered = await capture.CaptureAsync();

				// Checked before the image compare: a validation error explains a diff far better than the
				// diff does, and wgpu reports it out of band rather than failing the call that caused it.
				await Assert.That(capture.Device.LastUncapturedError).IsNull();

				await GoldenImage.Check(rendered, goldenName);
			}
			finally
			{
				LcdRenderSettings.Enabled = wasLcdEnabled;
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		[Test]
		public async Task Lines() => await Check("Primitives2D.Lines", Golden2DScenes.Lines);

		[Test]
		public async Task FilledPaths() => await Check("Primitives2D.FilledPaths", Golden2DScenes.FilledPaths);

		[Test]
		public async Task RoundedRects() => await Check("Primitives2D.RoundedRects", Golden2DScenes.RoundedRects);

		[Test]
		public async Task Gradients() => await Check("Primitives2D.Gradients", Golden2DScenes.Gradients);

		[Test]
		public async Task ImageBlits() => await Check("Primitives2D.ImageBlits", Golden2DScenes.ImageBlits);

		[Test]
		public async Task Transforms() => await Check("Primitives2D.Transforms", Golden2DScenes.Transforms);
	}
}
