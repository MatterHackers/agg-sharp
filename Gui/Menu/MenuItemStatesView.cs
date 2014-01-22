using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class MenuItemStatesView : GuiWidget
    {
        GuiWidget normalState;
        GuiWidget overState;

        public MenuItemStatesView(GuiWidget normalState, GuiWidget overState)
        {
            overState.HAnchor = UI.HAnchor.ParentLeftRight;
            normalState.HAnchor = UI.HAnchor.ParentLeftRight;
            HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
            VAnchor = UI.VAnchor.FitToChildren;
            Selectable = false;
            this.normalState = normalState;
            this.overState = overState;
            AddChild(normalState);
            AddChild(overState);

            overState.Visible = false;
        }

        public override void OnParentChanged(EventArgs e)
        {
            // We don't need to remove these as the parent we are attached to is the held list that gets turned
            // into the menu list when required and unhooking these breaks that list from working.
            // This will get cleared when the list is no longer need and the menu (the parent) is removed)
            Parent.MouseEnter += new EventHandler(Parent_MouseEnter);
            Parent.MouseLeave += new EventHandler(Parent_MouseLeave);
            base.OnParentChanged(e);
        }

        void Parent_MouseLeave(object sender, EventArgs e)
        {
            overState.Visible = false;
            normalState.Visible = true;
        }

        void Parent_MouseEnter(object sender, EventArgs e)
        {
            overState.Visible = true;
            normalState.Visible = false;
        }

        public override void OnDraw(Graphics2D graphics2D)
        {
            base.OnDraw(graphics2D);
        }
    }
}
