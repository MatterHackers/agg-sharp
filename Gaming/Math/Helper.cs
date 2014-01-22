using System;
using System.Collections.Generic;
using System.Text;

namespace Gaming.Math
{
    public static class Helper
    {
        public static double DegToRad(double Deg)
        {
            return Deg / 180 * (double)System.Math.PI;
        }
    }
}
