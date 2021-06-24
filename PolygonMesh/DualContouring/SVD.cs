/*
 * This is free and unencumbered software released into the public domain.
 *
 * Anyone is free to copy, modify, publish, use, compile, sell, or
 * distribute this software, either in source code form or as a compiled
 * binary, for any purpose, commercial or non-commercial, and by any
 * means.
 *
 * In jurisdictions that recognize copyright laws, the author or authors
 * of this software dedicate any and all copyright interest in the
 * software to the public domain. We make this dedication for the benefit
 * of the public at large and to the detriment of our heirs and
 * successors. We intend this dedication to be an overt act of
 * relinquishment in perpetuity of all present and future rights to this
 * software under copyright law.
 *
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
 * EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
 * MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT.
 * IN NO EVENT SHALL THE AUTHORS BE LIABLE FOR ANY CLAIM, DAMAGES OR
 * OTHER LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE,
 * ARISING FROM, OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR
 * OTHER DEALINGS IN THE SOFTWARE.
 *
 * For more information, please refer to <http://unlicense.org/>
 */

using MatterHackers.VectorMath;
using System;

public static class SVD
{
    public static void Rotate01(SMat3 vtav, Mat3 v)
    {
        if (vtav.m01 == 0)
        {
            return;
        }

        double c = 0, s = 0;
        Schur2.Rot01(vtav, c, s);
        Givens.Rot01Post(v, c, s);
    }

    public static void Rotate02(SMat3 vtav, Mat3 v)
    {
        if (vtav.m02 == 0)
        {
            return;
        }

        double c = 0, s = 0;
        Schur2.Rot02(vtav, c, s);
        Givens.Rot02Post(v, c, s);
    }

    public static void Rotate12(SMat3 vtav, Mat3 v)
    {
        if (vtav.m12 == 0)
        {
            return;
        }

        double c = 0, s = 0;
        Schur2.Rot12(vtav, c, s);
        Givens.Rot12Post(v, c, s);
    }

    public static void GetSymmetricSvd(SMat3 a, SMat3 vtav, Mat3 v, double tol, int max_sweeps)
    {
        vtav.SetSymmetric(a);
        v.Set(1, 0, 0, 0, 1, 0, 0, 0, 1);
        double delta = tol * MatUtils.Norm(vtav);

        for (int i = 0; i < max_sweeps && MatUtils.off(vtav) > delta; ++i)
        {
            Rotate01(vtav, v);
            Rotate02(vtav, v);
            Rotate12(vtav, v);
        }
    }

    public static double calcError(Mat3 A, Vector3 x, Vector3 b)
    {
        Vector3 vtmp;
        MatUtils.VMul(out vtmp, A, x);
        vtmp = b - vtmp;
        return vtmp.Dot(vtmp);
    }

    public static double CalcError(SMat3 origA, Vector3 x, Vector3 b)
    {
        Mat3 A = new Mat3();
        Vector3 vtmp;
        A.SetSymmetric(origA);
        MatUtils.VMul(out vtmp, A, x);
        vtmp = b - vtmp;
        return vtmp.Dot(vtmp);
    }

    public static double PinV(double x, double tol)
    {
        return (Math.Abs(x) < tol || Math.Abs(1 / x) < tol) ? 0 : (1 / x);
    }

    public static void PseudoInverse(out Mat3 Out, SMat3 d, Mat3 v, double tol)
    {
        double d0 = PinV(d.m00, tol), d1 = PinV(d.m11, tol), d2 = PinV(d.m22, tol);

        Out = new Mat3();
        Out.Set(v.m00 * d0 * v.m00 + v.m01 * d1 * v.m01 + v.m02 * d2 * v.m02,
                v.m00 * d0 * v.m10 + v.m01 * d1 * v.m11 + v.m02 * d2 * v.m12,
                v.m00 * d0 * v.m20 + v.m01 * d1 * v.m21 + v.m02 * d2 * v.m22,
                v.m10 * d0 * v.m00 + v.m11 * d1 * v.m01 + v.m12 * d2 * v.m02,
                v.m10 * d0 * v.m10 + v.m11 * d1 * v.m11 + v.m12 * d2 * v.m12,
                v.m10 * d0 * v.m20 + v.m11 * d1 * v.m21 + v.m12 * d2 * v.m22,
                v.m20 * d0 * v.m00 + v.m21 * d1 * v.m01 + v.m22 * d2 * v.m02,
                v.m20 * d0 * v.m10 + v.m21 * d1 * v.m11 + v.m22 * d2 * v.m12,
                v.m20 * d0 * v.m20 + v.m21 * d1 * v.m21 + v.m22 * d2 * v.m22);
    }

    public static double SolveSymmetric(SMat3 A, Vector3 b, Vector3 x, double svd_tol, int svd_sweeps, double pinv_tol)
    {
        Mat3 pinv;
        Mat3 V = new Mat3();
        SMat3 VTAV = new SMat3();
        GetSymmetricSvd(A, VTAV, V, svd_tol, svd_sweeps);
        PseudoInverse(out pinv, VTAV, V, pinv_tol);
        MatUtils.VMul(out x, pinv, b);
        return CalcError(A, x, b);
    }

    public static void CalcSymmetricGivensCoefficients(double a_pp, double a_pq, double a_qq, out double c, out double s)
    {
        if (a_pq == 0)
        {
            c = 1;
            s = 0;
            return;
        }

        double tau = (a_qq - a_pp) / (2 * a_pq);
        double stt = Math.Sqrt(1.0f + tau * tau);
        double tan = 1.0f / ((tau >= 0) ? (tau + stt) : (tau - stt));
        c = 1.0f / Math.Sqrt(1.0f + tan * tan);
        s = tan * c;
    }

    public static double SolveLeastSquares(Mat3 a, Vector3 b, Vector3 x, double svd_tol, int svd_sweeps, double pinv_tol)
    {
        Mat3 at;
        SMat3 ata;
        Vector3 atb;
        MatUtils.Transpose(out at, a);
        MatUtils.MmulAta(out ata, a);
        MatUtils.VMul(out atb, at, b);
        return SolveSymmetric(ata, atb, x, svd_tol, svd_sweeps, pinv_tol);
    }

}