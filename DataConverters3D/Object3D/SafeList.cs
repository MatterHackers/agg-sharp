/*
Copyright (c) 2017, Lars Brubaker, John Lewin
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections;
using System.Collections.Generic;

namespace MatterHackers.DataConverters3D
{
	public interface IAscendable<T>
	{
		T Parent { get; set; }
	}

	public class SafeList<T> : IEnumerable<T> where T : IAscendable<T>
	{
		public event EventHandler ItemsModified;

		private List<T> items = new List<T>();

		private T parentItem;

		public SafeList(T parent)
		{
			this.parentItem = parent;
		}

		public SafeList(IEnumerable<T> sourceItems, T parent)
		{
			this.parentItem = parent;
			items = new List<T>(sourceItems);
		}

		public void Add(T item) => this.Modify(list => list.Add(item));

		public void Remove(T item) => this.Modify(list => list.Remove(item));

		public int Count => items.Count;

		public bool Contains(T item) => items.Contains(item);

		public void SetParent(T parent)
		{
			this.parentItem = parent;
		}

		/// <summary>
		/// Provides a safe context to manipulate items. Copies items into a new list, invokes the 'modifier'
		/// Action passing in the copied list and finally swaps the modified list into place after the invoked Action completes
		/// </summary>
		/// <param name="modifier">The Action to invoke</param>
		public void Modify(Action<List<T>> modifier)
		{
			// Copy the child items to a new list
			var safeClone = new List<T>(items);

			// Pass the new list to the Action for manipulation
			modifier(safeClone);

			// Swap the modified list into place
			items = safeClone;

			if (parentItem != null)
			{
				foreach (var item in items)
				{
					item.Parent = parentItem;
				}
			}

			this.ItemsModified?.Invoke(this, null);
		}

		public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

		public override string ToString()
		{
			if (Count == 1)
			{
				return $"Count = {Count}, Item[0] = {items[0].ToString()}";
			}
			else
			{
				return $"Count = {Count}";
			}
		}
	}
}