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
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;

namespace MatterHackers.Agg.Platform.Browser
{
	/// <summary>
	/// One event of a file drag over the canvas, as <c>input.js</c> reports it. See
	/// <see cref="BrowserFileDrop.Handle"/> for the order they arrive in.
	/// </summary>
	public readonly struct BrowserFileDragEvent
	{
		/// <summary>dragover, dragleave, dropstart, dropfile, dropchunk, dropfileerror or dropend.</summary>
		public string Type { get; init; }

		/// <summary>CSS pixels from the canvas's padding box, like a pointer event's offsetX.</summary>
		public double OffsetX { get; init; }

		/// <summary>CSS pixels from the canvas's padding box, like a pointer event's offsetY.</summary>
		public double OffsetY { get; init; }

		/// <summary>
		/// For a dragover: one entry per dragged file, its name - or empty where the browser hid it, which
		/// every engine does for a real drag from the desktop (the names are only readable on the drop).
		/// </summary>
		public IReadOnlyList<string> Names { get; init; }

		/// <summary>For a dragover: one entry per dragged file, its MIME type, or empty when the browser has none.</summary>
		public IReadOnlyList<string> MimeTypes { get; init; }

		/// <summary>For a dropfile: the file's name. For a dropfileerror: why it could not be read.</summary>
		public string Name { get; init; }

		/// <summary>For a dropfile: its size in bytes as the browser reports it, so the staged file is sized once.</summary>
		public long Size { get; init; }

		/// <summary>For a dropchunk: the next slice of the current file's bytes.</summary>
		public byte[] Bytes { get; init; }
	}

	/// <summary>
	/// Turns the DOM's drag-and-drop events on the canvas into agg's drop sequence
	/// (<see cref="FileDropDispatcher"/>), and stages the dropped files into the wasm file system so the
	/// widget that takes them gets paths it can open, exactly as an open dialog's files are.
	/// </summary>
	/// <remarks>
	/// <para><b>A hover never knows the real files.</b> A browser keeps a drag's contents protected until it is
	/// dropped: while it hovers, <c>DataTransfer</c> says how many files there are and (usually) their MIME
	/// types, but not their names and never their bytes. The mac and Windows hosts hover with the real
	/// paths, and agg's widgets decide <see cref="MouseEventArgs.AcceptDrop"/> from the extension. So the
	/// hover here carries stand-in paths (<see cref="HoverPaths"/>) named from the MIME type - a video/mp4
	/// hovers as <c>dragged-1.mp4</c> - which is enough for an extension test. They are not files; nothing
	/// may open them, and nothing does, because only the drop is ever acted on.</para>
	/// <para><b>An unknown type is not a refusal.</b> Plenty of the files agg applications take have no MIME
	/// type a browser knows (an .stl or a .mcx reports an empty one). Refusing those would show "no drop" over
	/// a widget that would have taken them, and the browser then never fires the drop at all. So when a hover's
	/// stand-ins are guesses, the drag is shown as a copy regardless of the widgets' answer; the drop still
	/// goes to the widget under the pointer, which ignores files it does not want, as it does on Windows.</para>
	/// <para><b>The answer is one hover behind.</b> The browser wants dropEffect set inside the dragover
	/// listener, but agg is driven from the frame tick, not from DOM listeners (see
	/// <see cref="BrowserInputEvent"/>). So each hover is posted to the tick and the listener answers with
	/// the last one the tick delivered. dragover repeats every few tens of milliseconds while the pointer is
	/// over the canvas, moving or not, so the lag is one repeat and never outlasts a pause before dropping.</para>
	/// <para><b>A drop streams.</b> The files are read in slices (<c>input.js</c> does the reading) and each
	/// slice is appended to its staged file as it arrives, so a video of hundreds of megabytes is read once,
	/// never held whole in the managed heap, and never stalls the page for longer than one slice. The drop
	/// is delivered when the last file is staged. Each staged file is sized up front from what the browser
	/// says it is, because the in-memory file system otherwise regrows (and so copies) it on many of the
	/// appends, and is trimmed to what was actually written when it closes.</para>
	/// <para><b>The receiver owns the staged files.</b> A widget handed a drop's paths may keep them as long as
	/// it needs them - a queued import may read them long after the drop. Nothing here deletes them; a receiver
	/// that is finished calls <see cref="BrowserFileStaging.Release"/>, which removes the drop's directory.
	/// One that never does leaves them resident in the tab's memory, as an open dialog's files are.</para>
	/// </remarks>
	public sealed class BrowserFileDrop
	{
		/// <summary>Where hover stand-in paths point. Never created; see the class remarks.</summary>
		public static string HoverDirectory => Path.Combine(BrowserFileStaging.StagingRoot, "drag-hover");

