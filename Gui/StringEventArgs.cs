using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class StringEventArgs : EventArgs
    {
        string data;
        public string Data { get { return data; } }

        public StringEventArgs(string data)
        {
            this.data = data;
        }
    }
}
