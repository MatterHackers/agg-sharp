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
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's blend_color.cpp: only the shadow of the glyph "a" - mapped by a perspective transform onto a
	/// quadrilateral you drag, rendered as gray coverage, stack-blurred, and then coloured either with one colour
	/// (the gray is its cover) or through a 256-entry colour table (the gray picks the colour).
	/// </summary>
	/// <remarks>
	/// The blur reads back coverage already rendered, which the GPU path cannot do mid-frame, so there the shadow is
	/// drawn through <see cref="IBlurGraphics"/>, its blurred alpha coloured by the same table. A surface without it
	/// draws the shadow sharp. The "%3.2f ms" timer text is left out, as in the other ports.
	/// </remarks>
	public class BlendColorDemo : AggDemo
	{
		// blend_color.cpp g_gradient_colors: an sRGB r, g, b, a for each gray value (the a is not used).
		private static readonly byte[] GradientColors =
		{
			255, 255, 255, 255,
			255, 255, 254, 255,
			255, 255, 254, 255,
			255, 255, 254, 255,
			255, 255, 253, 255,
			255, 255, 253, 255,
			255, 255, 252, 255,
			255, 255, 251, 255,
			255, 255, 250, 255,
			255, 255, 248, 255,
			255, 255, 246, 255,
			255, 255, 244, 255,
			255, 255, 241, 255,
			255, 255, 238, 255,
			255, 255, 235, 255,
			255, 255, 231, 255,
			255, 255, 227, 255,
			255, 255, 222, 255,
			255, 255, 217, 255,
			255, 255, 211, 255,
			255, 255, 206, 255,
			255, 255, 200, 255,
			255, 254, 194, 255,
			255, 253, 188, 255,
			255, 252, 182, 255,
			255, 250, 176, 255,
			255, 249, 170, 255,
			255, 247, 164, 255,
			255, 246, 158, 255,
			255, 244, 152, 255,
			254, 242, 146, 255,
			254, 240, 141, 255,
			254, 238, 136, 255,
			254, 236, 131, 255,
			253, 234, 126, 255,
			253, 232, 121, 255,
			253, 229, 116, 255,
			252, 227, 112, 255,
			252, 224, 108, 255,
			251, 222, 104, 255,
			251, 219, 100, 255,
			251, 216,  96, 255,
			250, 214,  93, 255,
			250, 211,  89, 255,
			249, 208,  86, 255,
			249, 205,  83, 255,
			248, 202,  80, 255,
			247, 199,  77, 255,
			247, 196,  74, 255,
			246, 193,  72, 255,
			246, 190,  69, 255,
			245, 187,  67, 255,
			244, 183,  64, 255,
			244, 180,  62, 255,
			243, 177,  60, 255,
			242, 174,  58, 255,
			242, 170,  56, 255,
			241, 167,  54, 255,
			240, 164,  52, 255,
			239, 161,  51, 255,
			239, 157,  49, 255,
			238, 154,  47, 255,
			237, 151,  46, 255,
			236, 147,  44, 255,
			235, 144,  43, 255,
			235, 141,  41, 255,
			234, 138,  40, 255,
			233, 134,  39, 255,
			232, 131,  37, 255,
			231, 128,  36, 255,
			230, 125,  35, 255,
			229, 122,  34, 255,
			228, 119,  33, 255,
			227, 116,  31, 255,
			226, 113,  30, 255,
			225, 110,  29, 255,
			224, 107,  28, 255,
			223, 104,  27, 255,
			222, 101,  26, 255,
			221,  99,  25, 255,
			220,  96,  24, 255,
			219,  93,  23, 255,
			218,  91,  22, 255,
			217,  88,  21, 255,
			216,  86,  20, 255,
			215,  83,  19, 255,
			214,  81,  18, 255,
			213,  79,  17, 255,
			212,  77,  17, 255,
			211,  74,  16, 255,
			210,  72,  15, 255,
			209,  70,  14, 255,
			207,  68,  13, 255,
			206,  66,  13, 255,
			205,  64,  12, 255,
			204,  62,  11, 255,
			203,  60,  10, 255,
			202,  58,  10, 255,
			201,  56,   9, 255,
			199,  55,   9, 255,
			198,  53,   8, 255,
			197,  51,   7, 255,
			196,  50,   7, 255,
			195,  48,   6, 255,
			193,  46,   6, 255,
			192,  45,   5, 255,
			191,  43,   5, 255,
			190,  42,   4, 255,
			188,  41,   4, 255,
			187,  39,   3, 255,
			186,  38,   3, 255,
			185,  37,   2, 255,
			183,  35,   2, 255,
			182,  34,   1, 255,
			181,  33,   1, 255,
			179,  32,   1, 255,
			178,  30,   0, 255,
			177,  29,   0, 255,
			175,  28,   0, 255,
			174,  27,   0, 255,
			173,  26,   0, 255,
			171,  25,   0, 255,
			170,  24,   0, 255,
			168,  23,   0, 255,
			167,  22,   0, 255,
			165,  21,   0, 255,
			164,  21,   0, 255,
			163,  20,   0, 255,
			161,  19,   0, 255,
			160,  18,   0, 255,
			158,  17,   0, 255,
			156,  17,   0, 255,
			155,  16,   0, 255,
			153,  15,   0, 255,
			152,  14,   0, 255,
			150,  14,   0, 255,
			149,  13,   0, 255,
			147,  12,   0, 255,
			145,  12,   0, 255,
			144,  11,   0, 255,
			142,  11,   0, 255,
			140,  10,   0, 255,
			139,  10,   0, 255,
			137,   9,   0, 255,
			135,   9,   0, 255,
			134,   8,   0, 255,
			132,   8,   0, 255,
			130,   7,   0, 255,
			128,   7,   0, 255,
			126,   6,   0, 255,
			125,   6,   0, 255,
			123,   5,   0, 255,
			121,   5,   0, 255,
			119,   4,   0, 255,
			117,   4,   0, 255,
			115,   4,   0, 255,
			113,   3,   0, 255,
			111,   3,   0, 255,
			109,   2,   0, 255,
			107,   2,   0, 255,
			105,   2,   0, 255,
			103,   1,   0, 255,
			101,   1,   0, 255,
			 99,   1,   0, 255,
			 97,   0,   0, 255,
			 95,   0,   0, 255,
			 93,   0,   0, 255,
			 91,   0,   0, 255,
			 90,   0,   0, 255,
			 88,   0,   0, 255,
			 86,   0,   0, 255,
			 84,   0,   0, 255,
			 82,   0,   0, 255,
			 80,   0,   0, 255,
			 78,   0,   0, 255,
			 77,   0,   0, 255,
			 75,   0,   0, 255,
			 73,   0,   0, 255,
			 72,   0,   0, 255,
			 70,   0,   0, 255,
			 68,   0,   0, 255,
			 67,   0,   0, 255,
			 65,   0,   0, 255,
			 64,   0,   0, 255,
			 63,   0,   0, 255,
			 61,   0,   0, 255,
			 60,   0,   0, 255,
			 59,   0,   0, 255,
			 58,   0,   0, 255,
			 57,   0,   0, 255,
			 56,   0,   0, 255,
			 55,   0,   0, 255,
			 54,   0,   0, 255,
			 53,   0,   0, 255,
			 53,   0,   0, 255,
			 52,   0,   0, 255,
			 52,   0,   0, 255,
			 51,   0,   0, 255,
			 51,   0,   0, 255,
			 51,   0,   0, 255,
			 50,   0,   0, 255,
			 50,   0,   0, 255,
			 51,   0,   0, 255,
			 51,   0,   0, 255,
			 51,   0,   0, 255,
			 51,   0,   0, 255,
			 52,   0,   0, 255,
			 52,   0,   0, 255,
			 53,   0,   0, 255,
			 54,   1,   0, 255,
			 55,   2,   0, 255,
			 56,   3,   0, 255,
			 57,   4,   0, 255,
			 58,   5,   0, 255,
			 59,   6,   0, 255,
			 60,   7,   0, 255,
			 62,   8,   0, 255,
			 63,   9,   0, 255,
			 64,  11,   0, 255,
			 66,  12,   0, 255,
			 68,  13,   0, 255,
			 69,  14,   0, 255,
			 71,  16,   0, 255,
			 73,  17,   0, 255,
			 75,  18,   0, 255,
			 77,  20,   0, 255,
			 79,  21,   0, 255,
			 81,  23,   0, 255,
			 83,  24,   0, 255,
			 85,  26,   0, 255,
			 87,  28,   0, 255,
			 90,  29,   0, 255,
			 92,  31,   0, 255,
			 94,  33,   0, 255,
			 97,  34,   0, 255,
			 99,  36,   0, 255,
			102,  38,   0, 255,
			104,  40,   0, 255,
			107,  41,   0, 255,
			109,  43,   0, 255,
			112,  45,   0, 255,
			115,  47,   0, 255,
			117,  49,   0, 255,
			120,  51,   0, 255,
			123,  52,   0, 255,
			126,  54,   0, 255,
			128,  56,   0, 255,
			131,  58,   0, 255,
			134,  60,   0, 255,
			137,  62,   0, 255,
			140,  64,   0, 255,
			143,  66,   0, 255,
			145,  68,   0, 255,
			148,  70,   0, 255,
			151,  72,   0, 255,
			154,  74,   0, 255
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly VertexStorage path = GlyphA.Create();

		private readonly RectangleDouble shapeBounds;

		// C++ m_color_lut: the table's srgba8 colours as the linear rgba8 they convert to in the example's
		// pixfmt_bgr24, opaque above 63 and fading in (alpha i * 4) below.
		private readonly Color[] colorLut = new Color[256];

		public BlendColorDemo()
		{
			// blend_color.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.MethodRbox = new RboxCtrl(10.0, 10.0, 130.0, 55.0, false);
			this.MethodRbox.SetTextSize(8);
			this.MethodRbox.AddItem("Single Color");
			this.MethodRbox.AddItem("Color LUT");
			this.MethodRbox.CurrentItem = 1;

			this.RadiusSlider = new SliderCtrl(130 + 10.0, 10.0 + 4.0, 130 + 300.0, 10.0 + 8.0 + 4.0, false) { Label = "Blur Radius={0:F2}" };
			this.RadiusSlider.SetRange(0.0, 40.0);
			this.RadiusSlider.Value = 15.0;

			this.ctrls.Add(this.MethodRbox);
			this.ctrls.Add(this.RadiusSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			this.path.TransformAllPaths(Affine.NewScaling(4.0) * Affine.NewTranslation(150, 100));

			// C++ bounding_rect_single over conv_curve: the bounds of the flattened glyph.
			this.shapeBounds = this.Shape.GetBounds();
			this.ShadowQuad.SetPoint(0, this.shapeBounds.Left, this.shapeBounds.Bottom);
			this.ShadowQuad.SetPoint(1, this.shapeBounds.Right, this.shapeBounds.Bottom);
			this.ShadowQuad.SetPoint(2, this.shapeBounds.Right, this.shapeBounds.Top);
			this.ShadowQuad.SetPoint(3, this.shapeBounds.Left, this.shapeBounds.Top);

			for (int i = 0; i < 256; i++)
			{
				this.colorLut[i] = SrgbLut.FromSrgba8(GradientColors[i * 4], GradientColors[(i * 4) + 1], GradientColors[(i * 4) + 2], i > 63 ? 255 : i * 4);
			}
		}

		/// <summary>C++ <c>m_method</c>: 0 colours the shadow with one colour, 1 through the colour table.</summary>
		public RboxCtrl MethodRbox { get; }

		/// <summary>C++ <c>m_radius</c>: the blur radius, 0 to 40.</summary>
		public SliderCtrl RadiusSlider { get; }

		/// <summary>C++ <c>m_shadow_ctrl</c>: the quadrilateral the glyph's bounds map onto to make its shadow.</summary>
		public InteractivePolygon ShadowQuad { get; } = new InteractivePolygon(4, 5.0);

		public override string Name => "blend_color";

		public override string Category => "Images";

		public override string Description => "A blurred shadow of the glyph \"a\" coloured by one colour or a colour table. Drag the shadow's corners and change the radius.";

		public override int Width => 440;

		public override int Height => 330;

		// C++ m_shape: conv_curve over the path.
		private IVertexSource Shape => new FlattenCurves(this.path);

		// C++ blend_from_color's colour, srgba8(0, 100, 0), as the linear rgba8 it converts to.
		private static Color SingleColor => SrgbLut.FromSrgba8(0, 100, 0);

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Rgba8.FromRgba(1, 0.95, 0.95));

			var quad = new double[8];
			for (int i = 0; i < 4; i++)
			{
				quad[i * 2] = this.ShadowQuad.GetPoint(i).X;
				quad[(i * 2) + 1] = this.ShadowQuad.GetPoint(i).Y;
			}

			var shadowPerspective = new Perspective(this.shapeBounds.Left, this.shapeBounds.Bottom, this.shapeBounds.Right, this.shapeBounds.Top, quad);
			var shadow = new VertexSourceApplyTransform(this.Shape, shadowPerspective);

			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				// C++ m_ras.clip_box(0, 0, width(), height()): the one rasterizer the shadow and the ctrls go through.
				graphics.Rasterizer.SetVectorClipBox(0, 0, this.Width, this.Height);
				this.DrawBlurredShadow(destination, shadow);
			}
			else if (graphics is IBlurGraphics blurGraphics)
			{
				// The shadow's coverage stack blurred on the GPU. The table colours it by the blurred alpha as
				// BlendFromLut colours the gray; the single colour needs no table, the blurred cover scales its alpha.
				bool single = this.MethodRbox.CurrentItem == 0;
				blurGraphics.DrawBlurred(this.RadiusSlider.Value, () => graphics.Render(shadow, single ? SingleColor : Color.White), single ? null : this.colorLut);
			}
			else
			{
				graphics.Render(shadow, this.MethodRbox.CurrentItem == 0 ? SingleColor : this.colorLut[255]);
			}

			this.ctrls.Render(graphics);
			graphics.Render(this.ShadowQuad, Rgba8.FromRgba(0, 0.3, 0.5, 0.3));
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button) || !button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			if (this.ShadowQuad.OnMouseButtonDown(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseMove(x, y, flags))
			{
				return;
			}

			if (flags.HasFlag(AggInputFlags.MouseLeft))
			{
				if (this.ShadowQuad.OnMouseMove(x, y))
				{
					this.Invalidate();
				}
			}
			else if (this.ShadowQuad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
			if (this.ShadowQuad.OnMouseButtonUp(x, y))
			{
				this.Invalidate();
			}
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		// The shadow as gray8 coverage the size of the window, stack-blurred inside its bounds grown by the radius
		// (clipped to the window), then that area blended onto the frame.
		private void DrawBlurredShadow(IImageByte destination, IVertexSource shadow)
		{
			// C++ pixfmt_sgray8 blends with gray8::lerp, which BlenderGrayExact is.
			var gray = new ImageBuffer(this.Width, this.Height, 8, new BlenderGrayExact(1));
			var ras = new ScanlineRasterizer();
			ras.SetVectorClipBox(0, 0, this.Width, this.Height);
			ras.add_path(shadow);
			ScanlineBoolean2Demo.RenderScanlines(ras, new ScanlineCachePacked8(), new SolidScanlineSink(new ImageClippingProxy(gray), Color.White));

			double radius = this.RadiusSlider.Value;
			RectangleDouble bounds = shadow.GetBounds();
			double x1 = Math.Max(bounds.Left - radius, 0);
			double y1 = Math.Max(bounds.Bottom - radius, 0);
			double x2 = Math.Min(bounds.Right + radius, this.Width);
			double y2 = Math.Min(bounds.Top + radius, this.Height);
			if (x1 > x2 || y1 > y2)
			{
				return;
			}

			// C++ pixfmt::attach(pixf, int(x1), int(y1), int(x2), int(y2)): the inclusive integer box, clipped to
			// the image.
			int left = (int)x1;
			int bottom = (int)y1;
			int right = Math.Min((int)x2, this.Width - 1);
			int top = Math.Min((int)y2, this.Height - 1);
			var area = new ImageBuffer();
			area.AttachBuffer(gray.GetBuffer(), gray.GetBufferOffsetXY(left, bottom), right - left + 1, top - bottom + 1, gray.StrideInBytes(), 8, 1);
			area.SetRecieveBlender(new BlenderGrayExact(1));
			stack_blur.BlurGray8(area, Util.uround(radius), Util.uround(radius));

			if (this.MethodRbox.CurrentItem == 0)
			{
				destination.BlendFromColor(area, SingleColor, left, bottom);
			}
			else
			{
				destination.BlendFromLut(area, this.colorLut, left, bottom);
			}
		}
	}
}
