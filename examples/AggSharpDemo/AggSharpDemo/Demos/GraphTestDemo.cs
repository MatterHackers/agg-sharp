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
using System.Diagnostics;
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's graph_test.cpp: a random graph of 200 nodes and 100 directed edges, the edges drawn as lines,
	/// Bezier curves or dashed curves with arrowheads, or as filled curves (anti-aliased, or aliased through a
	/// threshold gamma), the nodes as radial-gradient discs. Draft mode draws the lines and nodes aliased, a
	/// pixel wide, through renderer_primitives.
	/// </summary>
	/// <remarks>
	/// The graph and each edge's colour come from <see cref="MsvcRand"/> seeded 100, as the reference renderer's
	/// do (C++ calls srand(100)). The type box is built at (-1, -1, -1, -1) as C++ builds it: an empty box, so
	/// no background or border shows, but its items still draw from the corner and still take clicks.
	/// <para>
	/// C++'s benchmark times the pipeline, add_path, sort and render stages separately and shows them in a
	/// message box; here Benchmark times ten whole scenes and shows the time on the demo.
	/// </para>
	/// <para>
	/// The GPU path draws the same paths through <see cref="Graphics2D.Render(IVertexSource, Color)"/>; the
	/// gradient nodes' span generator, and draft mode's aliased lines and ellipses through the same
	/// <see cref="RendererPrimitives"/>, go over a <see cref="Graphics2DSpanImage"/>, so they are the software
	/// path's pixels.
	/// </para>
	/// </remarks>
	public class GraphTestDemo : AggDemo
	{
		private const int NumNodes = 200;
		private const int NumEdges = 100;

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();
		private readonly (double X, double Y)[] nodes = new (double X, double Y)[NumNodes];
		private readonly (int Node1, int Node2)[] edges = new (int Node1, int Node2)[NumEdges];
		private readonly Color[] gradientColors = new Color[256];
		private MsvcRand rand;
		private string benchmarkReport;

		public GraphTestDemo()
		{
			// graph_test.cpp runs with flip_y = true and gives its controls !flip_y.
			this.TypeRbox = new RboxCtrl(-1, -1, -1, -1, false);
			this.WidthSlider = new SliderCtrl(110 + 80, 8.0, 110 + 200.0 + 80, 8.0 + 7.0, false) { Label = "Width={0:F2}" };
			this.BenchmarkCbox = new CboxCtrl(110 + 200 + 80 + 8, 8.0 - 2.0, "Benchmark", false);
			this.DrawNodesCbox = new CboxCtrl(110 + 200 + 80 + 8, 8.0 - 2.0 + 15.0, "Draw Nodes", false);
			this.DrawEdgesCbox = new CboxCtrl(200 + 200 + 80 + 8, 8.0 - 2.0 + 15.0, "Draw Edges", false);
			this.DraftCbox = new CboxCtrl(200 + 200 + 80 + 8, 8.0 - 2.0, "Draft Mode", false);
			this.TranslucentCbox = new CboxCtrl(110 + 80, 8.0 - 2.0 + 15.0, "Translucent Mode", false);

			this.TypeRbox.SetTextSize(8.0);
			foreach (string item in new[] { "Solid lines", "Bezier curves", "Dashed curves", "Poygons AA", "Poygons Bin" })
			{
				this.TypeRbox.AddItem(item);
			}

			this.TypeRbox.CurrentItem = 0;

			this.WidthSlider.NumSteps = 20;
			this.WidthSlider.SetRange(0.0, 5.0);
			this.WidthSlider.Value = 2.0;

			this.BenchmarkCbox.SetTextSize(8.0);
			this.DrawNodesCbox.SetTextSize(8.0);
			this.DraftCbox.SetTextSize(8.0);
			this.DrawNodesCbox.Checked = true;
			this.DrawEdgesCbox.Checked = true;

			this.ctrls.Add(this.TypeRbox);
			this.ctrls.Add(this.WidthSlider);
			this.ctrls.Add(this.BenchmarkCbox);
			this.ctrls.Add(this.DrawNodesCbox);
			this.ctrls.Add(this.DrawEdgesCbox);
			this.ctrls.Add(this.DraftCbox);
			this.ctrls.Add(this.TranslucentCbox);
			this.ctrls.Changed += (s, e) => this.OnCtrlChange();

			// C++ graph(200, 100): x from 0.2 to 0.95 and y from 0.1 to 0.95 of the window, and edges between two
			// different nodes.
			var graphRand = new MsvcRand(100);
			for (int i = 0; i < NumNodes; i++)
			{
				double x = (graphRand.Next() / 32767.0 * 0.75) + 0.2;
				double y = (graphRand.Next() / 32767.0 * 0.85) + 0.1;
				this.nodes[i] = (x, y);
			}

			for (int i = 0; i < NumEdges; i++)
			{
				int node1 = graphRand.Next() % NumNodes;
				int node2 = graphRand.Next() % NumNodes;
				this.edges[i] = (node1, node2);
				if (node1 == node2)
				{
					i--;
				}
			}

			// C++ rgba(1, 1, 0, 0.25).gradient(rgba(0, 0, 1), i / 255) into an rgba8 array.
			for (int i = 0; i < 256; i++)
			{
				double k = i / 255.0;
				this.gradientColors[i] = Rgba8.FromRgba(1 + ((0 - 1) * k), 1 + ((0 - 1) * k), 0 + ((1 - 0) * k), 0.25 + ((1 - 0.25) * k));
			}
		}

		/// <summary>C++ <c>m_type</c>: lines, curves, dashed curves, or filled curves anti-aliased or aliased.</summary>
		public RboxCtrl TypeRbox { get; }

		/// <summary>C++ <c>m_width</c>: the stroke width, and the size of the fine nodes.</summary>
		public SliderCtrl WidthSlider { get; }

		public CboxCtrl BenchmarkCbox { get; }

		public CboxCtrl DrawNodesCbox { get; }

		public CboxCtrl DrawEdgesCbox { get; }

		/// <summary>C++ <c>m_draft</c>: nodes and edges drawn aliased through renderer_primitives.</summary>
		public CboxCtrl DraftCbox { get; }

		/// <summary>C++ <c>m_translucent</c>: edges at alpha 80 instead of opaque.</summary>
		public CboxCtrl TranslucentCbox { get; }

		public override string Name => "graph_test";

		public override string Category => "Vector Graphics";

		public override string Description => "A random graph of 200 nodes and 100 arrows, drawn as lines, curves, dashed curves or filled curves, with gradient nodes. Draft Mode draws it aliased; Benchmark times it.";

		public override int Width => 700;

		public override int Height => 530;

		public override void Draw(Graphics2D graphics)
		{
			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				var target = new ImageClippingProxy(destination);
				target.clear(new ColorF(1, 1, 1));
				this.DrawScene(new SoftwareCanvas(target, this.gradientColors));
				destination.MarkImageChanged();
			}
			else
			{
				graphics.Clear(new Color(255, 255, 255));
				this.DrawScene(new GpuCanvas(graphics, this.gradientColors, this.Width, this.Height));
				graphics.FlushDeferredDraws();
			}

			if (this.benchmarkReport != null)
			{
				graphics.Render(Text(this.benchmarkReport, 10, this.Height - 60), new Color(0, 0, 0));
			}

			this.ctrls.Render(graphics);
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseDown(x, y, button);
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			this.ctrls.OnMouseMove(x, y, flags);
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.ctrls.OnMouseUp(x, y, button);
		}

		public override void OnKeyDown(Keys key, AggInputFlags flags)
		{
			this.ctrls.OnKeyDown(key);
		}

		/// <summary>The C++ report's font: gsv_text, stroked.</summary>
		private static IVertexSource Text(string text, double x, double y)
		{
			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; it is the font the rest of the demo's controls use.
			var gsv = new gsv_text();
#pragma warning restore CS0618
			gsv.size(10.0, 0.0);
			gsv.start_point(x, y);
			gsv.text(text);
			foreach (VertexData vertex in gsv.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			return new Stroke(outline, 1.2);
		}

		/// <summary>C++ <c>curve</c>: a Bezier from node to node, its control points bent off to the left of the line.</summary>
		private static IVertexSource Curve(double x1, double y1, double x2, double y2, double k = 0.5)
		{
			return new Curve4(x1, y1, x1 - ((y2 - y1) * k), y1 + ((x2 - x1) * k), x2 + ((y2 - y1) * k), y2 - ((x2 - x1) * k), x2, y2);
		}

		private static IVertexSource Line(double x1, double y1, double x2, double y2)
		{
			var line = new VertexStorage();
			line.MoveTo(x1, y1);
			line.LineTo(x2, y2);
			return line;
		}

		/// <summary>The arrowhead every edge ends in: C++ <c>ah.head(0, 10, 5, 0)</c>, placed by the edge's markers.</summary>
		private static IVertexSource Arrow(TerminalMarkers markers)
		{
			var arrowhead = new Arrowhead();
			arrowhead.Head(0, 10, 5, 0);
			return new MarkerPlacer(markers, arrowhead);
		}

		/// <summary>C++ <c>stroke_fine_arrow</c>: the stroke, shortened by twice its width to make room for the arrow.</summary>
		private static IVertexSource StrokeFine(IVertexSource source, double width)
		{
			var markers = new TerminalMarkers();
			var stroke = new Stroke(source, markers, width) { Shorten = width * 2.0 };
			return new ConcatPaths(stroke, Arrow(markers));
		}

		/// <summary>C++ <c>dash_stroke_fine_arrow</c>.</summary>
		private static IVertexSource DashStrokeFine(IVertexSource source, double width)
		{
			var markers = new TerminalMarkers();
			var dash = new Dash(source, markers) { Shorten = width * 2.0 };
			dash.AddDash(6.0, 3.0);
			return new ConcatPaths(new Stroke(dash, width), Arrow(markers));
		}

		/// <summary>C++ <c>stroke_draft_arrow</c>: the path itself, shortened by 10, for aliased lines.</summary>
		private static IVertexSource StrokeDraft(IVertexSource source)
		{
			var markers = new TerminalMarkers();
			return new ConcatPaths(new MarkerAdaptor(source, markers) { Shorten = 10.0 }, Arrow(markers));
		}

		/// <summary>C++ <c>dash_stroke_draft_arrow</c>.</summary>
		private static IVertexSource DashStrokeDraft(IVertexSource source)
		{
			var markers = new TerminalMarkers();
			var dash = new Dash(source, markers) { Shorten = 10.0 };
			dash.AddDash(6.0, 3.0);
			return new ConcatPaths(dash, Arrow(markers));
		}

		/// <summary>C++ <c>on_ctrl_change</c>: a checked Benchmark times ten scenes, then unchecks itself.</summary>
		private void OnCtrlChange()
		{
			if (this.BenchmarkCbox.Checked)
			{
				var scratch = new ImageBuffer(this.Width, this.Height);
				var canvas = new SoftwareCanvas(new ImageClippingProxy(scratch), this.gradientColors);
				var stopwatch = Stopwatch.StartNew();
				for (int i = 0; i < 10; i++)
				{
					this.DrawScene(canvas);
				}

				this.benchmarkReport = string.Format(CultureInfo.InvariantCulture, "{0:F3} milliseconds", stopwatch.Elapsed.TotalMilliseconds);
				this.BenchmarkCbox.Checked = false;
			}

			this.Invalidate();
		}

		private (double X, double Y) Node(int index)
		{
			return (this.nodes[index].X * this.Width, this.nodes[index].Y * this.Height);
		}

		/// <summary>C++ <c>draw_scene</c>. The edge colours restart from srand(100) every time.</summary>
		private void DrawScene(ICanvas canvas)
		{
			this.rand = new MsvcRand(100);
			bool draft = this.DraftCbox.Checked;
			double width = this.WidthSlider.Value;
			if (this.DrawNodesCbox.Checked)
			{
				for (int i = 0; i < NumNodes; i++)
				{
					var (x, y) = this.Node(i);
					if (draft)
					{
						canvas.DraftNode(x, y, this.gradientColors[147], this.gradientColors[255], this.gradientColors[50]);
					}
					else
					{
						canvas.GradientNode(x, y, width);
					}
				}
			}

			if (!this.DrawEdgesCbox.Checked)
			{
				return;
			}

			int type = this.TypeRbox.CurrentItem;
			if (draft && type > 2)
			{
				// C++ draws no edges for the polygon types in draft mode.
				return;
			}

			for (int i = 0; i < NumEdges; i++)
			{
				var (x1, y1) = this.Node(this.edges[i].Node1);
				var (x2, y2) = this.Node(this.edges[i].Node2);
				IVertexSource edge;
				switch (type)
				{
					case 0:
						edge = draft ? StrokeDraft(Line(x1, y1, x2, y2)) : StrokeFine(Line(x1, y1, x2, y2), width);
						break;

					case 1:
						edge = draft ? StrokeDraft(Curve(x1, y1, x2, y2)) : StrokeFine(Curve(x1, y1, x2, y2), width);
						break;

					case 2:
						edge = draft ? DashStrokeDraft(Curve(x1, y1, x2, y2)) : DashStrokeFine(Curve(x1, y1, x2, y2), width);
						break;

					default:
						edge = Curve(x1, y1, x2, y2);
						break;
				}

				int r = this.rand.Next() & 0x7F;
				int g = this.rand.Next() & 0x7F;
				int b = this.rand.Next() & 0x7F;
				int a = this.TranslucentCbox.Checked ? 80 : 255;
				Color color = SrgbLut.FromSrgba8(r, g, b, a);
				if (draft)
				{
					canvas.AliasedLines(edge, color);
				}
				else if (type == 4)
				{
					canvas.ThresholdFill(edge, color);
				}
				else
				{
					canvas.Fill(edge, color);
				}
			}
		}

		/// <summary>Where <see cref="DrawScene"/> draws: the software reference or a GPU surface.</summary>
		private interface ICanvas
		{
			/// <summary>C++ <c>draw_nodes_draft</c>'s node: an outlined ellipse of radius 10 and a dot of radius 4.</summary>
			void DraftNode(double x, double y, Color fill, Color line, Color dot);

			/// <summary>C++ <c>draw_nodes_fine</c>'s node: a disc of radius 5 * width through the radial gradient.</summary>
			void GradientNode(double x, double y, double width);

			/// <summary>The path as one-pixel aliased lines (C++ rasterizer_outline over renderer_primitives).</summary>
			void AliasedLines(IVertexSource path, Color color);

			/// <summary>The path filled anti-aliased (renderer_scanline_aa_solid).</summary>
			void Fill(IVertexSource path, Color color);

			/// <summary>The path filled through gamma_threshold(0.5) by renderer_scanline_bin_solid.</summary>
			void ThresholdFill(IVertexSource path, Color color);
		}

		/// <summary>The software reference: C++'s renderers, one rasterizer shared as C++ shares it.</summary>
		private sealed class SoftwareCanvas : ICanvas
		{
			private readonly ImageClippingProxy target;
			private readonly RendererPrimitives primitives;
			private readonly ScanlineRasterizer rasterizer = new ScanlineRasterizer();
			private readonly scanline_unpacked_8 scanline = new scanline_unpacked_8();
			private readonly ColorArrayFunction gradientColors;

			public SoftwareCanvas(ImageClippingProxy target, Color[] gradientColors)
			{
				this.target = target;
				this.primitives = new RendererPrimitives(target);
				this.gradientColors = new ColorArrayFunction(gradientColors);
			}

			public void DraftNode(double x, double y, Color fill, Color line, Color dot)
			{
				GraphTestDemo.DraftNode(this.primitives, x, y, fill, line, dot);
			}

			public void GradientNode(double x, double y, double width)
			{
				Affine matrix = Affine.NewScaling(width / 2.0) * Affine.NewTranslation(x, y);
				matrix.invert();
				var spans = new span_gradient(new span_interpolator_linear(matrix), new gradient_radial_d(), this.gradientColors, 0.0, 10.0);
				this.rasterizer.add_path(new Ellipse(x, y, 5.0 * width, 5.0 * width));
				new ScanlineRenderer().GenerateAndRender(this.rasterizer, this.scanline, this.target, new span_allocator(), spans);
			}

			public void AliasedLines(IVertexSource path, Color color)
			{
				GraphTestDemo.AliasedLines(this.primitives, path, color);
			}

			public void Fill(IVertexSource path, Color color)
			{
				this.rasterizer.add_path(path);
				new ScanlineRenderer().RenderSolid(this.target, this.rasterizer, this.scanline, color);
			}

			public void ThresholdFill(IVertexSource path, Color color)
			{
				this.rasterizer.gamma(new gamma_threshold(0.5));
				this.rasterizer.add_path(path);
				ScanlineBoolean2Demo.RenderScanlines(this.rasterizer, this.scanline, new BinSolidScanlineSink(this.target, color));
				this.rasterizer.gamma(new gamma_none());
			}
		}

		/// <summary>C++ <c>draw_nodes_draft</c>'s node: an outlined circle of radius 10 with a dot of radius 4.</summary>
		private static void DraftNode(RendererPrimitives primitives, double x, double y, Color fill, Color line, Color dot)
		{
			primitives.FillColor = fill;
			primitives.LineColor = line;
			primitives.OutlinedEllipse((int)x, (int)y, 10, 10);
			primitives.FillColor = dot;
			primitives.SolidEllipse((int)x, (int)y, 4, 4);
		}

		private static void AliasedLines(RendererPrimitives primitives, IVertexSource path, Color color)
		{
			primitives.LineColor = color;
			new RasterizerOutline(primitives).AddPath(path);
		}

		/// <summary>
		/// A GPU surface: the same paths, anti-aliased. The gradient nodes are the span generator's exact pixels and
		/// draft mode's aliased lines and ellipses renderer_primitives' pixels, both through a Graphics2DSpanImage.
		/// </summary>
		private sealed class GpuCanvas : ICanvas
		{
			private readonly Graphics2D graphics;
			private readonly ColorArrayFunction gradientColors;
			private readonly RendererPrimitives primitives;
			private readonly ImageClippingProxy spans;
			private readonly ScanlineRasterizer rasterizer = new ScanlineRasterizer();
			private readonly scanline_unpacked_8 scanline = new scanline_unpacked_8();

			public GpuCanvas(Graphics2D graphics, Color[] gradientColors, int width, int height)
			{
				this.graphics = graphics;
				this.gradientColors = new ColorArrayFunction(gradientColors);
				this.spans = new ImageClippingProxy(new Graphics2DSpanImage(graphics, width, height));
				this.primitives = new RendererPrimitives(this.spans);
			}

			public void DraftNode(double x, double y, Color fill, Color line, Color dot)
			{
				GraphTestDemo.DraftNode(this.primitives, x, y, fill, line, dot);
			}

			public void GradientNode(double x, double y, double width)
			{
				Affine matrix = Affine.NewScaling(width / 2.0) * Affine.NewTranslation(x, y);
				matrix.invert();
				var spanGenerator = new span_gradient(new span_interpolator_linear(matrix), new gradient_radial_d(), this.gradientColors, 0.0, 10.0);
				this.rasterizer.add_path(new Ellipse(x, y, 5.0 * width, 5.0 * width));
				new ScanlineRenderer().GenerateAndRender(this.rasterizer, this.scanline, this.spans, new span_allocator(), spanGenerator);
			}

			public void AliasedLines(IVertexSource path, Color color)
			{
				GraphTestDemo.AliasedLines(this.primitives, path, color);
			}

			public void Fill(IVertexSource path, Color color)
			{
				this.graphics.Render(path, color);
			}

			public void ThresholdFill(IVertexSource path, Color color)
			{
				this.graphics.Render(path, color);
			}
		}

		/// <summary>C++ <c>pod_auto_array&lt;rgba8, 256&gt;</c> as span_gradient's color function.</summary>
		private sealed class ColorArrayFunction : IColorFunction
		{
			private readonly Color[] colors;

			public ColorArrayFunction(Color[] colors)
			{
				this.colors = colors;
			}

			public Color this[int v] => this.colors[v];

			public int size() => 256;
		}
	}
}
