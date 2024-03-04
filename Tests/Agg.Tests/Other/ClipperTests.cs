/*
Copyright (c) 2023, Lars Brubaker
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

using System;
using System.Collections.Generic;
using ClipperLib;
using MatterHackers.VectorMath;
using Xunit;
using System.IO;

namespace MatterHackers.Agg.Tests
{
	using Polygons = List<List<IntPoint>>;
	using Polygon = List<IntPoint>;

	public class ClipperTests
	{
		[StaFact]
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
				Assert.True(Math.Abs(length - distToCenter) <= 3);
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