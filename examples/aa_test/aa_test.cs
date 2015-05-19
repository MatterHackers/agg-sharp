using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;

namespace MatterHackers.Agg
{
	public class aa_test : GuiWidget
	{
		private double[] m_x = new double[2];
		private double[] m_y = new double[2];
		private double m_dx;
		private double m_dy;
		private int m_idx;
		private MatterHackers.Agg.UI.Slider m_gamma;

		public aa_test()
		{
			AnchorAll();
			m_idx = (-1);
			m_gamma = new MatterHackers.Agg.UI.Slider(3, 3, 480 - 3, 8);

			m_x[0] = 100; m_y[0] = 100;
			m_x[1] = 500; m_y[1] = 350;
			AddChild(m_gamma);
			m_gamma.Text = "gamma={0:F3}";
			m_gamma.SetRange(0.0, 3.0);
			m_gamma.Value = 1.8;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			GammaLookUpTable gamma = new GammaLookUpTable(m_gamma.Value);
			IRecieveBlenderByte NormalBlender = new BlenderBGRA();
			IRecieveBlenderByte GammaBlender = new BlenderGammaBGRA(gamma);
			ImageBuffer rasterNormal = new ImageBuffer(NewGraphics2D().DestImage, NormalBlender);
			ImageBuffer rasterGamma = new ImageBuffer(NewGraphics2D().DestImage, GammaBlender);
			ImageClippingProxy clippingProxyNormal = new ImageClippingProxy(rasterNormal);
			ImageClippingProxy clippingProxyGamma = new ImageClippingProxy(rasterGamma);

			clippingProxyNormal.clear(new RGBA_Floats(0, 0, 0));

			ScanlineRasterizer ras = new ScanlineRasterizer();
			ScanlineCachePacked8 sl = new ScanlineCachePacked8();

			VertexSource.Ellipse e = new VertexSource.Ellipse();

			// TODO: If you drag the control circles below the bottom of the window we get an exception.  This does not happen in AGG.
			// It needs to be debugged.  Turning on clipping fixes it.  But standard agg works without clipping.  Could be a bigger problem than this.
			//ras.clip_box(0, 0, width(), height());

			// Render two "control" circles
			e.init(m_x[0], m_y[0], 3, 3, 16);
			ras.add_path(e);
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderSolid(clippingProxyNormal, ras, sl, new RGBA_Bytes(127, 127, 127));
			e.init(m_x[1], m_y[1], 3, 3, 16);
			ras.add_path(e);
			scanlineRenderer.RenderSolid(clippingProxyNormal, ras, sl, new RGBA_Bytes(127, 127, 127));

			// Creating a rounded rectangle
			VertexSource.RoundedRect r = new VertexSource.RoundedRect(m_x[0], m_y[0], m_x[1], m_y[1], 10);
			r.normalize_radius();

			// Drawing as an outline
			Stroke p = new Stroke(r);
			p.width(1.0);
			ras.add_path(p);

			//Renderer.RenderSolid(clippingProxyGamma, ras, sl, new RGBA_Bytes(0, 0, 0));
			scanlineRenderer.RenderSolid(clippingProxyGamma, ras, sl, new RGBA_Bytes(255, 1, 1));

			/*
					int i;

					// radial line test
					//-------------------------
					dashed_line<rasterizer_type,
								renderer_scanline_type,
								scanline_type> dash(ras, ren_sl, sl);

					double cx = width() / 2.0;
					double cy = height() / 2.0;

					ren_sl.color(agg::rgba(1.0, 1.0, 1.0, 0.2));
					for(i = 180; i > 0; i--)
					{
						double n = 2.0 * agg::pi * i / 180.0;
						dash.draw(cx + min(cx, cy) * sin(n), cy + min(cx, cy) * cos(n),
								  cx, cy,
								  1.0, (i < 90) ? i : 0.0);
					}

					typedef agg::gradient_x gradient_func_type;
					typedef agg::span_interpolator_linear<> interpolator_type;
					typedef agg::span_allocator<color_type> span_allocator_type;
					typedef agg::pod_auto_array<color_type, 256> color_array_type;
					typedef agg::span_gradient<color_type,
											   interpolator_type,
											   gradient_func_type,
											   color_array_type> span_gradient_type;

					typedef agg::renderer_scanline_aa<renderer_base_type,
													  span_allocator_type,
													  span_gradient_type> renderer_gradient_type;

					gradient_func_type  gradient_func;                   // The gradient function
					agg::trans_affine   gradient_mtx;                    // Affine transformer
					interpolator_type   span_interpolator(gradient_mtx); // Span interpolator
					span_allocator_type span_allocator;                  // Span Allocator
					color_array_type    gradient_colors;                 // The gradient colors
					span_gradient_type  span_gradient(span_interpolator,
													  gradient_func,
													  gradient_colors,
													  0, 100);

					renderer_gradient_type ren_gradient(ren_base, span_allocator, span_gradient);

					dashed_line<rasterizer_type,
								renderer_gradient_type,
								scanline_type> dash_gradient(ras, ren_gradient, sl);

					double x1, y1, x2, y2;

					for(i = 1; i <= 20; i++)
					{
						ren_sl.color(agg::rgba(1,1,1));

						// integral point sizes 1..20
						//----------------
						agg::ellipse ell;

						ell.init(20 + i * (i + 1) + 0.5,
								 20.5,
								 i / 2.0,
								 i / 2.0,
								 8 + i);
						ras.reset();
						ras.add_path(ell);
						agg::render_scanlines(ras, sl, ren_sl);

						// fractional point sizes 0..2
						//----------------
						ell.init(18 + i * 4 + 0.5, 33 + 0.5,
								 i/20.0, i/20.0,
								 8);
						ras.reset();
						ras.add_path(ell);
						agg::render_scanlines(ras, sl, ren_sl);

						// fractional point positioning
						//---------------
						ell.init(18 + i * 4 + (i-1) / 10.0 + 0.5,
								 27 + (i - 1) / 10.0 + 0.5,
								 0.5, 0.5, 8);
						ras.reset();
						ras.add_path(ell);
						agg::render_scanlines(ras, sl, ren_sl);

						// integral line widths 1..20
						//----------------
						fill_color_array(gradient_colors,
										 agg::rgba(1,1,1),
										 agg::rgba(i % 2, (i % 3) * 0.5, (i % 5) * 0.25));

						x1 = 20 + i* (i + 1);
						y1 = 40.5;
						x2 = 20 + i * (i + 1) + (i - 1) * 4;
						y2 = 100.5;
						calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
						dash_gradient.draw(x1, y1, x2, y2, i, 0);

						fill_color_array(gradient_colors,
										 agg::rgba(1,0,0),
										 agg::rgba(0,0,1));

						// fractional line lengths H (red/blue)
						//----------------
						x1 = 17.5 + i * 4;
						y1 = 107;
						x2 = 17.5 + i * 4 + i/6.66666667;
						y2 = 107;
						calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
						dash_gradient.draw(x1, y1, x2, y2, 1.0, 0);

						// fractional line lengths V (red/blue)
						//---------------
						x1 = 18 + i * 4;
						y1 = 112.5;
						x2 = 18 + i * 4;
						y2 = 112.5 + i / 6.66666667;
						calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
						dash_gradient.draw(x1, y1, x2, y2, 1.0, 0);

						// fractional line positioning (red)
						//---------------
						fill_color_array(gradient_colors,
										 agg::rgba(1,0,0),
										 agg::rgba(1,1,1));
						x1 = 21.5;
						y1 = 120 + (i - 1) * 3.1;
						x2 = 52.5;
						y2 = 120 + (i - 1) * 3.1;
						calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
						dash_gradient.draw(x1, y1, x2, y2, 1.0, 0);

						// fractional line width 2..0 (green)
						fill_color_array(gradient_colors,
										 agg::rgba(0,1,0),
										 agg::rgba(1,1,1));
						x1 = 52.5;
						y1 = 118 + i * 3;
						x2 = 83.5;
						y2 = 118 + i * 3;
						calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
						dash_gradient.draw(x1, y1, x2, y2, 2.0 - (i - 1) / 10.0, 0);

						// stippled fractional width 2..0 (blue)
						fill_color_array(gradient_colors,
										 agg::rgba(0,0,1),
										 agg::rgba(1,1,1));
						x1 = 83.5;
						y1 = 119 + i * 3;
						x2 = 114.5;
						y2 = 119 + i * 3;
						calc_linear_gradient_transform(x1, y1, x2, y2, gradient_mtx);
						dash_gradient.draw(x1, y1, x2, y2, 2.0 - (i - 1) / 10.0, 3.0);

						ren_sl.color(agg::rgba(1,1,1));
						if(i <= 10)
						{
							// integral line width, horz aligned (mipmap test)
							//-------------------
							dash.draw(125.5, 119.5 + (i + 2) * (i / 2.0),
									  135.5, 119.5 + (i + 2) * (i / 2.0),
									  i, 0.0);
						}

						// fractional line width 0..2, 1 px H
						//-----------------
						dash.draw(17.5 + i * 4, 192, 18.5 + i * 4, 192, i / 10.0, 0);

						// fractional line positioning, 1 px H
						//-----------------
						dash.draw(17.5 + i * 4 + (i - 1) / 10.0, 186,
								  18.5 + i * 4 + (i - 1) / 10.0, 186,
								  1.0, 0);
					}

					// Triangles
					//---------------
					for (int i = 1; i <= 13; i++)
					{
						fill_color_array(gradient_colors,
										 agg::rgba(1,1,1),
										 agg::rgba(i % 2, (i % 3) * 0.5, (i % 5) * 0.25));
						calc_linear_gradient_transform(width()  - 150,
													   height() - 20 - i * (i + 1.5),
													   width()  - 20,
													   height() - 20 - i * (i + 1),
													   gradient_mtx);
						ras.reset();
						ras.move_to_d(width() - 150, height() - 20 - i * (i + 1.5));
						ras.line_to_d(width() - 20,  height() - 20 - i * (i + 1));
						ras.line_to_d(width() - 20,  height() - 20 - i * (i + 2));
						agg::render_scanlines(ras, sl, ren_gradient);
					}
			 */

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				for (int i = 0; i < 2; i++)
				{
					double x = mouseEvent.X;
					double y = mouseEvent.Y;
					if (Math.Sqrt((x - m_x[i]) * (x - m_x[i]) + (y - m_y[i]) * (y - m_y[i])) < 5.0)
					{
						m_dx = x - m_x[i];
						m_dy = y - m_y[i];
						m_idx = i;
						break;
					}
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				if (m_idx >= 0)
				{
					m_x[m_idx] = mouseEvent.X - m_dx;
					m_y[m_idx] = mouseEvent.Y - m_dy;
					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		override public void OnMouseUp(MouseEventArgs mouseEvent)
		{
			m_idx = -1;
			base.OnMouseUp(mouseEvent);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			AppWidgetFactory appWidget = new AATestFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}
	}

	public class AATestFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new aa_test();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
				"Vector",
				"Anit-Alias Test",
				"A test of Anti-Aliasing the same as in http://homepage.mac.com/arekkusu/bugs/invariance",
				480,
				350);

			return appWidgetInfo;
		}
	}
}