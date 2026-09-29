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
using System.Collections.Generic;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The path a &lt;textPath&gt; lays its glyphs along, measured by arc length as usvg's collect_normals measures it:
	/// every segment is a cubic (a line one with controls at t = 0.33 and 0.66, a quadratic raised), whose length and
	/// inverse come from kurbo to within half a unit (<see cref="SvgTextPathCubic"/>). A move adds no length (subpaths
	/// join end to start), a close adds the line back to its subpath's start, and a distance off either end has no place.
	/// </summary>
	internal sealed class SvgTextPath
	{
		// usvg's arclen accuracy: 0.5 / max(sqrt(sx * sy), 1) of the text's absolute transform. The suite draws at
		// scale 1, and the transform is not known here, so this is the scale-1 value.
		private const double Accuracy = 0.5;

		private readonly List<(SvgTextPathCubic Curve, double Start, double Length)> segments = new();

		private SvgTextPath()
		{
		}

		/// <summary>The path's arc length.</summary>
		public double Length { get; private set; }

		/// <summary>startOffset in user units: a length, or a percentage of <see cref="Length"/>.</summary>
		public double StartOffset { get; private set; }

		/// <summary>
		/// The path <paramref name="textPath"/> follows - its SVG 2 path attribute when that parses to something,
		/// else the path or basic shape its href names, in that element's transform - or null when it has none,
		/// and then the textPath and all it holds draw nothing.
		/// </summary>
		public static SvgTextPath Resolve(SvgElement textPath, SvgDocument document, double viewportWidth, double viewportHeight, double fontSize)
		{
			var measured = new SvgTextPath();
			if (SvgShapes.Path(textPath["path"]) is VertexStorage inline)
			{
				measured.Measure(inline);
			}

			if (measured.segments.Count == 0
				&& document?.GetElementById(textPath["href"]) is SvgElement linked
				&& SvgShapes.ToPath(linked, viewportWidth, viewportHeight, fontSize) is VertexStorage shape)
			{
				measured = new SvgTextPath();
				measured.Measure(new VertexSourceApplyTransform(shape, SvgTransform.Resolve(linked, "transform", viewportWidth, viewportHeight)));
			}

			if (measured.segments.Count == 0)
			{
				return null;
			}

			string offset = textPath["startOffset"]?.Trim();
			measured.StartOffset = offset != null && offset.EndsWith("%")
				? measured.Length * SvgLength.ParseNumber(offset.TrimEnd('%'), 0) / 100
				: SvgLength.Parse(offset, 0, viewportWidth, fontSize);
			return measured;
		}

		/// <summary>
		/// The point <paramref name="distance"/> along the path and the path's direction there, in radians
		/// clockwise from +x; false when the distance is off either end.
		/// </summary>
		public bool TryPlace(double distance, out double x, out double y, out double angle)
		{
			x = y = angle = 0;
			if (distance < 0)
			{
				return false;
			}

			foreach (var s in this.segments)
			{
				if (distance >= s.Start && distance <= s.Start + s.Length)
				{
					double t = Math.Clamp(s.Curve.InverseArcLength(distance - s.Start, Accuracy), 0, 1);
					var position = s.Curve.Evaluate(t);
					var direction = s.Curve.Derivative(t);
					x = position.X;
					y = position.Y;
					angle = Math.Atan2(direction.Y, direction.X);
					return true;
				}
			}

			return false;
		}

		private void Measure(IVertexSource path)
		{
			// tiny-skia keeps path points as f32; rounding them the same way keeps lengths and places in step.
			static Vector2 Point(VertexData vertex) => new Vector2((float)vertex.Position.X, (float)vertex.Position.Y);
			Vector2 start = default, last = default;
			var controls = new List<Vector2>();
			bool any = false;
			void Add(SvgTextPathCubic curve)
			{
				double length = curve.ArcLength(Accuracy);

				// Zero-length pieces have no direction; they would only turn a glyph that lands on them.
				if (length > 0)
				{
					this.segments.Add((curve, this.Length, length));
					this.Length += length;
				}

				last = curve.P3;
			}

			foreach (VertexData vertex in path.Vertices())
			{
				if (vertex.IsMoveTo || (!any && vertex.IsVertex))
				{
					start = last = Point(vertex);
					controls.Clear();
					any = true;
				}
				else if (vertex.Command == FlagsAndCommand.LineTo)
				{
					Add(SvgTextPathCubic.FromLine(last, Point(vertex)));
				}
				else if (vertex.Command == FlagsAndCommand.Curve3)
				{
					// A quadratic's control and end both come as Curve3 vertices.
					if (controls.Count == 0)
					{
						controls.Add(Point(vertex));
					}
					else
					{
						Add(SvgTextPathCubic.FromQuadratic(last, controls[0], Point(vertex)));
						controls.Clear();
					}
				}
				else if (vertex.Command == FlagsAndCommand.Curve4)
				{
					if (controls.Count < 2)
					{
						controls.Add(Point(vertex));
					}
					else
					{
						Add(new SvgTextPathCubic(last, controls[0], controls[1], Point(vertex)));
						controls.Clear();
					}
				}
				else if (vertex.IsClose)
				{
					Add(SvgTextPathCubic.FromLine(last, start));
				}
			}
		}
	}
}
