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
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// A supersampled offscreen color + depth target: content (3D scenes, GL draws, 2D) is drawn into it at
	/// <see cref="Scale"/> times its logical size, then <see cref="Composite"/> box-downsamples it onto the
	/// current frame. The port of agg-gui's <c>SsaaFramebuffer</c> (agg-gui-wgpu/src/ssaa.rs).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Why SSAA, not MSAA.</b> WebGPU only guarantees sample counts 1 and 4, and the browser's WebGL2
	/// backend can silently lack more. Supersampling by size works on every adapter at any factor, so every
	/// texture and pipeline here has sample count 1.
	/// </para>
	/// <para>
	/// <b>Drawing.</b> <see cref="BeginDraw"/> points the compat layer at the oversized textures with its
	/// <see cref="GlCompatContext.CoordinateScale"/> set to the factor, so callers keep speaking logical
	/// pixels: GL viewports and scissors, <see cref="Graphics2DGpu"/> and the native scene renderer (which
	/// sizes its own targets from the coordinate scale) all land on the right texels. Depth is cleared to far
	/// and color to transparent at every <see cref="BeginDraw"/>; the texture holds premultiplied color.
	/// Nesting follows <see cref="GpuRenderTarget"/>: Begin/End pairs nest LIFO and the outer pass resumes
	/// with what it had. A full-frame capture (<see cref="INativeSceneRenderer.BeginFullFrameCapture"/>)
	/// opened inside a draw supersamples this texture in turn: its scale multiplies this target's, and
	/// ending it hands this target's scale back, so later draws into the target are unaffected.
	/// </para>
	/// <para>
	/// <b>Compositing.</b> Each destination pixel is the unweighted average of the
	/// <see cref="Scale"/> x <see cref="Scale"/> texels nearest behind it, blended premultiplied One /
	/// OneMinusSrcAlpha so pixels the content did not cover leave the frame showing through. That is the
	/// exact box average (and at 1x an exact copy) only at a 1:1 composite: no scale in the destination's
	/// transform, whole-pixel x and y, and a destination coordinate scale of 1. Anywhere else it is an
	/// approximation - the right block size, sampled around the nearest texel. The factor is capped where the device cannot allocate the texture.
	/// </para>
	/// </remarks>
	public sealed class SsaaRenderTarget : IDisposable
	{
		/// <summary>The WGSL module the downsample composite runs (the backend's <c>SsaaDownsample.wgsl</c>).</summary>
		public const string ShaderModuleKey = "SsaaDownsample";

		/// <summary>The largest supported factor: 4 x 4 = 16 samples per pixel.</summary>
		public const int MaxScale = 4;

		private const int UniformSize = 32;

		private readonly GL gl;
		private readonly GlCompatContext compat;
		private readonly byte[] uniformScratch = new byte[UniformSize];
		private IGpuBuffer uniform;
		private GpuTargetRedirect saved;

		/// <summary>Creates an empty target on the context behind <paramref name="gl"/>. No texture exists
		/// until the first <see cref="BeginDraw"/>.</summary>
		/// <param name="gl">The facade the frame draws through; its context must be the WebGPU compat layer.</param>
		/// <exception cref="NotSupportedException">The context is not a <see cref="GlCompatContext"/>.</exception>
		public SsaaRenderTarget(GL gl)
		{
			this.gl = gl ?? throw new ArgumentNullException(nameof(gl));
			this.compat = gl.GpuContext as GlCompatContext
				?? throw new NotSupportedException("An SsaaRenderTarget needs the WebGPU compat context (GlCompatContext) behind its GL.");
		}

		/// <summary>Logical width (what <see cref="Composite"/> covers at scale 1), 0 before the first draw.</summary>
		public int Width { get; private set; }

		/// <summary>Logical height, 0 before the first draw.</summary>
		public int Height { get; private set; }

		/// <summary>The linear supersample factor of the last draw (1 to <see cref="MaxScale"/>): the texture is
		/// this many times <see cref="Width"/> by <see cref="Height"/>.</summary>
		public int Scale { get; private set; } = 1;

		/// <summary>The supersampled color texture, or null before the first draw. Its top row is the top of
		/// the picture.</summary>
		public IGpuTexture ColorTexture { get; private set; }

		/// <summary>The matching <see cref="TextureFormat.Depth32Float"/> depth texture, or null before the first draw.</summary>
		public IGpuTexture DepthTexture { get; private set; }

		/// <summary>True between <see cref="BeginDraw"/> and <see cref="EndDraw"/>.</summary>
		public bool IsDrawing => this.saved != null;

		/// <summary>How many times the target has been painted (<see cref="BeginDraw"/> calls).</summary>
		public int DrawCount { get; private set; }

		/// <summary>
		/// Clears the target (reallocating if the size, factor or the frame's color format changed) and
		/// redirects drawing into it at <paramref name="scale"/> times <paramref name="width"/> by
		/// <paramref name="height"/>. Draw in logical pixels, origin bottom left - through the returned
		/// graphics, the <see cref="GL"/>, or the scene renderer - then dispose the scope (or call
		/// <see cref="EndDraw"/>) to put drawing back where it was.
		/// </summary>
		/// <param name="width">Logical width; at least 1 is used.</param>
		/// <param name="height">Logical height; at least 1 is used.</param>
		/// <param name="scale">Linear factor, 1 (off) to <see cref="MaxScale"/>. Lowered if the device's
		/// maximum texture size cannot hold it; <see cref="Scale"/> reports what was used.</param>
		/// <param name="deviceScale">The device scale the returned graphics reports.</param>
		/// <returns>The draw's scope; its <see cref="DrawScope.Graphics"/> paints 2D into the target.</returns>
		public DrawScope BeginDraw(int width, int height, int scale, double deviceScale = 1)
		{
			if (this.IsDrawing)
			{
				throw new InvalidOperationException("This SsaaRenderTarget is already being drawn into; call EndDraw first.");
			}

			if (scale < 1 || scale > MaxScale)
			{
				throw new ArgumentOutOfRangeException(nameof(scale), scale, $"The SSAA factor must be 1 to {MaxScale}.");
			}

			width = Math.Max(1, width);
			height = Math.Max(1, height);

			// A factor the device cannot allocate degrades to the largest that fits rather than failing the frame.
			int maxDimension = (int)this.compat.Device.Limits.MaxTextureDimension2D;
			while (scale > 1 && Math.Max(width, height) * scale > maxDimension)
			{
				scale--;
			}

			this.saved = GpuTargetRedirect.SaveCurrent(this.gl, this.compat);

			// Never the float format of a linear-light layer it is painted inside: the supersampled picture is sRGB,
			// and the downsample converts it once as it lands there (Composite).
			var colorFormat = this.compat.Passes.ColorFormat;
			this.EnsureTextures(width * scale, height * scale, colorFormat == TextureFormat.Undefined || colorFormat == TextureFormat.Rgba16Float ? TextureFormat.Bgra8Unorm : colorFormat);
			this.Width = width;
			this.Height = height;
			this.Scale = scale;
			this.saved.RedirectTo(this.ColorTexture, this.DepthTexture, scale, width, height, false);

			this.DrawCount++;
			return new DrawScope(this, new Graphics2DGpu(this.gl, width, height, deviceScale));
		}

		/// <summary>Ends drawing into the target and points drawing back at whatever was current at
		/// <see cref="BeginDraw"/>.</summary>
		public void EndDraw()
		{
			var frame = this.saved ?? throw new InvalidOperationException("EndDraw without a matching BeginDraw.");
			this.saved = null;
			frame.Restore();
		}

		/// <summary>
		/// Box-downsamples the picture onto the current target with its bottom-left corner at
		/// (<paramref name="x"/>, <paramref name="y"/>) and its logical size, through
		/// <paramref name="destination"/>'s transform (translation and scale) and the current GL scissor.
		/// Nothing is drawn before the first paint or while the target is being painted.
		/// </summary>
		/// <param name="destination">The surface drawn onto: the frame, or a layer's graphics.</param>
		/// <param name="x">Left edge in the destination's coordinates.</param>
		/// <param name="y">Bottom edge in the destination's coordinates.</param>
		public void Composite(Graphics2DGpu destination, double x, double y)
		{
			var target = this.compat.Passes.ColorTarget;
			if (this.ColorTexture == null || this.IsDrawing || target == null)
			{
				return;
			}

			double scaleX = 1;
			double scaleY = 1;
			var transform = destination.GetTransform();
			if (!transform.is_identity())
			{
				transform.Transform(ref x, ref y);
				scaleX = transform.sx;
				scaleY = transform.sy;
			}

			// Logical to the target's device pixels, then to clip space (y up in both).
			double pixels = this.compat.CoordinateScale;
			double targetWidth = target.Descriptor.Width;
			double targetHeight = target.Descriptor.Height;
			float ToClipX(double logical) => (float)((logical * pixels / targetWidth * 2) - 1);
			float ToClipY(double logical) => (float)((logical * pixels / targetHeight * 2) - 1);

			var span = this.uniformScratch.AsSpan();
			GlUniformBlock.WriteVector4(span, 0, ToClipX(x), ToClipY(y), ToClipX(x + (this.Width * scaleX)), ToClipY(y + (this.Height * scaleY)));
			GlUniformBlock.WriteVector4(span, 16, this.Scale, 0, 0, 0);

			// The GL scissor in device pixels; a clip that covers nothing draws nothing.
			var state = this.compat.State;
			(int X, int Y, int Width, int Height) scissor = (0, 0, (int)targetWidth, (int)targetHeight);
			if (state.ScissorEnabled)
			{
				scissor = this.compat.ToDeviceRect(state.Scissor);
				if (scissor.Width <= 0 || scissor.Height <= 0)
				{
					return;
				}
			}

			// Spend any clear queued on the target and end the compat pass: passes do not nest, and this
			// draw has to land after everything already drawn there.
			this.compat.Passes.EnsurePassOpen();
			this.compat.FlushPass();

			var device = this.compat.Device;
			this.uniform ??= device.CreateBuffer(BufferUsage.Uniform | BufferUsage.CopyDst, UniformSize);
			device.WriteBuffer(this.uniform, 0, this.uniformScratch);

			var cache = this.compat.Pipelines;
			var module = cache.GetShaderModule(GlShaderKeys.ForTarget(ShaderModuleKey, this.compat.Passes.LinearLight, true));
			var pipeline = cache.GetPipeline(new RenderPipelineDescriptor(
				module,
				"vertexMain",
				module,
				"fragmentMain",
				Array.Empty<VertexBufferLayout>(),
				new[]
				{
					// Premultiplied source-over: the texture already holds color times alpha.
					new ColorTargetState(
						target.Descriptor.Format,
						true,
						new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha),
						new BlendComponent(BlendOperation.Add, BlendFactor.One, BlendFactor.OneMinusSrcAlpha)),
				},
				new[]
				{
					new BindGroupLayoutEntry(0, 0, ShaderStage.Fragment, BindingType.Texture),
					new BindGroupLayoutEntry(0, 1, ShaderStage.Vertex | ShaderStage.Fragment, BindingType.UniformBuffer),
				},
				DepthStencilState.None,
				PrimitiveTopology.TriangleList,
				CullMode.None,
				FrontFace.Ccw,
				1,
				"SsaaDownsample"));

			var bindGroup = cache.GetBindGroup(new BindGroupDescriptor(
				pipeline,
				0,
				new[]
				{
					BindGroupEntry.ForTexture(0, this.ColorTexture),
					BindGroupEntry.ForBuffer(1, this.uniform),
				},
				"SsaaDownsample"));

			using (var encoder = device.BeginRenderPass(new RenderPassDescriptor(
				new[] { new ColorAttachment(target, LoadOp.Load) },
				DepthAttachment.None,
				"SsaaDownsample")))
			{
				encoder.SetPipeline(pipeline);
				encoder.SetBindGroup(0, bindGroup);
				encoder.SetScissor(scissor.X, scissor.Y, scissor.Width, scissor.Height);
				encoder.Draw(6);
			}

			// The one uniform buffer is rewritten by the next composite; submitting now keeps that write from
			// overtaking this draw in the queue.
			this.compat.Submit();
		}

		/// <summary>Releases the textures and the uniform buffer, first ending a draw still in progress.</summary>
		public void Dispose()
		{
			if (this.IsDrawing)
			{
				this.EndDraw();
			}

			this.ReleaseTextures();
			this.uniform?.Dispose();
			this.uniform = null;
		}

		private void EnsureTextures(int width, int height, TextureFormat format)
		{
			var current = this.ColorTexture?.Descriptor;
			if (current != null
				&& current.Value.Width == (uint)width
				&& current.Value.Height == (uint)height
				&& current.Value.Format == format)
			{
				return;
			}

			this.ReleaseTextures();

			// Sample count 1 on both: supersampling is done by size, never by MSAA.
			this.ColorTexture = this.compat.Device.CreateTexture(new TextureDescriptor(
				(uint)width,
				(uint)height,
				format,
				// CopySrc so a test or a screenshot can read the supersampled picture back.
				TextureUsage.RenderAttachment | TextureUsage.TextureBinding | TextureUsage.CopySrc,
				1,
				1,
				"SsaaColor"));
			this.DepthTexture = this.compat.Device.CreateTexture(new TextureDescriptor(
				(uint)width,
				(uint)height,
				TextureFormat.Depth32Float,
				TextureUsage.RenderAttachment,
				1,
				1,
				"SsaaDepth"));
		}

		private void ReleaseTextures()
		{
			if (this.ColorTexture == null)
			{
				return;
			}

			// Bind groups are cached on the texture object; without this every resize strands one.
			this.compat.Pipelines.InvalidateBindGroupsUsing(this.ColorTexture);
			this.ColorTexture.Dispose();
			this.DepthTexture.Dispose();
			this.ColorTexture = null;
			this.DepthTexture = null;
		}

		/// <summary>One paint of an <see cref="SsaaRenderTarget"/>, from <see cref="BeginDraw"/> to disposal.</summary>
		public sealed class DrawScope : IDisposable
		{
			private readonly int drawNumber;
			private SsaaRenderTarget owner;

			internal DrawScope(SsaaRenderTarget owner, Graphics2DGpu graphics)
			{
				this.owner = owner;
				this.drawNumber = owner.DrawCount;
				this.Graphics = graphics;
			}

			/// <summary>Draws 2D into the target in logical pixels, origin at the bottom left.</summary>
			public Graphics2DGpu Graphics { get; }

			/// <summary>Ends the draw (<see cref="EndDraw"/>) unless that already happened.</summary>
			public void Dispose()
			{
				var ending = this.owner;
				this.owner = null;
				// The draw number check keeps a stale scope from ending a later draw.
				if (ending != null && ending.IsDrawing && ending.DrawCount == this.drawNumber)
				{
					ending.EndDraw();
				}
			}
		}
	}
}
