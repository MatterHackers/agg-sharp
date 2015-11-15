using ClipperLib;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using MatterHackers.DataConverters2D;

namespace MatterHackers.Agg
{
	public class PolygonClippingDemo : GuiWidget
	{
		private PathStorage CombinePaths(IVertexSource a, IVertexSource b, ClipType clipType)
		{
			List<List<IntPoint>> aPolys = VertexSourceToClipperPolygons.CreatePolygons(a);
			List<List<IntPoint>> bPolys = VertexSourceToClipperPolygons.CreatePolygons(b);

			Clipper clipper = new Clipper();

			clipper.AddPaths(aPolys, PolyType.ptSubject, true);
			clipper.AddPaths(bPolys, PolyType.ptClip, true);

			List<List<IntPoint>> intersectedPolys = new List<List<IntPoint>>();
			clipper.Execute(clipType, intersectedPolys);

			PathStorage output = VertexSourceToClipperPolygons.CreatePathStorage(intersectedPolys);

			output.Add(0, 0, ShapePath.FlagsAndCommand.CommandStop);

			return output;
		}

		private double m_x;
		private double m_y;

        private RadioButtonGroup m_operation = new RadioButtonGroup(new Vector2(555, 5), new Vector2(80, 130))
        {
            HAnchor = HAnchor.ParentRight | HAnchor.FitToChildren,
            VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
            Margin = new BorderDouble(5),
        };
        private RadioButtonGroup m_polygons = new RadioButtonGroup(new Vector2(5, 5), new Vector2(205, 110))
        {
            HAnchor = HAnchor.ParentLeft | HAnchor.FitToChildren,
            VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
            Margin = new BorderDouble(5),
        };


public PolygonClippingDemo()
		{
			BackgroundColor = RGBA_Bytes.White;

			m_operation.AddRadioButton("None");
			m_operation.AddRadioButton("OR");
			m_operation.AddRadioButton("AND");
			m_operation.AddRadioButton("XOR");
			m_operation.AddRadioButton("A-B");
			m_operation.AddRadioButton("B-A");
			m_operation.SelectedIndex = 2;
			AddChild(m_operation);

			m_polygons.AddRadioButton("Two Simple Paths");
			m_polygons.AddRadioButton("Closed Stroke");
			m_polygons.AddRadioButton("Great Britain and Arrows");
			m_polygons.AddRadioButton("Great Britain and Spiral");
			m_polygons.AddRadioButton("Spiral and Glyph");
			m_polygons.SelectedIndex = 3;
			AddChild(m_polygons);

			AnchorAll();
		}

