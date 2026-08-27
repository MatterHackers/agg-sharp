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
using MatterHackers.Agg.Platform.Browser;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// File dialogs in a page: an <c>&lt;input type="file"&gt;</c> for open, and a download for save.
	/// </summary>
	/// <remarks>
	/// <para><b>The callback arrives after the call returns</b>, as it does on Linux and unlike mac and
	/// Windows, whose native panels spin a nested modal loop the provider can sit inside. A browser has no
	/// such loop to borrow and blocking the one thread would stop the picker ever being answered. So the
	/// return value means <i>a dialog was shown</i>, not <i>the user picked something</i>, and the answer
	/// comes through the callback on <see cref="UiThread"/> - see <c>LinuxFileDialogProvider</c>'s remarks,
	/// which is the same contract for the same reason.</para>
	/// <para><b>Cancel is silent</b>, matching all three desktop providers: no callback, and the caller's
	/// params are left as they were. <see cref="OpenFileDialog"/> clears them up front instead, so a params
	/// object reused across calls cannot hand back last time's answer. Note that some browsers raise no
	/// cancel event at all for a file input, in which case the silence is all there is - which is exactly
	/// why nothing may be left half-set on the way in.</para>
	/// <para><b>Open stages bytes into the virtual file system.</b> A page never learns the user's real path,
	/// so the paths this hands back are inside the wasm file system; see <see cref="BrowserFileStaging"/>.
	/// <see cref="ResolveFilePath"/> is the identity because those paths are already this platform's own.</para>
	/// <para><b>Save has no dialog at all.</b> The browser's own "where do you want this?" is the download
	/// it puts up when the bytes arrive, and it happens at the end rather than the beginning. So the save
	/// path handed to the caller is a staging path, and the provider watches the directory it is in: once a
	/// file is there and has stopped changing, the bytes are handed to the browser as a download and the
	/// staging directory is deleted. The directory rather than the path, because callers finish the path
	/// themselves - see <see cref="ResolveStagedFile"/>. The watching is polling from
	/// <see cref="UiThread"/>'s idle queue - a v1 mechanism with a known hole in it, argued out in
	/// <see cref="BrowserSaveWatch"/>.</para>
	/// <para><b>There is no folder picker.</b> <see cref="SelectFolderDialog"/> returns false. The File
	/// System Access API can grant a directory handle, but only on Chromium and only as a handle - not as a
	/// path any agg caller could use - so answering "no" is the honest form of not having one.</para>
	/// </remarks>
	public class BrowserFileDialogProvider : IFileDialogProvider
	{
		private readonly IBrowserFileDialogInterop interop;

		/// <summary>The provider <c>AggContext</c> constructs from its type string.</summary>
		public BrowserFileDialogProvider()
			: this(CreatePageInterop())
		{
		}

		/// <param name="interop">The page seam, or null for a provider that can show nothing - which is
		/// what a desktop test gets, and what lets the naming and filter rules be exercised there.</param>
		public BrowserFileDialogProvider(IBrowserFileDialogInterop interop)
		{
			this.interop = interop;
		}

		/// <summary>
		/// The staging directory the last dialog used. Not a place a user could navigate to - see the class
		/// remarks - but callers store and re-offer it, so it has to be something.
		/// </summary>
		public string LastDirectoryUsed { get; private set; }

		/// <summary>Staged paths are already this platform's own; nothing to translate.</summary>
		public string ResolveFilePath(string path) => path;

		/// <inheritdoc/>
		public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
		{
			// Reset first, exactly as the mac and Linux providers do, so a caller that keeps a params object
			// around never reads last time's answer as this time's. Doubly important here: see the class
			// remarks on browsers that never report a cancel.
			openParams.FileName = string.Empty;
			openParams.FileNames = null;

			if (this.interop == null)
			{
				return ReportNoPage("open");
			}

			string directory = BrowserFileStaging.CreateRequestDirectory("open");
			var stagedNames = new List<string>();
			var stagedPaths = new List<string>();

			this.interop.PickFiles(
				BrowserFileFilter.ToAcceptAttribute(openParams.Filter),
				openParams.MultiSelect,
				picked => StageOnePickedFile(directory, stagedNames, stagedPaths, picked),
				() => this.CompleteOpen(directory, stagedPaths, openParams, callback));

			return true;
		}

		/// <summary>
		/// Hands the caller a staging path to write to, and arranges for what it writes to be downloaded.
		/// </summary>
		/// <remarks>
		/// The callback is invoked unconditionally, which is the one place this differs in spirit from the
		/// desktop providers: there is nothing for the user to cancel yet. The browser's own cancel is the
		/// download prompt at the far end, long after the application has finished saving.
		/// </remarks>
		public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
		{
			if (this.interop == null)
			{
				return ReportNoPage("save");
			}

			string downloadName = BrowserFileStaging.SanitizeFileName(SuggestedSaveName(saveParams));
			string directory = BrowserFileStaging.CreateRequestDirectory("save");
			string stagingPath = Path.Combine(directory, downloadName);

			saveParams.FileName = stagingPath;
			saveParams.FileNames = new[] { stagingPath };
			this.LastDirectoryUsed = directory;

			this.WatchForCompletedSave(stagingPath);

			// Through the idle queue like every other answer this provider gives, so a caller cannot be
			// re-entered from inside its own SaveFileDialog call on one host and not on the others.
			UiThread.RunOnIdle(() => callback?.Invoke(saveParams));

			return true;
		}

		/// <summary>Always false: a page has no folder picker. See the class remarks.</summary>
		public bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
			=> false;

		/// <summary>
		/// Nothing to do: there is no file manager to reveal a file in, and the staged file the caller is
		/// holding a path to is inside this tab's memory rather than on the user's disk.
		/// </summary>
		public void ShowFileInFolder(string fileName)
		{
		}

		/// <summary>
		/// The name a save should download as: what the caller suggested, or something with the filter's
		/// first extension on it.
		/// </summary>
		/// <remarks>
		/// Only the file name of a suggested path is kept. A caller that passes a full desktop path - which
		/// is normal, since <c>InitialDirectory</c> and a name are how the other hosts are asked - would
		/// otherwise stage a file at that path in the virtual file system, where the directory does not
		/// exist and the write throws in the application's own save code.
		/// </remarks>
		public static string SuggestedSaveName(SaveFileDialogParams saveParams)
		{
			string suggested = saveParams.FileName;

			if (!string.IsNullOrWhiteSpace(suggested))
			{
				string name = Path.GetFileName(suggested.Replace('\\', '/'));

				if (!string.IsNullOrWhiteSpace(name))
				{
					return name;
				}
			}

			// The accept list's first entry is the filter's first pattern, which is what a save dialog on
			// any other host would have preselected.
			string accept = BrowserFileFilter.ToAcceptAttribute(saveParams.Filter);
			string extension = accept.Length == 0 ? string.Empty : accept.Split(',')[0];

			return "download" + extension;
		}

		/// <summary>
		/// The page seam in a browser, and nothing on a desktop. See <c>BrowserClipboard.CreatePageInterop</c>
		/// for why this is a method rather than a ternary.
		/// </summary>
		private static IBrowserFileDialogInterop CreatePageInterop()
		{
			if (OperatingSystem.IsBrowser())
			{
				return new BrowserPeripherals();
			}

			return null;
		}

		/// <summary>
		/// Says why no dialog appeared, and answers false the way <c>LinuxFileDialogProvider</c> does when
		/// neither helper is installed.
		/// </summary>
		private static bool ReportNoPage(string which)
		{
			Console.Error.WriteLine(
				$"BrowserFileDialogProvider cannot show a {which} dialog: it was constructed without a page to "
				+ "show one in, which happens when this provider is used outside a browser.");

			return false;
		}

		/// <summary>
		/// Writes one chosen file into the dialog's staging directory and records its path.
		/// </summary>
		/// <remarks>
		/// Called from a JS promise continuation, so a throw here would escape into the browser rather than
		/// into agg - the same containment <see cref="BrowserInputEvents"/> applies, and for the same reason.
		/// One unreadable file must not lose the others.
		/// </remarks>
		private static void StageOnePickedFile(
			string directory,
			ICollection<string> stagedNames,
			ICollection<string> stagedPaths,
			BrowserPickedFile picked)
		{
			try
			{
				string name = BrowserFileStaging.UniqueFileName(
					stagedNames, BrowserFileStaging.SanitizeFileName(picked.Name));

				string path = Path.Combine(directory, name);

				File.WriteAllBytes(path, picked.Bytes ?? Array.Empty<byte>());

				stagedNames.Add(name);
				stagedPaths.Add(path);
			}
			catch (Exception stagingException)
			{
				Console.Error.WriteLine($"BrowserFileDialogProvider could not stage '{picked.Name}': {stagingException}");
				UiThread.ReportUnhandledException(stagingException);
			}
		}

		/// <summary>
		/// Delivers an open dialog's answer, or cleans up after a cancel.
		/// </summary>
		private void CompleteOpen(
			string directory,
			List<string> stagedPaths,
			OpenFileDialogParams openParams,
			Action<OpenFileDialogParams> callback)
		{
			if (stagedPaths.Count == 0)
			{
				// Cancelled, or every file failed to stage. Silent either way (see the class remarks), but
				// the empty directory is this provider's to sweep up.
				TryDeleteDirectory(directory);
				return;
			}

			openParams.FileNames = stagedPaths.ToArray();
			openParams.FileName = stagedPaths[0];
			this.LastDirectoryUsed = directory;

			UiThread.RunOnIdle(() => callback?.Invoke(openParams));
		}

		/// <summary>
		/// Polls the directory <paramref name="stagingPath"/> is in from the idle queue until the file the
		/// application is writing there is finished, then downloads it. See <see cref="BrowserSaveWatch"/> for
		/// why polling and <see cref="ResolveStagedFile"/> for why the directory rather than the path.
		/// </summary>
		private void WatchForCompletedSave(string stagingPath)
		{
			var watch = new BrowserSaveWatch();
			long startedAtMs = UiThread.CurrentTimerMs;

			void Poll()
			{
				FileInfo stagedFile = ResolveStagedFile(stagingPath);
				bool exists = stagedFile != null;

				switch (watch.Observe(
					exists,
					exists ? stagedFile.Length : 0,
					(UiThread.CurrentTimerMs - startedAtMs) / 1000.0))
				{
					case SaveWatchDecision.Download:
						// Under the name it was written with rather than the one that was offered - see
						// ResolveStagedFile - so a caller that added its own extension downloads with it.
						this.DeliverSavedFile(stagedFile.FullName, stagedFile.Name);
						break;

					case SaveWatchDecision.GiveUp:
						// Quiet, like the screenshot give-up and unlike a staging failure: an application is
						// perfectly entitled to ask for a save path and then decide not to write anything
						// (a confirmation the user backed out of), and reporting that as a fault would file a
						// crash report for a user who did nothing wrong.
						Console.Error.WriteLine(
							$"BrowserFileDialogProvider gave up waiting for '{stagingPath}' to be written, after "
							+ $"{BrowserSaveWatch.GiveUpSeconds} seconds. Nothing was downloaded.");

						TryDeleteDirectory(Path.GetDirectoryName(stagingPath));
						break;

					default:
						UiThread.RunOnIdle(Poll, BrowserSaveWatch.PollIntervalSeconds);
						break;
				}
			}

			UiThread.RunOnIdle(Poll, BrowserSaveWatch.PollIntervalSeconds);
		}

		/// <summary>
		/// The file a save actually produced, or null if there is not yet exactly one to point at.
		/// </summary>
		/// <remarks>
		/// <para>Not simply <paramref name="stagingPath"/>, because applications routinely finish the path a
		/// save dialog hands them: MatterCAD's export page appends the export's extension when the chosen name
		/// does not already carry it, which every desktop host is happy with - the file lands beside the one
		/// the dialog named, on a disk where nothing more has to happen. Here something more does have to
		/// happen, and watching only the offered path means watching a file that is never written while the
		/// real one sits next to it, so nothing ever downloads.</para>
		/// <para>The staging directory is what makes the wider answer safe: it is made fresh for this one
		/// dialog and holds nothing else (see <see cref="BrowserFileStaging.CreateRequestDirectory"/>). More
		/// than one file in it is not a case with an answer - there is one download to give - so this waits
		/// rather than guessing, which for a caller writing a temporary file alongside its output means
		/// waiting until the temporary is gone.</para>
		/// </remarks>
		private static FileInfo ResolveStagedFile(string stagingPath)
		{
			var atOfferedPath = new FileInfo(stagingPath);

			if (atOfferedPath.Exists)
			{
				return atOfferedPath;
			}

			string directory = Path.GetDirectoryName(stagingPath);

			if (string.IsNullOrEmpty(directory)
				|| !Directory.Exists(directory))
			{
				return null;
			}

			string[] written = Directory.GetFiles(directory);

			return written.Length == 1 ? new FileInfo(written[0]) : null;
		}

		/// <summary>Hands a finished staged file to the browser as a download and sweeps the staging up.</summary>
		private void DeliverSavedFile(string stagingPath, string downloadName)
		{
			try
			{
				this.interop.DownloadFile(downloadName, File.ReadAllBytes(stagingPath));
			}
			catch (Exception downloadException)
			{
				// Loud, unlike the give-up: the application did write the file, so the user has every reason
				// to expect it, and a save that vanishes silently is the worst version of this bug.
				Console.Error.WriteLine($"BrowserFileDialogProvider could not download '{stagingPath}': {downloadException}");
				UiThread.ReportUnhandledException(downloadException);
			}
			finally
			{
				// The download is a copy the browser now owns, so the staged bytes are dead weight in a heap
				// that has no swap behind it.
				TryDeleteDirectory(Path.GetDirectoryName(stagingPath));
			}
		}

		/// <summary>
		/// Removes a staging directory, and says so rather than throwing if it cannot. Cleanup failing is
		/// never worth taking a save or an open down over.
		/// </summary>
		private static void TryDeleteDirectory(string directory)
		{
			if (string.IsNullOrEmpty(directory))
			{
				return;
			}

			try
			{
				Directory.Delete(directory, recursive: true);
			}
			catch (IOException cleanupException)
			{
				Console.Error.WriteLine($"BrowserFileDialogProvider could not clean up '{directory}': {cleanupException.Message}");
			}
			catch (UnauthorizedAccessException cleanupException)
			{
				Console.Error.WriteLine($"BrowserFileDialogProvider could not clean up '{directory}': {cleanupException.Message}");
			}
		}
	}
}
