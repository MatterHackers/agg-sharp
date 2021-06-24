using MatterHackers.VectorMath;
using System;

public static class MatUtils
{
    public static double Norm(Mat3 a) 
    { 
        return Math.Sqrt((a.m00 * a.m00) + (a.m01 * a.m01) + (a.m02 * a.m02)
                    + (a.m10 * a.m10) + (a.m11 * a.m11) + (a.m12 * a.m12)
                    + (a.m20 * a.m20) + (a.m21 * a.m21) + (a.m22 * a.m22));
    }

    public static double Norm(SMat3 a)
    {
        return Math.Sqrt((a.m00 * a.m00) + (a.m01 * a.m01) + (a.m02 * a.m02)
                    + (a.m01 * a.m01) + (a.m11 * a.m11) + (a.m12 * a.m12)
                    + (a.m02 * a.m02) + (a.m12 * a.m12) + (a.m22 * a.m22)); 
    }

    public static double Off(Mat3 a)
    { 
        return Math.Sqrt((a.m01 * a.m01) + (a.m02 * a.m02) + (a.m10 * a.m10) + (a.m12 * a.m12) + (a.m20 * a.m20) + (a.m21 * a.m21));
    }

    public static double off(SMat3 a) 
    { 
        return Math.Sqrt(2 * ((a.m01 * a.m01) + (a.m02 * a.m02) + (a.m12 * a.m12))); 
    }

    public static void Mmul(out Mat3 Out, Mat3 a, Mat3 b)
    { 
        Out = new Mat3();
        Out.Set(a.m00 * b.m00 + a.m01 * b.m10 + a.m02 * b.m20,
                a.m00 * b.m01 + a.m01 * b.m11 + a.m02 * b.m21,
                a.m00 * b.m02 + a.m01 * b.m12 + a.m02 * b.m22,
                a.m10 * b.m00 + a.m11 * b.m10 + a.m12 * b.m20,
                a.m10 * b.m01 + a.m11 * b.m11 + a.m12 * b.m21,
                a.m10 * b.m02 + a.m11 * b.m12 + a.m12 * b.m22,
                a.m20 * b.m00 + a.m21 * b.m10 + a.m22 * b.m20,
                a.m20 * b.m01 + a.m21 * b.m11 + a.m22 * b.m21,
                a.m20 * b.m02 + a.m21 * b.m12 + a.m22 * b.m22);
    }

    public static void MmulAta(out SMat3 Out, Mat3 a)
    { 
        Out = new SMat3();

        Out.setSymmetric(a.m00 * a.m00 + a.m10 * a.m10 + a.m20 * a.m20,
                         a.m00 * a.m01 + a.m10 * a.m11 + a.m20 * a.m21,
                         a.m00 * a.m02 + a.m10 * a.m12 + a.m20 * a.m22,
                         a.m01 * a.m01 + a.m11 * a.m11 + a.m21 * a.m21,
                         a.m01 * a.m02 + a.m11 * a.m12 + a.m21 * a.m22,
                         a.m02 * a.m02 + a.m12 * a.m12 + a.m22 * a.m22);
    }

    public static void Transpose(out Mat3 Out, Mat3 a) 
    {
        Out = new Mat3();

        Out.Set(a.m00, a.m10, a.m20, a.m01, a.m11, a.m21, a.m02, a.m12, a.m22);
    }

    public static void VMul(out Vector3 Out, Mat3 a, Vector3 v)
    { 
        Out = new Vector3(
            (a.m00 * v.X) + (a.m01 * v.Y) + (a.m02 * v.Z),
            (a.m10 * v.X) + (a.m11 * v.Y) + (a.m12 * v.Z),
            (a.m20 * v.X) + (a.m21 * v.Y) + (a.m22 * v.Z));
    }

    public static void VMuSymmetric(out Vector3 Out, SMat3 a, Vector3 v) 
    { 
        Out = new Vector3(
            (a.m00 * v.X) + (a.m01 * v.Y) + (a.m02 * v.Z),
            (a.m01 * v.X) + (a.m11 * v.Y) + (a.m12 * v.Z),
            (a.m02 * v.X) + (a.m12 * v.Y) + (a.m22 * v.Z));
    }
}
