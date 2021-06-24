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

public static class Schur2
{
    public static void Rot01(SMat3 m, double c, double s)
    {
        SVD.CalcSymmetricGivensCoefficients(m.m00, m.m01, m.m11, out c, out s);
        double cc = c * c;
        double ss = s * s;
        double mix = 2 * c * s * m.m01;
        m.setSymmetric(cc * m.m00 - mix + ss * m.m11, 0, c * m.m02 - s * m.m12,
                       ss * m.m00 + mix + cc * m.m11, s * m.m02 + c * m.m12, m.m22);
    }

    public static void Rot02(SMat3 m, double c, double s)
    {
        SVD.CalcSymmetricGivensCoefficients(m.m00, m.m02, m.m22, out c, out s);
        double cc = c * c;
        double ss = s * s;
        double mix = 2 * c * s * m.m02;
        m.setSymmetric(cc * m.m00 - mix + ss * m.m22, c * m.m01 - s * m.m12, 0,
                       m.m11, s * m.m01 + c * m.m12, ss * m.m00 + mix + cc * m.m22);
    }

    public static void Rot12(SMat3 m, double c, double s)
    {
        SVD.CalcSymmetricGivensCoefficients(m.m11, m.m12, m.m22, out c, out s);
        double cc = c * c;
        double ss = s * s;
        double mix = 2 * c * s * m.m12;
        m.setSymmetric(m.m00, c * m.m01 - s * m.m02, s * m.m01 + c * m.m02,
                       cc * m.m11 - mix + ss * m.m22, 0, ss * m.m11 + mix + cc * m.m22);
    }
}

