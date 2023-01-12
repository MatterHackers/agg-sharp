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
using System;

namespace MatterHackers.Agg
{
    public class FirstInFirstOutQueue<T>
    {
        private int head;
        private T[] itemArray;
        private int mask;
        private int shiftFactor;
        private int size;

        public FirstInFirstOutQueue(int shiftFactor)
        {
            this.shiftFactor = shiftFactor;
            mask = (1 << shiftFactor) - 1;
            itemArray = new T[1 << shiftFactor];
            head = 0;
            size = 0;
        }

        public int Count
        {
            get { return size; }
        }

        public T First
        {
            get { return itemArray[head & mask]; }
        }

        public T Dequeue()
        {
            int headIndex = head & mask;
            T firstItem = itemArray[headIndex];
            if (size > 0)
            {
                head++;
                size--;
            }
            return firstItem;
        }

        public void Enqueue(T itemToQueue)
        {
            if (size == itemArray.Length)
            {
                int headIndex = head & mask;
                shiftFactor += 1;
                mask = (1 << shiftFactor) - 1;
                T[] newArray = new T[1 << shiftFactor];
                // copy the from head to the end
                Array.Copy(itemArray, headIndex, newArray, 0, size - headIndex);
                // copy form 0 to the size
                Array.Copy(itemArray, 0, newArray, size - headIndex, headIndex);
                itemArray = newArray;
                head = 0;
            }
            itemArray[(head + size++) & mask] = itemToQueue;
        }
    }
}