using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public enum PropertyCausingLayout { PerformLayout, LocalBounds, ChildLocalBounds, Position, AddChild, RemoveChild, Padding, Margin, HAnchor, VAnchor, Visible };

    public class LayoutEventArgs : EventArgs
    {
        GuiWidget parentWidget;
        GuiWidget childWidget;
        PropertyCausingLayout changedProperty;

        public PropertyCausingLayout ChangedProperty
        {
            get { return changedProperty; }
        }

        public GuiWidget ParentWidget
        {
            get { return parentWidget; }
        }

        public GuiWidget ChildWidget
        {
            get { return childWidget; }
        }

        public LayoutEventArgs(GuiWidget parentWidget, GuiWidget childWidget, PropertyCausingLayout changedProperty)
        {
            if (parentWidget == null)
            {
                throw new InvalidOperationException("The LayoutEngine comes from the parent so you have to pass it.");
            }
            this.parentWidget = parentWidget;
            this.childWidget = childWidget;
            this.changedProperty = changedProperty;
        }
    }
}
