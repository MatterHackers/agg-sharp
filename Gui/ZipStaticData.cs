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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// An <see cref="IStaticData"/> served out of one or more zip archives held in memory: the asset tree
	/// of a host that has no asset folder to read. The browser host fetches a boot archive, installs this
	/// as <see cref="StaticData.Instance"/>, and layers a second archive in with <see cref="AddArchive(byte[])"/>
	/// once it arrives; nothing else about the app changes.
	/// </summary>
	/// <remarks>
	/// Each archive is opened once in read mode and kept open, so the central directory - not a scan -
	/// answers the existence and enumeration calls that the synchronous <see cref="IStaticData"/> contract
	/// demands (see its remarks on why the API is not async).
	///
	/// Lookup is case-insensitive on purpose. The assets were authored on Windows and macOS, whose
	/// filesystems fold case, so hundreds of call sites carry paths whose case does not match the file on
	/// disk and nobody ever noticed. A zip's central directory - like MEMFS, like Linux - is case-exact, so
	/// serving these assets case-sensitively would detonate every one of those latent mismatches at once,
	/// in the browser, as blank icons. Fold the case here instead.
	///
	/// Layering rather than a composite <see cref="IStaticData"/>: the case folding, the directory index
	/// and the <see cref="MapPath"/> staging directory are all one instance's business, and a composite
	/// would have to re-implement all three to merge two providers' answers. One index over several
	/// archives is the smaller thing.
	/// </remarks>
	public class ZipStaticData : StaticDataBase, IDisposable
	{
		// In layering order: earlier archives win a path collision, so a boot archive's copy of an asset is
		// authoritative even if a later archive carries one too.
		private readonly List<ZipArchive> archives = new List<ZipArchive>();

		// The streams this instance created (the byte[] constructors) and therefore must close.
		private readonly List<Stream> ownedStreams = new List<Stream>();

		// Case-insensitive because the assets are; see the class remarks. Rebuilt and swapped whole by
		// AddArchive rather than mutated in place, so a reader that is part way through an enumeration
		// during a layering keeps seeing a coherent index instead of a half-merged one.
		private Dictionary<string, ZipArchiveEntry> filesByPath = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

		private Dictionary<string, DirectoryContents> directories = new Dictionary<string, DirectoryContents>(StringComparer.OrdinalIgnoreCase);

		// ZipArchive is not thread safe and neither is entry decompression; the base class's icon pipeline
		// is already called from more than one thread.
		private readonly object archiveLocker = new object();

		private readonly string stagingPath;

		// Paths already reported as missed while a deferred archive was outstanding, so a widget that asks
		// for the same absent icon on every layout pass produces one warning rather than sixty a second.
		private readonly HashSet<string> reportedMisses = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

		// What the outstanding deferred archive holds, when the host was able to say. Null means "unknown",
		// which is treated as "it could hold anything".
		private HashSet<string> deferredPaths;

		private bool disposed;

		/// <summary>
		/// Reads the asset tree from zip bytes - what a browser host gets back from fetch. The bytes are
		/// wrapped, not copied, and are held for the life of this instance.
		/// </summary>
		public ZipStaticData(byte[] zipBytes)
			: this(new MemoryStream(zipBytes ?? throw new ArgumentNullException(nameof(zipBytes)), writable: false), ownsStream: true)
		{
		}

		/// <summary>
		/// Reads the asset tree from a seekable stream. The stream is left open on dispose; the caller owns it.
		/// </summary>
		public ZipStaticData(Stream seekableZipStream)
			: this(seekableZipStream, ownsStream: false)
		{
		}

		private ZipStaticData(Stream zipStream, bool ownsStream)
		{
			// One staging directory per instance, so two providers (or two processes) cannot collide on a
			// half-extracted file, and so dispose can delete the whole thing.
			this.stagingPath = Path.Combine(Path.GetTempPath(), "ZipStaticData_" + Guid.NewGuid().ToString("N"));

			this.OpenAndIndex(zipStream, ownsStream);
		}

		/// <summary>
		/// Gets the directory this instance extracts to when <see cref="MapPath"/> is called. Deleted on dispose.
		/// </summary>
		public string StagingPath => stagingPath;

		/// <summary>
		/// Gets whether another archive is still on its way in - the window in which a path this instance
		/// cannot answer for may be an asset that has not arrived yet rather than one that does not exist.
		/// </summary>
		public bool ExpectingDeferredArchive { get; private set; }

		/// <summary>
		/// Says that another archive is coming, and - if the host knows - exactly what is in it.
		/// </summary>
		/// <param name="deferredPaths">
		/// The archive-relative paths the deferred archive holds, or null if the host cannot say. Given the
		/// list, only a miss on one of those paths is reported, which is the only kind that means anything;
		/// without it every miss is reported, because a false alarm beats a silent one.
		/// </param>
		/// <remarks>
		/// A host that splits its assets calls this before it starts the deferred download;
		/// <see cref="AddArchive(byte[])"/> ends the window. While it is open, a matching miss is reported
		/// once through <see cref="DeferredAssetMissed"/>. That report is the whole safety net for the split:
		/// the synchronous <see cref="IStaticData"/> contract cannot await a download, so a too-eager split
		/// degrades silently (a blank icon, an untranslated string) unless something says so out loud.
		/// Nothing here waits or retries - the answer is the same "not found" a genuinely absent asset gets,
		/// and the host is expected to have put everything the boot path reads in the first archive.
		/// </remarks>
		public void ExpectDeferredArchive(IEnumerable<string> deferredPaths = null)
		{
			lock (archiveLocker)
			{
				this.deferredPaths = deferredPaths == null
					? null
					: new HashSet<string>(deferredPaths.Select(NormalizePath), StringComparer.OrdinalIgnoreCase);

				this.ExpectingDeferredArchive = true;
			}
		}

		/// <summary>
		/// Gets or sets where a miss during the <see cref="ExpectingDeferredArchive"/> window is reported.
		/// Defaults to standard error.
		/// </summary>
		public Action<string> DeferredAssetMissed { get; set; } = message => Console.Error.WriteLine(message);

		/// <summary>
		/// Layers another archive in behind the ones already loaded, and stops treating misses as possible
		/// late arrivals. A path already served by an earlier archive keeps its earlier entry.
		/// </summary>
		/// <remarks>
		/// The whole index is rebuilt rather than merged into, because merging would have to reconcile the
		/// per-directory file and sub-directory lists entry by entry, and rebuilding a few thousand
		/// dictionary entries costs less than the code to do that correctly. The new index is swapped in
		/// under the archive lock, and readers pick it up whole.
		/// </remarks>
		public void AddArchive(byte[] zipBytes)
		{
			if (zipBytes == null)
			{
				throw new ArgumentNullException(nameof(zipBytes));
			}

			this.OpenAndIndex(new MemoryStream(zipBytes, writable: false), ownsStream: true);
		}

		/// <summary>
		/// Layers another archive in from a seekable stream. The stream is left open on dispose; the caller
		/// owns it. See <see cref="AddArchive(byte[])"/>.
		/// </summary>
		public void AddArchive(Stream seekableZipStream)
		{
			this.OpenAndIndex(seekableZipStream, ownsStream: false);
		}

		/// <inheritdoc/>
		public override bool DirectoryExists(string path)
		{
			var normalized = NormalizePath(path);
			var found = directories.ContainsKey(normalized);

			if (!found)
			{
				ReportIfDeferredMayHold("directory", normalized);
			}

			return found;
		}

		/// <inheritdoc/>
		public override bool FileExists(string path)
		{
			var normalized = NormalizePath(path);
			var found = filesByPath.ContainsKey(normalized);

			if (!found)
			{
				ReportIfDeferredMayHold("file", normalized);
			}

			return found;
		}

		/// <summary>
		/// Gets the sub-directories of the given directory as archive-relative paths, which can be handed
		/// straight back in - the shape <c>AppContext</c> relies on when it walks Themes/System.
		/// </summary>
		/// <remarks>
		/// Ordinal sorted. <see cref="IStaticData"/> promises no order (the disk provider hands back whatever
		/// the filesystem says), but an archive can hand back a stable one for free, and a stable asset order
		/// is worth more than matching the filesystem's arbitrary one.
		/// </remarks>
		public override IEnumerable<string> GetDirectories(string path)
		{
			return GetContents(path).SubDirectories;
		}

		/// <summary>
		/// Gets the files of the given directory as archive-relative paths, ordinal sorted (see
		/// <see cref="GetDirectories"/> on ordering).
		/// </summary>
		public override IEnumerable<string> GetFiles(string path)
		{
			return GetContents(path).Files;
		}

		/// <summary>
		/// Returns a seekable copy of the entry's bytes. The decompressing stream a zip entry hands out
		/// cannot seek, and the asset loaders - ImageSharp above all - seek freely.
		/// </summary>
		public override Stream OpenStream(string path)
		{
			return new MemoryStream(ReadEntryBytes(path));
		}

		/// <inheritdoc/>
		public override string ReadAllText(string path)
		{
			using (var reader = new StreamReader(OpenStream(path), detectEncodingFromByteOrderMarks: true))
			{
				return reader.ReadToEnd();
			}
		}

		/// <inheritdoc/>
		public override string[] ReadAllLines(string path)
		{
			var lines = new List<string>();

			using (var reader = new StreamReader(OpenStream(path), detectEncodingFromByteOrderMarks: true))
			{
				string line;
				while ((line = reader.ReadLine()) != null)
				{
					lines.Add(line);
				}
			}

			return lines.ToArray();
		}

		/// <summary>
		/// Extracts the asset to this instance's staging directory on demand and returns that real path,
		/// stamped with the zip entry's own last-write time.
		/// </summary>
		/// <remarks>
		/// Callers that ask for a path want a file: <c>SceneActions.AddPhilToBed</c> hands it to a mesh
		/// loader, and <c>ThumbnailsConfig.StaticThumbnailWriteTime</c> reads <c>LastWriteTimeUtc</c> off it
		/// to decide whether a cached thumbnail is stale. Both work unchanged against the extracted copy,
		/// which is the whole reason this extracts rather than throwing NotSupported - and the reason the
		/// timestamp is copied from the entry rather than left as "now", which would make every cached
		/// thumbnail look stale on every run.
		///
		/// Timestamp fidelity is bounded by the format: a zip stores MS-DOS local time at 2 second
		/// resolution, so the stamp can differ from the original file's by up to 2 seconds (and by the
		/// UTC offset difference if the archive crosses a DST boundary). Thumbnail staleness compares
		/// against cache files that are minutes-to-months apart, so seconds do not matter there.
		///
		/// A path that is not in the archive still gets a path back, exactly as the disk provider returns a
		/// path for a file that is not there. A directory gets its staging folder created but its contents
		/// are not materialized - no caller needs that, and extracting a whole subtree eagerly would undo
		/// the point of serving from the archive.
		/// </remarks>
		public override string MapPath(string path)
		{
			var normalized = NormalizePath(path);
			var mapped = Path.GetFullPath(Path.Combine(stagingPath, normalized));

			lock (archiveLocker)
			{
				if (filesByPath.TryGetValue(normalized, out var entry))
				{
					ExtractIfStale(entry, mapped);
				}
				else if (directories.ContainsKey(normalized))
				{
					Directory.CreateDirectory(mapped);
				}
				else
				{
					// A path back for something that is not here is the documented behaviour (see the
					// remarks), but during the deferred window it is also the shape a too-short boot asset
					// list takes: the caller gets a path it will find nothing at.
					ReportIfDeferredMayHold("entry", normalized);
				}
			}

			return mapped;
		}

		public void Dispose()
		{
			if (disposed)
			{
				return;
			}

			disposed = true;

			foreach (var openArchive in archives)
			{
				openArchive.Dispose();
			}

			foreach (var stream in ownedStreams)
			{
				stream.Dispose();
			}

			// Best effort: the staging copies are disposable by definition, and failing to clean up scratch
			// files is not worth throwing out of Dispose.
			try
			{
				if (Directory.Exists(stagingPath))
				{
					Directory.Delete(stagingPath, true);
				}
			}
			catch (IOException)
			{
			}
			catch (UnauthorizedAccessException)
			{
			}

			GC.SuppressFinalize(this);
		}

		/// <summary>
		/// Opens a zip over the stream, adds it to the layering and rebuilds the index over every archive.
		/// </summary>
		private void OpenAndIndex(Stream zipStream, bool ownsStream)
		{
			if (zipStream == null)
			{
				throw new ArgumentNullException(nameof(zipStream));
			}

			if (!zipStream.CanSeek)
			{
				// ZipArchive needs to seek to the central directory. Failing here names the real problem;
				// letting ZipArchive fail later reports a corrupt archive instead.
				throw new ArgumentException("The zip stream must be seekable.", nameof(zipStream));
			}

			var opened = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: !ownsStream);

			lock (archiveLocker)
			{
				archives.Add(opened);

				if (ownsStream)
				{
					ownedStreams.Add(zipStream);
				}

				this.BuildIndex();

				// Whatever the host was waiting for has landed. Cleared after the index so a read racing the
				// swap either misses and is reported, or hits - never misses silently.
				this.ExpectingDeferredArchive = false;
				this.deferredPaths = null;
				this.reportedMisses.Clear();
			}
		}

		/// <summary>
		/// Rebuilds the whole index from every loaded archive and swaps it in. Call under the archive lock.
		/// </summary>
		private void BuildIndex()
		{
			var rebuiltFiles = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);
			var rebuiltDirectories = new Dictionary<string, DirectoryContents>(StringComparer.OrdinalIgnoreCase);

			// The root has to exist even for an empty archive: DirectoryExists("") and GetFiles("") are how
			// the asset root itself gets walked.
			rebuiltDirectories[string.Empty] = new DirectoryContents();

			foreach (var openArchive in archives)
			{
				foreach (var entry in openArchive.Entries)
				{
					var fullName = NormalizePath(entry.FullName);

					if (fullName.Length == 0)
					{
						continue;
					}

					// A zip marks a directory entry with a trailing separator (and an empty Name). Everything
					// else is a file, whether or not its directories were given entries of their own.
					if (entry.FullName.EndsWith("/", StringComparison.Ordinal)
						|| entry.FullName.EndsWith("\\", StringComparison.Ordinal))
					{
						EnsureDirectory(rebuiltDirectories, fullName);
						continue;
					}

					var parent = ParentOf(fullName);
					EnsureDirectory(rebuiltDirectories, parent);

					// First entry wins on a case-only duplicate, and - because the archives are walked in
					// layering order - on a duplicate between archives too. An archive can hold "Icons/A.png"
					// and "Icons/a.png" - a case-folding filesystem cannot, so the assets never do - and
					// silently serving one of them beats throwing at construction, which would be a boot
					// failure. The same rule keeps a boot archive authoritative over a deferred one.
					if (!rebuiltFiles.ContainsKey(fullName))
					{
						rebuiltFiles.Add(fullName, entry);
						rebuiltDirectories[parent].Files.Add(fullName);
					}
				}
			}

			foreach (var contents in rebuiltDirectories.Values)
			{
				contents.Sort();
			}

			this.filesByPath = rebuiltFiles;
			this.directories = rebuiltDirectories;
		}

		private static void EnsureDirectory(Dictionary<string, DirectoryContents> into, string path)
		{
			if (into.ContainsKey(path))
			{
				return;
			}

			into[path] = new DirectoryContents();

			if (path.Length == 0)
			{
				return;
			}

			var parent = ParentOf(path);
			EnsureDirectory(into, parent);
			into[parent].SubDirectories.Add(path);
		}

		/// <summary>
		/// Says out loud that an asset was asked for, and answered "no", while a deferred archive was still
		/// outstanding - the one signal that a host's boot asset list is short. Once per path.
		/// </summary>
		private void ReportIfDeferredMayHold(string kind, string normalizedPath)
		{
			if (!this.ExpectingDeferredArchive)
			{
				return;
			}

			bool known;

			lock (archiveLocker)
			{
				if (!this.ExpectingDeferredArchive
					|| (deferredPaths != null && !deferredPaths.Contains(normalizedPath))
					|| !reportedMisses.Add(normalizedPath))
				{
					return;
				}

				known = deferredPaths != null;
			}

			this.DeferredAssetMissed?.Invoke(
				$"ZipStaticData: no {kind} '{normalizedPath}' in the loaded archives"
				+ (known
					? ", and the deferred archive that holds it has not arrived - it belongs in the boot archive."
					: ", and a deferred archive has not arrived yet - if it holds this asset, this belongs in the boot archive."));
		}

		private DirectoryContents GetContents(string path)
		{
			var normalized = NormalizePath(path);

			if (directories.TryGetValue(normalized, out var contents))
			{
				return contents;
			}

			ReportIfDeferredMayHold("directory", normalized);

			// The disk provider throws DirectoryNotFoundException here; match it so a caller that guards
			// with DirectoryExists behaves the same either side of the seam.
			throw new DirectoryNotFoundException("No such directory in the asset archive: " + path);
		}

		private static string ParentOf(string normalizedPath)
		{
			var lastSeparator = normalizedPath.LastIndexOf('/');
			return lastSeparator < 0 ? string.Empty : normalizedPath.Substring(0, lastSeparator);
		}

		/// <summary>
		/// Folds a caller's path into the archive's own shape: forward separators, no leading or trailing
		/// separator, no "./" prefix.
		/// </summary>
		/// <remarks>
		/// Callers build asset paths with <see cref="Path.Combine(string, string)"/>, which uses a backslash
		/// on Windows, and a zip only ever uses '/'. A path that came back out of <see cref="MapPath"/> is
		/// folded back to its archive-relative form too, because the disk provider's MapPath is idempotent
		/// (Path.Combine ignores a rooted second argument) and callers lean on that - GetFiles output gets
		/// mapped a second time all over the code base.
		/// </remarks>
		private string NormalizePath(string path)
		{
			if (string.IsNullOrEmpty(path))
			{
				return string.Empty;
			}

			var normalized = path.Replace('\\', '/');

			if (normalized.StartsWith(stagingPath.Replace('\\', '/'), StringComparison.OrdinalIgnoreCase))
			{
				normalized = normalized.Substring(stagingPath.Length);
			}

			if (normalized.StartsWith("./", StringComparison.Ordinal))
			{
				normalized = normalized.Substring(2);
			}

			return normalized.Trim('/');
		}

		private byte[] ReadEntryBytes(string path)
		{
			var normalized = NormalizePath(path);

			lock (archiveLocker)
			{
				if (!filesByPath.TryGetValue(normalized, out var entry))
				{
					ReportIfDeferredMayHold("file", normalized);

					// Same exception the disk provider raises, so callers that catch it still catch it.
					throw new FileNotFoundException("No such file in the asset archive: " + path, path);
				}

				// Deliberately not cached. Decompressed asset bytes would live in the wasm heap alongside
				// the zip itself, and the big assets (sample parts, fonts) are exactly the ones that would
				// blow the budget. W5's risk list names per-entry caching as the lever to pull if first
				// paint drags - pull it here, bounded, once a real boot says it is needed.
				var bytes = new byte[entry.Length];
				using (var entryStream = entry.Open())
				{
					entryStream.ReadExactly(bytes);
				}

				return bytes;
			}
		}

		private void ExtractIfStale(ZipArchiveEntry entry, string destination)
		{
			var entryWriteTimeUtc = entry.LastWriteTime.UtcDateTime;

			var existing = new FileInfo(destination);
			if (existing.Exists
				&& existing.Length == entry.Length
				&& existing.LastWriteTimeUtc == entryWriteTimeUtc)
			{
				return;
			}

			Directory.CreateDirectory(Path.GetDirectoryName(destination));

			using (var entryStream = entry.Open())
			using (var fileStream = File.Create(destination))
			{
				entryStream.CopyTo(fileStream);
			}

			File.SetLastWriteTimeUtc(destination, entryWriteTimeUtc);
		}

		private class DirectoryContents
		{
			public List<string> Files { get; } = new List<string>();

			public List<string> SubDirectories { get; } = new List<string>();

			public void Sort()
			{
				Files.Sort(StringComparer.Ordinal);
				SubDirectories.Sort(StringComparer.Ordinal);
			}
		}
	}
}