		/// <summary>
		/// The extension a MIME type means, for the types agg applications take. A type missing here makes
		/// its stand-in a guess, which only makes the drag show as a copy; see the class remarks.
		/// </summary>
		private static readonly Dictionary<string, string> ExtensionByMimeType = new(StringComparer.OrdinalIgnoreCase)
		{
			["image/jpeg"] = ".jpg",
			["image/png"] = ".png",
			["image/tiff"] = ".tif",
			["image/bmp"] = ".bmp",
			["image/gif"] = ".gif",
			["image/webp"] = ".webp",
			["image/svg+xml"] = ".svg",
			["video/mp4"] = ".mp4",
			["video/quicktime"] = ".mov",
			["video/webm"] = ".webm",
			["video/x-msvideo"] = ".avi",
			["video/x-matroska"] = ".mkv",
			["model/stl"] = ".stl",
			["model/x.stl-binary"] = ".stl",
			["model/x.stl-ascii"] = ".stl",
			["application/sla"] = ".stl",
			["application/vnd.ms-pki.stl"] = ".stl",
			["model/3mf"] = ".3mf",
			["application/vnd.ms-package.3dmanufacturing-3dmodel+xml"] = ".3mf",
			["model/obj"] = ".obj",
			["application/zip"] = ".zip",
			["application/x-zip-compressed"] = ".zip",
			["application/json"] = ".json",
			["text/plain"] = ".txt",
		};

		private readonly Func<GuiWidget> windowSource;
		private readonly Func<BrowserBackingSize> backingSource;
		private readonly Action<Action> postToTick;
		private readonly Func<string, Stream> openStagedFile;

		private bool lastHoverAccepted;
		private bool lastHoverWasGuess;

		private string dropDirectory;
		private Vector2 dropPosition;
		private readonly List<string> stagedNames = new List<string>();
		private readonly List<string> stagedPaths = new List<string>();
		private Stream currentFile;
		private string currentPath;

		/// <param name="windowSource">The agg window a drag is over, read when the event is delivered; null
		/// when there is none, which drops the event.</param>
		/// <param name="backingSource">The canvas's backing size, read when the event arrives - the same
		/// moment a pointer event's position is converted, and for the same reason.</param>
		/// <param name="postToTick">How work reaches the frame tick: <see cref="UiThread.RunOnIdle(Action)"/>
		/// in a page, and something a test can run on demand on a desktop.</param>
		/// <param name="openStagedFile">Creates the staged file at a path; null for a real file. A test's way
		/// to make a write fail.</param>
		public BrowserFileDrop(
			Func<GuiWidget> windowSource,
			Func<BrowserBackingSize> backingSource,
			Action<Action> postToTick,
			Func<string, Stream> openStagedFile = null)
		{
			this.windowSource = windowSource;
			this.backingSource = backingSource;
			this.postToTick = postToTick;
			this.openStagedFile = openStagedFile ?? (path => new FileStream(path, FileMode.CreateNew, FileAccess.Write));
		}

		/// <summary>
		/// Whether a drop is being staged: begun and not yet ended.
		/// </summary>
		public bool IsStaging => this.dropDirectory != null;

		/// <summary>The directory the drop being staged is writing into; null when none is.</summary>
		public string StagingDirectory => this.dropDirectory;

