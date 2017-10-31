using System;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.UI.Examples;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg
{
	public class rounded_rect_application : GuiWidget, IDemoApp
	{
		private double[] m_x = new double[2];
		private double[] m_y = new double[2];
		private double m_dx;
		private double m_dy;
		private int m_idx;
		private MatterHackers.Agg.UI.Slider m_radius;
		private MatterHackers.Agg.UI.Slider m_gamma;
		private MatterHackers.Agg.UI.Slider m_offset;
		private MatterHackers.Agg.UI.CheckBox m_white_on_black;
		private MatterHackers.Agg.UI.CheckBox m_DrawAsOutlineCheckBox;

		public rounded_rect_application()
		{
			AnchorAll();
			m_idx = (-1);
			m_radius = new MatterHackers.Agg.UI.Slider(new Vector2(10, 10), new Vector2(580, 9));
			m_gamma = new MatterHackers.Agg.UI.Slider(new Vector2(10, 10 + 40), new Vector2(580, 9));
			m_offset = new MatterHackers.Agg.UI.Slider(new Vector2(10, 10 + 80), new Vector2(580, 9));
			m_white_on_black = new CheckBox(10, 10 + 60, "White on black");
			m_DrawAsOutlineCheckBox = new CheckBox(10 + 180, 10 + 60, "Fill Rounded Rect");

			m_radius.ValueChanged += new EventHandler(NeedsRedraw);
			m_gamma.ValueChanged += new EventHandler(NeedsRedraw);
			m_offset.ValueChanged += new EventHandler(NeedsRedraw);
			m_white_on_black.CheckedStateChanged += NeedsRedraw;
			m_DrawAsOutlineCheckBox.CheckedStateChanged += NeedsRedraw;

			m_x[0] = 100; m_y[0] = 100;
			m_x[1] = 500; m_y[1] = 350;
			AddChild(m_radius);
			AddChild(m_gamma);
			AddChild(m_offset);
			AddChild(m_white_on_black);
			AddChild(m_DrawAsOutlineCheckBox);
			m_gamma.Text = "gamma={0:F3}";
			m_gamma.SetRange(0.0, 3.0);
			m_gamma.Value = 1.8;

			m_radius.Text = "radius={0:F3}";
			m_radius.SetRange(0.0, 50.0);
			m_radius.Value = 25.0;

			m_offset.Text = "subpixel offset={0:F3}";
			m_offset.SetRange(-2.0, 3.0);

			m_white_on_black.TextColor = new Color(127, 127, 127);
			//m_white_on_black.inactive_color(new RGBA_Bytes(127, 127, 127));

			m_DrawAsOutlineCheckBox.TextColor = new ColorF(.5, .5, .5).GetAsRGBA_Bytes();
			//m_DrawAsOutlineCheckBox.inactive_color(new RGBA_Bytes(127, 127, 127));
		}

		public string Title { get; } = "Rounded Rect";

		public string DemoCategory { get; } = "Vector";

		public string DemoDescription { get; } = "Yet another example dedicated to Gamma Correction. If you have a CRT monitor: The rectangle looks bad - "
				+ " the rounded corners are thicker than its side lines. First try to drag the “subpixel offset” control "
				+ "— it simply adds some fractional value to the coordinates. When dragging you will see that the rectangle"
				+ "is 'blinking'. Then increase 'Gamma' to about 1.5. The result will look almost perfect — the visual "
				+ "thickness of the rectangle remains the same. That's good, but turn the checkbox 'White on black' on — what "
				+ "do we see? Our rounded rectangle looks terrible. Drag the 'subpixel offset' slider — it's blinking as hell."
				+ "Now decrease 'Gamma' to about 0.6. What do we see now? Perfect result! If you use an LCD monitor, the good "
				+ "value of gamma will be closer to 1.0 in both cases — black on white or white on black. There's no "
				+ "perfection in this world, but at least you can control Gamma in Anti-Grain Geometry :-).";

		private void NeedsRedraw(object sender, EventArgs e)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

			IImageByte backBuffer = widgetsSubImage;

			GammaLookUpTable gamma = new GammaLookUpTable(m_gamma.Value);
			IRecieveBlenderByte NormalBlender = new BlenderBGRA();
			IRecieveBlenderByte GammaBlender = new BlenderGammaBGRA(gamma);
			ImageBuffer rasterNormal = new ImageBuffer();
			rasterNormal.Attach(backBuffer, NormalBlender);
			ImageBuffer rasterGamma = new ImageBuffer();
			rasterGamma.Attach(backBuffer, GammaBlender);
			ImageClippingProxy clippingProxyNormal = new ImageClippingProxy(rasterNormal);
			ImageClippingProxy clippingProxyGamma = new ImageClippingProxy(rasterGamma);

			clippingProxyNormal.clear(m_white_on_black.Checked ? new ColorF(0, 0, 0) : new ColorF(1, 1, 1));

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
			scanlineRenderer.RenderSolid(clippingProxyNormal, ras, sl, new Color(127, 127, 127));
			e.init(m_x[1], m_y[1], 3, 3, 16);
			ras.add_path(e);
			scanlineRenderer.RenderSolid(clippingProxyNormal, ras, sl, new Color(127, 127, 127));

			double d = m_offset.Value;

			// Creating a rounded rectangle
			VertexSource.RoundedRect r = new VertexSource.RoundedRect(m_x[0] + d, m_y[0] + d, m_x[1] + d, m_y[1] + d, m_radius.Value);
			r.normalize_radius();

			// Drawing as an outline
			if (!m_DrawAsOutlineCheckBox.Checked)
			{
				Stroke p = new Stroke(r);
				p.width(1.0);
				ras.add_path(p);
			}
			else
			{
				ras.add_path(r);
			}

			scanlineRenderer.RenderSolid(clippingProxyGamma, ras, sl, m_white_on_black.Checked ? new Color(255, 255, 255) : new Color(0, 0, 0));

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
			var demoWidget = new rounded_rect_application();

			var systemWindow = new SystemWindow(600, 400);
			systemWindow.Title = demoWidget.Title;
			systemWindow.AddChild(demoWidget);
			systemWindow.ShowAsSystemWindow();
		}
	}
}