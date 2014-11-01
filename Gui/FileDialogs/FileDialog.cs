using System;
using System.IO;

namespace MatterHackers.Agg.UI
{
    public abstract class FileDialogCreator
    {
		public delegate void OpenFileDialogDelegate( OpenFileDialogParams openParams );
		public delegate void SelectFolderDialogDelegate( SelectFolderDialogParams folderParams );
		public delegate void SaveFileDialogDelegate( SaveFileDialogParams saveParams );

		public abstract Stream OpenFileDialog(ref OpenFileDialogParams openParams);
		public abstract bool OpenFileDialog(OpenFileDialogParams openParams, OpenFileDialogDelegate callback);

        public abstract string SelectFolderDialog(ref SelectFolderDialogParams folderParams);
		public abstract bool SelectFolderDialog(SelectFolderDialogParams folderParams, SelectFolderDialogDelegate callback);

        public abstract Stream SaveFileDialog(ref SaveFileDialogParams saveParams);
		public abstract bool SaveFileDialog(SaveFileDialogParams saveParams, SaveFileDialogDelegate callback);
    }

    public static class FileDialog
    {
        static FileDialogCreator fileDialogCreatorPlugin = null;
        static FileDialogCreator FileDialogCreatorPlugin
        {
            get
            {
                if (fileDialogCreatorPlugin == null)
                {
                    string pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
                    PluginFinder<FileDialogCreator> fileDialogCreatorPlugins = new PluginFinder<FileDialogCreator>(pluginPath);
                    if (fileDialogCreatorPlugins.Plugins.Count != 1)
                    {
                        throw new Exception(string.Format("Did not find any FileDialogCreators in Plugin path ({0}.", pluginPath));
                    }

                    fileDialogCreatorPlugin = fileDialogCreatorPlugins.Plugins[0];
                }

                return fileDialogCreatorPlugin;
            }
        }



        public static Stream OpenFileDialog(ref OpenFileDialogParams openParams)
        {
            return FileDialogCreatorPlugin.OpenFileDialog(ref openParams);
        }

        public static string SelectFolderDialog(ref SelectFolderDialogParams folderParams)
        {
            return FileDialogCreatorPlugin.SelectFolderDialog(ref folderParams);
        }

        public static Stream SaveFileDialog(ref SaveFileDialogParams saveParams)
        {
            return FileDialogCreatorPlugin.SaveFileDialog(ref saveParams);
        }

		public static bool OpenFileDialog(OpenFileDialogParams openParams, FileDialogCreator.OpenFileDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.OpenFileDialog(openParams, callback);
		}

		public static bool SelectFolderDialog(SelectFolderDialogParams folderParams, FileDialogCreator.SelectFolderDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.SelectFolderDialog(folderParams, callback);
		}

		public static bool SaveFileDialog(SaveFileDialogParams saveParams, FileDialogCreator.SaveFileDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.SaveFileDialog(saveParams, callback);
		}
    }
}
