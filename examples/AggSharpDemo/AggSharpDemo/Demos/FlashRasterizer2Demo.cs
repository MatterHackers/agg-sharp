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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's flash_rasterizer2.cpp: flash_rasterizer's shapes without the compound rasterizer. Each style is
	/// filled on its own by a plain scanline rasterizer with auto_close off - the paths with the style on their
	/// left as they are, those with it on their right inverted - so where two styles meet both anti-aliased
	/// edges are drawn and the background shows faintly through. Keys as flash_rasterizer. The GPU draws the
	/// same fills' pixels through a <see cref="Graphics2DSpanImage"/>.
	/// </summary>
	public class FlashRasterizer2Demo : FlashRasterizerDemo
	{
		public override string Name => "flash_rasterizer2";

		public override string Description => "A Flash shape filled one style at a time by the plain scanline rasterizer. Space: next shape; +/-: zoom; arrows: rotate.";

		/// <summary>C++ on_draw's fill loop, through rasterizer_scanline_aa&lt;rasterizer_sl_clip_dbl&gt; clipped to
		/// the window, each style in its premultiplied color.</summary>
		protected override void FillStyles(ImageClippingProxy target)
		{
			this.FillStyles(target, FlashDrawing.Premultiply);
		}

		/// <summary>
		/// The same fill loop's exact pixels through a <see cref="Graphics2DSpanImage"/>, which blends straight
		/// colors (source over), so each style keeps its straight palette color; the outlines and help are vectors.
		/// </summary>
		protected override void RenderOnGpu(Graphics2D graphics)
		{
			graphics.Clear(new Color(255, 255, 242));
			var spans = new Graphics2DSpanImage(graphics, this.Width, this.Height);
			this.FillStyles(new ImageClippingProxy(spans), color => color);
			spans.Flush();
			FlashDrawing.RenderStrokesAndHelpOnGpu(graphics, this.shape, this.View);
		}

		private void FillStyles(IImageByte target, Func<Color, Color> styleColor)
		{
			var ras = new ScanlineRasterizer(new VectorClipperDouble());
			var scanline = new scanline_unpacked_8();
			var renderer = new ScanlineRenderer();
			ras.SetVectorClipBox(0, 0, this.Width, this.Height);

			// A path is one edge of a region, not a closed contour: the paths of one style close each other.
			ras.auto_close(false);
			for (int s = this.shape.MinStyle; s <= this.shape.MaxStyle; s++)
			{
				ras.reset();
				for (int i = 0; i < this.shape.Paths; i++)
				{
					var style = this.shape.Style(i);
					if (style.Left != style.Right)
					{
						if (style.Left == s)
						{
							ras.add_path(this.shape.Path(i, this.View));
						}

						if (style.Right == s)
						{
							ras.add_path(Inverted(this.shape.Path(i, this.View)));
						}
					}
				}

				renderer.RenderSolid(target, ras, scanline, styleColor(this.colors[s]));
			}
		}

		/// <summary>C++ <c>tmp_path.concat_path(shape, path_id); tmp_path.invert_polygon(0)</c>: the path reversed.</summary>
		private static VertexStorage Inverted(IVertexSource path)
		{
			var inverted = new VertexStorage();
			foreach (VertexData vertex in path.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				inverted.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			inverted.InvertPolygon(0);
			return inverted;
		}
	}
}
