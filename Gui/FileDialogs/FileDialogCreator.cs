using System;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The FileDialog provider interface
	/// </summary>
	public abstract class FileDialogCreator
	{
		public abstract bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback);

		public abstract bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback);

		public abstract bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback);

		public abstract string ResolveFilePath(string path);
	}
}