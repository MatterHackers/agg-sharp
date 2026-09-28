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

using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using MatterHackers.Agg.RasterizerScanline;
using TUnit.Core;

namespace MatterHackers.Agg.Tests
{
	// ArrayPOD.Resize used to allocate the new array without recording its size, so Size() kept
	// returning the constructor size. Every caller grows with `if (need > arr.Size()) arr.Resize(need)`,
	// so once the need passed the initial size each call reallocated - e.g. every scanline reset on a
	// raster wider than ~998px threw away and reallocated both the spans and covers arrays.
	public class ArrayPODTests
	{
		[Test]
		public async Task ResizeRecordsTheNewSize()
		{
			var array = new ArrayPOD<int>(16);
			array.Resize(100);

			await Assert.That(array.Size()).IsEqualTo(100);
			await Assert.That(array.Array.Length).IsEqualTo(100);
		}

		[Test]
		public async Task ResizeToTheSameSizeKeepsTheArray()
		{
			var array = new ArrayPOD<int>(16);
			array.Resize(100);
			var first = array.Array;
			array.Resize(100);

			await Assert.That(ReferenceEquals(array.Array, first)).IsTrue();
		}

		[Test]
		public async Task WideScanlineResetDoesNotReallocateEachTime()
		{
			var scanline = new scanline_unpacked_8();
			scanline.reset(0, 2000);
			var covers = scanline.GetCovers();

			scanline.reset(0, 2000);
			await Assert.That(ReferenceEquals(scanline.GetCovers(), covers)).IsTrue();

			scanline.reset(0, 1500);
			await Assert.That(ReferenceEquals(scanline.GetCovers(), covers)).IsTrue();
		}
	}
}
