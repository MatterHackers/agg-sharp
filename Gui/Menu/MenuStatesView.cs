using System;

namespace MatterHackers.Agg.UI
{
	public class MenuStatesView : GuiWidget
	{
		private GuiWidget normal;
		private GuiWidget hover;
		private GuiWidget pressed;

		public MenuStatesView(GuiWidget normal, GuiWidget hover, GuiWidget pressed)
		{
			this.normal = normal;
			this.hover = hover;
			this.pressed = pressed;

			SetVisibleStates();
		}

		private void SetVisibleStates()
		{
			throw new NotImplementedException();
		}
	}
}