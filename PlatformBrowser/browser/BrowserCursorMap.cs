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

using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// Maps agg's <see cref="Cursors"/> onto the CSS <c>cursor</c> keyword the canvas gets set to.
	/// </summary>
	/// <remarks>
	/// CSS covers agg's set better than AppKit does - the diagonal resize cursors the mac host has to probe
	/// for privately are plain keywords here - so there is much less falling back. Where a fallback is
	/// needed it is a keyword that means something near enough rather than a custom image: a CSS keyword is
	/// drawn by the OS, matches whatever the user has themselves set, and costs no asset to ship.
	/// <para/>
	/// Pure - no JS interop - so the whole table runs in the desktop test suite.
	/// </remarks>
	public static class BrowserCursorMap
	{
		/// <summary>
		/// The CSS <c>cursor</c> value for an agg cursor. Never empty: an unknown member falls back to
		/// <c>default</c>, which is the arrow, exactly as the mac host falls back to arrowCursor.
		/// </summary>
		public static string ToCssCursor(Cursors cursor) => cursor switch
		{
			Cursors.Arrow or Cursors.Default => "default",
			Cursors.Cross => "crosshair",
			Cursors.Hand => "pointer",
			Cursors.Help => "help",
			Cursors.IBeam => "text",
			Cursors.No => "not-allowed",
			Cursors.WaitCursor => "wait",
			Cursors.UpArrow => "n-resize",

			// A splitter's grip. CSS names these by what the split does rather than by which way the bar
			// moves, and agg's HSplit is the horizontal bar between two stacked rows - which is dragged up
			// and down, hence row-resize.
			Cursors.HSplit => "row-resize",
			Cursors.VSplit => "col-resize",

			Cursors.SizeNS => "ns-resize",
			Cursors.SizeWE => "ew-resize",
			Cursors.SizeNESW => "nesw-resize",
			Cursors.SizeNWSE => "nwse-resize",

			// What agg means by SizeAll is "this can be dragged around", which is what CSS move is for. The
			// mac host has to settle for the open hand; here the exact cursor exists.
			Cursors.SizeAll => "move",

			// The autoscroll family - the origin marker and the eight pan directions a middle-drag shows.
			// CSS has one scroll cursor and no directional variants, but all-scroll is drawn for exactly
			// this gesture, so it is a truer answer than the plain arrow the mac host has to fall back to.
			Cursors.NoMove2D or Cursors.NoMoveHoriz or Cursors.NoMoveVert => "all-scroll",
			Cursors.PanEast or Cursors.PanNE or Cursors.PanNorth or Cursors.PanNW
				or Cursors.PanSE or Cursors.PanSouth or Cursors.PanSW or Cursors.PanWest => "all-scroll",

			_ => "default",
		};
	}
}
