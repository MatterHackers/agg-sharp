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

    public class QefData
    {
        public double ata_00, ata_01, ata_02, ata_11, ata_12, ata_22;
        public double atb_x, atb_y, atb_z;
        public double btb;
        public double massPoint_x, massPoint_y, massPoint_z;
        public int numPoints;

        public QefData()
        {
            clear();
        }

        public QefData(QefData rhs)
        {
            set(rhs);
        }

        public QefData(double ata_00, double ata_01, double ata_02, double ata_11, double ata_12, double ata_22, double atb_x, double atb_y,
                double atb_z, double btb, double massPoint_x, double massPoint_y, double massPoint_z, int numPoints)
        {
            
            set(ata_00, ata_01, ata_02, ata_11, ata_12, ata_22, atb_x, atb_y, atb_z, btb, massPoint_x, massPoint_y, massPoint_z, numPoints);
        }

        public void add(QefData rhs)
        {
            ata_00 += rhs.ata_00;
            ata_01 += rhs.ata_01;
            ata_02 += rhs.ata_02;
            ata_11 += rhs.ata_11;
            ata_12 += rhs.ata_12;
            ata_22 += rhs.ata_22;
            atb_x += rhs.atb_x;
            atb_y += rhs.atb_y;
            atb_z += rhs.atb_z;
            btb += rhs.btb;
            massPoint_x += rhs.massPoint_x;
            massPoint_y += rhs.massPoint_y;
            massPoint_z += rhs.massPoint_z;
            numPoints += rhs.numPoints;
        }

        public void clear()
        {
            set(0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0, 0);
        }

        public void set(double ata_00, double ata_01, double ata_02, double ata_11, double ata_12, double ata_22, double atb_x, double atb_y,
                 double atb_z, double btb, double massPoint_x, double massPoint_y, double massPoint_z, int numPoints) 
        {
            this.ata_00 = ata_00;
            this.ata_01 = ata_01;
            this.ata_02 = ata_02;
            this.ata_11 = ata_11;
            this.ata_12 = ata_12;
            this.ata_22 = ata_22;
            this.atb_x = atb_x;
            this.atb_y = atb_y;
            this.atb_z = atb_z;
            this.btb = btb;
            this.massPoint_x = massPoint_x;
            this.massPoint_y = massPoint_y;
            this.massPoint_z = massPoint_z;
            this.numPoints = numPoints;
        }

        public void set(QefData rhs)
        {
            set(rhs.ata_00, rhs.ata_01, rhs.ata_02, rhs.ata_11, rhs.ata_12,
                  rhs.ata_22, rhs.atb_x, rhs.atb_y, rhs.atb_z, rhs.btb,
                  rhs.massPoint_x, rhs.massPoint_y, rhs.massPoint_z,
                  rhs.numPoints);
        }
    }