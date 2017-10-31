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

//#define SourceDepthFloat
//#define SourceDepth24

using System;
using System.Diagnostics;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class image_filters : FlowLayoutWidget, IDemoApp
	{
#if SourceDepthFloat
        public static ImageBufferFloat m_TempDestImage = new ImageBufferFloat();
        public static ImageBufferFloat m_OriginalImage;
        public static ImageBufferFloat m_RotatedImage;
#else
		public static ImageBuffer m_TempDestImage = new ImageBuffer();
		public static ImageBuffer m_OriginalImage;
		public static ImageBuffer m_RotatedImage;
#endif

		private Slider m_radius;
		private Slider m_step;
		private RadioButtonGroup filterSelectionButtons;
		private CheckBox m_normalize;
		private Button m_run;
		private Button m_single_step;
		private Button m_refresh;
		private ScanlineCachePacked8 m_ScanlinePacked;
		private ScanlineRasterizer m_Rasterizer;
		private scanline_unpacked_8 m_ScanlineUnpacked;
		private span_allocator m_SpanAllocator;

		private double m_cur_angle;
		private int m_cur_filter;
		private int m_num_steps;
		private double m_num_pix;
		private double m_time1;
		private double m_time2;

		public image_filters()
			: base(FlowDirection.BottomToTop)
		{
			m_step = new Slider(new Vector2(115, 5), new Vector2(285, 6));
			m_radius = new Slider(new Vector2(115, 5 + 15), new Vector2(285, 6));
			filterSelectionButtons = new RadioButtonGroup(new Vector2(0.0, 10.0), new Vector2(110.0, 210.0));
			m_normalize = new CheckBox(8.0, 215.0, "Normalize Filter");

			m_refresh = new Button(8.0, 273.0, new ButtonViewText("Refresh", 8, 1, 3));
			m_refresh.Click += RefreshImage;
			m_run = new Button(8.0, 253.0, new ButtonViewText("RUN Test!", 8, 1, 3));
			m_run.Click += RunTest;
			m_single_step = new Button(8.0, 233.0, new ButtonViewText("Single Step", 8, 1, 3));
			m_single_step.Click += SingleStep;

			m_cur_angle = (0.0);
			m_cur_filter = (1);
			m_num_steps = (0);
			m_num_pix = (0.0);
			m_time1 = (0);
			m_time2 = (0);
			m_ScanlinePacked = new ScanlineCachePacked8();
			m_Rasterizer = new ScanlineRasterizer();
			m_ScanlineUnpacked = new scanline_unpacked_8();
			m_SpanAllocator = new span_allocator();

			AddChild(m_radius);
			AddChild(m_step);
			AddChild(filterSelectionButtons);
			AddChild(m_run);
			AddChild(m_single_step);
			AddChild(m_normalize);
			AddChild(m_refresh);
			m_normalize.Checked = true;

			m_radius.Text = "Filter Radius={0:F2}";
			m_step.Text = "Step={0:F2}";
			m_radius.SetRange(2.0, 8.0);
			m_radius.Value = 4.0;
			m_step.SetRange(1.0, 10.0);
			m_step.Value = 5.0;

			filterSelectionButtons.AddRadioButton("simple (NN)");
			filterSelectionButtons.AddRadioButton("bilinear");
			filterSelectionButtons.AddRadioButton("bicubic");
			filterSelectionButtons.AddRadioButton("spline16");
			filterSelectionButtons.AddRadioButton("spline36");
			filterSelectionButtons.AddRadioButton("hanning");
			filterSelectionButtons.AddRadioButton("hamming");
			filterSelectionButtons.AddRadioButton("hermite");
			filterSelectionButtons.AddRadioButton("kaiser");
			filterSelectionButtons.AddRadioButton("quadric");
			filterSelectionButtons.AddRadioButton("catrom");
			filterSelectionButtons.AddRadioButton("gaussian");
			filterSelectionButtons.AddRadioButton("bessel");
			filterSelectionButtons.AddRadioButton("mitchell");
			filterSelectionButtons.AddRadioButton("sinc");
			filterSelectionButtons.AddRadioButton("lanczos");
			filterSelectionButtons.AddRadioButton("blackman");
			filterSelectionButtons.SelectedIndex = 1;

			filterSelectionButtons.background_color(new ColorF(0.0, 0.0, 0.0, 0.1));
		}

		public string Title { get; } = "Image Filters Comparison";

		public string DemoCategory { get; } = "Bitmap";

		public string DemoDescription { get; } = "The image transformer algorithm can work with different interpolation filters, such as Bilinear, Bicubic, Sinc, Blackman. The example demonstrates the difference in quality between different filters. When switch the �Run Test� on, the image starts rotating. But at each step there is the previously rotated image taken, so the quality degrades. This degradation as well as the performance depend on the type of the interpolation filter.";

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();

#if SourceDepthFloat
            ImageBufferFloat tempImageToLoadInto = new ImageBufferFloat();
#else
			ImageBuffer tempImageToLoadInto = new ImageBuffer();
#endif

			string img_name = "spheres.bmp";
			if (!AggContext.ImageIO.LoadImageData(img_name, tempImageToLoadInto))
			{
				string buf;
				buf = "File not found: "
					+ img_name
					+ ".bmp"
					+ ". Download http://www.antigrain.com/" + img_name + "\n"
					+ "or copy it from another directory if available.";
				MessageBox.ShowMessageBox(buf, "Missing Files");
			}
			else
			{
#if SourceDepth24
                image_filters.m_TempDestImage = new ImageBuffer(tempImageToLoadInto, new BlenderBGR());

                image_filters.m_RotatedImage = new ImageBuffer(image_filters.m_TempDestImage, new BlenderBGR());
                image_filters.m_OriginalImage = new ImageBuffer(image_filters.m_TempDestImage, new BlenderBGR());

				image_filters.m_TempDestImage.SetRecieveBlender(new BlenderPreMultBGR());
#else
#if SourceDepthFloat
				image_filters.m_TempDestImage = new ImageBufferFloat(tempImageToLoadInto.Width, tempImageToLoadInto.Height, 128, new BlenderBGRAFloat());
				image_filters.m_TempDestImage.CopyFrom(tempImageToLoadInto);

				image_filters.m_RotatedImage = new ImageBufferFloat(image_filters.m_TempDestImage, new BlenderBGRAFloat());
				image_filters.m_OriginalImage = new ImageBufferFloat(image_filters.m_TempDestImage, new BlenderBGRAFloat());

				//image_filters.m_TempDestImage.SetRecieveBlender(new BlenderBGRAFloat());
				image_filters.m_TempDestImage.SetRecieveBlender(new BlenderPreMultBGRAFloat());
#else
				image_filters.m_TempDestImage = new ImageBuffer(tempImageToLoadInto.Width, tempImageToLoadInto.Height, 32, new BlenderBGRA());
				image_filters.m_TempDestImage.CopyFrom(tempImageToLoadInto);

				image_filters.m_RotatedImage = new ImageBuffer(image_filters.m_TempDestImage, new BlenderBGRA());
				image_filters.m_OriginalImage = new ImageBuffer(image_filters.m_TempDestImage, new BlenderBGRA());

				image_filters.m_TempDestImage.SetRecieveBlender(new BlenderPreMultBGRA());
#endif
#endif

				int w = image_filters.m_TempDestImage.Width + 220;
				int h = image_filters.m_TempDestImage.Height + 200;

				if (w < 305) w = 305;
				if (h < 325) h = 325;

				Parent.LocalBounds = new RectangleDouble(0, 0, w, h);

				transform_image(0.0);
			}

			base.OnParentChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

#if SourceDepthFloat
            ImageClippingProxyFloat clippingProxy = new ImageClippingProxyFloat(graphics2D.DestImageFloat);

            clippingProxy.clear(new RGBA_Floats(1.0, 1.0, 1.0));
			clippingProxy.CopyFrom(m_TempDestImage, new RectangleInt(0, 0, (int)Width, (int)Height), 110, 35);
#else
			ImageClippingProxy clippingProxy = new ImageClippingProxy(widgetsSubImage);

			clippingProxy.clear(new ColorF(1.0, 1.0, 1.0));
			clippingProxy.CopyFrom(m_TempDestImage, new RectangleInt(0, 0, (int)Width, (int)Height), 110, 35);
#endif

			string buf = string.Format("NSteps={0:F0}", m_num_steps);
			gsv_text t = new gsv_text();
			t.start_point(200.0, 430);
			t.SetFontSize(10.0);
			t.text(buf);

			Stroke pt = new Stroke(t);
			pt.width(1.5);

			m_Rasterizer.add_path(pt);
#if SourceDepthFloat
            RGBA_Floats colorBlack = new RGBA_Floats(0, 0, 0);
#else
			Color colorBlack = new Color(0, 0, 0);
#endif
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderSolid(clippingProxy, m_Rasterizer, m_ScanlinePacked, colorBlack);

			if (m_time1 != m_time2 && m_num_pix > 0.0)
			{
				buf = string.Format("{0:F2} Kpix/sec", m_num_pix / (m_time2 - m_time1));
				t.start_point(200.0, 450);
				t.text(buf);
				m_Rasterizer.add_path(pt);
				scanlineRenderer.RenderSolid(clippingProxy, m_Rasterizer, m_ScanlinePacked, colorBlack);
			}

			if (filterSelectionButtons.SelectedIndex >= 14)
			{
				m_radius.Visible = true;
			}
			else
			{
				m_radius.Visible = true;
			}

			base.OnDraw(graphics2D);
		}

		private void transform_image(double angle)
		{
			double width = m_TempDestImage.Width;
			double height = m_TempDestImage.Height;

#if SourceDepthFloat
            ImageClippingProxyFloat clippedDest = new ImageClippingProxyFloat(m_TempDestImage);
#else
			ImageClippingProxy clippedDest = new ImageClippingProxy(m_TempDestImage);
#endif

			clippedDest.clear(new ColorF(1.0, 1.0, 1.0));

			Affine src_mtx = Affine.NewIdentity();
			src_mtx *= Affine.NewTranslation(-width / 2.0, -height / 2.0);
			src_mtx *= Affine.NewRotation(angle * Math.PI / 180.0);
			src_mtx *= Affine.NewTranslation(width / 2.0, height / 2.0);

			Affine img_mtx = new Affine(src_mtx);
			img_mtx.invert();

			double r = width;
			if (height < r) r = height;

			r *= 0.5;
			r -= 4.0;
			VertexSource.Ellipse ell = new MatterHackers.Agg.VertexSource.Ellipse(width / 2.0, height / 2.0, r, r, 200);
			VertexSourceApplyTransform tr = new VertexSourceApplyTransform(ell, src_mtx);

			m_num_pix += r * r * Math.PI;

#if SourceDepthFloat
            span_interpolator_linear_float interpolator = new span_interpolator_linear_float(img_mtx);
#else
			span_interpolator_linear interpolator = new span_interpolator_linear(img_mtx);
#endif

			ImageFilterLookUpTable filter = new ImageFilterLookUpTable();
			bool norm = m_normalize.Checked;

#if SourceDepthFloat
            ImageBufferAccessorClipFloat source = new ImageBufferAccessorClipFloat(m_RotatedImage, RGBA_Floats.rgba_pre(0,0,0,0).GetAsRGBA_Floats());
#else
			ImageBufferAccessorClip source = new ImageBufferAccessorClip(m_RotatedImage, ColorF.rgba_pre(0, 0, 0, 0).GetAsRGBA_Bytes());
#endif
			IImageFilterFunction filterFunction = null;
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();

			switch (filterSelectionButtons.SelectedIndex)
			{
				case 0:
					{
#if SourceDepthFloat
                    span_image_filter_float spanGenerator;

#else
						span_image_filter spanGenerator;
#endif

						switch (source.SourceImage.BitDepth)
						{
							case 24:
#if SourceDepthFloat
                            throw new NotImplementedException();
#else
								spanGenerator = new span_image_filter_rgb_nn(source, interpolator);
#endif
								break;

							case 32:
#if SourceDepthFloat
                            throw new NotImplementedException();
#else
								spanGenerator = new span_image_filter_rgba_nn(source, interpolator);
#endif
								break;

							default:
								throw new NotImplementedException("only support 24 and 32 bit");
						}

						m_Rasterizer.add_path(tr);
						scanlineRenderer.GenerateAndRender(m_Rasterizer, m_ScanlineUnpacked, clippedDest, m_SpanAllocator, spanGenerator);
					}
					break;

				case 1:
					{
#if SourceDepthFloat
                    span_image_filter_float spanGenerator;
#else
						span_image_filter spanGenerator;
#endif
						switch (source.SourceImage.BitDepth)
						{
							case 24:
#if SourceDepthFloat
                            throw new NotImplementedException();
#else
								spanGenerator = new span_image_filter_rgb_bilinear(source, interpolator);
#endif
								break;

							case 32:
#if SourceDepthFloat
                            throw new NotImplementedException();
#else
								spanGenerator = new span_image_filter_rgba_bilinear(source, interpolator);
#endif
								break;

#if SourceDepthFloat
                        case 128:
                            spanGenerator = new span_image_filter_rgba_bilinear_float(source, interpolator);
                            break;
#endif

							default:
								throw new NotImplementedException("only support 24 and 32 bit");
						}
						m_Rasterizer.add_path(tr);
						scanlineRenderer.GenerateAndRender(m_Rasterizer, m_ScanlineUnpacked, clippedDest, m_SpanAllocator, spanGenerator);
					}
					break;

				case 5:
				case 6:
				case 7:
					{
						switch (filterSelectionButtons.SelectedIndex)
						{
							case 5: filter.calculate(new image_filter_hanning(), norm); break;
							case 6: filter.calculate(new image_filter_hamming(), norm); break;
							case 7: filter.calculate(new image_filter_hermite(), norm); break;
						}

#if SourceDepthFloat
                    throw new NotImplementedException();
#else
						span_image_filter_rgb_2x2 spanGenerator = new span_image_filter_rgb_2x2(source, interpolator, filter);
#endif
						m_Rasterizer.add_path(tr);
#if SourceDepthFloat
                    throw new NotImplementedException();
#else
						scanlineRenderer.GenerateAndRender(m_Rasterizer, m_ScanlineUnpacked, clippedDest, m_SpanAllocator, spanGenerator);
#endif
					}
					break;

				case 2:
				case 3:
				case 4:
				case 8:
				case 9:
				case 10:
				case 11:
				case 12:
				case 13:
				case 14:
				case 15:
				case 16:
					{
						switch (filterSelectionButtons.SelectedIndex)
						{
							case 2: filter.calculate(new image_filter_bicubic(), norm); break;
							case 3: filter.calculate(new image_filter_spline16(), norm); break;
							case 4: filter.calculate(new image_filter_spline36(), norm); break;
							case 8: filter.calculate(new image_filter_kaiser(), norm); break;
							case 9: filter.calculate(new image_filter_quadric(), norm); break;
							case 10: filter.calculate(new image_filter_catrom(), norm); break;
							case 11: filter.calculate(new image_filter_gaussian(), norm); break;
							case 12: filter.calculate(new image_filter_bessel(), norm); break;
							case 13: filter.calculate(new image_filter_mitchell(), norm); break;
							case 14: filter.calculate(new image_filter_sinc(m_radius.Value), norm); break;
							case 15: filter.calculate(new image_filter_lanczos(m_radius.Value), norm); break;
							case 16:
								filterFunction = new image_filter_blackman(m_radius.Value);
								//filterFunction = new image_filter_bilinear();
								filter.calculate(filterFunction, norm);
								break;
						}

#if SourceDepthFloat
                    span_image_filter_float spanGenerator;

#else
						span_image_filter spanGenerator;
#endif
						switch (source.SourceImage.BitDepth)
						{
							case 24:
#if SourceDepthFloat
                            throw new NotImplementedException();
#else
								spanGenerator = new span_image_filter_rgb(source, interpolator, filter);
#endif
								break;

							case 32:
#if SourceDepthFloat
                            throw new NotImplementedException();
#else
								spanGenerator = new span_image_filter_rgba(source, interpolator, filter);
#endif
								break;

#if SourceDepthFloat
                        case 128:
                            spanGenerator = new span_image_filter_rgba_float(source, interpolator, filterFunction);
                            break;
#endif

							default:
								throw new NotImplementedException("only support 24 and 32 bit");
						}

						m_Rasterizer.add_path(tr);
						scanlineRenderer.GenerateAndRender(m_Rasterizer, m_ScanlineUnpacked, clippedDest, m_SpanAllocator, spanGenerator);
					}
					break;
			}
		}

		private void SingleStep(object sender, EventArgs mouseEvent)
		{
			m_cur_angle += m_step.Value;
			m_RotatedImage.CopyFrom(m_TempDestImage);
			transform_image(m_step.Value);
			m_num_steps++;
			Invalidate();
		}

		private Stopwatch stopwatch = new Stopwatch();

		private void RunTest(object sender, EventArgs mouseEvent)
		{
			stopwatch.Restart();
			m_time1 = m_time2 = stopwatch.ElapsedMilliseconds;
			m_num_pix = 0.0;
			UiThread.RunOnIdle(OnIdle);
		}

		private void RefreshImage(object sender, EventArgs mouseEvent)
		{
			stopwatch.Restart();
			m_time1 = m_time2 = 0;
			m_num_pix = 0.0;
			m_cur_angle = 0.0;
			m_RotatedImage.CopyFrom(m_OriginalImage);
			transform_image(0.0);
			m_cur_filter = filterSelectionButtons.SelectedIndex;
			m_num_steps = 0;
			Invalidate();
		}

		public void OnIdle()
		{
			if (m_cur_angle < 360.0)
			{
				m_cur_angle += m_step.Value;
				image_filters.m_RotatedImage.CopyFrom(image_filters.m_TempDestImage);
				stopwatch.Restart();
				transform_image(m_step.Value);
				m_time2 += stopwatch.ElapsedMilliseconds;
				m_num_steps++;
				UiThread.RunOnIdle(OnIdle, .1);
			}
			else
			{
				m_cur_angle = 0.0;
				//m_time2 = clock();
			}
			Invalidate();
		}

		[STAThread]
		public static void Main(string[] args)
		{
			var demoWidget = new image_filters();

			var systemWindow = new SystemWindow(305, 325);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}