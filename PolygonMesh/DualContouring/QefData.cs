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

public class QefData
    {
        public double ata_00, ata_01, ata_02, ata_11, ata_12, ata_22;
        public Vector3 atb;
        public double btb;
        public Vector3 massPoint;
        public int numPoints;

        public QefData()
        {
            Clear();
        }

        public QefData(QefData rhs)
        {
            Set(rhs);
        }

        public QefData(double ata_00, double ata_01, double ata_02, double ata_11, double ata_12, double ata_22, Vector3 atb, double btb, Vector3 massPoint, int numPoints)
        {
            
            Set(ata_00, ata_01, ata_02, ata_11, ata_12, ata_22, atb, btb, massPoint, numPoints);
        }

        public void Add(QefData rhs)
        {
            ata_00 += rhs.ata_00;
            ata_01 += rhs.ata_01;
            ata_02 += rhs.ata_02;
            ata_11 += rhs.ata_11;
            ata_12 += rhs.ata_12;
            ata_22 += rhs.ata_22;
            atb += rhs.atb;
            btb += rhs.btb;
            massPoint += rhs.massPoint;
            numPoints += rhs.numPoints;
        }

        public void Clear()
        {
            Set(0, 0, 0, 0, 0, 0, Vector3.Zero, 0, Vector3.Zero, 0);
        }

        public void Set(double ata_00, double ata_01, double ata_02, double ata_11, double ata_12, double ata_22, Vector3 atb, double btb, Vector3 massPoint, int numPoints) 
        {
            this.ata_00 = ata_00;
            this.ata_01 = ata_01;
            this.ata_02 = ata_02;
            this.ata_11 = ata_11;
            this.ata_12 = ata_12;
            this.ata_22 = ata_22;
            this.atb = atb;
            this.btb = btb;
            this.massPoint = massPoint;
            this.numPoints = numPoints;
        }

        public void Set(QefData rhs)
        {
            Set(rhs.ata_00, rhs.ata_01, rhs.ata_02, rhs.ata_11, rhs.ata_12,
                  rhs.ata_22, rhs.atb, rhs.btb,
                  rhs.massPoint,
                  rhs.numPoints);
        }
    }