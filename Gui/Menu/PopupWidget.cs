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

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Marker interface for ignoring mouse input on popup widget children
	/// </summary>
	public interface IIgnoredPopupChild
	{
		bool KeepMenuOpen { get; }

		bool ContainsFocus { get; }

		bool Focused { get; }
	}

	public interface ISetableIgnoredPopupChild : IIgnoredPopupChild
	{
		new bool KeepMenuOpen { get; set; }
	}

	public interface IMenuCreator
	{
		bool AlwaysKeepOpen { get; }
	}

	public class IgnoredPopupWidget : GuiWidget, IIgnoredPopupChild
	{
		public virtual bool KeepMenuOpen => false;
	}

	public interface IPopupLayoutEngine
	{
		double MaxHeight { get; }

		GuiWidget Anchor { get; }

		void Closed();

		void ShowPopup(PopupWidget popupWidget);
	}

	public class PopupWidget : GuiWidget
	{
		private GuiWidget contentWidget;
		private IPopupLayoutEngine layoutEngine;
		private ScrollableWidget scrollingWindow;
		private Vector2 scrollPositionAtMouseDown;
		private Vector2 scrollPositionAtMouseUp;
		private bool holdingOpenForChild;

		public static bool DebugKeepOpen { get; set; } = false;

		public PopupWidget(GuiWidget contentWidget, IPopupLayoutEngine layoutEngine, bool makeScrollable)
		{
			this.contentWidget = contentWidget;

			// Wear the content's corners. A hosted PopupMenu rounds itself (ThemeConfig.MenuPopupRadius) and
			// this widget's outline is drawn around it, so taking the radius from the content is what keeps
			// the outline on the panel rather than square around a rounded menu. Content that rounds nothing
			// leaves this at zero, which is what drop down lists have always drawn.
			this.BackgroundRadius = contentWidget.BackgroundRadius;

			this.layoutEngine = layoutEngine;

			IgnoredWidgets = contentWidget.Children.OfType<IIgnoredPopupChild>().ToList();

			contentWidget.Closed += (s, e) => this.Close();

			if (makeScrollable)
			{
				// A hosted PopupMenu owns the arrow keys - see PopupMenu.MenuScrollWindow. Everything else
				// (drop down lists, arbitrary popup content) keeps the plain widget's arrow-to-scroll.
				scrollingWindow = contentWidget is PopupMenu ? new PopupMenu.MenuScrollWindow() : new ScrollableWidget(true);
				{
					contentWidget.ClearRemovedFlag();
					scrollingWindow.AddChild(contentWidget);

					contentWidget.HAnchor = UI.HAnchor.Left | UI.HAnchor.Fit;
					contentWidget.VAnchor |= UI.VAnchor.Bottom; // we may have fit or absolute so or it in
					Width = contentWidget.Width;
					Height = contentWidget.Height;
				}

				scrollingWindow.HAnchor = HAnchor.Stretch;
				scrollingWindow.VAnchor = VAnchor.Stretch;
				if (layoutEngine.MaxHeight > 0 && Height > layoutEngine.MaxHeight)
				{
					MakeMenuHaveScroll(layoutEngine.MaxHeight);
				}

				this.AddChild(scrollingWindow);
			}
			else
			{
				this.AddChild(contentWidget);

				Width = contentWidget.Width;

				// Clamp height to MaxHeight if specified, otherwise content height
				Height = layoutEngine.MaxHeight > 0 ? Math.Min(layoutEngine.MaxHeight, contentWidget.Height) : contentWidget.Height;
			}

			layoutEngine.ShowPopup(this);
		}

		public int BorderWidth { get; set; }

		private List<IIgnoredPopupChild> IgnoredWidgets { get; }

		public virtual void CloseMenu()
		{
			this.contentWidget?.Parent?.RemoveChild(this.contentWidget);
			this.contentWidget.ClearRemovedFlag();

			this.Parent?.RemoveChild(this);
			this.Close();
		}

		public override void OnClosed(EventArgs e)
		{
			layoutEngine.Closed();

			base.OnClosed(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var outline = new RoundedRect(LocalBounds, 0);
			outline.radius(BackgroundRadius.SW, BackgroundRadius.SE, BackgroundRadius.NE, BackgroundRadius.NW);

			graphics2D.Render(new Stroke(outline, BorderWidth * 2 * DeviceScale), BorderColor);
		}

		/// <summary>
		/// Hands unclaimed keys to a hosted <see cref="PopupMenu"/>.
		/// </summary>
		/// <remarks>
		/// A popup opened by a button (MatterCAD's PopupButton) focuses this widget, not the menu inside it,
		/// so <see cref="GuiWidget.OnKeyDown"/> finds no focused child and the menu never sees a key. The
		/// <c>ContainsFocus</c> guard is what keeps this from delivering twice: once a menu row has focus the
		/// base implementation already routes the key through the menu.
		/// </remarks>
		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			base.OnKeyDown(keyEvent);

			if (!keyEvent.Handled
				&& contentWidget is PopupMenu popupMenu
				&& !contentWidget.ContainsFocus)
			{
				popupMenu.OnKeyDown(keyEvent);
			}
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			scrollPositionAtMouseDown = scrollingWindow == null ? Vector2.Zero : scrollingWindow.ScrollPosition;
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			scrollPositionAtMouseUp = Vector2.Zero;

			if (scrollingWindow != null)
			{
				bool specialChildHasFocus = IgnoredWidgets.Any(w => w.ContainsFocus || w.Focused || w.KeepMenuOpen);
				bool descendantIsHoldingOpen = this.Descendants<GuiWidget>().Any(w => w is IIgnoredPopupChild ignoredPopupChild
					&& ignoredPopupChild.KeepMenuOpen);
// 					&& ((ignoredPopupChild.ContainsFocus || ignoredPopupChild.KeepMenuOpen()) && !this.ContainsFocus));

				bool clickIsInsideScrollArea = scrollingWindow?.ScrollArea?.Children?.FirstOrDefault()?.ChildHasMouseCaptured == true;

				bool keepMeOpen = false;

				if (layoutEngine.Anchor is IMenuCreator menuCreator)
				{
					keepMeOpen = menuCreator.AlwaysKeepOpen;
				}

				scrollPositionAtMouseUp = scrollingWindow.ScrollPosition;
				if (!scrollingWindow.VerticalScrollBar.ChildHasMouseCaptured
					&& AllowClickingItems()
					&& clickIsInsideScrollArea
					&& !specialChildHasFocus
					&& !descendantIsHoldingOpen
					&& !holdingOpenForChild
					&& !keepMeOpen
					&& !DebugKeepOpen)
				{
					UiThread.RunOnIdle(this.CloseMenu);
				}
			}

			base.OnMouseUp(mouseEvent);
		}

		public override void OnContainsFocusChanged(FocusChangedArgs e)
		{
			if (!e.Focused)
			{
				bool reclaimFocus = false;

				if (holdingOpenForChild)
				{
					holdingOpenForChild = false;
					reclaimFocus = true;
				}

				UiThread.RunOnIdle(() =>
				{
					// Fired any time focus changes. Traditionally we closed the menu if we weren't focused.
					// To accommodate children (or external widgets) having focus we also query for and consider special cases
					bool specialChildHasFocus = IgnoredWidgets.Any(w => w.ContainsFocus || w.Focused || w.KeepMenuOpen);
					bool descendantIsHoldingOpen = this.Descendants<GuiWidget>().Any(w => w is IIgnoredPopupChild ignoredPopupChild
						&& ignoredPopupChild.KeepMenuOpen);

					bool keepMeOpen = false;

					if (layoutEngine.Anchor is IMenuCreator menuCreator)
					{
						keepMeOpen = menuCreator.AlwaysKeepOpen;
					}

					// If the focused changed and we've lost focus and no special cases permit, close the menu
					if (!this.ContainsFocus
							&& !specialChildHasFocus
							&& !descendantIsHoldingOpen
							&& !holdingOpenForChild
							&& !keepMeOpen
							&& !DebugKeepOpen)
					{
						this.CloseMenu();
					}
					else if (reclaimFocus && !descendantIsHoldingOpen)
					{
						this.Focus();
					}

					holdingOpenForChild = descendantIsHoldingOpen;
				});
			}

			base.OnContainsFocusChanged(e);
		}


		/// <summary>
		/// Brings <paramref name="widget"/> into the popup's viewport, moving it as little as possible and
		/// not at all when the widget is already fully visible.
		/// </summary>
		/// <remarks>
		/// This defers to <see cref="ScrollableWidget.ScrollIntoView"/> rather than computing an offset of its
		/// own. The hand rolled version this replaced re-centered the list unconditionally, which is wrong for
		/// anything that calls this per keystroke: arrowing down a long menu snapped the whole list on every
		/// press, and a row that was already on screen was recentered for no reason. It also left the two
		/// scrollers a menu can end up in disagreeing, since the one <c>MakeMenuHaveScroll</c> builds has
		/// always done the minimum scroll.
		/// </remarks>
		/// <param name="widget">The descendant to reveal.</param>
		/// <param name="scrollAmount">
		/// How far to move. <see cref="ScrollableWidget.ScrollAmount.Center"/> is for the one-off case of
		/// revealing a selection as a list opens, where landing in the middle shows what is on either side of
		/// it; repeated navigation wants the default minimum.
		/// </param>
		public void ScrollIntoView(GuiWidget widget, ScrollableWidget.ScrollAmount scrollAmount = ScrollableWidget.ScrollAmount.Minimum)
		{
			scrollingWindow?.ScrollIntoView(widget, scrollAmount);
		}

		/// <summary>
		/// Filter to allow click events as long as the scroll position is less than the given threshold. Prevent click behavior on touch platforms when drag scrolling
		/// </summary>
		/// <returns>A bool indicating if scroll distance is within tolerance</returns>
		internal bool AllowClickingItems()
		{
			return (scrollPositionAtMouseDown - scrollPositionAtMouseUp).Length <= 5;
		}

		internal void MakeMenuHaveScroll(double maxHeight)
		{
			if (scrollingWindow == null)
			{
				return;
			}

			scrollingWindow.VAnchor = VAnchor.Absolute;
			scrollingWindow.Height = maxHeight;
			// leave room for the scroll bar the caller is about to get
			scrollingWindow.MinimumSize = new Vector2(Width + ScrollBar.ScrollBarWidth, 0);
			Width = scrollingWindow.Width;
			Height = maxHeight;
			scrollingWindow.ScrollArea.VAnchor = VAnchor.Fit;

			TakeOverContentFill();
		}

		/// <summary>
		/// Moves the content's background fill onto this widget, once scrolling has widened us past it.
		/// </summary>
		/// <remarks>
		/// A hosted <see cref="PopupMenu"/> paints the panel itself and stays <c>HAnchor.Left | Fit</c>, so it
		/// keeps its pre-scroll width while we grow by a scroll bar. <see cref="OnDraw"/> traces the rounded
		/// border on *our* bounds, so leaving the fill where it is draws two rounded edges a scroll bar apart.
		/// The content is also inside the scroll area, so its fill would slide away from the border as the menu
		/// is scrolled. Only one widget can carry the fill and it has to be the one the border is traced on.
		/// Content that paints no fill (a drop down list's item column - the container fills for it) is left
		/// alone.
		/// </remarks>
		private void TakeOverContentFill()
		{
			if (contentWidget.BackgroundColor.Alpha0To255 == 0)
			{
				return;
			}

			this.BackgroundColor = contentWidget.BackgroundColor;
			this.BackgroundRadius = contentWidget.BackgroundRadius;

			contentWidget.BackgroundColor = Color.Transparent;
		}
	}

	public class PopupLayoutEngine : IPopupLayoutEngine
	{
		protected GuiWidget widgetRelativeTo;
		private bool alignToRightEdge;
		private GuiWidget contentWidget;
		private Direction direction;
		private bool checkIfNeedScrollBar = true;
		private HashSet<GuiWidget> monitoredWidgets = new HashSet<GuiWidget>();
		private PopupWidget popupWidget;
		private SystemWindow windowToAddTo;

		public PopupLayoutEngine(GuiWidget contentWidget, GuiWidget widgetRelativeTo, Direction direction, double maxHeight, bool alignToRightEdge)
		{
			this.MaxHeight = maxHeight;
			this.contentWidget = contentWidget;
			this.alignToRightEdge = alignToRightEdge;
			this.direction = direction;
			this.widgetRelativeTo = widgetRelativeTo;
		}

		public GuiWidget Anchor => widgetRelativeTo;

		public double MaxHeight { get; private set; }

		public void Closed()
		{
			// Unbind callbacks on parents for position_changed if we're closing
			foreach (GuiWidget widget in monitoredWidgets)
			{
				widget.PositionChanged -= RecalculatePosition;
				widget.BoundsChanged -= RecalculatePosition;
			}

			// Long lived originating item must be unregistered
			widgetRelativeTo.Closed -= WidgetRelativeTo_Closed;

			// Restore focus to originating widget on close
			if (this.widgetRelativeTo != null
				&& !widgetRelativeTo.HasBeenClosed)
			{
				// On menu close, select the first scrollable parent of the widgetRelativeTo
				var scrollableParent = widgetRelativeTo.Parents<ScrollableWidget>().FirstOrDefault();
				if (scrollableParent != null)
				{
					scrollableParent.Focus();
				}
			}
		}

		public void ShowPopup(PopupWidget popupWidget)
		{
			this.popupWidget = popupWidget;
			windowToAddTo = widgetRelativeTo.Parents<SystemWindow>().LastOrDefault();
			windowToAddTo?.AddChild(popupWidget);

			monitoredWidgets.Clear();

			monitoredWidgets.Add(popupWidget);
			popupWidget.PositionChanged += RecalculatePosition;
			popupWidget.BoundsChanged += RecalculatePosition;

			// Iterate until the first SystemWindow is found
			GuiWidget topParent = widgetRelativeTo.Parent;
			while (topParent.Parent != null
				&& topParent as SystemWindow == null)
			{
				// Regrettably we don't know who it is that is the window that will actually think it is moving relative to its parent
				// but we need to know anytime our widgetRelativeTo has been moved by any change, so we hook them all.
				if (!monitoredWidgets.Contains(topParent))
				{
					monitoredWidgets.Add(topParent);
					topParent.PositionChanged += RecalculatePosition;
					topParent.BoundsChanged += RecalculatePosition;
				}

				topParent = topParent.Parent;
			}

			RecalculatePosition(widgetRelativeTo, null);
			widgetRelativeTo.Closed += WidgetRelativeTo_Closed;
		}

		private void WidgetRelativeTo_Closed(object sender, EventArgs e)
		{
			// If the owning widget closed, so should we
			popupWidget.CloseMenu();
		}

		private int recursCount = 0;

		private void RecalculatePosition(object sender, EventArgs e)
		{
			if (recursCount == 0
				&& widgetRelativeTo != null
				&& widgetRelativeTo.Parent != null)
			{
				recursCount++;

				var systemWindowWidth = windowToAddTo.Width;

				Vector2 bottomLeftScreenSpace;

				// Calculate left aligned screen space position (using widgetRelativeTo.parent)
				Vector2 alignLeftPosition = widgetRelativeTo.Parent.TransformToScreenSpace(widgetRelativeTo.Position);

				// Calculate right aligned screen space position (using widgetRelativeTo.parent)
				var bottomLeftForAlignRight = widgetRelativeTo.Position - new Vector2(popupWidget.Width - widgetRelativeTo.LocalBounds.Width, 0);
				Vector2 alignRightPosition = widgetRelativeTo.Parent.TransformToScreenSpace(bottomLeftForAlignRight);

				// Conditionally select appropriate left/right position
				if (alignToRightEdge
					&& alignRightPosition.X >= 0
					|| alignLeftPosition.X + popupWidget.Width > systemWindowWidth)
				{
					// Align right or align left with x > systemWindow.Width
					bottomLeftScreenSpace = alignRightPosition;
				}
				else
				{
					// Align left or align right with negative x
					bottomLeftScreenSpace = alignLeftPosition;
				}

				// we only check for the scroll bar one time (the first time we open)
				if (checkIfNeedScrollBar)
				{
					// Opening Down puts the popup between the bottom of the anchor and the bottom of the window,
					// opening Up puts it between the top of the anchor and the top of the window. Measure both,
					// then prefer the requested direction, fall back to the other one, and only squeeze in a
					// scroll bar when the popup fits in neither. windowToAddTo is the window the popup was
					// actually added to (the outermost SystemWindow), which is the space these screen space
					// coordinates are expressed in - a nearer SystemWindow ancestor would give the wrong height.
					var spaceBelow = bottomLeftScreenSpace.Y;
					var spaceAbove = windowToAddTo.Height - (bottomLeftScreenSpace.Y + widgetRelativeTo.Height);
					var neededHeight = popupWidget.LocalBounds.Height;

					var preferredSpace = direction == Direction.Down ? spaceBelow : spaceAbove;
					var oppositeSpace = direction == Direction.Down ? spaceAbove : spaceBelow;

					if (neededHeight > preferredSpace)
					{
						if (neededHeight <= oppositeSpace)
						{
							direction = direction == Direction.Down ? Direction.Up : Direction.Down;
						}
						else
						{
							// It fits nowhere, so open toward whichever side has more room (keeping the
							// preferred direction on a tie) and scroll within that space
							if (oppositeSpace > preferredSpace)
							{
								direction = direction == Direction.Down ? Direction.Up : Direction.Down;
							}

							popupWidget.MakeMenuHaveScroll(Math.Max(preferredSpace, oppositeSpace) - 5);
						}
					}

					// We only check the first time we position the popup
					checkIfNeedScrollBar = false;
				}

				switch (direction)
				{
					case Direction.Down:
						popupWidget.Position = bottomLeftScreenSpace + new Vector2(0, -popupWidget.Height);
						break;

					case Direction.Up:
						popupWidget.Position = bottomLeftScreenSpace + new Vector2(0, widgetRelativeTo.Height);
						break;

					default:
						throw new NotImplementedException();
				}
				recursCount--;
			}
		}
	}

	internal class DropDownContainer : PopupWidget
	{
		private readonly List<MenuItem> menuItems;

		public DropDownContainer(IEnumerable<MenuItem> menuItems, GuiWidget popupContent, GuiWidget widgetRelativeTo, Direction direction, double maxHeight, bool alignToRightEdge, bool makeScrollable)
			: base(popupContent, new PopupLayoutEngine(popupContent, widgetRelativeTo, direction, maxHeight, alignToRightEdge), makeScrollable)
		{
			this.Name = "_OpenMenuContents";
			this.menuItems = new List<MenuItem>();
			this.menuItems.AddRange(menuItems);

			foreach (MenuItem menu in menuItems)
			{
				menu.AllowClicks = AllowClickingItems;
			}
		}

		public override void CloseMenu()
		{
			if (this.Parent != null)
			{
				foreach (MenuItem item in menuItems)
				{
					item.Parent.RemoveChild(item);

					// Release reference on long lived menu items to local PopupMenu delegate
					item.AllowClicks = null;
				}
			}

			base.CloseMenu();
		}

		public override void OnClosed(EventArgs e)
		{
			foreach (MenuItem menuItem in menuItems)
			{
				menuItem.SendToChildren(new MenuItem.MenuClosedMessage());
			}

			base.OnClosed(e);
		}
	}
}