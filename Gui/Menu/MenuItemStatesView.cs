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
			overState.HAnchor |= UI.HAnchor.ParentLeftRight;
			normalState.HAnchor |= UI.HAnchor.ParentLeftRight;
			HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
			VAnchor = UI.VAnchor.FitToChildren;
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

		public override void SendToChildren(object objectToRout)
		{
			if (objectToRout as MenuItem.MenuClosedMessage != null)
			{
				this.Highlighted = false;
			}

			base.SendToChildren(objectToRout);
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

			// Loop over any sibling MenuItem wigets
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