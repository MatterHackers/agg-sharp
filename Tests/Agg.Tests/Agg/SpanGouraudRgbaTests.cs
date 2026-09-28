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

using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class SpanGouraudRgbaTests
	{
		/// <summary>
		/// A row's colours depend only on that row. The port keeps its edge interpolators in structs, and
		/// used to copy the lower sub-triangle's edge before recalculating it for the row, so a row below the
		/// middle vertex came out with whatever row was generated before it (or, first, an all-zero edge).
		/// </summary>
		[Test]
		public async Task RowBelowTheMiddleVertexDoesNotDependOnTheRowBeforeIt()
		{
			const int row = 5;
			const int length = 20;

			var direct = new Color[length];
			NewTriangle().generate(direct, 0, 0, row, length);

			var afterOthers = new Color[length];
			span_gouraud_rgba swept = NewTriangle();
			var scratch = new Color[length];
			for (int y = 0; y < row; y++)
			{
				swept.generate(scratch, 0, 0, y, length);
			}

			swept.generate(afterOthers, 0, 0, row, length);

			await Assert.That(direct).IsEquivalentTo(afterOthers);

			// The row starts on the red-to-green edge: red fading, green rising, no black from a zeroed edge.
			await Assert.That((int)direct[1].red).IsGreaterThan(100);
		}

		// Red at the bottom corner, green at the middle vertex (y 10, the split row), blue at the top.
		private static span_gouraud_rgba NewTriangle()
		{
			var triangle = new span_gouraud_rgba(
				new Color(255, 0, 0),
				new Color(0, 255, 0),
				new Color(0, 0, 255),
				0,
				0,
				20,
				10,
				5,
				20);
			triangle.prepare();
			return triangle;
		}
	}
}
