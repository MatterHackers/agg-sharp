using System;
using System.Diagnostics;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class image_resample : GuiWidget
	{
		private Stopwatch stopwatch = new Stopwatch();

		public static ImageBuffer m_SourceImage = new ImageBuffer();
		private ScanlineRasterizer g_rasterizer;
		private scanline_unpacked_8 g_scanline;
		private double g_x1 = 0;
		private double g_y1 = 0;
		private double g_x2 = 0;
		private double g_y2 = 0;
		private GammaLookUpTable m_gamma_lut;
		private UI.PolygonEditWidget m_quad;
		private MatterHackers.Agg.UI.RadioButtonGroup m_trans_type;
		private MatterHackers.Agg.UI.Slider m_gamma;
		private MatterHackers.Agg.UI.Slider m_blur;
		private double m_old_gamma;

		public image_resample()
		{
			m_gamma_lut = new GammaLookUpTable(2.0);
			m_quad = new MatterHackers.Agg.UI.PolygonEditWidget(4, 5.0);
			m_trans_type = new MatterHackers.Agg.UI.RadioButtonGroup(new Vector2(400, 5.0), new Vector2(30 + 170.0, 95));
			m_gamma = new MatterHackers.Agg.UI.Slider(5.0, 5.0 + 15 * 0, 400 - 5, 10.0 + 15 * 0);
			m_blur = new MatterHackers.Agg.UI.Slider(5.0, 5.0 + 15 * 1, 400 - 5, 10.0 + 15 * 1);
			m_blur.ValueChanged += new EventHandler(NeedRedraw);
			m_gamma.ValueChanged += new EventHandler(NeedRedraw);
			m_old_gamma = 2.0;

			g_rasterizer = new ScanlineRasterizer();
			g_scanline = new scanline_unpacked_8();

			m_trans_type.AddRadioButton("Affine No Resample");
			m_trans_type.AddRadioButton("Affine Resample");
			m_trans_type.AddRadioButton("Perspective No Resample LERP");
			m_trans_type.AddRadioButton("Perspective No Resample Exact");
			m_trans_type.AddRadioButton("Perspective Resample LERP");
			m_trans_type.AddRadioButton("Perspective Resample Exact");
			m_trans_type.SelectedIndex = 4;
			AddChild(m_trans_type);

			m_gamma.SetRange(0.5, 3.0);
			m_gamma.Value = 2.0;
			m_gamma.Text = "Gamma={0:F3}";
			AddChild(m_gamma);

			m_blur.SetRange(0.5, 5.0);
			m_blur.Value = 1.0;
			m_blur.Text = "Blur={0:F3}";
			AddChild(m_blur);
		}

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();

			string img_name = "spheres.bmp";
			if (!AggContext.ImageIO.LoadImageData(img_name, image_resample.m_SourceImage))
			{
				string buf;
				buf = "File not found: "
					+ img_name
					+ ".bmp"
					+ ". Download http://www.antigrain.com/" + img_name + ".bmp" + "\n"
					+ "or copy it from another directory if available.";
				throw new NotImplementedException(buf);
			}
			else
			{
				if (image_resample.m_SourceImage.BitDepth != 32)
				{
					throw new Exception("we are expecting 32 bit source.");
				}
				// give the image some alpha. [4/6/2009 lbrubaker]
				ImageBuffer image32 = new ImageBuffer(image_resample.m_SourceImage.Width, image_resample.m_SourceImage.Height, 32, new BlenderBGRA());
				int offset;
				byte[] source = image_resample.m_SourceImage.GetBuffer(out offset);
				byte[] dest = image32.GetBuffer(out offset);
				for (int y = 0; y < image32.Height; y++)
					for (int x = 0; x < image32.Width; x++)
					{
						int i = y * image32.Width + x;
						dest[i * 4 + 0] = source[i * 4 + 0];
						dest[i * 4 + 1] = source[i * 4 + 1];
						dest[i * 4 + 2] = source[i * 4 + 2];
						Vector2 pixel = new Vector2(x, y);
						Vector2 center = new Vector2(image32.Width / 2, image32.Height / 2);
						Vector2 delta = pixel - center;
						int length = (int)Math.Min(delta.Length * 3, 255);
						dest[i * 4 + 3] = (byte)length;
					}
				// and set our new image with alpha
				image_resample.m_SourceImage = image32;
				//image_resample_application.m_SourceImage.SetBlender(new BlenderBGR());
			}

			base.OnParentChanged(e);
		}

		private void NeedRedraw(object sender, EventArgs e)
		{
			Invalidate();
		}

		public void OnInitialize()
		{
			g_x1 = 0.0;
			g_y1 = 0.0;
			g_x2 = m_SourceImage.Width;
			g_y2 = m_SourceImage.Height;

			double x1 = g_x1;// * 100.0;
			double y1 = g_y1;// * 100.0;
			double x2 = g_x2;// * 100.0;
			double y2 = g_y2;// * 100.0;

			double dx = Width / 2.0 - (x2 - x1) / 2.0;
			double dy = Height / 2.0 - (y2 - y1) / 2.0;
			m_quad.SetXN(0, Math.Floor(x1 + dx));
			m_quad.SetYN(0, Math.Floor(y1 + dy));// - 150;
			m_quad.SetXN(1, Math.Floor(x2 + dx));
			m_quad.SetYN(1, Math.Floor(y1 + dy));// - 110;
			m_quad.SetXN(2, Math.Floor(x2 + dx));
			m_quad.SetYN(2, Math.Floor(y2 + dy));// - 300;
			m_quad.SetXN(3, Math.Floor(x1 + dx));
			m_quad.SetYN(3, Math.Floor(y2 + dy));// - 200;

			//m_SourceImage.apply_gamma_dir(m_gamma_lut);
		}

		private bool didInit = false;

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

			if (!didInit)
			{
				didInit = true;
				OnInitialize();
			}

			if (m_gamma.Value != m_old_gamma)
			{
				m_gamma_lut.SetGamma(m_gamma.Value);
				AggContext.ImageIO.LoadImageData("spheres.bmp", m_SourceImage);
				//m_SourceImage.apply_gamma_dir(m_gamma_lut);
				m_old_gamma = m_gamma.Value;
			}

			ImageBuffer pixf = new ImageBuffer();
			switch (widgetsSubImage.BitDepth)
			{
				case 24:
					pixf.Attach(widgetsSubImage, new BlenderBGR());
					break;

				case 32:
					pixf.Attach(widgetsSubImage, new BlenderBGRA());
					break;

				default:
					throw new NotImplementedException();
			}

			ImageClippingProxy clippingProxy = new ImageClippingProxy(pixf);

			clippingProxy.clear(new RGBA_Floats(1, 1, 1));

			if (m_trans_type.SelectedIndex < 2)
			{
				// For the affine parallelogram transformations we
				// calculate the 4-th (implicit) point of the parallelogram
				m_quad.SetXN(3, m_quad.GetXN(0) + (m_quad.GetXN(2) - m_quad.GetXN(1)));
				m_quad.SetYN(3, m_quad.GetYN(0) + (m_quad.GetYN(2) - m_quad.GetYN(1)));
			}

			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			// draw a background to show how the alpha is working
			int RectWidth = 70;
			int xoffset = 50;
			int yoffset = 50;
			for (int i = 0; i < 7; i++)
			{
				for (int j = 0; j < 7; j++)
				{
					if ((i + j) % 2 != 0)
					{
						VertexSource.RoundedRect rect = new VertexSource.RoundedRect(i * RectWidth + xoffset, j * RectWidth + yoffset,
							(i + 1) * RectWidth + xoffset, (j + 1) * RectWidth + yoffset, 2);
						rect.normalize_radius();

						g_rasterizer.add_path(rect);
						scanlineRenderer.RenderSolid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(.2, .2, .2));
					}
				}
			}

			//--------------------------
			// Render the "quad" tool and controls
			g_rasterizer.add_path(m_quad);
			scanlineRenderer.RenderSolid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(0, 0.3, 0.5, 0.1));

			// Prepare the polygon to rasterize. Here we need to fill
			// the destination (transformed) polygon.
			g_rasterizer.SetVectorClipBox(0, 0, Width, Height);
			g_rasterizer.reset();
			int b = 0;
			g_rasterizer.move_to_d(m_quad.GetXN(0) - b, m_quad.GetYN(0) - b);
			g_rasterizer.line_to_d(m_quad.GetXN(1) + b, m_quad.GetYN(1) - b);
			g_rasterizer.line_to_d(m_quad.GetXN(2) + b, m_quad.GetYN(2) + b);
			g_rasterizer.line_to_d(m_quad.GetXN(3) - b, m_quad.GetYN(3) + b);

			//typedef agg::span_allocator<color_type> span_alloc_type;
			span_allocator sa = new span_allocator();
			image_filter_bilinear filter_kernel = new image_filter_bilinear();
			ImageFilterLookUpTable filter = new ImageFilterLookUpTable(filter_kernel, true);

			ImageBufferAccessorClamp source = new ImageBufferAccessorClamp(m_SourceImage);

			stopwatch.Restart();

			switch (m_trans_type.SelectedIndex)
			{
				case 0:
					{
						/*
								agg::trans_affine tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);

								typedef agg::span_interpolator_linear<agg::trans_affine> interpolator_type;
								interpolator_type interpolator(tr);

								typedef image_filter_2x2_type<source_type,
															  interpolator_type> span_gen_type;
								span_gen_type sg(source, interpolator, filter);
								agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
						 */
						break;
					}

				case 1:
					{
						/*
								agg::trans_affine tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);

								typedef agg::span_interpolator_linear<agg::trans_affine> interpolator_type;
								typedef image_resample_affine_type<source_type> span_gen_type;

								interpolator_type interpolator(tr);
								span_gen_type sg(source, interpolator, filter);
								sg.blur(m_blur.Value);
								agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
						 */
						break;
					}

				case 2:
					{
						/*
								agg::trans_perspective tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
								if(tr.is_valid())
								{
									typedef agg::span_interpolator_linear_subdiv<agg::trans_perspective> interpolator_type;
									interpolator_type interpolator(tr);

									typedef image_filter_2x2_type<source_type,
																  interpolator_type> span_gen_type;
									span_gen_type sg(source, interpolator, filter);
									agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
								}
						 */
						break;
					}

				case 3:
					{
						/*
								agg::trans_perspective tr(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
								if(tr.is_valid())
								{
									typedef agg::span_interpolator_trans<agg::trans_perspective> interpolator_type;
									interpolator_type interpolator(tr);

									typedef image_filter_2x2_type<source_type,
																  interpolator_type> span_gen_type;
									span_gen_type sg(source, interpolator, filter);
									agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
								}
						 */
						break;
					}

				case 4:
					{
						//typedef agg::span_interpolator_persp_lerp<> interpolator_type;
						//typedef agg::span_subdiv_adaptor<interpolator_type> subdiv_adaptor_type;

						span_interpolator_persp_lerp interpolator = new span_interpolator_persp_lerp(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
						span_subdiv_adaptor subdiv_adaptor = new span_subdiv_adaptor(interpolator);

						span_image_resample sg = null;
						if (interpolator.is_valid())
						{
							switch (source.SourceImage.BitDepth)
							{
								case 24:
									sg = new span_image_resample_rgb(source, subdiv_adaptor, filter);
									break;

								case 32:
									sg = new span_image_resample_rgba(source, subdiv_adaptor, filter);
									break;
							}

							sg.blur(m_blur.Value);
							scanlineRenderer.GenerateAndRender(g_rasterizer, g_scanline, clippingProxy, sa, sg);
						}
						break;
					}

				case 5:
					{
						/*
								typedef agg::span_interpolator_persp_exact<> interpolator_type;
								typedef agg::span_subdiv_adaptor<interpolator_type> subdiv_adaptor_type;

								interpolator_type interpolator(m_quad.polygon(), g_x1, g_y1, g_x2, g_y2);
								subdiv_adaptor_type subdiv_adaptor(interpolator);

								if(interpolator.is_valid())
								{
									typedef image_resample_type<source_type,
																subdiv_adaptor_type> span_gen_type;
									span_gen_type sg(source, subdiv_adaptor, filter);
									sg.blur(m_blur.Value);
									agg::render_scanlines_aa(g_rasterizer, g_scanline, rb_pre, sa, sg);
								}
						 */
						break;
					}
			}

			double tm = stopwatch.ElapsedMilliseconds;
			//pixf.apply_gamma_inv(m_gamma_lut);

			gsv_text t = new gsv_text();
			t.SetFontSize(10.0);

			Stroke pt = new Stroke(t);
			pt.width(1.5);

			string buf = string.Format("{0:F2} ms", tm);
			t.start_point(10.0, 70.0);
			t.text(buf);

			g_rasterizer.add_path(pt);
			scanlineRenderer.RenderSolid(clippingProxy, g_rasterizer, g_scanline, new RGBA_Bytes(0, 0, 0));

			//--------------------------
			//m_trans_type.Render(g_rasterizer, g_scanline, clippingProxy);
			//m_gamma.Render(g_rasterizer, g_scanline, clippingProxy);
			//m_blur.Render(g_rasterizer, g_scanline, clippingProxy);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (mouseEvent.Button == MouseButtons.Left)
			{
				m_quad.OnMouseDown(mouseEvent);
				if (MouseCaptured)
				{
					Invalidate();
				}
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				m_quad.OnMouseMove(mouseEvent);
				if (MouseCaptured)
				{
					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			m_quad.OnMouseUp(mouseEvent);
			if (MouseCaptured)
			{
				Invalidate();
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnKeyDown(MatterHackers.Agg.UI.KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Space)
			{
				double cx = (m_quad.GetXN(0) + m_quad.GetXN(1) + m_quad.GetXN(2) + m_quad.GetXN(3)) / 4;
				double cy = (m_quad.GetYN(0) + m_quad.GetYN(1) + m_quad.GetYN(2) + m_quad.GetYN(3)) / 4;
				Affine tr = Affine.NewTranslation(-cx, -cy);
				tr *= Affine.NewRotation(Math.PI / 2.0);
				tr *= Affine.NewTranslation(cx, cy);
				double xn0 = m_quad.GetXN(0); double yn0 = m_quad.GetYN(0);
				double xn1 = m_quad.GetXN(1); double yn1 = m_quad.GetYN(1);
				double xn2 = m_quad.GetXN(2); double yn2 = m_quad.GetYN(2);
				double xn3 = m_quad.GetXN(3); double yn3 = m_quad.GetYN(3);
				tr.transform(ref xn0, ref yn0);
				tr.transform(ref xn1, ref yn1);
				tr.transform(ref xn2, ref yn2);
				tr.transform(ref xn3, ref yn3);
				m_quad.SetXN(0, xn0); m_quad.SetYN(0, yn0);
				m_quad.SetXN(1, xn1); m_quad.SetYN(1, yn1);
				m_quad.SetXN(2, xn2); m_quad.SetYN(2, yn2);
				m_quad.SetXN(3, xn3); m_quad.SetYN(3, yn3);
				Invalidate();
			}

			base.OnKeyDown(keyEvent);
		}
	}

	public class ImageResampleFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new image_resample();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
				"Bitmap",
				"Image Transformations with Resampling",
				"The demonstration of image transformations with resampling. You can see the difference in quality between regular image transformers and the ones with resampling. Of course, image tranformations with resampling work slower because they provide the best possible quality.",
				600,
				600);

			return appWidgetInfo;
		}
	}
}