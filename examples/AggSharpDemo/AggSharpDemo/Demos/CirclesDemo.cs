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
using System.Globalization;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's circles.cpp, a scatter plot prototype: 10000 small circles around a ring, colored by their
	/// depth z. The scale picks the z range shown in full; outside it circles fade, faster the higher the
	/// selectivity. Left-click scatters new points, right-click starts or stops them jittering.
	/// </summary>
	/// <remarks>
	/// circles.cpp scatters with rand(), whose sequence depends on the C library. The port and the C++
	/// reference renderer (demo_circles.cpp) both use MSVC's rand() generator seeded 1 in its place, so they
	/// scatter the same points.
	/// </remarks>
	public class CirclesDemo : AggDemo
	{
		private const int DemoWidth = 400;

		private const int DemoHeight = 400;

		private const int DefaultNumPoints = 10000;

		private static readonly bspline SplineR = new bspline(6, new[] { 0.000000, 0.200000, 0.400000, 0.910484, 0.957258, 1.000000 }, new[] { 1.000000, 0.800000, 0.600000, 0.066667, 0.169697, 0.600000 });

		private static readonly bspline SplineG = new bspline(6, new[] { 0.000000, 0.292244, 0.485655, 0.564859, 0.795607, 1.000000 }, new[] { 0.000000, 0.607260, 0.964065, 0.892558, 0.435571, 0.000000 });

		private static readonly bspline SplineB = new bspline(6, new[] { 0.000000, 0.055045, 0.143034, 0.433082, 0.764859, 1.000000 }, new[] { 0.385480, 0.128493, 0.021416, 0.271507, 0.713974, 1.000000 });

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		private readonly ScatterPoint[] points;

		private readonly MsvcRandom random = new MsvcRandom();

		public CirclesDemo()
			: this(DefaultNumPoints)
		{
		}

		/// <summary>A scatter of <paramref name="numPoints"/> points, as C++'s command-line argument sets.</summary>
		public CirclesDemo(int numPoints)
		{
			this.points = new ScatterPoint[numPoints];

			// circles.cpp runs with flip_y = true and gives its ctrls !flip_y.
			this.ScaleZ = new ScaleCtrl(5, 5, DemoWidth - 5, 12, false);
			this.SelectivitySlider = new SliderCtrl(5, 20, DemoWidth - 5, 27, false) { Label = "Selectivity" };
			this.SizeSlider = new SliderCtrl(5, 35, DemoWidth - 5, 42, false) { Label = "Size" };

			this.ctrls.Add(this.ScaleZ);
			this.ctrls.Add(this.SelectivitySlider);
			this.ctrls.Add(this.SizeSlider);
			this.ctrls.Changed += (s, e) => this.Invalidate();

			this.Generate();
		}

		/// <summary>C++ <c>m_scale_ctrl_z</c>: the z range drawn at full opacity.</summary>
		public ScaleCtrl ScaleZ { get; }

		/// <summary>C++ <c>m_slider_ctrl_sel</c>: how fast circles outside the range fade, and how far they jitter.</summary>
		public SliderCtrl SelectivitySlider { get; }

		/// <summary>C++ <c>m_slider_ctrl_size</c>: circle radius, 5 pixels per unit.</summary>
		public SliderCtrl SizeSlider { get; }

		public override string Name => "circles";

		public override string Category => "Shapes";

		public override string Description => "A scatter plot of 10000 circles. Narrow the scale to fade the rest; left-click for new points, right-click to animate.";

		public override int Width => DemoWidth;

		public override int Height => DemoHeight;

		public override void Draw(Graphics2D graphics)
		{
			graphics.FillRectangle(0, 0, this.Width, this.Height, Color.White);

			// C++ runs every circle through trans_affine_resizing(), which is identity at the initial size.
			double radius = this.SizeSlider.Value * 5.0;
			double selectivity = this.SelectivitySlider.Value;
			int numDrawn = 0;
			foreach (ScatterPoint point in this.points)
			{
				double z = point.Z;
				double alpha = 1.0;
				if (z < this.ScaleZ.Value1)
				{
					alpha = 1.0 - ((this.ScaleZ.Value1 - z) * selectivity * 100.0);
				}

				if (z > this.ScaleZ.Value2)
				{
					alpha = 1.0 - ((z - this.ScaleZ.Value2) * selectivity * 100.0);
				}

				if (alpha > 1.0)
				{
					alpha = 1.0;
				}

				if (alpha < 0.0)
				{
					alpha = 0.0;
				}

				if (alpha > 0.0)
				{
					graphics.Render(new Ellipse(point.X, point.Y, radius, radius, 8), Rgba8.FromRgba(point.R, point.G, point.B, alpha));
					numDrawn++;
				}
			}

			this.ctrls.Render(graphics);

			// C++ gsv_text_outline: the text stroked 1 wide with round joins and caps.
			var outline = new VertexStorage();
#pragma warning disable CS0618 // gsv_text is obsolete for UI text; circles.cpp draws its count with exactly this font.
			var text = new gsv_text();
#pragma warning restore CS0618
			text.size(15.0, 0.0);
			text.text(numDrawn.ToString("D8", CultureInfo.InvariantCulture));
			text.start_point(10.0, this.Height - 20.0);
			foreach (VertexData vertex in text.Vertices())
			{
				if (vertex.IsStop)
				{
					break;
				}

				outline.Add(vertex.Position.X, vertex.Position.Y, vertex.Command);
			}

			graphics.Render(new Stroke(outline, 1.0) { LineJoin = LineJoin.Round, LineCap = LineCap.Round }, Color.Black);
		}

		/// <summary>C++ <c>on_idle</c>: every point jitters by up to the selectivity, and its z by a hundredth of it.</summary>
		public override void OnIdle()
		{
			double selectivity = this.SelectivitySlider.Value;
			for (int i = 0; i < this.points.Length; i++)
			{
				this.points[i].X += this.RandomDouble(0, selectivity) - (selectivity * 0.5);
				this.points[i].Y += this.RandomDouble(0, selectivity) - (selectivity * 0.5);
				this.points[i].Z += this.RandomDouble(0, selectivity * 0.01) - (selectivity * 0.005);
				if (this.points[i].Z < 0.0)
				{
					this.points[i].Z = 0.0;
				}

				if (this.points[i].Z > 1.0)
				{
					this.points[i].Z = 1.0;
				}
			}

			this.Invalidate();
		}

		public override void OnMouseDown(int x, int y, AggInputFlags button, AggInputFlags flags)
		{
			if (this.ctrls.OnMouseDown(x, y, button))
			{
				return;
			}

			if (button.HasFlag(AggInputFlags.MouseLeft))
			{
				this.Generate();
				this.Invalidate();
			}

			if (button.HasFlag(AggInputFlags.MouseRight))
			{
				this.WaitMode = !this.WaitMode;
			}
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

		// C++ generate(): each point on a ring by its z, scattered around it by up to a seventh of the width.
		private void Generate()
		{
			double rx = DemoWidth / 3.5;
			double ry = DemoHeight / 3.5;
			for (int i = 0; i < this.points.Length; i++)
			{
				double z = this.RandomDouble(0.0, 1.0);
				double x = Math.Cos(z * 2.0 * Math.PI) * rx;
				double y = Math.Sin(z * 2.0 * Math.PI) * ry;

				double dist = this.RandomDouble(0.0, rx / 2.0);
				double angle = this.RandomDouble(0.0, Math.PI * 2.0);

				this.points[i] = new ScatterPoint
				{
					X = (DemoWidth / 2.0) + x + (Math.Cos(angle) * dist),
					Y = (DemoHeight / 2.0) + y + (Math.Sin(angle) * dist),
					Z = z,
					R = SplineR.get(z) * 0.8,
					G = SplineG.get(z) * 0.8,
					B = SplineB.get(z) * 0.8,
				};
			}
		}

		// C++ random_dbl.
		private double RandomDouble(double start, double end)
		{
			uint r = (uint)this.random.Next() & 0x7FFF;
			return (r * (end - start) / 32768.0) + start;
		}

		private struct ScatterPoint
		{
			public double X;

			public double Y;

			public double Z;

			public double R;

			public double G;

			public double B;
		}

		/// <summary>MSVC's rand(), seeded 1 as rand() starts: stands in for circles.cpp's rand() so the port
		/// and the C++ reference renderer draw the same scatter on every platform.</summary>
		private sealed class MsvcRandom
		{
			private uint holdrand = 1;

			public int Next()
			{
				this.holdrand = unchecked((this.holdrand * 214013u) + 2531011u);
				return (int)((this.holdrand >> 16) & 0x7fff);
			}
		}
	}
}
