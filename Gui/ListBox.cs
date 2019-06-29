using MatterHackers.VectorMath;
using System;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public class ListBoxTextItem : TextWidget
	{
		public string ItemValue { get; set; }

		public ListBoxTextItem(string displayName, string itemValue)
			: base(displayName)
		{
			Padding = new BorderDouble(3);
			ItemValue = itemValue;
			MinimumSize = new Vector2(Width, Height);
		}
	}

	public class ListBox : ScrollableWidget
	{
		public event EventHandler SelectedValueChanged;

		public event EventHandler HoverValueChanged;

		protected FlowLayoutWidget topToBottomItemList;

		private Color hoverColor = new Color(205, 205, 255, 255);
		private Color selectedColor = new Color(105, 105, 255, 255);

		private int selectedIndex = -1;
		private int hoverIndex = -1;
		private int dragIndex = -1;

		public int Count
		{
			get
			{
				return topToBottomItemList.Children.Count;
			}
		}

		public int SelectedIndex
		{
			get
			{
				return selectedIndex;
			}

			set
			{
				if (value < -1 || value >= topToBottomItemList.Children.Count)
				{
					throw new ArgumentOutOfRangeException();
				}

				if (value != selectedIndex)
				{
					selectedIndex = value;
					OnSelectedIndexChanged();

					int index = 0;
					foreach (GuiWidget child in topToBottomItemList.Children)
					{
						if (index == selectedIndex)
						{
							child.BackgroundColor = selectedColor;
							SelectedIndex = index;
						}
						else
						{
							child.BackgroundColor = new Color();
						}

						child.Invalidate();
						index++;
					}

					Invalidate();
				}
			}
		}

		public int DragIndex
		{
			get
			{
				return dragIndex;
			}
			set
			{
				if (value < -1 || value >= topToBottomItemList.Children.Count)
				{
					throw new ArgumentOutOfRangeException();
				}

				if (value != dragIndex)
				{
					dragIndex = value;
				}
			}
		}

		public int HoverIndex
		{
			get
			{
				return hoverIndex;
			}
			set
			{
				if (value < -1 || value >= topToBottomItemList.Children.Count)
				{
					throw new ArgumentOutOfRangeException();
				}

				if (value != hoverIndex)
				{
					hoverIndex = value;
					OnHoverIndexChanged();

					Color noneColor = new Color();
					var index = 0;
					foreach (GuiWidget child in topToBottomItemList.Children)
					{
						if (index != SelectedIndex)
						{
							if (index == HoverIndex)
							{
								HoverIndex = index;
								child.BackgroundColor = hoverColor;
							}
							else if (child.BackgroundColor != noneColor)
							{
								child.BackgroundColor = noneColor;
							}
							child.Invalidate();
						}
						index++;
					}

					Invalidate();
				}
			}
		}

		public ListBox(RectangleDouble bounds)
		{
			AutoScroll = true;
			LocalBounds = new RectangleDouble(0, 0, bounds.Width, bounds.Height);
			topToBottomItemList = new FlowLayoutWidget(FlowDirection.TopToBottom);
			topToBottomItemList.HAnchor = UI.HAnchor.Left | UI.HAnchor.Fit;
			base.AddChild(topToBottomItemList);
		}

		public ListBox()
			: this(new RectangleDouble())
		{
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			FlowLayoutWidget itemHolder = new FlowLayoutWidget();
			itemHolder.Name = "list item holder";
			itemHolder.Margin = new BorderDouble(3, 0, 0, 0);
			itemHolder.HAnchor = UI.HAnchor.Stretch | UI.HAnchor.Fit;
			itemHolder.AddChild(child);
			//itemHolder.FitToChildren();
			topToBottomItemList.AddChild(itemHolder, indexInChildrenList);

			itemHolder.MouseEnterBounds += itemToAdd_MouseEnterBounds;
			itemHolder.MouseLeaveBounds += itemToAdd_MouseLeaveBounds;
			itemHolder.MouseDown += itemHolder_MouseDownInBounds;
			itemHolder.ParentChanged += itemHolder_ParentChanged;
		}

		private bool settingLocalBounds = false;

		public override RectangleDouble LocalBounds
		{
			set
			{
				if (!settingLocalBounds)
				{
					Vector2 currentTopLeftOffset = new Vector2();
					if (Parent != null)
					{
						currentTopLeftOffset = TopLeftOffset;
					}
					settingLocalBounds = true;
					if (topToBottomItemList != null)
					{
						topToBottomItemList.Width = Math.Max(0, value.Width - ScrollArea.Padding.Width - topToBottomItemList.Margin.Width - VerticalScrollBar.Width);
					}

					base.LocalBounds = value;
					if (Parent != null)
					{
						TopLeftOffset = currentTopLeftOffset;
					}
					settingLocalBounds = false;
				}
			}
		}

		public override void RemoveChild(int index)
		{
			topToBottomItemList.RemoveChild(index);
		}

		public override void RemoveChild(GuiWidget childToRemove)
		{
			foreach (GuiWidget itemHolder in topToBottomItemList.Children.Reverse())
			{
				if (itemHolder == childToRemove || itemHolder.Children.FirstOrDefault() == childToRemove)
				{
					topToBottomItemList.RemoveChild(itemHolder);
				}
			}
		}

		private void itemHolder_ParentChanged(object sender, EventArgs e)
		{
			FlowLayoutWidget itemHolder = (FlowLayoutWidget)sender;
			itemHolder.MouseEnterBounds -= itemToAdd_MouseEnterBounds;
			itemHolder.MouseLeaveBounds -= itemToAdd_MouseLeaveBounds;
			itemHolder.MouseDown -= itemHolder_MouseDownInBounds;
			itemHolder.ParentChanged -= itemHolder_ParentChanged;
		}

		private void itemHolder_MouseDownInBounds(object sender, MouseEventArgs mouseEvent)
		{
			GuiWidget widgetClicked = ((GuiWidget)sender);
			var index = 0;
			foreach (GuiWidget child in topToBottomItemList.Children)
			{
				if (child == widgetClicked)
				{
					SelectedIndex = index;
				}
				index++;
			}
		}

		private void itemToAdd_MouseLeaveBounds(object sender, EventArgs e)
		{
			GuiWidget widgetLeft = ((GuiWidget)sender);
			if (SelectedIndex >= 0)
			{
				topToBottomItemList.Children.ReadOnly((list) =>
				{
					if (widgetLeft != list[SelectedIndex])
					{
						widgetLeft.BackgroundColor = default(Color);
						widgetLeft.Invalidate();
						Invalidate();
					}
				});
			}
		}

		private void itemToAdd_MouseEnterBounds(object sender, EventArgs e)
		{
			var widgetEntered = (GuiWidget)sender;
			int index = 0;
			foreach (var child in topToBottomItemList.Children)
			{
				if (index != SelectedIndex)
				{
					if (child == widgetEntered)
					{
						HoverIndex = index;
					}
				}

				index++;
			}
		}

		public void OnSelectedIndexChanged()
		{
			Invalidate();
			if (SelectedValueChanged != null)
			{
				SelectedValueChanged(this, null);
			}
		}

		public void OnHoverIndexChanged()
		{
			Invalidate();
			if (HoverValueChanged != null)
			{
				HoverValueChanged(this, null);
			}
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			//activeView.OnDraw(graphics2D);

			base.OnDraw(graphics2D);
		}

		public override void OnMouseDown(MouseEventArgs mouseEvent)
		{
			base.OnMouseDown(mouseEvent);
		}

		public override void OnMouseUp(MouseEventArgs mouseEvent)
		{
			base.OnMouseUp(mouseEvent);
		}

		public override void OnMouseMove(MouseEventArgs mouseEvent)
		{
			base.OnMouseMove(mouseEvent);
		}

		public void ClearSelected()
		{
			if (selectedIndex != -1)
			{
				selectedIndex = -1;
				OnSelectedIndexChanged();
			}
		}

		public GuiWidget SelectedItem
		{
			get
			{
				if (SelectedIndex != -1)
				{
					GuiWidget child = null;
					Children.ReadOnly((list) =>
					{
						child = list[SelectedIndex];
					});

					return child;
				}

				return null;
			}

			set
			{
				var index = 0;
				foreach (var child in Children)
				{
					if (child == value)
					{
						SelectedIndex = index;
					}

					index++;
				}
			}
		}
	}
}