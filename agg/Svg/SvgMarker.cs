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
using System.Globalization;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// marker-start, marker-mid and marker-end: a &lt;marker&gt;'s content drawn at a shape's vertices, placed,
	/// oriented and clipped as resvg (usvg's marker.rs) does. Its refX/refY sits on the vertex; markerUnits
	/// strokeWidth (the default) scales it by the shape's stroke-width; a viewBox scales its content to
	/// markerWidth by markerHeight (the alignment's translation is not applied, as in resvg); overflow other than
	/// visible or auto clips the content to the viewBox, else to 0..markerWidth by 0..markerHeight.
	/// </summary>
	internal static class SvgMarker
	{
		private enum Kind
		{
			MoveTo,
			LineTo,
			CubicTo,
			Close,
		}

		/// <summary>
		/// Draws <paramref name="style"/>'s markers on <paramref name="path"/> (the shape's outline in the user space
		/// <paramref name="transform"/> takes to pixels) onto <paramref name="target"/>.
		/// </summary>
		public static void Draw(SvgRenderer.Context context, VertexStorage path, SvgStyle style, Affine transform, ImageBuffer target, int useDepth)
		{
			if (style.MarkerStart == null && style.MarkerMid == null && style.MarkerEnd == null)
			{
				return;
			}

			List<Segment> segments = Segments(path);
			if (segments.Count < 2)
			{
				return;
			}

			DrawKind(context, segments, style.MarkerStart, 0, style, transform, target, useDepth);
			DrawKind(context, segments, style.MarkerMid, 1, style, transform, target, useDepth);
			DrawKind(context, segments, style.MarkerEnd, 2, style, transform, target, useDepth);
		}

		/// <summary>Draws one marker property's marker (<paramref name="kind"/> 0 start, 1 mid, 2 end) at its vertices.</summary>
		private static void DrawKind(SvgRenderer.Context context, List<Segment> segments, string reference, int kind, SvgStyle style, Affine transform, ImageBuffer target, int useDepth)
		{
			SvgElement marker = reference == null ? null : context.Document.GetElementById(reference);
			if (marker?.Name != "marker" || context.ActiveMarkers.Contains(marker))
			{
				return;
			}

			double strokeScale = marker["markerUnits"] == "userSpaceOnUse" ? 1 : style.StrokeWidth;
			double refX = SvgLength.Parse(marker["refX"], 0, context.ViewportWidth);
			double refY = SvgLength.Parse(marker["refY"], 0, context.ViewportHeight);
			double width = SvgLength.Parse(marker["markerWidth"], 3, context.ViewportWidth);
			double height = SvgLength.Parse(marker["markerHeight"], 3, context.ViewportHeight);
			if (strokeScale <= 0 || width <= 0 || height <= 0)
			{
				return;
			}

			RectangleDouble? viewBox = SvgViewport.ParseViewBox(marker["viewBox"]);
			double scaleX = strokeScale, scaleY = strokeScale;
			if (viewBox is RectangleDouble box)
			{
				Affine fit = SvgViewport.ViewBoxTransform(box, marker["preserveAspectRatio"], width * strokeScale, height * strokeScale);
				scaleX = fit.sx;
				scaleY = fit.sy;
			}

			string overflow = marker["overflow"];
			RectangleDouble? clip = overflow == null || overflow == "hidden" || overflow == "scroll"
				? viewBox ?? new RectangleDouble(0, 0, width, height)
				: (RectangleDouble?)null;

			void DrawAt(Vector2 point, int index)
			{
				double degrees = Orientation(marker["orient"], index == 0, () => VertexAngle(segments, index));
				Affine contentToPixels = Affine.NewTranslation(-refX, -refY)
					* Affine.NewScaling(scaleX, scaleY)
					* Affine.NewRotation(degrees * Math.PI / 180)
					* Affine.NewTranslation(point.X, point.Y)
					* transform;
				DrawContent(context, marker, contentToPixels, clip, target, useDepth);
			}

			switch (kind)
			{
				case 0:
					if (segments[0].Kind == Kind.MoveTo)
					{
						DrawAt(segments[0].P, 0);
					}

					break;
				case 1:
					for (int i = 1; i < segments.Count - 1; i++)
					{
						if (segments[i].Kind != Kind.Close)
						{
							DrawAt(segments[i].P, i);
						}
					}

					break;
				default:
					int last = segments.Count - 1;
					switch (segments[last].Kind)
					{
						case Kind.LineTo:
						case Kind.CubicTo:
							DrawAt(segments[last].P, last);
							break;
						case Kind.Close:
							DrawAt(SubpathStart(segments, last), last);
							break;
					}

					break;
			}
		}

		/// <summary>The marker's children, drawn in its own inherited style, clipped to <paramref name="clip"/> (content units) if given.</summary>
		private static void DrawContent(SvgRenderer.Context context, SvgElement marker, Affine contentToPixels, RectangleDouble? clip, ImageBuffer target, int useDepth)
		{
			context.ActiveMarkers.Add(marker);
			try
			{
				SvgStyle markerStyle = SvgRenderer.InheritedStyle(context, marker);
				if (!(clip is RectangleDouble c))
				{
					SvgRenderer.DrawChildren(context, marker, markerStyle, contentToPixels, target, useDepth);
					return;
				}

				ImageBuffer layer = SvgClipMask.NewLayer(target);
				SvgRenderer.DrawChildren(context, marker, markerStyle, contentToPixels, layer, useDepth);
				ImageBuffer coverage = SvgClipMask.NewLayer(target);
				coverage.NewGraphics2D().Render(new VertexSourceApplyTransform(SvgShapes.Rect(c.Left, c.Bottom, c.Width, c.Height, 0, 0), contentToPixels), Color.White);
				SvgClipMask.Multiply(layer, coverage, (pixels, i) => pixels[i + ImageBuffer.OrderA]);
				SvgRenderer.CompositeLayer(target, layer, 1);
			}
			finally
			{
				context.ActiveMarkers.Remove(marker);
			}
		}

		/// <summary>
		/// orient: "auto" and "auto-start-reverse" (the start turned half round) follow the path; otherwise an angle
		/// in deg (the default unit), grad, rad or turn; anything else is 0.
		/// </summary>
		internal static double Orientation(string orient, bool isStart, Func<double> vertexAngle)
		{
			string value = orient?.Trim() ?? "";
			if (value == "auto")
			{
				return vertexAngle();
			}

			if (value == "auto-start-reverse")
			{
				return isStart ? (vertexAngle() + 180) % 360 : vertexAngle();
			}

			(string Suffix, double ToDegrees)[] units = { ("deg", 1), ("grad", .9), ("rad", 180 / Math.PI), ("turn", 360) };
			double scale = 1;
			foreach ((string suffix, double toDegrees) in units)
			{
				if (value.EndsWith(suffix, StringComparison.Ordinal))
				{
					value = value.Substring(0, value.Length - suffix.Length);
					scale = toDegrees;
					break;
				}
			}

			return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out double angle) ? angle * scale : 0;
		}

		/// <summary>
		/// The path as tiny-skia builds it, which is what resvg indexes its markers by: quadratics raised to cubics,
		/// a repeated close dropped, a move straight after a move replacing it, and a move to the subpath's start
		/// inserted when a close is followed by a drawing command.
		/// </summary>
		private static List<Segment> Segments(VertexStorage path)
		{
			var segments = new List<Segment>();
			Vector2 current = default, subpathStart = default;
			bool needsMove = true;
			VertexData[] vertices = new List<VertexData>(path.Vertices()).ToArray();
			void Add(Segment segment)
			{
				if (segment.Kind == Kind.MoveTo)
				{
					if (segments.Count > 0 && segments[segments.Count - 1].Kind == Kind.MoveTo)
					{
						segments.RemoveAt(segments.Count - 1);
					}

					subpathStart = segment.P;
					needsMove = false;
				}
				else if (segment.Kind == Kind.Close)
				{
					if (segments.Count == 0 || segments[segments.Count - 1].Kind == Kind.Close)
					{
						return;
					}

					needsMove = true;
					current = subpathStart;
				}
				else if (needsMove)
				{
					segments.Add(new Segment(Kind.MoveTo, current, current, current));
					subpathStart = current;
					needsMove = false;
				}

				segments.Add(segment);
				if (segment.Kind != Kind.Close)
				{
					current = segment.P;
				}
			}

			for (int i = 0; i < vertices.Length; i++)
			{
				VertexData v = vertices[i];
				if (v.IsStop)
				{
					break;
				}

				if (v.IsClose)
				{
					Add(new Segment(Kind.Close, default, default, default));
				}
				else if (v.IsMoveTo)
				{
					Add(new Segment(Kind.MoveTo, v.Position, v.Position, v.Position));
				}
				else if (v.IsLineTo)
				{
					Add(new Segment(Kind.LineTo, v.Position, v.Position, v.Position));
				}
				else if (v.Command == FlagsAndCommand.Curve3 && i + 1 < vertices.Length)
				{
					// A quadratic's control point two thirds of the way from each end: the equivalent cubic.
					Vector2 control = v.Position, end = vertices[++i].Position;
					Add(new Segment(Kind.CubicTo, current + (control - current) * (2.0 / 3), end + (control - end) * (2.0 / 3), end));
				}
				else if (v.Command == FlagsAndCommand.Curve4 && i + 2 < vertices.Length)
				{
					Add(new Segment(Kind.CubicTo, v.Position, vertices[i + 1].Position, vertices[i + 2].Position));
					i += 2;
				}
			}

			return segments;
		}

		/// <summary>
		/// The direction in degrees a marker at segment <paramref name="index"/> faces under orient="auto": along the
		/// path at its ends, and bisecting the incoming and outgoing directions in between (usvg's calc_vertex_angle).
		/// </summary>
		private static double VertexAngle(List<Segment> path, int index)
		{
			if (index == 0)
			{
				Segment first = path[0], second = path[1];
				if (first.Kind != Kind.MoveTo)
				{
					return 0;
				}

				return second.Kind switch
				{
					Kind.LineTo => LineAngle(first.P, second.P),
					Kind.CubicTo => LineAngle(first.P, Same(first.P, second.P1) ? second.P : second.P1),
					_ => 0,
				};
			}

			Segment seg = path[index];
			if (index == path.Count - 1)
			{
				Segment before = path[index - 1];
				switch (seg.Kind)
				{
					case Kind.LineTo:
						return LineAngle(PreviousVertex(path, index), seg.P);
					case Kind.CubicTo:
						return Same(seg.P2, seg.P) ? LineAngle(seg.P1, seg.P) : LineAngle(seg.P2, seg.P);
					case Kind.Close when before.Kind == Kind.LineTo:
						return LineAngle(before.P, SubpathStart(path, index));
					case Kind.Close when before.Kind == Kind.CubicTo:
						Vector2 start = SubpathStart(path, index);
						return CurvesAngle(PreviousVertex(path, index), before.P2, before.P, start, start);
					default:
						return 0;
				}
			}

			Segment next = path[index + 1];
			Vector2 previous = PreviousVertex(path, index);
			switch ((seg.Kind, next.Kind))
			{
				case (Kind.Close, _):
					return 0;
				case (Kind.MoveTo, Kind.LineTo):
					return LineAngle(seg.P, next.P);
				case (Kind.MoveTo, Kind.CubicTo):
					return LineAngle(seg.P, next.P1);
				case (Kind.LineTo, Kind.LineTo):
					return Angle(previous, seg.P, seg.P, next.P);
				case (Kind.CubicTo, Kind.CubicTo):
					return CurvesAngle(previous, seg.P2, seg.P, next.P1, next.P);
				case (Kind.LineTo, Kind.CubicTo):
					return CurvesAngle(previous, previous, seg.P, next.P1, next.P);
				case (Kind.CubicTo, Kind.LineTo):
					return CurvesAngle(previous, seg.P2, seg.P, next.P, next.P);
				case (Kind.LineTo, Kind.MoveTo):
					return LineAngle(previous, seg.P);
				case (Kind.CubicTo, Kind.MoveTo):
					return Same(seg.P, seg.P2) ? LineAngle(previous, seg.P) : LineAngle(seg.P2, seg.P);
				case (Kind.LineTo, Kind.Close):
					return Angle(previous, seg.P, seg.P, SubpathStart(path, index));
				case (_, Kind.Close):
					// usvg's catch-all: the vertex before this segment to the subpath's start.
					return LineAngle(previous, SubpathStart(path, index));
				default:
					return 0;
			}
		}

		private static bool Same(Vector2 a, Vector2 b)
		{
			static bool Near(double x, double y) => Math.Abs(x - y) <= 1e-9 * Math.Max(1, Math.Max(Math.Abs(x), Math.Abs(y)));
			return Near(a.X, b.X) && Near(a.Y, b.Y);
		}

		private static double LineAngle(Vector2 from, Vector2 to) => Angle(from, to, from, to);

		/// <summary>A curve-to-curve join: where a control point sits on the vertex, the next point out gives the direction.</summary>
		private static double CurvesAngle(Vector2 previous, Vector2 controlIn, Vector2 vertex, Vector2 controlOut, Vector2 next)
		{
			if (Same(controlIn, vertex))
			{
				return Angle(previous, vertex, vertex, controlOut);
			}

			if (Same(vertex, controlOut))
			{
				return Angle(controlIn, vertex, vertex, next);
			}

			return Angle(controlIn, vertex, vertex, controlOut);
		}

		/// <summary>The bisector, in degrees 0..360, of the directions a1→a2 (incoming) and b1→b2 (outgoing).</summary>
		private static double Angle(Vector2 a1, Vector2 a2, Vector2 b1, Vector2 b2)
		{
			static double Normalize(double radians)
			{
				double v = radians % (Math.PI * 2);
				return v < 0 ? v + Math.PI * 2 : v;
			}

			double incoming = Normalize(Math.Atan2(a2.Y - a1.Y, a2.X - a1.X));
			double outgoing = Normalize(Math.Atan2(b2.Y - b1.Y, b2.X - b1.X));
			double half = (outgoing - incoming) / 2;
			double angle = incoming + half;
			if (Math.Abs(half) > Math.PI / 2)
			{
				angle -= Math.PI;
			}

			return Normalize(angle) * 180 / Math.PI;
		}

		/// <summary>The start of the subpath that segment <paramref name="index"/> is in: the last move before it.</summary>
		private static Vector2 SubpathStart(List<Segment> path, int index)
		{
			for (int i = index - 1; i >= 0; i--)
			{
				if (path[i].Kind == Kind.MoveTo)
				{
					return path[i].P;
				}
			}

			return default;
		}

		private static Vector2 PreviousVertex(List<Segment> path, int index)
		{
			Segment previous = path[index - 1];
			return previous.Kind == Kind.Close ? SubpathStart(path, index) : previous.P;
		}

		/// <summary>One path command: its end point <see cref="P"/> and, for a cubic, its control points.</summary>
		private readonly struct Segment
		{
			public Segment(Kind kind, Vector2 p1, Vector2 p2, Vector2 p)
			{
				this.Kind = kind;
				this.P1 = p1;
				this.P2 = p2;
				this.P = p;
			}

			public Kind Kind { get; }

			public Vector2 P1 { get; }

			public Vector2 P2 { get; }

			public Vector2 P { get; }
		}
	}
}
