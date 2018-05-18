﻿using System;
using System.IO;
using System.Windows.Forms;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform
{
	public class WinformsFileDialogProvider : IFileDialogProvider
	{
		public string LastDirectoryUsed { get; private set; }

		// Resolve not needed on non-Mac platforms
		public string ResolveFilePath(string path) => path;

		public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
		{
			WinformsSystemWindow.ShowingSystemDialog = true;
			openParams.FileName = "";
			openParams.FileNames = null;

			var openFileDialog1 = new OpenFileDialog
			{
				InitialDirectory = openParams.InitialDirectory,
				Filter = openParams.Filter,
				Multiselect = openParams.MultiSelect,
				Title = openParams.Title,
				FilterIndex = openParams.FilterIndex,
				RestoreDirectory = true
			};

			if (openFileDialog1.ShowDialog() == DialogResult.OK)
			{
				this.LastDirectoryUsed = System.IO.Path.GetDirectoryName(openFileDialog1.FileName);

				openParams.FileNames = openFileDialog1.FileNames;
				openParams.FileName = openFileDialog1.FileName;
			}

			WinformsSystemWindow.ShowingSystemDialog = false;

			UiThread.RunOnIdle(()=>
			{
				callback(openParams);
			});
			return true;
		}

		public bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
		{
			SelectFolderDialog(ref folderParams);
			UiThread.RunOnIdle(() =>
			{
				callback(folderParams);
			});
			return true;
		}

		private string SelectFolderDialog(ref SelectFolderDialogParams folderParams)
		{
			WinformsSystemWindow.ShowingSystemDialog = true;

			FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
			folderBrowserDialog.Description = folderParams.Description;
			switch (folderParams.RootFolder)
			{
				case SelectFolderDialogParams.RootFolderTypes.MyComputer:
					folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
					break;

				default:
					throw new NotImplementedException();
			}
			folderBrowserDialog.ShowNewFolderButton = folderParams.ShowNewFolderButton;

			folderBrowserDialog.ShowDialog();

			WinformsSystemWindow.ShowingSystemDialog = false;
			folderParams.FolderPath = folderBrowserDialog.SelectedPath;

			return folderBrowserDialog.SelectedPath;
		}

		// WinformFileDialogProvider.cs
		public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
		{
			WinformsSystemWindow.ShowingSystemDialog = true;

			var saveFileDialog1 = new SaveFileDialog
			{
				InitialDirectory = saveParams.InitialDirectory,
				Filter = saveParams.Filter,
				FilterIndex = saveParams.FilterIndex,
				RestoreDirectory = true,
				AddExtension = true,
				FileName = saveParams.FileName,
				Title = saveParams.Title,
				ShowHelp = false,
				OverwritePrompt = true,
				CheckPathExists = true,
				SupportMultiDottedExtensions = true,
				ValidateNames = false
			};

			if (saveFileDialog1.ShowDialog() == DialogResult.OK)
			{
				saveParams.FileName = saveFileDialog1.FileName;
				this.LastDirectoryUsed = System.IO.Path.GetDirectoryName(saveParams.FileName);
			}
			else
			{
				saveParams.FileName = null;
			}

			WinformsSystemWindow.ShowingSystemDialog = false;

			UiThread.RunOnIdle(() =>
			{
				callback(saveParams);
			});

			return true;
		}

		/// <summary>
		/// Opens a shell window to the requested path
		/// </summary>
		/// <param name="fileToShow">The path to open</param>
		public void ShowFileInFolder(string fileToShow)
		{
			if (AggContext.OperatingSystem == OSType.Windows)
			{
				System.Diagnostics.Process.Start("explorer.exe", $"/select, \"{Path.GetFullPath(fileToShow)}\"");
			}
		}
	}
}
