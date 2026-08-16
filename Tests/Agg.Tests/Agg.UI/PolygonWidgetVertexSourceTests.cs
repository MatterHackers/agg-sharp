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
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The GPU 2D path (Graphics2DGpu.DrawAAShape) keys its tessellation cache on
	/// IVertexSource.GetLongHashCode, which enumerates Vertices(). polygon_ctrl_impl
	/// only implemented the legacy Rewind/Vertex pair, so drawing a polygon editing
	/// widget (the blur demo) threw NotImplementedException on the GPU window.
	/// </summary>
	public class PolygonWidgetVertexSourceTests
	{
		[Test]
		public async Task GetLongHashCodeEnumeratesVertices()
		{
			var polygon = MakeTriangleWidget();

			var hash = polygon.GetLongHashCode();

			await Assert.That(hash).IsNotEqualTo(0UL);

			// Same geometry must hash the same twice (the cache key depends on it)
			await Assert.That(polygon.GetLongHashCode()).IsEqualTo(hash);

			// Moving a point must change the hash or the GPU cache would reuse stale triangles
			polygon.SetXN(0, polygon.GetXN(0) + 10);
			await Assert.That(polygon.GetLongHashCode()).IsNotEqualTo(hash);
		}

		[Test]
		public async Task VerticesMatchesLegacyVertexIteration()
		{
			var polygon = MakeTriangleWidget();

			var fromEnumeration = polygon.Vertices().ToList();
			var fromLegacyIteration = LegacyIterate(polygon);

			await Assert.That(fromEnumeration.Count).IsEqualTo(fromLegacyIteration.Count);
			await Assert.That(fromEnumeration.Count).IsGreaterThan(3);

			for (int i = 0; i < fromEnumeration.Count; i++)
			{
				await Assert.That(fromEnumeration[i].Command).IsEqualTo(fromLegacyIteration[i].Command);
				await Assert.That(fromEnumeration[i].Position.X).IsEqualTo(fromLegacyIteration[i].Position.X);
				await Assert.That(fromEnumeration[i].Position.Y).IsEqualTo(fromLegacyIteration[i].Position.Y);
			}
		}

		private static PolygonEditWidget MakeTriangleWidget()
		{
			var polygon = new PolygonEditWidget(3, 5);
			polygon.SetXN(0, 10); polygon.SetYN(0, 10);
			polygon.SetXN(1, 90); polygon.SetYN(1, 20);
			polygon.SetXN(2, 50); polygon.SetYN(2, 80);

			return polygon;
		}

		private static List<VertexData> LegacyIterate(IVertexSource source)
		{
			var vertices = new List<VertexData>();

			source.Rewind(0);
			FlagsAndCommand command;
			do
			{
				command = source.Vertex(out double x, out double y);
				vertices.Add(new VertexData(command, new VectorMath.Vector2(x, y)));
			} while (command != FlagsAndCommand.Stop);

			return vertices;
		}
	}
}
