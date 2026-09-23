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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using ClipperLib;
using MatterHackers.Agg.VertexSource;
using MatterHackers.DataConverters2D;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	using Polygons = List<List<IntPoint>>;

	/// <summary>
	/// agg's clipper offsets cut their round joins by the size of what they offset (<see cref="CurveTolerance"/>):
	/// a room-size shape scaled up from a printer-bed one gets the bed one's points, and a desk-size shape is
	/// offset point for point as it always was. Every desk-size checksum here was measured on the commit before
	/// the tolerance scaled.
	/// </summary>
	/// <remarks>
	/// The shape is a square FRAME: a round join only adds points where the offset goes around a corner, and
	/// shrinking the frame goes around the hole's four corners, so every extra point is arc and nothing else.
	/// </remarks>
	public class RoomScaleOffsetToleranceTests
	{
		/// <summary>Twenty feet, in mm.</summary>
		private const double TwentyFeet = 20 * 304.8;

		/// <summary>The printer bed <see cref="CurveTolerance.DeskSize"/> anchors on, in mm.</summary>
		private const double Bed = 300;

		/// <summary>
		/// An extrude's bevel rings: a 20 ft frame, bevelled in proportion, builds no more of a mesh than the
		/// same frame 300 mm across.
		/// </summary>
		[Test]
		public async Task ARoomSizeBevelledExtrudeIsNoFinerThanABedSizeOne()
		{
			var atBed = BevelledExtrude(Bed).Vertices.Count;
			var atRoom = BevelledExtrude(TwentyFeet).Vertices.Count;

			System.Console.WriteLine($"Bevelled frame 300 mm: {atBed} vertices; 20 ft: {atRoom}");

			await Assert.That((double)atRoom).IsLessThanOrEqualTo(atBed * 1.05)
				.Because($"a 20 ft bevelled frame built {atRoom} vertices where a 300 mm one builds {atBed}");
		}

		/// <summary>A 50 mm bevelled extrude is the mesh it always was.</summary>
		[Test]
		public async Task ADeskSizeBevelledExtrudeIsBitIdentical()
		{
			var mesh = BevelledExtrude(50);
			var checksum = Checksum(mesh);
			System.Console.WriteLine($"Bevelled frame 50 mm: {mesh.Vertices.Count} vertices, checksum {checksum}");

			await Assert.That(checksum).IsEqualTo(DeskBevelledExtrudeChecksum);
		}

		/// <summary>The three offset helpers: a 20 ft frame offset in proportion gets the 300 mm frame's points.</summary>
		[Test]
		public async Task ARoomSizeOffsetIsNoFinerThanABedSizeOne()
		{
			var counts = Helpers.Select(helper => (helper.name,
				atBed: helper.offset(Bed).Sum(p => p.Count),
				atRoom: helper.offset(TwentyFeet).Sum(p => p.Count))).ToList();

			foreach (var (name, atBed, atRoom) in counts)
			{
				System.Console.WriteLine($"{name}: 300 mm {atBed} points; 20 ft {atRoom}");
			}

			foreach (var (name, atBed, atRoom) in counts)
			{
				await Assert.That((double)atRoom).IsLessThanOrEqualTo(atBed * 1.05)
					.Because($"{name} of a 20 ft frame built {atRoom} points where 300 mm builds {atBed}");
			}
		}

		/// <summary>The three offset helpers offset a 50 mm frame point for point as they always did.</summary>
		[Test]
		public async Task ADeskSizeOffsetIsBitIdentical()
		{
			var expected = new Dictionary<string, long>
			{
				["Polygons.Offset"] = DeskPolygonsOffsetChecksum,
				["Polygon.Offset"] = DeskPolygonOffsetChecksum,
				["IVertexSource.Offset"] = DeskVertexSourceOffsetChecksum,
			};

			var checksums = Helpers.Select(helper => (helper.name, checksum: Checksum(helper.offset(50)))).ToList();

			foreach (var (name, checksum) in checksums)
			{
				System.Console.WriteLine($"{name} 50 mm: checksum {checksum}");
			}

			foreach (var (name, checksum) in checksums)
			{
				await Assert.That(checksum).IsEqualTo(expected[name]).Because($"{name} of a 50 mm frame moved");
			}
		}

		private const long DeskBevelledExtrudeChecksum = -600885690986330816;

		private const long DeskPolygonsOffsetChecksum = -726667921790164207;

		private const long DeskPolygonOffsetChecksum = -1803460346122481647;

		private const long DeskVertexSourceOffsetChecksum = -726667921790164207;

		/// <summary>Each helper, offsetting a frame <c>size</c> mm across by a distance in proportion to it.</summary>
		private static IEnumerable<(string name, System.Func<double, Polygons> offset)> Helpers =>
			new (string, System.Func<double, Polygons>)[]
			{
				("Polygons.Offset", size => Frame(size).CreatePolygons().Offset(-size / 20 * 1000, JoinType.jtRound)),

				// the single-polygon helper grows (it has no hole), so its round joins are the outer corners
				("Polygon.Offset", size => Frame(size).CreatePolygons()[0].Offset(size / 20 * 1000)),
				("IVertexSource.Offset", size => Frame(size).Offset(-size / 20, JoinType.jtRound).CreatePolygons()),
			};

		/// <summary>A frame <paramref name="size"/> mm across bevelled over a tenth of its size.</summary>
		private static Mesh BevelledExtrude(double size)
		{
			var radius = size / 10;
			var height = size / 5;
			var bevel = new List<(double height, double insetAmount)>
			{
				(height - radius, -radius * 0.1),
				(height - radius * 0.5, -radius * 0.5),
				(height, -radius),
			};

			return Frame(size).Extrude(height, bevel, JoinType.jtRound);
		}

		/// <summary>A square <paramref name="size"/> across with a square hole half its size.</summary>
		private static VertexStorage Frame(double size)
		{
			var outer = size / 2;
			var inner = size / 4;
			var frame = new VertexStorage();
			frame.MoveTo(-outer, -outer);
			frame.LineTo(outer, -outer);
			frame.LineTo(outer, outer);
			frame.LineTo(-outer, outer);
			frame.ClosePolygon();
			frame.MoveTo(-inner, -inner);
			frame.LineTo(-inner, inner);
			frame.LineTo(inner, inner);
			frame.LineTo(inner, -inner);
			frame.ClosePolygon();

			return frame;
		}

		/// <summary>
		/// The exact bits of every vertex position, in sorted order, plus the face count. Sorted because
		/// <c>Mesh.GetLongHashCode</c> came out different on two runs of the same build: the vertex ORDER
		/// is not stable, and the positions are what a stored mesh has to keep.
		/// </summary>
		private static long Checksum(Mesh mesh)
		{
			long checksum = mesh.Faces.Count;
			foreach (var vertex in mesh.Vertices.OrderBy(v => v.X).ThenBy(v => v.Y).ThenBy(v => v.Z))
			{
				checksum = unchecked((checksum * 31) + System.BitConverter.DoubleToInt64Bits(vertex.X));
				checksum = unchecked((checksum * 31) + System.BitConverter.DoubleToInt64Bits(vertex.Y));
				checksum = unchecked((checksum * 31) + System.BitConverter.DoubleToInt64Bits(vertex.Z));
			}

			return checksum;
		}

		private static long Checksum(Polygons polygons)
		{
			long checksum = 17;
			foreach (var point in polygons.SelectMany(polygon => polygon))
			{
				checksum = unchecked(((checksum * 31) + point.X) * 31 + point.Y);
			}

			return checksum;
		}
	}
}
