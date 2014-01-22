using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class FileDropEventArgs : EventArgs
    {
        public List<string> DroppedFiles;
        double x;
        double y;

        public double X { get { return x; } }
        public double Y { get { return y; } } 

        public FileDropEventArgs(List<string> droppedFiles, double x, double y)
        {
            this.x = x;
            this.y = y;
            // TODO: Complete member initialization
            this.DroppedFiles = droppedFiles;
        }

        public bool AcceptDrop { get; set; }
    }
}
