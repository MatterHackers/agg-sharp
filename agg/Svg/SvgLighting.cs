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
using MatterHackers.Agg.Image;

namespace MatterHackers.Agg.Svg
{
	/// <summary>A lighting primitive's light, in the region's pixel space: x right, y down from the region's top-left.</summary>
	internal sealed class SvgLight
	{
		/// <summary>distant, point or spot.</summary>
		public string Kind { get; set; }

		public double Azimuth { get; set; }

		public double Elevation { get; set; }

		public double X { get; set; }

		public double Y { get; set; }

		public double Z { get; set; }

		public double PointsAtX { get; set; }

		public double PointsAtY { get; set; }

		public double PointsAtZ { get; set; }

		public double SpecularExponent { get; set; } = 1;

		/// <summary>The spot's half-angle in degrees; null for no cone.</summary>
		public double? LimitingConeAngle { get; set; }
	}

	/// <summary>
	/// feDiffuseLighting and feSpecularLighting as the SVG spec defines them: the input's alpha is a height map
	/// (times surfaceScale) whose normals come from the spec's Sobel kernels - narrowed at the region's edges - lit
	/// by one light. The spec works y down, so the region is read top row first.
	/// </summary>
	internal static class SvgLighting
	{
		/// <summary>
		/// The lit region: diffuse is kd * N.L * colour, opaque; specular is ks * (N.H)^exponent * colour with alpha
		/// the largest channel. <paramref name="color"/> is straight RGB 0..1 in the primitive's colour space.
		/// </summary>
		public static byte[] Light(byte[] source, int width, int height, SvgPixelRect region, SvgLight light, bool specular, double surfaceScale, double constant, double exponent, (double R, double G, double B) color)
		{
			var result = new byte[width * height * 4];
			int w = region.Right - region.Left;
			int h = region.Top - region.Bottom;
			if (w <= 0 || h <= 0)
			{
				return result;
			}

			// Heights, y down: row 0 is the region's top.
			var heights = new double[w, h];
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					heights[x, y] = source[((region.Top - 1 - y) * width + region.Left + x) * 4 + ImageBuffer.OrderA] / 255.0;
				}
			}

			double azimuth = light.Azimuth * Math.PI / 180, elevation = light.Elevation * Math.PI / 180;
			(double X, double Y, double Z) distant = (Math.Cos(azimuth) * Math.Cos(elevation), Math.Sin(azimuth) * Math.Cos(elevation), Math.Sin(elevation));
			(double X, double Y, double Z) spotDirection = Normalize((light.PointsAtX - light.X, light.PointsAtY - light.Y, light.PointsAtZ - light.Z));
			double coneCos = light.LimitingConeAngle is double angle ? Math.Cos(Math.Abs(angle) * Math.PI / 180) : -1;
			for (int y = 0; y < h; y++)
			{
				for (int x = 0; x < w; x++)
				{
					(double nx, double ny) = Normal(heights, x, y, w, h, surfaceScale);
					(double X, double Y, double Z) normal = Normalize((nx, ny, 1));
					double z = surfaceScale * heights[x, y];
					(double X, double Y, double Z) toLight = light.Kind == "distant" ? distant : Normalize((light.X - x, light.Y - y, light.Z - z));

					// A spot's colour falls off as (-L.S)^exponent inside its cone and is black outside.
					double intensity = 1;
					if (light.Kind == "spot")
					{
						double minusLDotS = -Dot(toLight, spotDirection);
						intensity = minusLDotS <= 0 || minusLDotS < coneCos ? 0 : Math.Pow(minusLDotS, light.SpecularExponent);
					}

					double factor;
					if (specular)
					{
						(double X, double Y, double Z) half = Normalize((toLight.X, toLight.Y, toLight.Z + 1));
						factor = constant * Math.Pow(Math.Max(0, Dot(normal, half)), exponent);
					}
					else
					{
						factor = constant * Math.Max(0, Dot(normal, toLight));
					}

					double r = Clamp01(factor * intensity * color.R);
					double g = Clamp01(factor * intensity * color.G);
					double b = Clamp01(factor * intensity * color.B);
					double a = specular ? Math.Max(r, Math.Max(g, b)) : 1;
					int i = ((region.Top - 1 - y) * width + region.Left + x) * 4;

					// Diffuse is straight colour on an opaque alpha; specular's channels never exceed its alpha, so
					// they are stored as they are, premultiplied already, as resvg does.
					result[i + ImageBuffer.OrderR] = (byte)Math.Round(r * 255);
					result[i + ImageBuffer.OrderG] = (byte)Math.Round(g * 255);
					result[i + ImageBuffer.OrderB] = (byte)Math.Round(b * 255);
					result[i + ImageBuffer.OrderA] = (byte)Math.Round(a * 255);
				}
			}

			return result;
		}

		/// <summary>
		/// The spec's surface normal (x, y parts): the Sobel sums over the neighbours that exist, each scaled by
		/// 2 / (row weights * column span) - which gives the spec's 1/4, 1/3, 1/2 and 2/3 at the interior, edges and corners.
		/// </summary>
		private static (double X, double Y) Normal(double[,] heights, int x, int y, int w, int h, double surfaceScale)
		{
			int left = Math.Max(0, x - 1), right = Math.Min(w - 1, x + 1);
			int top = Math.Max(0, y - 1), bottom = Math.Min(h - 1, y + 1);
			double sumX = 0, weightX = 0, sumY = 0, weightY = 0;
			for (int row = top; row <= bottom; row++)
			{
				double weight = row == y ? 2 : 1;
				sumX += weight * (heights[right, row] - heights[left, row]);
				weightX += weight;
			}

			for (int column = left; column <= right; column++)
			{
				double weight = column == x ? 2 : 1;
				sumY += weight * (heights[column, bottom] - heights[column, top]);
				weightY += weight;
			}

			double nx = right > left ? -surfaceScale * 2 / (weightX * (right - left)) * sumX : 0;
			double ny = bottom > top ? -surfaceScale * 2 / (weightY * (bottom - top)) * sumY : 0;
			return (nx, ny);
		}

		private static (double X, double Y, double Z) Normalize((double X, double Y, double Z) v)
		{
			double length = Math.Sqrt(v.X * v.X + v.Y * v.Y + v.Z * v.Z);
			return length > 0 ? (v.X / length, v.Y / length, v.Z / length) : (0, 0, 0);
		}

		private static double Dot((double X, double Y, double Z) a, (double X, double Y, double Z) b) => a.X * b.X + a.Y * b.Y + a.Z * b.Z;

		private static double Clamp01(double value) => Math.Max(0, Math.Min(1, value));
	}
}
