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

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// The pure math of agg-gui's animated 3D bar grid (demo-wgpu bar_grid_render.rs and bar_grid_math.rs):
	/// a 16 x 8 grid of boxes whose heights follow a travelling sine wave, coloured by grid position and
	/// height and flat-lit per face. Free of GPU types so it is unit tested on its own.
	/// </summary>
	/// <remarks>
	/// agg-gui computes height and colour in its shaders from one instanced draw. Both are constant per bar
	/// (the grid coordinate is per instance, the normal per face), so here they are computed on the CPU per
	/// bar and per face and handed to the GL facade as flat colours - the same picture. Matrices are GL
	/// convention (clip z in -1..1), column-major; the compat layer remaps depth for WebGPU.
	/// </remarks>
	public static class BarGridMath
	{
		public const int Columns = 16;
		public const int Rows = 8;

		/// <summary>Half the footprint of one bar; grid cells are 1 apart, so a 0.1 gap separates bars.</summary>
		public const double BarHalf = 0.45;

		/// <summary>Radians of wave phase per second.</summary>
		public const double WaveSpeed = 1.4;

		public const double WaveFrequency = 0.55;
		public const double MaxHeight = 2.10;
		public const double MinHeight = MaxHeight * 0.4;

		public const double FieldOfViewYDegrees = 35;
		public const double Near = 0.5;
		public const double Far = 100;

		/// <summary>agg-gui's camera eye.</summary>
		public static readonly double[] Eye = { -7.0, 8.5, 11.0 };

		/// <summary>agg-gui's camera target.</summary>
		public static readonly double[] Target = { 0.0, 0.5, 0.0 };

		/// <summary>The light direction, normalized (0.55, 0.85, 0.45).</summary>
		public static readonly double[] LightDirection = Normalize(new[] { 0.55, 0.85, 0.45 });

		/// <summary>The wave phase after <paramref name="elapsedSeconds"/>, wrapped to [0, 2 pi) so it stays
		/// precise however long the demo runs.</summary>
		public static double WavePhase(double elapsedSeconds)
		{
			double phase = (elapsedSeconds * WaveSpeed) % (2 * Math.PI);
			return phase < 0 ? phase + (2 * Math.PI) : phase;
		}

		/// <summary>The wave at a bar, 0 (lowest) to 1 (highest).</summary>
		public static double WaveUnit(int column, int row, double phase)
			=> (Math.Sin((column * WaveFrequency) + (row * WaveFrequency) + phase) * 0.5) + 0.5;

		/// <summary>The bar's height in world units, <see cref="MinHeight"/> to <see cref="MaxHeight"/>.</summary>
		public static double Height(int column, int row, double phase)
			=> Lerp(MinHeight, MaxHeight, WaveUnit(column, row, phase));

		/// <summary>The bar's world-space centre on the ground plane; the grid is centred on the origin.</summary>
		public static (double X, double Z) BarCenter(int column, int row)
			=> (column - ((Columns - 1) * 0.5), row - ((Rows - 1) * 0.5));

		/// <summary>The palette agg-gui picks for a dark or a light theme; light-theme peaks tint toward the
		/// text colour.</summary>
		public static BarPalette PaletteFor(bool dark, ColorF textColor)
		{
			return dark
				? new BarPalette(new ColorF(0.18, 0.55, 0.95), new ColorF(0.92, 0.32, 0.62), new ColorF(1.00, 0.78, 0.30), new ColorF(1, 1, 1))
				: new BarPalette(new ColorF(0.10, 0.42, 0.85), new ColorF(0.78, 0.18, 0.45), new ColorF(0.95, 0.55, 0.10), textColor);
		}

		/// <summary>The bar's unlit colour: left-to-right blend across columns, an accent toward the back rows,
		/// and a tint toward the peak colour as the bar rises.</summary>
		public static ColorF BaseColor(int column, int row, double phase, BarPalette palette)
		{
			double u = column / (double)(Columns - 1);
			double v = row / (double)(Rows - 1);
			double h = WaveUnit(column, row, phase);
			ColorF color = Mix(palette.Left, palette.Right, u);
			color = Mix(color, palette.Accent, v * 0.35);
			return Mix(color, palette.Peak, h * h * 0.25);
		}

		/// <summary>The light factor of a face with <paramref name="normal"/>: 0.45 ambient plus 0.55 diffuse.</summary>
		public static double Lighting(double[] normal)
			=> 0.45 + (0.55 * Math.Max(Dot(normal, LightDirection), 0));

		/// <summary><paramref name="color"/> times <paramref name="light"/>, opaque.</summary>
		public static ColorF Lit(ColorF color, double light)
			=> new ColorF(color.red * light, color.green * light, color.blue * light, 1);

		/// <summary>GL-convention right-handed perspective, column-major.</summary>
		public static double[] Perspective(double fovYRadians, double aspect, double near, double far)
		{
			double f = 1.0 / Math.Tan(fovYRadians * 0.5);
			double nf = 1.0 / (near - far);
			return new[]
			{
				f / aspect, 0, 0, 0,
				0, f, 0, 0,
				0, 0, (far + near) * nf, -1,
				0, 0, 2 * far * near * nf, 0,
			};
		}

		/// <summary>A right-handed view matrix looking from <paramref name="eye"/> at <paramref name="target"/>,
		/// column-major.</summary>
		public static double[] LookAt(double[] eye, double[] target, double[] up)
		{
			double[] f = Normalize(Sub(target, eye));
			double[] s = Normalize(Cross(f, up));
			double[] u = Cross(s, f);
			return new[]
			{
				s[0], u[0], -f[0], 0,
				s[1], u[1], -f[1], 0,
				s[2], u[2], -f[2], 0,
				-Dot(s, eye), -Dot(u, eye), Dot(f, eye), 1,
			};
		}

		/// <summary>The demo camera's projection for a viewport of <paramref name="aspect"/> (width / height).</summary>
		public static double[] Projection(double aspect)
			=> Perspective(FieldOfViewYDegrees * Math.PI / 180, aspect, Near, Far);

		/// <summary>The demo camera's view matrix.</summary>
		public static double[] View() => LookAt(Eye, Target, new[] { 0.0, 1.0, 0.0 });

		/// <summary>Column-major 4x4 product a * b.</summary>
		public static double[] Multiply(double[] a, double[] b)
		{
			var result = new double[16];
			for (int row = 0; row < 4; row++)
			{
				for (int col = 0; col < 4; col++)
				{
					result[(col * 4) + row] = (a[row] * b[col * 4])
						+ (a[4 + row] * b[(col * 4) + 1])
						+ (a[8 + row] * b[(col * 4) + 2])
						+ (a[12 + row] * b[(col * 4) + 3]);
				}
			}

			return result;
		}

		/// <summary>Transforms the point (x, y, z, 1) by column-major <paramref name="m"/>, returning (x, y, z, w).</summary>
		public static double[] Transform(double[] m, double x, double y, double z)
		{
			var result = new double[4];
			for (int row = 0; row < 4; row++)
			{
				result[row] = (m[row] * x) + (m[4 + row] * y) + (m[8 + row] * z) + m[12 + row];
			}

			return result;
		}

		private static double Lerp(double a, double b, double t) => a + ((b - a) * t);

		private static ColorF Mix(ColorF a, ColorF b, double t)
			=> new ColorF(Lerp(a.red, b.red, t), Lerp(a.green, b.green, t), Lerp(a.blue, b.blue, t), 1);

		private static double[] Sub(double[] a, double[] b) => new[] { a[0] - b[0], a[1] - b[1], a[2] - b[2] };

		private static double Dot(double[] a, double[] b) => (a[0] * b[0]) + (a[1] * b[1]) + (a[2] * b[2]);

		private static double[] Cross(double[] a, double[] b)
			=> new[] { (a[1] * b[2]) - (a[2] * b[1]), (a[2] * b[0]) - (a[0] * b[2]), (a[0] * b[1]) - (a[1] * b[0]) };

		private static double[] Normalize(double[] v)
		{
			double length = Math.Max(Math.Sqrt(Dot(v, v)), 1e-9);
			return new[] { v[0] / length, v[1] / length, v[2] / length };
		}
	}

	/// <summary>The four colours the bars blend between.</summary>
	public readonly struct BarPalette
	{
		public BarPalette(ColorF left, ColorF right, ColorF accent, ColorF peak)
		{
			this.Left = left;
			this.Right = right;
			this.Accent = accent;
			this.Peak = peak;
		}

		/// <summary>Column 0's colour.</summary>
		public ColorF Left { get; }

		/// <summary>The last column's colour.</summary>
		public ColorF Right { get; }

		/// <summary>Blended in (up to 35%) toward the back rows.</summary>
		public ColorF Accent { get; }

		/// <summary>Blended in (up to 25%) as a bar peaks.</summary>
		public ColorF Peak { get; }
	}
}
