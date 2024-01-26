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

using MatterHackers.Agg.Transform;
using Xunit;

namespace MatterHackers.Agg.Tests
{

    //
	public class AffineTests
	{
		[Fact]
		public void invert_test()
		{
			Affine a = Affine.NewIdentity();
			a.translate(10, 10);
			Affine b = new Affine(a);
			b.invert();

			double x = 100;
			double y = 100;
			double newx = x;
			double newy = y;

			a.Transform(ref newx, ref newy);
			b.Transform(ref newx, ref newy);
			Assert.Equal(x, newx, .001);
			Assert.Equal(y, newy, .001);
		}

		[Fact]
		public void transform_test()
		{
			Affine a = Affine.NewIdentity();
			a.translate(10, 20);

			double x = 10;
			double y = 20;
			double newx = 0;
			double newy = 0;

			a.Transform(ref newx, ref newy);
			Assert.Equal(x, newx, .001);
			Assert.Equal(y, newy, .001);
		}
	}
}