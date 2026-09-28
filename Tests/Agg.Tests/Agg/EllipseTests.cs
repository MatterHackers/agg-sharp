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
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.VertexSource;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Ellipse has to hand out the vertices C++ agg_ellipse.h does, bit for bit: every C++ AGG golden with a
	/// circle in it (slider pointers, cbox, rbox, the demos' handles) depends on it.
	/// </summary>
	public class EllipseTests
	{
		/// <summary>
		/// These radii make calc_num_steps' 2pi / da land exactly on n + 0.5 with n even (6.5, 8.5, 54.5), where
		/// C++ uround goes up and Math.Round's half-to-even would go down.
		/// </summary>
		[Test]
		[Arguments(0.9662839432439579, 7)]
		[Arguments(1.7260902832628522, 9)]
		[Arguments(75.13314822347097, 55)]
		public async Task StepCountRoundsHalfUpAsCppDoes(double radius, int expectedSteps)
		{
			await Assert.That(new Ellipse(0, 0, radius, radius).NumSteps).IsEqualTo(expectedSteps);
		}

		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task EachVertexAngleIsItsStepOverTheStepCountTimesTwoPi(bool cw)
		{
			// 100 steps accumulate enough floating point drift to differ from C++ if the angle is summed.
			var ellipse = new Ellipse(12.25, 7.5, 40.3, 17.9, 100, cw);
			List<VertexData> vertices = ellipse.Vertices().ToList();

			await Assert.That(vertices.Count).IsEqualTo(102);
			for (int step = 0; step < 100; step++)
			{
				// agg_ellipse.h vertex(): angle = double(m_step) / double(m_num) * 2.0 * pi.
				double angle = (double)step / 100.0 * 2.0 * Math.PI;
				if (cw)
				{
					angle = (2.0 * Math.PI) - angle;
				}

				await Assert.That(vertices[step].Position.X).IsEqualTo(12.25 + (Math.Cos(angle) * 40.3));
				await Assert.That(vertices[step].Position.Y).IsEqualTo(7.5 + (Math.Sin(angle) * 17.9));
				await Assert.That(vertices[step].Command).IsEqualTo(step == 0 ? FlagsAndCommand.MoveTo : FlagsAndCommand.LineTo);
			}

			await Assert.That(vertices[100].Command).IsEqualTo(FlagsAndCommand.EndPoly | FlagsAndCommand.FlagClose | FlagsAndCommand.FlagCCW);
			await Assert.That(vertices[101].Command).IsEqualTo(FlagsAndCommand.Stop);
		}
	}
}
