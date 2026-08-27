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
	/// An <see cref="IStaticData"/> served out of a zip archive held in memory: the asset tree of a host
	/// that has no asset folder to read. The browser host fetches one zip at boot and installs this as
	/// <see cref="StaticData.Instance"/>; nothing else about the app changes.
	/// </summary>
	/// <remarks>
	/// The archive is opened once in read mode and kept open, so the central directory - not a scan -
	/// answers the existence and enumeration calls that the synchronous <see cref="IStaticData"/> contract
	/// demands (see its remarks on why the API is not async).
	///
	/// Lookup is case-insensitive on purpose. The assets were authored on Windows and macOS, whose
	/// filesystems fold case, so hundreds of call sites carry paths whose case does not match the file on
	/// disk and nobody ever noticed. A zip's central directory - like MEMFS, like Linux - is case-exact, so
	/// serving these assets case-sensitively would detonate every one of those latent mismatches at once,
	/// in the browser, as blank icons. Fold the case here instead.
	/// </remarks>
	public class ZipStaticData : StaticDataBase, IDisposable
	{
		private readonly ZipArchive archive;

		// Set when this instance created the stream (the byte[] constructor) and therefore must close it.
		private readonly Stream ownedStream;

		// Case-insensitive because the assets are; see the class remarks.
		private readonly Dictionary<string, ZipArchiveEntry> filesByPath = new Dictionary<string, ZipArchiveEntry>(StringComparer.OrdinalIgnoreCase);

		private readonly Dictionary<string, DirectoryContents> directories = new Dictionary<string, DirectoryContents>(StringComparer.OrdinalIgnoreCase);

		// ZipArchive is not thread safe and neither is entry decompression; the base class's icon pipeline
		// is already called from more than one thread.
		private readonly object archiveLocker = new object();

		private readonly string stagingPath;

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

			this.ownedStream = ownsStream ? zipStream : null;
			this.archive = new ZipArchive(zipStream, ZipArchiveMode.Read, leaveOpen: !ownsStream);

			// One staging directory per instance, so two providers (or two processes) cannot collide on a
			// half-extracted file, and so dispose can delete the whole thing.
			this.stagingPath = Path.Combine(Path.GetTempPath(), "ZipStaticData_" + Guid.NewGuid().ToString("N"));

			this.BuildIndex();
		}

		/// <summary>
		/// Gets the directory this instance extracts to when <see cref="MapPath"/> is called. Deleted on dispose.
		/// </summary>
		public string StagingPath => stagingPath;

		/// <inheritdoc/>
		public override bool DirectoryExists(string path)
		{
			return directories.ContainsKey(NormalizePath(path));
		}

		/// <inheritdoc/>
		public override bool FileExists(string path)
		{
			return filesByPath.ContainsKey(NormalizePath(path));
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

			archive.Dispose();
			ownedStream?.Dispose();

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

		private void BuildIndex()
		{
			// The root has to exist even for an empty archive: DirectoryExists("") and GetFiles("") are how
			// the asset root itself gets walked.
			directories[string.Empty] = new DirectoryContents();

			foreach (var entry in archive.Entries)
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
					EnsureDirectory(fullName);
					continue;
				}

				var parent = ParentOf(fullName);
				EnsureDirectory(parent);

				// First entry wins on a case-only duplicate. An archive can hold "Icons/A.png" and
				// "Icons/a.png" - a case-folding filesystem cannot, so the assets never do - and silently
				// serving one of them beats throwing at construction, which would be a boot failure.
				if (!filesByPath.ContainsKey(fullName))
				{
					filesByPath.Add(fullName, entry);
					directories[parent].Files.Add(fullName);
				}
			}

			foreach (var contents in directories.Values)
			{
				contents.Sort();
			}
		}

		private void EnsureDirectory(string path)
		{
			if (directories.ContainsKey(path))
			{
				return;
			}

			directories[path] = new DirectoryContents();

			if (path.Length == 0)
			{
				return;
			}

			var parent = ParentOf(path);
			EnsureDirectory(parent);
			directories[parent].SubDirectories.Add(path);
		}

		private DirectoryContents GetContents(string path)
		{
			var normalized = NormalizePath(path);

			if (directories.TryGetValue(normalized, out var contents))
			{
				return contents;
			}

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
