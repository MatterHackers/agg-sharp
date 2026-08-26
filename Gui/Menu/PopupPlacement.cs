/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

// Where a popup goes, as opposed to what is in it. These types moved out of PopupMenu.cs unchanged; they
// describe the edge-to-edge mating a popup is positioned by and the SystemWindow extension that applies it,
// and are used by drop downs and tool tips as well as by menus.
namespace MatterHackers.Agg.UI
{
	[Flags]
	public enum MateEdge
	{
		Top = 1,
		Bottom = 2,
		Left = 4,
		Right = 8
	}

	public class MateOptions
	{
		public MateOptions(MateEdge horizontalEdge = MateEdge.Left, MateEdge verticalEdge = MateEdge.Bottom)
		{
			this.HorizontalEdge = horizontalEdge;
			this.VerticalEdge = verticalEdge;
		}

		public MateEdge HorizontalEdge { get; set; }

		public MateEdge VerticalEdge { get; set; }

		public bool Top => this.VerticalEdge.HasFlag(MateEdge.Top);

		public bool Bottom => this.VerticalEdge.HasFlag(MateEdge.Bottom);

		public bool Left => this.HorizontalEdge.HasFlag(MateEdge.Left);

		public bool Right => this.HorizontalEdge.HasFlag(MateEdge.Right);
	}

	public class MatePoint
	{
		public MateOptions Mate { get; set; } = new MateOptions();

		public MateOptions AltMate { get; set; } = new MateOptions();

		public GuiWidget Widget { get; set; }

		public MatePoint()
		{
		}

		public MatePoint(GuiWidget widget)
		{
			this.Widget = widget;
		}

		public RectangleDouble Offset { get; set; }
	}

	public interface IOverrideAutoClose
	{
		bool AllowAutoClose { get; }
	}

	public static class SystemWindowExtension
	{
		private static void RightHorizontalSplitPopup(SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds)
		{
			// Calculate left for right aligned split
			Vector2 popupPosition = new Vector2(systemWindow.Width - popup.Widget.Width, 0);

			Vector2 anchorLeft = anchor.Widget.Parent.TransformToScreenSpace(anchor.Widget.Position);

			popup.Widget.Height = anchorLeft.Y;

			popup.Widget.Position = popupPosition;
		}

		public static void ShowPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			ShowPopup(systemWindow, theme, anchor, popup, altBounds, borderWidth, BestPopupPosition);
		}

