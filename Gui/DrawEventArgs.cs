using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public delegate void DrawEventHandler(Object sender, DrawEventArgs e);

    public class DrawEventArgs : EventArgs
    {
        Graphics2D internal_graphics2D;

        public Graphics2D graphics2D
        {
            get { return internal_graphics2D; }
        }

        public DrawEventArgs(Graphics2D graphics2D)
        {
            this.internal_graphics2D = graphics2D;
        }
    }
}
