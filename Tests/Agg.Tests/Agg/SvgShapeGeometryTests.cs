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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Svg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.Agg
{
	/// <summary>
	/// The outlines SvgShapes builds for curved shapes, which match usvg's: kurbo's arc-to-cubic at a tolerance of
	/// 0.1 user units - one cubic per quarter up to a radius of about 367, more beyond - and an arc to its own start
	/// is a line.
	/// </summary>
	public class SvgShapeGeometryTests
	{
		private static int Cubics(VertexStorage path) => path.Vertices().Count(v => v.Command == FlagsAndCommand.Curve4) / 3;

		[Test]
		public async Task AnArcIsOneCubicPerQuarterAtAnOrdinaryRadius()
		{
			VertexStorage circle = SvgShapes.Ellipse(0, 0, 100, 100);
			await Assert.That(Cubics(circle)).IsEqualTo(4);
			VertexData control = circle.Vertices().First(v => v.Command == FlagsAndCommand.Curve4);
			await Assert.That(control.Position.X).IsEqualTo(100).Within(1e-9);
			await Assert.That(control.Position.Y).IsEqualTo(100 * 4.0 / 3 * Math.Tan(Math.PI / 8)).Within(1e-9);
		}

		[Test]
		public async Task ALargeArcIsCutIntoMoreCubicsAsKurboCutsIt()
		{
			// kurbo: max(1.1163 r / 0.1, ...)^(1/6) subdivisions per ellipse - 4.73 at r = 1000, so two per quarter.
			await Assert.That(Cubics(SvgShapes.Ellipse(0, 0, 1000, 1000))).IsEqualTo(8);
			await Assert.That(Cubics(SvgShapes.Rect(0, 0, 2000, 2000, 1000, 1000))).IsEqualTo(8);
			await Assert.That(Cubics(SvgShapes.Path("M 1000 0 A 1000 1000 0 0 1 0 1000"))).IsEqualTo(2);
			await Assert.That(Cubics(SvgShapes.Path("M 100 0 A 100 100 0 0 1 0 100"))).IsEqualTo(1);
		}

		[Test]
		public async Task AnArcToItsOwnStartIsALine()
		{
			VertexData[] vertices = SvgShapes.Path("M 10 10 A 5 5 0 0 1 10 10").Vertices().TakeWhile(v => !v.IsStop).ToArray();
			await Assert.That(vertices.Length).IsEqualTo(2);
			await Assert.That(vertices[1].IsLineTo).IsTrue();
		}
	}
}
