// Copyright (c) 2026, Lars Brubaker
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using g3;
using MatterHackers.Agg.Tests;
using MatterHackers.PolygonMesh.Processors;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Other
{
	/// <summary>
	/// Mesh readers and writers name the invariant culture at every number instead of switching the thread's
	/// culture around the call: a swap leaks into the caller (the STL parse left the caller's thread invariant
	/// for good) and cannot follow an async continuation onto another thread. Each test runs under de-DE,
	/// where culture-sensitive code writes 1.5 as "1,5".
	/// </summary>
	public class MeshFileCultureTests
	{
		private static readonly CultureInfo German = CultureInfo.GetCultureInfo("de-DE");

		private const string AsciiStl = "solid t\nfacet normal 0 0 1\nouter loop\nvertex 1.5 0 0\nvertex 0 2.5 0\nvertex 0 0 3.5\nendloop\nendfacet\nendsolid t\n";

		private static List<WriteMesh> HalfUnitTriangle()
		{
			var mesh = new DMesh3();
			mesh.AppendVertex(new Vector3d(1.5, 0, 0));
			mesh.AppendVertex(new Vector3d(0, 2.5, 0));
			mesh.AppendVertex(new Vector3d(0, 0, 3.5));
			mesh.AppendTriangle(0, 1, 2);
			return new List<WriteMesh> { new WriteMesh(mesh) };
		}

		[Test]
		[NotInParallel]
		public async Task ReadingAnStlLeavesTheCallersCultureAlone()
		{
			using (new CultureScope(German))
			{
				var mesh = StlProcessing.ParseFileContents(new MemoryStream(Encoding.ASCII.GetBytes(AsciiStl)), CancellationToken.None, null);

				await Assert.That(CultureInfo.CurrentCulture.Name).IsEqualTo("de-DE");
				await Assert.That(mesh.GetAxisAlignedBoundingBox().XSize).IsEqualTo(1.5).Within(1e-6);
			}
		}

		[Test]
		[NotInParallel]
		[Arguments("obj")]
		[Arguments("off")]
		[Arguments("stl")]
		public async Task A3Writer_WritesPointDecimalsAndItsReaderReadsThemBack(string format)
		{
			using (new CultureScope(German))
			{
				var text = new StringWriter();
				IMeshWriter writer = format switch { "obj" => new OBJWriter(), "off" => new OFFWriter(), _ => new STLWriter() };
				var written = writer.Write(text, HalfUnitTriangle(), WriteOptions.Defaults);
				await Assert.That(written.code).IsEqualTo(IOCode.Ok);
				await Assert.That(text.ToString()).Contains("1.5");
				await Assert.That(text.ToString()).DoesNotContain("1,5");

				if (format == "off")
				{
					// OFFReader is internal to geometry3Sharp; its parse sites are checked by the analyzer
					return;
				}

				var builder = new DMesh3Builder();
				IMeshReader reader = format == "obj" ? new OBJReader() : new STLReader();
				var read = reader.Read(new StringReader(text.ToString()), ReadOptions.Defaults, builder);
				await Assert.That(read.code).IsEqualTo(IOCode.Ok);
				var bounds = builder.Meshes[0].CachedBounds;
				await Assert.That(bounds.Max.x).IsEqualTo(1.5).Within(1e-6);
				await Assert.That(bounds.Max.z).IsEqualTo(3.5).Within(1e-6);
			}
		}

		[Test]
		[NotInParallel]
		public async Task AnSvgWrittenUnderGermanHasPointDecimals()
		{
			var path = Path.Combine(Path.GetTempPath(), "MeshFileCultureTests-" + Guid.NewGuid().ToString("N") + ".svg");
			try
			{
				using (new CultureScope(German))
				{
					var svg = new SVGWriter();
					svg.AddPolygon(new Polygon2d(new[] { new Vector2d(1.5, 2.5), new Vector2d(10.5, 2.5), new Vector2d(1.5, 12.5) }));
					svg.Write(path);

					await Assert.That(CultureInfo.CurrentCulture.Name).IsEqualTo("de-DE");
				}

				var text = File.ReadAllText(path);
				await Assert.That(text).Contains("1.5,");
				await Assert.That(text).DoesNotContain("1,5");
			}
			finally
			{
				File.Delete(path);
			}
		}
	}
}
