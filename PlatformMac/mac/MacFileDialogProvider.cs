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

using static MatterHackers.Agg.Platform.Mac.ObjC;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// File dialogs on macOS, over <c>NSOpenPanel</c> and <c>NSSavePanel</c>.
	/// <para>
	/// <c>runModal</c> spins AppKit's own nested modal loop, so the caller blocks here exactly the way it
	/// blocks in <c>ShowDialog</c> on Windows. That loop still services the run loop, which is why
	/// <c>MacSystemWindow</c>'s idle pump is an <c>NSTimer</c> rather than a tick in its own event loop -
	/// RunOnIdle keeps running while a file dialog is up.
	/// </para>
	/// <para>
	/// Each dialog runs through <see cref="MainThreadDispatcher"/> because a panel is AppKit and
	/// <c>runModal</c> in particular has to be the main thread's own loop. In an application that is the
	/// thread the caller is already on and costs nothing; under a test runner it is not.
	/// </para>
	/// <para>
	/// <b>Not implemented:</b> the <c>Filter</c> string is ignored. Translating "Word Documents|*.doc"
	/// into the <c>UTType</c> list modern AppKit wants is a job in itself, and every caller in this repo
	/// treats the filter as a convenience rather than a guarantee.
	/// </para>
	/// </summary>
	public class MacFileDialogProvider : IFileDialogProvider
	{
		/// <summary>NSModalResponseOK.</summary>
		private const long NSModalResponseOK = 1;

		public string LastDirectoryUsed { get; private set; }

		/// <summary>macOS paths need no translation; this exists for platforms whose paths do.</summary>
		public string ResolveFilePath(string path) => path;

		public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
			=> MainThreadDispatcher.Invoke(() => this.OpenFileDialogOnMainThread(openParams, callback));

		private bool OpenFileDialogOnMainThread(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
		{
			openParams.FileName = string.Empty;
			openParams.FileNames = null;

			IntPtr panel = Send_r(Class("NSOpenPanel"), Sel("openPanel"));
			Send_v_B(panel, Sel("setCanChooseFiles:"), YES);
			Send_v_B(panel, Sel("setCanChooseDirectories:"), NO);
			Send_v_B(panel, Sel("setAllowsMultipleSelection:"), openParams.MultiSelect ? YES : NO);

			ApplyCommonOptions(panel, openParams.Title, openParams.ActionButtonLabel, openParams.InitialDirectory);

			if (Send_q(panel, Sel("runModal")) != NSModalResponseOK)
			{
				return false;
			}

			var paths = ReadPaths(Send_r(panel, Sel("URLs")));
			if (paths.Count == 0)
			{
				return false;
			}

			openParams.FileNames = paths.ToArray();
			openParams.FileName = paths[0];
			this.LastDirectoryUsed = Path.GetDirectoryName(paths[0]);

			callback?.Invoke(openParams);
			return true;
		}

		public bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
			=> MainThreadDispatcher.Invoke(() => this.SelectFolderDialogOnMainThread(folderParams, callback));

		private bool SelectFolderDialogOnMainThread(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
		{
			IntPtr panel = Send_r(Class("NSOpenPanel"), Sel("openPanel"));
			Send_v_B(panel, Sel("setCanChooseFiles:"), NO);
			Send_v_B(panel, Sel("setCanChooseDirectories:"), YES);
			Send_v_B(panel, Sel("setAllowsMultipleSelection:"), NO);
			Send_v_B(panel, Sel("setCanCreateDirectories:"), folderParams.ShowNewFolderButton ? YES : NO);

			ApplyCommonOptions(panel, folderParams.Title, folderParams.ActionButtonLabel, folderParams.FolderPath);

			if (!string.IsNullOrEmpty(folderParams.Description))
			{
				Send_v_r(panel, Sel("setMessage:"), NSString(folderParams.Description));
			}

			if (Send_q(panel, Sel("runModal")) != NSModalResponseOK)
			{
				return false;
			}

			var paths = ReadPaths(Send_r(panel, Sel("URLs")));
			if (paths.Count == 0)
			{
				return false;
			}

			folderParams.FolderPath = paths[0];
			this.LastDirectoryUsed = paths[0];

			callback?.Invoke(folderParams);
			return true;
		}

		public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
			=> MainThreadDispatcher.Invoke(() => this.SaveFileDialogOnMainThread(saveParams, callback));

		private bool SaveFileDialogOnMainThread(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
		{
			IntPtr panel = Send_r(Class("NSSavePanel"), Sel("savePanel"));

			ApplyCommonOptions(panel, saveParams.Title, saveParams.ActionButtonLabel, saveParams.InitialDirectory);

			if (!string.IsNullOrEmpty(saveParams.FileName))
			{
				Send_v_r(panel, Sel("setNameFieldStringValue:"), NSString(Path.GetFileName(saveParams.FileName)));
			}

			if (Send_q(panel, Sel("runModal")) != NSModalResponseOK)
			{
				return false;
			}

			string path = PathOfUrl(Send_r(panel, Sel("URL")));
			if (string.IsNullOrEmpty(path))
			{
				return false;
			}

			saveParams.FileName = path;
			saveParams.FileNames = new[] { path };
			this.LastDirectoryUsed = Path.GetDirectoryName(path);

			callback?.Invoke(saveParams);
			return true;
		}

		/// <summary>Reveals a file in Finder, selected, the way "Show in Explorer" does on Windows.</summary>
		public void ShowFileInFolder(string fileName)
		{
			if (string.IsNullOrEmpty(fileName))
			{
				return;
			}

			MainThreadDispatcher.Invoke(() =>
			{
				IntPtr workspace = Send_r(Class("NSWorkspace"), Sel("sharedWorkspace"));
				Send_B_r_r(workspace, Sel("selectFile:inFileViewerRootedAtPath:"), NSString(fileName), NSString(string.Empty));
			});
		}

		private static void ApplyCommonOptions(IntPtr panel, string title, string actionButtonLabel, string initialDirectory)
		{
			if (!string.IsNullOrEmpty(title))
			{
				Send_v_r(panel, Sel("setTitle:"), NSString(title));
			}

			if (!string.IsNullOrEmpty(actionButtonLabel))
			{
				Send_v_r(panel, Sel("setPrompt:"), NSString(actionButtonLabel));
			}

			if (!string.IsNullOrEmpty(initialDirectory) && Directory.Exists(initialDirectory))
			{
				IntPtr url = Send_r_r(Class("NSURL"), Sel("fileURLWithPath:"), NSString(initialDirectory));
				Send_v_r(panel, Sel("setDirectoryURL:"), url);
			}
		}

		private static List<string> ReadPaths(IntPtr urlArray)
		{
			var paths = new List<string>();
			if (urlArray == IntPtr.Zero)
			{
				return paths;
			}

			ulong count = Send_Q(urlArray, Sel("count"));
			for (ulong i = 0; i < count; i++)
			{
				string path = PathOfUrl(Send_r_Q(urlArray, Sel("objectAtIndex:"), i));
				if (!string.IsNullOrEmpty(path))
				{
					paths.Add(path);
				}
			}

			return paths;
		}

		private static string PathOfUrl(IntPtr url)
			=> url == IntPtr.Zero ? null : FromNSString(Send_r(url, Sel("path")));
	}
}
