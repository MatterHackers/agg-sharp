﻿using System;
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
			overState.HAnchor |= HAnchor.Stretch;
			normalState.HAnchor |= HAnchor.Stretch;
			HAnchor = HAnchor.Stretch | HAnchor.Fit;
			VAnchor = VAnchor.Fit;
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
		TextWidget textWidget;

		public Color NormalBackgroundColor { get; set; }
		public Color OverBackgroundColor { get; set; }
		public Color DisabledBackgroundColor { get; set; }

		public Color NormalTextColor { get; set; }
		public Color OverTextColor { get; set; }
		public Color DisabledTextColor { get; set; }

		public double PointSize { get { return textWidget.PointSize; } set { textWidget.PointSize = value; } }

		public MenuItemColorStatesView(string name)
		{
			HAnchor = HAnchor.Stretch | HAnchor.Fit;
			VAnchor = VAnchor.Fit;
			Selectable = false;

			textWidget = new TextWidget(name)
			{
				AutoExpandBoundsToText = true,
				HAnchor = HAnchor.Left
			};

			AddChild(textWidget);
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