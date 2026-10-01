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

using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	public static class PopupMenuExtensions
	{
		public static void ShowMenu(this PopupMenu popupMenu, GuiWidget anchorWidget, MouseEventArgs mouseEvent)
		{
			popupMenu.ShowMenu(anchorWidget, mouseEvent.Position);
		}

		public static void ShowMenu(this PopupMenu popupMenu, GuiWidget anchorWidget, Vector2 menuPosition)
		{
			var systemWindow = anchorWidget.PopupHostWindow();
			PopupMenu.ClearToolTipsAbove(anchorWidget);

			systemWindow.ShowPopup(
				popupMenu.Theme,
				new MatePoint(anchorWidget)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Left, MateEdge.Bottom)
				},
				new MatePoint(popupMenu)
				{
					Mate = new MateOptions(MateEdge.Left, MateEdge.Top),
					AltMate = new MateOptions(MateEdge.Right, MateEdge.Bottom)
				},
				altBounds: new RectangleDouble(menuPosition.X + 1, menuPosition.Y + 1, menuPosition.X + 1, menuPosition.Y + 1));
		}
	}
}
