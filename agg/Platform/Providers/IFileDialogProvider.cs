using System;

namespace MatterHackers.Agg.Platform
{
	/// <summary>
	/// The FileDialog provider interface
	/// </summary>
	public interface IFileDialogProvider
	{
		string LastDirectoryUsed { get; }

		string ResolveFilePath(string path);

		bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback);

		bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback);

		bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback);
	}
}