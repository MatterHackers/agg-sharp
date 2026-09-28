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

using System.Collections.Generic;

namespace MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests
{
	/// <summary>One row of <see cref="EventHistory"/>: an event kind and how many times it arrived in a row.</summary>
	public sealed class EventHistoryEntry
	{
		public EventHistoryEntry(string summary, string full)
		{
			this.Summary = summary;
			this.Full = full;
			this.Count = 1;
		}

		/// <summary>The kind of event ("MouseDown Left"); consecutive events with the same summary coalesce.</summary>
		public string Summary { get; }

		/// <summary>The detailed text (with position) of the latest occurrence.</summary>
		public string Full { get; internal set; }

		public int Count { get; internal set; }
	}

	/// <summary>
	/// agg-gui's deduplicated input history (egui's DeduplicatedHistory): newest first, a repeat of the newest
	/// entry's summary bumps its count instead of adding a row, capped at <see cref="Capacity"/> rows.
	/// </summary>
	public sealed class EventHistory
	{
		public const int Capacity = 1000;

		private readonly List<EventHistoryEntry> entries = new List<EventHistoryEntry>();

		/// <summary>Newest at index 0.</summary>
		public IReadOnlyList<EventHistoryEntry> Entries => this.entries;

		public void Add(string summary, string full)
		{
			if (this.entries.Count > 0 && this.entries[0].Summary == summary)
			{
				this.entries[0].Count++;
				this.entries[0].Full = full;
				return;
			}

			this.entries.Insert(0, new EventHistoryEntry(summary, full));
			if (this.entries.Count > Capacity)
			{
				this.entries.RemoveAt(this.entries.Count - 1);
			}
		}
	}
}
