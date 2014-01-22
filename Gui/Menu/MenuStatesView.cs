using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class MenuStatesView : GuiWidget
    {
        GuiWidget normal;
        GuiWidget hover;
        GuiWidget pressed;

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
