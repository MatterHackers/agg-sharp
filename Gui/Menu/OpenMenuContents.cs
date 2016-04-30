using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;

namespace MatterHackers.Agg.UI
{
	internal class OpenMenuContents : GuiWidget
	{
		private bool alignToRightEdge;
		private Direction direction;
		private GuiWidget widgetRelativeTo;
		private RGBA_Bytes borderColor;
		private int borderWidth;
		private Vector2 openOffset;
		private ScrollableWidget scrollingWindow;

		private List<MenuItem> MenuItems;

		internal OpenMenuContents(ObservableCollection<MenuItem> MenuItems, GuiWidget widgetRelativeTo, Vector2 openOffset, Direction direction, RGBA_Bytes backgroundColor, RGBA_Bytes borderColor, int borderWidth, double maxHeight, bool alignToRightEdge)
		{
			this.MenuItems = new List<MenuItem>();
			this.MenuItems.AddRange(MenuItems);
			this.alignToRightEdge = alignToRightEdge;
			this.openOffset = openOffset;
			this.borderWidth = borderWidth;
			this.borderColor = borderColor;
			this.BackgroundColor = backgroundColor;

			this.direction = direction;
			this.widgetRelativeTo = widgetRelativeTo;
			scrollingWindow = new ScrollableWidget(true);
			{
				FlowLayoutWidget topToBottom = new FlowLayoutWidget(FlowDirection.TopToBottom);
				foreach (MenuItem menu in MenuItems)
				{
					menu.ClearRemovedFlag();
					topToBottom.AddChild(menu);
					menu.AllowClicks = AllowClickingItems;
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

			LostFocus += DropListItems_LostFocus;

			GuiWidget topParent = widgetRelativeTo.Parent;
			while (topParent.Parent != null
				&& topParent as SystemWindow == null)
			{
				// Regrettably we don't know who it is that is the window that will actually think it is moving relative to its parent
				// but we need to know anytime our widgetRelativeTo has been moved by any change, so we hook them all.

				if (!widgetRefList.Contains(topParent))
				{
					widgetRefList.Add(topParent);
					topParent.PositionChanged += new EventHandler(widgetRelativeTo_PositionChanged);
					topParent.BoundsChanged += new EventHandler(widgetRelativeTo_PositionChanged);
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

		private void widgetRelativeTo_Closed(object sender, EventArgs e)
		{
			widgetRelativeTo.Closed -= widgetRelativeTo_Closed;
			widgetRelativeTo = null;
			UnbindCallbacks();
			DropListItems_LostFocus(null, null);
		}

		private HashSet<GuiWidget> widgetRefList = new HashSet<GuiWidget>();

		public override void OnClosed(EventArgs e)
		{
			foreach (MenuItem menuItem in MenuItems)
			{
				menuItem.SendToChildren(new MenuItem.MenuClosedMessage());
			}

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

			RoundedRect outlineRect = new RoundedRect(LocalBounds, 0);
			Stroke wideOutline = new Stroke(outlineRect, borderWidth * 2);
			graphics2D.Render(wideOutline, borderColor);
		}

		bool firstTimeSizing = true;
		private void widgetRelativeTo_PositionChanged(object sender, EventArgs e)
		{
			if (widgetRelativeTo != null)
			{
				Vector2 zero = widgetRelativeTo.OriginRelativeParent;
				zero += new Vector2(widgetRelativeTo.LocalBounds.Left, widgetRelativeTo.LocalBounds.Bottom);

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
					double distanceToWindowBottom = zero.y - Height;
					if (distanceToWindowBottom < 0)
					{
						if (MenuItems.Count > 0 
							&& MenuItems[0].Height > 10
							&& Height + distanceToWindowBottom > MenuItems[0].Height * 3)
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

		private void CloseMenu()
		{
			if (this.Parent != null)
			{
				foreach (MenuItem item in MenuItems)
				{
					item.Parent.RemoveChild(item);
				}

				this.Parent.RemoveChild(this);
				this.Close();
			}
		}

		internal void DropListItems_LostFocus(object sender, EventArgs e)
		{
			UiThread.RunOnIdle(CloseMenu);
		}
	}
}