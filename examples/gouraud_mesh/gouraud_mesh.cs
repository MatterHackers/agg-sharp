using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using System;
using System.Collections.Generic;
using System.Diagnostics;

namespace MatterHackers.Agg
{
	public class gouraud_mesh_application : GuiWidget
	{
		private Stopwatch stopwatch = new Stopwatch();

		public struct mesh_point
		{
			public double x, y;
			public double dx, dy;
			public RGBA_Bytes color;
			public RGBA_Bytes dc;

			public mesh_point(double x_, double y_,
					   double dx_, double dy_,
					   RGBA_Bytes c, RGBA_Bytes dc_)
			{
				x = (x_);
				y = (y_);
				dx = (dx_);
				dy = (dy_);
				color = (c);
				dc = (dc_);
			}
		};

		public struct mesh_triangle
		{
			public int p1, p2, p3;

			public mesh_triangle(int i, int j, int k)
			{
				p1 = (i);
				p2 = (j);
				p3 = (k);
			}
		};

		public struct mesh_edge
		{
			public int p1, p2;
			public int tl, tr;

			public mesh_edge(int p1_, int p2_, int tl_, int tr_)
			{
				p1 = (p1_);
				p2 = (p2_);
				tl = (tl_);
				tr = (tr_);
			}
		};

		private static System.Random rand = new Random();

		private static double random(double v1, double v2)
		{
			return (v2 - v1) * (rand.Next() % 1000) / 999.0 + v1;
		}

		public class mesh_ctrl
		{
			private int m_cols;
			private int m_rows;
			private int m_drag_idx;
			private double m_drag_dx;
			private double m_drag_dy;
			private double m_cell_w;
			private double m_cell_h;
			private double m_start_x;
			private double m_start_y;
			private VectorPOD<mesh_point> m_vertices = new VectorPOD<mesh_point>();
			private VectorPOD<mesh_triangle> m_triangles = new VectorPOD<mesh_triangle>();
			private VectorPOD<mesh_edge> m_edges = new VectorPOD<mesh_edge>();

			public mesh_ctrl()
			{
				m_cols = (0);
				m_rows = (0);
				m_drag_idx = (-1);
				m_drag_dx = (0);
				m_drag_dy = (0);
			}

			public void generate(int cols, int rows,
									 double cell_w, double cell_h,
									 double start_x, double start_y)
			{
				m_cols = cols;
				m_rows = rows;
				m_cell_w = cell_w;
				m_cell_h = cell_h;
				m_start_x = start_x;
				m_start_y = start_y;

				m_vertices.remove_all();
				for (int i = 0; i < m_rows; i++)
				{
					double x = start_x;
					for (int j = 0; j < m_cols; j++)
					{
						double dx = random(-0.5, 0.5);
						double dy = random(-0.5, 0.5);
						RGBA_Bytes c = new RGBA_Bytes(rand.Next() & 0xFF, rand.Next() & 0xFF, rand.Next() & 0xFF);
						RGBA_Bytes dc = new RGBA_Bytes(rand.Next() & 1, rand.Next() & 1, rand.Next() & 1);
						m_vertices.add(new mesh_point(x, start_y, dx, dy, c, dc));
						x += cell_w;
					}
					start_y += cell_h;
				}

				//  4---3
				//  |t2/|
				//  | / |
				//  |/t1|
				//  1---2
				m_triangles.remove_all();
				m_edges.remove_all();
				for (int i = 0; i < m_rows - 1; i++)
				{
					for (int j = 0; j < m_cols - 1; j++)
					{
						int p1 = i * m_cols + j;
						int p2 = p1 + 1;
						int p3 = p2 + m_cols;
						int p4 = p1 + m_cols;
						m_triangles.add(new mesh_triangle((int)p1, (int)p2, (int)p3));
						m_triangles.add(new mesh_triangle((int)p3, (int)p4, (int)p1));

						int curr_cell = i * (m_cols - 1) + j;
						int left_cell = j != 0 ? (int)(curr_cell - 1) : -1;
						int bott_cell = i != 0 ? (int)(curr_cell - (m_cols - 1)) : -1;

						int curr_t1 = curr_cell * 2;
						int curr_t2 = curr_t1 + 1;

						int left_t1 = (left_cell >= 0) ? left_cell * 2 : -1;
						int left_t2 = (left_cell >= 0) ? left_t1 + 1 : -1;

						int bott_t1 = (bott_cell >= 0) ? bott_cell * 2 : -1;
						int bott_t2 = (bott_cell >= 0) ? bott_t1 + 1 : -1;

						m_edges.add(new mesh_edge((int)p1, (int)p2, curr_t1, bott_t2));
						m_edges.add(new mesh_edge((int)p1, (int)p3, curr_t2, curr_t1));
						m_edges.add(new mesh_edge((int)p1, (int)p4, left_t1, curr_t2));

						if (j == m_cols - 2) // Last column
						{
							m_edges.add(new mesh_edge((int)p2, (int)p3, curr_t1, -1));
						}

						if (i == m_rows - 2) // Last row
						{
							m_edges.add(new mesh_edge((int)p3, (int)p4, curr_t2, -1));
						}
					}
				}
			}

