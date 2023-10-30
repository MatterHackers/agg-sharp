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
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.Agg
{
    //--------------------------------------------------------------pod_vector
    // A simple class template to store Plain Old Data, a vector
    // of a fixed size. The data is contiguous in memory
    //------------------------------------------------------------------------
    public class VectorPOD<DataType> where DataType : struct
    {
        protected int currentSize;
        private static DataType zeroed_object;
        private DataType[] internalArray = new DataType[0];

        public VectorPOD()
        {
        }

        public VectorPOD(int cap)
            : this(cap, 0)
        {
        }

        public VectorPOD(int capacity, int extraTail)
        {
            Allocate(capacity, extraTail);
        }

        // Copying
        public VectorPOD(VectorPOD<DataType> vectorToCopy)
        {
            currentSize = vectorToCopy.currentSize;
            internalArray = (DataType[])vectorToCopy.internalArray.Clone();
        }

        public int AllocatedSize
        {
            get
            {
                return internalArray.Length;
            }
        }

        public DataType[] Array
        {
            get
            {
                return internalArray;
            }
        }

        public int Count
        {
            get { return currentSize; }
        }

        public int Length
        {
            get
            {
                return currentSize;
            }
        }

        public DataType this[int i]
        {
            get
            {
                return internalArray[i];
            }
        }

        public virtual void Add(DataType v)
        {
            if (internalArray == null || internalArray.Length < (currentSize + 1))
            {
                if (currentSize < 100000)
                {
                    Resize(currentSize + (currentSize / 2) + 16);
                }
                else
                {
                    Resize(currentSize + currentSize / 4);
                }
            }
            internalArray[currentSize++] = v;
        }

        // Allocate n elements. All data is lost,
        // but elements can be accessed in range 0...size-1.
        public void Allocate(int size)
        {
            Allocate(size, 0);
        }

        public void Allocate(int size, int extraTail)
        {
            Capacity(size, extraTail);
            currentSize = size;
        }

        public DataType at(int i)
        {
            return internalArray[i];
        }

        // Set new capacity. All data is lost, size is set to zero.
        public void Capacity(int newCapacity)
        {
            Capacity(newCapacity, 0);
        }

        public void Capacity(int newCapacity, int extraTail)
        {
            currentSize = 0;
            if (newCapacity > AllocatedSize)
            {
                internalArray = null;
                int sizeToAllocate = newCapacity + extraTail;
                if (sizeToAllocate != 0)
                {
                    internalArray = new DataType[sizeToAllocate];
                }
            }
        }

        public int Capacity()
        {
            return AllocatedSize;
        }

        public void clear()
        {
            currentSize = 0;
        }

        public void Clear()
        {
            currentSize = 0;
        }

        public void CopyFrom(VectorPOD<DataType> vetorToCopy)
        {
            Allocate(vetorToCopy.currentSize);
            if (vetorToCopy.currentSize != 0)
            {
                vetorToCopy.internalArray.CopyTo(internalArray, 0);
            }
        }

        public void cut_at(int num)
        {
            if (num < currentSize) currentSize = num;
        }

        public DataType[] data()
        {
            return internalArray;
        }

        public IEnumerable<DataType> DataIterator()
        {
            for (int index = 0; index < currentSize; index++)
            {
                // Yield each day of the week.
                yield return internalArray[index];
            }
        }

        public IEnumerator GetEnumerator()
        {
            for (int index = 0; index < currentSize; index++)
            {
                // Yield each day of the week.
                yield return internalArray[index];
            }
        }

        public void inc_size(int size)
        {
            currentSize += size;
        }

        public void Insert(int index, DataType value)
        {
            insert_at(index, value);
        }

        public void insert_at(int pos, DataType val)
        {
            if (pos >= currentSize)
            {
                internalArray[currentSize] = val;
            }
            else
            {
                for (int i = 0; i < currentSize - pos; i++)
                {
                    internalArray[i + pos + 1] = internalArray[i + pos];
                }
                internalArray[pos] = val;
            }
            ++currentSize;
        }

        public void push_back(DataType v)
        {
            internalArray[currentSize++] = v;
        }

        public void Remove(int indexToRemove)
        {
            if (indexToRemove >= Length)
            {
                throw new Exception("requested remove past end of array");
            }

            for (int i = indexToRemove; i < Length - 1; i++)
            {
                internalArray[i] = internalArray[i + 1];
            }

            currentSize--;
        }

        public void Remove(DataType itemToRemove)
        {
            for (int i = 0; i < Length; i++)
            {
                if ((object)internalArray[i] == (object)itemToRemove)
                {
                    Remove(i);
                }
            }
        }

        public void RemoveAt(int indexToRemove)
        {
            Remove(indexToRemove);
        }

        public void RemoveLast()
        {
            if (currentSize != 0)
            {
                currentSize--;
            }
        }

        // Resize keeping the content.
        public void Resize(int newSize)
        {
            if (newSize > currentSize)
            {
                if (newSize > AllocatedSize)
                {
                    var newArray = new DataType[newSize];
                    if (internalArray != null)
                    {
                        for (int i = 0; i < internalArray.Length; i++)
                        {
                            newArray[i] = internalArray[i];
                        }
                    }
                    internalArray = newArray;
                }
            }
        }

#pragma warning disable 649
#pragma warning restore 649

        public DataType value_at(int i)
        {
            return internalArray[i];
        }

        public void zero()
        {
            int NumItems = internalArray.Length;
            for (int i = 0; i < NumItems; i++)
            {
                internalArray[i] = zeroed_object;
            }
        }
    }
}