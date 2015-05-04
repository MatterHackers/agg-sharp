using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Diagnostics;

namespace MatterHackers.Agg
{
	public class blur : GuiWidget
	{
		private RadioButtonGroup m_method;
		private Slider m_radius;
		private PolygonEditWidget m_shadow_ctrl;
		private CheckBox m_channel_r;
		private CheckBox m_channel_g;
		private CheckBox m_channel_b;
		private CheckBox m_FlattenCurves;

		private IVertexSource m_path;
		private FlattenCurves m_shape;

		private ScanlineRasterizer m_ras = new ScanlineRasterizer();
		private ScanlineCachePacked8 m_sl;
		private ImageBuffer m_rbuf2;

		//agg::stack_blur    <agg::rgba8, agg::stack_blur_calc_rgb<> >     m_stack_blur;
		private RecursiveBlur m_recursive_blur = new RecursiveBlur(new recursive_blur_calc_rgb());

		private RectangleDouble m_shape_bounds;

		private Stopwatch stopwatch = new Stopwatch();

		public blur()
		{
			m_rbuf2 = new ImageBuffer();
			m_shape_bounds = new RectangleDouble();
			m_method = new RadioButtonGroup(new Vector2(10.0, 10.0), new Vector2(130.0, 60.0));
			m_radius = new Slider(new Vector2(130 + 10.0, 10.0 + 4.0), new Vector2(290, 8.0));
			m_shadow_ctrl = new PolygonEditWidget(4);
			m_channel_r = new CheckBox(10.0, 80.0, "Red");
			m_channel_g = new CheckBox(10.0, 95.0, "Green");
			m_channel_b = new CheckBox(10.0, 110.0, "Blue");
			m_FlattenCurves = new CheckBox(10, 315, "Convert And Flatten Curves");
			m_FlattenCurves.Checked = true;

			AddChild(m_method);
			m_method.AddRadioButton("Stack Blur");
			m_method.AddRadioButton("Recursive Blur");
			m_method.AddRadioButton("Channels");
			m_method.SelectedIndex = 1;

			AddChild(m_radius);
			m_radius.SetRange(0.0, 40.0);
			m_radius.Value = 15.0;
			m_radius.Text = "Blur Radius={0:F2}";

			AddChild(m_shadow_ctrl);

			AddChild(m_channel_r);
			AddChild(m_channel_g);
			AddChild(m_channel_b);
			AddChild(m_FlattenCurves);
			m_channel_g.Checked = true;

			m_sl = new ScanlineCachePacked8();

			StyledTypeFace typeFaceForLargeA = new StyledTypeFace(LiberationSansFont.Instance, 300, flatenCurves: false);
			m_path = typeFaceForLargeA.GetGlyphForCharacter('a');

			Affine shape_mtx = Affine.NewIdentity();
			shape_mtx *= Affine.NewTranslation(150, 100);
			m_path = new VertexSourceApplyTransform(m_path, shape_mtx);
			m_shape = new FlattenCurves(m_path);

			bounding_rect.bounding_rect_single(m_shape, 0, ref m_shape_bounds);

			m_shadow_ctrl.SetXN(0, m_shape_bounds.Left);
			m_shadow_ctrl.SetYN(0, m_shape_bounds.Bottom);
			m_shadow_ctrl.SetXN(1, m_shape_bounds.Right);
			m_shadow_ctrl.SetYN(1, m_shape_bounds.Bottom);
			m_shadow_ctrl.SetXN(2, m_shape_bounds.Right);
			m_shadow_ctrl.SetYN(2, m_shape_bounds.Top);
			m_shadow_ctrl.SetXN(3, m_shape_bounds.Left);
			m_shadow_ctrl.SetYN(3, m_shape_bounds.Top);
			m_shadow_ctrl.line_color(new RGBA_Floats(0, 0.3, 0.5, 0.3));
		}

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			base.OnParentChanged(e);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());
			ImageClippingProxy clippingProxy = new ImageClippingProxy(widgetsSubImage);
			clippingProxy.clear(new RGBA_Floats(1, 1, 1));
			m_ras.SetVectorClipBox(0, 0, Width, Height);

			Affine move = Affine.NewTranslation(10, 10);

			Perspective shadow_persp = new Perspective(m_shape_bounds.Left, m_shape_bounds.Bottom,
												m_shape_bounds.Right, m_shape_bounds.Top,
												m_shadow_ctrl.polygon());

			IVertexSource shadow_trans;
			if (m_FlattenCurves.Checked)
			{
				shadow_trans = new VertexSourceApplyTransform(m_shape, shadow_persp);
			}
			else
			{
				shadow_trans = new VertexSourceApplyTransform(m_path, shadow_persp);
				// this will make it very smooth after the transform
				//shadow_trans = new conv_curve(shadow_trans);
			}

			// Render shadow
			m_ras.add_path(shadow_trans);
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderSolid(clippingProxy, m_ras, m_sl, new RGBA_Floats(0.2, 0.3, 0).GetAsRGBA_Bytes());

			// Calculate the bounding box and extend it by the blur radius
			RectangleDouble bbox = new RectangleDouble();
			bounding_rect.bounding_rect_single(shadow_trans, 0, ref bbox);

