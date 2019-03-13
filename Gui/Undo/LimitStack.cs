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
using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public class LimitStack<T>
	{
		private List<T> content = new List<T>();

		public int Limit { get; set; } = int.MaxValue;

		public int Count => content.Count;

		public void Push(T item)
		{
			content.Add(item);

			if(content.Count > Limit)
			{
				// remove the oldest item
				content.RemoveAt(0);
			}
		}

		public T Pop()
		{
			var index = content.Count - 1;
			var item = content[index];
			content.RemoveAt(index);
			return item;
		}

		public void Clear()
		{
			content.Clear();
		}
	}
}