using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ObjParser.Types
{
    public class TextureVertex : IType
    {
        public const int MinimumDataLength = 3;
        public const string Prefix = "vt";

        public double X { get; set; }

        public double Y { get; set; }

		public int Index { get; set; }

		public void LoadFromStringArray(string[] data)
        {
            if (data.Length < MinimumDataLength)
                throw new ArgumentException("Input array must be of minimum length " + MinimumDataLength, "data");

            if (!data[0].ToLower().Equals(Prefix))
                throw new ArgumentException("Data prefix must be '" + Prefix + "'", "data");

            bool success;

            double x, y;

            success = double.TryParse(data[1], NumberStyles.Any, CultureInfo.InvariantCulture, out x);
            if (!success) throw new ArgumentException("Could not parse X parameter as double");

            success = double.TryParse(data[2], NumberStyles.Any, CultureInfo.InvariantCulture, out y);
            if (!success) throw new ArgumentException("Could not parse Y parameter as double");
            X = x;
            Y = y;
        }

        public override string ToString()
        {
            return string.Format("vt {0} {1}", X, Y);
        }
    }
}
