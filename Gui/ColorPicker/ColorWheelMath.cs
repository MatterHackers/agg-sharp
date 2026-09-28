/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
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
using System.Globalization;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Geometry of <see cref="ColorWheelPicker"/>: the hue ring and the saturation/value triangle inside it
	/// (agg-gui's color_wheel_picker/hsv_math.rs). Y is up and hue increases counter-clockwise from 3 o'clock,
	/// so hue 0 is to the right and hue 90 straight up.
	/// </summary>
	/// <remarks>
	/// The triangle's vertices are the pure hue (s = 1, v = 1), white (s = 0, v = 1) and black (v = 0), the pure
	/// hue pointing at its own hue on the ring. A point's barycentric weights (pure, white, black) give
	/// v = pure + white and s = pure / v.
	/// </remarks>
	public static class ColorWheelMath
	{
		/// <summary>The hue in degrees [0, 360) of a point <paramref name="offset"/> from the wheel's centre.</summary>
		public static double HueFromOffset(Vector2 offset)
		{
			return HsvColor.NormalizeHue(Math.Atan2(offset.Y, offset.X) * 180 / Math.PI);
		}

		/// <summary>Whether <paramref name="offset"/> from the centre lies on the ring, edges included.</summary>
		public static bool InRing(Vector2 offset, double innerRadius, double outerRadius)
		{
			var lengthSquared = offset.LengthSquared;
			return lengthSquared >= innerRadius * innerRadius && lengthSquared <= outerRadius * outerRadius;
		}

		/// <summary>The triangle inscribed in a circle of <paramref name="radius"/>, turned to <paramref name="hue"/>.</summary>
		public static void TriangleVertices(Vector2 center, double radius, double hue, out Vector2 pure, out Vector2 white, out Vector2 black)
		{
			var angle = hue * Math.PI / 180;
			Vector2 Vertex(double turn) => center + new Vector2(Math.Cos(angle + turn), Math.Sin(angle + turn)) * radius;
			pure = Vertex(0);
			white = Vertex(2 * Math.PI / 3);
			black = Vertex(4 * Math.PI / 3);
		}

		/// <summary>Whether <paramref name="point"/> is inside the triangle, edges included.</summary>
		public static bool InTriangle(Vector2 point, Vector2 center, double radius, double hue)
		{
			var (pure, white, black) = Weights(point, center, radius, hue);
			const double Tolerance = -1e-9;
			return pure >= Tolerance && white >= Tolerance && black >= Tolerance;
		}

		/// <summary>
		/// The saturation and value at <paramref name="point"/>. A point outside the triangle clamps to it, so a
		/// drag that strays off an edge keeps following along that edge.
		/// </summary>
		public static (double Saturation, double Value) SaturationValueAt(Vector2 point, Vector2 center, double radius, double hue)
		{
			var (pure, white, black) = Weights(point, center, radius, hue);
			pure = Math.Clamp(pure, 0, 1);
			white = Math.Clamp(white, 0, 1);
			black = Math.Clamp(black, 0, 1);
			var sum = pure + white + black;
			if (sum > 0)
			{
				pure /= sum;
				white /= sum;
			}

			var value = Math.Clamp(pure + white, 0, 1);
			var saturation = value > 1e-12 ? Math.Clamp(pure / value, 0, 1) : 0;
			return (saturation, value);
		}

		/// <summary>The point inside the triangle where <paramref name="saturation"/> and <paramref name="value"/> sit.</summary>
		public static Vector2 PointAt(double saturation, double value, Vector2 center, double radius, double hue)
		{
			TriangleVertices(center, radius, hue, out var pure, out var white, out var black);
			var pureWeight = saturation * value;
			var whiteWeight = (1 - saturation) * value;
			return pure * pureWeight + white * whiteWeight + black * (1 - pureWeight - whiteWeight);
		}

		/// <summary>
		/// Parses <c>#RGB</c>, <c>#RGBA</c>, <c>#RRGGBB</c> or <c>#RRGGBBAA</c> (the <c>#</c> and surrounding spaces
		/// optional). False for anything else, so a half-typed hex leaves the colour as it was.
		/// </summary>
		public static bool TryParseHex(string text, out Color color)
		{
			color = default;
			var hex = text?.Trim() ?? "";
			if (hex.StartsWith("#"))
			{
				hex = hex.Substring(1);
			}

			if (hex.Length == 3 || hex.Length == 4)
			{
				// Each short digit doubles: F becomes FF.
				var expanded = "";
				foreach (var digit in hex)
				{
					expanded += new string(digit, 2);
				}

				hex = expanded;
			}

			if ((hex.Length != 6 && hex.Length != 8)
				|| !uint.TryParse(hex, NumberStyles.AllowHexSpecifier, CultureInfo.InvariantCulture, out var bits))
			{
				return false;
			}

			if (hex.Length == 6)
			{
				bits = (bits << 8) | 0xFF;
			}

			color = new Color((int)(bits >> 24), (int)((bits >> 16) & 0xFF), (int)((bits >> 8) & 0xFF), (int)(bits & 0xFF));
			return true;
		}

		/// <summary>The barycentric weights of <paramref name="point"/> against the pure, white and black vertices.</summary>
		private static (double Pure, double White, double Black) Weights(Vector2 point, Vector2 center, double radius, double hue)
		{
			TriangleVertices(center, radius, hue, out var a, out var b, out var c);
			var v0 = b - a;
			var v1 = c - a;
			var v2 = point - a;
			var denominator = v0.X * v1.Y - v1.X * v0.Y;
			if (Math.Abs(denominator) < 1e-12)
			{
				return (0, 0, 0);
			}

			var white = (v2.X * v1.Y - v1.X * v2.Y) / denominator;
			var black = (v0.X * v2.Y - v2.X * v0.Y) / denominator;
			return (1 - white - black, white, black);
		}
	}
}
