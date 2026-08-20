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

using MatterHackers.Csg.Operations;
using MatterHackers.Csg.Processors;
using MatterHackers.Csg.Solids;
using MatterHackers.Csg.Transform;
using System.Globalization;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Csg
{
	public class OpenSCadOutputTests
	{
		/// <summary>
		/// OpenSCAD only understands '.' as a decimal separator. On a comma-decimal culture
		/// (de-DE and friends) culture-sensitive number formatting used to emit "r1=2,5",
		/// which OpenSCAD parses as two arguments and rejects. See agg-sharp issue #327.
		/// </summary>
		[Test]
		// The test swaps the thread culture around awaits, so it must not share a thread with
		// anything else that formats numbers.
		[NotInParallel]
		public async Task ScadNumbersUseInvariantDecimalSeparator()
		{
			var originalCulture = Thread.CurrentThread.CurrentCulture;
			var originalUiCulture = Thread.CurrentThread.CurrentUICulture;

			try
			{
				var commaDecimalCulture = new CultureInfo("de-DE");
				Thread.CurrentThread.CurrentCulture = commaDecimalCulture;
				Thread.CurrentThread.CurrentUICulture = commaDecimalCulture;

				CsgObject box = new Box(2.5, 1.25, 3.5);
				CsgObject cylinder = new Cylinder(2.5, 4.75, 30);
				CsgObject sphere = new Sphere(1.75);
				CsgObject rotateExtrude = new RotateExtrude(new double[] { 0.5, 0.25, 1.5, 0.25, 1.5, 2.75 }, axisOffset: 3.25);
				CsgObject linearExtrude = new LinearExtrude(new double[] { 0.5, 0.25, 1.5, 0.25, 1.5, 2.75 }, 6.25);
				CsgObject nGon = new NGonExtrusion(2.25, 6, 4.5);
				CsgObject scene = new Translate(
					new Union(box, new Union(cylinder, new Union(sphere, new Union(rotateExtrude, new Union(linearExtrude, nGon))))),
					1.5, -2.25, 0.125);

				string scad = OpenSCadOutput.GetScadString(scene);

				await Assert.That(scad).Contains("2.5");
				await Assert.That(scad).Contains("1.25");
				await Assert.That(scad).Contains("1.75");

				// Prove the extrusion primitives really made it into the scene, so the regex below
				// is actually covering their formatting and not just the box/cylinder/sphere.
				await Assert.That(scad).Contains("rotate_extrude");
				await Assert.That(scad).Contains("3.25");
				await Assert.That(scad).Contains("linear_extrude");
				await Assert.That(scad).Contains("6.25");
				await Assert.That(scad).Contains("2.25");

				// Commas are legal as argument separators, but never between two digits.
				await Assert.That(Regex.IsMatch(scad, @"\d,\d")).IsFalse();
			}
			finally
			{
				Thread.CurrentThread.CurrentCulture = originalCulture;
				Thread.CurrentThread.CurrentUICulture = originalUiCulture;
			}
		}
	}
}
