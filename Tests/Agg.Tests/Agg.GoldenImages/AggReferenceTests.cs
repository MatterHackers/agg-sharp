/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// agg-sharp's software renderer against C++ AGG, byte for byte. Each test mirrors one
	/// tools/cpp-renderer/src/demo_*.cpp scene through agg-sharp production code; see
	/// <see cref="AggReference"/> for the golden format.
	/// </summary>
	public class AggReferenceTests
	{
		/// <summary>
		/// demo_simple_line.cpp: three closed polylines through the anti-aliased outline rasterizer
		/// (1px line profile, round joins, no round caps), black on opaque white RGBA. Only the two
		/// polygons appear: the third path has two vertices, and rasterizer_outline_aa draws nothing for a
		/// closed path with fewer than three - C++ AGG agrees, so it stays in as part of the reference.
		/// </summary>
		[Test]
		public async Task SimpleLineMatchesCppAgg()
		{
			var image = new ImageBuffer(512, 512, 32, new BlenderRGBA());
			var destination = new ImageClippingProxy(image);
			destination.clear(new ColorF(1, 1, 1, 1));

			var profile = new LineProfileAnitAlias(1.0, new gamma_none());
			var outlineRenderer = new OutlineRenderer(destination, profile);
			var rasterizer = new rasterizer_outline_aa(outlineRenderer);
			rasterizer.round_cap(false);
			rasterizer.line_join(rasterizer_outline_aa.outline_aa_join_e.outline_round_join);

			var path = new VertexStorage();
			path.MoveTo(50, 50);
			path.LineTo(150, 50);
			path.LineTo(100, 150);
			path.ClosePolygon();

			path.MoveTo(200, 100);
			path.LineTo(250, 70);
			path.LineTo(280, 110);
			path.LineTo(260, 160);
			path.LineTo(210, 150);
			path.ClosePolygon();

			path.MoveTo(50, 200);
			path.LineTo(200, 250);
			path.ClosePolygon();

			var transform = Affine.NewIdentity();
			transform *= Affine.NewRotation(Math.PI);
			transform *= Affine.NewTranslation(256, 256.5);

			rasterizer.RenderAllPaths(
				new VertexSourceApplyTransform(path, transform),
				new[] { new Color(0, 0, 0, 255) },
				new[] { 0 },
				1);

			await AggReference.Check(image, "simple_line_512x512");
		}

		/// <summary>
		/// demo_lion.cpp at its default state without the alpha slider: the filled lion, every path at alpha
		/// 0.1 (25), rotated by pi and centered in a 512x400 BGR24 view cleared to white. Almost every pixel is
		/// a partial blend, so this pins the blenders' rounding and the cover-times-alpha multiply to C++'s.
		/// </summary>
		[Test]
		public async Task LionMatchesCppAgg()
		{
			var image = new ImageBuffer(512, 400, 24, new BlenderBGR());
			var graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);

			var lion = new LionShape();
			lion.Render(graphics2D, lion.GetDemoTransform(image.Width, image.Height), (byte)(0.1 * 255));

			await AggReference.Check(image, "lion_512x400_nocontrols");
		}

		/// <summary>
		/// demo_conv_dash_marker.cpp without the ctrls: the two half-transparent triangles; with smoothing, the
		/// conv_smooth_poly1 output filled and stroked as it comes (its control points drawn as vertices, as
		/// C++ does); then the path - flattened through conv_curve after smoothing, or raw with skip_smooth -
		/// dashed (20/5, 5/5, 5/5 from 10), stroked, and the arrowheads conv_marker places from the dash's
		/// vcgen_markers_term, all in one black pass. Open with butt caps at width 3 (head and tail), then
		/// closed with round caps, width 6 and even-odd fill (head only, and the closing edges dashed).
		/// </summary>
		[Test]
		[Arguments(false, 0, 3.0, false, 0.0, "conv_dash_marker_500x330_nosmooth")]
		[Arguments(true, 2, 6.0, true, 0.0, "conv_dash_marker_500x330_nosmooth_closed_round")]
		[Arguments(false, 0, 3.0, false, 1.0, "conv_dash_marker_500x330_nocontrols")]
		[Arguments(true, 2, 6.0, true, 1.5, "conv_dash_marker_500x330_nocontrols_closed_round")]
		// smoothValue 0 stands for the demo's skip_smooth.
		public async Task DashesAndArrowheadsMatchCppAgg(bool close, int capItem, double width, bool evenOdd, double smoothValue, string golden)
		{
			var image = new ImageBuffer(500, 330, 24, new BlenderBGR());
			var destination = new ImageClippingProxy(image);
			destination.clear(new ColorF(1, 1, 1, 1));
			var rasterizer = new ScanlineRasterizer();
			var scanline = new ScanlineCachePacked8();
			var renderer = new ScanlineRenderer();

			double[] x = { 57 + 100, 369 + 100, 143 + 100 };
			double[] y = { 60, 170, 310 };
			var path = new VertexStorage();
			path.MoveTo(x[0], y[0]);
			path.LineTo(x[1], y[1]);
			path.LineTo((x[0] + x[1] + x[2]) / 3.0, (y[0] + y[1] + y[2]) / 3.0);
			path.LineTo(x[2], y[2]);
			if (close)
			{
				path.ClosePolygon();
			}

			path.MoveTo((x[0] + x[1]) / 2, (y[0] + y[1]) / 2);
			path.LineTo((x[1] + x[2]) / 2, (y[1] + y[2]) / 2);
			path.LineTo((x[2] + x[0]) / 2, (y[2] + y[0]) / 2);
			if (close)
			{
				path.ClosePolygon();
			}

			if (evenOdd)
			{
				rasterizer.filling_rule(Util.filling_rule_e.fill_even_odd);
			}

			rasterizer.add_path(path);
			renderer.RenderSolid(destination, rasterizer, scanline, Rgba8.FromRgba(0.7, 0.5, 0.1, 0.5));

			// C++ dashes conv_curve<path>; with no curve commands in the path that is the path itself.
			IVertexSource dashed = path;
			if (smoothValue > 0)
			{
				var smooth = new SmoothPolygon(path) { SmoothValue = smoothValue };
				rasterizer.add_path(smooth);
				renderer.RenderSolid(destination, rasterizer, scanline, Rgba8.FromRgba(0.1, 0.5, 0.7, 0.1));

				rasterizer.add_path(new Stroke(smooth));
				renderer.RenderSolid(destination, rasterizer, scanline, Rgba8.FromRgba(0.0, 0.6, 0.0, 0.8));

				dashed = new SmoothPolygonCurve(path) { SmoothValue = smoothValue };
			}

			var markers = new TerminalMarkers();
			var dash = new Dash(dashed, markers);
			var stroke = new Stroke(dash, width);
			stroke.LineCap = capItem == 1 ? LineCap.Square : capItem == 2 ? LineCap.Round : LineCap.Butt;

			double k = Math.Pow(width, 0.7);
			var arrowhead = new Arrowhead();
			arrowhead.Head(4 * k, 4 * k, 3 * k, 2 * k);
			if (!close)
			{
				arrowhead.Tail(1 * k, 1.5 * k, 3 * k, 5 * k);
			}

			var arrows = new MarkerPlacer(markers, arrowhead);

			dash.AddDash(20.0, 5.0);
			dash.AddDash(5.0, 5.0);
			dash.AddDash(5.0, 5.0);
			dash.DashStart(10);

			// The arrows read the markers the stroke's pass over the dash collected, so the order matters.
			rasterizer.add_path(stroke);
			rasterizer.add_path(arrows);
			renderer.RenderSolid(destination, rasterizer, scanline, new Color(0, 0, 0, 255));

			await AggReference.Check(image, golden);
		}

		/// <summary>
		/// demo_bspline.cpp without the polygon tool and ctrls: the six default points of bspline.cpp in a
		/// 600x600 view, through conv_bspline at 1/numPoints and stroked 2 wide in black. Open at the default 20
		/// steps per span, then closed at 7 (so the samples miss the source vertices and the closing wrap shows).
		/// </summary>
		[Test]
		[Arguments(20.0, false, "bspline_600x600_nocontrols")]
		[Arguments(7.0, true, "bspline_600x600_nocontrols_closed")]
		public async Task BSplineMatchesCppAgg(double numPoints, bool close, string golden)
		{
			var image = new ImageBuffer(600, 600, 24, new BlenderBGR());
			var destination = new ImageClippingProxy(image);
			destination.clear(new ColorF(1, 1, 1, 1));
			var rasterizer = new ScanlineRasterizer();
			var scanline = new ScanlineCachePacked8();
			var renderer = new ScanlineRenderer();

			var path = new VertexStorage();
			path.MoveTo(100, 100);
			path.LineTo(500, 100);
			path.LineTo(500, 500);
			path.LineTo(100, 500);
			path.LineTo(300, 300);
			path.LineTo(300, 200);
			if (close)
			{
				path.ClosePolygon();
			}

			var bspline = new BSplinePath(path) { InterpolationStep = 1.0 / numPoints };
			rasterizer.add_path(new Stroke(bspline, 2.0));
			renderer.RenderSolid(destination, rasterizer, scanline, new Color(0, 0, 0, 255));

			await AggReference.Check(image, golden);
		}
	}
}
