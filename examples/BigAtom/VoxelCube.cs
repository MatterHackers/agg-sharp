using System;
using System.Collections.Generic;
using System.Text;

namespace BigAtom
{
    public class VoxelCube
    {
        int pow2Size;
        int voxelWidth;

        public VoxelCube(int cubePow2Size)
        {
            this.pow2Size = cubePow2Size;
            voxelWidth = (int)Math.Pow(2, cubePow2Size);
        }


    }
}
