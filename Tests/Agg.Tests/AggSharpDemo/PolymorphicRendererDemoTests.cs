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
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Tests.GoldenImages;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// polymorphic_renderer.cpp against C++ AGG (demo_polymorphic_renderer.cpp), one golden per kind of format.
	public class PolymorphicRendererDemoTests
	{
		/// <summary>The default frame: C++'s rgb555, 5 bits a channel.</summary>
		[Test]
		public async Task Rgb555FrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new PolymorphicRendererDemo()), "polymorphic_renderer_400x330");
		}

		/// <summary>rgb565: green keeps a sixth bit.</summary>
		[Test]
		public async Task Rgb565FrameMatchesCppAgg()
		{
			var demo = new PolymorphicRendererDemo();
			demo.FormatRbox.CurrentItem = 1;
			await AggReference.Check(Render(demo), "polymorphic_renderer_400x330_rgb565");
		}

		/// <summary>bgra32: a full 8 bits a channel.</summary>
		[Test]
		public async Task Bgra32FrameMatchesCppAgg()
		{
			var demo = new PolymorphicRendererDemo();
			demo.FormatRbox.CurrentItem = 7;
			await AggReference.Check(Render(demo), "polymorphic_renderer_400x330_bgra32");
		}

		private static ImageBuffer Render(PolymorphicRendererDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
