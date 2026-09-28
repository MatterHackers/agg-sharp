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
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's line_patterns_clip.cpp: a five-point polyline drawn twice, as an 8-wide anti-aliased outline and
	/// with picture 1 laid along it, clipped to a box 50 pixels in from the edges. The first pass is clipped only
	/// as vectors (to a box a little larger) and shows faded behind the second, clipped to the pixel. Drag the
	/// points; + and - zoom about the mouse; Scale X stretches the picture, Start X slides it.
	/// </summary>
	/// <remarks>
	/// The software mode is the C++ render, byte for byte, drawn straight into the BGRA frame as C++ draws through
	/// pixfmt_bgra32. The outline and image renderers write pixels themselves; any other surface - the GPU - takes
	/// those same pixels as rectangles through a <see cref="Graphics2DSpanImage"/>.
	/// </remarks>
	public class LinePatternsClipDemo : AggDemo
	{
		// C++ w2: how far the vector clip box reaches past the pixel clip, so clipped line caps still draw whole.
		private const double ClipDilation = 9.0;

		private const int ClipInset = 50;

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly BrightnessToAlphaSource pattern = BrightnessToAlphaSource.Load(1);

		// C++ m_ctrl_color: an srgba8 of rgba(0, 0.3, 0.5, 0.3), handed to ctrls that draw rgba8.
		private readonly Color ctrlColor = SrgbLut.FromRgbaThroughSrgba8(0, 0.3, 0.5, 0.3);

		// C++ on_key zooms about the mouse; the demo's key events carry no position, so it is the last one seen.
		private int mouseX;

		private int mouseY;

		public LinePatternsClipDemo()
		{
			this.Polyline.SetPoint(0, 20, 20);
			this.Polyline.SetPoint(1, 500 - 20, 500 - 20);
			this.Polyline.SetPoint(2, 500 - 60, 20);
			this.Polyline.SetPoint(3, 40, 500 - 40);
			this.Polyline.SetPoint(4, 100, 300);

			// line_patterns_clip.cpp runs with flip_y = true and gives its sliders !flip_y.
			this.ScaleXSlider = new SliderCtrl(5.0, 5.0, 240.0, 12.0, false) { Label = "Scale X={0:F2}" };
			this.ScaleXSlider.SetRange(0.2, 3.0);
			this.ScaleXSlider.Value = 1.0;

			this.StartXSlider = new SliderCtrl(250.0, 5.0, 495.0, 12.0, false) { Label = "Start X={0:F2}" };
			this.StartXSlider.SetRange(0.0, 10.0);
			this.StartXSlider.Value = 0.0;

			this.ctrls.Add(this.ScaleXSlider);
			this.ctrls.Add(this.StartXSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();
		}

		/// <summary>C++ <c>m_line1</c>: the polyline's five points, before <see cref="Zoom"/>.</summary>
		public InteractivePolygon Polyline { get; } = new InteractivePolygon(5, 5.0) { Close = false };

		/// <summary>C++ <c>m_scale_x</c>: how far the picture is stretched along the polyline, 0.2 to 3.</summary>
		public SliderCtrl ScaleXSlider { get; }

		/// <summary>C++ <c>m_start_x</c>: how far along the polyline the picture starts, 0 to 10 pixels.</summary>
		public SliderCtrl StartXSlider { get; }

		/// <summary>C++ <c>m_scale</c>: the view zoom the polyline is drawn and dragged through.</summary>
		public Affine Zoom { get; private set; } = Affine.NewIdentity();

		public override string Name => "line_patterns_clip";

		public override string Category => "Lines";

		public override string Description => "Clipping lines drawn with an image pattern: a polyline drawn as an outline and with a picture along it, clipped to a box. Drag the points; + and - zoom about the mouse; Scale X stretches the picture, Start X slides it.";

		public override int Width => 500;

		public override int Height => 500;

		/// <summary>C++ on_key's + and -: zoom in (by 1.1) or out about (<paramref name="x"/>, <paramref name="y"/>).</summary>
		public void ZoomAbout(double x, double y, bool zoomIn)
		{
			Affine zoom = this.Zoom;
			zoom *= Affine.NewTranslation(-x, -y);
			zoom *= Affine.NewScaling(zoomIn ? 1.1 : 1 / 1.1);
			zoom *= Affine.NewTranslation(x, y);
			this.Zoom = zoom;
			this.Invalidate();
		}

		public override void Draw(Graphics2D graphics)
		{
			if (graphics is ImageGraphics2D && graphics.DestImage is IImageByte destination)
			{
				this.DrawSoftware(graphics, destination);
			}
			else
			{
				this.DrawSpans(graphics);
			}

			// C++ sets these every frame, so the points and outline stay the same size on screen at any zoom.
			double scale = this.Zoom.GetScale();
			this.Polyline.LineWidth = 1 / scale;
			this.Polyline.PointRadius = 5 / scale;

			graphics.Render(new VertexSourceApplyTransform(this.Polyline, this.Zoom), this.ctrlColor);
			this.ctrls.Render(graphics);

			// The length of the first segment, in gsv_text stroked 1.5 wide with round caps.
			Vector2 p0 = this.Polyline.GetPoint(0);
			Vector2 p1 = this.Polyline.GetPoint(1);
			double length = (p1 - p0).Length * scale;
			var textPath = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; line_patterns_clip.cpp draws its length with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.size(10.0, 0.0);
			text.start_point(10.0, 30.0);
			text.text("Len=" + length.ToString("F2", CultureInfo.InvariantCulture));
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				textPath.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			graphics.Render(new Stroke(textPath, 1.5) { LineCap = LineCap.Round }, Color.Black);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.mouseX = x;
			this.mouseY = y;
			if (!button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			// C++ add_ctrl order: the polyline, then the sliders; the first to take the press has it.
			Vector2 point = this.Zoom.InverseTransform(new Vector2(x, y));
			if (this.Polyline.OnMouseButtonDown(point.X, point.Y))
			{
				this.Invalidate();
				return;
			}

			this.ctrls.OnMouseDown(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.mouseX = x;
			this.mouseY = y;
			Vector2 point = this.Zoom.InverseTransform(new Vector2(x, y));
			if (this.Polyline.OnMouseMove(point.X, point.Y))
			{
				this.Invalidate();
			}

			this.ctrls.OnMouseMove(x, y, flags);
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.Polyline.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}

			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			if (this.ctrls.OnKeyDown(key))
			{
				return;
			}

			if (key == Keys.Add || key == Keys.Oemplus)
			{
				this.ZoomAbout(this.mouseX, this.mouseY, true);
			}
			else if (key == Keys.Subtract || key == Keys.OemMinus)
			{
				this.ZoomAbout(this.mouseX, this.mouseY, false);
			}
		}

		// C++ draw_polyline's source: the points as poly_plain_adaptor gives them (open, so no end_poly) through m_scale.
		private IVertexSource ZoomedPolyline()
		{
			var path = new VertexStorage();
			for (int i = 0; i < this.Polyline.NumPoints; i++)
			{
				Vector2 point = this.Zoom.Transform(this.Polyline.GetPoint(i));
				if (i == 0)
				{
					path.MoveTo(point.X, point.Y);
				}
				else
				{
					path.LineTo(point.X, point.Y);
				}
			}

			return path;
		}

		private void DrawSoftware(Graphics2D graphics, IImageByte destination)
		{
			var baseRenderer = new ImageClippingProxy(destination);
			baseRenderer.clear(Rgba8.FromRgba(0.5, 0.75, 0.85));

			// C++ ras.clip_box(0, 0, width(), height()): the scanline rasterizer the ctrls and text go through.
			graphics.Rasterizer.SetVectorClipBox(0, 0, this.Width, this.Height);

			this.DrawPasses(baseRenderer, true);
		}

		/// <summary>
		/// Any other surface - the GPU - takes the renderers' pixels as rectangles through a <see cref="Graphics2DSpanImage"/>,
		/// which the graphics' own transform places.
		/// </summary>
		private void DrawSpans(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Rgba8.FromRgba(0.5, 0.75, 0.85));
			var spans = new Graphics2DSpanImage(graphics, this.Width, this.Height);
			this.DrawPasses(new ImageClippingProxy(spans), false);
			spans.Flush();
		}

		/// <summary>
		/// C++ on_draw's two passes over the cleared <paramref name="baseRenderer"/>. Without
		/// <paramref name="canCopy"/> (a span image only blends), the white bar is an opaque blend, the same pixels.
		/// </summary>
		private void DrawPasses(ImageClippingProxy baseRenderer, bool canCopy)
		{
			int width = this.Width;
			int height = this.Height;

			var pattern = new line_image_pattern(new pattern_filter_bilinear_RGBA_Bytes());
			pattern.create(this.pattern);
			var imageRenderer = new ImageLineRenderer(baseRenderer, pattern);
			var imageRasterizer = new rasterizer_outline_aa(imageRenderer);

			var profile = new LineProfileAnitAlias();
			profile.smoother_width(10.0);
			profile.width(8.0);
			var lineRenderer = new OutlineRenderer(baseRenderer, profile);
			lineRenderer.color(SrgbLut.FromSrgba8(0, 0, 127));
			var lineRasterizer = new rasterizer_outline_aa(lineRenderer);
			lineRasterizer.round_cap(true);

			double w2 = ClipDilation;
			imageRenderer.scale_x(this.ScaleXSlider.Value);
			imageRenderer.start_x(this.StartXSlider.Value);
			imageRenderer.clip_box(ClipInset - w2, ClipInset - w2, width - ClipInset + w2, height - ClipInset + w2);
			lineRenderer.clip_box(ClipInset - w2, ClipInset - w2, width - ClipInset + w2, height - ClipInset + w2);

			// The first pass, clipped only as vectors, to show the idea.
			IVertexSource polyline = this.ZoomedPolyline();
			lineRasterizer.add_path(polyline);
			imageRasterizer.add_path(polyline);

			// C++ blend_bar: fade it almost, not quite, away.
			var white = new Color(255, 255, 255, 255);
			for (int y = 0; y <= height; y++)
			{
				baseRenderer.blend_hline(0, y, width, white, 200);
			}

			// Then the pixel clip box, a white copy_bar inside it (for the demonstration) and the second pass, clipped
			// both ways: vectors to the larger box, so the caps draw whole, pixels to the exact one.
			baseRenderer.SetClippingBox(ClipInset, ClipInset, width - ClipInset, height - ClipInset);
			for (int y = 0; y <= height; y++)
			{
				if (canCopy)
				{
					// C++ copy_bar; copy_hline takes a length, where C++ renderer_base takes an end x.
					baseRenderer.copy_hline(0, y, width + 1, white);
				}
				else
				{
					baseRenderer.blend_hline(0, y, width, white, 255);
				}
			}

			// The first pass left the pattern where its last segment ended; start it over.
			imageRenderer.scale_x(this.ScaleXSlider.Value);
			imageRenderer.start_x(this.StartXSlider.Value);
			lineRasterizer.add_path(polyline);
			imageRasterizer.add_path(polyline);

			baseRenderer.reset_clipping(true);
		}
	}
}
