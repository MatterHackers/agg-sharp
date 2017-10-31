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
	}

	public class IgnoredPopupWidget : GuiWidget, IIgnoredPopupChild
	{
	}

	public interface IPopupLayoutEngine
	{
		double MaxHeight { get; }

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

		public PopupWidget(GuiWidget contentWidget, IPopupLayoutEngine layoutEngine, bool makeScrollable)
		{
			this.contentWidget = contentWidget;

			this.layoutEngine = layoutEngine;

			ignoredWidgets = contentWidget.Children.Where(c => c is IIgnoredPopupChild).ToList();

			if (contentWidget is IIgnoredPopupChild)
			{
				ignoredWidgets.Add(contentWidget);
			}

			if (makeScrollable)
			{
				scrollingWindow = new ScrollableWidget(true);
				{
					contentWidget.ClearRemovedFlag();
					scrollingWindow.AddChild(contentWidget);

					contentWidget.HAnchor = UI.HAnchor.Left | UI.HAnchor.Fit;
					contentWidget.VAnchor = UI.VAnchor.Bottom;
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
				Height = layoutEngine.MaxHeight;
			}

			layoutEngine.ShowPopup(this);
		}

		public Color BorderColor { get; set; }

		public int BorderWidth { get; set; }
		private List<GuiWidget> ignoredWidgets { get; }

		public virtual void CloseMenu()
		{
			this.contentWidget?.Parent?.RemoveChild(this.contentWidget);
			this.contentWidget.ClearRemovedFlag();

			this.Parent?.RemoveChild(this);
			this.Close();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			layoutEngine.Closed();

			base.OnClosed(e);
		}

		public override void OnContainsFocusChanged(EventArgs e)
		{
			UiThread.RunOnIdle(() =>
			{
				// Fired any time focus changes. Traditionally we closed the menu if the we weren't focused.
				// To accommodate children (or external widgets) having focus we also query for and consider special cases
				bool specialChildHasFocus = ignoredWidgets.Any(w => w.ContainsFocus || w.Focused)
					|| this.ChildrenRecursive<DropDownList>().Any(w => w.IsOpen);

				// If the focused changed and we've lost focus and no special cases permit, close the menu
				if (!this.ContainsFocus
					&& !specialChildHasFocus)
				{
					this.CloseMenu();
				}
			});

			base.OnContainsFocusChanged(e);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var outline = new RoundedRect(LocalBounds, 0);
			graphics2D.Render(new Stroke(outline, BorderWidth * 2), BorderColor);
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
				bool mouseUpOnIgnoredChild = ignoredWidgets.Any(w => w.MouseCaptured || w.ChildHasMouseCaptured);

				bool clickIsInsideScrollArea = (scrollingWindow?.ScrollArea?.Children?[0]?.ChildHasMouseCaptured == true);

				scrollPositionAtMouseUp = scrollingWindow.ScrollPosition;
				if (!scrollingWindow.VerticalScrollBar.ChildHasMouseCaptured
					&& AllowClickingItems()
					&& clickIsInsideScrollArea
					&& !mouseUpOnIgnoredChild)
				{
					UiThread.RunOnIdle(CloseMenu);
				}
			}

			base.OnMouseUp(mouseEvent);
		}

		public void ScrollIntoView(GuiWidget widget)
		{
			if (scrollingWindow?.VerticalScrollBar.Visible == true)
			{
				scrollingWindow.ScrollPosition = new Vector2(0, -widget.BoundsRelativeToParent.Bottom + this.Height / 2);
			}
		}

		/// <summary>
		/// Filter to allow click events as long as the scroll position is less than the given threshhold. Prevent click behavior on touch platfroms when drag scrolling
		/// </summary>
		/// <returns>A bool indicating if scroll distance is within tolerance</returns>
		internal bool AllowClickingItems()
		{
			return (scrollPositionAtMouseDown - scrollPositionAtMouseUp).Length <= 5;
		}

		internal void MakeMenuHaveScroll(double maxHeight)
		{
			if(scrollingWindow == null)
			{
				return;
			}

			scrollingWindow.VAnchor = VAnchor.Absolute;
			scrollingWindow.Height = maxHeight;
			scrollingWindow.MinimumSize = new Vector2(Width + 15, 0);
			Width = scrollingWindow.Width;
			Height = maxHeight;
			scrollingWindow.ScrollArea.VAnchor = VAnchor.Fit;
		}
	}

	public class PopupLayoutEngine : IPopupLayoutEngine
	{
		protected GuiWidget widgetRelativeTo;
		private bool alignToRightEdge;
		private GuiWidget contentWidget;
		private Direction direction;
		private bool checkIfNeedScrollBar = true;
		private HashSet<GuiWidget> hookedParents = new HashSet<GuiWidget>();
		private PopupWidget popupWidget;

		public PopupLayoutEngine(GuiWidget contentWidget, GuiWidget widgetRelativeTo, Direction direction, double maxHeight, bool alignToRightEdge)
		{
			this.MaxHeight = maxHeight;
			this.contentWidget = contentWidget;
			this.alignToRightEdge = alignToRightEdge;
			this.direction = direction;
			this.widgetRelativeTo = widgetRelativeTo;
		}

		public double MaxHeight { get; private set; }

		public void Closed()
		{
			// Unbind callbacks on parents for position_changed if we're closing
			foreach (GuiWidget widget in hookedParents)
			{
				widget.PositionChanged -= widgetRelativeTo_PositionChanged;
				widget.BoundsChanged -= widgetRelativeTo_PositionChanged;
			}

			// Long lived originating item must be unregistered
			widgetRelativeTo.Closed -= widgetRelativeTo_Closed;

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
			SystemWindow windowToAddTo = widgetRelativeTo.Parents<SystemWindow>().FirstOrDefault();
			windowToAddTo?.AddChild(popupWidget);

			GuiWidget topParent = widgetRelativeTo.Parent;
			while (topParent.Parent != null
				&& topParent as SystemWindow == null)
			{
				// Regrettably we don't know who it is that is the window that will actually think it is moving relative to its parent
				// but we need to know anytime our widgetRelativeTo has been moved by any change, so we hook them all.
				if (!hookedParents.Contains(topParent))
				{
					hookedParents.Add(topParent);
					topParent.PositionChanged += widgetRelativeTo_PositionChanged;
					topParent.BoundsChanged += widgetRelativeTo_PositionChanged;
				}

				topParent = topParent.Parent;
			}

			widgetRelativeTo_PositionChanged(widgetRelativeTo, null);
			widgetRelativeTo.Closed += widgetRelativeTo_Closed;
		}

		private void widgetRelativeTo_Closed(object sender, ClosedEventArgs e)
		{
			// If the owning widget closed, so should we
			popupWidget.CloseMenu();
		}

		private void widgetRelativeTo_PositionChanged(object sender, EventArgs e)
		{
			if (widgetRelativeTo != null
				&& widgetRelativeTo.Parent != null)
			{
				Vector2 bottomLeftScreenSpace = widgetRelativeTo.Position;
				if (alignToRightEdge)
				{
					// adjust the offset to make it align to the right edged instead
					bottomLeftScreenSpace -= new Vector2(popupWidget.Width - widgetRelativeTo.LocalBounds.Width, 0);
				}

				// actually transform it to screen space (use the parent because the position is in parent space and it would be double trasformed)
				bottomLeftScreenSpace = widgetRelativeTo.Parent.TransformToScreenSpace(bottomLeftScreenSpace);

				// we only check for the scroll bar one time (the first time we open)
				if (checkIfNeedScrollBar)
				{
					var minimumOpenHeight = 50;

					// If the bottom of the popup is below the bottom of the screen
					if (direction == Direction.Down)
					{
						if (bottomLeftScreenSpace.y - popupWidget.LocalBounds.Height < 0)
						{
							if (bottomLeftScreenSpace.y <= minimumOpenHeight)
							{
								direction = Direction.Up;
							}
							else
							{
								popupWidget.MakeMenuHaveScroll(bottomLeftScreenSpace.y - 5);
							}
						}
					}
					else
					{
						SystemWindow windowToAddTo = widgetRelativeTo.Parents<SystemWindow>().FirstOrDefault();
						if (bottomLeftScreenSpace.y + popupWidget.LocalBounds.Height > windowToAddTo.Height)
						{
							popupWidget.MakeMenuHaveScroll(bottomLeftScreenSpace.y - 5);
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
			}
		}
	}

	internal class PopupMenu : PopupWidget
	{
		private List<MenuItem> MenuItems;

		public PopupMenu(IEnumerable<MenuItem> MenuItems, GuiWidget popupContent, GuiWidget widgetRelativeTo, Direction direction, double maxHeight, bool alignToRightEdge, bool makeScrollable)
			: base(popupContent, new PopupLayoutEngine(popupContent, widgetRelativeTo, direction, maxHeight, alignToRightEdge), makeScrollable)
		{
			this.Name = "_OpenMenuContents";
			this.MenuItems = new List<MenuItem>();
			this.MenuItems.AddRange(MenuItems);

			foreach (MenuItem menu in MenuItems)
			{
				menu.AllowClicks = AllowClickingItems;
			}
		}

		public override void CloseMenu()
		{
			if (this.Parent != null)
			{
				foreach (MenuItem item in MenuItems)
				{
					item.Parent.RemoveChild(item);

					// Release reference on long lived menu items to local PopupMenu delegate
					item.AllowClicks = null;
				}
			}

			base.CloseMenu();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			foreach (MenuItem menuItem in MenuItems)
			{
				menuItem.SendToChildren(new MenuItem.MenuClosedMessage());
			}

			base.OnClosed(e);
		}
	}
}