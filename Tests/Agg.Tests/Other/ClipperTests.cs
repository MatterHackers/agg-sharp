using System;
using System.Collections.Generic;
using ClipperLib;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;
using NUnit.Framework;
using MatterHackers.DataConverters2D;
using System.IO;

namespace MatterHackers.Agg.Tests
{
	using Polygons = List<List<IntPoint>>;
	using Polygon = List<IntPoint>;

	public class ClipperTests
	{
		[Test]
		public void CleanPolygonsTest()
		{
			var polygon = new Polygon();

			var length = 500;

			for (int i = 0; i < 360; i++)
			{
				var angle = MathHelper.DegreesToRadians(i);
				polygon.Add(new IntPoint(Math.Cos(angle) * length, Math.Sin(angle) * length));
			}

			var cleanedPolygon = Clipper.CleanPolygon(polygon, 2);

			var originalPolygons = new Polygons() { polygon };
			var cleanPolygons = new Polygons() { cleanedPolygon };

			//this.WriteSvg("before.svg", originalPolygons.CreateVertexStorage(1).GetSvgDString());
			//this.WriteSvg("after.svg", cleanPolygons.CreateVertexStorage(1).GetSvgDString());

			// Ensure mid-point between points is within threshold
			for (int i = 0; i < cleanedPolygon.Count - 1; i++)
			{
				var centerPoint = (cleanedPolygon[i + 1] + cleanedPolygon[i]) / 2;
				var distToCenter = centerPoint.Length();
				Assert.AreEqual(length, distToCenter, 3);
			}
		}

		private void WriteSvg(string filePath, string dstring)
		{
			filePath = Path.Combine(@"c:\temp\", filePath);

			File.WriteAllText(filePath, $@"
<svg xmlns='http://www.w3.org/2000/svg' version='1.1'>
	<path d=""{dstring}"" fill=""none"" stroke=""blue"" stroke-width=""5""/>
</svg>");
		}
	}



}