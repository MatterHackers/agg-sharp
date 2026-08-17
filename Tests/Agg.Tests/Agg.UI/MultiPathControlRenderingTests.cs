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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// gamma_ctrl and spline_ctrl are multi-path vertex sources that hand themselves to the renderer once
	/// per path. Both overrode OnDraw to run that loop by hand - <c>Rewind(i)</c> then
	/// <c>Graphics2D.Render(this, color)</c> - and then called base, which runs the very same loop. The
	/// hand-rolled half could never select a path: every consumer re-rewinds before it reads, the GPU path
	/// (Graphics2DGpu.DrawAAShape, through GetLongHashCode and SendShapeToTesselator) by pulling
	/// <see cref="SimpleVertexSourceWidget.Vertices"/>, which opens with <c>Rewind(CurrentPathIndex)</c>,
	/// and the software rasterizer by calling <c>Rewind(0)</c> outright in ScanlineRasterizer.add_path.
	/// So those passes each painted path 0 again, in the wrong color - the spline control repainted its
	/// whole background five times over, ending in the active point's red.
	///
	/// These tests drive the controls' real OnDraw against a Graphics2D that drains Vertices() the way the
	/// GPU renderer does, and assert each path is drawn exactly once with its own geometry.
	/// </summary>
	public class MultiPathControlRenderingTests
	{
		[Test]
		public async Task GammaControlDrawsEachPathOnce()
		{
			var gamma = new gamma_ctrl(Vector2.Zero, new Vector2(200, 120));

			var recorder = new RecordingGraphics2D(new ImageBuffer(220, 140));
			gamma.OnDraw(recorder);

			var passes = recorder.RendersOf(gamma);

			// curve, grid, point 1, point 2 and the vestigial fifth path - the background fill and the
			// separately built border are their own vertex sources, so they are not in this list
			await Assert.That(passes.Count).IsEqualTo(5);

			// The grid is a fixed 20 vertex, 4 subpath figure, so it pins itself exactly
			var grid = passes[1];
			await Assert.That(grid.Count).IsEqualTo(21); // 20 vertices plus the trailing Stop
			await Assert.That(grid[20].Command).IsEqualTo(FlagsAndCommand.Stop);
			foreach (var moveToIndex in new[] { 0, 4, 8, 14 })
			{
				await Assert.That(grid[moveToIndex].Command).IsEqualTo(FlagsAndCommand.MoveTo);
			}

			await Assert.That(MoveToCount(grid)).IsEqualTo(4);

			// Both handles are 32 step ellipses of radius m_point_size (5), drawn at different places
			var inactivePoint = BoundsOf(passes[2]);
			var activePoint = BoundsOf(passes[3]);

			await Assert.That(inactivePoint.Width).IsEqualTo(10.0).Within(0.5);
			await Assert.That(inactivePoint.Height).IsEqualTo(10.0).Within(0.5);
			await Assert.That(activePoint.Width).IsEqualTo(10.0).Within(0.5);
			await Assert.That(activePoint.Height).IsEqualTo(10.0).Within(0.5);
			await Assert.That(passes[2].Count).IsGreaterThan(30);
			await Assert.That(passes[3].Count).IsGreaterThan(30);

			await Assert.That((inactivePoint.Center - activePoint.Center).Length).IsGreaterThan(20.0);

			// The curve runs across the spline box, so it cannot be mistaken for either handle
			var curve = BoundsOf(passes[0]);
			await Assert.That(curve.Width).IsGreaterThan(50.0);

			// num_paths() still claims five, but Vertex has no case 4 - it stops immediately, so the pass
			// draws nothing. The text that path used to carry is a child TextWidget now.
			await Assert.That(passes[4].Count).IsEqualTo(1);
			await Assert.That(passes[4][0].Command).IsEqualTo(FlagsAndCommand.Stop);
		}

		[Test]
		public async Task SplineControlDrawsEachPathOnce()
		{
			var spline = new spline_ctrl(Vector2.Zero, new Vector2(200, 100), 6);
			spline.active_point(1);

			var recorder = new RecordingGraphics2D(new ImageBuffer(220, 120));
			spline.OnDraw(recorder);

			var passes = recorder.RendersOf(spline);

			// background, border, curve, inactive points, active point
			await Assert.That(passes.Count).IsEqualTo(5);

			// Background is a single quad
			await Assert.That(passes[0].Count).IsEqualTo(5); // 4 corners plus Stop
			await Assert.That(MoveToCount(passes[0])).IsEqualTo(1);
			var background = BoundsOf(passes[0]);
			await Assert.That(background.Width).IsEqualTo(200.0).Within(0.001);
			await Assert.That(background.Height).IsEqualTo(100.0).Within(0.001);

			// Border is two nested rings, so two subpaths
			await Assert.That(passes[1].Count).IsEqualTo(9); // 8 corners plus Stop
			await Assert.That(MoveToCount(passes[1])).IsEqualTo(2);

			// The curve is a stroked 256 segment polyline
			await Assert.That(passes[2].Count).IsGreaterThan(200);

			// Five inactive handles (six points, one of them active) each open with a MoveTo
			await Assert.That(MoveToCount(passes[3])).IsEqualTo(5);

			// The active handle is one ellipse of radius m_point_size (3) at control point 1, which sits at
			// (m_xs1 + (m_xs2 - m_xs1) * 0.2, m_ys1 + (m_ys2 - m_ys1) * 0.5) for a 200x100 control with a
			// border width of 1
			await Assert.That(MoveToCount(passes[4])).IsEqualTo(1);
			var activePoint = BoundsOf(passes[4]);
			await Assert.That(activePoint.Center.X).IsEqualTo(40.6).Within(0.1);
			await Assert.That(activePoint.Center.Y).IsEqualTo(50.0).Within(0.1);
			await Assert.That(activePoint.Width).IsEqualTo(6.0).Within(0.3);
			await Assert.That(activePoint.Height).IsEqualTo(6.0).Within(0.3);
		}

		private static int MoveToCount(List<VertexData> vertices)
		{
			return vertices.Count(vertex => vertex.Command == FlagsAndCommand.MoveTo);
		}

		/// <summary>
		/// The bounds of the drawn points only. EndPoly and Stop carry no position, so including them would
		/// drag every figure's box back to the origin.
		/// </summary>
		private static RectangleDouble BoundsOf(List<VertexData> vertices)
		{
			var bounds = new RectangleDouble(double.MaxValue, double.MaxValue, double.MinValue, double.MinValue);
			foreach (var vertex in vertices)
			{
				if (ShapePath.IsVertex(vertex.Command))
				{
					bounds.ExpandToInclude(vertex.Position);
				}
			}

			return bounds;
		}

		/// <summary>
		/// An ImageGraphics2D that records what each fill would have drawn instead of rasterizing it,
		/// draining the source through Vertices() exactly as the GPU renderer does.
		/// </summary>
		private class RecordingGraphics2D : ImageGraphics2D
		{
			private readonly List<(IVertexSource Source, List<VertexData> Vertices)> renders = new List<(IVertexSource, List<VertexData>)>();

			public RecordingGraphics2D(ImageBuffer destImage)
				: base()
			{
				Initialize(new ImageClippingProxy(destImage), new ScanlineRasterizer());
				ScanlineCache = new ScanlineCachePacked8();
			}

			/// <summary>
			/// The vertices of every fill of <paramref name="source"/>, in draw order. Fills of anything else
			/// (backgrounds, the gamma control's separately built border, child widgets) are skipped.
			/// </summary>
			public List<List<VertexData>> RendersOf(IVertexSource source)
			{
				return renders.Where(render => ReferenceEquals(render.Source, source))
					.Select(render => render.Vertices)
					.ToList();
			}

			protected override void RenderVertexSource(IVertexSource vertexSource, IColorType colorType)
			{
				renders.Add((vertexSource, vertexSource.Vertices().ToList()));
			}
		}
	}
}
