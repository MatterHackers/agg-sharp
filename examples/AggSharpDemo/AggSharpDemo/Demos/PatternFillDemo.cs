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
using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's pattern_fill.cpp: a 14-point star filled with a small generated tile (a smoothed six-point star on
	/// a translucent background), tiled by mirroring it at every edge (span_pattern_rgba over image_accessor_wrap
	/// with wrap_mode_reflect_auto_pow2). Drag the star; turn it or the tile's star on idle.
	/// </summary>
	public class PatternFillDemo : AggDemo
	{
		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		// C++ m_pattern: the tile, premultiplied, rebuilt whenever a ctrl changes.
		private ImageBuffer pattern;

		// The GPU's straight-alpha copy of the tile, kept with it so its texture is uploaded once per tile.
		private ImageBuffer gpuTile;

		private bool dragging;

		private double dragDx;

		private double dragDy;

		public PatternFillDemo()
		{
			// on_init: the star starts in the middle of the window.
			this.PolygonCenterX = this.Width / 2.0;
			this.PolygonCenterY = this.Height / 2.0;

			// pattern_fill.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.PolygonAngleSlider = new SliderCtrl(5, 5, 145, 12, false) { Label = "Polygon Angle={0:F2}" };
			this.PolygonAngleSlider.SetRange(-180.0, 180.0);
			this.PolygonAngleSlider.Value = 0.0;
			this.PolygonScaleSlider = new SliderCtrl(5, 5 + 14, 145, 12 + 14, false) { Label = "Polygon Scale={0:F2}" };
			this.PolygonScaleSlider.SetRange(0.1, 5.0);
			this.PolygonScaleSlider.Value = 1.0;
			this.PatternAngleSlider = new SliderCtrl(155, 5, 300, 12, false) { Label = "Pattern Angle={0:F2}" };
			this.PatternAngleSlider.SetRange(-180.0, 180.0);
			this.PatternAngleSlider.Value = 0.0;
			this.PatternSizeSlider = new SliderCtrl(155, 5 + 14, 300, 12 + 14, false) { Label = "Pattern Size={0:F2}" };
			this.PatternSizeSlider.SetRange(10, 40);
			this.PatternSizeSlider.Value = 30;
			this.PatternAlphaSlider = new SliderCtrl(310, 5, 460, 12, false) { Label = "Background Alpha={0:F2}" };
			this.PatternAlphaSlider.Value = 0.1;
			this.RotatePolygonCbox = new CboxCtrl(5, 5 + 14 + 14, "Rotate Polygon", false);
			this.RotatePatternCbox = new CboxCtrl(5, 5 + 14 + 14 + 14, "Rotate Pattern", false);
			this.TiePatternCbox = new CboxCtrl(155, 5 + 14 + 14, "Tie pattern to polygon", false);

			// C++ add_ctrl order, which is also the order they are drawn in.
			this.ctrls.Add(this.PolygonAngleSlider);
			this.ctrls.Add(this.PolygonScaleSlider);
			this.ctrls.Add(this.PatternAngleSlider);
			this.ctrls.Add(this.PatternSizeSlider);
			this.ctrls.Add(this.PatternAlphaSlider);
			this.ctrls.Add(this.RotatePolygonCbox);
			this.ctrls.Add(this.RotatePatternCbox);
			this.ctrls.Add(this.TiePatternCbox);
			this.ctrls.Changed += (s, e) =>
			{
				// on_ctrl_change: animate while either rotation is on, and rebuild the tile.
				this.WaitMode = !(this.RotatePolygonCbox.Checked || this.RotatePatternCbox.Checked);
				this.pattern = null;
				this.gpuTile = null;
				this.Invalidate();
			};
		}

		/// <summary>C++ <c>m_polygon_angle</c>, in degrees.</summary>
		public SliderCtrl PolygonAngleSlider { get; }

		/// <summary>C++ <c>m_polygon_scale</c>.</summary>
		public SliderCtrl PolygonScaleSlider { get; }

		/// <summary>C++ <c>m_pattern_angle</c>: the tile star's turn, in degrees.</summary>
		public SliderCtrl PatternAngleSlider { get; }

		/// <summary>C++ <c>m_pattern_size</c>: the tile's side in pixels (truncated to a whole pixel).</summary>
		public SliderCtrl PatternSizeSlider { get; }

		/// <summary>C++ <c>m_pattern_alpha</c>: the tile background's opacity.</summary>
		public SliderCtrl PatternAlphaSlider { get; }

		public CboxCtrl RotatePolygonCbox { get; }

		public CboxCtrl RotatePatternCbox { get; }

		/// <summary>C++ <c>m_tie_pattern</c>: offsets the tiling by the star's center, so the tiles move with it.</summary>
		public CboxCtrl TiePatternCbox { get; }

		public double PolygonCenterX { get; set; }

		public double PolygonCenterY { get; set; }

		public override string Name => "pattern_fill";

		public override string Category => "Images";

		public override string Description => "A star filled with a mirrored tile pattern. Drag the star; change or turn the star and the tile.";

		public override int Width => 640;

		public override int Height => 480;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);

			this.pattern ??= this.GeneratePattern();
			IVertexSource star = new VertexSourceApplyTransform(this.CreateStar(), this.PolygonMatrix());

			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				this.RenderPatternSpans(graphics, destination, star);
			}
			else
			{
				this.RenderPatternTiles(graphics, star);
			}

			this.ctrls.Render(graphics);
		}

		public override void OnIdle()
		{
			bool redraw = false;
			if (this.RotatePolygonCbox.Checked)
			{
				double angle = this.PolygonAngleSlider.Value + 0.5;
				this.PolygonAngleSlider.Value = angle >= 180.0 ? angle - 360.0 : angle;
				redraw = true;
			}

			if (this.RotatePatternCbox.Checked)
			{
				double angle = this.PatternAngleSlider.Value - 0.5;
				this.PatternAngleSlider.Value = angle <= -180.0 ? angle + 360.0 : angle;
				this.pattern = null;
				this.gpuTile = null;
				redraw = true;
			}

			if (redraw)
			{
				this.Invalidate();
			}
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (InsidePolygon(new VertexSourceApplyTransform(this.CreateStar(), this.PolygonMatrix()), x, y))
			{
				this.dragDx = x - this.PolygonCenterX;
				this.dragDy = y - this.PolygonCenterY;
				this.dragging = true;
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (!flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.dragging = false;
			}
			else if (this.dragging)
			{
				this.PolygonCenterX = x - this.dragDx;
				this.PolygonCenterY = y - this.dragDy;
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			this.dragging = false;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		// C++ create_star: n points alternating between r2 (the even ones, the first pointing down in y-up) and
		// r1, turned by startAngle degrees.
		private static VertexStorage CreateStar(double xc, double yc, double r1, double r2, int n, double startAngle = 0.0)
		{
			var star = new VertexStorage();
			startAngle *= Math.PI / 180.0;
			for (int i = 0; i < n; i++)
			{
				double a = (Math.PI * 2.0 * i / n) - (Math.PI / 2.0);
				double dx = Math.Cos(a + startAngle);
				double dy = Math.Sin(a + startAngle);
				double radius = (i & 1) != 0 ? r1 : r2;
				if (i == 0)
				{
					star.MoveTo(xc + (dx * radius), yc + (dy * radius));
				}
				else
				{
					star.LineTo(xc + (dx * radius), yc + (dy * radius));
				}
			}

			star.ClosePolygon();
			return star;
		}

		// A crossing test on the outline in place of C++'s rasterizer hit_test (private in agg-sharp): the star
		// is a simple polygon, so both give the same answer.
		private static bool InsidePolygon(IVertexSource path, double x, double y)
		{
			var points = new List<Vector2>();
			foreach (var vertex in path.Vertices())
			{
				if (vertex.IsMoveTo || vertex.IsLineTo)
				{
					points.Add(vertex.Position);
				}
			}

			bool inside = false;
			for (int i = 0, j = points.Count - 1; i < points.Count; j = i++)
			{
				Vector2 a = points[i];
				Vector2 b = points[j];
				if ((a.Y > y) != (b.Y > y) && x < ((b.X - a.X) * (y - a.Y) / (b.Y - a.Y)) + a.X)
				{
					inside = !inside;
				}
			}

			return inside;
		}

		// The 14-point star; C++ sizes it from initial_width().
		private VertexStorage CreateStar()
		{
			double r = (this.Width / 3.0) - 8.0;
			return CreateStar(this.PolygonCenterX, this.PolygonCenterY, r, r / 1.45, 14);
		}

		private Affine PolygonMatrix()
		{
			Affine m = Affine.NewTranslation(-this.PolygonCenterX, -this.PolygonCenterY);
			m *= Affine.NewRotation(this.PolygonAngleSlider.Value * Math.PI / 180.0);
			m *= Affine.NewScaling(this.PolygonScaleSlider.Value);
			m *= Affine.NewTranslation(this.PolygonCenterX, this.PolygonCenterY);
			return m;
		}

		// C++ generate_pattern: a six-point star, smoothed, filled and outlined on a premultiplied background.
		private ImageBuffer GeneratePattern()
		{
			double sizeValue = this.PatternSizeSlider.Value;
			int size = (int)sizeValue;
			var tile = new ImageBuffer(size, size);

			// rb.clear(rgba_pre(0.4, 0, 0.1, alpha)): the premultiplied colour, each channel rounded to a byte as
			// C++'s rgba8(rgba) does, written straight in.
			double alpha = this.PatternAlphaSlider.Value;
			byte[] pixels = tile.GetBuffer();
			for (int i = 0; i < size * size; i++)
			{
				int at = i * 4;
				pixels[at + ImageBuffer.OrderR] = (byte)(int)((0.4 * alpha * 255) + 0.5);
				pixels[at + ImageBuffer.OrderG] = 0;
				pixels[at + ImageBuffer.OrderB] = (byte)(int)((0.1 * alpha * 255) + 0.5);
				pixels[at + ImageBuffer.OrderA] = (byte)(int)((alpha * 255) + 0.5);
			}

			VertexStorage star = CreateStar(sizeValue / 2.0, sizeValue / 2.0, sizeValue / 2.5, sizeValue / 6.0, 6, this.PatternAngleSlider.Value);
			var smooth = new SmoothPolygonCurve(star)
			{
				SmoothValue = 1.0,
				ResolutionScale = 4.0,
			};

			Graphics2D graphics = tile.NewGraphics2D();
			graphics.Render(smooth, SrgbLut.FromSrgba8(110, 130, 50, 255));
			graphics.Render(new Stroke(smooth, sizeValue / 15.0), SrgbLut.FromSrgba8(0, 50, 80, 255));
			return tile;
		}

		// C++ tiles the plane with the pattern from window pixel (0, 0), or from the star's center mirrored
		// through the window's top right when tied.
		private (int X, int Y) PatternOffset()
		{
			if (!this.TiePatternCbox.Checked)
			{
				return (0, 0);
			}

			return ((int)(uint)(this.Width - this.PolygonCenterX), (int)(uint)(this.Height - this.PolygonCenterY));
		}

		// The software reference: the star's spans copied from the mirrored tiles, through the premultiplied
		// blender C++'s rb_pre uses. The tiling is in frame pixels, as C++'s is in window pixels.
		private void RenderPatternSpans(Graphics2D graphics, IImageByte destination, IVertexSource star)
		{
			var accessor = new ImageBufferAccessorWrap(this.pattern, new WrapModeReflectAutoPow2(this.pattern.Width), new WrapModeReflectAutoPow2(this.pattern.Height));
			(int offsetX, int offsetY) = this.PatternOffset();
			var spanGenerator = new span_pattern_rgba(accessor, offsetX, offsetY)
			{
				Alpha = (byte)(this.PatternAlphaSlider.Value * 255.0),
			};

			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			rasterizer.reset();
			rasterizer.add_path(new VertexSourceApplyTransform(star, graphics.GetTransform()));

			IRecieveBlenderByte blender = destination.GetRecieveBlender();
			destination.SetRecieveBlender(new BlenderPreMultBGRA());
			try
			{
				new ScanlineRenderer().GenerateAndRender(rasterizer, new scanline_unpacked_8(), destination, new span_allocator(), spanGenerator);
			}
			finally
			{
				destination.SetRecieveBlender(blender);
			}

			destination.MarkImageChanged();
		}

		// A GPU surface has no span generator: it fills the star with the tile as a mirror-repeated texture. The texture
		// path reads texels as straight alpha, so it is drawn from an un-premultiplied copy of the tile. Pixel x reads
		// the tile at x + offset, as span_pattern_rgba does.
		private void RenderPatternTiles(Graphics2D graphics, IVertexSource star)
		{
			if (graphics is not IPatternFillGraphics patternFill)
			{
				return;
			}

			ImageBuffer tile = this.gpuTile ??= StraightAlpha(this.pattern);
			(int offsetX, int offsetY) = this.PatternOffset();
			patternFill.FillPathWithImage(star, tile, Affine.NewTranslation(-offsetX, -offsetY), ImageWrapMode.Reflect, ImageWrapMode.Reflect);
		}

		private static ImageBuffer StraightAlpha(ImageBuffer premultiplied)
		{
			var straight = new ImageBuffer(premultiplied);
			byte[] pixels = straight.GetBuffer();
			for (int at = 0; at + 3 < pixels.Length; at += 4)
			{
				int alpha = pixels[at + ImageBuffer.OrderA];
				if (alpha != 0 && alpha != 255)
				{
					pixels[at + ImageBuffer.OrderR] = (byte)Math.Min(255, ((pixels[at + ImageBuffer.OrderR] * 255) + (alpha / 2)) / alpha);
					pixels[at + ImageBuffer.OrderG] = (byte)Math.Min(255, ((pixels[at + ImageBuffer.OrderG] * 255) + (alpha / 2)) / alpha);
					pixels[at + ImageBuffer.OrderB] = (byte)Math.Min(255, ((pixels[at + ImageBuffer.OrderB] * 255) + (alpha / 2)) / alpha);
				}
			}

			return straight;
		}
	}
}
