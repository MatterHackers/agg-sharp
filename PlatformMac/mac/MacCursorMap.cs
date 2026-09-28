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

namespace MatterHackers.Agg.Platform.Mac
{
	/// <summary>
	/// Maps agg's <see cref="Cursors"/> onto the NSCursor class-method selector that vends it.
	/// </summary>
	/// <remarks>
	/// Pure - the window sends the selector - so the table runs in the test suite. Some selectors are private
	/// AppKit (those starting with an underscore, and <c>busyButClickableCursor</c>); the window probes every
	/// one with <c>respondsToSelector:</c> and shows the arrow for any this macOS lacks. Where macOS has no
	/// cursor at all the answer is the arrow, not something misleading.
	/// </remarks>
	public static class MacCursorMap
	{
		/// <summary>The NSCursor class-method selector name for an agg cursor. Never empty.</summary>
		public static string ToSelectorName(Cursors cursor) => cursor switch
		{
			Cursors.Hand => "pointingHandCursor",
			Cursors.IBeam => "IBeamCursor",
			Cursors.Cross => "crosshairCursor",
			Cursors.No => "operationNotAllowedCursor",
			Cursors.SizeNS => "resizeUpDownCursor",
			Cursors.SizeWE => "resizeLeftRightCursor",
			Cursors.HSplit => "resizeUpDownCursor",
			Cursors.VSplit => "resizeLeftRightCursor",
			Cursors.UpArrow => "resizeUpCursor",

			// The diagonal resize cursors macOS draws at its own window corners exist, but only as
			// private class methods on NSCursor - so they are probed for rather than assumed (see
			// MacSystemWindow.ResolveCursor). Without them a window-widget corner grip, which is the one
			// place agg asks for them, would hover as a plain arrow.
			Cursors.SizeNWSE => "_windowResizeNorthWestSouthEastCursor",
			Cursors.SizeNESW => "_windowResizeNorthEastSouthWestCursor",

			// No move-in-any-direction cursor exists here; the open hand is what macOS itself shows
			// for "this can be dragged around", which is what SizeAll means to agg.
			Cursors.SizeAll => "openHandCursor",

			Cursors.ContextMenu => "contextualMenuCursor",
			Cursors.VerticalText => "IBeamCursorForVerticalLayout",
			Cursors.Alias => "dragLinkCursor",
			Cursors.Copy => "dragCopyCursor",
			Cursors.NoDrop => "operationNotAllowedCursor",
			Cursors.Grab => "openHandCursor",
			Cursors.Grabbing => "closedHandCursor",

			// macOS has no table-cell cursor; the crosshair is the nearest precision pointer.
			Cursors.Cell => "crosshairCursor",

			// Private, like the diagonals: the spinning-arrow cursor, and the magnifiers Preview uses.
			Cursors.Progress => "busyButClickableCursor",
			Cursors.ZoomIn => "_zoomInCursor",
			Cursors.ZoomOut => "_zoomOutCursor",

			// The four edges have public one-way arrows. The corners have no one-way cursor worth probing
			// for, so they use the two-way diagonal through that corner - still the right axis.
			Cursors.ResizeNorth => "resizeUpCursor",
			Cursors.ResizeSouth => "resizeDownCursor",
			Cursors.ResizeEast => "resizeRightCursor",
			Cursors.ResizeWest => "resizeLeftCursor",
			Cursors.ResizeNorthEast or Cursors.ResizeSouthWest => "_windowResizeNorthEastSouthWestCursor",
			Cursors.ResizeNorthWest or Cursors.ResizeSouthEast => "_windowResizeNorthWestSouthEastCursor",

			// The eight pan directions have no macOS equivalent, private or otherwise, so they fall back to
			// the arrow rather than being faked with something misleading. None falls back too: hiding
			// the pointer on macOS is a global [NSCursor hide] counter, not a cursor, and cursor rects
			// cannot scope it to a widget.
			_ => "arrowCursor",
		};
	}
}
