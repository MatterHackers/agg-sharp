namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The FileDialog provider interface
	/// </summary>
	public abstract class FileDialogCreator
	{
		public abstract bool OpenFileDialog(OpenFileDialogParams openParams, OpenFileDialogDelegate callback);

		public abstract bool SelectFolderDialog(SelectFolderDialogParams folderParams, SelectFolderDialogDelegate callback);

		public abstract bool SaveFileDialog(SaveFileDialogParams saveParams, SaveFileDialogDelegate callback);

		public abstract string ResolveFilePath(string path);

		public delegate void OpenFileDialogDelegate(OpenFileDialogParams openParams);

		public delegate void SelectFolderDialogDelegate(SelectFolderDialogParams folderParams);

		public delegate void SaveFileDialogDelegate(SaveFileDialogParams saveParams);

	}
}