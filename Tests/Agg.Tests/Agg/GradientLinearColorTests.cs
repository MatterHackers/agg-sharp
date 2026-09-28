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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	public class GradientLinearColorTests
	{
		/// <summary>
		/// C++ gradient_linear_color&lt;rgba8&gt;[v] is c1.gradient(c2, v * (1 / (size - 1))): the step rounded to 0..255,
		/// then rgba8's rounded integer lerp per channel. The expected colors are that arithmetic done by hand for
		/// raster_text's red to (0, 128, 0).
		/// </summary>
		[Test]
		public async Task StepsMatchCppRgba8Gradient()
		{
			var colors = new gradient_linear_color(new Color(255, 0, 0), new Color(0, 128, 0));

			await Assert.That(colors[0]).IsEqualTo(new Color(255, 0, 0));
			await Assert.That(colors[100]).IsEqualTo(new Color(155, 50, 0));
			await Assert.That(colors[173]).IsEqualTo(new Color(82, 87, 0));
			await Assert.That(colors[200]).IsEqualTo(new Color(55, 100, 0));
			await Assert.That(colors[255]).IsEqualTo(new Color(0, 128, 0));
		}
	}
}
