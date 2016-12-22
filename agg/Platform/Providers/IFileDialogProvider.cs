using System;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The FileDialog provider interface
	/// </summary>
	public interface IFileDialogProvider
	{
		bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback);

		bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback);

		bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback);

		string ResolveFilePath(string path);
	}
}