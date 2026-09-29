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

using System.Globalization;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using MatterHackers.Agg.SvgTools;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// The path data grammar as SVG and svgtypes read it: <see cref="SvgParser.ParseSvgDString"/> stops at the first
	/// invalid segment and keeps the path drawn so far, arc flags are single characters, and a path must open with a
	/// moveto. The same parser reads MatterCAD's own d-strings, so valid data has to come out as it always did.
	/// </summary>
	public class SvgPathDataTests
	{
		/// <summary>Commands and positions, so two parses can be compared exactly.</summary>
		private static string Vertices(string dString)
		{
			return string.Join(" ", new VertexStorage(dString).Vertices().Select(v => $"{(int)v.Command}:{v.Position.X:0.######},{v.Position.Y:0.######}"));
		}

		[Test]
		public async Task ArcFlagsMayRunIntoTheNextNumber()
		{
			await Assert.That(Vertices("M10,10 a1,1 0 01 5,5")).IsEqualTo(Vertices("M10,10 a1,1 0 0 1 5,5"));
			await Assert.That(Vertices("M10,10 a1,1 0 015,5")).IsEqualTo(Vertices("M10,10 a1,1 0 0 1 5,5"));
		}

		[Test]
		public async Task AnArcFlagOtherThanZeroOrOneEndsThePath()
		{
			await Assert.That(Vertices("M10,10 L20,20 A5,5 0 2 1 30,30")).IsEqualTo(Vertices("M10,10 L20,20"));
			await Assert.That(Vertices("M10,10 L20,20 A5,5 0 0 -1 30,30")).IsEqualTo(Vertices("M10,10 L20,20"));
		}

		[Test]
		public async Task InvalidDataKeepsThePathDrawnSoFar()
		{
			await Assert.That(Vertices("M10 20 L30 40 L50 x 70 80")).IsEqualTo(Vertices("M10 20 L30 40"));
			await Assert.That(Vertices("M10 20 L30 40 # L 70 80")).IsEqualTo(Vertices("M10 20 L30 40"));
		}

		[Test]
		public async Task AMissingCoordinateEndsThePath()
		{
			await Assert.That(Vertices("M10 20 L30 40 L50")).IsEqualTo(Vertices("M10 20 L30 40"));
			await Assert.That(Vertices("M10 20 L30 40 50")).IsEqualTo(Vertices("M10 20 L30 40"));
		}

		[Test]
		public async Task APathThatDoesNotOpenWithAMovetoIsEmpty()
		{
			await Assert.That(Vertices("L20 30 L40 50")).IsEqualTo(Vertices(""));
			await Assert.That(Vertices("A5 5 0 0 1 30 30")).IsEqualTo(Vertices(""));
			await Assert.That(Vertices("  m1 1 l2 2")).IsEqualTo(Vertices("M1 1 L3 3"));
		}

		/// <summary>Exponents in either case, which the old reader dropped ("-2e1" read as 0).</summary>
		[Test]
		public async Task NumbersMayHaveExponents()
		{
			await Assert.That(Vertices("M 1.5 -2e1 L 3E+1 4.25e-1")).IsEqualTo(Vertices("M 1.5 -20 L 30 .425"));
		}

		/// <summary>SVG 1.1 section 8.3.7: T reflects only a previous quadratic's control point. An arc is stored as cubics, which fooled a check of the stored vertices.</summary>
		[Test]
		public async Task SmoothQuadraticAfterAnArcStartsAtTheCurrentPoint()
		{
			var storage = new VertexStorage("M0,0 a10,10 0 0 1 20,0 t20,0");
			var quad = storage.Vertices().Where(v => v.Command == FlagsAndCommand.Curve3).Select(v => v.Position).ToList();

			await Assert.That(quad.Count).IsEqualTo(2);
			await Assert.That(quad[0]).IsEqualTo(new Vector2(20, 0));
			await Assert.That(quad[1]).IsEqualTo(new Vector2(40, 0));
		}

		/// <summary>Repeated coordinate sets after q are further quadratics, each relative to the previous one's end.</summary>
		[Test]
		public async Task RepeatedRelativeQuadraticsChainFromEachEnd()
		{
			await Assert.That(Vertices("M0 0 q5 5 10 0 5 5 10 0")).IsEqualTo(Vertices("M0 0 Q5 5 10 0 Q15 5 20 0"));
		}

		/// <summary>
		/// Valid d-strings - glyphs, MatterCAD's icons, and every command - parse as they did before the
		/// grammar was tightened (count, sums of the positions and of the commands, taken from that parser). The arc icon's
		/// sums moved by under 2e-5 when arcs were made to end exactly on their end points.
		/// </summary>
		[Test]
		[Arguments("M581.29,257.57c0-47.11,31.91-83.68,78.4-83.68,50.82,0,78.07,38.56,78.4,94.7a1.05,1.05,0,0,1-1,1.07H624a1.06,1.06,0,0,0-1,1.06s0,.09,0,.13c3.46,23.78,17,38.48,40.43,38.48,16.13,0,25.68-7.17,30.43-18.88a1,1,0,0,1,1-.65h39.51a1,1,0,0,1,1.06,1,1,1,0,0,1,0,.24C729,318.71,704,341.86,663.71,341.86,611,341.86,581.29,305,581.29,257.57Z", 44, 28494.654006584486, 11667.499597733797, 240)]
		[Arguments("M797.92,443.43a360.33,360.33,0,1,0,28.25,139.86A357.92,357.92,0,0,0,797.92,443.43ZM662.66,586.82,594.25,705.31a41.07,41.07,0,0,1-35.47,20.48H422.54l-36.61,63.4a40.43,40.43,0,0,1-35.19,20.53,42.21,42.21,0,0,1-10.88-1.44,40.51,40.51,0,0,1-30.35-39.57v-197A41,41,0,0,1,315,551.22l71.5-123.84A41.09,41.09,0,0,1,422,406.9H558.78a41.07,41.07,0,0,1,35.47,20.48l68.41,118.49A41.07,41.07,0,0,1,662.66,586.82Z", 51, 23223.294047331845, 29047.446813179624, 330)]
		[Arguments("m8 2.748-.717-.737C5.6.281 2.514.878 1.4 3.053c-.523 1.023-.641 2.5.314 4.385.92 1.815 2.834 3.989 6.286 6.357 3.452-2.368 5.365-4.542 6.286-6.357.955-1.886.838-3.362.314-4.385C13.486.878 10.4.28 8.717 2.01zM8 15C-7.333 4.868 3.279-3.04 7.824 1.143q.09.083.176.171a3 3 0 0 1 .176-.17C12.72-3.042 23.333 4.867 8 15", 34, 256.08437683057963, 145.41321214945324, 197)]
		[Arguments("M267.96599,177.26875L276.43374,168.80101C276.43374,170.2123 276.43374,171.62359 276.43374,173.03488C280.02731,173.01874 282.82991,174.13254 286.53647,171.29154C290.08503,168.16609 288.97661,164.24968 289.13534,160.33327L284.90147,160.33327L293.36921,151.86553L301.83695,160.33327L297.60308,160.33327C297.60308,167.38972 298.67653,171.4841 293.23666,177.24919C286.80975,182.82626 283.014,181.02643 276.43374,181.50262L276.43374,185.73649L267.96599,177.26875L267.96599,177.26875z", 26, 6817.141809999999, 4106.750040000001, 156)]
		[Arguments("M 1.5 -20 L 30 .425 Q 5 6 7 8 T 9 10 S 11 12 13 14 H 15 V 16 h 1 v 1 Z m 2 2 l 1 1 z", 18, 164.5, 78.425, 196)]
		public async Task ValidPathDataParsesAsBefore(string dString, int count, double sumX, double sumY, int sumCommands)
		{
			var vertices = new VertexStorage(dString).Vertices().ToList();

			await Assert.That(vertices.Count).IsEqualTo(count);
			await Assert.That(vertices.Sum(v => v.Position.X)).IsEqualTo(sumX).Within(1e-6);
			await Assert.That(vertices.Sum(v => v.Position.Y)).IsEqualTo(sumY).Within(1e-6);
			await Assert.That(vertices.Sum(v => (int)v.Command)).IsEqualTo(sumCommands);
		}

		/// <summary>What <see cref="SvgParser.SvgDString"/> writes - MatterCAD's saved paths - reads back to the same vertices.</summary>
		[Test]
		public async Task WrittenPathDataRoundTrips()
		{
			var storage = new VertexStorage();
			storage.MoveTo(-1.25, 2);
			storage.LineTo(10, 0);
			storage.Curve3(12, 4, 14.5, -3);
			storage.Curve4(15, 1, 18, 2, 20.125, 0);
			storage.ClosePolygon();
			storage.MoveTo(100, 100);
			storage.LineTo(110, 100);
			storage.LineTo(105, 90);
			storage.ClosePolygon();

			string written = storage.SvgDString();
			var read = new VertexStorage(written);

			await Assert.That(read.SvgDString()).IsEqualTo(written);
			await Assert.That(read.Vertices().Count()).IsEqualTo(storage.Vertices().Count());
		}

		/// <summary>
		/// SVG numbers always use '.' as the decimal point. Written under a comma-decimal culture the d-string used to
		/// read "M -1,25 2,5", which is invalid SVG and reads back as different numbers.
		/// </summary>
		[Test]
		// The test swaps the thread culture, so it must not share a thread with anything else that formats numbers.
		[NotInParallel]
		public async Task WrittenPathDataIsCultureInvariant()
		{
			var originalCulture = Thread.CurrentThread.CurrentCulture;
			try
			{
				Thread.CurrentThread.CurrentCulture = new CultureInfo("de-DE");

				var storage = new VertexStorage();
				storage.MoveTo(-1.25, 2.5);
				storage.LineTo(10.75, 0.125);
				storage.Curve3(12.5, 4.5, 14.5, -3.5);
				storage.Curve4(15.5, 1.5, 18.5, 2.5, 20.125, 0.5);
				storage.ClosePolygon();

				string written = storage.SvgDString();
				var read = new VertexStorage(written);

				await Assert.That(Regex.IsMatch(written, @"\d,\d")).IsFalse();
				await Assert.That(written).Contains("-1.25");
				await Assert.That(read.Vertices().Select(v => v.Position)).IsEquivalentTo(storage.Vertices().Select(v => v.Position), CollectionOrdering.Matching);
			}
			finally
			{
				Thread.CurrentThread.CurrentCulture = originalCulture;
			}
		}

		/// <summary>A path must open with a moveto, so storage that starts with a LineTo is written as starting with one.</summary>
		[Test]
		public async Task StorageStartingWithLineToRoundTrips()
		{
			var storage = new VertexStorage();
			storage.LineTo(1, 2);
			storage.LineTo(10, 0);
			storage.LineTo(5, 7);

			string written = storage.SvgDString();
			var read = new VertexStorage(written);

			await Assert.That(written).StartsWith("M");
			await Assert.That(read.Vertices().Where(v => v.IsVertex).Select(v => v.Position))
				.IsEquivalentTo(storage.Vertices().Where(v => v.IsVertex).Select(v => v.Position), CollectionOrdering.Matching);
		}

		/// <summary>An arc ends exactly at its end point, even when its radii are too small and are scaled up to reach it.</summary>
		[Test]
		[Arguments("M10,10 a1,1 0 01 5,5", 15.0, 15.0)]
		[Arguments("M10,10 A5,3 30 1 0 17.3,-4.1", 17.3, -4.1)]
		[Arguments("M0.1,0.2 A7,7 0 0 1 3.3,9.7", 3.3, 9.7)]
		public async Task ArcEndsExactlyAtItsEndPoint(string dString, double endX, double endY)
		{
			var last = new VertexStorage(dString).Vertices().Last(v => v.IsVertex);

			await Assert.That(last.Position.X).IsEqualTo(endX);
			await Assert.That(last.Position.Y).IsEqualTo(endY);
		}
	}
}
