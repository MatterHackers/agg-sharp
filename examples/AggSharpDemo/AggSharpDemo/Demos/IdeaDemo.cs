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
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;

namespace MatterHackers.AggSharpDemo.Demos
{
	/// <summary>
	/// C++ AGG's idea.cpp: a light bulb and a figure, filled and outlined, turning when Rotate is checked.
	/// Even-Odd switches the fill rule, Draft renders aliased, Roundoff snaps every vertex to a whole pixel.
	/// </summary>
	/// <remarks>
	/// idea.cpp runs with flip_y = false (y down, row 0 at the top), so the port <see cref="DrawsYDown"/>:
	/// it draws and takes the mouse exactly as C++ does, and the view turns the frame the right way up.
	/// </remarks>
	public class IdeaDemo : AggDemo
	{
		private const int DemoWidth = 250;

		private const int DemoHeight = 280;

		private static readonly double[] PolyBulb =
		{
			-6, -67, -6, -71, -7, -74, -8, -76, -10, -79,
			-10, -82, -9, -84, -6, -86, -4, -87, -2, -86,
			-1, -86, 1, -84, 2, -82, 2, -79, 0, -77,
			-2, -73, -2, -71, -2, -69, -3, -67, -4, -65,
		};

		private static readonly double[] PolyBeam1 = { -14, -84, -22, -85, -23, -87, -22, -88, -21, -88 };

		private static readonly double[] PolyBeam2 = { -10, -92, -14, -96, -14, -98, -12, -99, -11, -97 };

		private static readonly double[] PolyBeam3 = { -1, -92, -2, -98, 0, -100, 2, -100, 1, -98 };

		private static readonly double[] PolyBeam4 = { 5, -89, 11, -94, 13, -93, 13, -92, 12, -91 };

		private static readonly double[] PolyFig1 =
		{
			1, -48, -3, -54, -7, -58, -12, -58, -17, -55, -20, -52, -21, -47,
			-20, -40, -17, -33, -11, -28, -6, -26, -2, -25, 2, -26, 4, -28, 5,
			-33, 5, -39, 3, -44, 12, -48, 12, -50, 12, -51, 3, -46,
		};

		private static readonly double[] PolyFig2 =
		{
			11, -27, 6, -23, 4, -22, 3, -19, 5,
			-16, 6, -15, 11, -17, 19, -23, 25, -30, 32, -38, 32, -41, 32, -50, 30, -64, 32, -72,
			32, -75, 31, -77, 28, -78, 26, -80, 28, -87, 27, -89, 25, -88, 24, -79, 24, -76, 23,
			-75, 20, -76, 17, -76, 17, -74, 19, -73, 22, -73, 24, -71, 26, -69, 27, -64, 28, -55,
			28, -47, 28, -40, 26, -38, 20, -33, 14, -30,
		};

		private static readonly double[] PolyFig3 =
		{
			-6, -20, -9, -21, -15, -21, -20, -17,
			-28, -8, -32, -1, -32, 1, -30, 6, -26, 8, -20, 10, -16, 12, -14, 14, -15, 16, -18, 20,
			-22, 20, -25, 19, -27, 20, -26, 22, -23, 23, -18, 23, -14, 22, -11, 20, -10, 17, -9, 14,
			-11, 11, -16, 9, -22, 8, -26, 5, -28, 2, -27, -2, -23, -8, -19, -11, -12, -14, -6, -15,
			-6, -18,
		};

		private static readonly double[] PolyFig4 =
		{
			11, -6, 8, -16, 5, -21, -1, -23, -7,
			-22, -10, -17, -9, -10, -8, 0, -8, 10, -10, 18, -11, 22, -10, 26, -7, 28, -3, 30, 0, 31,
			5, 31, 10, 27, 14, 18, 14, 11, 11, 2,
		};

		private static readonly double[] PolyFig5 =
		{
			0, 22, -5, 21, -8, 22, -9, 26, -8, 49,
			-8, 54, -10, 64, -10, 75, -9, 81, -10, 84, -16, 89, -18, 95, -18, 97, -13, 100, -12, 99,
			-12, 95, -10, 90, -8, 87, -6, 86, -4, 83, -3, 82, -5, 80, -6, 79, -7, 74, -6, 63, -3, 52,
			0, 42, 1, 31,
		};

