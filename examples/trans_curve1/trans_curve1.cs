using System;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.Agg
{
	public class trans_curve1_application : GuiWidget, IDemoApp
	{
		private PolygonEditWidget m_poly;
		private Slider m_num_points;
		private CheckBox m_close;
		private CheckBox m_preserve_x_scale;
		private CheckBox m_fixed_len;
		private CheckBox m_animate;
		private double[] m_dx = new double[6];
		private double[] m_dy = new double[6];

		public trans_curve1_application()
		{
			AnchorAll();

			m_poly = new PolygonEditWidget(6, 5);
			on_init();
			m_poly.Changed += NeedsRedraw;
			AddChild(m_poly);

			m_num_points = new MatterHackers.Agg.UI.Slider(5, 5, 340, 12);

			m_num_points.ValueChanged += new EventHandler(NeedsRedraw);

			AddChild(m_num_points);

			m_num_points.Text = "Number of intermediate Points = {0:F3}";
			m_num_points.SetRange(10, 400);
			m_num_points.Value = 200;

			m_close = new CheckBox(350, 5.0, "Close");
			m_close.CheckedStateChanged += NeedsRedraw;
			AddChild(m_close);

			m_preserve_x_scale = new CheckBox(460, 5, "Preserve X scale");
			m_preserve_x_scale.Checked = true;
			m_preserve_x_scale.CheckedStateChanged += NeedsRedraw;
			AddChild(m_preserve_x_scale);

			m_fixed_len = new CheckBox(350, 25, "Fixed Length");
			m_fixed_len.Checked = true;
			m_fixed_len.CheckedStateChanged += NeedsRedraw;
			AddChild(m_fixed_len);

			m_animate = new CheckBox(460, 25, "Animate");
			m_animate.CheckedStateChanged += m_animate_CheckedStateChanged;
			AddChild(m_animate);
		}

		public string Title { get; } = "Trans Curve1";

		public string DemoCategory { get; } = "Vector";

		public string DemoDescription { get; } = "Text follows a curve through six points (trans_single_path). Drag a point, a line or the whole curve.";

		private void on_init()
		{
			m_poly.SetXN(0, 50);
			m_poly.SetYN(0, 50);
			m_poly.SetXN(1, 150 + 20);
			m_poly.SetYN(1, 150 - 20);
			m_poly.SetXN(2, 250 - 20);
			m_poly.SetYN(2, 250 + 20);
			m_poly.SetXN(3, 350 + 20);
			m_poly.SetYN(3, 350 - 20);
			m_poly.SetXN(4, 450 - 20);
			m_poly.SetYN(4, 450 + 20);
			m_poly.SetXN(5, 550);
			m_poly.SetYN(5, 550);
		}

		private Random rand = new Random();

		private void m_animate_CheckedStateChanged(object sender, EventArgs e)
		{
			if (m_animate.Checked)
			{
				on_init();
				int i;
				for (i = 0; i < 6; i++)
				{
					m_dx[i] = ((rand.Next() % 1000) - 500) * 0.01;
					m_dy[i] = ((rand.Next() % 1000) - 500) * 0.01;
				}

				Invalidate();

				UiThread.RunOnIdle(guiSurface_Idle);
			}
		}

		private void NeedsRedraw(object sender, EventArgs e)
		{
			Invalidate();
		}

		private const string Text =
			"Anti-Grain Geometry is designed as a set of loosely coupled "
			+ "algorithms and class templates united with a common idea, "
			+ "so that all the components can be easily combined. Also, "
			+ "the template based design allows you to replace any part of "
			+ "the library without the necessity to modify a single byte in "
			+ "the existing code. ";

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Clear(Color.White);

			m_poly.close(m_close.Checked);
			var path = new VertexStorage();
			for (int i = 0; i < m_poly.num_points(); i++)
			{
				if (i == 0)
				{
					path.MoveTo(m_poly.GetXN(i), m_poly.GetYN(i));
				}
				else
				{
					path.LineTo(m_poly.GetXN(i), m_poly.GetYN(i));
				}
			}

			path.EndPoly(m_close.Checked ? FlagsAndCommand.FlagClose : FlagsAndCommand.FlagNone);

			var bspline = new BSplinePath(path) { InterpolationStep = 1.0 / m_num_points.Value };

			var tcurve = new TransSinglePath();
			tcurve.AddPath(bspline);
			tcurve.PreserveXScale = m_preserve_x_scale.Checked;
			if (m_fixed_len.Checked)
			{
				tcurve.BaseLength = 1120;
			}

			// The C++ demo draws a Win32 TrueType font; gsv_text stroked into outlines stands in for it. Each glyph
			// is cut into short pieces (Segmentator) so the transform can bend its straight edges.
			var glyph = new gsv_text();
			glyph.size(24.0, 18.0);
			var outline = new Stroke(glyph, 2.0) { LineJoin = LineJoin.Round, LineCap = LineCap.Round };
			var bent = new VertexSourceApplyTransform(new Segmentator(outline) { ApproximationScale = 3.0 }, tcurve);

			double x = 0.0;
			foreach (char character in Text)
			{
				if (x > tcurve.TotalLength)
				{
					break;
				}

				glyph.text(character.ToString());
				glyph.start_point(x, 3.0);
				graphics2D.Render(bent, Color.Black);

				// gsv_text's last vertex is where its pen ends up: the next glyph's start. Rewinding does not move
				// the pen back, so it is put back at the glyph's start before the glyph is walked again.
				glyph.start_point(x, 3.0);
				foreach (VertexData vertex in glyph.Vertices())
				{
					if (vertex.IsVertex)
					{
						x = vertex.Position.X;
					}
				}
			}

			graphics2D.Render(new Stroke(bspline, 2.0), new Color(170, 50, 20, 100));

			base.OnDraw(graphics2D);
		}

		private void move_point(ref double x, ref double y, ref double dx, ref double dy)
		{
			if (x < 0.0) { x = 0.0; dx = -dx; }
			if (x > this.Width) { x = this.Width; dx = -dx; }
			if (y < 0.0) { y = 0.0; dy = -dy; }
			if (y > this.Height) { y = this.Height; dy = -dy; }
			x += dx;
			y += dy;
		}

		private void guiSurface_Idle()
		{
			int i;
			for (i = 0; i < 6; i++)
			{
				double x = m_poly.GetXN(i);
				double y = m_poly.GetYN(i);
				move_point(ref x, ref y, ref m_dx[i], ref m_dy[i]);
				m_poly.SetXN(i, x);
				m_poly.SetYN(i, y);
			}
			Invalidate();

			if (m_animate.Checked)
			{
				UiThread.RunOnIdle(guiSurface_Idle);
			}
		}

		[STAThread]
		public static void Main(string[] args)
		{
			var demoWidget = new trans_curve1_application();

			var systemWindow = new SystemWindow(600, 600);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}