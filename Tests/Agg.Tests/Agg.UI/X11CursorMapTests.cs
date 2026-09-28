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
using MatterHackers.Agg.Platform.Linux;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>The cursorfont shapes the X11 host picks for agg's cursors.</summary>
	public class X11CursorMapTests
	{
		/// <summary>cursorfont names every window side and corner, so the single-edge resizes are exact.</summary>
		[Test]
		[Arguments(Cursors.ResizeNorth, X11.XC_top_side)]
		[Arguments(Cursors.ResizeEast, X11.XC_right_side)]
		[Arguments(Cursors.ResizeSouth, X11.XC_bottom_side)]
		[Arguments(Cursors.ResizeWest, X11.XC_left_side)]
		[Arguments(Cursors.ResizeNorthEast, X11.XC_top_right_corner)]
		[Arguments(Cursors.ResizeNorthWest, X11.XC_top_left_corner)]
		[Arguments(Cursors.ResizeSouthEast, X11.XC_bottom_right_corner)]
		[Arguments(Cursors.ResizeSouthWest, X11.XC_bottom_left_corner)]
		[Arguments(Cursors.Grab, X11.XC_hand1)]
		[Arguments(Cursors.Progress, X11.XC_watch)]
		public async Task TheNewCursorsUseTheirFontShapes(Cursors cursor, uint expected)
		{
			await Assert.That(X11CursorMap.ToFontShape(cursor)).IsEqualTo(expected);
		}

		/// <summary>No shape at all in cursorfont: the arrow, not something misleading.</summary>
		[Test]
		[Arguments(Cursors.None)]
		[Arguments(Cursors.ZoomIn)]
		[Arguments(Cursors.PanNorth)]
		public async Task CursorsWithNoFontShapeFallBackToTheArrow(Cursors cursor)
		{
			await Assert.That(X11CursorMap.ToFontShape(cursor)).IsEqualTo(X11.XC_arrow);
		}
	}
}
