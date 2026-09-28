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

namespace MatterHackers.Agg.Platform.Linux
{
	/// <summary>
	/// Maps agg's <see cref="Cursors"/> onto a shape in the X "cursor" font (cursorfont.h).
	/// </summary>
	/// <remarks>
	/// cursorfont is an old, small set: no pan arrows, no "no move", no zoom, no alias/copy badges and no
	/// way to hide the pointer. Those fall back to the arrow rather than being faked with something
	/// misleading.
	/// </remarks>
	internal static class X11CursorMap
	{
		public static uint ToFontShape(Cursors cursor) => cursor switch
		{
			Cursors.IBeam or Cursors.VerticalText => X11.XC_xterm,
			Cursors.Hand => X11.XC_hand2,
			Cursors.Cross => X11.XC_crosshair,
			Cursors.Cell => X11.XC_plus,
			Cursors.Help => X11.XC_question_arrow,
			Cursors.WaitCursor or Cursors.Progress => X11.XC_watch,
			Cursors.SizeAll => X11.XC_fleur,

			// hand1 is the open grabbing hand; once it has hold, the four-way fleur says "moving".
			Cursors.Grab => X11.XC_hand1,
			Cursors.Grabbing => X11.XC_fleur,

			// A split bar is dragged along one axis, which is the same gesture - and in every toolkit
			// the same cursor - as a window edge on that axis.
			Cursors.SizeWE or Cursors.VSplit => X11.XC_sb_h_double_arrow,
			Cursors.SizeNS or Cursors.HSplit => X11.XC_sb_v_double_arrow,

			// cursorfont has no free-floating diagonal arrows, only the four named window corners. The
			// bottom pair point the right way for the one place agg asks: a window-widget corner grip.
			Cursors.SizeNWSE => X11.XC_bottom_right_corner,
			Cursors.SizeNESW => X11.XC_bottom_left_corner,

			// The single-edge resizes are exactly what the named sides and corners are.
			Cursors.ResizeNorth => X11.XC_top_side,
			Cursors.ResizeEast => X11.XC_right_side,
			Cursors.ResizeSouth => X11.XC_bottom_side,
			Cursors.ResizeWest => X11.XC_left_side,
			Cursors.ResizeNorthEast => X11.XC_top_right_corner,
			Cursors.ResizeNorthWest => X11.XC_top_left_corner,
			Cursors.ResizeSouthEast => X11.XC_bottom_right_corner,
			Cursors.ResizeSouthWest => X11.XC_bottom_left_corner,

			_ => X11.XC_arrow,
		};
	}
}
