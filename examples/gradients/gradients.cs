using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using MatterHackers.Agg.UI.Examples;

namespace MatterHackers.Agg
{
	public struct color_function_profile : IColorFunction
	{
		public color_function_profile(RGBA_Bytes[] colors, byte[] profile)
		{
			m_colors = colors;
			m_profile = profile;
		}

		public int size()
		{
			return 256;
		}

		public RGBA_Bytes this[int v]
		{
			get
			{
				return m_colors[m_profile[v]];
			}
		}

		private RGBA_Bytes[] m_colors;
		private byte[] m_profile;
	};

	public class Gradients : GuiWidget, IDemoApp
	{
		private double center_x = 350;
		private double center_y = 280;

		private gamma_ctrl m_profile;
		private spline_ctrl m_spline_r;
		private spline_ctrl m_spline_g;
		private spline_ctrl m_spline_b;
		private spline_ctrl m_spline_a;
		private UI.RadioButtonGroup m_GradTypeRBox;
		private UI.RadioButtonGroup m_GradWrapRBox;

		private double m_pdx;
		private double m_pdy;

		private class SaveData
		{
			internal double m_center_x;
			internal double m_center_y;
			internal double m_scale;
			internal double m_angle;
			internal double[] m_splineRArray = new double[10];
			internal double[] m_splineGArray = new double[10];
			internal double[] m_splineBArray = new double[10];
			internal double[] m_splineAArray = new double[10];
			internal double[] m_profileArray = new double[4];
		};

		private SaveData m_SaveData = new SaveData();

		private double m_prev_scale;
		private double m_prev_angle;
		private double m_scale_x;
		private double m_prev_scale_x;
		private double m_scale_y;
		private double m_prev_scale_y;
		private bool m_mouse_move;

		public Gradients()
		{
			AnchorAll();
			//m_profile = new gamma_ctrl(new Vector2(10.0, 10.0), new Vector2(190, 155.0));
			//m_spline_r = new spline_ctrl(new Vector2(210, 10), new Vector2(250, 5 + 30), 6);
			//m_spline_g = new spline_ctrl(new Vector2(210, 10 + 40), new Vector2(250, 5 + 30), 6);
			//m_spline_b = new spline_ctrl(new Vector2(210, 10 + 80), new Vector2(250, 5 + 30), 6);
			//m_spline_a = new spline_ctrl(new Vector2(210, 10 + 120), new Vector2(250, 5 + 30), 6);
			m_profile = new gamma_ctrl(new Vector2(10.0, 10.0), new Vector2(190, 155.0));
			m_spline_r = new spline_ctrl(new Vector2(210, 10), new Vector2(250, 5 + 30), 6);
			m_spline_g = new spline_ctrl(new Vector2(210, 10 + 40), new Vector2(250, 5 + 30), 6);
			m_spline_b = new spline_ctrl(new Vector2(210, 10 + 80), new Vector2(250, 5 + 30), 6);
			m_spline_a = new spline_ctrl(new Vector2(210, 10 + 120), new Vector2(250, 5 + 30), 6);
			m_profile.MouseMove += NeedRedraw;
			m_spline_r.MouseMove += NeedRedraw;
			m_spline_g.MouseMove += NeedRedraw;
			m_spline_b.MouseMove += NeedRedraw;
			m_spline_a.MouseMove += NeedRedraw;
			m_GradTypeRBox = new RadioButtonGroup(new Vector2(10.0, 180.0), new Vector2(190.0, 120.0));
			m_GradWrapRBox = new RadioButtonGroup(new Vector2(10, 310), new Vector2(190, 65));

			m_pdx = (0.0);
			m_pdy = (0.0);
			m_SaveData.m_center_x = (center_x);
			m_SaveData.m_center_y = (center_y);
			m_SaveData.m_scale = (1.0);
			m_prev_scale = (1.0);
			m_SaveData.m_angle = (0.0);
			m_prev_angle = (0.0);
			m_scale_x = (1.0);
			m_prev_scale_x = (1.0);
			m_scale_y = (1.0);
			m_prev_scale_y = (1.0);
			m_mouse_move = (false);

			AddChild(m_profile);
			AddChild(m_spline_r);
			AddChild(m_spline_g);
			AddChild(m_spline_b);
			AddChild(m_spline_a);
			AddChild(m_GradTypeRBox);
			AddChild(m_GradWrapRBox);

			m_profile.border_width(2.0, 2.0);

			m_spline_r.background_color(new RGBA_Bytes(1.0, 0.8, 0.8));
			m_spline_g.background_color(new RGBA_Bytes(0.8, 1.0, 0.8));
			m_spline_b.background_color(new RGBA_Bytes(0.8, 0.8, 1.0));
			m_spline_a.background_color(new RGBA_Bytes(1.0, 1.0, 1.0));

			m_spline_r.border_width(1.0, 2.0);
			m_spline_g.border_width(1.0, 2.0);
			m_spline_b.border_width(1.0, 2.0);
			m_spline_a.border_width(1.0, 2.0);

			m_spline_r.point(0, 0.0, 1.0);
			m_spline_r.point(1, 1.0 / 5.0, 1.0 - 1.0 / 5.0);
			m_spline_r.point(2, 2.0 / 5.0, 1.0 - 2.0 / 5.0);
			m_spline_r.point(3, 3.0 / 5.0, 1.0 - 3.0 / 5.0);
			m_spline_r.point(4, 4.0 / 5.0, 1.0 - 4.0 / 5.0);
			m_spline_r.point(5, 1.0, 0.0);
			m_spline_r.update_spline();

			m_spline_g.point(0, 0.0, 1.0);
			m_spline_g.point(1, 1.0 / 5.0, 1.0 - 1.0 / 5.0);
			m_spline_g.point(2, 2.0 / 5.0, 1.0 - 2.0 / 5.0);
			m_spline_g.point(3, 3.0 / 5.0, 1.0 - 3.0 / 5.0);
			m_spline_g.point(4, 4.0 / 5.0, 1.0 - 4.0 / 5.0);
			m_spline_g.point(5, 1.0, 0.0);
			m_spline_g.update_spline();

			m_spline_b.point(0, 0.0, 1.0);
			m_spline_b.point(1, 1.0 / 5.0, 1.0 - 1.0 / 5.0);
			m_spline_b.point(2, 2.0 / 5.0, 1.0 - 2.0 / 5.0);
			m_spline_b.point(3, 3.0 / 5.0, 1.0 - 3.0 / 5.0);
			m_spline_b.point(4, 4.0 / 5.0, 1.0 - 4.0 / 5.0);
			m_spline_b.point(5, 1.0, 0.0);
			m_spline_b.update_spline();

			m_spline_a.point(0, 0.0, 1.0);
			m_spline_a.point(1, 1.0 / 5.0, 1.0);
			m_spline_a.point(2, 2.0 / 5.0, 1.0);
			m_spline_a.point(3, 3.0 / 5.0, 1.0);
			m_spline_a.point(4, 4.0 / 5.0, 1.0);
			m_spline_a.point(5, 1.0, 1.0);
			m_spline_a.update_spline();

			m_GradTypeRBox.AddRadioButton("Circular");
			m_GradTypeRBox.AddRadioButton("Diamond");
			m_GradTypeRBox.AddRadioButton("Linear");
			m_GradTypeRBox.AddRadioButton("XY");
			m_GradTypeRBox.AddRadioButton("sqrt(XY)");
			m_GradTypeRBox.AddRadioButton("Conic");
			m_GradTypeRBox.SelectedIndex = 0;

			m_GradWrapRBox.AddRadioButton("Reflect");
			m_GradWrapRBox.AddRadioButton("Repeat");
			m_GradWrapRBox.AddRadioButton("Clamp");
			m_GradWrapRBox.SelectedIndex = 0;
		}

