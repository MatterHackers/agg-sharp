using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg
{
	public class square
	{
		private double m_size;

		public square(double size)
		{
			m_size = size;
		}

		public void draw(ScanlineRasterizer ras, IScanlineCache sl, IImageByte destImage, RGBA_Bytes color,
				  double x, double y)
		{
			ras.reset();
			ras.move_to_d(x * m_size, y * m_size);
			ras.line_to_d(x * m_size + m_size, y * m_size);
			ras.line_to_d(x * m_size + m_size, y * m_size + m_size);
			ras.line_to_d(x * m_size, y * m_size + m_size);
			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderSolid(destImage, ras, sl, color);
		}
	}

	internal class renderer_enlarged : ScanlineRenderer
	{
		private double m_size;
		private square m_square;
		private scanline_unpacked_8 m_sl = new scanline_unpacked_8();

		public renderer_enlarged(double size)
		{
			m_size = size;
			m_square = new square(size);
		}

		protected override void RenderSolidSingleScanLine(IImageByte destImage, IScanlineCache scanLineCache, RGBA_Bytes color)
		{
			int y = scanLineCache.y();
			int num_spans = scanLineCache.num_spans();
			ScanlineSpan scanlineSpan = scanLineCache.begin();

			byte[] ManagedCoversArray = scanLineCache.GetCovers();
			for (; ; )
			{
				int x = scanlineSpan.x;
				int num_pix = scanlineSpan.len;
				int coverIndex = scanlineSpan.cover_index;

				do
				{
					int a = (ManagedCoversArray[coverIndex++] * color.Alpha0To255) >> 8;
					m_square.draw(destImage.NewGraphics2D().Rasterizer, m_sl, destImage,
									new RGBA_Bytes(color.Red0To255, color.Green0To255, color.Blue0To255, a),
									x, y);
					++x;
				}
				while (--num_pix > 0);
				if (--num_spans == 0) break;
				scanlineSpan = scanLineCache.GetNextScanlineSpan();
			}
		}
	}

	public class aa_demo : GuiWidget
	{
		private double[] m_x = new double[3];
		private double[] m_y = new double[3];
		private double m_dx;
		private double m_dy;
		private int m_idx;
		private Slider pixelSizeSlider;
		private Slider gammaSlider;

		public aa_demo()
		{
			m_idx = -1;
			m_x[0] = 57; m_y[0] = 100;
			m_x[1] = 369; m_y[1] = 170;
			m_x[2] = 143; m_y[2] = 310;

			pixelSizeSlider = new Slider(new Vector2(30, 30), 600 - 60);
			gammaSlider = new Slider(new Vector2(30, 70), 600 - 60);

			pixelSizeSlider.ValueChanged += new EventHandler(NeedsRedraw);
			gammaSlider.ValueChanged += new EventHandler(NeedsRedraw);

			AddChild(pixelSizeSlider);
			AddChild(gammaSlider);

			pixelSizeSlider.Text = "Pixel size={0:F3}";
			pixelSizeSlider.SetRange(8, 100);
			pixelSizeSlider.NumTicks = 23;
			pixelSizeSlider.Value = 32;

			gammaSlider.Text = "Gamma={0:F3}";
			gammaSlider.SetRange(0.0, 3.0);
			gammaSlider.Value = 1.0;
		}

		public override void OnParentChanged(EventArgs e)
		{
			AnchorAll();
			base.OnParentChanged(e);
		}

		private void NeedsRedraw(object sender, EventArgs e)
		{
			Invalidate();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

			GammaLookUpTable gamma = new GammaLookUpTable(gammaSlider.Value);
			IRecieveBlenderByte NormalBlender = new BlenderBGRA();
			IRecieveBlenderByte GammaBlender = new BlenderGammaBGRA(gamma);
			ImageBuffer rasterGamma = new ImageBuffer();
			rasterGamma.Attach(widgetsSubImage, GammaBlender);
			ImageClippingProxy clippingProxyNormal = new ImageClippingProxy(widgetsSubImage);
			ImageClippingProxy clippingProxyGamma = new ImageClippingProxy(rasterGamma);

			clippingProxyNormal.clear(new RGBA_Floats(1, 1, 1));

			ScanlineRasterizer rasterizer = new ScanlineRasterizer();
			scanline_unpacked_8 sl = new scanline_unpacked_8();

			int size_mul = (int)pixelSizeSlider.Value;

			renderer_enlarged ren_en = new renderer_enlarged(size_mul);

			rasterizer.reset();
			rasterizer.move_to_d(m_x[0] / size_mul, m_y[0] / size_mul);
			rasterizer.line_to_d(m_x[1] / size_mul, m_y[1] / size_mul);
			rasterizer.line_to_d(m_x[2] / size_mul, m_y[2] / size_mul);
			ren_en.RenderSolid(clippingProxyGamma, rasterizer, sl, RGBA_Bytes.Black);

			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderSolid(clippingProxyGamma, rasterizer, sl, RGBA_Bytes.Black);

			rasterizer.gamma(new gamma_none());

			VertexStorage ps = new VertexStorage();
			Stroke pg = new Stroke(ps);
			pg.width(2);

			ps.remove_all();
			ps.MoveTo(m_x[0], m_y[0]);
			ps.LineTo(m_x[1], m_y[1]);
			ps.LineTo(m_x[2], m_y[2]);
			ps.LineTo(m_x[0], m_y[0]);
			rasterizer.add_path(pg);
			scanlineRenderer.RenderSolid(clippingProxyNormal, rasterizer, sl, new RGBA_Bytes(0, 150, 160, 200));

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				double x = mouseEvent.X;
				double y = mouseEvent.Y;
				int i;
				for (i = 0; i < 3; i++)
				{
					if (Math.Sqrt((x - m_x[i]) * (x - m_x[i]) + (y - m_y[i]) * (y - m_y[i])) < 5.0)
					{
						m_dx = x - m_x[i];
						m_dy = y - m_y[i];
						m_idx = i;
						break;
					}
				}
				if (i == 3)
				{
					if (agg_math.point_in_triangle(m_x[0], m_y[0],
										  m_x[1], m_y[1],
										  m_x[2], m_y[2],
										  x, y))
					{
						m_dx = x - m_x[0];
						m_dy = y - m_y[0];
						m_idx = 3;
					}
				}
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (mouseEvent.Button == MouseButtons.Left)
			{
				double x = mouseEvent.X;
				double y = mouseEvent.Y;
				if (m_idx == 3)
				{
					double dx = x - m_dx;
					double dy = y - m_dy;
					m_x[1] -= m_x[0] - dx;
					m_y[1] -= m_y[0] - dy;
					m_x[2] -= m_x[0] - dx;
					m_y[2] -= m_y[0] - dy;
					m_x[0] = dx;
					m_y[0] = dy;
					Invalidate();
					return;
				}

				if (m_idx >= 0)
				{
					m_x[m_idx] = x - m_dx;
					m_y[m_idx] = y - m_dy;
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
			AppWidgetFactory appWidget = new aa_demoFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}
	}

	public class aa_demoFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new aa_demo();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
			"Vector",
			"Zoomed Anti-Aliasing",
			"Demonstration of the Anti-Aliasing principle with Subpixel Accuracy. The triangle "
					+ "is rendered two times, with its “natural” size (at the bottom-left) and enlarged. "
					+ "To draw the enlarged version there is a special scanline renderer written (see "
					+ "class renderer_enlarged in the source code). You can drag the whole triangle as well "
					+ "as each vertex of it. Also change “Gamma” to see how it affects the quality of Anti-Aliasing.",
			600,
			400);

			return appWidgetInfo;
		}
	}
}