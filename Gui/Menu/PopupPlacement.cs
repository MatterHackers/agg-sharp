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

		/// <summary>
		/// The window a popup anchored to <paramref name="widget"/> belongs in: the outermost
		/// <see cref="SystemWindow"/> above it.
		/// </summary>
		/// <remarks>
		/// SystemWindows nest in single window mode, and only the outermost one is the whole screen a popup
		/// has to fit inside - an inner one is a panel and reports a height a menu would be clamped to for no
		/// reason. Every menu path asks through here so the window a popup is added to, measured against and
		/// positioned in is always the same one; the sub menu path used to take the innermost and so
		/// disagreed with the one that showed it.
		/// </remarks>
		public static SystemWindow PopupHostWindow(this GuiWidget widget)
		{
			return widget.Parents<SystemWindow>().LastOrDefault();
		}

		public static void ShowPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			// Any menu shown this way is fully populated by now, so this is the point at which we can tell
			// whether it fits. Doing it here rather than at each call site is what makes scrolling the
			// default for every popup menu - the three callers that remembered to ask for it themselves were
			// the only menus that ever got it, and the rest were drawn off the edge of the window with their
			// items unreachable.
			if (popup.Widget is PopupMenu popupMenu)
			{
				popupMenu.MakeMenuHaveScroll(systemWindow.Height - PopupMenu.WindowEdgeInset);
			}

			ShowPopup(systemWindow, theme, anchor, popup, altBounds, borderWidth, BestPopupPosition);
		}

		public static void ShowRightSplitPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds = default(RectangleDouble), int borderWidth = 1)
		{
			ShowPopup(systemWindow, theme, anchor, popup, altBounds, borderWidth, RightHorizontalSplitPopup);
		}

		public static void ShowPopup(this SystemWindow systemWindow, ThemeConfig theme, MatePoint anchor, MatePoint popup, RectangleDouble altBounds, int borderWidth, Action<SystemWindow, MatePoint, MatePoint, RectangleDouble> layoutHelper)
		{
			var hookedParents = new HashSet<GuiWidget>();

			// A menu row can change the theme and leave the menu open (View > Color), so the open popup restyles
			// with it. A menu follows the theme it was built from, which is the one its colours were copied out of.
			ThemeBindings.FollowWhileOpen(popup.Widget, popup.Widget is PopupMenu popupMenu ? popupMenu.Theme : theme);

			List<IIgnoredPopupChild> ignoredWidgets = popup.Widget.Children.OfType<IIgnoredPopupChild>().ToList();

			void Widget_Draw(object sender, DrawEventArgs e)
			{
				if (borderWidth > 0)
				{
					// Trace whatever corner rounding the popup fills itself with (a PopupMenu sets
					// ThemeConfig.MenuPopupRadius), or the square corners of one that sets none. Drawing this
					// at radius 0 unconditionally put a square border back around a rounded panel.
					var radius = popup.Widget.BackgroundRadius;
					var outline = new RoundedRect(popup.Widget.LocalBounds, 0);
					outline.radius(radius.SW, radius.SE, radius.NE, radius.NW);

					e.Graphics2D.Render(
						new Stroke(outline, borderWidth * 2),
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

			void CloseMenu()
			{
				// Where the focus is *before* Close() drops this popup's own claim on it. Something outside
				// this popup holding it means the focus has moved on rather than been given up.
				// FocusedLeafOfWindow roots at the topmost parent rather than at systemWindow, which for a
				// real top level window is the same widget; the systemWindow comparison below keeps the
				// window itself from counting as "moved on" either way.
				var focused = systemWindow?.FocusedLeafOfWindow();
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

			// The user switching to another application closes the menu, as a native one does. Not left to
			// FocusChanged: the mac and browser hosts keep agg's focus across an app switch (see
			// SystemWindow.Deactivated), so the popup would still hold it and stay open.
			void Window_Deactivated(object sender, EventArgs e)
			{
				bool descendantIsHoldingOpen = popup.Widget.Descendants<GuiWidget>().Any(w => w is IIgnoredPopupChild ignoredPopupChild
					&& ignoredPopupChild.KeepMenuOpen);

				if (!popup.Widget.HasBeenClosed
					&& !descendantIsHoldingOpen
					&& !PopupWidget.DebugKeepOpen)
				{
					CloseMenu();
				}
			}

			void Popup_Closed(object sender, EventArgs e)
			{
				// However the popup went (a pick, Escape, focus), the window must stop holding it.
				popup.Widget.Closed -= Popup_Closed;
				if (systemWindow != null)
				{
					systemWindow.Deactivated -= Window_Deactivated;
				}
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
			popup.Widget.Closed += Popup_Closed;
			if (systemWindow != null)
			{
				systemWindow.Deactivated += Window_Deactivated;
			}

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
			// The rectangle the popup mates to - the anchor, or altBounds (a click point, say) in the anchor's
			// coordinates - read as it is drawn in the window. The mates below add its width and height to its
			// corner, so both have to come through the same transform: mapping only the corner and adding the
			// anchor's own widths put a popup opened inside a zoomed node editor off by the zoom.
			// The anchor's own LocalBounds, not its origin: a widget whose bounds do not start at 0, 0 (a text
			// button) is drawn from its LocalBounds corner, and mating to its origin put the popup beside it.
			var localBounds = altBounds == default(RectangleDouble) ? anchor.Widget.LocalBounds : altBounds;
			var drawnBounds = anchor.Widget.TransformToParentSpace(systemWindow, localBounds);

			Vector2 anchorLeft = new Vector2(drawnBounds.Left, drawnBounds.Bottom);

			Vector2 popupPosition = anchorLeft;

			var bounds = new RectangleDouble(0, 0, drawnBounds.Width, drawnBounds.Height);

			Vector2 xPosition = PopupMenu.GetXAnchor(anchor.Mate, popup.Mate, popup.Widget, bounds);

			Vector2 screenPosition;

			screenPosition = anchorLeft + xPosition;

			bool FitsAcross(Vector2 offset)
			{
				double left = anchorLeft.X + offset.X;
				return left >= 0 && left + popup.Widget.Width <= systemWindow.Width;
			}

			// Constrain
			bool fitsEitherSide = true;
			if (!FitsAcross(xPosition))
			{
				xPosition = PopupMenu.GetXAnchor(anchor.AltMate, popup.AltMate, popup.Widget, bounds);
				fitsEitherSide = FitsAcross(xPosition);
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

			// A popup that fits on neither side of its anchor (a sub menu on a phone-width window) has just been
			// clamped back across the anchor, and if it is level with it too it covers the row the user opened it
			// from - only the row's arrow showed. Move it off the anchor vertically instead: below it, else above
			// it. DIVERGES from agg-gui, whose stack_layout clamps a sub menu back over its parent row too.
			if (!fitsEitherSide)
			{
				popupPosition.Y = ClearOfAnchorY(systemWindow, popup.Widget, drawnBounds, popupPosition);
			}

			popup.Widget.Position = popupPosition;
		}

		/// <summary>
		/// The bottom for <paramref name="popup"/> that keeps it off <paramref name="anchorBounds"/>: unchanged
		/// when it already clears the anchor, else hanging below the anchor, else standing on top of it, else
		/// (no room on either side) unchanged.
		/// </summary>
		private static double ClearOfAnchorY(SystemWindow systemWindow, GuiWidget popup, RectangleDouble anchorBounds, Vector2 position)
		{
			bool overlapsAnchor = position.X < anchorBounds.Right
				&& position.X + popup.Width > anchorBounds.Left
				&& position.Y < anchorBounds.Top
				&& position.Y + popup.Height > anchorBounds.Bottom;
			if (!overlapsAnchor)
			{
				return position.Y;
			}

			double below = anchorBounds.Bottom - popup.Height;
			if (below >= 0)
			{
				return below;
			}

			double above = anchorBounds.Top;
			if (above + popup.Height <= systemWindow.Height)
			{
				return above;
			}

			return position.Y;
		}
	}
}
