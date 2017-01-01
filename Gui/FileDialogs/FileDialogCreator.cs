using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public abstract class FileDialogCreator
	{
		public delegate void OpenFileDialogDelegate(OpenFileDialogParams openParams);

		public delegate void SelectFolderDialogDelegate(SelectFolderDialogParams folderParams);

		public delegate void SaveFileDialogDelegate(SaveFileDialogParams saveParams);

		public abstract bool OpenFileDialog(OpenFileDialogParams openParams, OpenFileDialogDelegate callback);

		public abstract bool SelectFolderDialog(SelectFolderDialogParams folderParams, SelectFolderDialogDelegate callback);

		public abstract bool SaveFileDialog(SaveFileDialogParams saveParams, SaveFileDialogDelegate callback);

		public abstract string ResolveFilePath(string path);
	}
}