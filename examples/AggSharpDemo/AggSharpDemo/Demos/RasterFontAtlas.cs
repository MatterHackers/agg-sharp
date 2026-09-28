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

using System.Collections.Generic;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.RenderGl;
using MatterHackers.RenderGl.OpenGl;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// One embedded raster font's glyphs side by side in a texture, built once per font, for drawing raster text on
	/// the GPU as a textured quad per glyph.
	/// </summary>
	/// <remarks>
	/// The atlas is white where a font bit is set and transparent elsewhere; the quad's vertex color tints it. Row y
	/// of the atlas is glyph row y from the bottom (what glyph_raster_bin.span(y) returns), so an unflipped quad maps
	/// it straight on. The sampler is nearest and the quads land on whole demo pixels, so at scale 1 the pixels are
	/// the software path's; scaled, the glyphs magnify blockily as the bitmaps they are. The atlas is padded to powers
	/// of two so its texture coordinates hold on hardware that pads textures.
	/// </remarks>
	internal sealed class RasterFontAtlas
	{
		private static readonly Dictionary<string, RasterFontAtlas> Atlases = new Dictionary<string, RasterFontAtlas>();

		private readonly glyph_raster_bin glyph;
		private readonly ImageBuffer image;
		private readonly int startChar;
		private readonly int[] glyphX;

		private RasterFontAtlas(string name)
		{
			this.Name = name;
			byte[] font = EmbeddedRasterFonts.Get(name);
			this.glyph = new glyph_raster_bin(font);
			this.Height = font[0];
			this.startChar = font[2];
			int count = font[3];

			// A column of padding after each glyph keeps a neighbour out of reach of any filtering.
			this.glyphX = new int[count];
			int width = 0;
			for (int i = 0; i < count; i++)
			{
				this.glyphX[i] = width;
				this.glyph.prepare(out glyph_raster_bin.glyph_rect r, 0, 0, this.startChar + i, false);
				width += r.x2 - r.x1 + 2;
			}

			this.image = new ImageBuffer(MathHelper.FirstPowerTowGreaterThanOrEqualTo(width), MathHelper.FirstPowerTowGreaterThanOrEqualTo(this.Height));
			for (int i = 0; i < count; i++)
			{
				this.glyph.prepare(out glyph_raster_bin.glyph_rect r, 0, 0, this.startChar + i, false);
				int glyphWidth = r.x2 - r.x1 + 1;
				for (int row = 0; row < this.Height; row++)
				{
					byte[] covers = this.glyph.span(row);
					for (int x = 0; x < glyphWidth; x++)
					{
						if (covers[x] != 0)
						{
							this.image.SetPixel(this.glyphX[i] + x, row, Color.White);
						}
					}
				}
			}
		}

		public string Name { get; }

		/// <summary>The font's line height in pixels.</summary>
		public int Height { get; }

		/// <summary>The atlas for the embedded raster font <paramref name="name"/>, built on first use.</summary>
		public static RasterFontAtlas Get(string name)
		{
			if (!Atlases.TryGetValue(name, out RasterFontAtlas atlas))
			{
				atlas = new RasterFontAtlas(name);
				Atlases[name] = atlas;
			}

			return atlas;
		}

		/// <summary>
		/// <paramref name="text"/> in <paramref name="color"/> with its pen at (<paramref name="x"/>, <paramref name="y"/>),
		/// y up, placed exactly as renderer_raster_htext_solid places it.
		/// </summary>
		public void Draw(Graphics2DGpu gpu, double x, double y, string text, Color color)
		{
			Affine demoToFrame = gpu.GetTransform();
			GL gl = gpu.gl;
			var texture = ImageTexturePlugin.GetImageTexturePlugin(gl, this.image, false, false, true);
			gpu.PushOrthoProjection();
			gl.Disable(EnableCap.Lighting);
			gl.Enable(EnableCap.Texture2D);
			gl.Disable(EnableCap.DepthTest);
			gl.Enable(EnableCap.Blend);
			gl.BlendFunc(BlendingFactorSrc.SrcAlpha, BlendingFactorDest.OneMinusSrcAlpha);
			gl.Color4(color);
			gl.BindTexture(TextureTarget.Texture2D, texture.GLTextureHandle);
			gl.Begin(BeginMode.Triangles);
			foreach (char c in text)
			{
				this.glyph.prepare(out glyph_raster_bin.glyph_rect r, x, y, c, false);
				if (r.x2 >= r.x1)
				{
					int index = c - this.startChar;
					double u0 = (double)this.glyphX[index] / this.image.Width;
					double u1 = (double)(this.glyphX[index] + r.x2 - r.x1 + 1) / this.image.Width;
					double v1 = (double)this.Height / this.image.Height;

					void Corner(double cx, double cy, double u, double v)
					{
						demoToFrame.Transform(ref cx, ref cy);
						gl.TexCoord2(new Vector2(u, v));
						gl.Vertex2(new Vector2(cx, cy));
					}

					Corner(r.x1, r.y1, u0, 0);
					Corner(r.x2 + 1, r.y1, u1, 0);
					Corner(r.x2 + 1, r.y2 + 1, u1, v1);
					Corner(r.x1, r.y1, u0, 0);
					Corner(r.x2 + 1, r.y2 + 1, u1, v1);
					Corner(r.x1, r.y2 + 1, u0, v1);
				}

				x += r.dx;
				y += r.dy;
			}

			gl.End();
			gpu.PopOrthoProjection();
		}
	}
}
