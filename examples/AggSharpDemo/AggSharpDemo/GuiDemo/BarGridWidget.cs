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
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.Compat;
using MatterHackers.RenderGl.OpenGl;

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// agg-gui's animated 3D bar grid (WgpuCubeWidget): the bars of <see cref="BarGridMath"/> drawn into a
	/// supersampled <see cref="SsaaRenderTarget"/> behind the widget's children and box-downsampled onto the
	/// frame. Pixels the bars do not cover stay transparent, so the widget's background shows through.
	/// </summary>
	/// <remarks>
	/// GPU only: on a software surface there is no 3D path, so nothing is drawn but
	/// <see cref="SoftwarePlaceholder"/> (if set). While it is drawn on the GPU it asks for the next frame
	/// through <see cref="UiThread"/>; a frame is only requested from a draw, and the request is dropped
	/// once the widget is hidden or closed, so the animation stops when nobody can see it.
	/// </remarks>
	public class BarGridWidget : GuiWidget
	{
		// The five faces a camera above the grid can see (the bottom never shows), as agg-gui's box lists
		// them: four corners in unit-bar space (y 0..1, scaled by the bar height) and the flat normal.
		private static readonly (double[][] Corners, double[] Normal)[] Faces =
		{
			(new[] { new[] { -1.0, 1, -1 }, new[] { 1.0, 1, -1 }, new[] { 1.0, 1, 1 }, new[] { -1.0, 1, 1 } }, new[] { 0.0, 1, 0 }),
			(new[] { new[] { -1.0, 0, 1 }, new[] { 1.0, 0, 1 }, new[] { 1.0, 1, 1 }, new[] { -1.0, 1, 1 } }, new[] { 0.0, 0, 1 }),
			(new[] { new[] { 1.0, 0, -1 }, new[] { -1.0, 0, -1 }, new[] { -1.0, 1, -1 }, new[] { 1.0, 1, -1 } }, new[] { 0.0, 0, -1 }),
			(new[] { new[] { 1.0, 0, 1 }, new[] { 1.0, 0, -1 }, new[] { 1.0, 1, -1 }, new[] { 1.0, 1, 1 } }, new[] { 1.0, 0, 0 }),
			(new[] { new[] { -1.0, 0, -1 }, new[] { -1.0, 0, 1 }, new[] { -1.0, 1, 1 }, new[] { -1.0, 1, -1 } }, new[] { -1.0, 0, 0 }),
		};

		private readonly Stopwatch clock = Stopwatch.StartNew();
		private readonly DemoTheme demoTheme;
		private SsaaRenderTarget target;
		private GL targetGl;
		private int ssaaFactor = 2;
		private bool frameRequested;

		/// <param name="demoTheme">Picks the dark or light palette; null always uses the dark one.</param>
		public BarGridWidget(DemoTheme demoTheme = null)
		{
			this.demoTheme = demoTheme;
		}

		/// <summary>The linear supersample factor, 1 (off) to <see cref="SsaaRenderTarget.MaxScale"/>: the bars
		/// are drawn at this many times the widget's pixel size in each direction (agg-gui's Off / 4x / 9x / 16x
		/// are 1 to 4 here). Out-of-range values are clamped. Default 2 (4 samples per pixel).</summary>
		public int SsaaFactor
		{
			get => this.ssaaFactor;
			set
			{
				int clamped = Math.Clamp(value, 1, SsaaRenderTarget.MaxScale);
				if (clamped != this.ssaaFactor)
				{
					this.ssaaFactor = clamped;
					this.Invalidate();
				}
			}
		}

		/// <summary>When set, the animation shows this many seconds in and does not advance - for tests and
		/// stills. Null (the default) follows the clock.</summary>
		public double? FixedTimeSeconds { get; set; }

		/// <summary>Text drawn centred on a software surface, where the 3D path does not exist; null draws
		/// nothing there.</summary>
		public string SoftwarePlaceholder { get; set; }

		/// <summary>True when the last draw had the GPU and drew the bars.</summary>
		public bool DrewBars { get; private set; }

		/// <summary>How long the last GPU draw took on the CPU (recording the bars and the composite), in
		/// milliseconds.</summary>
		public double LastDrawMilliseconds { get; private set; }

		/// <summary>True while a next frame has been asked for and not yet delivered.</summary>
		public bool FrameRequested => this.frameRequested;

		/// <summary>Whether a requested frame should still be drawn: the widget is open and visible on screen.</summary>
		public bool ShouldAnimate => !this.HasBeenClosed && this.FixedTimeSeconds == null && this.ActuallyVisibleOnScreen();

		public override void OnDraw(Graphics2D graphics2D)
		{
			this.DrewBars = false;
			if (graphics2D is Graphics2DGpu gpu && gpu.gl?.GpuContext is GlCompatContext && this.Width >= 1 && this.Height >= 1)
			{
				var timer = Stopwatch.StartNew();
				this.DrawBars(gpu);
				this.LastDrawMilliseconds = timer.Elapsed.TotalMilliseconds;
				this.DrewBars = true;
				this.RequestNextFrame();
			}
			else if (!string.IsNullOrEmpty(this.SoftwarePlaceholder))
			{
				var color = this.demoTheme?.Palette.TextDim ?? Color.Gray;
				graphics2D.DrawString(this.SoftwarePlaceholder, this.Width / 2, this.Height / 2, 11 * DeviceScale, Justification.Center, Baseline.BoundsCenter, color);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnClosed(EventArgs e)
		{
			this.target?.Dispose();
			this.target = null;
			base.OnClosed(e);
		}

		/// <summary>Asks for one more frame on the next idle, unless one is already pending or the animation is
		/// frozen. The frame is only drawn if the widget is still visible then.</summary>
		private void RequestNextFrame()
		{
			if (this.frameRequested || this.FixedTimeSeconds != null)
			{
				return;
			}

			this.frameRequested = true;
			UiThread.RunOnIdle(() =>
			{
				this.frameRequested = false;
				if (this.ShouldAnimate)
				{
					this.Invalidate();
				}
			});
		}

		private BarPalette Palette()
		{
			bool dark = this.demoTheme?.IsDark ?? true;
			var text = this.demoTheme?.Palette.TextColor ?? Color.White;
			return BarGridMath.PaletteFor(dark, new ColorF(text.red / 255.0, text.green / 255.0, text.blue / 255.0));
		}

		private void DrawBars(Graphics2DGpu frame)
		{
			var gl = frame.gl;
			if (this.target == null || this.targetGl != gl)
			{
				this.target?.Dispose();
				this.target = new SsaaRenderTarget(gl);
				this.targetGl = gl;
			}

			int width = (int)Math.Ceiling(this.Width);
			int height = (int)Math.Ceiling(this.Height);
			double seconds = this.FixedTimeSeconds ?? this.clock.Elapsed.TotalSeconds;
			double phase = BarGridMath.WavePhase(seconds);
			var palette = this.Palette();

			using (this.target.BeginDraw(width, height, this.ssaaFactor))
			{
				gl.MatrixMode(MatrixMode.Projection);
				gl.PushMatrix();
				gl.LoadMatrix(BarGridMath.Projection(width / (double)height));
				gl.MatrixMode(MatrixMode.Modelview);
				gl.PushMatrix();
				gl.LoadMatrix(BarGridMath.View());

				gl.Disable(EnableCap.Texture2D);
				gl.Disable(EnableCap.Blend);
				gl.Disable(EnableCap.Lighting);
				gl.Disable(EnableCap.CullFace);
				gl.Enable(EnableCap.DepthTest);
				gl.DepthFunc(DepthFunction.Less);
				gl.DepthMask(true);

				gl.Begin(BeginMode.Triangles);
				for (int row = 0; row < BarGridMath.Rows; row++)
				{
					for (int column = 0; column < BarGridMath.Columns; column++)
					{
						var (x, z) = BarGridMath.BarCenter(column, row);
						double barHeight = BarGridMath.Height(column, row, phase);
						var baseColor = BarGridMath.BaseColor(column, row, phase, palette);
						foreach (var (corners, normal) in Faces)
						{
							gl.Color4(BarGridMath.Lit(baseColor, BarGridMath.Lighting(normal)).ToColor());
							EmitCorner(gl, corners[0], x, z, barHeight);
							EmitCorner(gl, corners[1], x, z, barHeight);
							EmitCorner(gl, corners[2], x, z, barHeight);
							EmitCorner(gl, corners[0], x, z, barHeight);
							EmitCorner(gl, corners[2], x, z, barHeight);
							EmitCorner(gl, corners[3], x, z, barHeight);
						}
					}
				}

				gl.End();

				gl.Disable(EnableCap.DepthTest);
				gl.MatrixMode(MatrixMode.Projection);
				gl.PopMatrix();
				gl.MatrixMode(MatrixMode.Modelview);
				gl.PopMatrix();
			}

			// The 2D path expects blending on and no depth test.
			gl.Disable(EnableCap.DepthTest);
			gl.Enable(EnableCap.Blend);
			this.target.Composite(frame, 0, 0);
		}

		private static void EmitCorner(GL gl, double[] corner, double x, double z, double barHeight)
			=> gl.Vertex3(x + (corner[0] * BarGridMath.BarHalf), corner[1] * barHeight, z + (corner[2] * BarGridMath.BarHalf));
	}
}
