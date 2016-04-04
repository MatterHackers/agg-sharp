using System;
using System.Diagnostics;

namespace MatterHackers.Agg.UI
{
	[DebuggerDisplay("{Text}:{Value}")]
	public class MenuItem : GuiWidget
	{
		public class MenuClosedMessage
		{
		}

		public event EventHandler Selected;

		public delegate bool CheckIfShouldClick();

		public CheckIfShouldClick DoClickFunction;

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
			if (DoClickFunction != null
				&& DoClickFunction())
			{
				if (PositionWithinLocalBounds(mouseEvent.X, mouseEvent.Y))
				{
					Selected?.Invoke(this, mouseEvent);
				}
			}
			base.OnMouseUp(mouseEvent);
		}
	}
}