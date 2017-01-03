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
	using MSPolygon = List<MSClipperLib.IntPoint>;
	using MSPolygons = List<List<MSClipperLib.IntPoint>>;

	public class PolygonPathingDemo : SystemWindow
	{
		private Vector2 lineStart = Vector2.Zero;
		private Vector2 mousePosition;
		private RGBA_Bytes pathColor = RGBA_Bytes.Green;
		int scale = 1;

		private RadioButtonGroup pathTypeRadioGroup = new RadioButtonGroup(new Vector2(555, 5), new Vector2(80, 130))
		{
			HAnchor = HAnchor.ParentRight | HAnchor.FitToChildren,
			VAnchor = VAnchor.ParentBottom | VAnchor.FitToChildren,
			Margin = new BorderDouble(5),
		};

		private MSPolygons polygonsToPathAround;

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
			shapeTypeRadioGroup.AddRadioButton("Raise The Barre");
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
				MSClipperLib.IntPoint startPos = new MSClipperLib.IntPoint(lineStart.x, lineStart.y);
				MSClipperLib.IntPoint mousePos = new MSClipperLib.IntPoint(mousePosition.x, mousePosition.y);

				var avoid = new AvoidCrossingPerimeters(polygonsToPathAround);

				// creat the path
				List<MSClipperLib.IntPoint> pathThatIsInside = new List<MSClipperLib.IntPoint>();
				bool found = avoid.CreatePathInsideBoundary(startPos, mousePos, pathThatIsInside);

				foreach (var node in avoid.Waypoints.Nodes)
				{
					foreach (var link in node.Links)
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

				//var triangulated = avoid.BoundaryPolygons.Triangulate();
			}

			base.OnDraw(graphics2D);
		}

		private MSPolygons PolygonsToMSPolygons(Polygons polygonsToPathAround)
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

			return otherPolygons;
		}

		private Polygons MSPolygonsToPolygons(MSPolygons polygonsToPathAround)
		{
			var otherPolygons = new List<List<ClipperLib.IntPoint>>();
			foreach (var polygon in polygonsToPathAround)
			{
				otherPolygons.Add(new List<ClipperLib.IntPoint>());
				for (int i = 0; i < polygon.Count; i++)
				{
					otherPolygons[otherPolygons.Count - 1].Add(new ClipperLib.IntPoint(polygon[i].X, polygon[i].Y));
				}
			}

			return otherPolygons;
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

		private MSPolygons CreateTravelPath(MSPolygons polygonsToPathAround, MSPolygons travelPolysLine)
		{
			var clipper = new MSClipperLib.Clipper();

			clipper.AddPaths(travelPolysLine, MSClipperLib.PolyType.ptSubject, false);
			clipper.AddPaths(polygonsToPathAround, MSClipperLib.PolyType.ptClip, true);

			var clippedLine = new MSClipperLib.PolyTree();

			//List<List<IntPoint>> intersectedPolys = new List<List<IntPoint>>();
			//clipper.Execute(ClipType.ctDifference, intersectedPolys);

			clipper.Execute(MSClipperLib.ClipType.ctDifference, clippedLine);

			return MSClipperLib.Clipper.OpenPathsFromPolyTree(clippedLine);
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
			MSPolygons directPolygons = null;
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

				case 3: // Raise The Barre
					{
						string raiseTheBarreString = "x:207963, y:42979, x:208138, y:43007, x:208424, y:43118, x:208660, y:43242, x:209089, y:43671, x:209279, y:44046, x:209325, y:44182, x:209359, y:44395, x:209393, y:44479, x:209400, y:44563, x:209393, y:67664, x:209333, y:67485, x:209008, y:66942, x:208491, y:66425, x:207948, y:66100, x:207614, y:65989, x:207443, y:65962, x:207294, y:65901, x:207057, y:65897, x:207071, y:65579, x:207004, y:65258, x:206773, y:65027, x:206790, y:64992, x:206755, y:64211, x:206634, y:63608, x:206365, y:63350, x:206683, y:62979, x:206694, y:62380, x:206635, y:62194, x:206589, y:61364, x:206320, y:61140, x:206635, y:61109, x:206979, y:60753, x:206994, y:60056, x:206932, y:59861, x:206891, y:59409, x:206627, y:59067, x:206324, y:58862, x:205932, y:58834, x:205025, y:58839, x:204722, y:58904, x:203678, y:58901, x:203535, y:58856, x:203231, y:58834, x:200833, y:58852, x:200192, y:59382, x:200226, y:59316, x:199676, y:58860, x:199433, y:58844, x:197147, y:58841, x:196857, y:58902, x:196784, y:58848, x:194085, y:58850, x:193350, y:59458, x:194111, y:60133, x:194239, y:60153, x:194255, y:60922, x:194286, y:61324, x:194271, y:61563, x:194286, y:61915, x:194271, y:62168, x:194287, y:62484, x:194269, y:62582, x:194286, y:62934, x:194269, y:63032, x:194286, y:63384, x:194269, y:63482, x:194287, y:63836, x:194283, y:63857, x:193687, y:63854, x:193522, y:63815, x:193089, y:63325, x:192509, y:63383, x:192165, y:63752, x:192165, y:64299, x:192223, y:64439, x:192254, y:64706, x:192303, y:64826, x:192351, y:65128, x:192176, y:65303, x:192153, y:65813, x:192220, y:66134, x:192536, y:66449, x:192989, y:66484, x:205156, y:66478, x:204774, y:66858, x:204371, y:67473, x:204255, y:67819, x:204228, y:67990, x:204173, y:68123, x:204149, y:68460, x:204169, y:69033, x:204228, y:69177, x:204262, y:69392, x:204578, y:70015, x:204858, y:70406, x:205134, y:70666, x:205529, y:70920, x:205814, y:71059, x:160729, y:71052, x:160960, y:70928, x:161567, y:70504, x:161904, y:70153, x:162311, y:69531, x:162549, y:69042, x:162737, y:68353, x:162754, y:68185, x:162808, y:68055, x:162820, y:67447, x:162755, y:67133, x:162438, y:66817, x:161984, y:66784, x:159957, y:66790, x:159650, y:66854, x:159344, y:67160, x:159298, y:67554, x:159260, y:67648, x:159228, y:67847, x:159121, y:68123, x:158868, y:68583, x:158621, y:68893, x:158088, y:69265, x:157730, y:69413, x:157541, y:69444, x:157458, y:69477, x:157369, y:69484, x:156599, y:69480, x:156594, y:67440, x:156530, y:67133, x:156197, y:66800, x:153809, y:66790, x:153500, y:66854, x:153185, y:67170, x:153150, y:67622, x:153146, y:69533, x:153087, y:69831, x:153012, y:69978, x:152795, y:70067, x:152624, y:70085, x:151751, y:70106, x:151292, y:70561, x:151292, y:70704, x:150910, y:70322, x:150722, y:69950, x:150675, y:69810, x:150641, y:69598, x:150607, y:69514, x:150600, y:69426, x:150607, y:44495, x:150646, y:44367, x:150673, y:44196, x:150783, y:43913, x:150907, y:43675, x:151339, y:43244, x:151709, y:43057, x:151855, y:43008, x:152081, y:42967, x:152121, y:42951, x:163403, y:42949, x:163299, y:43484, x:163974, y:43885, x:164596, y:43919, x:164714, y:43963, x:164741, y:44004, x:164776, y:44108, x:164810, y:44320, x:164842, y:44398, x:164868, y:44831, x:164921, y:45130, x:164933, y:46404, x:164996, y:46710, x:165312, y:47025, x:165766, y:47060, x:167719, y:47053, x:168026, y:46989, x:168342, y:46672, x:168375, y:46220, x:168380, y:45128, x:168443, y:44753, x:168458, y:44723, x:168586, y:44596, x:168813, y:44480, x:168935, y:44440, x:169222, y:44405, x:169318, y:44367, x:169406, y:44360, x:170074, y:44364, x:170377, y:44423, x:170864, y:44670, x:171215, y:45021, x:171405, y:45396, x:171450, y:45529, x:171526, y:46026, x:171560, y:46673, x:171912, y:47025, x:172366, y:47060, x:174316, y:47053, x:174366, y:47043, x:174340, y:47072, x:174128, y:47060, x:171705, y:47078, x:171326, y:47454, x:171096, y:48063, x:170642, y:48777, x:170125, y:49288, x:169847, y:49477, x:169517, y:49638, x:169380, y:49684, x:168962, y:49754, x:168374, y:49749, x:168369, y:47716, x:168305, y:47409, x:167972, y:47076, x:165584, y:47066, x:165275, y:47130, x:164960, y:47446, x:164925, y:47899, x:164932, y:53527, x:164996, y:53835, x:165312, y:54150, x:165391, y:54156, x:165275, y:54180, x:164947, y:54508, x:164915, y:55725, x:164861, y:55972, x:164834, y:56036, x:164774, y:56081, x:164523, y:56130, x:163848, y:56141, x:163530, y:56209, x:163131, y:56232, x:162407, y:56496, x:161981, y:56720, x:161525, y:57072, x:160728, y:57888, x:160532, y:58143, x:160379, y:58307, x:157740, y:61974, x:156964, y:61993, x:156733, y:62008, x:156600, y:62023, x:156605, y:58938, x:156668, y:58642, x:156686, y:58146, x:156773, y:57828, x:156807, y:57796, x:156826, y:57786, x:157140, y:57757, x:157234, y:57719, x:158052, y:57683, x:158380, y:57408, x:158531, y:57133, x:158531, y:56729, x:158253, y:56368, x:157950, y:56163, x:157556, y:56135, x:156668, y:56144, x:156426, y:56160, x:156074, y:56209, x:153628, y:56205, x:153380, y:56153, x:153234, y:56162, x:152886, y:56136, x:152176, y:56170, x:151592, y:56524, x:151592, y:57232, x:152045, y:57681, x:152818, y:57718, x:152955, y:57772, x:152957, y:57775, x:153001, y:57906, x:153035, y:58119, x:153064, y:58189, x:153083, y:58717, x:153146, y:59013, x:153157, y:66126, x:153221, y:66434, x:153537, y:66749, x:153991, y:66784, x:155944, y:66777, x:156251, y:66713, x:156567, y:66396, x:156600, y:65945, x:156614, y:64160, x:156915, y:64132, x:157018, y:64091, x:157106, y:64084, x:158045, y:64090, x:158164, y:64128, x:158308, y:64151, x:158488, y:64243, x:158860, y:64615, x:159016, y:64857, x:159101, y:65035, x:159237, y:65426, x:159302, y:65713, x:159327, y:65976, x:159381, y:66107, x:159441, y:66429, x:159762, y:66749, x:160217, y:66784, x:161903, y:66776, x:162632, y:66546, x:162483, y:65814, x:162098, y:65055, x:161681, y:64456, x:161539, y:64304, x:161372, y:64069, x:160667, y:63372, x:160540, y:63273, x:160624, y:63182, x:160780, y:62963, x:160924, y:62807, x:161080, y:62588, x:161224, y:62432, x:161380, y:62213, x:161524, y:62057, x:161681, y:61837, x:161824, y:61682, x:161980, y:61463, x:162109, y:61323, x:163517, y:64711, x:163592, y:64955, x:163887, y:65249, x:164347, y:65285, x:165328, y:65262, x:165834, y:64828, x:165608, y:64205, x:166547, y:64209, x:166438, y:64506, x:166470, y:64914, x:166818, y:65250, x:167269, y:65284, x:168846, y:65265, x:169209, y:64956, x:170732, y:61237, x:170954, y:60807, x:171121, y:60576, x:171359, y:60345, x:171382, y:60333, x:171622, y:60304, x:171696, y:60275, x:171823, y:60274, x:171850, y:60645, x:171894, y:60752, x:171900, y:60836, x:171885, y:68686, x:171868, y:68720, x:171253, y:68768, x:170507, y:69444, x:171252, y:70125, x:171691, y:70159, x:175140, y:70152, x:175447, y:70091, x:175794, y:69745, x:175769, y:69123, x:175411, y:68764, x:174791, y:68725, x:174776, y:68719, x:174756, y:68674, x:174750, y:68599, x:174757, y:60619, x:174800, y:60478, x:174820, y:60301, x:174915, y:60270, x:175651, y:60233, x:175656, y:60958, x:175723, y:61269, x:175745, y:62083, x:175944, y:62470, x:176244, y:62714, x:176755, y:62714, x:177192, y:62269, x:177232, y:61727, x:177266, y:61644, x:177298, y:61445, x:177409, y:61160, x:177474, y:61038, x:177618, y:60839, x:177931, y:60615, x:178062, y:60553, x:178235, y:60524, x:178318, y:60491, x:178400, y:60485, x:178929, y:60497, x:179175, y:60592, x:179409, y:60715, x:179614, y:60921, x:179730, y:61149, x:179766, y:61256, x:179773, y:61346, x:179762, y:61745, x:179622, y:62139, x:179447, y:62403, x:179122, y:62764, x:178932, y:62917, x:178621, y:63206, x:178038, y:63662, x:177814, y:63865, x:177576, y:64046, x:176996, y:64646, x:176466, y:65381, x:176169, y:66034, x:176051, y:66412, x:176023, y:66654, x:175975, y:66772, x:175949, y:67109, x:175970, y:67684, x:176024, y:67814, x:176054, y:68075, x:176242, y:68561, x:176384, y:68822, x:176658, y:69204, x:176998, y:69530, x:177468, y:69864, x:177952, y:70100, x:178261, y:70203, x:178433, y:70230, x:178560, y:70282, x:178904, y:70314, x:179014, y:70359, x:179343, y:70384, x:180210, y:70375, x:180394, y:70318, x:180834, y:70284, x:180953, y:70236, x:181204, y:70206, x:181722, y:70031, x:182116, y:69521, x:182087, y:69725, x:182486, y:70124, x:182939, y:70159, x:188272, y:70166, x:188657, y:70286, x:189049, y:70294, x:189510, y:69824, x:189506, y:69344, x:189456, y:69218, x:189428, y:68845, x:189378, y:68622, x:189354, y:67573, x:188662, y:66817, x:187982, y:67560, x:187942, y:68110, x:187927, y:68154, x:187908, y:68176, x:187834, y:68213, x:187545, y:68279, x:186225, y:68278, x:186230, y:65735, x:187820, y:65741, x:187938, y:65778, x:188012, y:65790, x:188230, y:65972, x:188491, y:66103, x:188934, y:66060, x:189279, y:65702, x:189295, y:64893, x:189228, y:64576, x:189205, y:63314, x:188919, y:62972, x:188616, y:62818, x:188164, y:62853, x:187845, y:63194, x:187781, y:63684, x:187741, y:63842, x:187742, y:63842, x:187712, y:63863, x:187542, y:63926, x:187443, y:63934, x:186225, y:63929, x:186231, y:60986, x:186255, y:60904, x:186275, y:60881, x:186330, y:60850, x:186484, y:60824, x:186567, y:60791, x:186655, y:60784, x:187820, y:60790, x:187938, y:60828, x:188082, y:60851, x:188247, y:60936, x:188332, y:61101, x:188359, y:61269, x:188403, y:61377, x:188451, y:61678, x:189112, y:62400, x:189804, y:61643, x:189820, y:60843, x:189751, y:60515, x:189729, y:60030, x:189676, y:59903, x:189642, y:59607, x:189429, y:59201, x:189127, y:58955, x:188815, y:58848, x:186730, y:58840, x:186422, y:58904, x:182745, y:58926, x:181981, y:59619, x:182729, y:60304, x:183281, y:60339, x:183293, y:60370, x:183321, y:60677, x:183370, y:60891, x:183359, y:68674, x:183342, y:68706, x:183290, y:68723, x:182794, y:68742, x:182440, y:68844, x:182172, y:69124, x:182119, y:69499,  x:189204, y:49614, x:189345, y:49461, x:189726, y:48942, x:189873, y:48783, x:190039, y:48549, x:190387, y:48132, x:190620, y:47886, x:190769, y:47677, x:191422, y:47035, x:191727, y:46817, x:192013, y:46679, x:192184, y:46650, x:192245, y:46626, x:192360, y:46625, x:192367, y:46642, x:192399, y:47071, x:192443, y:47178, x:192449, y:47262, x:192456, y:52327, x:192520, y:52635, x:192836, y:52950, x:193291, y:52985, x:194569, y:52978, x:194875, y:52914, x:195204, y:52585, x:195231, y:51232, x:195246, y:51230, x:195342, y:51192, x:195430, y:51185, x:196070, y:51190, x:196189, y:51225, x:196337, y:51324, x:196545, y:51611, x:196738, y:52099, x:196882, y:52650, x:197190, y:52949, x:197643, y:52985, x:198925, y:52964, x:199477, y:52491, x:198990, y:51543, x:198413, y:50786, x:198037, y:50419, x:198351, y:49992, x:198498, y:49833, x:198654, y:49614, x:198795, y:49461, x:199176, y:48942, x:199323, y:48783, x:199489, y:48549, x:199837, y:48132, x:200070, y:47886, x:200219, y:47677, x:200872, y:47035, x:201177, y:46817, x:201463, y:46679, x:201634, y:46650, x:201674, y:46634, x:201741, y:46662, x:202181, y:46690, x:202193, y:46721, x:202221, y:47028, x:202270, y:47242, x:202259, y:55025, x:202240, y:55060, x:202236, y:55063, x:202230, y:55065, x:201388, y:55122, x:201006, y:55493, x:201031, y:56120, x:201386, y:56475, x:201839, y:56510, x:207172, y:56517, x:207557, y:56637, x:207949, y:56645, x:208410, y:56175, x:208406, y:55695, x:208356, y:55569, x:208328, y:55196, x:208278, y:54973, x:208254, y:53924, x:207562, y:53168, x:206882, y:53911, x:206842, y:54459, x:206827, y:54505, x:206808, y:54527, x:206734, y:54564, x:206445, y:54630, x:205125, y:54629, x:205130, y:52086, x:206720, y:52092, x:206838, y:52129, x:206912, y:52141, x:207130, y:52323, x:207391, y:52454, x:207834, y:52411, x:208179, y:52053, x:208195, y:51244, x:208128, y:50927, x:208105, y:49665, x:207819, y:49323, x:207516, y:49169, x:207064, y:49204, x:206745, y:49545, x:206681, y:50033, x:206641, y:50193, x:206612, y:50214, x:206442, y:50277, x:206343, y:50285, x:205125, y:50280, x:205131, y:47337, x:205155, y:47255, x:205175, y:47232, x:205230, y:47201, x:205384, y:47175, x:205467, y:47142, x:205555, y:47135, x:206720, y:47141, x:206838, y:47179, x:206982, y:47202, x:207147, y:47287, x:207238, y:47464, x:207363, y:48041, x:208012, y:48751, x:208704, y:47995, x:208720, y:47310, x:208694, y:46965, x:208658, y:46875, x:208627, y:46376, x:208576, y:46254, x:208542, y:45958, x:208329, y:45552, x:208030, y:45308, x:207749, y:45209, x:207434, y:45185, x:205626, y:45190, x:205321, y:45255, x:202225, y:45255, x:201920, y:45191, x:200079, y:45191, x:199746, y:45261, x:199487, y:45287, x:198996, y:45476, x:198719, y:45628, x:198189, y:46058, x:197923, y:46345, x:197727, y:46598, x:197575, y:46761, x:195732, y:49385, x:195225, y:49381, x:195229, y:47163, x:195289, y:46879, x:195304, y:46677, x:195319, y:46647, x:195395, y:46620, x:196219, y:46573, x:196560, y:46165, x:196549, y:45651, x:196110, y:45217, x:195656, y:45185, x:194974, y:45191, x:194671, y:45255, x:193075, y:45255, x:192770, y:45191, x:190629, y:45191, x:190296, y:45261, x:190037, y:45287, x:189546, y:45476, x:189270, y:45627, x:188737, y:46058, x:188473, y:46346, x:188278, y:46598, x:188125, y:46761, x:186282, y:49385, x:185775, y:49381, x:185779, y:47163, x:185839, y:46879, x:185854, y:46677, x:185869, y:46647, x:185945, y:46620, x:186769, y:46573, x:187110, y:46165, x:187099, y:45651, x:186660, y:45217, x:186206, y:45185, x:185524, y:45191, x:185221, y:45255, x:183625, y:45255, x:183321, y:45191, x:182627, y:45190, x:182435, y:45215, x:179520, y:45281, x:178658, y:46046, x:179283, y:46448, x:178443, y:48710, x:176066, y:48705, x:175421, y:47004, x:174895, y:46720, x:175016, y:46599, x:174782, y:45575, x:174490, y:44906, x:174192, y:44431, x:173886, y:44033, x:173444, y:43611, x:173007, y:43272, x:172433, y:42948, x:207846, y:42942, |x:209392, y:69505, x:209355, y:69622, x:209327, y:69797, x:209218, y:70079, x:209093, y:70317, x:208661, y:70750, x:208291, y:70937, x:208152, y:70984, x:208055, y:71000, x:208622, y:70620, x:209010, y:70221, x:209333, y:69682, x:209392, y:69504, |x:166418, y:65378, x:166074, y:65648, x:166073, y:65651, x:165798, y:65377, x:164751, y:65365, x:164418, y:65436, x:164115, y:65809, x:164124, y:66186, x:165595, y:69767, x:165760, y:70071, x:166012, y:70313, x:166270, y:70442, x:166597, y:70435, x:167029, y:70225, x:167328, y:69688, x:168739, y:66145, x:168706, y:65729, x:168356, y:65391, x:167906, y:65359, |x:201024, y:63114, x:201036, y:63386, x:201020, y:63484, x:201036, y:63854, x:201009, y:64169, x:200658, y:64216, x:200322, y:64631, x:200351, y:64920, x:200059, y:64920, x:199567, y:64224, x:199303, y:64180, x:199279, y:64062, x:199287, y:63110, |x:205583, y:63197, x:205748, y:63292, x:205492, y:63645, x:205413, y:63905, x:205314, y:63929, x:204901, y:63928, x:204904, y:63188, |x:196882, y:60141, x:197094, y:60165, x:197099, y:60232, x:197085, y:63813, x:196913, y:63854, x:196468, y:63854, x:196462, y:63733, x:196480, y:63634, x:196462, y:63282, x:196479, y:63187, x:196463, y:62783, x:196478, y:62552, x:196462, y:62202, x:196478, y:61954, x:196463, y:61603, x:196478, y:61350, x:196462, y:61016, x:196498, y:60713, x:196510, y:60176, x:196878, y:60137, |x:169893, y:58207, x:170621, y:58734, x:171019, y:58759, x:177565, y:58756, x:177558, y:58758, x:177146, y:58782, x:177009, y:58837, x:176819, y:58868, x:176190, y:59113, x:176064, y:59215, x:175711, y:58866, x:175257, y:58834, x:174501, y:58839, x:174197, y:58904, x:172526, y:58904, x:172221, y:58840, x:169360, y:58840, x:168260, y:58958, x:167945, y:59358, x:167945, y:59904, x:168117, y:60037, x:168062, y:60156, x:167244, y:62358, x:164884, y:62345, x:164473, y:61247, x:164441, y:61048, x:164415, y:60985, x:164470, y:60702, x:164585, y:60407, x:164805, y:59980, x:164980, y:59738, x:165124, y:59582, x:165266, y:59382, x:165390, y:59266, x:165928, y:58891, x:166364, y:58677, x:166535, y:58649, x:166618, y:58616, x:167274, y:58567, x:167611, y:58164, x:167602, y:57758, x:167737, y:57756, x:168007, y:57784, x:169558, y:57778, x:169895, y:57753, x:169992, y:57714, x:170339, y:57683, x:170431, y:57646, |x:205595, y:60414, x:205640, y:60427, x:205652, y:60463, x:205720, y:60837, x:205955, y:61026, x:205634, y:61058, x:205317, y:61398, x:205293, y:61607, x:205278, y:61609, x:204901, y:61606, x:204913, y:60412, x:204955, y:60409, |x:200861, y:60133, x:200997, y:60155, x:201016, y:60569, x:201034, y:60684, x:201021, y:60797, x:201035, y:61138, x:201020, y:61238, x:201034, y:61532, x:199275, y:61521, x:199279, y:60738, x:199333, y:60483, x:199318, y:60312, x:199334, y:60174, x:199600, y:60149, x:199927, y:59896, x:200136, y:59490, |x:174862, y:51326, x:175137, y:51600, x:175466, y:51626, x:175215, y:51935, x:175224, y:52309, x:176817, y:56278, x:177142, y:56711, x:172690, y:56710, x:172409, y:56781, x:172315, y:56834, x:172625, y:56569, x:172776, y:56368, x:172955, y:56175, x:173363, y:55369, x:173398, y:55150, x:173462, y:54987, x:173446, y:54501, x:173088, y:54143, x:172962, y:54134, x:173047, y:54117, x:173386, y:53778, x:173382, y:53271, x:172781, y:52038, x:172385, y:51533, x:172101, y:51258, x:172154, y:51236, x:172596, y:50971, x:173177, y:50566, x:173335, y:50420, x:173569, y:50253, x:173899, y:49909, x:174140, y:49559, |x:197054, y:53080, x:196752, y:53533, x:196616, y:53885, x:196482, y:54139, x:196321, y:54324, x:196077, y:54529, x:195844, y:54646, x:195659, y:54701, x:195549, y:54709, x:195223, y:54703, x:195218, y:53716, x:195154, y:53409, x:194821, y:53077, x:193109, y:53066, x:192799, y:53130, x:192484, y:53446, x:192449, y:53895, x:192436, y:55022, x:192415, y:55060, x:192411, y:55063, x:192395, y:55069, x:192231, y:55085, x:191875, y:55091, x:191515, y:55195, x:191247, y:55475, x:191162, y:56076, x:191561, y:56475, x:192015, y:56510, x:196407, y:56502, x:196594, y:56444, x:197034, y:56410, x:197166, y:56357, x:197356, y:56326, x:197821, y:56145, x:198427, y:55832, x:198951, y:55308, x:199212, y:54904, x:199366, y:54582, x:199469, y:54274, x:199496, y:54100, x:199561, y:53937, x:199545, y:53451, x:199187, y:53093, x:198730, y:53060, |x:187604, y:53080, x:187302, y:53533, x:187166, y:53885, x:187032, y:54139, x:186871, y:54324, x:186627, y:54529, x:186394, y:54646, x:186209, y:54701, x:186099, y:54709, x:185773, y:54703, x:185768, y:53716, x:185704, y:53409, x:185371, y:53077, x:183659, y:53066, x:183349, y:53130, x:183034, y:53446, x:182999, y:53895, x:182986, y:55022, x:182965, y:55060, x:182961, y:55063, x:182945, y:55069, x:182781, y:55085, x:182425, y:55091, x:182065, y:55195, x:181797, y:55475, x:181712, y:56076, x:182111, y:56475, x:182565, y:56510, x:186957, y:56502, x:187144, y:56444, x:187584, y:56410, x:187716, y:56357, x:187906, y:56326, x:188371, y:56145, x:188977, y:55832, x:189501, y:55308, x:189762, y:54904, x:189916, y:54582, x:190019, y:54274, x:190046, y:54100, x:190111, y:53937, x:190095, y:53451, x:189737, y:53093, x:189280, y:53060, |x:169148, y:51756, x:169374, y:51786, x:169644, y:51891, x:169734, y:51940, x:170015, y:52221, x:170205, y:52596, x:170251, y:52732, x:170285, y:52945, x:170317, y:53022, x:170361, y:53800, x:170689, y:54127, x:170433, y:54129, x:170146, y:54555, x:169848, y:55132, x:169643, y:55397, x:169318, y:55629, x:169029, y:55770, x:168726, y:55830, x:168383, y:55830, x:168368, y:54763, x:168305, y:54459, x:167972, y:54126, x:167969, y:54126, x:168026, y:54114, x:168342, y:53797, x:168375, y:53346, x:168389, y:51765, x:168480, y:51755, |x:177735, y:50597, x:177523, y:51184, x:177893, y:51501, x:177629, y:51504, x:177263, y:51867, x:176909, y:51514, x:176636, y:51499, x:177041, y:51152, x:176777, y:50592, |";
						directPolygons = MatterHackers.MatterSlice.PolygonsHelper.CreateFromString(raiseTheBarreString);
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

			if (directPolygons == null)
			{
				polygonsToPathAround = PolygonsToMSPolygons(FixWinding(VertexSourceToClipperPolygons.CreatePolygons(pathToUse, 1)));
				scale = 1;
			}
			else
			{
				polygonsToPathAround = directPolygons;
				scale = 1000;
			}


			PathStorage shapePath = VertexSourceToClipperPolygons.CreatePathStorage(MSPolygonsToPolygons(polygonsToPathAround), scale);
			graphics2D.Render(shapePath, fillColor);

			// render the travel line
			PathStorage travelLine = new PathStorage();
			travelLine.MoveTo(lineStart);
			travelLine.LineTo(mousePosition);

			Polygons travelPolysLine = VertexSourceToClipperPolygons.CreatePolygons(travelLine, 1);
			MSPolygons travelPolygons = CreateTravelPath(polygonsToPathAround, PolygonsToMSPolygons(travelPolysLine));
			PathStorage travelPath = VertexSourceToClipperPolygons.CreatePathStorage(MSPolygonsToPolygons(travelPolygons));
			travelPath.Add(0, 0, ShapePath.FlagsAndCommand.CommandStop);
			graphics2D.Render(new Stroke(travelPath), pathColor);
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