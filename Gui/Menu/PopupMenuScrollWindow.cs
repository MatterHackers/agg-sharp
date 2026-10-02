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
	/// The scroll window a clamped menu keeps its rows in.
	/// </summary>
	/// <remarks>
	/// This exists only so that <see cref="ScrollableWidget.OnKeyDown"/> does not claim Up and Down to
	/// nudge the viewport 16 pixels. Once a row has keyboard focus the scroll window is on the routing
	/// path between the menu and that row, so the plain widget would swallow every arrow key before the
	/// menu could move its highlight. Inside a menu the arrows belong to the highlight, and
	/// <see cref="PopupMenu.MoveHighlight(int)"/> does the scrolling that keeps it visible.
	/// </remarks>
	internal class PopupMenuScrollWindow : ScrollableWidget
	{
		public PopupMenuScrollWindow()
			: base(true)
		{
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.Up
				|| keyEvent.KeyCode == Keys.Down)
			{
				// The focused row still gets its turn; only the base class's scroll nudge is skipped, so
				// the key arrives at the menu unclaimed.
				// Returning early also skips GuiWidget's Tab handling and its public KeyDown event for
				// these two keys. Neither matters to a menu - Tab is not Up or Down, and nothing
				// subscribes to a menu scroller's KeyDown - but a subscriber added later would silently
				// not hear the arrows.
				var childWithFocus = GetChildContainingFocus();
				if (childWithFocus != null
					&& childWithFocus.Visible
					&& childWithFocus.Enabled)
				{
					childWithFocus.OnKeyDown(keyEvent);
				}

				return;
			}

			base.OnKeyDown(keyEvent);
		}
	}
}