		public static void ShowRightSplitPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			ShowPopup(systemWindow, theme, anchor, popup, altBounds, borderWidth, RightHorizontalSplitPopup);
		}

		public static void ShowPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds, int borderWidth, Action<SystemWindow, MatePoint, MatePoint, RectangleDouble> layoutHelper)
		{
			var hookedParents = new HashSet<GuiWidget>();

			List<IIgnoredPopupChild> ignoredWidgets = popup.Widget.Children.OfType<IIgnoredPopupChild>().ToList();

			void Widget_Draw(object sender, DrawEventArgs e)
			{
				if (borderWidth > 0)
				{
					e.Graphics2D.Render(
						new Stroke(
							new RoundedRect(popup.Widget.LocalBounds, 0),
							borderWidth * 2),
						theme.PopupBorderColor);
				}
			}

			void WidgetRelativeTo_PositionChanged(object sender, EventArgs e)
			{
				if (anchor.Widget?.Parent != null)
				{
					layoutHelper.Invoke(systemWindow, anchor, popup, altBounds);
				}
			}

			// The widget that currently owns the keyboard focus, or null when nothing in the window does.
			// Walked from the window down because Focused is only true on the leaf of the focus chain.
			GuiWidget FocusedWidget()
			{
				if (systemWindow?.ContainsFocus != true)
				{
					return null;
				}

				var focused = (GuiWidget)systemWindow;
				while (focused.Children.FirstOrDefault(child => child.ContainsFocus) is GuiWidget focusedChild)
				{
					focused = focusedChild;
				}

				return focused;
			}

			void CloseMenu()
			{
				// Where the focus is *before* Close() drops this popup's own claim on it. Something outside
				// this popup holding it means the focus has moved on rather than been given up.
				var focused = FocusedWidget();
				bool focusHasMovedOn = focused != null
					&& focused != systemWindow
					&& focused != popup.Widget
					&& !focused.Parents<GuiWidget>().Any(parent => parent == popup.Widget);

				popup.Widget.AfterDraw -= Widget_Draw;

				popup.Widget.Close();

				anchor.Widget.Closed -= Anchor_Closed;

				// Unbind callbacks on parents for position_changed if we're closing
				foreach (GuiWidget widget in hookedParents)
				{
					widget.PositionChanged -= WidgetRelativeTo_PositionChanged;
					widget.BoundsChanged -= WidgetRelativeTo_PositionChanged;
				}

				// Long lived originating item must be unregistered
				anchor.Widget.Closed -= Anchor_Closed;

				// Restore focus to the widget this popup was opened from - choosing an item or pressing Escape
				// gives the focus up, and it must not be left stranded on a widget that no longer exists.
				// Not when something else has already taken it, though: a popup that is closing *because* the
				// focus moved on must leave it where it went. Sweeping down a column of sub menu parents is
				// where that bites - the sibling sub menu being left behind would otherwise drag the highlight
				// back onto its own row and close the sub menu the pointer had already moved on to.
				if (!focusHasMovedOn
					&& anchor.Widget?.HasBeenClosed == false)
				{
					anchor.Widget.Focus();
				}
			}

			void FocusChanged(object s, EventArgs e)
			{
				UiThread.RunOnIdle(() =>
				{
					// Fired any time focus changes. Traditionally we closed the menu if we weren't focused.
					// To accommodate children (or external widgets) having focus we also query for and consider special cases
					bool specialChildHasFocus = ignoredWidgets.Any(w => w.ContainsFocus || w.Focused || w.KeepMenuOpen);
					bool descendantIsHoldingOpen = popup.Widget.Descendants<GuiWidget>().Any(w => w is IIgnoredPopupChild ignoredPopupChild
						&& ignoredPopupChild.KeepMenuOpen);

					// If the focused changed and we've lost focus and no special cases permit, close the menu
					if (!popup.Widget.ContainsFocus
						&& !specialChildHasFocus
						&& !descendantIsHoldingOpen
						&& !PopupWidget.DebugKeepOpen)
					{
						CloseMenu();
					}
				});
			}

			void Anchor_Closed(object sender, EventArgs e)
			{
				// If the owning widget closed, so should we
				CloseMenu();
			}

			foreach (var ancestor in anchor.Widget.Parents<GuiWidget>().Where(p => p != systemWindow))
			{
				if (hookedParents.Add(ancestor))
				{
					ancestor.PositionChanged += WidgetRelativeTo_PositionChanged;
					ancestor.BoundsChanged += WidgetRelativeTo_PositionChanged;
				}
			}

			popup.Widget.ContainsFocusChanged += FocusChanged;
			popup.Widget.AfterDraw += Widget_Draw;

			WidgetRelativeTo_PositionChanged(anchor.Widget, null);
			anchor.Widget.Closed += Anchor_Closed;

			// When the widgets position changes, sync the popup position
			systemWindow?.AddChild(popup.Widget);

			popup.Widget.Focus();

			popup.Widget.Invalidate();
		}

		private static void BestPopupPosition(this SystemWindow systemWindow, MatePoint anchor, MatePoint popup, RectangleDouble altBounds)
		{
			// Calculate left aligned screen space position (using widgetRelativeTo.parent)
			Vector2 anchorLeft = anchor.Widget.Parent.TransformToParentSpace(systemWindow, anchor.Widget.Position);
			anchorLeft += new Vector2(altBounds.Left, altBounds.Bottom);

			Vector2 popupPosition = anchorLeft;

			var bounds = altBounds == default(RectangleDouble) ? anchor.Widget.LocalBounds : altBounds;

			Vector2 xPosition = PopupMenu.GetXAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

			Vector2 screenPosition;

			screenPosition = anchorLeft + xPosition;

			// Constrain
			if (screenPosition.X + popup.Widget.Width > systemWindow.Width
				|| screenPosition.X < 0)
			{
				xPosition = PopupMenu.GetXAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);
			}

			popupPosition += xPosition;

			Vector2 yPosition = PopupMenu.GetYAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

			screenPosition = anchorLeft + yPosition;

			// Constrain
			if (anchor.AltMate != null
				&& (screenPosition.Y + popup.Widget.Height > systemWindow.Height
					|| screenPosition.Y < 0))
			{
				yPosition = PopupMenu.GetYAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);
			}

			popupPosition += yPosition;

			// Flipping to the alt mate does not guarantee an on screen result - several mate combinations
			// (anchor bottom to popup bottom, for one) resolve to no offset at all, leaving the popup exactly
			// where it did not fit. Clamp so the content stays reachable. The mate flip's choice is still
			// respected on both axes: only a result that would land off screen is pulled back, so callers
			// that rely on a particular edge alignment keep it whenever it fits.
			double topAlignedY = systemWindow.Height - popup.Widget.Height;
			if (popup.Widget.Height > systemWindow.Height)
			{
				// Nothing can show all of a popup that is taller than the window (menus avoid this by
				// scrolling first, see PopupMenu.MakeMenuHaveScroll). Show its top - that is where the items
				// a user is looking for are; bottom aligning it would push them above the top of the window.
				popupPosition.Y = topAlignedY;
			}
			else
			{
				popupPosition.Y = Math.Max(0, Math.Min(popupPosition.Y, topAlignedY));
			}

			if (popup.Widget.Width > systemWindow.Width)
			{
				// Nothing can show all of a popup wider than the window. Keep its left edge, which is where
				// icons and the start of every label are; right aligning would hide the text a user reads by.
				popupPosition.X = 0;
			}
			else
			{
				// DIVERGES from agg-gui, which clamps with a 4 pixel MARGIN (popup_clamps_to_viewport in
				// agg-gui/src/widgets/menu/mod.rs). Here the margin is 0, matching the vertical clamp above,
				// so an edge popup sits flush against the window rather than being inset on one axis only.
				popupPosition.X = Math.Max(0, Math.Min(popupPosition.X, systemWindow.Width - popup.Widget.Width));
			}

			popup.Widget.Position = popupPosition;
		}
	}
}
