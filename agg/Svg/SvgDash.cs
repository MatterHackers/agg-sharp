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
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Svg
{
	/// <summary>
	/// stroke-dasharray as resvg draws it (tiny-skia-path's dash.rs). Distance along a curve is measured on chords of it,
	/// split in half until each is within 0.5 / scale of the curve (the "too curvy" tests), and each dash is cut from
	/// the true curve at the t its distance maps to. The chords are shorter than the curve, so dashes land a little
	/// further along than true arc length puts them - by a third of a pixel on the suite's circles, enough to fail
	/// them. agg's conv_dash measures the flattened outline instead and stays as it is for its other callers.
	/// </summary>
	public static class SvgDash
	{
		private const uint MaxT = 0x3FFFFFFF;

		/// <summary>
		/// The dashes of <paramref name="path"/> (lines and curves, in its space) for the even-count pattern
		/// <paramref name="array"/> starting <paramref name="offset"/> in, measured at <paramref name="resolutionScale"/>
		/// pixels per unit. Null when the pattern draws nothing.
		/// </summary>
		public static VertexStorage Dash(IVertexSource path, IReadOnlyList<double> array, double offset, double resolutionScale)
		{
			double intervalLength = 0;
			foreach (double length in array)
			{
				intervalLength += length;
			}

			if (array.Count < 2 || array.Count % 2 != 0 || !(intervalLength > 0) || double.IsInfinity(intervalLength) || !double.IsFinite(offset))
			{
				return null;
			}

			offset = AdjustOffset(offset, intervalLength);
			(double firstLength, int firstIndex) = FirstInterval(array, offset);
			double tolerance = 0.5 / (resolutionScale > 0 ? resolutionScale : 1);

			var output = new VertexStorage();
			double dashCount = 0;
			foreach (Contour contour in Contours(path, tolerance))
			{
				// tiny-skia's guard against patterns so fine they would never finish.
				dashCount += contour.Length * (array.Count >> 1) / intervalLength;
				if (dashCount > 1000000)
				{
					return null;
				}

				bool skipFirst = contour.IsClosed;
				int index = firstIndex;
				double distance = 0;
				double dashLength = firstLength;
				while (distance < contour.Length)
				{
					if (index % 2 == 0 && !skipFirst)
					{
						contour.AppendPiece(distance, distance + dashLength, true, output);
					}

					distance += dashLength;
					skipFirst = false;
					index = (index + 1) % array.Count;
					dashLength = array[index];
				}

				// A closed contour's first dash is skipped above and drawn last. dash.rs continues it from a dash that
				// reached the end (no moveto), but the suite's references draw it as a dash of its own: with
				// stroke-dasharray="40 0" on a rect (painting/stroke-dasharray/n-0) the start corner is left square cut,
				// like the other corners, where a join would fill it.
				if (contour.IsClosed && firstIndex % 2 == 0 && firstLength >= 0)
				{
					contour.AppendPiece(0, firstLength, true, output);
				}
			}

			return output;
		}

		private static double AdjustOffset(double offset, double length)
		{
			if (offset < 0)
			{
				offset = -offset;
				if (offset > length)
				{
					offset %= length;
				}

				offset = length - offset;
				return offset == length ? 0 : offset;
			}

			return offset >= length ? offset % length : offset;
		}

		private static (double, int) FirstInterval(IReadOnlyList<double> array, double offset)
		{
			for (int i = 0; i < array.Count; i++)
			{
				double gap = array[i];
				if (offset > gap || (offset == gap && gap != 0))
				{
					offset -= gap;
				}
				else
				{
					return (gap - offset, i);
				}
			}

			return (array[0], 0);
		}

		private enum Kind
		{
			Line,
			Quad,
			Cubic
		}

		/// <summary>A piece of the measure: the distance at its end, and the t it ends at on the source segment starting at PointIndex.</summary>
		private struct Piece
		{
			public double Distance;
			public int PointIndex;
			public uint T;
			public Kind Kind;

			public double ScalarT => T / (double)MaxT;
		}

		/// <summary>One subpath measured as tiny-skia's ContourMeasure measures it.</summary>
		private class Contour
		{
			public readonly List<Piece> Pieces = new List<Piece>();
			public readonly List<Vector2> Points = new List<Vector2>();
			public double Length;
			public bool IsClosed;

			public double AddLine(Vector2 p0, Vector2 p1, double distance, int pointIndex)
			{
				double next = distance + (p1 - p0).Length;
				if (next > distance)
				{
					Pieces.Add(new Piece { Distance = next, PointIndex = pointIndex, T = MaxT, Kind = Kind.Line });
				}

				return next;
			}

			public double AddQuad(Vector2 p0, Vector2 p1, Vector2 p2, double distance, uint minT, uint maxT, int pointIndex, double tolerance)
			{
				if (((maxT - minT) >> 10) != 0 && QuadTooCurvy(p0, p1, p2, tolerance))
				{
					uint halfT = (minT + maxT) >> 1;
					var (a, b, c, d, e) = ChopQuad(p0, p1, p2, 0.5);
					distance = AddQuad(a, b, c, distance, minT, halfT, pointIndex, tolerance);
					return AddQuad(c, d, e, distance, halfT, maxT, pointIndex, tolerance);
				}

				double next = distance + (p2 - p0).Length;
				if (next > distance)
				{
					Pieces.Add(new Piece { Distance = next, PointIndex = pointIndex, T = maxT, Kind = Kind.Quad });
				}

				return next;
			}

			public double AddCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, double distance, uint minT, uint maxT, int pointIndex, double tolerance)
			{
				if (((maxT - minT) >> 10) != 0 && CubicTooCurvy(p0, p1, p2, p3, tolerance))
				{
					uint halfT = (minT + maxT) >> 1;
					Vector2[] halves = ChopCubic(p0, p1, p2, p3, 0.5);
					distance = AddCubic(halves[0], halves[1], halves[2], halves[3], distance, minT, halfT, pointIndex, tolerance);
					return AddCubic(halves[3], halves[4], halves[5], halves[6], distance, halfT, maxT, pointIndex, tolerance);
				}

				double next = distance + (p3 - p0).Length;
				if (next > distance)
				{
					Pieces.Add(new Piece { Distance = next, PointIndex = pointIndex, T = maxT, Kind = Kind.Cubic });
				}

				return next;
			}

			/// <summary>The piece holding <paramref name="distance"/> and the t it maps to, interpolated linearly within the piece.</summary>
			private (int, double) Locate(double distance)
			{
				// The first piece ending at or past distance.
				int low = 0;
				int high = Pieces.Count - 1;
				while (low < high)
				{
					int middle = (low + high) >> 1;
					if (Pieces[middle].Distance < distance)
					{
						low = middle + 1;
					}
					else
					{
						high = middle;
					}
				}

				if (Pieces[high].Distance < distance)
				{
					high++;
				}

				Piece piece = Pieces[high];
				double startT = 0;
				double startDistance = 0;
				if (high > 0)
				{
					startDistance = Pieces[high - 1].Distance;
					if (Pieces[high - 1].PointIndex == piece.PointIndex)
					{
						startT = Pieces[high - 1].ScalarT;
					}
				}

				double t = startT + (piece.ScalarT - startT) * (distance - startDistance) / (piece.Distance - startDistance);
				return (high, Math.Clamp(t, 0, 1));
			}

			/// <summary>tiny-skia's push_segment: the stretch from <paramref name="start"/> to <paramref name="stop"/> along the contour.</summary>
			public void AppendPiece(double start, double stop, bool startWithMoveTo, VertexStorage output)
			{
				start = Math.Max(start, 0);
				stop = Math.Min(stop, Length);
				if (!(start <= stop) || Pieces.Count == 0)
				{
					return;
				}

				(int index, double startT) = Locate(start);
				(int stopIndex, double stopT) = Locate(stop);
				Piece piece = Pieces[index];
				Piece stopPiece = Pieces[stopIndex];
				if (startWithMoveTo)
				{
					Vector2 p = Evaluate(piece, startT);
					output.MoveTo(p.X, p.Y);
				}

				if (piece.PointIndex == stopPiece.PointIndex)
				{
					SegmentTo(piece, startT, stopT, output);
					return;
				}

				while (true)
				{
					SegmentTo(piece, startT, 1, output);
					int oldPointIndex = piece.PointIndex;
					do
					{
						index++;
					}
					while (Pieces[index].PointIndex == oldPointIndex);

					piece = Pieces[index];
					startT = 0;
					if (piece.PointIndex >= stopPiece.PointIndex)
					{
						break;
					}
				}

				SegmentTo(piece, 0, stopT, output);
			}

			private Vector2 Evaluate(Piece piece, double t)
			{
				int i = piece.PointIndex;
				switch (piece.Kind)
				{
					case Kind.Line:
						return Points[i] + (Points[i + 1] - Points[i]) * t;

					case Kind.Quad:
						return ChopQuad(Points[i], Points[i + 1], Points[i + 2], t).Item3;

					default:
						return ChopCubic(Points[i], Points[i + 1], Points[i + 2], Points[i + 3], t)[3];
				}
			}

			/// <summary>The source segment of <paramref name="piece"/> from t <paramref name="startT"/> to <paramref name="stopT"/>, as the same kind of segment.</summary>
			private void SegmentTo(Piece piece, double startT, double stopT, VertexStorage output)
			{
				if (startT == stopT)
				{
					// A zero length dash is still a dash: round and square caps draw it as a dot.
					output.LastVertex(out double x, out double y);
					output.LineTo(x, y);
					return;
				}

				int i = piece.PointIndex;
				switch (piece.Kind)
				{
					case Kind.Line:
						{
							Vector2 end = stopT == 1 ? Points[i + 1] : Points[i] + (Points[i + 1] - Points[i]) * stopT;
							output.LineTo(end.X, end.Y);
						}

						break;

					case Kind.Quad:
						{
							Vector2 p0 = Points[i], p1 = Points[i + 1], p2 = Points[i + 2];
							if (startT > 0)
							{
								var tail = ChopQuad(p0, p1, p2, startT);
								(p0, p1, p2) = (tail.Item3, tail.Item4, tail.Item5);
								stopT = (stopT - startT) / (1 - startT);
							}

							if (stopT < 1)
							{
								var head = ChopQuad(p0, p1, p2, stopT);
								(p1, p2) = (head.Item2, head.Item3);
							}

							output.Curve3(p1.X, p1.Y, p2.X, p2.Y);
						}

						break;

					default:
						{
							Vector2 p0 = Points[i], p1 = Points[i + 1], p2 = Points[i + 2], p3 = Points[i + 3];
							if (startT > 0)
							{
								Vector2[] tail = ChopCubic(p0, p1, p2, p3, startT);
								(p0, p1, p2, p3) = (tail[3], tail[4], tail[5], tail[6]);
								stopT = (stopT - startT) / (1 - startT);
							}

							if (stopT < 1)
							{
								Vector2[] head = ChopCubic(p0, p1, p2, p3, stopT);
								(p1, p2, p3) = (head[1], head[2], head[3]);
							}

							output.Curve4(p1.X, p1.Y, p2.X, p2.Y, p3.X, p3.Y);
						}

						break;
				}
			}
		}

		/// <summary>
		/// The subpaths of <paramref name="path"/> measured. A moveto that follows a moveto replaces it, as tiny-skia's
		/// path builder does; a subpath that goes nowhere measures nothing and draws no dashes.
		/// </summary>
		private static IEnumerable<Contour> Contours(IVertexSource path, double tolerance)
		{
			Contour contour = null;
			int pointIndex = 0;
			double distance = 0;
			Vector2 previous = Vector2.Zero;
			var pending = new List<Vector2>(3);

			Contour Finish()
			{
				Contour done = contour;
				contour = null;
				if (done == null)
				{
					return null;
				}

				if (done.IsClosed)
				{
					double before = distance;
					Vector2 first = done.Points[0];
					distance = done.AddLine(done.Points[pointIndex], first, distance, pointIndex);
					if (distance > before)
					{
						done.Points.Add(first);
					}
				}

				done.Length = distance;
				return double.IsFinite(distance) ? done : null;
			}

			foreach (VertexData vertex in path.Vertices())
			{
				FlagsAndCommand command = vertex.Command & FlagsAndCommand.CommandsMask;
				if (command == FlagsAndCommand.MoveTo)
				{
					if (contour != null && contour.Points.Count == 1 && !contour.IsClosed)
					{
						contour.Points[0] = vertex.Position;
					}
					else
					{
						if (Finish() is Contour done)
						{
							yield return done;
						}

						contour = new Contour();
						contour.Points.Add(vertex.Position);
						pointIndex = 0;
						distance = 0;
					}

					previous = vertex.Position;
					pending.Clear();
				}
				else if (command == FlagsAndCommand.EndPoly)
				{
					if (contour != null && ShapePath.IsClose(vertex.Command))
					{
						contour.IsClosed = true;
					}
				}
				else if (contour != null && !contour.IsClosed && (command == FlagsAndCommand.LineTo || command == FlagsAndCommand.Curve3 || command == FlagsAndCommand.Curve4))
				{
					pending.Add(vertex.Position);
					double before = distance;
					if (command == FlagsAndCommand.LineTo)
					{
						distance = contour.AddLine(previous, pending[0], distance, pointIndex);
					}
					else if (command == FlagsAndCommand.Curve3 && pending.Count == 2)
					{
						distance = contour.AddQuad(previous, pending[0], pending[1], distance, 0, MaxT, pointIndex, tolerance);
					}
					else if (command == FlagsAndCommand.Curve4 && pending.Count == 3)
					{
						distance = contour.AddCubic(previous, pending[0], pending[1], pending[2], distance, 0, MaxT, pointIndex, tolerance);
					}
					else
					{
						continue;
					}

					if (distance > before)
					{
						contour.Points.AddRange(pending);
						pointIndex += pending.Count;
					}

					previous = pending[pending.Count - 1];
					pending.Clear();
				}
			}

			if (Finish() is Contour last)
			{
				yield return last;
			}
		}

		private static bool QuadTooCurvy(Vector2 p0, Vector2 p1, Vector2 p2, double tolerance)
		{
			double dx = p1.X / 2 - (p0.X + p2.X) / 4;
			double dy = p1.Y / 2 - (p0.Y + p2.Y) / 4;
			return Math.Max(Math.Abs(dx), Math.Abs(dy)) > tolerance;
		}

		private static bool CubicTooCurvy(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, double tolerance)
		{
			return CheapDistanceExceeds(p1, p0 + (p3 - p0) * (1.0 / 3), tolerance)
				|| CheapDistanceExceeds(p2, p0 + (p3 - p0) * (2.0 / 3), tolerance);
		}

		private static bool CheapDistanceExceeds(Vector2 a, Vector2 b, double tolerance)
		{
			return Math.Max(Math.Abs(a.X - b.X), Math.Abs(a.Y - b.Y)) > tolerance;
		}

		/// <summary>de Casteljau at <paramref name="t"/>: the two halves share the middle point.</summary>
		private static (Vector2, Vector2, Vector2, Vector2, Vector2) ChopQuad(Vector2 p0, Vector2 p1, Vector2 p2, double t)
		{
			Vector2 a = p0 + (p1 - p0) * t;
			Vector2 b = p1 + (p2 - p1) * t;
			return (p0, a, a + (b - a) * t, b, p2);
		}

		private static Vector2[] ChopCubic(Vector2 p0, Vector2 p1, Vector2 p2, Vector2 p3, double t)
		{
			Vector2 ab = p0 + (p1 - p0) * t;
			Vector2 bc = p1 + (p2 - p1) * t;
			Vector2 cd = p2 + (p3 - p2) * t;
			Vector2 abc = ab + (bc - ab) * t;
			Vector2 bcd = bc + (cd - bc) * t;
			return new[] { p0, ab, abc, abc + (bcd - abc) * t, bcd, cd, p3 };
		}
	}
}