			bbox.Left -= m_radius.Value;
			bbox.Bottom -= m_radius.Value;
			bbox.Right += m_radius.Value;
			bbox.Top += m_radius.Value;

			if (m_method.SelectedIndex == 1)
			{
				// The recursive blur method represents the true Gaussian Blur,
				// with theoretically infinite kernel. The restricted window size
				// results in extra influence of edge pixels. It's impossible to
				// solve correctly, but extending the right and top areas to another
				// radius value produces fair result.
				//------------------
				bbox.Right += m_radius.Value;
				bbox.Top += m_radius.Value;
			}

			stopwatch.Restart();

			if (m_method.SelectedIndex != 2)
			{
				// Create a new pixel renderer and attach it to the main one as a child image.
				// It returns true if the attachment succeeded. It fails if the rectangle
				// (bbox) is fully clipped.
				//------------------
#if SourceDepth24
                ImageBuffer image2 = new ImageBuffer(new BlenderBGR());
#else
				ImageBuffer image2 = new ImageBuffer(new BlenderBGRA());
#endif
				if (image2.Attach(widgetsSubImage, (int)bbox.Left, (int)bbox.Bottom, (int)bbox.Right, (int)bbox.Top))
				{
					// Blur it
					if (m_method.SelectedIndex == 0)
					{
						// More general method, but 30-40% slower.
						//------------------
						//m_stack_blur.blur(pixf2, agg::uround(m_radius.Value));

						// Faster, but bore specific.
						// Works only for 8 bits per channel and only with radii <= 254.
						//------------------
						stack_blur test = new stack_blur();
						test.Blur(image2, agg_basics.uround(m_radius.Value), agg_basics.uround(m_radius.Value));
					}
					else
					{
						// True Gaussian Blur, 3-5 times slower than Stack Blur,
						// but still constant time of radius. Very sensitive
						// to precision, doubles are must here.
						//------------------
						m_recursive_blur.blur(image2, m_radius.Value);
					}
				}
			}
			else
			{
				/*
				// Blur separate channels
				//------------------
				if(m_channel_r.Checked)
				{
					typedef agg::pixfmt_alpha_blend_gray<
						agg::blender_gray8,
						agg::rendering_buffer,
						3, 2> pixfmt_gray8r;

					pixfmt_gray8r pixf2r(m_rbuf2);
					if(pixf2r.attach(pixf, int(bbox.x1), int(bbox.y1), int(bbox.x2), int(bbox.y2)))
					{
						agg::stack_blur_gray8(pixf2r, agg::uround(m_radius.Value),
													  agg::uround(m_radius.Value));
					}
				}

				if(m_channel_g.Checked)
				{
					typedef agg::pixfmt_alpha_blend_gray<
						agg::blender_gray8,
						agg::rendering_buffer,
						3, 1> pixfmt_gray8g;

					pixfmt_gray8g pixf2g(m_rbuf2);
					if(pixf2g.attach(pixf, int(bbox.x1), int(bbox.y1), int(bbox.x2), int(bbox.y2)))
					{
						agg::stack_blur_gray8(pixf2g, agg::uround(m_radius.Value),
													  agg::uround(m_radius.Value));
					}
				}

				if(m_channel_b.Checked)
				{
					typedef agg::pixfmt_alpha_blend_gray<
						agg::blender_gray8,
						agg::rendering_buffer,
						3, 0> pixfmt_gray8b;

					pixfmt_gray8b pixf2b(m_rbuf2);
					if(pixf2b.attach(pixf, int(bbox.x1), int(bbox.y1), int(bbox.x2), int(bbox.y2)))
					{
						agg::stack_blur_gray8(pixf2b, agg::uround(m_radius.Value),
													  agg::uround(m_radius.Value));
					}
				}
				 */
			}

			double tm = stopwatch.ElapsedMilliseconds;

			// Render the shape itself
			//------------------
			if (m_FlattenCurves.Checked)
			{
				m_ras.add_path(m_shape);
			}
			else
			{
				m_ras.add_path(m_path);
			}

			scanlineRenderer.RenderSolid(clippingProxy, m_ras, m_sl, new RGBA_Floats(0.6, 0.9, 0.7, 0.8).GetAsRGBA_Bytes());

			graphics2D.DrawString(string.Format("{0:F2} ms", tm), 140, 30);
			base.OnDraw(graphics2D);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			AppWidgetFactory appWidget = new BlurFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}
	}

	public class BlurFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new blur();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
				"Bitmap",
				"Gaussian and Stack Blur",
				@"Now you can blur rendered images rather fast! There two algorithms are used:
Stack Blur by Mario Klingemann and Fast Recursive Gaussian Filter, described
here and here (PDF). The speed of both methods does not depend on the filter radius.
Mario's method works 3-5 times faster; it doesn't produce exactly Gaussian response,
but pretty fair for most practical purposes. The recursive filter uses floating
point arithmetic and works slower. But it is true Gaussian filter, with theoretically
infinite impulse response. The radius (actually 2*sigma value) can be fractional
and the filter produces quite adequate result.",
										   440,
										   330);

			return appWidgetInfo;
		}
	}
}