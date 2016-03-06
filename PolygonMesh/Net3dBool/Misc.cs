using System;
using System.Collections;
using System.Collections.Generic;

namespace Net3dBool
{
    public class Shape3D
    {

    }

    public static class Helper
    {
        public static void fill<T>(this T[] self, T value)
        {
            for (var i = 0; i < self.Length; i++)
            {
                self[i] = value;
            }
        }

        //        public static double dot(this Vector3d self, Vector3d v)
        //        {
        //            double dot = self.X * v.X + self.Y * v.Y + self.Z * v.Z;
        //            return dot;
        //        }
        //
        //        public static Vector3d cross(this Vector3d self, Vector3d v)
        //        {
        //            double crossX = self.Y * v.Z - v.Y * self.Z;
        //            double crossY = self.Z * v.X - v.Z * self.X;
        //            double crossZ = self.X * v.Y - v.X * self.Y;
        //            return new Vector3d(crossX, crossY, crossZ);
        //        }
        //
        //        public static double distance(this Vector3d v1, Vector3d v2)
        //        {
        //            double dx = v1.X - v2.X;
        //            double dy = v1.Y - v2.Y;
        //            double dz = v1.Z - v2.Z;
        //            return (double)Math.Sqrt(dx * dx + dy * dy + dz * dz);
        //        }

        private static Random rnd = new Random();

        public static double random()
        {
            return rnd.NextDouble();
        }
    }
}

