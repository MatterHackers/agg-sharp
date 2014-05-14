using System;
using System.IO;

namespace MatterHackers.Agg.UI
{
    public abstract class FileDialogCreator
    {
        public abstract Stream OpenFileDialog(ref OpenFileDialogParams openParams);
        public abstract string SelectFolderDialog(ref SelectFolderDialogParams folderParams);
        public abstract Stream SaveFileDialog(ref SaveFileDialogParams saveParams);
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
    }
}
