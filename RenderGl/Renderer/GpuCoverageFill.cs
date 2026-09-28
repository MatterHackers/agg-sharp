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
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderCore;
using MatterHackers.RenderGl.Compat;

namespace MatterHackers.RenderGl
{
	/// <summary>
	/// The first half of the per-pixel fills (<see cref="GpuGradientFill"/>, <see cref="GpuImageFilter"/>): the path
	/// drawn in white into a transparent coverage layer the size of the target - the ordinary halo-anti-aliased fill -
	/// and the device rectangle and device-to-screen mapping a full-target shading pass then needs.
	/// </summary>
	internal static class GpuCoverageFill
	{
		/// <summary>The GPU context a per-pixel fill draws through, or why there is none.</summary>
		public static GlCompatContext ContextOf(Graphics2DGpu graphics, string operation)
		{
			if (!(graphics.gl?.GpuContext is GlCompatContext context) || context.Passes.ColorTarget == null)
			{
				throw new NotSupportedException(operation + " needs a GPU render target.");
			}

			return context;
		}

		/// <summary>
		/// Draws <paramref name="path"/>'s coverage into <paramref name="coverage"/> (fitted to the target) and works
		/// out where the shading pass runs. False when there is nothing to shade. On true the compat pass has been
		/// ended, so the caller's own pass can run.
		/// </summary>
		/// <param name="inUse">The caller's guard against a nested fill: its coverage layer is in use while the path draws.</param>
		public static bool DrawCoverage(Graphics2DGpu graphics, GlCompatContext context, IVertexSource path, ref IGpuTexture coverage, ref bool inUse, string label, string operation, out CoveragePass pass)
		{
			pass = default;
			if (inUse)
			{
				throw new NotSupportedException(operation + " cannot be nested.");
			}

			var gl = graphics.gl;
			var target = context.Passes.ColorTarget;
			var descriptor = target.Descriptor;
			int scale = context.CoordinateScale;
			var state = context.State;
			GlViewportRect viewport = state.ViewportSet
				? state.Viewport
				: new GlViewportRect(0, 0, (int)descriptor.Width / scale, (int)descriptor.Height / scale);

			var bounds = new VertexSourceApplyTransform(path, graphics.GetTransform()).GetBounds();
			if (bounds.Width <= 0 || bounds.Height <= 0)
			{
				return false;
			}

			inUse = true;
			var redirect = GpuTargetRedirect.SaveCurrent(gl, context);
			bool drawn = false;
			try
			{
				coverage = GpuBlur.Layers.Fit(context, coverage, descriptor, label);
				redirect.RedirectTo(coverage, null, scale, (int)descriptor.Width / scale, (int)descriptor.Height / scale);

				// The layer lines up with the target texel for texel, so the draw keeps the target's viewport.
				if (state.ViewportSet)
				{
					gl.Viewport(viewport.X, viewport.Y, viewport.Width, viewport.Height);
				}

				graphics.Render(path, Color.White);
				drawn = true;
			}
			finally
			{
				redirect.Restore();
				inUse = false;
			}

			if (!drawn)
			{
				return false;
			}

			var deviceToScreen = DeviceToScreen(graphics, context);
			Affine screenToDevice = deviceToScreen;
			screenToDevice.invert();
			double left = bounds.Left, bottom = bounds.Bottom, right = bounds.Right, top = bounds.Top;
			screenToDevice.Transform(ref left, ref bottom);
			screenToDevice.Transform(ref right, ref top);

			// The halo reaches a pixel or so past the path; the scissor, if any, limits it further.
			var clip = DeviceClip(context);
			int x0 = Math.Max((int)Math.Floor(Math.Min(left, right)) - 2, clip.X);
			int y0 = Math.Max((int)Math.Floor(Math.Min(bottom, top)) - 2, clip.Y);
			int x1 = Math.Min((int)Math.Ceiling(Math.Max(left, right)) + 2, clip.X + clip.Width);
			int y1 = Math.Min((int)Math.Ceiling(Math.Max(bottom, top)) + 2, clip.Y + clip.Height);
			if (x1 <= x0 || y1 <= y0)
			{
				return false;
			}

			// Spend any clear queued on the target and end the compat pass: the shading pass cannot run inside it.
			context.Passes.EnsurePassOpen();
			context.FlushPass();

			pass = new CoveragePass(target, deviceToScreen, x0, y0, x1 - x0, y1 - y0);
			return true;
		}

		/// <summary>
		/// Device pixels (top-down, centres at .5) to the Graphics2D's pixels, which the ortho projection maps onto
		/// the viewport.
		/// </summary>
		public static Affine DeviceToScreen(Graphics2DGpu graphics, GlCompatContext context)
		{
			var descriptor = context.Passes.ColorTarget.Descriptor;
			int scale = context.CoordinateScale;
			var state = context.State;
			GlViewportRect viewport = state.ViewportSet
				? state.Viewport
				: new GlViewportRect(0, 0, (int)descriptor.Width / scale, (int)descriptor.Height / scale);
			double width = graphics.Width;
			double height = graphics.Height;
			return new Affine(
				width / (viewport.Width * (double)scale),
				0,
				0,
				-height / (viewport.Height * (double)scale),
				-viewport.X * width / viewport.Width,
				(((double)descriptor.Height / scale) - viewport.Y) * height / viewport.Height);
		}

		/// <summary>The device rectangle a pass may touch: the whole target, or the scissor when one is set.</summary>
		public static (int X, int Y, int Width, int Height) DeviceClip(GlCompatContext context)
		{
			var descriptor = context.Passes.ColorTarget.Descriptor;
			return context.State.ScissorEnabled
				? context.ToDeviceRect(context.State.Scissor)
				: (0, 0, (int)descriptor.Width, (int)descriptor.Height);
		}

		/// <summary>Where a shading pass over a drawn coverage layer runs.</summary>
		public readonly struct CoveragePass
		{
			public CoveragePass(IGpuTexture target, Affine deviceToScreen, int x, int y, int width, int height)
			{
				this.Target = target;
				this.DeviceToScreen = deviceToScreen;
				this.X = x;
				this.Y = y;
				this.Width = width;
				this.Height = height;
			}

			/// <summary>The render target to shade onto.</summary>
			public IGpuTexture Target { get; }

			/// <summary>Device pixels (top-down, centres at .5) to the Graphics2D's pixels.</summary>
			public Affine DeviceToScreen { get; }

			/// <summary>The scissor rectangle, in device pixels.</summary>
			public int X { get; }

			public int Y { get; }

			public int Width { get; }

			public int Height { get; }
		}
	}
}
