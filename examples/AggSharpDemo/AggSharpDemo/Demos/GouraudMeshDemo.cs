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

using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's gouraud_mesh.cpp: a 20 by 20 mesh of Gouraud-shaded triangles drawn in one pass by a compound
	/// rasterizer, each triangle a style with its own span_gouraud_rgba. An edge carries the triangles on both
	/// its sides, so shared edges are anti-aliased once and the triangles meet without seams. The points drift
	/// and the colours cycle every frame; drag a point to move it.
	/// </summary>
	/// <remarks>
	/// The jitter and colours come from <see cref="MsvcRand"/> in argument order, as the reference renderer's do.
	/// A GPU surface draws the same pixels through <see cref="IGouraudGraphics.FillGouraudCompound"/>. A surface with
	/// neither a byte back buffer nor <see cref="IGouraudGraphics"/> - LcdBufferGraphics2D, whose rasterizer draws into
	/// no byte buffer, or any other Graphics2D - gets vertex-coloured primitives, without the compound rasterizer's
	/// stitched anti-aliasing.
	/// </remarks>
	public class GouraudMeshDemo : AggDemo
	{
		private const int Cols = 20;
		private const int Rows = 20;
		private const double CellW = 17;
		private const double CellH = 17;
		private const double StartX = 40;
		private const double StartY = 40;

		private readonly List<MeshPoint> vertices = new List<MeshPoint>();
		private readonly List<(int P1, int P2, int P3)> triangles = new List<(int P1, int P2, int P3)>();
		private readonly List<(int P1, int P2, int Tl, int Tr)> edges = new List<(int P1, int P2, int Tl, int Tr)>();
		private readonly MsvcRand rand = new MsvcRand();
		private int dragIndex = -1;
		private double dragDx;
		private double dragDy;

		public GouraudMeshDemo()
		{
			this.Generate();
			this.WaitMode = false;
		}

		public override string Name => "gouraud_mesh";

		public override string Category => "Vector Graphics";

		public override string Description => "A mesh of Gouraud-shaded triangles drawn in one pass by a compound rasterizer, so shared edges meet without seams. The points drift and the colours cycle; drag a point to move it.";

		public override int Width => 400;

		public override int Height => 400;

		/// <summary>
		/// The time the report shows, in milliseconds, instead of the measured one. C++ reports how long the mesh
		/// took to draw, which no reference image can hold; the reference frames set this.
		/// </summary>
		public double? ReportedMilliseconds { get; set; }

		public override void Draw(Graphics2D graphics)
		{
			// The rasterizer is asked first so a GPU surface is never asked for DestImage, which would make it
			// allocate a full-screen CPU layer. The reference frame has no transform, so pixels are frame pixels.
			if (graphics.Rasterizer != null && graphics.DestImage is IImageByte destination)
			{
				// C++ draws everything through pixfmt_bgra32_pre.
				var preView = new ImageBuffer();
				preView.Attach(destination, new BlenderPreMultBGRA());
				var target = new ImageClippingProxy(preView);
				target.clear(new ColorF(0, 0, 0));

				var stopwatch = Stopwatch.StartNew();
				var rasc = new rasterizer_compound_aa();
				foreach (var edge in this.edges)
				{
					MeshPoint p1 = this.vertices[edge.P1];
					MeshPoint p2 = this.vertices[edge.P2];
					rasc.styles(edge.Tl, edge.Tr);
					rasc.move_to_d(p1.X, p1.Y);
					rasc.line_to_d(p2.X, p2.Y);
				}

				new ScanlineRenderer().RenderCompound(rasc, new scanline_unpacked_8(), new scanline_bin(), target, new span_allocator(), new GouraudStyles(this));
				double milliseconds = stopwatch.Elapsed.TotalMilliseconds;

				var rasterizer = new ScanlineRasterizer();
				rasterizer.add_path(this.Report(milliseconds));
				new ScanlineRenderer().RenderSolid(target, rasterizer, new scanline_unpacked_8(), new Color(255, 255, 255));
				destination.MarkImageChanged();
			}
			else if (graphics is IGouraudGraphics gouraudGraphics)
			{
				// A GPU surface: the compound rasterizer's pixels, each triangle's coverage-weighted span colour
				// summed where triangles share a pixel.
				graphics.Clear(new Color(0, 0, 0));
				var stopwatch = Stopwatch.StartNew();
				gouraudGraphics.FillGouraudCompound(new GouraudStyles(this).Spans);
				graphics.Render(this.Report(stopwatch.Elapsed.TotalMilliseconds), new Color(255, 255, 255));
			}
			else
			{
				graphics.Clear(new Color(0, 0, 0));
				var stopwatch = Stopwatch.StartNew();
				Affine transform = graphics.GetTransform();
				var primitives = new PosColorVertex[this.triangles.Count * 3];
				for (int i = 0; i < this.triangles.Count; i++)
				{
					var t = this.triangles[i];
					primitives[(i * 3) + 0] = this.Primitive(transform, t.P1);
					primitives[(i * 3) + 1] = this.Primitive(transform, t.P2);
					primitives[(i * 3) + 2] = this.Primitive(transform, t.P3);
				}

				graphics.DrawColoredPrimitives(DrawTopology.TriangleList, primitives);
				graphics.Render(this.Report(stopwatch.Elapsed.TotalMilliseconds), new Color(255, 255, 255));
			}
		}

		/// <summary>C++ <c>on_idle</c>: every point drifts one step and every colour but the first cycles.</summary>
		public override void OnIdle()
		{
			this.RandomizePoints();
			this.RotateColors();
			this.Invalidate();
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (!button.HasFlag(AggInputFlags.MouseLeft))
			{
				return;
			}

			for (int i = 0; i < this.vertices.Count; i++)
			{
				MeshPoint p = this.vertices[i];
				if (agg_math.CalcDistance(x, y, p.X, p.Y) < 5)
				{
					this.dragIndex = i;
					this.dragDx = x - p.X;
					this.dragDy = y - p.Y;
					return;
				}
			}
		}

		public override void OnMouseMove(int x, int y, AggInputFlags flags)
		{
			if (!flags.HasFlag(AggInputFlags.MouseLeft))
			{
				this.dragIndex = -1;
				return;
			}

			if (this.dragIndex >= 0)
			{
				MeshPoint p = this.vertices[this.dragIndex];
				p.X = x - this.dragDx;
				p.Y = y - this.dragDy;
				this.vertices[this.dragIndex] = p;
				this.Invalidate();
			}
		}

		public override void OnMouseUp(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			this.dragIndex = -1;
		}

		/// <summary>C++ <c>random(v1, v2)</c>: one of 1000 evenly spaced values from v1 to v2.</summary>
		private double Random(double v1, double v2)
		{
			return ((v2 - v1) * (this.rand.Next() % 1000) / 999.0) + v1;
		}

		/// <summary>C++ <c>mesh_ctrl::generate(20, 20, 17, 17, 40, 40)</c>.</summary>
		private void Generate()
		{
			double y = StartY;
			for (int i = 0; i < Rows; i++)
			{
				double x = StartX;
				for (int j = 0; j < Cols; j++)
				{
					var p = new MeshPoint { X = x, Y = y };
					p.Dx = this.Random(-0.5, 0.5);
					p.Dy = this.Random(-0.5, 0.5);
					p.R = this.rand.Next() & 0xFF;
					p.G = this.rand.Next() & 0xFF;
					p.B = this.rand.Next() & 0xFF;
					p.Dr = this.rand.Next() & 1;
					p.Dg = this.rand.Next() & 1;
					p.Db = this.rand.Next() & 1;
					this.vertices.Add(p);
					x += CellW;
				}

				y += CellH;
			}

			//  4---3
			//  |t2/|
			//  | / |
			//  |/t1|
			//  1---2
			for (int i = 0; i < Rows - 1; i++)
			{
				for (int j = 0; j < Cols - 1; j++)
				{
					int p1 = (i * Cols) + j;
					int p2 = p1 + 1;
					int p3 = p2 + Cols;
					int p4 = p1 + Cols;
					this.triangles.Add((p1, p2, p3));
					this.triangles.Add((p3, p4, p1));

					int currCell = (i * (Cols - 1)) + j;
					int leftCell = j != 0 ? currCell - 1 : -1;
					int bottCell = i != 0 ? currCell - (Cols - 1) : -1;
					int currT1 = currCell * 2;
					int currT2 = currT1 + 1;
					int leftT1 = leftCell >= 0 ? leftCell * 2 : -1;
					int bottT2 = bottCell >= 0 ? (bottCell * 2) + 1 : -1;

					this.edges.Add((p1, p2, currT1, bottT2));
					this.edges.Add((p1, p3, currT2, currT1));
					this.edges.Add((p1, p4, leftT1, currT2));
					if (j == Cols - 2)
					{
						this.edges.Add((p2, p3, currT1, -1));
					}

					if (i == Rows - 2)
					{
						this.edges.Add((p3, p4, currT2, -1));
					}
				}
			}
		}

		/// <summary>C++ <c>randomize_points</c>: each point steps by its drift, bouncing inside a quarter cell of home.</summary>
		private void RandomizePoints()
		{
			for (int i = 0; i < Rows; i++)
			{
				for (int j = 0; j < Cols; j++)
				{
					double xc = (j * CellW) + StartX;
					double yc = (i * CellH) + StartY;
					double x1 = xc - (CellW / 4);
					double y1 = yc - (CellH / 4);
					double x2 = xc + (CellW / 4);
					double y2 = yc + (CellH / 4);

					// C++ indexes vertex(x, y) by the row count; the mesh is square, so that is the column count too.
					int index = (i * Rows) + j;
					MeshPoint p = this.vertices[index];
					p.X += p.Dx;
					p.Y += p.Dy;
					if (p.X < x1)
					{
						p.X = x1;
						p.Dx = -p.Dx;
					}

					if (p.Y < y1)
					{
						p.Y = y1;
						p.Dy = -p.Dy;
					}

					if (p.X > x2)
					{
						p.X = x2;
						p.Dx = -p.Dx;
					}

					if (p.Y > y2)
					{
						p.Y = y2;
						p.Dy = -p.Dy;
					}

					this.vertices[index] = p;
				}
			}
		}

		/// <summary>C++ <c>rotate_colors</c>: every channel but the first point's steps 5 up or down, turning at 0 and 255.</summary>
		private void RotateColors()
		{
			for (int i = 1; i < this.vertices.Count; i++)
			{
				MeshPoint p = this.vertices[i];
				Step(ref p.R, ref p.Dr);
				Step(ref p.G, ref p.Dg);
				Step(ref p.B, ref p.Db);
				this.vertices[i] = p;
			}

			static void Step(ref int value, ref int direction)
			{
				value += direction != 0 ? 5 : -5;
				if (value < 0)
				{
					value = 0;
					direction ^= 1;
				}

				if (value > 255)
				{
					value = 255;
					direction ^= 1;
				}
			}
		}

		/// <summary>The C++ report, "%3.2f ms, %d triangles, %.0f tri/sec", in gsv_text stroked 1.5 wide and round.</summary>
		private IVertexSource Report(double measuredMilliseconds)
		{
			double milliseconds = this.ReportedMilliseconds ?? measuredMilliseconds;
			string report = string.Format(
				CultureInfo.InvariantCulture,
				"{0:F2} ms, {1} triangles, {2:F0} tri/sec",
				milliseconds,
				this.triangles.Count,
				this.triangles.Count / milliseconds * 1000.0);

			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; gouraud_mesh.cpp draws its report with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.size(10.0, 0.0);
			text.start_point(10.0, 10.0);
			text.text(report);
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			return new Stroke(outline, 1.5) { LineJoin = LineJoin.Round, LineCap = LineCap.Round };
		}

		/// <summary>A point's linear colour: C++ keeps srgba8 and converts it as the span generator takes it.</summary>
		private Color LinearColor(int index)
		{
			MeshPoint p = this.vertices[index];
			return SrgbLut.FromSrgba8(p.R, p.G, p.B);
		}

		private PosColorVertex Primitive(Affine transform, int index)
		{
			MeshPoint p = this.vertices[index];
			double x = p.X;
			double y = p.Y;
			transform.Transform(ref x, ref y);
			return new PosColorVertex(new Vector2(x, y), this.LinearColor(index));
		}

		/// <summary>C++ <c>mesh_point</c>: a position, its drift, an sRGB colour and each channel's direction (1 up, 0 down).</summary>
		private struct MeshPoint
		{
			public double X;
			public double Y;
			public double Dx;
			public double Dy;
			public int R;
			public int G;
			public int B;
			public int Dr;
			public int Dg;
			public int Db;
		}

		/// <summary>C++ <c>styles_gouraud</c>: one prepared span_gouraud_rgba per triangle, the style being its index.</summary>
		private sealed class GouraudStyles : IStyleHandler
		{
			private readonly List<span_gouraud_rgba> spans = new List<span_gouraud_rgba>();

			/// <summary>The triangles in style order, prepared.</summary>
			public IReadOnlyList<span_gouraud_rgba> Spans => this.spans;

			public GouraudStyles(GouraudMeshDemo mesh)
			{
				foreach (var t in mesh.triangles)
				{
					MeshPoint p1 = mesh.vertices[t.P1];
					MeshPoint p2 = mesh.vertices[t.P2];
					MeshPoint p3 = mesh.vertices[t.P3];
					var gouraud = new span_gouraud_rgba(mesh.LinearColor(t.P1), mesh.LinearColor(t.P2), mesh.LinearColor(t.P3), p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y);
					gouraud.prepare();
					this.spans.Add(gouraud);
				}
			}

			public Color color(int style) => new Color(0, 0, 0, 0);

			public void GenerateSpan(Color[] span, int spanIndex, int x, int y, int len, int style)
			{
				this.spans[style].generate(span, spanIndex, x, y, len);
			}

			public bool IsSolid(int style) => false;
		}
	}
}
