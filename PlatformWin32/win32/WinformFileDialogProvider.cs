using System;
using System.Windows.Forms;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform
{
	public class WinformsFileDialogProvider : IFileDialogProvider
	{
		// Resolve not needed on non-Mac platforms
		public string ResolveFilePath(string path) => path;

		public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
		{
			WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = true;
			openParams.FileName = "";
			openParams.FileNames = null;

			OpenFileDialog openFileDialog1 = new OpenFileDialog();

			openFileDialog1.InitialDirectory = openParams.InitialDirectory;
			openFileDialog1.Filter = openParams.Filter;
			openFileDialog1.Multiselect = openParams.MultiSelect;
			openFileDialog1.Title = openParams.Title;

			openFileDialog1.FilterIndex = openParams.FilterIndex;
			openFileDialog1.RestoreDirectory = true;

			if (openFileDialog1.ShowDialog() == DialogResult.OK)
			{
				openParams.FileNames = openFileDialog1.FileNames;
				openParams.FileName = openFileDialog1.FileName;
			}

			WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = false;

			UiThread.RunOnIdle((state) =>
			{
				OpenFileDialogParams openParamsIn = state as OpenFileDialogParams;
				if (openParamsIn != null)
				{
					callback(openParamsIn);
				}
			}, openParams);
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
			WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = true;

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

			WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = false;
			folderParams.FolderPath = folderBrowserDialog.SelectedPath;

			return folderBrowserDialog.SelectedPath;
		}

		public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
		{
			WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = true;
			SaveFileDialogParams SaveFileDialogDialogParams = saveParams;

			SaveFileDialog saveFileDialog1 = new SaveFileDialog();

			saveFileDialog1.InitialDirectory = SaveFileDialogDialogParams.InitialDirectory;
			saveFileDialog1.Filter = saveParams.Filter;
			saveFileDialog1.FilterIndex = saveParams.FilterIndex;
			saveFileDialog1.RestoreDirectory = true;
			saveFileDialog1.AddExtension = true;
			saveFileDialog1.FileName = saveParams.FileName;

			saveFileDialog1.Title = saveParams.Title;
			saveFileDialog1.ShowHelp = false;
			saveFileDialog1.OverwritePrompt = true;
			saveFileDialog1.CheckPathExists = true;
			saveFileDialog1.SupportMultiDottedExtensions = true;
			saveFileDialog1.ValidateNames = false;

			if (saveFileDialog1.ShowDialog() == DialogResult.OK)
			{
				SaveFileDialogDialogParams.FileName = saveFileDialog1.FileName;
			}
			else
			{
				SaveFileDialogDialogParams.FileName = null;
			}

			WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = false;

			UiThread.RunOnIdle(() =>
			{
				callback(saveParams);
			});

			return true;
		}
	}
}