			public void randomize_points(double delta)
			{
				int i, j;
				for (i = 0; i < m_rows; i++)
				{
					for (j = 0; j < m_cols; j++)
					{
						double xc = j * m_cell_w + m_start_x;
						double yc = i * m_cell_h + m_start_y;
						double x1 = xc - m_cell_w / 4;
						double y1 = yc - m_cell_h / 4;
						double x2 = xc + m_cell_w / 4;
						double y2 = yc + m_cell_h / 4;
						mesh_point p = vertex(j, i);
						p.x += p.dx;
						p.y += p.dy;
						if (p.x < x1) { p.x = x1; p.dx = -p.dx; }
						if (p.y < y1) { p.y = y1; p.dy = -p.dy; }
						if (p.x > x2) { p.x = x2; p.dx = -p.dx; }
						if (p.y > y2) { p.y = y2; p.dy = -p.dy; }
					}
				}
			}

			public void rotate_colors()
			{
				int i;
				for (i = 1; i < m_vertices.size(); i++)
				{
					RGBA_Bytes c = m_vertices[i].color;
					RGBA_Bytes dc = m_vertices[i].dc;
					int r = (int)c.Red0To255 + (dc.Red0To255 != 0 ? 5 : -5);
					int g = (int)c.Green0To255 + (dc.Green0To255 != 0 ? 5 : -5);
					int b = (int)c.Blue0To255 + (dc.Blue0To255 != 0 ? 5 : -5);
					if (r < 0) { r = 0; dc.Red0To255 ^= 1; } if (r > 255) { r = 255; dc.Red0To255 ^= 1; }
					if (g < 0) { g = 0; dc.Green0To255 ^= 1; } if (g > 255) { g = 255; dc.Green0To255 ^= 1; }
					if (b < 0) { b = 0; dc.Blue0To255 ^= 1; } if (b > 255) { b = 255; dc.Blue0To255 ^= 1; }
					c.Red0To255 = (int)r;
					c.Green0To255 = (int)g;
					c.Blue0To255 = (int)b;
				}
			}

			public bool OnMouseDown(MouseEventArgs mouseEvent)
			{
				double x = mouseEvent.X;
				double y = mouseEvent.Y;
				if (mouseEvent.Button == MouseButtons.Left)
				{
					int i;
					for (i = 0; i < m_vertices.size(); i++)
					{
						if (agg_math.calc_distance(x, y, m_vertices[i].x, m_vertices[i].y) < 5)
						{
							m_drag_idx = i;
							m_drag_dx = x - m_vertices[i].x;
							m_drag_dy = y - m_vertices[i].y;
							return true;
						}
					}
				}
				return false;
			}

			public bool OnMouseMove(MouseEventArgs mouseEvent)
			{
				double x = mouseEvent.X;
				double y = mouseEvent.Y;
				if (mouseEvent.Button == MouseButtons.Left)
				{
					if (m_drag_idx >= 0)
					{
						m_vertices.Array[m_drag_idx].x = x - m_drag_dx;
						m_vertices.Array[m_drag_idx].y = y - m_drag_dy;
						return true;
					}
				}

				return false;
			}

			public bool OnMouseUp(MouseEventArgs mouseEvent)
			{
				bool ret = m_drag_idx >= 0;
				m_drag_idx = -1;
				return ret;
			}

			public int num_vertices()
			{
				return m_vertices.size();
			}

			public mesh_point vertex(int i)
			{
				return m_vertices[i];
			}

			public mesh_point vertex(int x, int y)
			{
				return m_vertices[(int)y * m_rows + (int)x];
			}

			public int num_triangles()
			{
				return m_triangles.size();
			}

			public mesh_triangle triangle(int i)
			{
				return m_triangles[i];
			}

			public int num_edges()
			{
				return m_edges.size();
			}

			public mesh_edge edge(int i)
			{
				return m_edges[i];
			}
		}

		public class styles_gouraud : IStyleHandler
		{
			private List<span_gouraud_rgba> m_triangles = new List<span_gouraud_rgba>();

