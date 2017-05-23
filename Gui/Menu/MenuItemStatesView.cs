using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public class MenuItemStatesView : GuiWidget
	{
		private GuiWidget normalState;
		private GuiWidget overState;

		public MenuItemStatesView(GuiWidget normalState, GuiWidget overState)
		{
			overState.HAnchor |= HAnchor.ParentLeftRight;
			normalState.HAnchor |= HAnchor.ParentLeftRight;
			HAnchor = HAnchor.ParentLeftRight | HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;
			Selectable = false;
			this.normalState = normalState;
			this.overState = overState;
			AddChild(normalState);
			AddChild(overState);

			overState.Visible = false;
		}

		public override void OnParentChanged(EventArgs ex)
		{
			// We don't need to remove these as the parent we are attached to is the held list that gets turned
			// into the menu list when required and unhooking these breaks that list from working.
			// This will get cleared when the list is no longer need and the menu (the parent) is removed)
			Parent.MouseLeave += (s, e) => this.Highlighted = false;
			Parent.MouseEnter += (s, e) =>
			{
				ClearActiveHighlight();
				this.Highlighted = true;
			};

			base.OnParentChanged(ex);
		}

		public override void SendToChildren(object objectToRoute)
		{
			if (objectToRoute is MenuItem.MenuClosedMessage)
			{
				this.Highlighted = false;
			}

			base.SendToChildren(objectToRoute);
		}

		public bool Highlighted
		{
			get
			{
				return overState.Visible;
			}
			set
			{
				overState.Visible = value;
				normalState.Visible = !value;
			}
		}

		private void ClearActiveHighlight()
		{
			// Find the FlowLayoutWidget containing this MenuItemStatesView  instance
			var dropListContainer = this.Parents<FlowLayoutWidget>().FirstOrDefault();

			// Loop over any sibling MenuItem widgets
			foreach (var menuItem in dropListContainer.Children<MenuItem>())
			{
				// Find the MenuItemStatesView instance that they contain and set highlighted to false
				var statesView = menuItem.Children<MenuItemStatesView>().FirstOrDefault();
				if (statesView != null)
				{
					statesView.Highlighted = false;
				}
			}
		}
	}

	public class MenuItemColorStatesView : GuiWidget
	{
		GuiWidget textWidgetContainer;
		TextWidget textWidget;

		public RGBA_Bytes NormalBackgroundColor { get; set; }
		public RGBA_Bytes OverBackgroundColor { get; set; }
		public RGBA_Bytes DisabledBackgroundColor { get; set; }

		public RGBA_Bytes NormalTextColor { get; set; }
		public RGBA_Bytes OverTextColor { get; set; }
		public RGBA_Bytes DisabledTextColor { get; set; }

		public double PointSize { get { return textWidget.PointSize; } set { textWidget.PointSize = value; } }

		public MenuItemColorStatesView(string name)
		{
			HAnchor = HAnchor.ParentLeftRight | HAnchor.FitToChildren;
			VAnchor = VAnchor.FitToChildren;
			Selectable = false;

			textWidgetContainer = new GuiWidget();
			textWidget = new TextWidget(name)
			{
				AutoExpandBoundsToText = true,
			};

			textWidgetContainer.HAnchor = HAnchor.FitToChildren;
			textWidgetContainer.AddChild(textWidget);
			textWidgetContainer.VAnchor = VAnchor.FitToChildren;

			AddChild(textWidgetContainer);
		}

		public override void OnParentChanged(EventArgs ex)
		{
			// We don't need to remove these as the parent we are attached to is the held list that gets turned
			// into the menu list when required and unhooking these breaks that list from working.
			// This will get cleared when the list is no longer need and the menu (the parent) is removed)
			Parent.MouseLeave += (s, e) => this.Highlighted = false;
			Parent.MouseEnter += (s, e) =>
			{
				ClearActiveHighlight();
				this.Highlighted = true;
			};

			Parent.EnabledChanged += (s, e) =>
			{
				if (Parent.Enabled)
				{
					textWidget.TextColor = NormalTextColor;
				}
				else
				{
					textWidget.TextColor = DisabledTextColor;
				}
			};

			base.OnParentChanged(ex);
		}

		public override void SendToChildren(object objectToRoute)
		{
			if (objectToRoute is MenuItem.MenuClosedMessage)
			{
				this.Highlighted = false;
			}

			base.SendToChildren(objectToRoute);
		}

		public bool Highlighted
		{
			get
			{
				return BackgroundColor == OverBackgroundColor;
			}
			set
			{
				if(value)
				{
					BackgroundColor = OverBackgroundColor;
					if (Parent.Enabled)
					{
						textWidget.TextColor = OverTextColor;
					}
					else
					{
						textWidget.TextColor = DisabledTextColor;
					}
				}
				else
				{
					BackgroundColor = NormalBackgroundColor;
					if (Parent.Enabled)
					{
						textWidget.TextColor = NormalTextColor;
					}
					else
					{
						textWidget.TextColor = DisabledTextColor;
					}
				}
			}
		}

		private void ClearActiveHighlight()
		{
			// Find the FlowLayoutWidget containing this MenuItemStatesView  instance
			var dropListContainer = this.Parents<FlowLayoutWidget>().FirstOrDefault();

			// Loop over any sibling MenuItem widgets
			foreach (var menuItem in dropListContainer.Children<MenuItem>())
			{
				// Find the MenuItemStatesView instance that they contain and set highlighted to false
				var statesView = menuItem.Children<MenuItemStatesView>().FirstOrDefault();
				if (statesView != null)
				{
					statesView.Highlighted = false;
				}
			}
		}
	}
}