using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace ObjParser.Types
{
    public class Color : IType
    {
        public float r { get; set; }
        public float g { get; set; }
        public float b { get; set; }

        public Color()
        {
            this.r = 1f;
            this.g = 1f;
            this.b = 1f;
        }

        public void LoadFromStringArray(string[] data)
        {
            if (data.Length != 4) return;
            r = float.Parse(data[1], CultureInfo.InvariantCulture);
            g = float.Parse(data[2], CultureInfo.InvariantCulture);
            b = float.Parse(data[3], CultureInfo.InvariantCulture);
        }

        public override string ToString()
        {
            return string.Format(CultureInfo.InvariantCulture, "{0} {1} {2}", r, g, b);
        }
    }
}
