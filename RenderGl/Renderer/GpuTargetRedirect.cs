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

using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// Points the compat layer's drawing at an offscreen texture in the middle of a frame and back again -
	/// the half <see cref="GpuRenderTarget"/> and <see cref="SsaaRenderTarget"/> share.
	/// </summary>
	/// <remarks>
	/// <see cref="SaveCurrent"/> ends the pass open on the current target, remembering that target with its
	/// viewport, scissor and coordinate scale; <see cref="Restore"/> puts all of it back, and the next draw
	/// re-opens the outer pass with <see cref="LoadOp.Load"/>, so what was already drawn there survives.
	/// Redirects nest LIFO.
	/// </remarks>
	internal sealed class GpuTargetRedirect
	{
		private readonly GL gl;
		private readonly GlCompatContext compat;
		private IGpuTexture color;
		private IGpuTexture depth;
		private bool linearLight;
		private int coordinateScale;
		private GlViewportRect viewport;
		private GlViewportRect scissor;
		private bool scissorEnabled;

		private GpuTargetRedirect(GL gl, GlCompatContext compat)
		{
			this.gl = gl;
			this.compat = compat;
		}

		/// <summary>Remembers where drawing goes now. Nothing is redirected until <see cref="RedirectTo"/>.</summary>
		public static GpuTargetRedirect SaveCurrent(GL gl, GlCompatContext compat)
		{
			var passes = compat.Passes;

			// A clear queued on the outer target is not tied to that target in the pass scope - redirecting
			// first would spend it on the offscreen texture and the outer target would never be cleared.
			// Opening the pass spends it where it belongs (the scene renderer's full-frame capture does the same).
			if (passes.ColorTarget != null)
			{
				passes.EnsurePassOpen();
			}

			var state = compat.State;
			return new GpuTargetRedirect(gl, compat)
			{
				color = passes.ColorTarget,
				depth = passes.DepthTarget,
				linearLight = passes.LinearLight,
				coordinateScale = compat.CoordinateScale,
				viewport = state.ViewportSet
					? state.Viewport
					: new GlViewportRect(0, 0, passes.TargetWidth / compat.CoordinateScale, passes.TargetHeight / compat.CoordinateScale),
				scissor = state.Scissor,
				scissorEnabled = state.ScissorEnabled,
			};
		}

		/// <summary>
		/// Draws into <paramref name="colorTarget"/> (and <paramref name="depthTarget"/> when given), both
		/// cleared - color to transparent, depth to far - with the viewport and scissor covering
		/// <paramref name="logicalWidth"/> by <paramref name="logicalHeight"/> logical pixels, which the
		/// compat layer multiplies by <paramref name="coordinateScale"/>.
		/// <para>
		/// The texture holds what the saved target holds - linear light or sRGB - as a scratch layer made in the
		/// target's format to be composited back texel for texel does; a target with its own kind of content says so
		/// through the overload.
		/// </para>
		/// </summary>
		public void RedirectTo(IGpuTexture colorTarget, IGpuTexture depthTarget, int coordinateScale, int logicalWidth, int logicalHeight)
			=> this.RedirectTo(colorTarget, depthTarget, coordinateScale, logicalWidth, logicalHeight, this.linearLight);

		/// <summary><see cref="RedirectTo(IGpuTexture, IGpuTexture, int, int, int)"/> into a texture that holds linear
		/// light when <paramref name="linearLight"/> (<see cref="GlRenderPassScope.LinearLight"/>).</summary>
		public void RedirectTo(IGpuTexture colorTarget, IGpuTexture depthTarget, int coordinateScale, int logicalWidth, int logicalHeight, bool linearLight)
		{
			this.compat.SetRenderTarget(colorTarget, depthTarget, linearLight);
			this.compat.Passes.RequestClear(true, depthTarget != null, ClearColor.Transparent);
			this.compat.CoordinateScale = coordinateScale;

			this.gl.Viewport(0, 0, logicalWidth, logicalHeight);
			this.gl.Scissor(0, 0, logicalWidth, logicalHeight);
			this.gl.Enable(EnableCap.ScissorTest);
		}

		/// <summary>Points drawing back at the saved target, with its viewport, scissor and coordinate scale.</summary>
		public void Restore()
		{
			// Spends the clear RedirectTo queued even when nothing was drawn, so it cannot leak onto the outer
			// target and the texture really is cleared.
			this.compat.Passes.EnsurePassOpen();

			this.compat.SetRenderTarget(this.color, this.depth, this.linearLight);
			this.compat.CoordinateScale = this.coordinateScale;

			this.gl.Viewport(this.viewport.X, this.viewport.Y, this.viewport.Width, this.viewport.Height);
			this.gl.Scissor(this.scissor.X, this.scissor.Y, this.scissor.Width, this.scissor.Height);
			if (this.scissorEnabled)
			{
				this.gl.Enable(EnableCap.ScissorTest);
			}
			else
			{
				this.gl.Disable(EnableCap.ScissorTest);
			}
		}
	}
}
