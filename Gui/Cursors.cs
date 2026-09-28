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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The pointer shapes a widget can ask for through <see cref="GuiWidget.Cursor"/>. Each platform maps
	/// these onto its own cursors and falls back to the nearest thing (or the arrow) where it has none.
	/// </summary>
	/// <remarks>
	/// Append only: the numeric values are persisted and consumed outside this repo (MatterCAD), so a member
	/// is never reordered or removed, and new ones go on the end. <c>CursorsEnumTests</c> pins this.
	/// </remarks>
	public enum Cursors
	{
		Arrow,
		Cross,
		Default,
		Hand,
		Help,
		HSplit,
		IBeam,
		No,
		NoMove2D,
		NoMoveHoriz,
		NoMoveVert,
		PanEast,
		PanNE,
		PanNorth,
		PanNW,
		PanSE,
		PanSouth,
		PanSW,
		PanWest,
		SizeAll,
		SizeNESW,
		SizeNS,
		SizeNWSE,
		SizeWE,
		UpArrow,
		VSplit,
		WaitCursor,

		// The set agg-gui's Cursor Test lists (egui's CursorIcon) that agg did not already have.

		/// <summary>Hide the pointer while it is over the widget.</summary>
		None,

		/// <summary>A context menu is available here.</summary>
		ContextMenu,

		/// <summary>Busy in the background, but still interactive (the arrow with a spinner).</summary>
		Progress,

		/// <summary>Hovering a cell in a table.</summary>
		Cell,

		/// <summary>Insertion caret for vertical text.</summary>
		VerticalText,

		/// <summary>Dropping here makes an alias or shortcut.</summary>
		Alias,

		/// <summary>Dropping here makes a copy.</summary>
		Copy,

		/// <summary>The dragged item cannot be dropped here.</summary>
		NoDrop,

		/// <summary>The item under the pointer can be picked up (open hand).</summary>
		Grab,

		/// <summary>An item is being dragged (closed hand).</summary>
		Grabbing,

		ZoomIn,

		ZoomOut,

		// Single-edge resizes: the edge or corner that moves, where SizeNS / SizeWE / SizeNESW / SizeNWSE
		// are the two-way versions.
		ResizeNorth,
		ResizeEast,
		ResizeSouth,
		ResizeWest,
		ResizeNorthEast,
		ResizeNorthWest,
		ResizeSouthEast,
		ResizeSouthWest,
	}
}
