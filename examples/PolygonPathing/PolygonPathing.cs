/*
Copyright (c) 2015, Lars Brubaker
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

using System;
using System.Collections.Generic;
using ClipperLib;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.VectorMath;

namespace MatterHackers.PolygonPathing
{
	using Agg;
	using MatterSlice;
	using Polygon = List<IntPoint>;
	using Polygons = List<List<IntPoint>>;

	public class PolygonPathingDemo : SystemWindow
	{
		private Vector2 lineStart = Vector2.Zero;
		private Vector2 mousePosition;
		private RGBA_Bytes pathColor = RGBA_Bytes.Green;

		private RadioButtonGroup pathTypeRadioGroup = new RadioButtonGroup(new Vector2(555, 5), new Vector2(80, 130))
		{
			HAnchor = HAnchor.ParentRight | HAnchor.FitToChildren,
			VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
			Margin = new BorderDouble(5),
		};

		private Polygons polygonsToPathAround;

		private RadioButtonGroup shapeTypeRadioGroup = new RadioButtonGroup(new Vector2(5, 5), new Vector2(205, 110))
		{
			HAnchor = HAnchor.ParentLeft | HAnchor.FitToChildren,
			VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
			Margin = new BorderDouble(5),
		};

		public PolygonPathingDemo()
			: base(740, 520)
		{
			BackgroundColor = RGBA_Bytes.White;

			pathTypeRadioGroup.AddRadioButton("Stay Inside");
			pathTypeRadioGroup.AddRadioButton("Stay Outside");
			pathTypeRadioGroup.SelectedIndex = 0;
			AddChild(pathTypeRadioGroup);

			shapeTypeRadioGroup.AddRadioButton("Simple Map");
			shapeTypeRadioGroup.AddRadioButton("Multiple Pegs");
			shapeTypeRadioGroup.AddRadioButton("Circle Holes");
			shapeTypeRadioGroup.AddRadioButton("Great Britain");
			shapeTypeRadioGroup.AddRadioButton("Arrows");
			shapeTypeRadioGroup.AddRadioButton("Spiral");
			shapeTypeRadioGroup.AddRadioButton("Glyph");
			shapeTypeRadioGroup.SelectedIndex = 0;
			AddChild(shapeTypeRadioGroup);

			AnchorAll();
		}

		private RGBA_Bytes fillColor
		{ get { return RGBA_Bytes.Pink; } }

		[STAThread]
		public static void Main(string[] args)
		{
			PolygonPathingDemo demo = new PolygonPathingDemo();
			demo.ShowAsSystemWindow();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			RenderPolygonToPathAgainst(graphics2D);

			graphics2D.Line(lineStart, mousePosition, RGBA_Bytes.Red);

			if (polygonsToPathAround?.Count > 0)
			{
				var otherPolygons = new List<List<MSClipperLib.IntPoint>>();
				foreach (var polygon in polygonsToPathAround)
				{
					otherPolygons.Add(new List<MSClipperLib.IntPoint>());
					for (int i = 0; i < polygon.Count; i++)
					{
						otherPolygons[otherPolygons.Count - 1].Add(new MSClipperLib.IntPoint(polygon[i].X, polygon[i].Y));
					}
				}

				MSClipperLib.IntPoint startPos = new MSClipperLib.IntPoint(lineStart.x, lineStart.y);
				MSClipperLib.IntPoint mousePos = new MSClipperLib.IntPoint(mousePosition.x, mousePosition.y);

				var avoid = new AvoidCrossingPerimeters(otherPolygons);

				// creat the path
				List<MSClipperLib.IntPoint> pathThatIsInside = new List<MSClipperLib.IntPoint>();
				bool found = avoid.CreatePathInsideBoundary(startPos, mousePos, pathThatIsInside);

				foreach(var node in avoid.Waypoints.Nodes)
				{
					foreach(var link in node.Links)
					{
						var pointA = ((Pathfinding.IntPointNode)link.nodeA).Position;
						var pointB = ((Pathfinding.IntPointNode)link.nodeB).Position;
						graphics2D.Line(pointA.X, pointA.Y, pointB.X, pointB.Y, RGBA_Bytes.Yellow);
					}
					graphics2D.Circle(node.Position.X, node.Position.Y, 4, RGBA_Bytes.Green);
				}

				if (found)
				{
					MSClipperLib.IntPoint last = startPos;
					foreach (var point in pathThatIsInside)
					{
						graphics2D.Line(last.X, last.Y, point.X, point.Y, new RGBA_Bytes(RGBA_Bytes.Black, 128), 2);
						last = point;
					}

					graphics2D.Line(last.X, last.Y, mousePos.X, mousePos.Y, new RGBA_Bytes(RGBA_Bytes.Black, 128), 2);
				}
			}

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);

			if (mouseEvent.Button == MouseButtons.Left && FirstWidgetUnderMouse)
			{
				lineStart = mousePosition = mouseEvent.Position;
				Invalidate();
			}
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			if (MouseCaptured)
			{
				mousePosition = mouseEvent.Position;
				Invalidate();
			}
			base.OnMouseMove(mouseEvent);
		}

		private void CreateAndRenderPathing(Graphics2D graphics2D, Polygons travelPolysLine)
		{
			Polygons travelPolygons = CreateTravelPath(polygonsToPathAround, travelPolysLine);
			PathStorage travelPath = VertexSourceToClipperPolygons.CreatePathStorage(travelPolygons);
			travelPath.Add(0, 0, ShapePath.FlagsAndCommand.CommandStop);
			graphics2D.Render(new Stroke(travelPath), pathColor);
		}

		private Polygons CreateTravelPath(Polygons polygonsToPathAround, Polygons travelPolysLine)
		{
			Clipper clipper = new Clipper();

			clipper.AddPaths(travelPolysLine, PolyType.ptSubject, false);
			clipper.AddPaths(polygonsToPathAround, PolyType.ptClip, true);

			PolyTree clippedLine = new PolyTree();

			//List<List<IntPoint>> intersectedPolys = new List<List<IntPoint>>();
			//clipper.Execute(ClipType.ctDifference, intersectedPolys);

			clipper.Execute(ClipType.ctDifference, clippedLine);

			return Clipper.OpenPathsFromPolyTree(clippedLine);
		}

		private Polygons FixWinding(Polygons polygonsToPathAround)
		{
			polygonsToPathAround = Clipper.CleanPolygons(polygonsToPathAround);
			Polygon boundsPolygon = new Polygon();
			IntRect bounds = Clipper.GetBounds(polygonsToPathAround);
			bounds.left -= 10;
			bounds.bottom += 10;
			bounds.right += 10;
			bounds.top -= 10;

			boundsPolygon.Add(new IntPoint(bounds.left, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.top));
			boundsPolygon.Add(new IntPoint(bounds.right, bounds.bottom));
			boundsPolygon.Add(new IntPoint(bounds.left, bounds.bottom));

			Clipper clipper = new Clipper();

			clipper.AddPaths(polygonsToPathAround, PolyType.ptSubject, true);
			clipper.AddPath(boundsPolygon, PolyType.ptClip, true);

			PolyTree intersectionResult = new PolyTree();
			clipper.Execute(ClipType.ctIntersection, intersectionResult);

			Polygons outputPolygons = Clipper.ClosedPathsFromPolyTree(intersectionResult);

			return outputPolygons;
		}

		private void make_arrows(PathStorage ps)
		{
			ps.remove_all();

			ps.MoveTo(1330.6, 1282.4);
			ps.LineTo(1377.4, 1282.4);
			ps.LineTo(1361.8, 1298.0);
			ps.LineTo(1393.0, 1313.6);
			ps.LineTo(1361.8, 1344.8);
			ps.LineTo(1346.2, 1313.6);
			ps.LineTo(1330.6, 1329.2);
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

		private void RenderPolygonToPathAgainst(Graphics2D graphics2D)
		{
			IVertexSource pathToUse = null;
			switch (shapeTypeRadioGroup.SelectedIndex)
			{
				case 0:// simple polygon map
					{
						PathStorage ps1 = new PathStorage();

						ps1.MoveTo(85, 417);
						ps1.LineTo(338, 428);
						ps1.LineTo(344, 325);
						ps1.LineTo(399, 324);
						ps1.LineTo(400, 421);
						ps1.LineTo(644, 415);
						ps1.LineTo(664, 75);
						ps1.LineTo(98, 81);
						ps1.ClosePolygon();

						ps1.MoveTo(343, 290);
						ps1.LineTo(159, 235);
						ps1.LineTo(154, 162);
						ps1.LineTo(340, 114);
						ps1.ClosePolygon();

						ps1.MoveTo(406, 121);
						ps1.LineTo(587, 158);
						ps1.LineTo(591, 236);
						ps1.LineTo(404, 291);
						ps1.ClosePolygon();

						pathToUse = ps1;
					}
					break;

				case 1: // multiple pegs
					{
						PathStorage ps2 = new PathStorage();

						double x = 0;
						double y = 0;

						ps2.MoveTo(100 + 32, 100 + 77);
						ps2.LineTo(100 + 473, 100 + 263);
						ps2.LineTo(100 + 351, 100 + 290);
						ps2.LineTo(100 + 354, 100 + 374);

						pathToUse = ps2;
					}
					break;

				case 2:
					{
						// circle holes
						PathStorage ps1 = new PathStorage();

						double x = 0;
						double y = 0;
						ps1.MoveTo(x + 140, y + 145);
						ps1.LineTo(x + 225, y + 44);
						ps1.LineTo(x + 296, y + 219);
						ps1.ClosePolygon();

						ps1.LineTo(x + 226, y + 289);
						ps1.LineTo(x + 82, y + 292);
						ps1.ClosePolygon();

						ps1.MoveTo(x + 220 - 50, y + 222);
						ps1.LineTo(x + 265 - 50, y + 331);
						ps1.LineTo(x + 363 - 50, y + 249);
						ps1.ClosePolygon();

						pathToUse = ps1;
					}
					break;

				case 3: // Great Britain
					{
						PathStorage gb_poly = new PathStorage();
						GreatBritanPathStorage.Make(gb_poly);

						Affine mtx1 = Affine.NewIdentity();
						Affine mtx2 = Affine.NewIdentity();
						mtx1 *= Affine.NewTranslation(-1150, -1150);
						mtx1 *= Affine.NewScaling(2.0);

						mtx2 = mtx1;
						mtx2 *= Affine.NewTranslation(Width / 2, Height / 2);

						VertexSourceApplyTransform trans_gb_poly = new VertexSourceApplyTransform(gb_poly, mtx1);

						pathToUse = trans_gb_poly;
					}
					break;

				case 4: // Arrows
					{
						PathStorage arrows = new PathStorage();
						make_arrows(arrows);

						Affine mtx1 = Affine.NewIdentity();
						mtx1 *= Affine.NewTranslation(-1150, -1150);
						mtx1 *= Affine.NewScaling(2.0);

						VertexSourceApplyTransform trans_arrows = new VertexSourceApplyTransform(arrows, mtx1);

						pathToUse = trans_arrows;
					}
					break;

				case 5: // Spiral
					{
						spiral sp = new spiral(Width / 2, Height / 2, 10, 150, 30, 0.0);
						Stroke stroke = new Stroke(sp);
						stroke.width(15.0);

						Affine mtx = Affine.NewIdentity(); ;
						mtx *= Affine.NewTranslation(-1150, -1150);
						mtx *= Affine.NewScaling(2.0);

						pathToUse = stroke;
					}
					break;

				case 6: // Glyph
					{
						//------------------------------------
						// Spiral and glyph
						//
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

						pathToUse = curve;
					}
					break;
			}

			polygonsToPathAround = VertexSourceToClipperPolygons.CreatePolygons(pathToUse, 1);
			polygonsToPathAround = FixWinding(polygonsToPathAround);

			PathStorage shapePath = VertexSourceToClipperPolygons.CreatePathStorage(polygonsToPathAround, 1);
			graphics2D.Render(shapePath, fillColor);

			PathStorage travelLine = new PathStorage();
			travelLine.MoveTo(lineStart);
			travelLine.LineTo(mousePosition);

			Polygons travelPolysLine = VertexSourceToClipperPolygons.CreatePolygons(travelLine, 1);

			CreateAndRenderPathing(graphics2D, travelPolysLine);
		}
	}

	public class spiral : IVertexSource
	{
		private double m_angle;
		private double m_curr_r;
		private double m_da;
		private double m_dr;
		private double m_r1;
		private double m_r2;
		private bool m_start;
		private double m_start_angle;
		private double m_step;
		private double m_x;
		private double m_y;

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

		public IEnumerable<VertexData> Vertices()
		{
			throw new NotImplementedException();
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