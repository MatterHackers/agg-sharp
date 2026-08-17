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
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THE
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// VertexSourceLegacySupport caches the IEnumerator returned by Vertices() in Rewind(), and
	/// Vertex() only creates one lazily when the cache is null. Nothing in the derived types'
	/// re-initialization methods (Ellipse.init, Arc.init, RoundedRect.rect/radius,
	/// FlattenCurves.SetVertexSource, TypeFacePrinter.Text) clears that cache. So a source that has
	/// already been drained to Stop keeps handing back an exhausted enumerator: the caller reads
	/// Stop on the very first Vertex() call and the reshaped geometry silently draws nothing.
	///
	/// This is the same class of hazard that was previously patched at a single call site (the
	/// polygon edit widget re-Rewound by hand); these tests pin the behavior at the source so the
	/// fix can live in the base class instead of in every caller that happens to remember.
	/// </summary>
	public class VertexSourceReinitTests
	{
		[Test]
		public async Task EllipseInitInvalidatesCachedEnumerator()
		{
			var ellipse = new Ellipse(0, 0, 10, 10);

			var firstPass = Drain(ellipse);
			await Assert.That(firstPass.Count).IsGreaterThan(0);

			ellipse.init(50, 50, 5, 5);

			// Deliberately no Rewind - re-initializing is supposed to be enough
			var secondPass = Drain(ellipse);

			await Assert.That(secondPass.Count).IsGreaterThan(0);

			// Ellipse.Vertices() opens with a MoveTo at (originX + radiusX, originY)
			await Assert.That(secondPass[0].Position.X).IsEqualTo(55.0).Within(0.001);
			await Assert.That(secondPass[0].Position.Y).IsEqualTo(50.0).Within(0.001);
		}

		[Test]
		public async Task ArcInitInvalidatesCachedEnumerator()
		{
			var arc = new Arc();
			arc.init(0, 0, 10, 10, 0, MathHelper.Tau / 4);

			var firstPass = Drain(arc);
			await Assert.That(firstPass.Count).IsGreaterThan(0);

			arc.init(100, 100, 5, 5, 0, MathHelper.Tau / 4);

			var secondPass = Drain(arc);

			await Assert.That(secondPass.Count).IsGreaterThan(0);

			// Counter clockwise arcs open with a MoveTo at the start angle
			await Assert.That(secondPass[0].Position.X).IsEqualTo(105.0).Within(0.001);
			await Assert.That(secondPass[0].Position.Y).IsEqualTo(100.0).Within(0.001);
		}

		[Test]
		public async Task RoundedRectReinitInvalidatesCachedEnumerator()
		{
			var roundedRect = new RoundedRect(0, 0, 10, 10, 2);

			var firstPass = Drain(roundedRect);
			await Assert.That(firstPass.Count).IsGreaterThan(0);

			roundedRect.rect(100, 100, 120, 120);
			roundedRect.radius(3);

			var secondPass = Drain(roundedRect);

			await Assert.That(secondPass.Count).IsGreaterThan(0);

			foreach (var vertex in secondPass)
			{
				// JoinPaths (which RoundedRect builds on) closes with an EndPoly whose position is
				// deliberately Vector2.Zero - only the drawn points carry coordinates.
				if (!ShapePath.IsVertex(vertex.Command))
				{
					continue;
				}

				await Assert.That(vertex.Position.X).IsGreaterThanOrEqualTo(100.0 - 0.001);
				await Assert.That(vertex.Position.X).IsLessThanOrEqualTo(120.0 + 0.001);
				await Assert.That(vertex.Position.Y).IsGreaterThanOrEqualTo(100.0 - 0.001);
				await Assert.That(vertex.Position.Y).IsLessThanOrEqualTo(120.0 + 0.001);
			}
		}

		[Test]
		public async Task FlattenCurvesSetVertexSourceInvalidatesCachedEnumerator()
		{
			var flattenCurves = new FlattenCurves(MakeTriangle(0, 0));

			var firstPass = Drain(flattenCurves);
			await Assert.That(firstPass.Count).IsGreaterThan(0);

			flattenCurves.SetVertexSource(MakeTriangle(100, 100));

			var secondPass = Drain(flattenCurves);

			await Assert.That(secondPass.Count).IsGreaterThan(0);
			await Assert.That(secondPass[0].Position.X).IsEqualTo(100.0).Within(0.001);
			await Assert.That(secondPass[0].Position.Y).IsEqualTo(100.0).Within(0.001);
		}

		[Test]
		public async Task TypeFacePrinterTextChangeInvalidatesCachedEnumerator()
		{
			var typeFaceStyle = new StyledTypeFace(LiberationSansFont.Instance, 12);
			var printer = new TypeFacePrinter("A", typeFaceStyle);

			var firstPass = Drain(printer);
			await Assert.That(firstPass.Count).IsGreaterThan(0);

			printer.Text = "B";

			var secondPass = Drain(printer);

			await Assert.That(secondPass.Count).IsGreaterThan(0);

			// A re-texted printer must emit exactly what a printer built for that text emits - not merely
			// some vertices, which a stale-but-not-empty cache could also produce.
			var expected = Drain(new TypeFacePrinter("B", typeFaceStyle));

			await Assert.That(secondPass.Count).IsEqualTo(expected.Count);
			for (int i = 0; i < expected.Count; i++)
			{
				await Assert.That(secondPass[i].Command).IsEqualTo(expected[i].Command);
				await Assert.That(secondPass[i].Position.X).IsEqualTo(expected[i].Position.X).Within(0.000001);
				await Assert.That(secondPass[i].Position.Y).IsEqualTo(expected[i].Position.Y).Within(0.000001);
			}
		}

		[Test]
		public async Task FlattenCurvesResolutionScaleInvalidatesCachedEnumerator()
		{
			var flattenCurves = new FlattenCurves(MakeCurvedPath());

			var firstPass = Drain(flattenCurves);
			await Assert.That(firstPass.Count).IsGreaterThan(0);

			// ResolutionScale changes how finely the curve is flattened, so it changes the vertices
			flattenCurves.ResolutionScale = 10;

			var secondPass = Drain(flattenCurves);

			await Assert.That(secondPass.Count).IsGreaterThan(0);

			// A finer approximation scale has to spend more line segments on the same curve
			await Assert.That(secondPass.Count).IsGreaterThan(firstPass.Count);
		}

		private static VertexStorage MakeCurvedPath()
		{
			var storage = new VertexStorage();
			storage.MoveTo(0, 0);
			storage.Curve4(10, 40, 30, -40, 40, 0);

			return storage;
		}

		private static VertexStorage MakeTriangle(double left, double bottom)
		{
			var storage = new VertexStorage();
			storage.MoveTo(left, bottom);
			storage.LineTo(left + 10, bottom);
			storage.LineTo(left + 5, bottom + 10);
			storage.ClosePolygon();

			return storage;
		}

		/// <summary>
		/// Pulls every vertex up to (but not including) Stop out of a source using only the legacy
		/// Vertex() call. Rewind is intentionally never called - the first Vertex() on a fresh source
		/// rewinds lazily, which is what real drawing code relies on.
		/// </summary>
		private static List<VertexData> Drain(IVertexSource source)
		{
			var vertices = new List<VertexData>();

			while (true)
			{
				var command = source.Vertex(out double x, out double y);
				if (command == FlagsAndCommand.Stop)
				{
					break;
				}

				vertices.Add(new VertexData(command, new Vector2(x, y)));

				if (vertices.Count > 100000)
				{
					throw new Exception("Vertex source did not terminate");
				}
			}

			return vertices;
		}
	}
}
