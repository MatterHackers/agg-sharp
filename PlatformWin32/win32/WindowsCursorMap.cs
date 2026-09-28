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

using WinFormsCursor = System.Windows.Forms.Cursor;
using WinFormsCursors = System.Windows.Forms.Cursors;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Maps agg's <see cref="Cursors"/> onto the stock WinForms cursors. agg's original set was modelled on
	/// these, so those map one to one; the later additions (agg-gui's Cursor Test set) take the nearest
	/// stock cursor, or the arrow where Windows has none - WinForms has no hidden, zoom, alias or copy cursor.
	/// </summary>
	internal static class WindowsCursorMap
	{
		public static WinFormsCursor ToWinFormsCursor(Cursors cursor) => cursor switch
		{
			Cursors.Arrow => WinFormsCursors.Arrow,
			Cursors.Hand => WinFormsCursors.Hand,
			Cursors.IBeam => WinFormsCursors.IBeam,
			Cursors.Cross => WinFormsCursors.Cross,
			Cursors.Default => WinFormsCursors.Default,
			Cursors.Help => WinFormsCursors.Help,
			Cursors.HSplit => WinFormsCursors.HSplit,
			Cursors.No => WinFormsCursors.No,
			Cursors.NoMove2D => WinFormsCursors.NoMove2D,
			Cursors.NoMoveHoriz => WinFormsCursors.NoMoveHoriz,
			Cursors.NoMoveVert => WinFormsCursors.NoMoveVert,
			Cursors.PanEast => WinFormsCursors.PanEast,
			Cursors.PanNE => WinFormsCursors.PanNE,
			Cursors.PanNorth => WinFormsCursors.PanNorth,
			Cursors.PanNW => WinFormsCursors.PanNW,
			Cursors.PanSE => WinFormsCursors.PanSE,
			Cursors.PanSouth => WinFormsCursors.PanSouth,
			Cursors.PanSW => WinFormsCursors.PanSW,
			Cursors.PanWest => WinFormsCursors.PanWest,
			Cursors.SizeAll => WinFormsCursors.SizeAll,
			Cursors.SizeNESW => WinFormsCursors.SizeNESW,
			Cursors.SizeNS => WinFormsCursors.SizeNS,
			Cursors.SizeNWSE => WinFormsCursors.SizeNWSE,
			Cursors.SizeWE => WinFormsCursors.SizeWE,
			Cursors.UpArrow => WinFormsCursors.UpArrow,
			Cursors.VSplit => WinFormsCursors.VSplit,
			Cursors.WaitCursor => WinFormsCursors.WaitCursor,

			Cursors.Progress => WinFormsCursors.AppStarting,
			Cursors.Cell => WinFormsCursors.Cross,
			Cursors.VerticalText => WinFormsCursors.IBeam,
			Cursors.NoDrop => WinFormsCursors.No,
			Cursors.Grab => WinFormsCursors.Hand,
			Cursors.Grabbing => WinFormsCursors.SizeAll,

			// Windows' resize cursors are all two-way; a single edge uses the one on its axis.
			Cursors.ResizeNorth or Cursors.ResizeSouth => WinFormsCursors.SizeNS,
			Cursors.ResizeEast or Cursors.ResizeWest => WinFormsCursors.SizeWE,
			Cursors.ResizeNorthEast or Cursors.ResizeSouthWest => WinFormsCursors.SizeNESW,
			Cursors.ResizeNorthWest or Cursors.ResizeSouthEast => WinFormsCursors.SizeNWSE,

			_ => WinFormsCursors.Arrow,
		};
	}
}