		private void render_gpc(Graphics2D graphics2D)
		{
			switch (m_polygons.SelectedIndex)
			{
				case 0:
					{
						//------------------------------------
						// Two simple paths
						//
						PathStorage ps1 = new PathStorage();
						PathStorage ps2 = new PathStorage();

						double x = m_x - Width / 2 + 100;
						double y = m_y - Height / 2 + 100;
						ps1.MoveTo(x + 140, y + 145);
						ps1.LineTo(x + 225, y + 44);
						ps1.LineTo(x + 296, y + 219);
						ps1.ClosePolygon();

						ps1.LineTo(x + 226, y + 289);
						ps1.LineTo(x + 82, y + 292);

						ps1.MoveTo(x + 220, y + 222);
						ps1.LineTo(x + 363, y + 249);
						ps1.LineTo(x + 265, y + 331);

						ps1.MoveTo(x + 242, y + 243);
						ps1.LineTo(x + 268, y + 309);
						ps1.LineTo(x + 325, y + 261);

						ps1.MoveTo(x + 259, y + 259);
						ps1.LineTo(x + 273, y + 288);
						ps1.LineTo(x + 298, y + 266);

						ps2.MoveTo(100 + 32, 100 + 77);
						ps2.LineTo(100 + 473, 100 + 263);
						ps2.LineTo(100 + 351, 100 + 290);
						ps2.LineTo(100 + 354, 100 + 374);

						graphics2D.Render(ps1, new RGBA_Floats(0, 0, 0, 0.1).GetAsRGBA_Bytes());
						graphics2D.Render(ps2, new RGBA_Floats(0, 0.6, 0, 0.1).GetAsRGBA_Bytes());

						CreateAndRenderCombined(graphics2D, ps1, ps2);
					}
					break;

				case 1:
					{
						//------------------------------------
						// Closed stroke
						//
						PathStorage ps1 = new PathStorage();
						PathStorage ps2 = new PathStorage();
						Stroke stroke = new Stroke(ps2);
						stroke.width(10.0);

						double x = m_x - Width / 2 + 100;
						double y = m_y - Height / 2 + 100;
						ps1.MoveTo(x + 140, y + 145);
						ps1.LineTo(x + 225, y + 44);
						ps1.LineTo(x + 296, y + 219);
						ps1.ClosePolygon();

						ps1.LineTo(x + 226, y + 289);
						ps1.LineTo(x + 82, y + 292);

						ps1.MoveTo(x + 220 - 50, y + 222);
						ps1.LineTo(x + 265 - 50, y + 331);
						ps1.LineTo(x + 363 - 50, y + 249);
						ps1.close_polygon(ShapePath.FlagsAndCommand.FlagCCW);

						ps2.MoveTo(100 + 32, 100 + 77);
						ps2.LineTo(100 + 473, 100 + 263);
						ps2.LineTo(100 + 351, 100 + 290);
						ps2.LineTo(100 + 354, 100 + 374);
						ps2.ClosePolygon();

						graphics2D.Render(ps1, new RGBA_Floats(0, 0, 0, 0.1).GetAsRGBA_Bytes());
						graphics2D.Render(stroke, new RGBA_Floats(0, 0.6, 0, 0.1).GetAsRGBA_Bytes());

						CreateAndRenderCombined(graphics2D, ps1, stroke);
					}
					break;

				case 2:
					{
						//------------------------------------
						// Great Britain and Arrows
						//
						PathStorage gb_poly = new PathStorage();
						PathStorage arrows = new PathStorage();
						GreatBritanPathStorage.Make(gb_poly);
						make_arrows(arrows);

						Affine mtx1 = Affine.NewIdentity();
						Affine mtx2 = Affine.NewIdentity();
						mtx1 *= Affine.NewTranslation(-1150, -1150);
						mtx1 *= Affine.NewScaling(2.0);

						mtx2 = mtx1;
						mtx2 *= Affine.NewTranslation(m_x - Width / 2, m_y - Height / 2);

						VertexSourceApplyTransform trans_gb_poly = new VertexSourceApplyTransform(gb_poly, mtx1);
						VertexSourceApplyTransform trans_arrows = new VertexSourceApplyTransform(arrows, mtx2);

						graphics2D.Render(trans_gb_poly, new RGBA_Floats(0.5, 0.5, 0, 0.1).GetAsRGBA_Bytes());

						Stroke stroke_gb_poly = new Stroke(trans_gb_poly);
						stroke_gb_poly.Width = 0.1;
						graphics2D.Render(stroke_gb_poly, new RGBA_Floats(0, 0, 0).GetAsRGBA_Bytes());

						graphics2D.Render(trans_arrows, new RGBA_Floats(0.0, 0.5, 0.5, 0.1).GetAsRGBA_Bytes());

						CreateAndRenderCombined(graphics2D, trans_gb_poly, trans_arrows);
					}
					break;

				case 3:
					{
						//------------------------------------
						// Great Britain and a Spiral
						//
						spiral sp = new spiral(m_x, m_y, 10, 150, 30, 0.0);
						Stroke stroke = new Stroke(sp);
						stroke.width(15.0);

						PathStorage gb_poly = new PathStorage();
						GreatBritanPathStorage.Make(gb_poly);

						Affine mtx = Affine.NewIdentity(); ;
						mtx *= Affine.NewTranslation(-1150, -1150);
						mtx *= Affine.NewScaling(2.0);

						VertexSourceApplyTransform trans_gb_poly = new VertexSourceApplyTransform(gb_poly, mtx);

						graphics2D.Render(trans_gb_poly, new RGBA_Floats(0.5, 0.5, 0, 0.1).GetAsRGBA_Bytes());

						Stroke stroke_gb_poly = new Stroke(trans_gb_poly);
						stroke_gb_poly.width(0.1);
						graphics2D.Render(stroke_gb_poly, new RGBA_Floats(0, 0, 0).GetAsRGBA_Bytes());

						graphics2D.Render(stroke, new RGBA_Floats(0.0, 0.5, 0.5, 0.1).GetAsRGBA_Bytes());

						CreateAndRenderCombined(graphics2D, trans_gb_poly, stroke);
					}
					break;

				case 4:
					{
						//------------------------------------
						// Spiral and glyph
						//
						spiral sp = new spiral(m_x, m_y, 10, 150, 30, 0.0);
						Stroke stroke = new Stroke(sp);
						stroke.width(15.0);

						PathStorage glyph = new PathStorage();
						glyph.MoveTo(28.47, 6.45);
						glyph.curve3(21.58, 1.12, 19.82, 0.29);
						glyph.curve3(17.19, -0.93, 14.21, -0.93);
						glyph.curve3(9.57, -0.93, 6.57, 2.25);
						glyph.curve3(3.56, 5.42, 3.56, 10.60);
						glyph.curve3(3.56, 13.87, 5.03, 16.26);
						glyph.curve3(7.03, 19.58, 11.99, 22.51);
						glyph.curve3(16.94, 25.44, 28.47, 29.64);
						glyph.LineTo(28.47, 31.40);
						glyph.curve3(28.47, 38.09, 26.34, 40.58);
						glyph.curve3(24.22, 43.07, 20.17, 43.07);
						glyph.curve3(17.09, 43.07, 15.28, 41.41);
						glyph.curve3(13.43, 39.75, 13.43, 37.60);
						glyph.LineTo(13.53, 34.77);
						glyph.curve3(13.53, 32.52, 12.38, 31.30);
						glyph.curve3(11.23, 30.08, 9.38, 30.08);
						glyph.curve3(7.57, 30.08, 6.42, 31.35);
						glyph.curve3(5.27, 32.62, 5.27, 34.81);
						glyph.curve3(5.27, 39.01, 9.57, 42.53);
						glyph.curve3(13.87, 46.04, 21.63, 46.04);
						glyph.curve3(27.59, 46.04, 31.40, 44.04);
						glyph.curve3(34.28, 42.53, 35.64, 39.31);
						glyph.curve3(36.52, 37.21, 36.52, 30.71);
						glyph.LineTo(36.52, 15.53);
						glyph.curve3(36.52, 9.13, 36.77, 7.69);
						glyph.curve3(37.01, 6.25, 37.57, 5.76);
						glyph.curve3(38.13, 5.27, 38.87, 5.27);
						glyph.curve3(39.65, 5.27, 40.23, 5.62);
						glyph.curve3(41.26, 6.25, 44.19, 9.18);
						glyph.LineTo(44.19, 6.45);
						glyph.curve3(38.72, -0.88, 33.74, -0.88);
						glyph.curve3(31.35, -0.88, 29.93, 0.78);
						glyph.curve3(28.52, 2.44, 28.47, 6.45);
						glyph.ClosePolygon();

						glyph.MoveTo(28.47, 9.62);
						glyph.LineTo(28.47, 26.66);
						glyph.curve3(21.09, 23.73, 18.95, 22.51);
						glyph.curve3(15.09, 20.36, 13.43, 18.02);
						glyph.curve3(11.77, 15.67, 11.77, 12.89);
						glyph.curve3(11.77, 9.38, 13.87, 7.06);
						glyph.curve3(15.97, 4.74, 18.70, 4.74);
						glyph.curve3(22.41, 4.74, 28.47, 9.62);
						glyph.ClosePolygon();

						Affine mtx = Affine.NewIdentity();
						mtx *= Affine.NewScaling(4.0);
						mtx *= Affine.NewTranslation(220, 200);
						VertexSourceApplyTransform trans = new VertexSourceApplyTransform(glyph, mtx);
						FlattenCurves curve = new FlattenCurves(trans);

						CreateAndRenderCombined(graphics2D, stroke, curve);

						graphics2D.Render(stroke, new RGBA_Floats(0, 0, 0, 0.1).GetAsRGBA_Bytes());

						graphics2D.Render(curve, new RGBA_Floats(0, 0.6, 0, 0.1).GetAsRGBA_Bytes());
					}
					break;
			}
		}

