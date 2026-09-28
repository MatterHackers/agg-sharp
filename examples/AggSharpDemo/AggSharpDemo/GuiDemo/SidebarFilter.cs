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

namespace MatterHackers.AggSharpDemo.GuiDemo
{
	/// <summary>
	/// What the GUI demo's sidebar shows, apart from any widget: the search text, which groups are collapsed,
	/// and from those which group headers and rows are visible (agg-gui's sidebar.rs FilterableGroup and
	/// FilterableItem).
	/// </summary>
	/// <remarks>
	/// A group none of whose rows match the search is hidden, header and all, so a search leaves no dead
	/// headers. Collapsing belongs to the user and is independent of the search, as agg-gui's CollapsingHeader
	/// keeps its own open state: a collapsed group with matches still shows its header and still hides its rows.
	/// </remarks>
	public class SidebarFilter
	{
		private readonly HashSet<string> collapsedGroups = new HashSet<string>();

		/// <summary>Raised after <see cref="Query"/> or a group's collapsed state changed.</summary>
		public event EventHandler Changed;

		/// <summary>The search text; empty shows everything.</summary>
		public string Query { get; private set; } = "";

		/// <summary>agg-gui's match: a case-insensitive substring of the title. An empty query matches all.</summary>
		public static bool Matches(string title, string query)
		{
			return string.IsNullOrEmpty(query)
				|| title.IndexOf(query, StringComparison.OrdinalIgnoreCase) >= 0;
		}

		/// <summary>The specs listed under <paramref name="group"/>, sorted by title as app_builder.rs's
		/// sidebar_sort_key does (case-insensitive; agg-gui also skips the icon prefix, which our titles do not
		/// carry).</summary>
		public static IReadOnlyList<DemoSpec> EntriesOf(string group)
		{
			// The Inspector is app_builder.rs's tool entry, listed with the Tools windows.
			return GuiDemoSpecs.All
				.Append(GuiDemoSpecs.Inspector)
				.Where(spec => spec.Group == group)
				.OrderBy(spec => spec.Title.ToLowerInvariant(), StringComparer.Ordinal)
				.ToList();
		}

		public void SetQuery(string query)
		{
			query ??= "";
			if (query != this.Query)
			{
				this.Query = query;
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		public bool IsCollapsed(string group) => this.collapsedGroups.Contains(group);

		public void SetCollapsed(string group, bool collapsed)
		{
			bool changed = collapsed ? this.collapsedGroups.Add(group) : this.collapsedGroups.Remove(group);
			if (changed)
			{
				this.Changed?.Invoke(this, EventArgs.Empty);
			}
		}

		/// <summary>Whether <paramref name="group"/>'s header shows: some of its titles match the search.</summary>
		public bool IsGroupVisible(string group)
		{
			return EntriesOf(group).Any(spec => Matches(spec.Title, this.Query));
		}

		/// <summary>Whether <paramref name="spec"/>'s row shows: its group is expanded and its title matches.</summary>
		public bool IsEntryVisible(DemoSpec spec)
		{
			return !this.IsCollapsed(spec.Group) && Matches(spec.Title, this.Query);
		}
	}
}
