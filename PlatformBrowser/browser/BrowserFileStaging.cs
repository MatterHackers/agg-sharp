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
	/// Where a browser file dialog's bytes live while agg treats them as a file, and how they are named.
	/// </summary>
	/// <remarks>
	/// <para>A page cannot hand out a real path: what a file picker yields is a <c>File</c> object whose
	/// bytes are only readable through a promise, and the user's actual path is deliberately hidden. Every
	/// caller of <see cref="IFileDialogProvider.OpenFileDialog"/>, though, expects a path it can open. So
	/// the bytes are written into the wasm virtual file system - a real, if entirely in-memory, file system
	/// that <c>System.IO</c> works against normally - and those paths are what the callback carries.</para>
	/// <para>The write is done from managed code rather than from JS poking at Emscripten's <c>FS</c> object,
	/// which is the other way it could have gone. This way the file system is reached through the same
	/// <c>System.IO</c> every other platform layer uses, the naming rules below are testable on a desktop,
	/// and nothing depends on how the runtime happens to expose its Emscripten module today.</para>
	/// <para>The consequence to know: an opened file is resident in the wasm heap in its entirety, twice for
	/// a moment (the browser's copy and this one). W5's memory work is where that gets revisited.</para>
	/// </remarks>
	public static class BrowserFileStaging
	{
		/// <summary>
		/// The folder every dialog's staging directory is made under. Under wasm this is <c>/tmp</c> in the
		/// virtual file system; on a desktop - where only the tests go - it is the real temp folder.
		/// </summary>
		public static string StagingRoot => Path.Combine(Path.GetTempPath(), "agg-file-dialogs");

		/// <summary>
		/// Makes a fresh, empty directory for one dialog's files.
		/// </summary>
		/// <remarks>
		/// One per dialog rather than one shared folder so that two files chosen in different dialogs can
		/// have the same name without either being renamed, and so a save's cleanup can delete a whole
		/// directory without wondering what else is in it.
		/// </remarks>
		/// <param name="purpose">A short word that goes in the directory name, so a devtools file system
		/// listing says which dialog left it behind.</param>
		public static string CreateRequestDirectory(string purpose)
		{
			string directory = Path.Combine(StagingRoot, $"{purpose}-{Guid.NewGuid():N}");

			Directory.CreateDirectory(directory);

			return directory;
		}

		/// <summary>
		/// Reduces whatever the browser called a file to something safe to use as one path segment.
		/// </summary>
		/// <remarks>
		/// A picker normally reports a bare name with no path in it, but not always - a directory pick
		/// reports a relative path, and nothing stops a name containing a separator on a platform where it
		/// is legal. Taking only the last segment is what keeps a staged file inside its own directory
		/// rather than somewhere a <c>..</c> pointed. An empty result falls back to a name rather than
		/// yielding a path that is just a directory.
		/// </remarks>
		public static string SanitizeFileName(string name)
		{
			if (string.IsNullOrWhiteSpace(name))
			{
				return "file";
			}

			// Both separators, whatever this platform's own is: the name came from the user's machine, not
			// from this one.
			string lastSegment = name.Replace('\\', '/');
			int lastSlash = lastSegment.LastIndexOf('/');
			if (lastSlash >= 0)
			{
				lastSegment = lastSegment.Substring(lastSlash + 1);
			}

			var cleaned = new System.Text.StringBuilder(lastSegment.Length);
			foreach (char character in lastSegment)
			{
				// Control characters and the invalid set, replaced rather than dropped so two names that
				// differed only in a stripped character do not collide into one.
				cleaned.Append(char.IsControl(character) || Array.IndexOf(Path.GetInvalidFileNameChars(), character) >= 0
					? '_'
					: character);
			}

			string sanitized = cleaned.ToString().Trim();

			// "." and ".." survive everything above and are not names at all.
			return sanitized.Length == 0 || sanitized == "." || sanitized == ".." ? "file" : sanitized;
		}

		/// <summary>
		/// The name to stage a file under given the names already used in the same directory, so a
		/// multi-select of two files that are both called "part.stl" stages two files and not one.
		/// </summary>
		/// <remarks>
		/// The suffix goes before the extension - <c>part (2).stl</c>, the shape every desktop file manager
		/// uses - because callers switch on the extension and <c>part.stl (2)</c> would not load.
		/// </remarks>
		public static string UniqueFileName(ICollection<string> namesAlreadyUsed, string fileName)
		{
			if (!namesAlreadyUsed.Contains(fileName))
			{
				return fileName;
			}

			string stem = Path.GetFileNameWithoutExtension(fileName);
			string extension = Path.GetExtension(fileName);

			for (int suffix = 2; ; suffix++)
			{
				string candidate = $"{stem} ({suffix}){extension}";

				if (!namesAlreadyUsed.Contains(candidate))
				{
					return candidate;
				}
			}
		}
	}
}
