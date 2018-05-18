/*
Copyright (c) 2014, Lars Brubaker
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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	internal class pattern_src_brightness_to_alpha_RGBA_Bytes : ImageProxy
	{
		private static byte[] brightness_to_alpha =
        {
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 254, 254, 254, 254, 254, 254,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255, 255,
            254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 254, 253, 253,
            253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 253, 252,
            252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 252, 251, 251, 251, 251, 251,
            251, 251, 251, 251, 250, 250, 250, 250, 250, 250, 250, 250, 249, 249, 249, 249,
            249, 249, 249, 248, 248, 248, 248, 248, 248, 248, 247, 247, 247, 247, 247, 246,
            246, 246, 246, 246, 246, 245, 245, 245, 245, 245, 244, 244, 244, 244, 243, 243,
            243, 243, 243, 242, 242, 242, 242, 241, 241, 241, 241, 240, 240, 240, 239, 239,
            239, 239, 238, 238, 238, 238, 237, 237, 237, 236, 236, 236, 235, 235, 235, 234,
            234, 234, 233, 233, 233, 232, 232, 232, 231, 231, 230, 230, 230, 229, 229, 229,
            228, 228, 227, 227, 227, 226, 226, 225, 225, 224, 224, 224, 223, 223, 222, 222,
            221, 221, 220, 220, 219, 219, 219, 218, 218, 217, 217, 216, 216, 215, 214, 214,
            213, 213, 212, 212, 211, 211, 210, 210, 209, 209, 208, 207, 207, 206, 206, 205,
            204, 204, 203, 203, 202, 201, 201, 200, 200, 199, 198, 198, 197, 196, 196, 195,
            194, 194, 193, 192, 192, 191, 190, 190, 189, 188, 188, 187, 186, 186, 185, 184,
            183, 183, 182, 181, 180, 180, 179, 178, 177, 177, 176, 175, 174, 174, 173, 172,
            171, 171, 170, 169, 168, 167, 166, 166, 165, 164, 163, 162, 162, 161, 160, 159,
            158, 157, 156, 156, 155, 154, 153, 152, 151, 150, 149, 148, 148, 147, 146, 145,
            144, 143, 142, 141, 140, 139, 138, 137, 136, 135, 134, 133, 132, 131, 130, 129,
            128, 128, 127, 125, 124, 123, 122, 121, 120, 119, 118, 117, 116, 115, 114, 113,
            112, 111, 110, 109, 108, 107, 106, 105, 104, 102, 101, 100,  99,  98,  97,  96,
             95,  94,  93,  91,  90,  89,  88,  87,  86,  85,  84,  82,  81,  80,  79,  78,
             77,  75,  74,  73,  72,  71,  70,  69,  67,  66,  65,  64,  63,  61,  60,  59,
             58,  57,  56,  54,  53,  52,  51,  50,  48,  47,  46,  45,  44,  42,  41,  40,
             39,  37,  36,  35,  34,  33,  31,  30,  29,  28,  27,  25,  24,  23,  22,  20,
             19,  18,  17,  15,  14,  13,  12,  11,   9,   8,   7,   6,   4,   3,   2,   1
        };

		public pattern_src_brightness_to_alpha_RGBA_Bytes(ImageBuffer rb)
			: base(new ImageBuffer(rb, new BlenderBGRA()))
		{
		}

		public override Color GetPixel(int x, int y)
		{
			Color c = linkedImage.GetPixel(x, y);
			c.Alpha0To255 = brightness_to_alpha[c.Red0To255 + c.Green0To255 + c.Blue0To255];
			return c;
		}
	};

	public class line_patterns_application : GuiWidget, IDemoApp
	{
		private Color m_ctrl_color;
		private CheckBox m_approximation_method;
		private bezier_ctrl m_curve1 = new bezier_ctrl();
		private bezier_ctrl m_curve2 = new bezier_ctrl();
		private bezier_ctrl m_curve3 = new bezier_ctrl();
		private bezier_ctrl m_curve4 = new bezier_ctrl();
		private bezier_ctrl m_curve5 = new bezier_ctrl();
		private bezier_ctrl m_curve6 = new bezier_ctrl();
		private bezier_ctrl m_curve7 = new bezier_ctrl();
		private bezier_ctrl m_curve8 = new bezier_ctrl();
		private bezier_ctrl m_curve9 = new bezier_ctrl();
		private Slider m_scale_x;
		private Slider m_start_x;

		public static ImageBuffer rbuf_img0 = new ImageBuffer();
		public static ImageBuffer rbuf_img1 = new ImageBuffer();
		public static ImageBuffer rbuf_img2 = new ImageBuffer();
		public static ImageBuffer rbuf_img3 = new ImageBuffer();
		public static ImageBuffer rbuf_img4 = new ImageBuffer();
		public static ImageBuffer rbuf_img5 = new ImageBuffer();
		public static ImageBuffer rbuf_img6 = new ImageBuffer();
		public static ImageBuffer rbuf_img7 = new ImageBuffer();
		public static ImageBuffer rbuf_img8 = new ImageBuffer();

		static line_patterns_application()
		{
			var ImageIO = AggContext.ImageIO;
			if (!ImageIO.LoadImageData("1.bmp", line_patterns_application.rbuf_img0)
				|| !ImageIO.LoadImageData("2.bmp", line_patterns_application.rbuf_img1)
				|| !ImageIO.LoadImageData("3.bmp", line_patterns_application.rbuf_img2)
				|| !ImageIO.LoadImageData("4.bmp", line_patterns_application.rbuf_img3)
				|| !ImageIO.LoadImageData("5.bmp", line_patterns_application.rbuf_img4)
				|| !ImageIO.LoadImageData("6.bmp", line_patterns_application.rbuf_img5)
				|| !ImageIO.LoadImageData("7.bmp", line_patterns_application.rbuf_img6)
				|| !ImageIO.LoadImageData("8.bmp", line_patterns_application.rbuf_img7)
				|| !ImageIO.LoadImageData("9.bmp", line_patterns_application.rbuf_img8))
			{
				String buf = "There must be files 1%s...9%s\n"
							 + "Download and unzip:\n"
							 + "http://www.antigrain.com/line_patterns.bmp.zip\n"
							 + "or\n"
							 + "http://www.antigrain.com/line_patterns.ppm.tar.gz\n";
				throw new System.Exception(buf);
			}
		}

		public line_patterns_application()
		{
			AnchorAll();
			m_ctrl_color = new Color(0, 0.3, 0.5, 0.3);
			m_scale_x = new Slider(5.0, 5.0, 240.0, 12.0);
			m_start_x = new Slider(250.0, 5.0, 495.0, 12.0);
			m_approximation_method = new CheckBox(10, 30, "Approximation Method = curve_div");

			m_curve1.line_color(m_ctrl_color);
			m_curve2.line_color(m_ctrl_color);
			m_curve3.line_color(m_ctrl_color);
			m_curve4.line_color(m_ctrl_color);
			m_curve5.line_color(m_ctrl_color);
			m_curve6.line_color(m_ctrl_color);
			m_curve7.line_color(m_ctrl_color);
			m_curve8.line_color(m_ctrl_color);
			m_curve9.line_color(m_ctrl_color);

			m_curve1.curve(64, 19, 14, 126, 118, 266, 19, 265);
			m_curve2.curve(112, 113, 178, 32, 200, 132, 125, 438);
			m_curve3.curve(401, 24, 326, 149, 285, 11, 177, 77);
			m_curve4.curve(188, 427, 129, 295, 19, 283, 25, 410);
			m_curve5.curve(451, 346, 302, 218, 265, 441, 459, 400);
			m_curve6.curve(454, 198, 14, 13, 220, 291, 483, 283);
			m_curve7.curve(301, 398, 355, 231, 209, 211, 170, 353);
			m_curve8.curve(484, 101, 222, 33, 486, 435, 487, 138);
			m_curve9.curve(143, 147, 11, 45, 83, 427, 132, 197);

			AddChild(m_curve1);
			AddChild(m_curve2);
			AddChild(m_curve3);
			AddChild(m_curve4);
			AddChild(m_curve5);
			AddChild(m_curve6);
			AddChild(m_curve7);
			AddChild(m_curve8);
			AddChild(m_curve9);
			AddChild(m_approximation_method);

			m_scale_x.Text = "Scale X=%.2f";
			m_scale_x.SetRange(0.2, 3.0);
			m_scale_x.Value = 1.0;
			AddChild(m_scale_x);

			m_start_x.Text = "Start X=%.2f";
			m_start_x.SetRange(0.0, 10.0);
			m_start_x.Value = 0.0;
			AddChild(m_start_x);

			m_approximation_method.CheckedStateChanged += m_approximation_method_CheckedStateChanged;
		}

		public string Title { get; } = "Line Patterns";

		public string DemoCategory { get; } = "Vector";

		public string DemoDescription { get; } = "AGG Example. Drawing Lines with Image Patterns";

		private void m_approximation_method_CheckedStateChanged(object sender, EventArgs e)
		{
			Curves.CurveApproximationMethod method = Curves.CurveApproximationMethod.curve_div;
			if (m_approximation_method.Checked)
			{
				method = Curves.CurveApproximationMethod.curve_inc;
				m_approximation_method.Text = "Approximation Method = curve_inc";
			}
			else
			{
				m_approximation_method.Text = "Approximation Method = curve_div";
			}
			m_curve1.curve().approximation_method(method);
			m_curve2.curve().approximation_method(method);
			m_curve3.curve().approximation_method(method);
			m_curve4.curve().approximation_method(method);
			m_curve5.curve().approximation_method(method);
			m_curve6.curve().approximation_method(method);
			m_curve7.curve().approximation_method(method);
			m_curve8.curve().approximation_method(method);
			m_curve9.curve().approximation_method(method);
		}

		private void draw_curve(line_image_pattern patt, rasterizer_outline_aa ras, ImageLineRenderer ren,
			pattern_src_brightness_to_alpha_RGBA_Bytes src, IVertexSource vs)
		{
			patt.create(src);
			ren.scale_x(m_scale_x.Value);
			ren.start_x(m_start_x.Value);
			ras.add_path(vs);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageClippingProxy ren_base = new ImageClippingProxy(graphics2D.DestImage);

			ren_base.clear(new ColorF(1.0, 1.0, .95));

			ScanlineRasterizer ras = new ScanlineRasterizer();
			ScanlineCachePacked8 sl = new ScanlineCachePacked8();

			// Pattern source. Must have an interface:
			// width() const
			// height() const
			// pixel(int x, int y) const
			// Any agg::renderer_base<> or derived
			// is good for the use as a source.
			//-----------------------------------
			pattern_src_brightness_to_alpha_RGBA_Bytes p1 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img0);
			pattern_src_brightness_to_alpha_RGBA_Bytes p2 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img1);
			pattern_src_brightness_to_alpha_RGBA_Bytes p3 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img2);
			pattern_src_brightness_to_alpha_RGBA_Bytes p4 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img3);
			pattern_src_brightness_to_alpha_RGBA_Bytes p5 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img4);
			pattern_src_brightness_to_alpha_RGBA_Bytes p6 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img5);
			pattern_src_brightness_to_alpha_RGBA_Bytes p7 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img6);
			pattern_src_brightness_to_alpha_RGBA_Bytes p8 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img7);
			pattern_src_brightness_to_alpha_RGBA_Bytes p9 = new pattern_src_brightness_to_alpha_RGBA_Bytes(rbuf_img8);

			//pattern_filter_bilinear_RGBA_Bytes fltr = new pattern_filter_bilinear_RGBA_Bytes();           // Filtering functor

			// agg::line_image_pattern is the main container for the patterns. It creates
			// a copy of the patterns extended according to the needs of the filter.
			// agg::line_image_pattern can operate with arbitrary image width, but if the
			// width of the pattern is power of 2, it's better to use the modified
			// version agg::line_image_pattern_pow2 because it works about 15-25 percent
			// faster than agg::line_image_pattern (because of using simple masking instead
			// of expensive '%' operation).

			//-- Create with specifying the source

			//-- Create uninitialized and set the source

			line_image_pattern patt = new line_image_pattern(new pattern_filter_bilinear_RGBA_Bytes());
			ImageLineRenderer ren_img = new ImageLineRenderer(ren_base, patt);

			rasterizer_outline_aa ras_img = new rasterizer_outline_aa(ren_img);

			draw_curve(patt, ras_img, ren_img, p1, m_curve1.curve());
			/*
			draw_curve(patt, ras_img, ren_img, p2, m_curve2.curve());
			draw_curve(patt, ras_img, ren_img, p3, m_curve3.curve());
			draw_curve(patt, ras_img, ren_img, p4, m_curve4.curve());
			draw_curve(patt, ras_img, ren_img, p5, m_curve5.curve());
			draw_curve(patt, ras_img, ren_img, p6, m_curve6.curve());
			draw_curve(patt, ras_img, ren_img, p7, m_curve7.curve());
			draw_curve(patt, ras_img, ren_img, p8, m_curve8.curve());
			draw_curve(patt, ras_img, ren_img, p9, m_curve9.curve());
			 */

			base.OnDraw(graphics2D);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			var demoWidget = new line_patterns_application();

			var systemWindow = new SystemWindow(500, 450);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}