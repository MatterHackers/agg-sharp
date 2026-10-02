/*
Copyright (c) 2026, Lars Brubaker
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
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// Tree walks over a GuiWidget's children, descendants and ancestors, filtered by widget type.
	/// </summary>
	public static class ExtensionMethods
	{
		/// <summary>
		/// Returns all children of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> Children<T>(this GuiWidget widget) where T : GuiWidget
		{
			return widget.Children.OfType<T>();
		}

		public static IEnumerable<GuiWidget> DescendantsAndSelf(this GuiWidget widget)
		{
			return DescendantsAndSelf<GuiWidget>(widget);
		}

		/// <summary>
		/// Returns all descendants and this of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> DescendantsAndSelf<T>(this GuiWidget widget) where T : GuiWidget
		{
			var items = new Stack<GuiWidget>();
			items.Push(widget);

			while (items.Any())
			{
				GuiWidget item = items.Pop();

				foreach (var child in item.Children)
				{
					items.Push(child);
				}

				if (item is T itemIsType)
				{
					yield return itemIsType;
				}
			}
		}

		public static IEnumerable<GuiWidget> Descendants(this GuiWidget widget)
		{
			return Descendants<GuiWidget>(widget);
		}

		public enum ReturnOrder
		{
			BredthFirst,
			DepthFirst
		}

		/// <summary>
		/// Returns all descendants of the current GuiWiget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <param name="evaluate">Determines if a given child widget should be added or descended.</param>
		/// <returns>All matching child widgets</returns>
		public static IEnumerable<T> Descendants<T>(this GuiWidget widget,
			Func<GuiWidget, bool> evaluate = null) where T : GuiWidget
		{
			var items = new Stack<GuiWidget>(widget.Children);

			while (items.Any())
			{
				GuiWidget item = items.Pop();

				foreach (var child in item.Children.Reverse())
				{
					if (evaluate == null
						|| evaluate(child))
					{
						items.Push(child);
					}
				}

				if (item is T itemIsType)
				{
					yield return itemIsType;
				}
			}
		}

		/// <summary>
		/// Returns all ancestors of the current GuiWidget matching the given type
		/// </summary>
		/// <typeparam name="T">The type filter</typeparam>
		/// <param name="widget">The context widget</param>
		/// <returns>The matching ancestor widgets</returns>
		public static IEnumerable<T> Parents<T>(this GuiWidget widget) where T : GuiWidget
		{
			GuiWidget context = widget.Parent;
			while (context != null)
			{
				if (context is T)
				{
					yield return (T)context;
				}

				context = context.Parent;
			}
		}
	}
}
