/*
Copyright (c) 2026, Lars Brubaker, John Lewin
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

namespace MatterHackers.Agg
{
	public class SafeList<T> : IEnumerable<T>
	{
		public event EventHandler ItemsModified;

		protected List<T> items = new List<T>();

		public SafeList()
		{
		}

		public SafeList(IEnumerable<T> sourceItems)
		{
			items = new List<T>(sourceItems);
		}

		public void Add(T item) => this.Modify(list => list.Add(item));

		public void Remove(T item) => this.Modify(list => list.Remove(item));

		public int Count => items.Count;

		public bool Contains(T item) => items.Contains(item);

		public T this[int index]
		{
			get
			{
				var tempItems = items;
				if (index < tempItems.Count)
				{
					return tempItems[index];
				}

				return default(T);
			}
		}

		/// <summary>
		/// Provides a safe context to manipulate items. Copies items into a new list, invokes the 'modifier'
		/// Action passing in the copied list and finally swaps the modified list into place after the invoked Action completes.
		/// </summary>
		/// <remarks>
		/// The modifier only owns its list for the duration of the call. Publishing that same instance let
		/// anything still holding it - most importantly an `async` lambda bound to this Action, which returns
		/// at its first await and resumes on a later pump (or, off the main loop, on a thread pool thread)
		/// long after Modify returned - keep calling
		/// Add on the LIVE list from outside Modify. That is not merely a lost update: List&lt;T&gt;.Add
		/// publishes the incremented Count before it stores the element, so a concurrent reader (the next
		/// Modify's `new List&lt;T&gt;(items)`, which copies via ICollection.CopyTo and therefore skips the
		/// enumerator's version check) can capture a null element. A null child in an Object3D tree then
		/// throws a bare NullReferenceException in the next Object3DExtensions.DescendantsAndSelf walk - on
		/// whichever background rebuild thread happens to walk it, which takes the whole process down.
		/// Publishing a private copy makes the live list unreachable, so no leaked reference can corrupt it.
		/// </remarks>
		/// <param name="modifier">The Action to invoke</param>
		virtual public void Modify(Action<List<T>> modifier)
		{
			// Copy the child items to a new list
			var safeClone = new List<T>(items);

			// Pass the new list to the Action for manipulation
			modifier(safeClone);

			// Swap a private copy of the modified list into place - never the modifier's own instance
			items = new List<T>(safeClone);

			this.OnItemsModified(null);
		}

		public IEnumerator<T> GetEnumerator() => items.GetEnumerator();

		IEnumerator IEnumerable.GetEnumerator() => items.GetEnumerator();

		public override string ToString()
		{
			if (Count == 1)
			{
				return $"Count = {Count}, Item[0] = {items[0]}";
			}
			else
			{
				return $"Count = {Count}";
			}
		}

		public int IndexOf(T childToFind)
		{
			return items.IndexOf(childToFind);
		}

		protected void OnItemsModified(EventArgs e)
		{
			this.ItemsModified?.Invoke(this, e);
		}

		public void Clear()
		{
			Modify((list) =>
			{
				list.Clear();
			});
		}

		public void AddRange(IEnumerable<T> enumerable)
		{
			this.Modify(list => list.AddRange(enumerable));
		}
	}
}