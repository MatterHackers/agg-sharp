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

public class QefSolver
{
    public QefData QefData { get; private set; } = new QefData();
    private SMat3 ata;
    private Vector3 atb;
    private Vector3 massPoint;
    private Vector3 x;
    private bool hasSolution;

    public QefSolver()
    {
        ata = new SMat3();
        atb = Vector3.Zero;
        massPoint = Vector3.Zero;
        x = Vector3.Zero;
        hasSolution = false;
    }

    private QefSolver(QefSolver rhs)
    { }

    public Vector3 GetMassPoint()
    {
        return massPoint;
    }

    public void Add(Vector3 p, Vector3 n)
    {
        hasSolution = false;

        var normal = n.GetNormal();
        var nx = normal.X;
        var ny = normal.Y;
        var nz = normal.Z;

        QefData.ata_00 += nx * nx;
        QefData.ata_01 += nx * ny;
        QefData.ata_02 += nx * nz;
        QefData.ata_11 += ny * ny;
        QefData.ata_12 += ny * nz;
        QefData.ata_22 += nz * nz;
        double dot = nx * p.X + ny * p.Y + nz * p.Z;
        QefData.atb_x += dot * nx;
        QefData.atb_y += dot * ny;
        QefData.atb_z += dot * nz;
        QefData.btb += dot * dot;
        QefData.massPoint_x += p.X;
        QefData.massPoint_y += p.Y;
        QefData.massPoint_z += p.Z;
        ++QefData.numPoints;
    }

    public void Add(QefData rhs)
    {
        hasSolution = false;
        QefData.add(rhs);
    }

    public double getError()
    {
        if (!hasSolution)
        {
            throw new ArgumentException("Qef Solver does not have a solution!");
        }

        return getError(x);
    }

    public double getError(Vector3 pos)
    {
        if (!hasSolution)
        {
            setAta();
            setAtb();
        }

        Vector3 atax;
        MatUtils.VMuSymmetric(out atax, ata, pos);
        return pos.Dot(atax) - 2 * pos.Dot(atb) + QefData.btb;
    }

    public void reset()
    {
        hasSolution = false;
        QefData.clear();
    }

    public double Solve(out Vector3 outx, double svd_tol, int svd_sweeps, double pinv_tol)
    {
        if (QefData.numPoints == 0)
        {
            throw new ArgumentException("...");
        }

        massPoint.Set(QefData.massPoint_x, QefData.massPoint_y, QefData.massPoint_z);
        massPoint *= (1.0f / QefData.numPoints);
        setAta();
        setAtb();
        Vector3 tmpv;
        MatUtils.VMuSymmetric(out tmpv, ata, massPoint);
        atb = atb - tmpv;
        x = Vector3.Zero;
        double result = SVD.SolveSymmetric(ata, atb, x, svd_tol, svd_sweeps, pinv_tol);
        x += massPoint * 1;
        setAtb();
        outx = x;
        hasSolution = true;
        return result;
    }

    private void setAta()
    {
        ata.SetSymmetric(QefData.ata_00, QefData.ata_01, QefData.ata_02, QefData.ata_11, QefData.ata_12, QefData.ata_22);
    }

    private void setAtb()
    {
        atb.Set(QefData.atb_x, QefData.atb_y, QefData.atb_z);
    }
}
    
