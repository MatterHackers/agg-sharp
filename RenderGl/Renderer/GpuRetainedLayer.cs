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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// A <see cref="GpuRenderTarget"/> behind the <see cref="IRetainedLayer"/> contract.
	/// </summary>
	/// <remarks>
	/// <para>
	/// <b>Sharpness.</b> The layer paints at the compat context's <see cref="GlCompatContext.CoordinateScale"/>
	/// of the moment - 1 on the window, 3 inside a supersampled full-frame capture - so its texture holds
	/// as many pixels as the surface it is composited onto, and a layer inside a capture is as sharp as
	/// the capture. Widget coordinates are already device pixels, so nothing else scales it.
	/// </para>
	/// <para>
	/// <b>DestImage inside a layer works.</b> <see cref="Graphics2DGpu.DestImage"/> is a CPU raster layer that is
	/// normally drawn over the frame by the window host at the end of the frame. That one layer is per
	/// device context, so a widget inside a retained layer that asked for it would have replaced the
	/// window's buffer with one the layer's size, and its pixels would have landed on the window at the
	/// frame's origin rather than in the layer. So a paint sets the window's buffer aside, a widget that
	/// asks for DestImage during it gets a fresh buffer the layer's size, that buffer is drawn into the
	/// texture over whatever the GPU drew there when the paint ends (the same "over, at the end" order the
	/// window gives it) and then dropped, and the window's buffer is put back.
	/// </para>
	/// </remarks>
	internal sealed class GpuRetainedLayer : IRetainedLayer, IRoundedLayerCompositor
	{
		private readonly GL gl;
		private readonly double deviceScale;

		private Paint currentPaint;

		/// <param name="linearLight">True for a layer that holds linear light in rgba16float
		/// (<see cref="GpuRenderTarget.LinearLight"/>).</param>
		public GpuRetainedLayer(GL gl, double deviceScale, bool linearLight = false)
		{
			this.gl = gl;
			this.deviceScale = deviceScale;
			this.Target = new GpuRenderTarget(gl, linearLight);
		}

		public GpuRenderTarget Target { get; }

		/// <summary>The coordinate scale the retained texture was painted at.</summary>
		public int PaintedCoordinateScale { get; private set; } = 1;

		public int Width { get; private set; }

		public int Height { get; private set; }

		public int DrawCount => this.Target.DrawCount;

		public bool BelongsTo(Graphics2D destination)
		{
			return destination is Graphics2DGpu gpu && ReferenceEquals(gpu.gl, this.gl);
		}

		/// <summary>
		/// True when this layer cannot stand for what a paint onto <paramref name="destination"/> would give
		/// now: it was never painted, belongs to another device, or was painted at another coordinate scale -
		/// a 1x texture inside a supersampled capture would be magnified and blurry, and one painted during
		/// the capture would sit at 3x resolution on the window.
		/// </summary>
		public bool NeedsRepaintFor(Graphics2D destination)
		{
			return this.DrawCount == 0
				|| !this.BelongsTo(destination)
				|| ((GlCompatContext)this.gl.GpuContext).CoordinateScale != this.PaintedCoordinateScale;
		}

		public IRetainedLayerPaint Begin(int width, int height)
		{
			if (this.currentPaint != null)
			{
				throw new InvalidOperationException("This retained layer is already being painted.");
			}

			width = Math.Max(1, width);
			height = Math.Max(1, height);
			var compat = (GlCompatContext)this.gl.GpuContext;
			int scale = compat.CoordinateScale;

			var scope = this.Target.BeginDraw(width * scale, height * scale, this.deviceScale);
			try
			{
				// BeginDraw leaves the target at coordinate scale 1 in its own pixels; put it back in logical
				// pixels so the widgets painting into it clip and draw exactly as they would on the frame.
				compat.CoordinateScale = scale;
				this.gl.Viewport(0, 0, width, height);
				this.gl.Scissor(0, 0, width, height);

				this.Width = width;
				this.Height = height;
				this.PaintedCoordinateScale = scale;

				// The paint starts with no CPU raster layer of its own, so only a widget that asks for
				// DestImage during this paint gets one (see Paint.Dispose), and it is dropped afterwards
				// rather than uploaded again on every later repaint.
				var graphics = new Graphics2DGpu(this.gl, width, height, this.deviceScale);
				var outerCpuLayer = graphics.CpuLayer;
				graphics.CpuLayer = null;
				this.currentPaint = new Paint(this, scope, graphics, outerCpuLayer);
				return this.currentPaint;
			}
			catch
			{
				// Nothing owns the redirect yet, so give the frame its target back here.
				scope.Dispose();
				throw;
			}
		}

		/// <summary>
		/// Draws the retained texture onto <paramref name="destination"/> with its bottom-left corner at
		/// (<paramref name="x"/>, <paramref name="y"/>) - <see cref="Graphics2DGpu.RenderRetainedLayer"/>.
		/// </summary>
		/// <param name="roundedClip">When set, only what lies inside this rectangle (the layer's logical
		/// pixels, from its bottom-left) rounded by the radius is drawn - see
		/// <see cref="GpuRenderTarget.Composite"/>.</param>
		/// <returns>False, drawing nothing, when the destination is on another device.</returns>
		public bool CompositeOnto(Graphics2DGpu destination, double x, double y, double opacity, (Agg.RectangleDouble Bounds, double Radius)? roundedClip = null)
		{
			if (!this.BelongsTo(destination))
			{
				return false;
			}

			// The texture is CoordinateScale times the logical size (see Begin), and Composite sizes the quad
			// by the texture, so it is drawn through the destination's own transform with that scale divided
			// back out and the placement folded in.
			var saved = destination.GetTransform();
			try
			{
				double inverseScale = 1.0 / this.PaintedCoordinateScale;
				destination.SetTransform(Affine.NewScaling(inverseScale) * Affine.NewTranslation(x, y) * saved);
				// The clip is in logical pixels and the target in texture pixels, CoordinateScale times as many.
				var clip = roundedClip;
				if (clip.HasValue)
				{
					var (bounds, radius) = clip.Value;
					int scale = this.PaintedCoordinateScale;
					clip = (new Agg.RectangleDouble(bounds.Left * scale, bounds.Bottom * scale, bounds.Right * scale, bounds.Top * scale), radius * scale);
				}

				this.Target.Composite(destination, 0, 0, opacity, clip);
			}
			finally
			{
				destination.SetTransform(saved);
			}

			return true;
		}

		/// <inheritdoc/>
		public bool CompositeRounded(Graphics2D destination, double x, double y, double opacity, Agg.RectangleDouble roundedClip, double cornerRadius)
		{
			// A linear-light layer has no rounded composite; false, per the contract, and the caller draws it unclipped.
			return !this.Target.LinearLight && destination is Graphics2DGpu gpu && this.CompositeOnto(gpu, x, y, opacity, (roundedClip, cornerRadius));
		}

		/// <summary>Ends a paint still in progress and releases the texture. Safe from any thread (a widget
		/// closed by a worker): off the render thread the whole release waits for it, paint included.</summary>
		public void Dispose()
		{
			if (((GlCompatContext)this.gl.GpuContext).ReleaseOnRenderThread(this))
			{
				return;
			}

			this.currentPaint?.Dispose();
			this.Target.Dispose();
		}

		private sealed class Paint : IRetainedLayerPaint
		{
			private readonly GpuRetainedLayer owner;
			private readonly GpuRenderTarget.DrawScope scope;
			private readonly ImageBuffer outerCpuLayer;
			private bool ended;

			public Paint(GpuRetainedLayer owner, GpuRenderTarget.DrawScope scope, Graphics2DGpu graphics, ImageBuffer outerCpuLayer)
			{
				this.owner = owner;
				this.scope = scope;
				this.Graphics = graphics;
				this.outerCpuLayer = outerCpuLayer;
			}

			public Graphics2D Graphics { get; }

			public void Dispose()
			{
				if (this.ended)
				{
					return;
				}

				this.ended = true;
				var graphics = (Graphics2DGpu)this.Graphics;
				try
				{
					if (graphics.HasCpuLayer)
					{
						// Still redirected, so this lands in the texture; clip to the whole layer so the
						// last child's scissor does not cut it.
						graphics.SetClippingRect(new Agg.RectangleDouble(0, 0, graphics.Width, graphics.Height));
						graphics.CompositeCpuLayer();
					}
				}
				finally
				{
					graphics.CpuLayer = this.outerCpuLayer;
					this.owner.currentPaint = null;
					this.scope.Dispose();
				}
			}
		}
	}
}
