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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Vertex output of the conv_dash / vcgen_markers_term / arrowhead / conv_marker ports. The expected
	/// sequences are traced by hand through the C++ AGG 2.4 sources (agg_vcgen_dash.cpp,
	/// agg_vcgen_markers_term.cpp, agg_arrowhead.cpp, agg_conv_marker.h); the pixel-level check against C++
	/// itself is AggReferenceTests.DashesAndArrowheadsMatchCppAgg.
	/// </summary>
	public class DashMarkerTests
	{
		private const FlagsAndCommand M = FlagsAndCommand.MoveTo;
		private const FlagsAndCommand L = FlagsAndCommand.LineTo;

		[Test]
		public async Task DashCutsALineIntoDashesAndGaps()
		{
			var dash = new Dash(Line(0, 0, 100, 0));
			dash.AddDash(20, 10);

			await AssertVertices(dash, (M, 0, 0), (L, 20, 0), (M, 30, 0), (L, 50, 0), (M, 60, 0), (L, 80, 0), (M, 90, 0), (L, 100, 0));
		}

		[Test]
		public async Task DashStartOffsetsThePattern()
		{
			var dash = new Dash(Line(0, 0, 100, 0));
			dash.AddDash(20, 10);
			dash.DashStart(15);

			// The line ends inside a gap, so the last vertex is a MoveTo: C++ emits it too.
			await AssertVertices(dash, (M, 0, 0), (L, 5, 0), (M, 15, 0), (L, 35, 0), (M, 45, 0), (L, 65, 0), (M, 75, 0), (L, 95, 0), (M, 100, 0));

			// Re-reading gives the same dashes: a non-negative start is re-applied on every read.
			await AssertVertices(dash, (M, 0, 0), (L, 5, 0), (M, 15, 0), (L, 35, 0), (M, 45, 0), (L, 65, 0), (M, 75, 0), (L, 95, 0), (M, 100, 0));
		}

		[Test]
		public async Task DashFollowsAClosedPolygonBackToItsStart()
		{
			var square = new VertexStorage();
			square.MoveTo(0, 0);
			square.LineTo(10, 0);
			square.LineTo(10, 10);
			square.LineTo(0, 10);
			square.ClosePolygon();
			var dash = new Dash(square);
			dash.AddDash(15, 5);

			// The closing edge (0,10)->(0,0) is dashed too. A gap that ends exactly on a corner yields a
			// second MoveTo there, as in C++.
			await AssertVertices(dash, (M, 0, 0), (L, 10, 0), (L, 10, 5), (M, 10, 10), (M, 10, 10), (L, 0, 10), (L, 0, 5), (M, 0, 0));
		}

		/// <summary>
		/// Shorten cuts the path's end back before dashing or stroking (agg_shorten_path.h moves the last
		/// vertex along its segment). The port used to move a copy of that vertex, so nothing was cut.
		/// </summary>
		[Test]
		public async Task ShortenCutsThePathEnd()
		{
			var dash = new Dash(Line(0, 0, 100, 0));
			dash.AddDash(20, 10);
			dash.Shorten = 25;
			await AssertVertices(dash, (M, 0, 0), (L, 20, 0), (M, 30, 0), (L, 50, 0), (M, 60, 0), (L, 75, 0));

			// A butt-capped stroke of (0,0)-(100,0) shortened by 25 spans x 0 to 75.
			var stroke = new Stroke(Line(0, 0, 100, 0), 2) { Shorten = 25 };
			var xs = stroke.Vertices().Where(v => ShapePath.IsVertex(v.Command)).Select(v => v.Position.X).ToList();
			await Assert.That(xs.Min()).IsEqualTo(0.0);
			await Assert.That(xs.Max()).IsEqualTo(75.0);
		}

		[Test]
		public async Task DashWithoutAPatternOrPathYieldsNothing()
		{
			await AssertVertices(new Dash(Line(0, 0, 100, 0)));

			var dash = new Dash(Line(0, 0, 100, 0));
			dash.AddDash(10, 5);
			dash.RemoveAllDashes();
			await AssertVertices(dash);
		}

		[Test]
		public async Task AllZeroDashPatternYieldsNothing()
		{
			var dash = new Dash(Line(0, 0, 100, 0));
			dash.AddDash(0, 0);

			// AssertRead stops after 100 vertices, so a pattern that never advances fails here rather than hanging.
			await AssertVertices(dash);
		}

		[Test]
		public async Task TerminalMarkersHoldEachSubPathsEnds()
		{
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(10, 0);
			path.LineTo(10, 10);
			path.MoveTo(20, 20);
			path.LineTo(30, 20);
			var markers = new TerminalMarkers();
			var dash = new Dash(path, markers);
			dash.AddDash(3, 2);
			dash.Vertices().ToList();

			// Tails: first point, then the second as its direction.
			await AssertVertices(markers, (M, 0, 0), (L, 10, 0), (M, 20, 20), (L, 30, 20));

			// Heads: last point, then the one before.
			markers.Rewind(1);
			await AssertRead(markers, (M, 10, 10), (L, 10, 0), (M, 30, 20), (L, 20, 20));

			// Vertices() reads both: tails, then heads.
			var all = markers.Vertices().Select(v => (v.Command, v.Position.X, v.Position.Y)).ToList();
			await Assert.That(string.Join(" ", all)).IsEqualTo(string.Join(" ", new[]
			{
				(M, 0.0, 0.0), (L, 10.0, 0.0), (M, 20.0, 20.0), (L, 30.0, 20.0),
				(M, 10.0, 10.0), (L, 10.0, 0.0), (M, 30.0, 20.0), (L, 20.0, 20.0), (FlagsAndCommand.Stop, 0.0, 0.0),
			}));
		}

		[Test]
		public async Task ArrowheadShapes()
		{
			var arrowhead = new Arrowhead();
			arrowhead.Head(4, 4, 3, 2);
			arrowhead.Tail(1, 1.5, 3, 5);

			// C++ puts the tail's stop in slot 6, before its close command, so the tail is left open.
			arrowhead.Rewind(0);
			await AssertRead(arrowhead, (M, 1, 0), (L, -4, 3), (L, -6.5, 3), (L, -1.5, 0), (L, -6.5, -3), (L, -4, -3));

			// The head's end-poly carries whatever coordinates slot 4 last held - here the tail's (-6.5, -3) -
			// because C++ never clears them.
			arrowhead.Rewind(1);
			await AssertRead(arrowhead, (M, -4, 0), (L, 6, -3), (L, 4, 0), (L, 6, 3), (ClosedCcw, -6.5, -3));
		}

		[Test]
		public async Task MarkerPlacerTurnsTheHeadOntoThePathEnd()
		{
			var markers = new TerminalMarkers();
			var dash = new Dash(Line(0, 0, 10, 0), markers);
			dash.AddDash(3, 2);
			dash.Vertices().ToList();

			var arrowhead = new Arrowhead();
			arrowhead.Head(4, 4, 3, 2);

			// The head sits on (10,0) facing back along the line (angle pi): its tip lands 4 past the end.
			// Its end-poly command goes through the transform too, as in C++.
			await AssertVertices(new MarkerPlacer(markers, arrowhead), (M, 14, 0), (L, 4, 3), (L, 6, 0), (L, 4, -3), (ClosedCcw, 10, 0));
		}

		/// <summary>
		/// conv_marker_adaptor (vcgen_vertex_sequence): the path itself, its end cut back by the shorten, closed
		/// off with an open end-poly; the markers still see the unshortened ends.
		/// </summary>
		[Test]
		public async Task MarkerAdaptorPassesThePathThroughShortenedAndRecordsItsEnds()
		{
			var path = new VertexStorage();
			path.MoveTo(0, 0);
			path.LineTo(40, 0);
			path.LineTo(40, 30);
			var markers = new TerminalMarkers();
			var adaptor = new MarkerAdaptor(path, markers) { Shorten = 10 };

			await AssertVertices(adaptor, (M, 0, 0), (L, 40, 0), (L, 40, 20), (FlagsAndCommand.EndPoly, 0, 0));

			markers.Rewind(1);
			await AssertRead(markers, (M, 40, 30), (L, 40, 0));
		}

		/// <summary>conv_stroke with vcgen_markers_term: reading the stroke fills the markers with the source's ends.</summary>
		[Test]
		public async Task StrokeRecordsItsSourceEndsInItsMarkers()
		{
			var markers = new TerminalMarkers();
			var stroke = new Stroke(Line(0, 0, 100, 0), markers, 2);
			stroke.Vertices().ToList();

			markers.Rewind(0);
			await AssertRead(markers, (M, 0, 0), (L, 100, 0));
			markers.Rewind(1);
			await AssertRead(markers, (M, 100, 0), (L, 0, 0));
		}

		/// <summary>conv_concat: every vertex of the first source, then every vertex of the second.</summary>
		[Test]
		public async Task ConcatPathsReadsTheFirstSourceThenTheSecond()
		{
			var concat = new ConcatPaths(Line(0, 0, 1, 0), Line(5, 5, 6, 6));

			await AssertVertices(concat, (M, 0, 0), (L, 1, 0), (M, 5, 5), (L, 6, 6));
			await AssertVertices(concat, (M, 0, 0), (L, 1, 0), (M, 5, 5), (L, 6, 6));
		}

		private const FlagsAndCommand ClosedCcw =FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW;

		private static VertexStorage Line(double x1, double y1, double x2, double y2)
		{
			var line = new VertexStorage();
			line.MoveTo(x1, y1);
			line.LineTo(x2, y2);
			return line;
		}

		private static async Task AssertVertices(IVertexSource source, params (FlagsAndCommand command, double x, double y)[] expected)
		{
			source.Rewind(0);
			await AssertRead(source, expected);
		}

		// Reads from the current position to Stop and compares with the expected vertices plus that Stop.
		private static async Task AssertRead(IVertexSource source, params (FlagsAndCommand command, double x, double y)[] expected)
		{
			var actual = new List<(FlagsAndCommand command, double x, double y)>();
			FlagsAndCommand command;
			while (!ShapePath.IsStop(command = source.Vertex(out double x, out double y)) && actual.Count < 100)
			{
				actual.Add((command, x, y));
			}

			// Adding 0.0 turns the -0 of a rounded -1e-16 into 0, so it prints as "0".
			string Describe(IEnumerable<(FlagsAndCommand command, double x, double y)> vertices)
				=> string.Join(" ", vertices.Select(v => $"{v.command}({Math.Round(v.x, 6) + 0.0},{Math.Round(v.y, 6) + 0.0})"));

			await Assert.That(Describe(actual)).IsEqualTo(Describe(expected));
			for (int i = 0; i < expected.Length; i++)
			{
				await Assert.That(Math.Abs(actual[i].x - expected[i].x)).IsLessThan(1e-9);
				await Assert.That(Math.Abs(actual[i].y - expected[i].y)).IsLessThan(1e-9);
			}
		}
	}
}
