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
using MatterHackers.Agg;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// An offscreen color texture a <see cref="Graphics2DGpu"/> can paint into and that can then be drawn
	/// onto any other target with <see cref="Composite"/>
	/// - the GPU half of a retained widget backbuffer (agg-gui's <c>BackbufferKind::GlFbo</c>).
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Nesting.</b> <see cref="BeginDraw"/> may be called in the middle of a frame (or in the middle of
	/// another target's draw): it ends the pass open on the current target, remembers that target with its
	/// viewport, scissor and coordinate scale, and points the compat layer at this texture.
	/// <see cref="EndDraw"/> puts all of it back, and the next draw re-opens the outer pass with
	/// <see cref="LoadOp.Load"/>, so what was already drawn there survives. Begin/End pairs must nest LIFO.
	/// </para>
	/// <para>
	/// <b>Alpha.</b> The texture starts every draw cleared to transparent black and holds
	/// <em>premultiplied</em> color: the 2D path blends One / OneMinusSrcAlpha with premultiplied sources, so
	/// both the color and the alpha channel accumulate correctly over transparent. It is composited back the
	/// same way, with the opacity multiplied into all four channels.
	/// </para>
	/// <para>
	/// <b>Scale.</b> The target always paints at coordinate scale 1 and <see cref="Composite"/> places it in
	/// the destination's logical pixels, so inside a supersampled full-frame capture
	/// (<see cref="GlCompatContext.CoordinateScale"/> above 1) it is magnified rather than painted at the
	/// capture's resolution. A caller that wants it sharp there sizes it by the coordinate scale.
	/// </para>
	/// <para>
	/// <b>Retention</b> is the caller's decision: the texture keeps its pixels until the next
	/// <see cref="BeginDraw"/>, so a clean widget draws it again without painting anything.
	/// <see cref="DrawCount"/> counts the repaints.
	/// </para>
	/// <para>
	/// Nothing here waits on the GPU, so the same code runs in the browser, where only readback is async.
	/// </para>
	/// </remarks>
	public sealed class GpuRenderTarget : IDisposable
	{
		private readonly GL gl;
		private readonly GlCompatContext compat;
		private int textureName;
		private GpuTargetRedirect saved;

		/// <summary>Creates an empty target on the context behind <paramref name="gl"/>. No texture exists
		/// until the first <see cref="BeginDraw"/>.</summary>
		/// <param name="gl">The facade the frame draws through; its context must be the WebGPU compat layer.</param>
		/// <exception cref="NotSupportedException">The context is not a <see cref="GlCompatContext"/>.</exception>
		public GpuRenderTarget(GL gl)
			: this(gl, false)
		{
		}

		/// <summary>Creates an empty target that, when <paramref name="linearLight"/>, holds linear light in
		/// rgba16float in place of the frame's sRGB bytes (see <see cref="LinearLight"/>).</summary>
		internal GpuRenderTarget(GL gl, bool linearLight)
		{
			this.LinearLight = linearLight;
			this.gl = gl ?? throw new ArgumentNullException(nameof(gl));
			this.compat = gl.GpuContext as GlCompatContext
				?? throw new NotSupportedException("A GpuRenderTarget needs the WebGPU compat context (GlCompatContext) behind its GL.");

			// A target its holder never disposes (a demo or widget kept past its surface) would otherwise keep its
			// texture, and through it the whole wgpu device, alive after the context is gone.
			this.compat.Own(this);
		}

		/// <summary>
		/// True when the texture is rgba16float holding linear premultiplied light: draws into it are converted from
		/// sRGB (<see cref="GlShaderKeys.ForTarget"/>), so they mix as C++ AGG's float buffers do, and
		/// <see cref="Composite(Graphics2DGpu, double, double, double)"/> mixes it onto its destination in linear light
		/// too (<see cref="GpuCompOp.CompositeLinearLayer"/>). Such a target is composited texel for texel only: no
		/// scaling and no rounded clip.
		/// </summary>
		internal bool LinearLight { get; }

		/// <summary>Width of the texture in pixels, 0 before the first draw.</summary>
		public int Width => (int)(this.Texture?.Descriptor.Width ?? 0);

		/// <summary>Height of the texture in pixels, 0 before the first draw.</summary>
		public int Height => (int)(this.Texture?.Descriptor.Height ?? 0);

		/// <summary>The color texture, or null before the first draw. Its top row is the top of the picture.</summary>
		public IGpuTexture Texture { get; private set; }

		/// <summary>True between <see cref="BeginDraw"/> and <see cref="EndDraw"/>.</summary>
		public bool IsDrawing => this.saved != null;

		/// <summary>How many times the target has been painted (<see cref="BeginDraw"/> calls).</summary>
		public int DrawCount { get; private set; }

		/// <summary>The GL texture name the compositing draw binds.</summary>
		internal int TextureName => this.textureName;

		/// <summary>
		/// Clears the target to transparent (reallocating it if the size or the frame's color format
		/// changed), redirects drawing into it and returns the scope to paint through: its
		/// <see cref="DrawScope.Graphics"/> draws in the target's own pixels with the origin at the bottom
		/// left, and disposing it (or calling <see cref="EndDraw"/>) puts drawing back where it was. Use it in
		/// a <c>using</c> so a paint that throws still restores the frame.
		/// </summary>
		/// <param name="width">Width in pixels; at least 1 is used.</param>
		/// <param name="height">Height in pixels; at least 1 is used.</param>
		/// <param name="deviceScale">The device scale the returned graphics reports.</param>
		public DrawScope BeginDraw(int width, int height, double deviceScale = 1)
		{
			if (this.IsDrawing)
			{
				throw new InvalidOperationException("This GpuRenderTarget is already being drawn into; call EndDraw first.");
			}

			width = Math.Max(1, width);
			height = Math.Max(1, height);

			this.saved = GpuTargetRedirect.SaveCurrent(this.gl, this.compat);

			// A plain target painted inside a linear-light one must not take its float format: it holds sRGB bytes,
			// and is converted once, when it is composited into the linear layer.
			var colorFormat = this.compat.Passes.ColorFormat;
			var format = this.LinearLight ? TextureFormat.Rgba16Float
				: colorFormat == TextureFormat.Undefined || colorFormat == TextureFormat.Rgba16Float ? TextureFormat.Bgra8Unorm : colorFormat;
			this.EnsureTexture(width, height, format);
			this.saved.RedirectTo(this.Texture, null, 1, width, height, this.LinearLight);

			this.DrawCount++;
			return new DrawScope(this, new Graphics2DGpu(this.gl, width, height, deviceScale));
		}

		/// <summary>Ends drawing into the target and points drawing back at whatever was current at
		/// <see cref="BeginDraw"/>, with its viewport, scissor and coordinate scale.</summary>
		public void EndDraw()
		{
			var frame = this.saved ?? throw new InvalidOperationException("EndDraw without a matching BeginDraw.");
			this.saved = null;
			frame.Restore();
		}

		/// <summary>
		/// Draws the target's picture as a textured quad with its bottom-left corner at
		/// (<paramref name="x"/>, <paramref name="y"/>), through <paramref name="destination"/>'s transform
		/// (translation and scale) and clip.
		/// </summary>
		/// <remarks>
		/// The target holds premultiplied color, so this blends One / OneMinusSrcAlpha - unlike the
		/// straight-alpha <see cref="Graphics2DGpu.Render(Agg.Image.IImageByte, double, double, double, double, double)"/> -
		/// and applies <paramref name="opacity"/> to all four channels, which is what scaling a premultiplied
		/// pixel means. Opacity is quantized to 1/255, the vertex color's resolution. Nothing is drawn before
		/// the first paint or while the target is being painted.
		/// </remarks>
		/// <param name="destination">The surface drawn onto: the frame, or another target's graphics.</param>
		/// <param name="x">Left edge in the destination's coordinates.</param>
		/// <param name="y">Bottom edge in the destination's coordinates.</param>
		/// <param name="opacity">0 (invisible) to 1 (as painted).</param>
		public void Composite(Graphics2DGpu destination, double x, double y, double opacity = 1)
		{
			this.Composite(destination, x, y, opacity, null);
		}

		/// <summary>
		/// <see cref="Composite(Graphics2DGpu, double, double, double)"/> keeping only what lies inside
		/// <paramref name="roundedClip"/> (the target's pixels, from its bottom-left) rounded by
		/// <paramref name="cornerRadius"/>, with a 1 pixel anti-aliased edge (see <see cref="RoundedLayerClip"/>).
		/// </summary>
		public void Composite(Graphics2DGpu destination, double x, double y, double opacity, Agg.RectangleDouble roundedClip, double cornerRadius)
		{
			this.Composite(destination, x, y, opacity, (roundedClip, cornerRadius));
		}

		internal void Composite(Graphics2DGpu destination, double x, double y, double opacity, (Agg.RectangleDouble Bounds, double Radius)? roundedClip)
		{
			if (this.Texture == null || this.IsDrawing || opacity <= 0)
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

			double right = x + (this.Width * scaleX);
			double top = y + (this.Height * scaleY);

			if (this.LinearLight)
			{
				this.CompositeLinear(destination, x, top, scaleX, scaleY, opacity, roundedClip.HasValue);
				return;
			}

			var destinationGl = destination.gl;
			destination.PushOrthoProjection();
			destinationGl.Disable(EnableCap.Lighting);
			destinationGl.Enable(EnableCap.Texture2D);
			destinationGl.Disable(EnableCap.DepthTest);
			destinationGl.Enable(EnableCap.Blend);
			destinationGl.BlendFunc(BlendingFactorSrc.One, BlendingFactorDest.OneMinusSrcAlpha);

			var alpha = (byte)Math.Round(Math.Min(1, opacity) * 255);
			destinationGl.Color4(alpha, alpha, alpha, alpha);
			destinationGl.BindTexture(TextureTarget.Texture2D, this.textureName);

			if (roundedClip.HasValue)
			{
				var (clip, radius) = roundedClip.Value;
				var quad = new Agg.RectangleDouble(x, y, right, top);
				var clipOnDestination = new Agg.RectangleDouble(x + (clip.Left * scaleX), y + (clip.Bottom * scaleY), x + (clip.Right * scaleX), y + (clip.Top * scaleY));
				RoundedLayerClip.Draw(destinationGl, quad, clipOnDestination, radius * Math.Min(Math.Abs(scaleX), Math.Abs(scaleY)), alpha);
				destination.PopOrthoProjection();
				return;
			}

			// The texture's row 0 is the top of its picture (WebGPU framebuffer space), while the destination
			// is y-up, so v runs 1 at the bottom to 0 at the top - the reverse of an uploaded ImageBuffer,
			// whose row 0 is its bottom.
			destinationGl.Begin(BeginMode.TriangleFan);
			destinationGl.TexCoord2(0, 1);
			destinationGl.Vertex2(x, y);
			destinationGl.TexCoord2(0, 0);
			destinationGl.Vertex2(x, top);
			destinationGl.TexCoord2(1, 0);
			destinationGl.Vertex2(right, top);
			destinationGl.TexCoord2(1, 1);
			destinationGl.Vertex2(right, y);
			destinationGl.End();

			destination.PopOrthoProjection();
		}

		/// <summary>Releases the texture and its GL name, first ending a draw still in progress so the frame
		/// it interrupted gets its target back.</summary>
		public void Dispose()
		{
			if (this.IsDrawing)
			{
				this.EndDraw();
			}

			if (this.textureName != 0)
			{
				this.compat.Textures.Delete(this.textureName);
				this.textureName = 0;
			}

			this.ReleaseTexture();
			this.compat.Disown(this);
		}

		/// <summary>The linear-light composite: the texture placed texel for texel with its top-left at logical
		/// (<paramref name="x"/>, <paramref name="top"/>) on the destination.</summary>
		private void CompositeLinear(Graphics2DGpu destination, double x, double top, double scaleX, double scaleY, double opacity, bool rounded)
		{
			int scale = this.compat.CoordinateScale;
			if (rounded || Math.Abs((scaleX * scale) - 1) > 1e-9 || Math.Abs((scaleY * scale) - 1) > 1e-9)
			{
				throw new NotSupportedException("A linear-light layer is composited texel for texel: without scaling or a rounded clip.");
			}

			var state = this.compat.State;
			double originX = state.ViewportSet ? state.Viewport.X : 0;
			double originY = state.ViewportSet ? state.Viewport.Y : 0;
			int deviceX = (int)Math.Round((originX + x) * scale);
			int deviceY = this.compat.Passes.TargetHeight - (int)Math.Round((originY + top) * scale);
			GpuCompOp.CompositeLinearLayer(destination.gl, this.Texture, deviceX, deviceY, opacity);
		}

		private void EnsureTexture(int width, int height, TextureFormat format)
		{
			var current = this.Texture?.Descriptor;
			if (current != null
				&& current.Value.Width == (uint)width
				&& current.Value.Height == (uint)height
				&& current.Value.Format == format)
			{
				return;
			}

			this.ReleaseTexture();
			this.Texture = this.compat.Device.CreateTexture(new TextureDescriptor(
				(uint)width,
				(uint)height,
				format,
				// CopySrc so a test or a screenshot can read the picture back.
				TextureUsage.RenderAttachment | TextureUsage.TextureBinding | TextureUsage.CopySrc,
				1,
				1,
				"GpuRenderTarget"));

			if (this.textureName == 0)
			{
				this.textureName = this.compat.Textures.GenerateName(this.Texture);

				// Composited 1:1 onto whole pixels, where nearest sampling is exact; clamped so the edge
				// texels never pick up the opposite edge.
				var entry = this.compat.Textures.Find(this.textureName);
				entry.MagFilterLinear = false;
				entry.MinFilterLinear = false;
				entry.Clamp = true;
			}
			else
			{
				this.compat.Textures.Find(this.textureName).Texture = this.Texture;
			}
		}

		private void ReleaseTexture()
		{
			if (this.Texture == null)
			{
				return;
			}

			// The pipeline cache keys bind groups on the texture object, so without this every resize would
			// strand a bind group pointing at a destroyed texture for the life of the context.
			this.compat.Pipelines.InvalidateBindGroupsUsing(this.Texture);
			this.Texture.Dispose();
			this.Texture = null;
		}

		/// <summary>
		/// One paint of a <see cref="GpuRenderTarget"/>, from <see cref="BeginDraw"/> to disposal.
		/// </summary>
		public sealed class DrawScope : IDisposable
		{
			private readonly int drawNumber;
			private GpuRenderTarget owner;

			internal DrawScope(GpuRenderTarget owner, Graphics2DGpu graphics)
			{
				this.owner = owner;
				this.drawNumber = owner.DrawCount;
				this.Graphics = graphics;
			}

			/// <summary>Draws into the target, in its pixels, origin at the bottom left.</summary>
			public Graphics2DGpu Graphics { get; }

			/// <summary>Ends the draw (<see cref="EndDraw"/>) unless that already happened.</summary>
			public void Dispose()
			{
				var ending = this.owner;
				this.owner = null;
				// The draw number check keeps a stale scope, disposed after its draw was ended by hand and a
				// new one begun, from ending someone else's draw.
				if (ending != null && ending.IsDrawing && ending.DrawCount == this.drawNumber)
				{
					ending.EndDraw();
				}
			}
		}
	}
}
