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
using MatterHackers.RenderGl;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// The cross-backend half of the 3D suite: the scenes in <see cref="Golden3DScenes"/> rendered by
	/// <c>WebGpuSceneRenderer</c> on <c>WebGpuRenderDevice</c> and compared against the <b>same</b> PNGs
	/// the classic D3D11 path captured.
	/// </summary>
	/// <remarks>
	/// Tolerance is zero, per O4 and per the Phase 2 result: the goal is 1:1 pixel identity, and a suite
	/// that starts permissive can never be tightened afterwards because nobody knows which differences
	/// were real. Where a difference turns out to be a genuine cross-API artifact rather than a port bug,
	/// the tolerance is raised <i>at the individual call site</i> with the evidence written next to it.
	/// <para>
	/// Every scene the classic suite captures has a counterpart here as of Phase 3 leg C, transparency
	/// and the bed included, and all but one of them compare at zero difference.
	/// </para>
	/// </remarks>
	[NotInParallel]
	public class GoldenSceneOnWebGpuTests
	{
		private static async Task Check(
			string goldenName,
			Action<WebGpuOffscreenCapture> drawScene,
			double maxPercentDifferingPixels = 0,
			bool supersample = false)
		{
			using var capture = WebGpuOffscreenCapture.Create();

			capture.ClearTo(Golden3DScenes.Background);

			var world = Golden3DScenes.CreateCamera(capture.Width, capture.Height);

			// A fresh LightingData per capture: RenderHelper.SetGlContext normalises LightDirection0 in
			// place, so a shared instance would be renormalising an already normalised vector.
			var lighting = new LightingData();

			RenderHelper.ResetLegacyMeshFallbackCount();
			capture.RenderScene(world, lighting, () => drawScene(capture), supersample);

			var rendered = await capture.CaptureAsync();

			// Checked before the image compare: a validation error explains a diff far better than the
			// diff does, and wgpu reports it out of band rather than failing the call that caused it.
			await Assert.That(capture.Device.LastUncapturedError).IsNull();

			// The whole point of closing the CanRender gaps: no mesh in a scene frame may reach the legacy
			// immediate-mode draw, which this backend's compat layer does not implement. A pixel diff would
			// catch a mesh that vanished, but not one the fallback happened to draw acceptably - this does.
			await Assert.That(RenderHelper.LegacyMeshFallbackCount).IsEqualTo(0)
				.Because($"'{goldenName}' fell through INativeSceneRenderer.CanRender to the legacy GL mesh path");

			await GoldenImage.Check(rendered, goldenName, channelTolerance: 0, maxPercentDifferingPixels);
		}

		[Test]
		public async Task OpaqueMeshes()
		{
			await Check("Scene.Opaque", capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 255));
		}

		[Test]
		public async Task OutlineRenderType()
		{
			await Check("Scene.Outlines", capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Outlines, 255));
		}

		[Test]
		public async Task WireframeRenderType()
		{
			await Check("Scene.Wireframe", capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Wireframe, 255));
		}

		/// <summary>
		/// The one golden in this suite that is not compared at zero difference, and the allowance is a
		/// measured, understood one rather than a shrug.
		/// </summary>
		/// <remarks>
		/// Every colour channel matches the golden exactly except on 1846 pixels (0.94%): the inner half of
		/// the selection outline, where it crosses geometry. The classic path dims those, because its
		/// selection mask never actually writes depth - it asks for a depth-writing state through
		/// <c>ShouldBindDepthStencilState</c>, whose cache the composite and blit passes invalidate without
		/// updating, so the state that stays bound is the blit's depth-off one and the occlusion test
		/// degenerates into "is there geometry under this pixel". The bug is frame-shape dependent (an
		/// overlay command in the frame makes the same code path bind depth correctly), so
		/// <c>WebGpuSceneRenderer</c> implements what the classic code says rather than what its state
		/// cache does. Emulating the bug was tried and does reproduce the golden channel for channel, which
		/// is where the 1846 number comes from.
		/// <para>
		/// This allowance goes away when the goldens are re-baselined from wgpu at cutover; until then it
		/// is deliberately just above the measured figure, so any <i>new</i> divergence still fails.
		/// </para>
		/// </remarks>
		[Test]
		public async Task SelectionOutline()
		{
			await Check(
				"Scene.SelectionOutline",
				capture => Golden3DScenes.DrawSelectionOutlineScene(capture.Gl, capture.SceneRenderer),
				maxPercentDifferingPixels: 1.0);
		}

		/// <summary>
		/// The transparency compositor: every mesh partly transparent and overlapping, so the image is
		/// entirely a product of the dual depth peeling passes. This is the golden the leg C reformulation
		/// exists for - the classic path's MAX-blended Rg32Float depth range against two hardware depth
		/// tests here, which must peel the same layers in the same order and terminate on the same
		/// iteration.
		/// </summary>
		[Test]
		public async Task TransparentMeshes()
		{
			await Check("Scene.Transparent", capture =>
				Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 120));
		}

		/// <summary>Opaque geometry in front of transparent geometry - the case that exercises the peel's
		/// rejection against the opaque depth buffer rather than peeling alone.</summary>
		[Test]
		public async Task MixedOpaqueAndTransparent()
		{
			await Check("Scene.Mixed", capture => Golden3DScenes.DrawMixedScene(capture.Gl));
		}

		/// <summary>Supersampled transparency: the capture target and the peel targets have to agree about
		/// resolution, and the peel shaders read their depth textures by pixel coordinate.</summary>
		[Test]
		public async Task SupersampledTransparent()
		{
			await Check(
				"Scene.SupersampledTransparent",
				capture => Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 120),
				supersample: true);
		}

		/// <summary>The printer bed, with its cast shadow and analytic grid.</summary>
		[Test]
		public async Task BedWithShadow()
		{
			await Check("Scene.Bed", capture => Golden3DScenes.DrawBedScene(capture.Gl, capture.SceneRenderer));
		}

		/// <summary>A textured mesh - the only scene that reaches the shaders' texture entry points.</summary>
		[Test]
		public async Task TexturedMesh()
		{
			await Check("Scene.TexturedMesh", capture => Golden3DScenes.DrawTexturedMeshScene(capture.Gl));
		}

		/// <summary>Overhang, drawn natively here and by the fallback on the oracle.</summary>
		[Test]
		public async Task OverhangRenderType()
		{
			await Check("Scene.Overhang", capture => Golden3DScenes.DrawOverhangScene(capture.Gl));
		}

		/// <summary>
		/// The 3x supersampled full-frame capture. The classic path's "swap the render target view" trick
		/// has no WebGPU equivalent - here the compat layer's target is swapped instead - so this golden is
		/// what says the replacement produces the same pixels, box filter and pixel-space widths included.
		/// </summary>
		[Test]
		public async Task SupersampledFullFrame()
		{
			await Check(
				"Scene.Supersampled",
				capture => Golden3DScenes.DrawStandardScene(capture.Gl, RenderTypes.Shaded, 255),
				supersample: true);
		}
	}
}