		private void CreateAndRenderCombined(Graphics2D graphics2D, IVertexSource ps1, IVertexSource ps2)
		{
			PathStorage combined = null;

			switch (m_operation.SelectedIndex)
			{
				case 1:
					combined = CombinePaths(ps1, ps2, ClipType.ctUnion);
					break;

				case 2:
					combined = CombinePaths(ps1, ps2, ClipType.ctIntersection);
					break;

				case 3:
					combined = CombinePaths(ps1, ps2, ClipType.ctXor);
					break;

				case 4:
					combined = CombinePaths(ps1, ps2, ClipType.ctDifference);
					break;

				case 5:
					combined = CombinePaths(ps2, ps1, ClipType.ctDifference);
					break;
			}

			if (combined != null)
			{
				graphics2D.Render(combined, new RGBA_Floats(0.5, 0.0, 0, 0.5).GetAsRGBA_Bytes());
			}
		}

		public override void OnBoundsChanged(EventArgs e)
		{
			m_x = Width / 2.0;
			m_y = Height / 2.0;

			base.OnBoundsChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			render_gpc(graphics2D);

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (mouseEvent.Button == MouseButtons.Left && FirstWidgetUnderMouse)
			{
				m_x = mouseEvent.X;
				m_y = mouseEvent.Y;
				Invalidate();
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (MouseCaptured)
			{
				m_x = mouseEvent.X;
				m_y = mouseEvent.Y;
				Invalidate();
			}
			base.OnMouseMove(mouseEvent);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			AppWidgetFactory appWidget = new PolygonClippingFactory();
			appWidget.CreateWidgetAndRunInWindow();
		}

		private void make_arrows(PathStorage ps)
		{
			ps.remove_all();

			ps.MoveTo(1330.599999999999909, 1282.399999999999864);
			ps.LineTo(1377.400000000000091, 1282.399999999999864);
			ps.LineTo(1361.799999999999955, 1298.000000000000000);
			ps.LineTo(1393.000000000000000, 1313.599999999999909);
			ps.LineTo(1361.799999999999955, 1344.799999999999955);
			ps.LineTo(1346.200000000000045, 1313.599999999999909);
			ps.LineTo(1330.599999999999909, 1329.200000000000045);
			ps.ClosePolygon();

			ps.MoveTo(1330.599999999999909, 1266.799999999999955);
			ps.LineTo(1377.400000000000091, 1266.799999999999955);
			ps.LineTo(1361.799999999999955, 1251.200000000000045);
			ps.LineTo(1393.000000000000000, 1235.599999999999909);
			ps.LineTo(1361.799999999999955, 1204.399999999999864);
			ps.LineTo(1346.200000000000045, 1235.599999999999909);
			ps.LineTo(1330.599999999999909, 1220.000000000000000);
			ps.ClosePolygon();

			ps.MoveTo(1315.000000000000000, 1282.399999999999864);
			ps.LineTo(1315.000000000000000, 1329.200000000000045);
			ps.LineTo(1299.400000000000091, 1313.599999999999909);
			ps.LineTo(1283.799999999999955, 1344.799999999999955);
			ps.LineTo(1252.599999999999909, 1313.599999999999909);
			ps.LineTo(1283.799999999999955, 1298.000000000000000);
			ps.LineTo(1268.200000000000045, 1282.399999999999864);
			ps.ClosePolygon();

			ps.MoveTo(1268.200000000000045, 1266.799999999999955);
			ps.LineTo(1315.000000000000000, 1266.799999999999955);
			ps.LineTo(1315.000000000000000, 1220.000000000000000);
			ps.LineTo(1299.400000000000091, 1235.599999999999909);
			ps.LineTo(1283.799999999999955, 1204.399999999999864);
			ps.LineTo(1252.599999999999909, 1235.599999999999909);
			ps.LineTo(1283.799999999999955, 1251.200000000000045);
			ps.ClosePolygon();
		}
	}

