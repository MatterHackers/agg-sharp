//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------

namespace MatterHackers.Agg
{

    //----------------------------------------------------------range_adaptor
    public class VectorPodRangeAdaptor
    {
        private VectorPOD<int> podArray;
        private int size;
        private int start;

        public VectorPodRangeAdaptor(VectorPOD<int> array, int start, int size)
        {
            podArray = array;
            this.start = start;
            this.size = size;
        }

        public int this[int i]
        {
            get
            {
                return podArray.Array[start + i];
            }

            set
            {
                podArray.Array[start + i] = value;
            }
        }

        public int at(int i)
        {
            return podArray.Array[start + i];
        }

        public int Size()
        {
            return size;
        }

        public int ValueAt(int i)
        {
            return podArray.Array[start + i];
        }
    }
}