		/// <summary>
		/// Takes one drag event, returning what the dragover listener should set dropEffect to: true for
		/// "copy", false for "none". The answer only matters for a dragover.
		/// </summary>
		/// <remarks>
		/// A drop arrives as dropstart, then for each file a dropfile followed by its dropchunks (or a
		/// dropfileerror if it could not be read), then dropend. <c>input.js</c> sends a whole drop before the
		/// next one starts.
		/// </remarks>
		public bool Handle(BrowserFileDragEvent dragEvent)
		{
			switch (dragEvent.Type)
			{
				case "dragover":
					return this.DragOver(dragEvent);

				case "dragleave":
					this.PostExit();
					return false;

				case "dropstart":
					this.BeginDrop(dragEvent.OffsetX, dragEvent.OffsetY);
					return false;

				case "dropfile":
					this.BeginFile(dragEvent.Name, dragEvent.Size);
					return false;

				case "dropchunk":
					this.AppendChunk(dragEvent.Bytes);
					return false;

				case "dropfileerror":
					this.AbandonFile(dragEvent.Name);
					return false;

				case "dropend":
					this.EndDrop();
					return false;

				default:
					return false;
			}
		}

		/// <summary>
		/// The paths a hover carries: under <see cref="HoverDirectory"/>, the real name where the browser
		/// gave one and a stand-in named from the MIME type where it did not.
		/// </summary>
		/// <param name="allKnown">False when any stand-in had to be named without knowing its extension.</param>
		public static List<string> HoverPaths(IReadOnlyList<string> names, IReadOnlyList<string> mimeTypes, out bool allKnown)
		{
			allKnown = true;
			var paths = new List<string>();
			int count = Math.Max(names?.Count ?? 0, mimeTypes?.Count ?? 0);

			for (int index = 0; index < count; index++)
			{
				string name = names != null && index < names.Count ? names[index] : null;

				if (string.IsNullOrEmpty(name))
				{
					string mimeType = mimeTypes != null && index < mimeTypes.Count ? mimeTypes[index] : null;
					string extension = null;

					if (string.IsNullOrEmpty(mimeType)
						|| !ExtensionByMimeType.TryGetValue(mimeType, out extension))
					{
						allKnown = false;
					}

					name = $"dragged-{index + 1}{extension}";
				}

				paths.Add(Path.Combine(HoverDirectory, BrowserFileStaging.SanitizeFileName(name)));
			}

			return paths;
		}

		private bool DragOver(BrowserFileDragEvent dragEvent)
		{
			List<string> paths = HoverPaths(dragEvent.Names, dragEvent.MimeTypes, out bool allKnown);
			Vector2 position = this.ToAggPosition(dragEvent.OffsetX, dragEvent.OffsetY);

			this.Post(window =>
			{
				this.lastHoverAccepted = FileDropDispatcher.Hover(window, paths, position.X, position.Y);
				this.lastHoverWasGuess = !allKnown;
			});

			return this.lastHoverAccepted || this.lastHoverWasGuess;
		}

		private void BeginDrop(double offsetX, double offsetY)
		{
			// A drop that never saw its end (the page threw between files) is delivered with what it had
			// rather than leaking an open file handle into the next one.
			if (this.IsStaging)
			{
				this.EndDrop();
			}

			// On the tick, behind any hover still queued, for the reason PostExit gives.
			this.postToTick(this.ClearHoverAnswer);
			this.dropPosition = this.ToAggPosition(offsetX, offsetY);
			this.dropDirectory = BrowserFileStaging.CreateRequestDirectory("drop");
		}

		private void BeginFile(string name, long size)
		{
			this.FinishCurrentFile();

			// A file with no drop around it (the page lost the start) has nowhere to go.
			if (!this.IsStaging)
			{
				return;
			}

			string fileName = BrowserFileStaging.UniqueFileName(this.stagedNames, BrowserFileStaging.SanitizeFileName(name));

			this.currentPath = Path.Combine(this.dropDirectory, fileName);
			this.stagedNames.Add(fileName);
			this.stagedPaths.Add(this.currentPath);

			try
			{
				this.currentFile = this.openStagedFile(this.currentPath);

				if (size > 0)
				{
					this.currentFile.SetLength(size);
				}
			}
			catch (Exception openException)
			{
				this.AbandonFile(openException.Message);
			}
		}