	public class PolygonClippingFactory : AppWidgetFactory
	{
		public override GuiWidget NewWidget()
		{
			return new PolygonClippingDemo();
		}

		public override AppWidgetInfo GetAppParameters()
		{
			AppWidgetInfo appWidgetInfo = new AppWidgetInfo(
			"Clipping",
			"PolygonClipping",
			"Demonstration of general polygon clipping using the clipper library.",
			640,
			520);

			return appWidgetInfo;
		}
	}

	public class spiral : IVertexSource
	{
		private double m_x;
		private double m_y;
		private double m_r1;
		private double m_r2;
		private double m_step;
		private double m_start_angle;

		private double m_angle;
		private double m_curr_r;
		private double m_da;
		private double m_dr;
		private bool m_start;

		public spiral(double x, double y, double r1, double r2, double step, double start_angle = 0)
		{
			m_x = x;
			m_y = y;
			m_r1 = r1;
			m_r2 = r2;
			m_step = step;
			m_start_angle = start_angle;
			m_angle = start_angle;
			m_da = agg_basics.deg2rad(4.0);
			m_dr = m_step / 90.0;
		}

		public IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
		}

		public void rewind(int index)
		{
			m_angle = m_start_angle;
			m_curr_r = m_r1;
			m_start = true;
		}

		public ShapePath.FlagsAndCommand vertex(out double x, out double y)
		{
			x = 0;
			y = 0;
			if (m_curr_r > m_r2)
			{
				return ShapePath.FlagsAndCommand.CommandStop;
			}

			x = m_x + Math.Cos(m_angle) * m_curr_r;
			y = m_y + Math.Sin(m_angle) * m_curr_r;
			m_curr_r += m_dr;
			m_angle += m_da;
			if (m_start)
			{
				m_start = false;
				return ShapePath.FlagsAndCommand.CommandMoveTo;
			}
			return ShapePath.FlagsAndCommand.CommandLineTo;
		}
	}

	internal class conv_poly_counter
	{
		private int m_contours;
		private int m_points;

		private conv_poly_counter(IVertexSource src)
		{
			m_contours = 0;
			m_points = 0;

			foreach (VertexData vertexData in src.Vertices())
			{
				if (ShapePath.is_vertex(vertexData.command))
				{
					++m_points;
				}

				if (ShapePath.is_move_to(vertexData.command))
				{
					++m_contours;
				}
			}
		}
	}
}