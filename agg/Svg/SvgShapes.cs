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
using MatterHackers.Agg.SvgTools;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// The outline of each basic shape - rect (with rx/ry), circle, ellipse, line, polyline, polygon and path - in
	/// the element's user space. Curved corners, ellipses and arcs are usvg's cubic Béziers (see <see cref="SvgArc"/>), which the renderer flattens at
	/// the output resolution.
	/// </summary>
	public static class SvgShapes
	{
		/// <summary>
		/// The outline of <paramref name="element"/>, or null when it is not a shape or is a shape that draws
		/// nothing (a zero-size rect, a circle without a radius). Percentages are of the viewport,
		/// <paramref name="viewportWidth"/> by <paramref name="viewportHeight"/>; em and ex are of the element's <paramref name="fontSize"/>.
		/// </summary>
		public static VertexStorage ToPath(SvgElement element, double viewportWidth, double viewportHeight, double fontSize = SvgLength.DefaultFontSize)
		{
			double diagonal = Math.Sqrt((viewportWidth * viewportWidth + viewportHeight * viewportHeight) / 2);
			double X(string name) => SvgLength.Parse(element[name], 0, viewportWidth, fontSize);
			double Y(string name) => SvgLength.Parse(element[name], 0, viewportHeight, fontSize);
			switch (element.Name)
			{
				case "rect":
					return Rect(X("x"), Y("y"), X("width"), Y("height"), element["rx"] == null ? -1 : X("rx"), element["ry"] == null ? -1 : Y("ry"));
				case "circle":
					double r = SvgLength.Parse(element["r"], 0, diagonal, fontSize);
					return Ellipse(X("cx"), Y("cy"), r, r);
				case "ellipse":
					// rx/ry "auto" (or one of them missing) takes the other, as SVG 2 says.
					double rx = element["rx"] == null || element["rx"] == "auto" ? -1 : X("rx");
					double ry = element["ry"] == null || element["ry"] == "auto" ? -1 : Y("ry");
					return Ellipse(X("cx"), Y("cy"), rx < 0 ? ry : rx, ry < 0 ? rx : ry);
				case "line":
					var line = new VertexStorage();
					line.MoveTo(X("x1"), Y("y1"));
					line.LineTo(X("x2"), Y("y2"));
					return line;
				case "polyline":
				case "polygon":
					return Poly(SvgLength.ParseList(element["points"]), element.Name == "polygon");
				case "path":
					return Path(element["d"]);
				default:
					return null;
			}
		}

		/// <summary>A rect, its corners rounded by rx/ry (-1 for not given: then each takes the other's value).</summary>
		public static VertexStorage Rect(double x, double y, double width, double height, double rx, double ry)
		{
			if (width <= 0 || height <= 0)
			{
				return null;
			}

			if (rx < 0)
			{
				rx = ry;
			}

			if (ry < 0)
			{
				ry = rx;
			}

			rx = Math.Max(0, Math.Min(rx, width / 2));
			ry = Math.Max(0, Math.Min(ry, height / 2));
			var path = new VertexStorage();
			if (rx == 0 || ry == 0)
			{
				path.MoveTo(x, y);
				path.LineTo(x + width, y);
				path.LineTo(x + width, y + height);
				path.LineTo(x, y + height);
				path.ClosePolygon();
				return path;
			}

			// Each corner a quarter of the ellipse centred rx, ry in from it, as usvg's rect path draws it.
			double right = x + width, bottom = y + height;
			path.MoveTo(x + rx, y);
			path.LineTo(right - rx, y);
			SvgArc.AppendCubics(path, right - rx, y + ry, rx, ry, 0, -Math.PI / 2, Math.PI / 2);
			path.LineTo(right, bottom - ry);
			SvgArc.AppendCubics(path, right - rx, bottom - ry, rx, ry, 0, 0, Math.PI / 2);
			path.LineTo(x + rx, bottom);
			SvgArc.AppendCubics(path, x + rx, bottom - ry, rx, ry, 0, Math.PI / 2, Math.PI / 2);
			path.LineTo(x, y + ry);
			SvgArc.AppendCubics(path, x + rx, y + ry, rx, ry, 0, Math.PI, Math.PI / 2);
			path.ClosePolygon();
			return path;
		}

		/// <summary>An ellipse as four quarter arcs of cubics (see <see cref="SvgArc"/>), starting at its rightmost point as SVG's does; null without a radius.</summary>
		public static VertexStorage Ellipse(double cx, double cy, double rx, double ry)
		{
			if (rx <= 0 || ry <= 0)
			{
				return null;
			}

			// Four quarter arcs, each cut as kurbo cuts it (usvg's ellipse_to_path).
			var path = new VertexStorage();
			path.MoveTo(cx + rx, cy);
			for (int quarter = 0; quarter < 4; quarter++)
			{
				SvgArc.AppendCubics(path, cx, cy, rx, ry, 0, quarter * Math.PI / 2, Math.PI / 2);
			}

			path.ClosePolygon();
			return path;
		}

		/// <summary>
		/// points="x y x y ...": a polyline, or a closed polygon. An odd trailing coordinate is dropped, and fewer
		/// than two points draw nothing.
		/// </summary>
		public static VertexStorage Poly(List<double> points, bool close)
		{
			if (points.Count < 4)
			{
				return null;
			}

			var path = new VertexStorage();
			path.MoveTo(points[0], points[1]);
			for (int i = 2; i + 1 < points.Count; i += 2)
			{
				path.LineTo(points[i], points[i + 1]);
			}

			if (close)
			{
				path.ClosePolygon();
			}

			return path;
		}

		/// <summary>
		/// path d="...", through the path-data parser <see cref="SvgParser"/> has always used. Path data that goes
		/// wrong part way draws up to the error, as SVG says.
		/// </summary>
		public static VertexStorage Path(string d)
		{
			if (string.IsNullOrWhiteSpace(d))
			{
				return null;
			}

			var parsed = new VertexStorage();
			try
			{
				parsed.ParseSvgDString(d, (path, start, radii, angle, size, sweep, end) => SvgArc.AppendSvgArc(path, start, radii, angle, size != 0, sweep != 0, end));
			}
			catch (NotImplementedException)
			{
				// An unknown command letter: keep what was parsed before it.
			}

			return parsed;
		}
	}
}
