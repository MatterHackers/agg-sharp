using System;

namespace Gaming.Core
{
	public class TwoSidedStack<ElementType>
	{
		protected ElementType[] m_InnerList;
		protected int m_HeadIndex;
		protected int m_TailIndex;
		protected int m_Count;

		protected int m_ArrayPower2SizeFactor;
		protected int m_ArrayMask;
		protected int m_ArrayCapacity;

		public TwoSidedStack()
			: this(5)
		{ }

		public TwoSidedStack(int innerArrayPower2SizeFactor)
		{
			SetSize(innerArrayPower2SizeFactor);
		}

		public void PushHead(ElementType value)
		{
			if (m_Count == m_ArrayCapacity)
			{
				SetSize(m_ArrayPower2SizeFactor + 1);
			}

			m_HeadIndex--;
			if (m_HeadIndex < 0)
			{
				m_HeadIndex += m_ArrayCapacity;
			}

			m_InnerList[m_HeadIndex] = value;
			m_Count++;
		}

		public void PushTail(ElementType value)
		{
			if (m_Count == m_ArrayCapacity)
			{
				SetSize(m_ArrayPower2SizeFactor + 1);
			}

			m_InnerList[m_TailIndex] = value;
			m_TailIndex++;
			m_TailIndex &= m_ArrayMask;
			m_Count++;
		}

		public ElementType PopHead()
		{
			if (m_Count == 0)
			{
				throw new Exception("Stack was empty!");
			}

			ElementType r = m_InnerList[m_HeadIndex];
			m_HeadIndex++;
			m_HeadIndex &= m_ArrayMask;
			m_Count--;
			return r;
		}

		public ElementType PopTail()
		{
			if (m_Count == 0)
			{
				throw new Exception("Stack was empty!");
			}

			ElementType r = m_InnerList[m_TailIndex];
			m_TailIndex--;
			if (m_TailIndex < 0)
			{
				m_TailIndex &= m_ArrayMask;
			}

			m_Count--;
			return r;
		}

		public void Zero()
		{
			m_HeadIndex = m_TailIndex = m_Count = 0;
		}

		public ElementType this[int index]
		{
			get
			{
				return m_InnerList[(m_HeadIndex + index) & m_ArrayMask];
			}
			set
			{
				m_InnerList[(m_HeadIndex + index) & m_ArrayMask] = value;
			}
		}

		protected void SetSize(int innerArrayPower2SizeFactor)
		{
			int newSize = 1 << innerArrayPower2SizeFactor;
			if (newSize < m_Count)
			{
				throw new ArgumentException("The new size is smaller than the count.");
			}

			if (innerArrayPower2SizeFactor > m_ArrayPower2SizeFactor)
			{
				var resizedList = new ElementType[newSize];
				int i;
				for (i = 0; i < Count; i++)
				{
					resizedList[i] = this[i];
				}

				m_ArrayPower2SizeFactor = innerArrayPower2SizeFactor;
				m_ArrayCapacity = 1 << m_ArrayPower2SizeFactor;
				m_ArrayMask = m_ArrayCapacity - 1;
				m_InnerList = new ElementType[m_ArrayCapacity];

				m_HeadIndex = 0;
				m_TailIndex = Count;
				m_InnerList = resizedList;
			}
		}

		public int Count
		{
			get
			{
				return m_Count;
			}
		}
	}
}