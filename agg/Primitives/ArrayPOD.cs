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
    public class ArrayPOD<T> where T : struct
    {
        private T[] m_array;

        private int m_size;

        public ArrayPOD()
                            : this(64)
        {
        }

        public ArrayPOD(int size)
        {
            m_array = new T[size];
            m_size = size;
        }

        public ArrayPOD(ArrayPOD<T> v)
        {
            m_array = (T[])v.m_array.Clone();
            m_size = v.m_size;
        }

        public T[] Array
        {
            get
            {
                return m_array;
            }
        }

        public T this[int index]
        {
            get
            {
                return m_array[index];
            }

            set
            {
                m_array[index] = value;
            }
        }

        public void RemoveLast()
        {
            throw new NotImplementedException();
        }

        public void Resize(int size)
        {
            if (size != m_size)
            {
                m_array = new T[size];
            }
        }

        public int Size()
        {
            return m_size;
        }
    }
}