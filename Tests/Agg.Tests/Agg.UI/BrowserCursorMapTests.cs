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
using System.Threading.Tasks;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The canvas's CSS cursor is set from this table on every OnMouseEnter, so a member that fell out of it
	/// would leave the pointer showing the wrong thing rather than failing loudly - which is what makes the
	/// exhaustive check below worth more than the handful of spot checks.
	/// </summary>
	public class BrowserCursorMapTests
	{
		/// <summary>
		/// Every member of the enum, including any added later: an empty or null cursor value would be
		/// invalid CSS and silently leave the previous cursor showing.
		/// </summary>
		[Test]
		public async Task EveryCursorHasACssKeyword()
		{
			foreach (Cursors cursor in Enum.GetValues<Cursors>())
			{
				await Assert.That(BrowserCursorMap.ToCssCursor(cursor)).IsNotNullOrEmpty();
			}
		}

		/// <summary>
		/// The ones with an exact CSS equivalent, which is most of them - including the two diagonal resize
		/// cursors the mac host has to go looking for private AppKit selectors to find.
		/// </summary>
		[Test]
		[Arguments(Cursors.Default, "default")]
		[Arguments(Cursors.Arrow, "default")]
		[Arguments(Cursors.Hand, "pointer")]
		[Arguments(Cursors.IBeam, "text")]
		[Arguments(Cursors.Cross, "crosshair")]
		[Arguments(Cursors.SizeWE, "ew-resize")]
		[Arguments(Cursors.SizeNS, "ns-resize")]
		[Arguments(Cursors.SizeNESW, "nesw-resize")]
		[Arguments(Cursors.SizeNWSE, "nwse-resize")]
		[Arguments(Cursors.SizeAll, "move")]
		[Arguments(Cursors.WaitCursor, "wait")]
		[Arguments(Cursors.Help, "help")]
		[Arguments(Cursors.No, "not-allowed")]
		public async Task TheExactEquivalentsMap(Cursors cursor, string expected)
		{
			await Assert.That(BrowserCursorMap.ToCssCursor(cursor)).IsEqualTo(expected);
		}

		/// <summary>
		/// The autoscroll family has no directional CSS cursors, so all nine fall back to the one scroll
		/// cursor a browser draws for exactly this gesture - closer than the plain arrow the mac host has to
		/// settle for, and the deliberate choice worth stating.
		/// </summary>
		[Test]
		[Arguments(Cursors.NoMove2D)]
		[Arguments(Cursors.NoMoveHoriz)]
		[Arguments(Cursors.NoMoveVert)]
		[Arguments(Cursors.PanNorth)]
		[Arguments(Cursors.PanNE)]
		[Arguments(Cursors.PanEast)]
		[Arguments(Cursors.PanSE)]
		[Arguments(Cursors.PanSouth)]
		[Arguments(Cursors.PanSW)]
		[Arguments(Cursors.PanWest)]
		[Arguments(Cursors.PanNW)]
		public async Task ThePanFamilyFallsBackToTheScrollCursor(Cursors cursor)
		{
			await Assert.That(BrowserCursorMap.ToCssCursor(cursor)).IsEqualTo("all-scroll");
		}

		/// <summary>
		/// A splitter grip is named by what the split does, not by which way the bar moves, and the two are
		/// easy to swap: HSplit is the horizontal bar between stacked rows, and it drags up and down.
		/// </summary>
		[Test]
		public async Task TheSplittersDoNotSwap()
		{
			await Assert.That(BrowserCursorMap.ToCssCursor(Cursors.HSplit)).IsEqualTo("row-resize");
			await Assert.That(BrowserCursorMap.ToCssCursor(Cursors.VSplit)).IsEqualTo("col-resize");
		}
	}
}