			public styles_gouraud(mesh_ctrl mesh, GammaLookUpTable gamma)
			{
				int i;
				for (i = 0; i < mesh.num_triangles(); i++)
				{
					mesh_triangle t = mesh.triangle(i);
					mesh_point p1 = mesh.vertex(t.p1);
					mesh_point p2 = mesh.vertex(t.p2);
					mesh_point p3 = mesh.vertex(t.p3);

					RGBA_Bytes c1 = p1.color;
					RGBA_Bytes c2 = p2.color;
					RGBA_Bytes c3 = p3.color;
					c1.apply_gamma_dir(gamma);
					c2.apply_gamma_dir(gamma);
					c3.apply_gamma_dir(gamma);
					span_gouraud_rgba gouraud = new span_gouraud_rgba(c1, c2, c3,
										 p1.x, p1.y,
										 p2.x, p2.y,
										 p3.x, p3.y);
					gouraud.prepare();
					m_triangles.Add(gouraud);
				}
			}

			public bool is_solid(int style)
			{
				return false;
			}

			public RGBA_Bytes color(int style)
			{
				return new RGBA_Bytes(0, 0, 0, 0);
			}

			public void generate_span(RGBA_Bytes[] span, int spanIndex, int x, int y, int len, int style)
			{
				m_triangles[style].generate(span, spanIndex, x, y, len);
			}
		};

		private mesh_ctrl m_mesh = new mesh_ctrl();
		private GammaLookUpTable m_gamma = new GammaLookUpTable();

		public gouraud_mesh_application()
		{
			AnchorAll();
			//        m_gamma.gamma(2.0);
			m_mesh.generate(20, 20, 17, 17, 40, 40);
			UiThread.RunOnIdle(OnIdle);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageBuffer widgetsSubImage = ImageBuffer.NewSubImageReference(graphics2D.DestImage, graphics2D.GetClippingRect());

			IImageByte backBuffer = widgetsSubImage;

			IImageByte destImage = backBuffer;
			ImageClippingProxy clippingProxy = new ImageClippingProxy(destImage);
			clippingProxy.clear(new RGBA_Floats(0, 0, 0));

			ScanlineRasterizer ras = new ScanlineRasterizer();
			scanline_unpacked_8 sl = new scanline_unpacked_8();
			scanline_bin sl_bin = new scanline_bin();

			rasterizer_compound_aa rasc = new rasterizer_compound_aa();
			span_allocator alloc = new span_allocator();

			int i;
			styles_gouraud styles = new styles_gouraud(m_mesh, m_gamma);
			stopwatch.Restart();
			rasc.reset();
			//rasc.clip_box(40, 40, width() - 40, height() - 40);
			for (i = 0; i < m_mesh.num_edges(); i++)
			{
				mesh_edge e = m_mesh.edge(i);
				mesh_point p1 = m_mesh.vertex(e.p1);
				mesh_point p2 = m_mesh.vertex(e.p2);
				rasc.styles(e.tl, e.tr);
				rasc.move_to_d(p1.x, p1.y);
				rasc.line_to_d(p2.x, p2.y);
			}

			ScanlineRenderer scanlineRenderer = new ScanlineRenderer();
			scanlineRenderer.RenderCompound(rasc, sl, sl_bin, clippingProxy, alloc, styles);
			double tm = stopwatch.ElapsedMilliseconds;

			gsv_text t = new gsv_text();
			t.SetFontSize(10.0);

			Stroke pt = new Stroke(t);
			pt.width(1.5);
			pt.line_cap(LineCap.Round);
			pt.line_join(LineJoin.Round);

			string buf = string.Format("{0:F2} ms, {1} triangles, {2:F0} tri/sec",
				tm,
				m_mesh.num_triangles(),
				m_mesh.num_triangles() / tm * 1000.0);
			t.start_point(10.0, 10.0);
			t.text(buf);

			ras.add_path(pt);
			scanlineRenderer.RenderSolid(clippingProxy, ras, sl, new RGBA_Bytes(255, 255, 255));

			if (m_gamma.GetGamma() != 1.0)
			{
				((ImageBuffer)destImage).apply_gamma_inv(m_gamma);
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (m_mesh.OnMouseMove(mouseEvent))
			{
				Invalidate();
			}

			base.OnMouseMove(mouseEvent);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			if (m_mesh.OnMouseDown(mouseEvent))
			{
				Invalidate();
			}

			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			if (m_mesh.OnMouseUp(mouseEvent))
			{
				Invalidate();
			}

			base.OnMouseUp(mouseEvent);
		}

		public void OnIdle()
		{
			m_mesh.randomize_points(1.0);
			m_mesh.rotate_colors();
			Invalidate();
			UiThread.RunOnIdle(OnIdle);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			AppWidgetFactory appWidget = new GouraudMeshShadingFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}
	}

	public class GouraudMeshShadingFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new gouraud_mesh_application();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
				"Vector",
				"Gouraud Mesh Shading",
				"Yet another example that demonstrates the power of compound shape rasterization. Here we create a "
				+ "mesh of triangles and render them in one pass with multiple Gouraud shaders (span_gouraud_rgba). "
				+ "The example demonstrates perfect Anti-Aliasing and perfect triangle stitching (seamless edges) at the same time.",
				400,
				400);

			return appWidgetInfo;
		}
	}
}