		private void AppendChunk(byte[] bytes)
		{
			if (this.currentFile == null)
			{
				return;
			}

			try
			{
				this.currentFile.Write(bytes ?? Array.Empty<byte>());
			}
			catch (Exception writeException)
			{
				// A write that failed part way leaves a truncated file, which must never be delivered as if it
				// were the whole one; the rest of its slices then fall on the floor until the next dropfile.
				this.AbandonFile(writeException.Message);
			}
		}

		/// <summary>
		/// Drops a file the browser could not finish reading, so the widget is never handed half of one.
		/// </summary>
		private void AbandonFile(string reason)
		{
			if (this.currentPath == null)
			{
				return;
			}

			string abandoned = this.currentPath;
			this.stagedPaths.Remove(abandoned);

			try
			{
				this.CloseCurrentFile();
			}
			catch (Exception closeException)
			{
				// Flushing what a failed write left buffered can fail the same way; the file is going anyway.
				Console.Error.WriteLine($"BrowserFileDrop could not close '{Path.GetFileName(abandoned)}': {closeException.Message}");
			}

			File.Delete(abandoned);
			Console.Error.WriteLine($"BrowserFileDrop could not read '{Path.GetFileName(abandoned)}': {reason}");
		}

		private void EndDrop()
		{
			this.FinishCurrentFile();

			string directory = this.dropDirectory;
			var paths = new List<string>(this.stagedPaths);
			Vector2 position = this.dropPosition;

			this.dropDirectory = null;
			this.stagedNames.Clear();
			this.stagedPaths.Clear();

			if (directory == null)
			{
				return;
			}

			if (paths.Count == 0)
			{
				// Nothing could be read. The hover highlight still has to go out, and the empty directory is
				// this class's to sweep up.
				Directory.Delete(directory, recursive: true);
				this.PostExit();
				return;
			}

			this.Post(window => FileDropDispatcher.Drop(window, paths, position.X, position.Y));
		}

		/// <summary>
		/// Closes the file being staged, and takes it out of the drop if even that fails - its last bytes may
		/// not have reached the file system.
		/// </summary>
		private void FinishCurrentFile()
		{
			string path = this.currentPath;

			try
			{
				this.CloseCurrentFile();
			}
			catch (Exception closeException)
			{
				this.stagedPaths.Remove(path);
				File.Delete(path);
				Console.Error.WriteLine($"BrowserFileDrop could not finish '{Path.GetFileName(path)}': {closeException.Message}");
			}
		}

		/// <summary>
		/// Closes the file being staged, trimming it to what was written: it was sized from what the browser
		/// said, and a read that ended short must not leave zeros on the end.
		/// </summary>
		private void CloseCurrentFile()
		{
			Stream file = this.currentFile;
			this.currentFile = null;
			this.currentPath = null;

			if (file == null)
			{
				return;
			}

			try
			{
				if (file.CanSeek && file.Length != file.Position)
				{
					file.SetLength(file.Position);
				}
			}
			finally
			{
				file.Dispose();
			}
		}

		/// <summary>
		/// Ends the hover, clearing the dragover answer on the tick rather than now: a hover posted before
		/// this and still queued writes the answer when it runs, and would light it again after a clear made
		/// here.
		/// </summary>
		private void PostExit()
		{
			this.postToTick(this.ClearHoverAnswer);
			this.Post(window => FileDropDispatcher.Exit(window));
		}

		private void ClearHoverAnswer()
		{
			this.lastHoverAccepted = false;
			this.lastHoverWasGuess = false;
		}

		private Vector2 ToAggPosition(double offsetX, double offsetY)
		{
			BrowserBackingSize backing = this.backingSource();

			return BrowserPointer.ToAggPosition(offsetX, offsetY, backing.DevicePixelRatio, backing.PixelHeight);
		}

		private void Post(Action<GuiWidget> action)
		{
			this.postToTick(() =>
			{
				GuiWidget window = this.windowSource();

				if (window != null)
				{
					action(window);
				}
			});
		}
	}
}
