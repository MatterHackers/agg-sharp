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

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// One mirrored file as a sweep sees it: its key, and the two numbers a change is detected by.
	/// </summary>
	public readonly struct MirrorFileState
	{
		public MirrorFileState(string key, long modifiedTicks, long length)
		{
			this.Key = key;
			this.ModifiedTicks = modifiedTicks;
			this.Length = length;
		}

		/// <summary>The path relative to the mirror root, with forward slashes. Also the store's key.</summary>
		public string Key { get; }

		/// <summary>Last write time, in ticks.</summary>
		public long ModifiedTicks { get; }

		/// <summary>Size in bytes.</summary>
		public long Length { get; }

		/// <summary>
		/// Whether these two states are the same file contents as far as the mirror can tell.
		/// </summary>
		/// <remarks>
		/// Timestamp and size, not a hash. Hashing every file on every sweep would read the whole mirrored
		/// tree once a second in a heap that also holds the application; timestamp-and-size is what every
		/// backup and build system detects changes with, and it is wrong in exactly one case - a rewrite that
		/// keeps the byte count identical and lands inside the filesystem's timestamp resolution (a
		/// millisecond in MEMFS). The next change to that file pushes both, so the window is one edit wide and
		/// not permanent.
		/// </remarks>
		public bool Matches(MirrorFileState other)
		{
			return this.ModifiedTicks == other.ModifiedTicks
				&& this.Length == other.Length;
		}
	}

	/// <summary>
	/// What one sweep decided to do: the files to push, and the keys to remove.
	/// </summary>
	public sealed class MirrorSweepPlan
	{
		internal MirrorSweepPlan(IReadOnlyList<MirrorFileState> puts, IReadOnlyList<string> deletes)
		{
			this.Puts = puts;
			this.Deletes = deletes;
		}

		/// <summary>Files whose bytes are to be written to the store, with the state they were seen in.</summary>
		public IReadOnlyList<MirrorFileState> Puts { get; }

		/// <summary>Keys whose files are gone and which are to be removed from the store.</summary>
		public IReadOnlyList<string> Deletes { get; }

		public bool IsEmpty => this.Puts.Count == 0 && this.Deletes.Count == 0;
	}

	/// <summary>
	/// Decides what a write-behind sweep should push and remove. All of the mirror's policy arithmetic, and
	/// none of its input or output.
	/// </summary>
	/// <remarks>
	/// <para>Separated from <see cref="BrowserStorageMirror"/> for the reason <see cref="BrowserFrameTick"/>
	/// is separated from the window: everything interesting about write-behind is a decision - is this file
	/// new, has it settled, does it skip the wait, has it been deleted - and none of those decisions needs a
	/// browser, a filesystem or a clock. Here they are a function of (what was mirrored, what is on disk now,
	/// what time it is), which runs in the desktop test suite on every OS.</para>
	/// <para>The planner is stateful but not authoritative: it holds what it BELIEVES the store contains, and
	/// that belief only advances when the caller reports a write actually landed
	/// (<see cref="MarkMirrored"/>, <see cref="MarkDeleted"/>). A push that fails is therefore planned again
	/// on the next sweep, which is the whole of the retry policy.</para>
	/// </remarks>
	public sealed class MirrorSweepPlanner
	{
		private static readonly IReadOnlyList<MirrorFileState> NoPuts = Array.Empty<MirrorFileState>();

		private static readonly IReadOnlyList<string> NoDeletes = Array.Empty<string>();

		private readonly MirrorPolicy policy;

		/// <summary>What the store is believed to hold, keyed by store key.</summary>
		private readonly Dictionary<string, MirrorFileState> mirrored = new Dictionary<string, MirrorFileState>(StringComparer.Ordinal);

		/// <summary>Files seen changed and still waiting out their quiet period.</summary>
		private readonly Dictionary<string, PendingChange> pending = new Dictionary<string, PendingChange>(StringComparer.Ordinal);

		public MirrorSweepPlanner(MirrorPolicy policy)
		{
			this.policy = policy ?? throw new ArgumentNullException(nameof(policy));
		}

		/// <summary>How many keys the store is believed to hold.</summary>
		public int MirroredCount => mirrored.Count;

		/// <summary>
		/// Declares that the store already holds exactly these files in exactly these states - what a restore
		/// just wrote into the filesystem.
		/// </summary>
		/// <remarks>
		/// Without this the first sweep after a restore would see a tree full of files it had never pushed and
		/// push every one of them straight back into the store they came from: a boot's worth of pointless
		/// transactions, and on a large library the slowest thing the page does. The states have to be read
		/// back from the filesystem AFTER writing rather than carried over from the store, because writing a
		/// file stamps it with the time it was written.
		/// </remarks>
		public void Seed(IEnumerable<MirrorFileState> restored)
		{
			mirrored.Clear();
			pending.Clear();

			foreach (var file in restored)
			{
				if (!policy.IsExcluded(file.Key))
				{
					mirrored[file.Key] = file;
				}
			}
		}

		/// <summary>
		/// Compares the tree as it is now against what the store is believed to hold.
		/// </summary>
		/// <param name="currentFiles">Every file under the mirror root, as this sweep found them.</param>
		/// <param name="nowMilliseconds">The sweep's clock reading. Supplied rather than read so the quiet
		/// period is testable without waiting one out.</param>
		/// <param name="pushEverythingNow">Ignores the quiet period, pushing everything dirty. What a final
		/// flush does: there will be no next sweep to catch what is still settling.</param>
		public MirrorSweepPlan Plan(IEnumerable<MirrorFileState> currentFiles, long nowMilliseconds, bool pushEverythingNow = false)
		{
			List<MirrorFileState> puts = null;
			var seen = new HashSet<string>(StringComparer.Ordinal);
			long quietPeriodMilliseconds = (long)(policy.QuietPeriodSeconds * 1000);

			foreach (var file in currentFiles)
			{
				if (policy.IsExcluded(file.Key))
				{
					continue;
				}

				seen.Add(file.Key);

				if (mirrored.TryGetValue(file.Key, out var stored)
					&& stored.Matches(file))
				{
					// Up to date. Also clears any wait in progress: a file edited and put back the way it was
					// is not dirty, whatever it looked like halfway through.
					pending.Remove(file.Key);

					continue;
				}

				if (!pending.TryGetValue(file.Key, out var waiting)
					|| !waiting.State.Matches(file))
				{
					// Either the first sweep to see this change, or the file moved again while waiting - which
					// restarts the wait, since the point is to push a file that has stopped changing.
					waiting = new PendingChange(file, nowMilliseconds);
					pending[file.Key] = waiting;
				}

				if (pushEverythingNow
					|| policy.IsImmediate(file.Key)
					|| nowMilliseconds - waiting.UnchangedSince >= quietPeriodMilliseconds)
				{
					(puts ??= new List<MirrorFileState>()).Add(file);
				}
			}

			List<string> deletes = null;

			foreach (var key in mirrored.Keys)
			{
				// Not debounced, unlike a change: a file that is gone cannot be mid-write, and a stale key is
				// a file the user deleted and would be astonished to see come back on reload.
				if (!seen.Contains(key))
				{
					(deletes ??= new List<string>()).Add(key);
				}
			}

			// Waits for files that no longer exist are just garbage now. If one reappears it starts a fresh
			// wait, which is correct - it is a new write.
			if (pending.Count > 0)
			{
				List<string> vanished = null;

				foreach (var key in pending.Keys)
				{
					if (!seen.Contains(key))
					{
						(vanished ??= new List<string>()).Add(key);
					}
				}

				if (vanished != null)
				{
					foreach (string key in vanished)
					{
						pending.Remove(key);
					}
				}
			}

			return new MirrorSweepPlan(puts ?? NoPuts, deletes ?? NoDeletes);
		}

		/// <summary>
		/// Records that this file's bytes reached the store. Called after the write, not after the plan.
		/// </summary>
		public void MarkMirrored(MirrorFileState file)
		{
			mirrored[file.Key] = file;
			pending.Remove(file.Key);
		}

		/// <summary>Records that this key was removed from the store.</summary>
		public void MarkDeleted(string key)
		{
			mirrored.Remove(key);
			pending.Remove(key);
		}

		/// <summary>A change seen but not yet pushed, and when it was last seen to move.</summary>
		private readonly struct PendingChange
		{
			public PendingChange(MirrorFileState state, long unchangedSince)
			{
				this.State = state;
				this.UnchangedSince = unchangedSince;
			}

			public MirrorFileState State { get; }

			public long UnchangedSince { get; }
		}
	}
}