		private static readonly double[] PolyFig6 =
		{
			12, 31, 12, 24, 8, 21, 3, 21, 2, 24, 3,
			30, 5, 40, 8, 47, 10, 56, 11, 64, 11, 71, 10, 76, 8, 77, 8, 79, 10, 81, 13, 82, 17, 82, 26,
			84, 28, 87, 32, 86, 33, 81, 32, 80, 25, 79, 17, 79, 14, 79, 13, 76, 14, 72, 14, 64, 13, 55,
			12, 44, 12, 34,
		};

		// C++ g_attr: the bulb, the beams and the figure, each filled then (if wide enough) stroked. Per
		// instance, not static: a VertexStorage keeps its read position, so two demos drawing at once (tests
		// run in parallel) must not share one.
		private readonly PathAttributes[] attributes =
		{
			new PathAttributes(Polygons(PolyBulb), SrgbLut.FromSrgba8(255, 255, 0), SrgbLut.FromSrgba8(0, 0, 0), 1.0),
			new PathAttributes(Polygons(PolyBeam1, PolyBeam2, PolyBeam3, PolyBeam4), SrgbLut.FromSrgba8(255, 255, 200), SrgbLut.FromSrgba8(90, 0, 0), 0.7),
			new PathAttributes(Polygons(PolyFig1, PolyFig2, PolyFig3, PolyFig4, PolyFig5, PolyFig6), SrgbLut.FromSrgba8(0, 0, 0), SrgbLut.FromSrgba8(0, 0, 0), 0.0),
		};

		private readonly AggCtrlContainer ctrls = new AggCtrlContainer();

		public IdeaDemo()
		{
			// idea.cpp runs with flip_y = false and gives its ctrls !flip_y.
			this.RotateBox = this.NewCbox(10, "Rotate");
			this.EvenOddBox = this.NewCbox(60, "Even-Odd");
			this.DraftBox = this.NewCbox(130, "Draft");
			this.RoundoffBox = this.NewCbox(175, "Roundoff");
			this.StepSlider = new SliderCtrl(10, 21, 250 - 10, 27, true)
			{
				Label = "Step={0,4:F3} degree",
			};
			this.StepSlider.Value = 0.01;
			this.ctrls.Add(this.StepSlider);

			this.ctrls.Changed += (s, e) =>
			{
				// C++ on_ctrl_change.
				this.WaitMode = !this.RotateBox.Checked;
				this.Invalidate();
			};
		}

		/// <summary>C++ <c>m_rotate</c>: turns the picture by <see cref="StepSlider"/> every frame.</summary>
		public CboxCtrl RotateBox { get; }

		/// <summary>C++ <c>m_even_odd</c>: even-odd fill rule in place of non-zero.</summary>
		public CboxCtrl EvenOddBox { get; }

		/// <summary>C++ <c>m_draft</c>: aliased, through a 0.4 threshold gamma.</summary>
		public CboxCtrl DraftBox { get; }

		/// <summary>C++ <c>m_roundoff</c>: every transformed vertex snapped to a whole pixel.</summary>
		public CboxCtrl RoundoffBox { get; }

		/// <summary>C++ <c>m_angle_delta</c>: degrees turned per frame.</summary>
		public SliderCtrl StepSlider { get; }

		/// <summary>C++ <c>g_angle</c>: the current turn, in degrees.</summary>
		public double Angle { get; set; }

		public override string Name => "idea";

		public override string Category => "Shapes";

		public override string Description => "A light bulb idea, filled and outlined. Check Rotate to spin it, and compare Even-Odd, Draft and Roundoff.";

		public override int Width => DemoWidth;

		public override int Height => DemoHeight;

		public override bool DrawsYDown => true;