		public string Title { get; } = "Gradients";

		public string DemoCategory { get; } = "Vector";

		public string DemoDescription { get; } = "This �sphere� is rendered with color gradients only. Initially there was an idea to compensate so called Mach Bands effect. To do so I added a gradient profile functor. Then the concept was extended to set a color profile. As a result you can render simple geometrical objects in 2D looking like 3D ones. In this example you can construct your own color profile and select the gradient function. There're not so many gradient functions in AGG, but you can easily add your own. Also, drag the �gradient� with the left mouse button, scale and rotate it with the right one.";

		private void NeedRedraw(object sender, MouseEventArgs mouseEvent)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

			ScanlineRasterizer ras = new ScanlineRasterizer();
			scanline_unpacked_8 sl = new scanline_unpacked_8();

			ImageClippingProxy clippingProxy = new ImageClippingProxy(widgetsSubImage);
			clippingProxy.clear(new RGBA_Floats(0, 0, 0));

			m_profile.text_size(8.0);

			// draw a background to show how the alpha is working
			int RectWidth = 32;
			int xoffset = 238;
			int yoffset = 171;
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			for (int i = 0; i < 7; i++)
			{
				for (int j = 0; j < 7; j++)
				{
					if ((i + j) % 2 != 0)
					{
						VertexSource.RoundedRect rect = new VertexSource.RoundedRect(i * RectWidth + xoffset, j * RectWidth + yoffset,
							(i + 1) * RectWidth + xoffset, (j + 1) * RectWidth + yoffset, 2);
						rect.normalize_radius();

						ras.add_path(rect);
						scanlineRenderer.RenderSolid(clippingProxy, ras, sl, new RGBA_Bytes(.9, .9, .9));
					}
				}
			}

			double ini_scale = 1.0;

