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
using System.IO;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// What one <see cref="BrowserStorageMirror"/> mirrors, what it leaves alone, and how quickly.
	/// </summary>
	/// <remarks>
	/// <para>The mirror machinery is generic; this is where an application says what it wants of it. The
	/// division is deliberate: agg-sharp knows how to keep a directory and a key-value store in step, and it
	/// knows nothing whatever about settings tables, caches or gcode - so every judgement of the form "this
	/// path is worth bytes in the user's browser storage" or "this file must not wait" is written by the head
	/// and read here.</para>
	/// </remarks>
	public sealed class MirrorPolicy
	{
		/// <param name="rootPath">The directory to mirror. Keys are paths relative to it.</param>
		public MirrorPolicy(string rootPath)
		{
			if (string.IsNullOrEmpty(rootPath))
			{
				throw new ArgumentException("A mirror needs a root directory to mirror.", nameof(rootPath));
			}

			this.RootPath = rootPath;
			this.DatabaseName = DatabaseNameForRoot(rootPath);
		}

		/// <summary>The directory being mirrored.</summary>
		public string RootPath { get; }

		/// <summary>
		/// The IndexedDB database the mirror lives in, derived from <see cref="RootPath"/>.
		/// </summary>
		/// <remarks>
		/// <para><b>Why the root is in the name.</b> IndexedDB is keyed by origin, and a page served from one
		/// origin may be more than one build of the app: MatterCAD's Debug configuration puts its user data in
		/// <c>MatterCAD_Debug</c> and its Release configuration in <c>MatterCAD</c>. Sharing one database
		/// between them would mean a Debug build silently reading, sweeping and deleting a user's Release
		/// settings - the mirror's delete pass would remove every key it saw no file for, so opening the other
		/// configuration once would be enough to lose the first one's data. Deriving the name from the root
		/// makes the two simply different databases, which is what their different directories already said.
		/// </para>
		/// <para>Settable so a head that wants a different partitioning (per user, per document) can say so;
		/// changing it after the mirror opened has no effect, since the store is opened once at boot.</para>
		/// </remarks>
		public string DatabaseName { get; set; }

		/// <summary>
		/// Relative paths - a file or a whole folder - that are never mirrored. Compared case-insensitively
		/// against the start of a key, on '/' boundaries.
		/// </summary>
		/// <remarks>
		/// The point is not disk space, it is honesty about what "persistent" means: a cache and a scratch
		/// folder are regenerable by definition, and pushing them into a store the browser may evict under
		/// pressure spends the user's quota on bytes nobody would miss. Excluded paths are skipped by the walk
		/// (they are never even statted) and by the plan (so a key left behind by an older policy is not
		/// deleted on sight either - it is simply not this mirror's business).
		/// </remarks>
		public IReadOnlyList<string> ExcludedPaths { get; set; } = Array.Empty<string>();

		/// <summary>
		/// How often the write-behind sweep walks the root, in seconds.
		/// </summary>
		public double SweepIntervalSeconds { get; set; } = 1;

		/// <summary>
		/// How long a changed file must sit unchanged before it is pushed, in seconds. This is the debounce.
		/// </summary>
		/// <remarks>
		/// A design being edited is rewritten repeatedly; pushing every intermediate state would spend a
		/// transaction per keystroke-sized change and, on a large file, could keep the store permanently
		/// behind. Waiting for the file to settle costs a bounded amount of durability, and the bound is this
		/// value plus the sweep interval. Zero pushes on the first sweep that sees the change.
		/// </remarks>
		public double QuietPeriodSeconds { get; set; } = 2;

		/// <summary>
		/// Keys that skip the quiet period entirely - pushed by the first sweep that sees them change.
		/// </summary>
		/// <remarks>
		/// <para>For the small, committed, one-write-per-change files: MatterCAD points this at its datastore's
		/// <c>db/*.json</c> tables, which are kilobytes, are rewritten exactly once per committed row, and are
		/// where every user setting the application has ever been told lives. Losing the last two seconds of
		/// those is losing the setting the user just changed.</para>
		/// <para>This predicate is also the whole of the "per-commit immediate push" the mirror promises, and
		/// the reason the datastore needs no hook into the mirror at all: <c>MemorySqlite</c> already writes
		/// through to its JSON file on every commit, so a commit is visible as a changed file on the very next
		/// sweep. A callback from the datastore into the mirror would deliver the same push a fraction of a
		/// second earlier at the price of a browser-only dependency in the core library.</para>
		/// </remarks>
		public Func<string, bool> PushImmediately { get; set; }

		/// <summary>
		/// The database name <paramref name="rootPath"/> maps to. See <see cref="DatabaseName"/> for why.
		/// </summary>
		public static string DatabaseNameForRoot(string rootPath)
		{
			// Separators normalized and any trailing one dropped so that the same directory named two ways
			// ("/MatterCAD", "/MatterCAD/") is one database and not two.
			string normalized = (rootPath ?? string.Empty).Replace('\\', '/').TrimEnd('/');

			return "agg-storage-mirror:" + normalized;
		}

		/// <summary>
		/// Whether <paramref name="key"/> is inside one of <see cref="ExcludedPaths"/>.
		/// </summary>
		public bool IsExcluded(string key)
		{
			if (string.IsNullOrEmpty(key))
			{
				return false;
			}

			foreach (string excluded in this.ExcludedPaths)
			{
				string prefix = excluded.Replace('\\', '/').Trim('/');

				if (prefix.Length == 0)
				{
					continue;
				}

				if (key.Equals(prefix, StringComparison.OrdinalIgnoreCase)
					|| (key.Length > prefix.Length
						&& key[prefix.Length] == '/'
						&& key.StartsWith(prefix, StringComparison.OrdinalIgnoreCase)))
				{
					return true;
				}
			}

			return false;
		}

		/// <summary>
		/// Whether <paramref name="key"/> skips the quiet period. See <see cref="PushImmediately"/>.
		/// </summary>
		public bool IsImmediate(string key)
		{
			return this.PushImmediately != null && this.PushImmediately(key);
		}

		/// <summary>
		/// Turns a full path under <see cref="RootPath"/> into the key it is stored under: the relative path,
		/// with forward slashes, so a store written by one host reads back on another.
		/// </summary>
		public string KeyForPath(string fullPath)
		{
			string relative = Path.GetRelativePath(this.RootPath, fullPath);

			return relative.Replace('\\', '/');
		}

		/// <summary>
		/// The reverse of <see cref="KeyForPath"/>: where in the mirrored tree a key's bytes belong.
		/// </summary>
		public string PathForKey(string key)
		{
			return Path.Combine(this.RootPath, key.Replace('/', Path.DirectorySeparatorChar));
		}
	}
}
