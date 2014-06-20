using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class InvalidateEventArgs : EventArgs
    {
        RectangleDouble invalidRectangle;
        public RectangleDouble InvalidRectangle
        {
            get
            {
                return invalidRectangle;
            }
        }

        public InvalidateEventArgs(RectangleDouble invalidRectangle)
        {
            this.invalidRectangle = invalidRectangle;
        }
    }
}