			Transform.Affine mtx1 = Affine.NewIdentity();
			mtx1 *= Affine.NewScaling(ini_scale, ini_scale);
			mtx1 *= Affine.NewTranslation(center_x, center_y);

			VertexSource.Ellipse e1 = new MatterHackers.Agg.VertexSource.Ellipse();
			e1.init(0.0, 0.0, 110.0, 110.0, 64);

			Transform.Affine mtx_g1 = Affine.NewIdentity();
			mtx_g1 *= Affine.NewScaling(ini_scale, ini_scale);
			mtx_g1 *= Affine.NewScaling(m_SaveData.m_scale, m_SaveData.m_scale);
			mtx_g1 *= Affine.NewScaling(m_scale_x, m_scale_y);
			mtx_g1 *= Affine.NewRotation(m_SaveData.m_angle);
			mtx_g1 *= Affine.NewTranslation(m_SaveData.m_center_x, m_SaveData.m_center_y);
			mtx_g1.invert();

			RGBA_Bytes[] color_profile = new RGBA_Bytes[256]; // color_type is defined in pixel_formats.h
			for (int i = 0; i < 256; i++)
			{
				color_profile[i] = new RGBA_Bytes(m_spline_r.spline()[i],
														m_spline_g.spline()[i],
														m_spline_b.spline()[i],
														m_spline_a.spline()[i]);
			}

			VertexSourceApplyTransform t1 = new VertexSourceApplyTransform(e1, mtx1);

			IGradient innerGradient = null;
			switch (m_GradTypeRBox.SelectedIndex)
			{
				case 0:
					innerGradient = new gradient_radial();
					break;

				case 1:
					innerGradient = new gradient_diamond();
					break;

				case 2:
					innerGradient = new gradient_x();
					break;

				case 3:
					innerGradient = new gradient_xy();
					break;

				case 4:
					innerGradient = new gradient_sqrt_xy();
					break;

				case 5:
					innerGradient = new gradient_conic();
					break;
			}

			IGradient outerGradient = null;
			switch (m_GradWrapRBox.SelectedIndex)
			{
				case 0:
					outerGradient = new gradient_reflect_adaptor(innerGradient);
					break;

				case 1:
					outerGradient = new gradient_repeat_adaptor(innerGradient);
					break;

				case 2:
					outerGradient = new gradient_clamp_adaptor(innerGradient);
					break;
			}

			span_allocator span_alloc = new span_allocator();
			color_function_profile colors = new color_function_profile(color_profile, m_profile.gamma());
			span_interpolator_linear inter = new span_interpolator_linear(mtx_g1);
			span_gradient span_gen = new span_gradient(inter, outerGradient, colors, 0, 150);

			ras.add_path(t1);
			scanlineRenderer.GenerateAndRender(ras, sl, clippingProxy, span_alloc, span_gen);
			base.OnDraw(graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (m_mouse_move)
			{
				double x2 = mouseEvent.X;
				double y2 = mouseEvent.Y;

				if (mouseEvent.Button == MouseButtons.Left)
				{
					m_SaveData.m_center_x = x2 + m_pdx;
					m_SaveData.m_center_y = y2 + m_pdy;
					Invalidate();
				}
				else if (mouseEvent.Button == MouseButtons.Right)
				{
					double dx = x2 - m_SaveData.m_center_x;
					double dy = y2 - m_SaveData.m_center_y;
					m_SaveData.m_scale = m_prev_scale *
							  System.Math.Sqrt(dx * dx + dy * dy) /
							  System.Math.Sqrt(m_pdx * m_pdx + m_pdy * m_pdy);

					m_SaveData.m_angle = m_prev_angle + System.Math.Atan2(dy, dx) - System.Math.Atan2(m_pdy, m_pdx);
					Invalidate();
				}
				else if (mouseEvent.Button == MouseButtons.Middle)
				{
					double dx = x2 - m_SaveData.m_center_x;
					double dy = y2 - m_SaveData.m_center_y;
					m_scale_x = m_prev_scale_x * dx / m_pdx;
					m_scale_y = m_prev_scale_y * dy / m_pdy;
					Invalidate();
				}
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (MouseCaptured)
			{
				m_mouse_move = true;
				double x2 = mouseEvent.X;
				double y2 = mouseEvent.Y;

				m_pdx = m_SaveData.m_center_x - x2;
				m_pdy = m_SaveData.m_center_y - y2;
				m_prev_scale = m_SaveData.m_scale;
				m_prev_angle = m_SaveData.m_angle + System.Math.PI;
				m_prev_scale_x = m_scale_x;
				m_prev_scale_y = m_scale_y;
			}
			Invalidate();
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			m_mouse_move = false;
			base.OnMouseUp(mouseEvent);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			var demoWidget = new Gradients();

			var systemWindow = new SystemWindow(512, 400);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}