		public override void Draw(Graphics2D graphics)
		{
			graphics.Clear(Color.White);
			this.ctrls.Render(graphics);

			var mtx = Affine.NewRotation(this.Angle * Math.PI / 180.0);
			mtx *= Affine.NewTranslation(this.Width / 2.0, (this.Height / 2.0) + 10);

			// C++ scales by the window size over the initial size, 1 here since the demo never resizes.
			mtx *= Affine.NewScaling(1.0, 1.0);
			double strokeScale = mtx.GetScale();

			// Only the software rasterizer takes a gamma; the GPU is always anti-aliased. The fill rule goes to the
			// rasterizer in software and to the graphics itself on the GPU.
			ScanlineRasterizer rasterizer = graphics.Rasterizer;
			var gpuFillRule = graphics as IFillRuleGraphics;
			if (this.DraftBox.Checked)
			{
				rasterizer?.gamma(new gamma_threshold(0.4));
			}

			var fillingRule = this.EvenOddBox.Checked ? Util.filling_rule_e.fill_even_odd : Util.filling_rule_e.fill_non_zero;
			rasterizer?.filling_rule(fillingRule);
			if (gpuFillRule != null)
			{
				gpuFillRule.FillingRule = fillingRule;
			}

			try
			{
				foreach (PathAttributes attribute in this.attributes)
				{
					IVertexSource fill = new VertexSourceApplyTransform(attribute.Path, mtx);
					if (this.RoundoffBox.Checked)
					{
						fill = new VertexSourceApplyTransform(fill, new RoundoffTransform());
					}

					// Draft: C++ renders through render_scanlines_bin_solid. Under the threshold gamma every
					// cell's coverage is 0 or 255, so the anti-aliased fill lands the same bytes.
					graphics.Render(fill, attribute.FillColor);

					if (attribute.StrokeWidth > 0.001)
					{
						var stroke = new Stroke(fill, attribute.StrokeWidth * strokeScale);
						graphics.Render(stroke, attribute.StrokeColor);
					}
				}
			}
			finally
			{
				// ScanlineRasterizer cannot report its gamma or rule, so both go back to the defaults every
				// other fill in agg-sharp assumes.
				rasterizer?.gamma(new gamma_none());
				rasterizer?.filling_rule(Util.filling_rule_e.fill_non_zero);
				if (gpuFillRule != null)
				{
					gpuFillRule.FillingRule = Util.filling_rule_e.fill_non_zero;
				}
			}
		}

		public override void OnIdle()
		{
			// C++ on_idle.
			this.Angle += this.StepSlider.Value;
			if (this.Angle > 360.0)
			{
				this.Angle -= 360.0;
			}

			this.Invalidate();
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

		// C++ path_storage::concat_poly(poly, n, true) for each polygon, into one path.
		private static VertexStorage Polygons(params double[][] polygons)
		{
			var path = new VertexStorage();
			foreach (double[] polygon in polygons)
			{
				path.MoveTo(polygon[0], polygon[1]);
				for (int i = 2; i < polygon.Length; i += 2)
				{
					path.LineTo(polygon[i], polygon[i + 1]);
				}

				path.ClosePolygon();
			}

			return path;
		}

		private CboxCtrl NewCbox(double x, string label)
		{
			var cbox = new CboxCtrl(x, 3, label, true);
			cbox.SetTextSize(7);
			this.ctrls.Add(cbox);
			return cbox;
		}

		/// <summary>C++ <c>path_attributes</c>, with the path itself in place of its index.</summary>
		private sealed class PathAttributes
		{
			public PathAttributes(VertexStorage path, Color fillColor, Color strokeColor, double strokeWidth)
			{
				this.Path = path;
				this.FillColor = fillColor;
				this.StrokeColor = strokeColor;
				this.StrokeWidth = strokeWidth;
			}

			public VertexStorage Path { get; }

			public Color FillColor { get; }

			public Color StrokeColor { get; }

			public double StrokeWidth { get; }
		}

		/// <summary>idea.cpp's <c>trans_roundoff</c>: both coordinates rounded to the nearest whole number.</summary>
		private sealed class RoundoffTransform : ITransform
		{
			public void Transform(ref double x, ref double y)
			{
				x = Math.Floor(x + 0.5);
				y = Math.Floor(y + 0.5);
			}
		}
	}
}
