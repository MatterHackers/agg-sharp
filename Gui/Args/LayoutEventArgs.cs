using System;

namespace MatterHackers.Agg.UI
{
	public class LayoutEventArgs : EventArgs
	{
		private GuiWidget parentWidget;
		private GuiWidget childWidget;

		public GuiWidget ParentWidget
		{
			get { return parentWidget; }
		}

		public GuiWidget ChildWidget
		{
			get { return childWidget; }
		}

		public LayoutEventArgs(GuiWidget parentWidget, GuiWidget childWidget)
		{
			if (parentWidget == null)
			{
				throw new InvalidOperationException("The LayoutEngine comes from the parent so you have to pass it.");
			}

			this.parentWidget = parentWidget;
			this.childWidget = childWidget;
		}
	}
}