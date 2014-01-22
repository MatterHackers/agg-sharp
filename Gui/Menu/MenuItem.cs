using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class MenuItem : GuiWidget
    {
        public event EventHandler Selected;

        public string Value
        {
            get;
            set;
        }

        public MenuItem(GuiWidget viewItem, string value = null)
        {
            Value = value;
            HAnchor = UI.HAnchor.ParentLeftRight | UI.HAnchor.FitToChildren;
            VAnchor = UI.VAnchor.FitToChildren;
            AddChild(viewItem);
        }

        public override void OnMouseUp(MouseEventArgs mouseEvent)
        {
            if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
            {
                if (Selected != null)
                {
                    Selected(this, mouseEvent);
                }
            }
            base.OnMouseUp(mouseEvent);
        }
    }
}
