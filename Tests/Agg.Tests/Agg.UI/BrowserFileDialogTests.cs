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
using System.Threading.Tasks;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Platform.Browser;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The browser file dialogs, with the browser taken out. Everything with a right and a wrong answer here
	/// is either pure (the filter translation, the staging names, the save watch) or reaches the page only
	/// through <see cref="IBrowserFileDialogInterop"/>, so all of it runs on a desktop.
	/// </summary>
	// Delivers its answers through UiThread.RunOnIdle, the way every host's file dialog does, so it takes
	// the same key the windowed tests use rather than draining a queue somebody else is filling.
	[NotInParallel(nameof(MatterHackers.GuiAutomation.AutomationRunner.ShowWindowAndExecuteTests))]
	public class BrowserFileDialogTests
	{
		/// <summary>
		/// agg's filter is descriptions paired with globs; a browser's <c>accept</c> is a flat list of
		/// extensions with no descriptions, because its picker has no filter dropdown to put them in. So the
		/// groups flatten, and the order the caller wrote is the order the picker shows.
		/// </summary>
		[Test]
		[Arguments("Meshes|*.stl;*.amf", ".stl,.amf")]
		[Arguments("STL|*.stl|AMF|*.amf", ".stl,.amf")]
		[Arguments("Word Documents|*.doc", ".doc")]
		[Arguments("Mixed Case|*.STL", ".stl")]
		[Arguments("Padded | *.stl ; *.obj ", ".stl,.obj")]
		public async Task AFilterBecomesTheEquivalentAcceptAttribute(string filter, string expected)
		{
			await Assert.That(BrowserFileFilter.ToAcceptAttribute(filter)).IsEqualTo(expected);
		}

		/// <summary>
		/// The case that is easy to get wrong: an all-files group means the user may pick anything, and an
		/// accept listing only the other groups' extensions would stop them. There is no dropdown to switch
		/// filters with, so the only faithful translation is no accept at all.
		/// </summary>
		[Test]
		[Arguments("All Files|*.*")]
		[Arguments("Meshes|*.stl|All Files|*.*")]
		[Arguments("Anything|*")]
		public async Task AFilterThatAllowsEverythingProducesNoAccept(string filter)
		{
			await Assert.That(BrowserFileFilter.ToAcceptAttribute(filter)).IsEqualTo(string.Empty);
		}

		/// <summary>
		/// Nothing to filter by, for four different reasons that all mean the same thing to a picker.
		/// </summary>
		[Test]
		[Arguments((string)null)]
		[Arguments("")]
		[Arguments("Description with no patterns")]
		[Arguments("Globs accept cannot express|backup?.stl;*.st?")]
		public async Task AFilterWithNothingUsableInItProducesNoAccept(string filter)
		{
			await Assert.That(BrowserFileFilter.ToAcceptAttribute(filter)).IsEqualTo(string.Empty);
		}

		/// <summary>
		/// agg's filters overlap constantly - a "Meshes" group and an "STL" group both list <c>*.stl</c> -
		/// and a repeated accept entry is noise at best.
		/// </summary>
		[Test]
		public async Task RepeatedExtensionsAppearOnceInTheAcceptList()
		{
			await Assert.That(BrowserFileFilter.ToAcceptAttribute("Meshes|*.stl;*.obj|STL|*.stl"))
				.IsEqualTo(".stl,.obj");
		}

		/// <summary>
		/// A picker reports a bare name, except when it does not - a directory pick reports a relative path,
		/// and nothing stops a name holding a separator on the machine it came from. Only the last segment
		/// is kept, which is what keeps a staged file inside its own directory.
		/// </summary>
		[Test]
		[Arguments("part.stl", "part.stl")]
		[Arguments("folder/part.stl", "part.stl")]
		[Arguments("..\\..\\etc\\passwd", "passwd")]
		[Arguments("..", "file")]
		[Arguments("   ", "file")]
		[Arguments((string)null, "file")]
		public async Task APickedNameIsReducedToOneSafeSegment(string reported, string expected)
		{
			await Assert.That(BrowserFileStaging.SanitizeFileName(reported)).IsEqualTo(expected);
		}

		/// <summary>
		/// A multi-select of two files that are both called part.stl has to stage two files. The suffix goes
		/// before the extension because callers switch on the extension.
		/// </summary>
		[Test]
		public async Task FilesWithTheSameNameAreStagedSideBySide()
		{
			var used = new List<string>();

			string first = BrowserFileStaging.UniqueFileName(used, "part.stl");
			used.Add(first);

			string second = BrowserFileStaging.UniqueFileName(used, "part.stl");
			used.Add(second);

			string third = BrowserFileStaging.UniqueFileName(used, "part.stl");

			await Assert.That(first).IsEqualTo("part.stl");
			await Assert.That(second).IsEqualTo("part (2).stl");
			await Assert.That(third).IsEqualTo("part (3).stl");
		}

		/// <summary>
		/// The save watch calls a file finished once it has stopped growing across two consecutive polls -
		/// not one, which any pause between two buffer flushes would satisfy.
		/// </summary>
		[Test]
		public async Task ASaveIsDownloadedOnceTheFileHasStoppedGrowing()
		{
			var watch = new BrowserSaveWatch();

			// Not written yet.
			await Assert.That(watch.Observe(exists: false, length: 0, elapsedSeconds: 0))
				.IsEqualTo(SaveWatchDecision.KeepWaiting);

			// First sighting is never stable - the writer has only just started.
			await Assert.That(watch.Observe(exists: true, length: 1024, elapsedSeconds: 1))
				.IsEqualTo(SaveWatchDecision.KeepWaiting);

			// Still growing.
			await Assert.That(watch.Observe(exists: true, length: 4096, elapsedSeconds: 2))
				.IsEqualTo(SaveWatchDecision.KeepWaiting);

			// One unchanged poll is not enough.
			await Assert.That(watch.Observe(exists: true, length: 4096, elapsedSeconds: 3))
				.IsEqualTo(SaveWatchDecision.KeepWaiting);

			await Assert.That(watch.Observe(exists: true, length: 4096, elapsedSeconds: 4))
				.IsEqualTo(SaveWatchDecision.Download);
		}

		/// <summary>
		/// A file that is created and never written to is still a finished file - an empty export is a legal
		/// one - so a zero length has to settle exactly like any other.
		/// </summary>
		[Test]
		public async Task AnEmptyFileSettlesLikeAnyOther()
		{
			var watch = new BrowserSaveWatch();

			await Assert.That(watch.Observe(exists: true, length: 0, elapsedSeconds: 0))
				.IsEqualTo(SaveWatchDecision.KeepWaiting);
			await Assert.That(watch.Observe(exists: true, length: 0, elapsedSeconds: 1))
				.IsEqualTo(SaveWatchDecision.KeepWaiting);
			await Assert.That(watch.Observe(exists: true, length: 0, elapsedSeconds: 2))
				.IsEqualTo(SaveWatchDecision.Download);
		}

		/// <summary>
		/// The give-up path: an application is entitled to ask for a save path and then never write anything
		/// (a confirmation the user backed out of), and the watch cannot poll forever waiting for it.
		/// </summary>
		[Test]
		public async Task AFileThatIsNeverWrittenIsEventuallyGivenUpOn()
		{
			var watch = new BrowserSaveWatch();

			await Assert.That(watch.Observe(exists: false, length: 0, elapsedSeconds: BrowserSaveWatch.GiveUpSeconds - 1))
				.IsEqualTo(SaveWatchDecision.KeepWaiting);
			await Assert.That(watch.Observe(exists: false, length: 0, elapsedSeconds: BrowserSaveWatch.GiveUpSeconds))
				.IsEqualTo(SaveWatchDecision.GiveUp);
		}

		/// <summary>
		/// A file that settled on the very poll that ran out of time is still a finished file, and throwing
		/// it away would be perverse. Download is checked before the give-up for exactly this.
		/// </summary>
		[Test]
		public async Task AFileThatSettlesOnTheLastPollIsStillDownloaded()
		{
			var watch = new BrowserSaveWatch();

			watch.Observe(exists: true, length: 8, elapsedSeconds: BrowserSaveWatch.GiveUpSeconds - 2);
			watch.Observe(exists: true, length: 8, elapsedSeconds: BrowserSaveWatch.GiveUpSeconds - 1);

			await Assert.That(watch.Observe(exists: true, length: 8, elapsedSeconds: BrowserSaveWatch.GiveUpSeconds))
				.IsEqualTo(SaveWatchDecision.Download);
		}

		/// <summary>
		/// A caller normally passes a full desktop path as its suggestion. Staging at that path would write
		/// into a directory the virtual file system does not have, and the throw would land in the
		/// application's own save code rather than here.
		/// </summary>
		[Test]
		[Arguments("/Users/someone/Documents/part.stl", "Meshes|*.stl", "part.stl")]
		[Arguments("C:\\Users\\someone\\part.amf", "Meshes|*.amf", "part.amf")]
		[Arguments("", "Meshes|*.stl;*.amf", "download.stl")]
		[Arguments("", "All Files|*.*", "download")]
		[Arguments((string)null, (string)null, "download")]
		public async Task ASaveDownloadsUnderTheCallersSuggestedNameOrTheFiltersExtension(
			string suggested, string filter, string expected)
		{
			var saveParams = new SaveFileDialogParams(filter, initialDirectory: "not used")
			{
				FileName = suggested,
			};

			await Assert.That(BrowserFileDialogProvider.SuggestedSaveName(saveParams)).IsEqualTo(expected);
		}

		/// <summary>
		/// The whole open path: the accept the picker was given, the bytes landing in the virtual file system
		/// under paths agg can open, and the answer arriving through the idle queue after the call returned -
		/// the contract this shares with <c>LinuxFileDialogProvider</c> and not with mac or Windows.
		/// </summary>
		[Test]
		[Timeout(30_000)]
		public async Task OpeningFilesStagesTheirBytesAndAnswersThroughTheIdleQueue()
		{
			var picker = new RecordingFileDialogInterop();
			var provider = new BrowserFileDialogProvider(picker);

			var openParams = new OpenFileDialogParams("Meshes|*.stl;*.amf", multiSelect: true);

			OpenFileDialogParams answered = null;

			try
			{
				bool shown = provider.OpenFileDialog(openParams, chosen => answered = chosen);

				await Assert.That(shown).IsTrue();
				await Assert.That(picker.Accept).IsEqualTo(".stl,.amf");
				await Assert.That(picker.Multiple).IsTrue();

				picker.RaiseFile("part.stl", new byte[] { 1, 2, 3 });
				picker.RaiseFile("part.stl", new byte[] { 4, 5 });
				picker.RaiseComplete();

				// Not yet: the answer goes through the idle queue, exactly as it does on Linux, so no caller
				// is re-entered from inside its own OpenFileDialog call.
				await Assert.That(answered).IsNull();

				UiThread.InvokePendingActions();

				await Assert.That(answered).IsNotNull();
				await Assert.That(answered.FileNames.Length).IsEqualTo(2);
				await Assert.That(answered.FileName).IsEqualTo(answered.FileNames[0]);

				// Real files, at the paths handed back - which is the entire point of staging.
				await Assert.That(File.ReadAllBytes(answered.FileNames[0])).IsEquivalentTo(new byte[] { 1, 2, 3 });
				await Assert.That(File.ReadAllBytes(answered.FileNames[1])).IsEquivalentTo(new byte[] { 4, 5 });

				// The second file kept its own bytes rather than overwriting the first's.
				await Assert.That(Path.GetFileName(answered.FileNames[1])).IsEqualTo("part (2).stl");
			}
			finally
			{
				CleanUp(answered);
				UiThread.ResetForTests();
			}
		}

		/// <summary>
		/// Cancel is silent, matching all three desktop providers - and the params were cleared on the way in
		/// so a caller that reuses one cannot read last time's answer as this time's. That matters more here
		/// than anywhere: some browsers raise no cancel event at all, in which case the silence is all there is.
		/// </summary>
		[Test]
		[Timeout(30_000)]
		public async Task ACancelledOpenSaysNothingAndLeavesNothingBehind()
		{
			var picker = new RecordingFileDialogInterop();
			var provider = new BrowserFileDialogProvider(picker);

			var openParams = new OpenFileDialogParams("Meshes|*.stl")
			{
				FileName = "last time's answer",
				FileNames = new[] { "last time's answer" },
			};

			bool answered = false;

			// Snapshotted rather than asked of the provider, which does not publish the directory it made -
			// a cancelled dialog hands back no path to read it from, which is the whole point of this test.
			var stagingBefore = new HashSet<string>(StagingDirectories());

			try
			{
				provider.OpenFileDialog(openParams, _ => answered = true);

				await Assert.That(openParams.FileName).IsEqualTo(string.Empty);
				await Assert.That(openParams.FileNames).IsNull();

				picker.RaiseComplete();
				UiThread.InvokePendingActions();

				await Assert.That(answered).IsFalse();

				// And the staging directory it made on the way in is gone rather than left in the heap.
				await Assert.That(new HashSet<string>(StagingDirectories()).SetEquals(stagingBefore)).IsTrue();
			}
			finally
			{
				UiThread.ResetForTests();
			}
		}

		/// <summary>
		/// Save hands back a staging path the caller can write to, and does it through the idle queue like
		/// every other answer this provider gives. The download itself is the watch's business.
		/// </summary>
		[Test]
		[Timeout(30_000)]
		public async Task SavingHandsBackAWritablePathForTheApplicationToFill()
		{
			var picker = new RecordingFileDialogInterop();
			var provider = new BrowserFileDialogProvider(picker);

			var saveParams = new SaveFileDialogParams("Meshes|*.stl", initialDirectory: "not used")
			{
				FileName = "/somewhere/on/a/desktop/part.stl",
			};

			SaveFileDialogParams answered = null;

			try
			{
				bool shown = provider.SaveFileDialog(saveParams, chosen => answered = chosen);

				await Assert.That(shown).IsTrue();

				UiThread.InvokePendingActions();

				await Assert.That(answered).IsNotNull();
				await Assert.That(Path.GetFileName(answered.FileName)).IsEqualTo("part.stl");

				// The directory exists already, so an application's File.WriteAllBytes to this path works
				// without it having to create anything.
				await Assert.That(Directory.Exists(Path.GetDirectoryName(answered.FileName))).IsTrue();
			}
			finally
			{
				if (answered?.FileName != null)
				{
					Directory.Delete(Path.GetDirectoryName(answered.FileName), recursive: true);
				}

				UiThread.ResetForTests();
			}
		}

		/// <summary>
		/// The two the browser simply does not have. A folder picker would need the File System Access API,
		/// which yields a handle rather than a path; there is no file manager to reveal anything in.
		/// </summary>
		[Test]
		public async Task ThereIsNoFolderPickerAndNoFileManager()
		{
			var provider = new BrowserFileDialogProvider(new RecordingFileDialogInterop());

			await Assert.That(provider.SelectFolderDialog(new SelectFolderDialogParams("pick one"), _ => { }))
				.IsFalse();

			// Nothing to assert but that it is harmless; a caller reveals a file after every save.
			provider.ShowFileInFolder("/anything");
		}

		/// <summary>
		/// Constructed outside a browser - which is what a mis-set provider string does - it shows nothing and
		/// says so, rather than throwing out of a menu click. Same answer <c>LinuxFileDialogProvider</c> gives
		/// when neither helper is installed.
		/// </summary>
		[Test]
		public async Task WithNoPageToShowADialogInItAnswersFalse()
		{
			var provider = new BrowserFileDialogProvider(interop: null);

			await Assert.That(provider.OpenFileDialog(new OpenFileDialogParams("Meshes|*.stl"), _ => { })).IsFalse();
			await Assert.That(provider.SaveFileDialog(
				new SaveFileDialogParams("Meshes|*.stl", initialDirectory: "not used"), _ => { })).IsFalse();
		}

		/// <summary>Every dialog staging directory that exists right now.</summary>
		private static string[] StagingDirectories()
			=> Directory.Exists(BrowserFileStaging.StagingRoot)
				? Directory.GetDirectories(BrowserFileStaging.StagingRoot)
				: Array.Empty<string>();

		/// <summary>Removes what an open test staged.</summary>
		private static void CleanUp(OpenFileDialogParams answered)
		{
			if (answered?.FileName == null)
			{
				return;
			}

			string directory = Path.GetDirectoryName(answered.FileName);

			if (Directory.Exists(directory))
			{
				Directory.Delete(directory, recursive: true);
			}
		}

		/// <summary>The file picker and the download anchor, replaced by a recorder the test drives.</summary>
		private sealed class RecordingFileDialogInterop : IBrowserFileDialogInterop
		{
			private Action<BrowserPickedFile> onFile;

			private Action onComplete;

			/// <summary>The accept attribute the picker was given.</summary>
			public string Accept { get; private set; }

			public bool Multiple { get; private set; }

			/// <summary>The name the last download was offered under, or null if there was none.</summary>
			public string DownloadedAs { get; private set; }

			public byte[] DownloadedBytes { get; private set; }

			public void PickFiles(string accept, bool multiple, Action<BrowserPickedFile> onFile, Action onComplete)
			{
				this.Accept = accept;
				this.Multiple = multiple;
				this.onFile = onFile;
				this.onComplete = onComplete;
			}

			public void DownloadFile(string fileName, byte[] bytes)
			{
				this.DownloadedAs = fileName;
				this.DownloadedBytes = bytes;
			}

			/// <summary>Delivers one chosen file, the way the change listener's read loop would.</summary>
			public void RaiseFile(string name, byte[] bytes) => this.onFile(new BrowserPickedFile(name, bytes));

			/// <summary>Ends the pick - after the last file, or on a cancel with no files at all.</summary>
			public void RaiseComplete() => this.onComplete();
		}
	}
}
