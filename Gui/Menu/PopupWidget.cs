using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	internal class PopupMenu : PopupWidget
	{
		private List<MenuItem> MenuItems;

		public PopupMenu(IEnumerable<MenuItem> MenuItems, GuiWidget popupContent, GuiWidget widgetRelativeTo, Vector2 openOffset, Direction direction, double maxHeight, bool alignToRightEdge)
			: base(popupContent, widgetRelativeTo, openOffset, direction, maxHeight, alignToRightEdge)
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

	public class PopupWidget : GuiWidget
	{
		public RGBA_Bytes BorderColor { get; set; }

		public int BorderWidth { get; set; }

		private bool firstTimeSizing = true;
		private bool alignToRightEdge;

		private Direction direction;
		
		private Vector2 openOffset;
		
		private Vector2 scrollPositionAtMouseDown;
		private Vector2 scrollPositionAtMouseUp;

		protected GuiWidget widgetRelativeTo;
		private GuiWidget contentWidget;
		private ScrollableWidget scrollingWindow;
		private List<GuiWidget> ignoredWidgets { get; }
		private HashSet<GuiWidget> hookedParents = new HashSet<GuiWidget>();

		public PopupWidget(GuiWidget contentWidget, GuiWidget widgetRelativeTo, Vector2 openOffset, Direction direction, double maxHeight, bool alignToRightEdge)
		{
			this.alignToRightEdge = alignToRightEdge;
			this.openOffset = openOffset;
			this.contentWidget = contentWidget;

			ignoredWidgets = contentWidget.Children.Where(c => c is IIgnoredPopupChild).ToList();

			if (contentWidget is IIgnoredPopupChild)
			{
				ignoredWidgets.Add(contentWidget);
			}

			this.direction = direction;
			this.widgetRelativeTo = widgetRelativeTo;

			scrollingWindow = new ScrollableWidget(true);
			{
				contentWidget.ClearRemovedFlag();
				scrollingWindow.AddChild(contentWidget);

				contentWidget.HAnchor = UI.HAnchor.ParentLeft | UI.HAnchor.FitToChildren;
				contentWidget.VAnchor = UI.VAnchor.ParentBottom;
				Width = contentWidget.Width;
				Height = contentWidget.Height;
			}

			scrollingWindow.HAnchor = HAnchor.ParentLeftRight;
			scrollingWindow.VAnchor = VAnchor.ParentBottomTop;
			if (maxHeight > 0 && Height > maxHeight)
			{
				MakeMenuHaveScroll(maxHeight);
			}
			AddChild(scrollingWindow);

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
			topParent.AddChild(this);

			widgetRelativeTo_PositionChanged(widgetRelativeTo, null);
			widgetRelativeTo.Closed += widgetRelativeTo_Closed;
		}

		public void ScrollIntoView(GuiWidget widget)
		{
			if (scrollingWindow.VerticalScrollBar.Visible)
			{
				scrollingWindow.ScrollPosition = new Vector2(0, -widget.BoundsRelativeToParent.Bottom + this.Height /2);
			}
		}

		private void MakeMenuHaveScroll(double maxHeight)
		{
			scrollingWindow.VAnchor = VAnchor.AbsolutePosition;
			scrollingWindow.Height = maxHeight;
			scrollingWindow.MinimumSize = new Vector2(Width + 15, 0);
			Width = scrollingWindow.Width;
			Height = maxHeight;
			scrollingWindow.ScrollArea.VAnchor = VAnchor.FitToChildren;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var outline = new RoundedRect(LocalBounds, 0);
			graphics2D.Render(new Stroke(outline, BorderWidth * 2), BorderColor);
		}

		private void widgetRelativeTo_PositionChanged(object sender, EventArgs e)
		{
			if (widgetRelativeTo != null)
			{
				Vector2 zero = widgetRelativeTo.OriginRelativeParent;
				if (alignToRightEdge)
				{
					zero += new Vector2(widgetRelativeTo.LocalBounds.Left, widgetRelativeTo.LocalBounds.Bottom);
				}

				GuiWidget topParent = widgetRelativeTo.Parent;
				while (topParent != null && topParent.Parent != null)
				{
					topParent.ParentToChildTransform.transform(ref zero);
					topParent = topParent.Parent;
				}

				double alignmentOffset = 0;
				if (alignToRightEdge)
				{
					alignmentOffset = -Width + widgetRelativeTo.Width;
				}

				if (firstTimeSizing)
				{
					var maxAllowed = 25;
					double distanceToWindowBottom = zero.y - Height;
					if (distanceToWindowBottom < 0)
					{
						if (Height + distanceToWindowBottom > maxAllowed)
						{
							MakeMenuHaveScroll(Height + distanceToWindowBottom - 5);
						}
						else
						{
							direction = Direction.Up;
						}
					}
				}

				switch (direction)
				{
					case Direction.Down:
						this.OriginRelativeParent = zero + new Vector2(alignmentOffset, -Height) + openOffset;
						break;

					case Direction.Up:
						this.OriginRelativeParent = zero + new Vector2(alignmentOffset, widgetRelativeTo.Height) + openOffset;
						break;

					default:
						throw new NotImplementedException();
				}
			}

			firstTimeSizing = false;
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			scrollPositionAtMouseDown = scrollingWindow.ScrollPosition;
			base.OnMouseDown(mouseEvent);
		}

		/// <summary>
		/// Filter to allow click events as long as the scroll position is less than the given threshhold. Prevent click behavior on touch platfroms when drag scrolling
		/// </summary>
		/// <returns>A bool indicating if scroll distance is within tolerance</returns>
		internal bool AllowClickingItems()
		{
			return (scrollPositionAtMouseDown - scrollPositionAtMouseUp).Length <= 5;
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
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
			base.OnMouseUp(mouseEvent);
		}

		public virtual void CloseMenu()
		{
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

			this.contentWidget?.Parent?.RemoveChild(this.contentWidget);
			this.contentWidget.ClearRemovedFlag();

			this.Parent?.RemoveChild(this);
			this.Close();
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
					UiThread.RunOnIdle(CloseMenu);
				}
			});

			base.OnContainsFocusChanged(e);
		}

		private void widgetRelativeTo_Closed(object sender, ClosedEventArgs e)
		{
			// If the owning widget closed, so should we
			this.CloseMenu();
		}

		public override void OnClosed(ClosedEventArgs e)
		{
			// Unbind callbacks on parents for position_changed if we're closing
			foreach (GuiWidget widget in hookedParents)
			{
				widget.PositionChanged -= widgetRelativeTo_PositionChanged;
				widget.BoundsChanged -= widgetRelativeTo_PositionChanged;
			}

			// Long lived originating item must be unregistered
			widgetRelativeTo.Closed -= widgetRelativeTo_Closed;

			base.OnClosed(e);
		}
	}

	/// <summary>
	/// Marker interface for ignoring mouse input on popup widget children
	/// </summary>
	public interface IIgnoredPopupChild
	{
	}
}