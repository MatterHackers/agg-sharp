using System;
using System.Collections.Generic;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.UI
{
	internal class PopupMenu : PopupWidget
	{
		private List<MenuItem> MenuItems;

		public PopupMenu(IEnumerable<MenuItem> MenuItems, GuiWidget widgetRelativeTo, Vector2 openOffset, Direction direction, double maxHeight, bool alignToRightEdge)
			: base(MenuItems, widgetRelativeTo, openOffset, direction, maxHeight, alignToRightEdge)
		{
			this.Name = "_OpenMenuContents";
			this.MenuItems = new List<MenuItem>();
			this.MenuItems.AddRange(MenuItems);
		}

		internal override void CloseMenu()
		{
			if (this.Parent != null)
			{
				foreach (MenuItem item in MenuItems)
				{
					item.Parent.RemoveChild(item);
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

		private bool alignToRightEdge;
		private Direction direction;
		private GuiWidget widgetRelativeTo;
		
		private Vector2 openOffset;
		private ScrollableWidget scrollingWindow;

		public PopupWidget(IEnumerable<GuiWidget> childrenToAdd, GuiWidget widgetRelativeTo, Vector2 openOffset, Direction direction, double maxHeight, bool alignToRightEdge)
		{
			this.alignToRightEdge = alignToRightEdge;
			this.openOffset = openOffset;

			this.direction = direction;
			this.widgetRelativeTo = widgetRelativeTo;
			scrollingWindow = new ScrollableWidget(true);
			{
				var topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom)
				{
					Name = "_topToBottom",
				};

				foreach (var widget in childrenToAdd)
				{
					var menu = widget as MenuItem;
					menu?.ClearRemovedFlag();

					topToBottom.AddChild(widget);

					if (widget is MenuItem)
					{
						menu.AllowClicks = AllowClickingItems;
					}
				}

				topToBottom.HAnchor = UI.HAnchor.ParentLeft | UI.HAnchor.FitToChildren;
				topToBottom.VAnchor = UI.VAnchor.ParentBottom;
				Width = topToBottom.Width;
				Height = topToBottom.Height;

				scrollingWindow.AddChild(topToBottom);
			}

			scrollingWindow.HAnchor = HAnchor.ParentLeftRight;
			scrollingWindow.VAnchor = VAnchor.ParentBottomTop;
			if (maxHeight > 0 && Height > maxHeight)
			{
				MakeMenuHaveScroll(maxHeight);
			}
			AddChild(scrollingWindow);

			ContainsFocusChanged += DropListItems_ContainsFocusChanged;

			GuiWidget topParent = widgetRelativeTo.Parent;
			while (topParent.Parent != null
				&& topParent as SystemWindow == null)
			{
				// Regrettably we don't know who it is that is the window that will actually think it is moving relative to its parent
				// but we need to know anytime our widgetRelativeTo has been moved by any change, so we hook them all.

				if (!widgetRefList.Contains(topParent))
				{
					widgetRefList.Add(topParent);
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

		private void widgetRelativeTo_Closed(object sender, ClosedEventArgs e)
		{
			widgetRelativeTo.Closed -= widgetRelativeTo_Closed;
			widgetRelativeTo = null;
			UnbindCallbacks();
			DropListItems_ContainsFocusChanged(null, null);
		}

		private HashSet<GuiWidget> widgetRefList = new HashSet<GuiWidget>();

		public override void OnClosed(ClosedEventArgs e)
		{
			UnbindCallbacks();
			base.OnClosed(e);
		}

		private void UnbindCallbacks()
		{
			foreach (GuiWidget widget in widgetRefList)
			{
				widget.PositionChanged -= new EventHandler(widgetRelativeTo_PositionChanged);
				widget.BoundsChanged -= new EventHandler(widgetRelativeTo_PositionChanged);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			base.OnDraw(graphics2D);

			var outline = new RoundedRect(LocalBounds, 0);
			graphics2D.Render(new Stroke(outline, BorderWidth * 2), BorderColor);
		}

		bool firstTimeSizing = true;
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
					var minHeight = 25;
					double distanceToWindowBottom = zero.y - Height;
					if (distanceToWindowBottom < 0)
					{
						if (Height + distanceToWindowBottom > minHeight)
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

		private Vector2 positionAtMouseDown;
		private Vector2 positionAtMouseUp;

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			positionAtMouseDown = scrollingWindow.ScrollPosition;
			base.OnMouseDown(mouseEvent);
		}

		internal bool AllowClickingItems()
		{
			if ((positionAtMouseDown - positionAtMouseUp).Length > 5)
			{
				return false;
			}

			return true;
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			bool clickWasOnValidMenuItem = true;
			if(scrollingWindow?.ScrollArea?.Children?[0]?.ChildHasMouseCaptured == false)
			{
				clickWasOnValidMenuItem = false;
			}
			positionAtMouseUp = scrollingWindow.ScrollPosition;
			if (!scrollingWindow.VerticalScrollBar.ChildHasMouseCaptured
				&& AllowClickingItems()
				&& clickWasOnValidMenuItem)
			{
				UiThread.RunOnIdle(CloseMenu);
			}
			base.OnMouseUp(mouseEvent);
		}

		internal virtual void CloseMenu()
		{
			if (this.Parent != null)
			{
				this.Parent.RemoveChild(this);
				this.Close();
			}
		}

		internal void DropListItems_ContainsFocusChanged(object sender, EventArgs e)
		{
			var widget = sender as GuiWidget;
			if (widget == null
				|| !widget.Focused)
			{
				UiThread.RunOnIdle(CloseMenu);
			}
		}
	}
}