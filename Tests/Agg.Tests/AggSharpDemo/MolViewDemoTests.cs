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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.Demos;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// mol_view.cpp's port against C++ AGG (demo_mol_view.cpp).
	public class MolViewDemoTests
	{
		/// <summary>The first molecule of 1.sdf as the example opens on it.</summary>
		[Test]
		public async Task DefaultFrameMatchesCppAgg()
		{
			await AggReference.Check(Render(new MolViewDemo()), "mol_view_400x400");
		}

		/// <summary>The second molecule - its halogen labels coloured - with thicker bonds, bigger labels,
		/// turned 30 degrees, zoomed 1.3 times and moved off centre.</summary>
		[Test]
		public async Task SecondMoleculeTurnedZoomedMovedMatchesCppAgg()
		{
			var demo = new MolViewDemo();
			demo.ThicknessSlider.Value = 0.8;
			demo.TextSizeSlider.Value = 0.7;
			demo.OnKeyDown(Keys.PageDown, 0);
			demo.Angle = 30 * System.Math.PI / 180.0;
			demo.Scale = 1.3;
			demo.CenterX = 210;
			demo.CenterY = 190;
			await AggReference.Check(Render(demo), "mol_view_400x400_second_turned_zoomed_moved");
		}

		/// <summary>A copy of 1.sdf checked out with CRLF line endings draws the same frame.</summary>
		[Test]
		public async Task CrlfFileDrawsTheSameFrame()
		{
			string crlf = MolViewDemo.EmbeddedMolecules().Replace("\r\n", "\n").Replace("\n", "\r\n");
			await AggReference.Check(Render(new MolViewDemo(new System.IO.StringReader(crlf))), "mol_view_400x400");
		}

		/// <summary>C++ reads every one of 1.sdf's seven molecules; Page Up and Page Down stop at the ends.</summary>
		[Test]
		public async Task ReadsEveryMoleculeAndPagesWithinThem()
		{
			var demo = new MolViewDemo();
			await Assert.That(demo.MoleculeCount).IsEqualTo(7);

			demo.OnKeyDown(Keys.PageUp, 0);
			await Assert.That(demo.CurrentMolecule).IsEqualTo(0);
			for (int i = 0; i < 10; i++)
			{
				demo.OnKeyDown(Keys.PageDown, 0);
			}

			await Assert.That(demo.CurrentMolecule).IsEqualTo(6);
		}

		private static ImageBuffer Render(MolViewDemo demo)
		{
			ImageBuffer frame = AggDemoView.NewReferenceFrame(demo);
			AggDemoView.DrawReferenceFrame(demo, frame);
			return frame;
		}
	}
}
