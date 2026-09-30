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
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using System.Xml;
using ClipperLib;
using Gaming.Game;
using MatterHackers.Agg;
using MatterHackers.Agg.Tests;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.PolygonMesh;
using MatterHackers.PolygonMesh.Processors;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Other
{
	/// <summary>
	/// Machine-readable numbers - type converter strings, vertex files, packed polygons, G-code style
	/// text, ASCII STL and the number edit's '.'-only text - are read and written the same way on a
	/// comma-decimal machine. Each test runs under de-DE, where culture-sensitive code reads "1.5" as 15
	/// and writes 1.5 as "1,5".
	/// </summary>
	/// <remarks>
	/// CurrentCulture is process-wide for any test that does not set its own, so every test is a keyless
	/// <c>[NotInParallel]</c> and runs under a <see cref="CultureScope"/>.
	/// </remarks>
	public class InvariantCultureTests
	{
		private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

		private static async Task UnderGerman(Func<Task> test)
		{
			using (new CultureScope(German))
			{
				await test();
			}
		}

		[Test]
		[NotInParallel]
		public Task BorderAndRadiusConvertersReadPointDecimals() => UnderGerman(async () =>
		{
			var border = (BorderDouble)TypeDescriptor.GetConverter(typeof(BorderDouble)).ConvertFrom("1.5,2.5");
			await Assert.That(border.Left).IsEqualTo(1.5);
			await Assert.That(border.Bottom).IsEqualTo(2.5);

			var radius = (RadiusCorners)TypeDescriptor.GetConverter(typeof(RadiusCorners)).ConvertFrom("1.5");
			await Assert.That(radius.NE).IsEqualTo(1.5);
		});

		[Test]
		[NotInParallel]
		public Task VectorParsingReadsPointDecimals() => UnderGerman(async () =>
		{
			await Assert.That(Vector2.Parse("1.5,2.5")).IsEqualTo(new Vector2(1.5, 2.5));
			await Assert.That(Vector3.Parse("1.5,2.5,3.5")).IsEqualTo(new Vector3(1.5, 2.5, 3.5));
			await Assert.That(Vector4.Parse("1.5,2.5,3.5,4.5")).IsEqualTo(new Vector4(1.5, 2.5, 3.5, 4.5));

			var converted = (Vector2)TypeDescriptor.GetConverter(typeof(Vector2)).ConvertFrom("(1.5,2.5)");
			await Assert.That(converted).IsEqualTo(new Vector2(1.5, 2.5));
		});

		[Test]
		[NotInParallel]
		public Task VertexSourceFilesRoundTrip() => UnderGerman(async () =>
		{
			string path = Path.Combine(Path.GetTempPath(), "agg-vertexsource-" + Guid.NewGuid().ToString("N") + ".txt");
			try
			{
				var source = new VertexStorage();
				source.MoveTo(1.5, 2.5);
				source.LineTo(3.25, 4.75);

				VertexSourceIO.Save(source, path);
				var loaded = new VertexStorage();
				VertexSourceIO.Load(loaded, path);

				await Assert.That(loaded.Count).IsEqualTo(2);
				loaded.Vertex(1, out double x, out double y);
				await Assert.That(x).IsEqualTo(3.25);
				await Assert.That(y).IsEqualTo(4.75);
			}
			finally
			{
				File.Delete(path);
			}
		});

		[Test]
		[NotInParallel]
		public Task ReplaceNumberAfterWritesPointDecimals() => UnderGerman(async () =>
		{
			await Assert.That(Util.ReplaceNumberAfter('X', "G1 X0 Y2", 1.5)).IsEqualTo("G1 X1.5 Y2");
		});

		[Test]
		[NotInParallel]
		public Task PackedPolygonStringsReadPointDecimals() => UnderGerman(async () =>
		{
			var polygons = PolygonsExtensions.CreateFromString("x:1.5,y:2.5,x:10.5,y:0.5,", 10);
			await Assert.That(polygons.Count).IsEqualTo(1);
			await Assert.That(polygons[0][0].X).IsEqualTo(15L);
			await Assert.That(polygons[0][0].Y).IsEqualTo(25L);
		});

		[Test]
		[NotInParallel]
		public Task AsciiStlIsWrittenWithPointDecimals() => UnderGerman(async () =>
		{
			var mesh = new Mesh();
			mesh.CreateFace(new Vector3(0, 0, 1.5), new Vector3(10, 0, 1.5), new Vector3(5, 5, 1.5));

			using var stream = new MemoryStream();
			StlProcessing.Save(mesh, stream, CancellationToken.None, new MeshOutputSettings(MeshOutputSettings.OutputType.Ascii), leaveStreamOpen: true);
			string text = Encoding.Default.GetString(stream.ToArray());

			await Assert.That(text).Contains("vertex 0.000000 0.000000 1.500000");

			stream.Position = 0;
			var loaded = StlProcessing.Load(stream, CancellationToken.None);
			await Assert.That(loaded.Vertices[0].Z).IsEqualTo(1.5f);
		});

		[Test]
		[NotInParallel]
		public Task NumberEditShowsTheUsersDecimalAndReadsBoth() => UnderGerman(async () =>
		{
			// The edit is what the user reads, so it shows their decimal comma; it still reads a typed '.'.
			var edit = new InternalNumberEdit(1.5, 12, true, true, -100, 100, 1, 0);
			await Assert.That(edit.Text).IsEqualTo("1,5");
			await Assert.That(edit.Value).IsEqualTo(1.5);

			edit.Text = "2.25";
			await Assert.That(edit.Value).IsEqualTo(2.25);

			edit.Value = 3.5;
			await Assert.That(edit.Text).IsEqualTo("3,5");
			await Assert.That(DisplayNumber.Format(1234.5)).IsEqualTo("1234,5");
		});

		/// <summary>
		/// A thousands separator cannot be typed: "1.000" is one under de-DE, and the ',' that would make it
		/// "1.000,5" is refused, as the ',' after "1" is under en-US.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments("de-DE", "1.000", ',')]
		[Arguments("en-US", "1", ',')]
		public async Task NumberEditRefusesAGroupingSeparatorAsItIsTyped(string culture, string start, char grouping)
		{
			using (new CultureScope(CultureInfo.GetCultureInfo(culture)))
			{
				var edit = new InternalNumberEdit(0, 12, true, true, -10000, 10000, 1, 0);
				edit.Text = start;
				edit.CharIndexToInsertBefore = start.Length;
				edit.OnKeyPress(new KeyPressEventArgs(grouping));
				await Assert.That(edit.Text).IsEqualTo(start);
			}
		}

		[Test]
		[Arguments("de-DE", "1.000", true)]
		[Arguments("de-DE", "12.500", true)]
		[Arguments("de-DE", "1.000.000", true)]
		[Arguments("de-DE", "-1.000", true)]
		[Arguments("de-DE", "0.500", false)]
		[Arguments("de-DE", "-0.250", false)]
		[Arguments("de-DE", "1.5", false)]
		[Arguments("de-DE", "2.25", false)]
		[Arguments("de-DE", "1.0000", false)]
		[Arguments("de-DE", "1234.500", false)]
		[Arguments("en-US", "1,000", true)]
		[Arguments("en-US", "1,000,000", true)]
		[Arguments("en-US", "1.000", false)]
		[Arguments("en-US", "12,5", false)]
		public async Task AThousandsGroupedWholeNumberIsAmbiguous(string culture, string text, bool ambiguous)
		{
			await Assert.That(DisplayNumber.IsAmbiguousGrouping(text, CultureInfo.GetCultureInfo(culture))).IsEqualTo(ambiguous);
		}

		/// <summary>
		/// Committing text the edit cannot read - a German "1.000" is a thousand or one - keeps the last value it
		/// held rather than reading as 0, so every caller that takes Value at EditComplete gets a real number.
		/// </summary>
		[Test]
		[NotInParallel]
		[Arguments("de-DE", "1.000", "2,5")]
		[Arguments("en-US", "1,000", "2.5")]
		[Arguments("en-US", ".", "2.5")]
		public async Task CommittingUnreadableTextKeepsTheLastValue(string culture, string typed, string shown)
		{
			using (new CultureScope(CultureInfo.GetCultureInfo(culture)))
			{
				var edit = new InternalNumberEdit(2.5, 12, true, true, -10000, 10000, 1, 0);
				double committed = double.NaN;
				edit.EditComplete += (s, e) => committed = edit.Value;

				edit.Text = typed;
				edit.OnEditComplete(EventArgs.Empty);

				await Assert.That(committed).IsEqualTo(2.5);
				await Assert.That(edit.Text).IsEqualTo(shown);

				// a readable commit becomes the value kept next time
				edit.Text = "4";
				edit.OnEditComplete(EventArgs.Empty);
				edit.Text = typed;
				edit.OnEditComplete(EventArgs.Empty);
				await Assert.That(committed).IsEqualTo(4);
			}
		}

		[Test]
		[NotInParallel]
		public Task NumberEditReadsTheUsersCommaDecimal() => UnderGerman(async () =>
		{
			// A German keyboard writes "2,5"; an invariant read with group separators made that 25.
			var edit = new InternalNumberEdit(0, 12, true, true, -100, 100, 1, 0);
			edit.Text = "2,5";
			await Assert.That(edit.Value).IsEqualTo(2.5);

			// and the comma can be typed at all
			edit.Text = "3";
			edit.CharIndexToInsertBefore = 1;
			var comma = new KeyPressEventArgs(',');
			edit.OnKeyPress(comma);
			edit.OnKeyPress(new KeyPressEventArgs('5'));
			await Assert.That(edit.Text).IsEqualTo("3,5");
			await Assert.That(edit.Value).IsEqualTo(3.5);
		});

		[Test]
		[NotInParallel]
		public async Task NumberEditDoesNotReadACommaAsAGroupSeparator()
		{
			var previous = CultureInfo.CurrentCulture;
			try
			{
				CultureInfo.CurrentCulture = new CultureInfo("en-US");
				// ',' is not a decimal here and cannot be typed; text that holds one is not a number
				var edit = new InternalNumberEdit(0, 12, true, true, -100, 100, 1, 0);
				edit.Text = "2,5";
				await Assert.That(edit.Value).IsEqualTo(0);
				edit.Text = "2.5";
				await Assert.That(edit.Value).IsEqualTo(2.5);
			}
			finally
			{
				CultureInfo.CurrentCulture = previous;
			}
		}

		[Test]
		[NotInParallel]
		public Task GameDataVector2RoundTrips() => UnderGerman(async () =>
		{
			// XmlWriter always writes numbers invariantly, so the reader must read them that way too.
			var attribute = new GameDataVector2DAttribute("Position");
			var text = new StringBuilder();
			using (var writer = XmlWriter.Create(text, new XmlWriterSettings { OmitXmlDeclaration = true }))
			{
				writer.WriteStartElement("Position");
				GameDataAttribute.WriteTypeAttributes(writer, new Vector2());
				attribute.WriteField(writer, new Vector2(1.5, 2.5));
				writer.WriteEndElement();
			}

			await Assert.That(text.ToString()).Contains("x=\"1.5\"");

			using var reader = XmlReader.Create(new StringReader(text.ToString()));
			reader.MoveToContent();
			await Assert.That((Vector2)attribute.ReadField(reader)).IsEqualTo(new Vector2(1.5, 2.5));
		});
	}
}
