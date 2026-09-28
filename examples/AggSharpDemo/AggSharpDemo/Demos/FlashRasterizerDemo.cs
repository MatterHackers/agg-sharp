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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's flash_rasterizer.cpp: a Flash-style shape from shapes.txt, whose paths each carry a fill
	/// style on either side, filled in one pass by the compound rasterizer, then outlined. Space shows the next
	/// shape; +/- zoom and the arrow keys rotate about the mouse pointer.
	/// </summary>
	/// <remarks>
	/// Left out: the "Fill=..ms" timing line (it differs every frame; the help text keeps its place), dragging a
	/// vertex with the left button and the right-button hit test that hides the outlines. The GPU takes the
	/// compound rasterizer's exact pixels through a <see cref="Graphics2DSpanImage"/>; the outlines and help are
	/// vectors there.
	/// </remarks>
	public class FlashRasterizerDemo : AggDemo
	{
		/// <summary>The shape read from shapes.txt.</summary>
		protected readonly FlashShape shape = new FlashShape();

		/// <summary>The palette, straight alpha.</summary>
		protected readonly Color[] colors = FlashDrawing.Palette();

		private int mouseX;

		private int mouseY;

		public FlashRasterizerDemo()
		{
			// agg_main: read_next() then scale() to the window.
			this.NextShape();
		}

		/// <summary>C++ <c>m_scale</c>: the zoom and rotation the keys build up, about the mouse pointer.</summary>
		public Affine View { get; set; } = Affine.NewIdentity();

		/// <summary>The shape read from shapes.txt.</summary>
		public FlashShape Shape => this.shape;

		public override string Name => "flash_rasterizer";

		public override string Category => "Vector Graphics";

		public override string Description => "A Flash shape filled in one pass by the compound rasterizer. Space: next shape; +/-: zoom; arrows: rotate.";

		public override int Width => 655;

		public override int Height => 520;

		public override bool DrawsYDown => true;

		/// <summary>The Space key: the next shape in shapes.txt, fitted to the window.</summary>
		public void NextShape()
		{
			this.shape.ReadNext();
			this.shape.Scale(this.Width, this.Height);
		}

		public override void Draw(Graphics2D graphics)
		{
			this.shape.ApproximationScale(this.View.GetScale());

			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				ImageClippingProxy target = FlashDrawing.PremultipliedTarget(destination);

				this.FillStyles(target);

				FlashDrawing.RenderStrokesAndHelp(this.shape, this.View, this.Width, this.Height, target);
				destination.MarkImageChanged();
			}
			else
			{
				this.RenderOnGpu(graphics);
			}
		}

		/// <summary>
		/// The frame on a surface without a rasterizer (the GPU): the software fill's pixels through a
		/// <see cref="Graphics2DSpanImage"/>, then the outlines and help as vectors.
		/// </summary>
		protected virtual void RenderOnGpu(Graphics2D graphics)
		{
			graphics.Clear(new Color(255, 255, 242));
			var spans = new Graphics2DSpanImage(graphics, this.Width, this.Height);

			// The mix where styles meet is premultiplied, and the span image blends straight colors, so every
			// style goes through color spans, which PremultipliedColorSpans hands on straight.
			this.FillCompound(new PremultipliedColorSpans(new ImageClippingProxy(spans)), colorSpans: true);
			spans.Flush();
			FlashDrawing.RenderStrokesAndHelpOnGpu(graphics, this.shape, this.View);
		}

		/// <summary>The software fill of every style: here, in one pass by the compound rasterizer.</summary>
		protected virtual void FillStyles(ImageClippingProxy target)
		{
			this.FillCompound(target, colorSpans: false);
		}

		/// <summary>
		/// The compound fill into <paramref name="target"/>, which blends premultiplied colors; with
		/// <paramref name="colorSpans"/> every style is a color span rather than a solid color.
		/// </summary>
		private void FillCompound(IImageByte target, bool colorSpans)
		{
			// C++ rasterizer_compound_aa<rasterizer_sl_clip_dbl>, clipped to the window.
			var rasc = new rasterizer_compound_aa(new VectorClipperDouble());
			rasc.clip_box(0, 0, this.Width, this.Height);
			rasc.reset();
			for (int i = 0; i < this.shape.Paths; i++)
			{
				var style = this.shape.Style(i);
				if (style.Left >= 0 || style.Right >= 0)
				{
					rasc.styles(style.Left, style.Right);
					rasc.add_path(this.shape.Path(i, this.View));
				}
			}

			var premultiplied = new Color[this.colors.Length];
			for (int i = 0; i < this.colors.Length; i++)
			{
				premultiplied[i] = FlashDrawing.Premultiply(this.colors[i]);
			}

			new ScanlineRenderer().RenderCompound(rasc, new scanline_unpacked_8(), new scanline_bin(), target, new span_allocator(), new SolidStyles(premultiplied, colorSpans));
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.mouseX = x;
			this.mouseY = y;
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			if (key == Keys.Space)
			{
				this.NextShape();
				this.Invalidate();
			}
			else if (FlashDrawing.ApplyViewKey(key, this.mouseX, this.mouseY, this.View) is Affine view)
			{
				this.View = view;
				this.Invalidate();
			}
		}

		/// <summary>
		/// The example's test_styles as it ships: every style solid, its color the palette's. With
		/// <c>asColorSpans</c> each style is the same color as a generated span instead, the same pixels through
		/// blend_color_hspan.
		/// </summary>
		private sealed class SolidStyles : IStyleHandler
		{
			private readonly Color[] colors;

			private readonly bool asColorSpans;

			public SolidStyles(Color[] colors, bool asColorSpans)
			{
				this.colors = colors;
				this.asColorSpans = asColorSpans;
			}

			public Color color(int style) => this.colors[style];

			public void GenerateSpan(Color[] span, int spanIndex, int x, int y, int len, int style)
			{
				Array.Fill(span, this.colors[style], spanIndex, len);
			}

			public bool IsSolid(int style) => !this.asColorSpans;
		}
	}
}
