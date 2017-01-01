using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public static class FileDialog
	{
		private static string lastDirectoryUsed = "";

		private static FileDialogCreator fileDialogCreatorPlugin = null;

		private static FileDialogCreator FileDialogCreatorPlugin
		{
			get
			{
				if (fileDialogCreatorPlugin == null)
				{
					string pluginPath = Path.GetDirectoryName(System.Reflection.Assembly.GetExecutingAssembly().Location);
					PluginFinder<FileDialogCreator> fileDialogCreatorPlugins = new PluginFinder<FileDialogCreator>(pluginPath);

					if (fileDialogCreatorPlugins.Plugins.Count == 0)
					{
						fileDialogCreatorPlugin = new AutomationFileDialogCreator();
					}
					else
					{
						fileDialogCreatorPlugin = fileDialogCreatorPlugins.Plugins[0];
					}
				}

				return fileDialogCreatorPlugin;
			}
		}

		public static IEnumerable<string> ResolveFilePaths(IEnumerable<string> filePaths)
		{
			// Only perform Mac file reference resolution when the string starts with the expected token
			return filePaths.Select(path => !path.StartsWith("/.file") ? path : FileDialogCreatorPlugin.ResolveFilePath(path));
		}

		public static bool OpenFileDialog(OpenFileDialogParams openParams, FileDialogCreator.OpenFileDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.OpenFileDialog(openParams, (OpenFileDialogParams outputOpenParams) =>
				{
					try
					{
						if (outputOpenParams.FileName != "")
						{
							string directory = Path.GetDirectoryName(outputOpenParams.FileName);
							if (directory != null && directory != "")
							{
								lastDirectoryUsed = directory;
							}
						}
					}
					catch (Exception e)
					{
						Debug.Print(e.Message);
						GuiWidget.BreakInDebugger();
					}
					callback(outputOpenParams);
				}
			);
		}

		public static bool SelectFolderDialog(SelectFolderDialogParams folderParams, FileDialogCreator.SelectFolderDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.SelectFolderDialog(folderParams, callback);
		}

		public static bool SaveFileDialog(SaveFileDialogParams saveParams, FileDialogCreator.SaveFileDialogDelegate callback)
		{
			return FileDialogCreatorPlugin.SaveFileDialog(saveParams, (SaveFileDialogParams outputSaveParams) =>
				{
					try
					{
						if (outputSaveParams.FileName != "")
						{
							string directory = Path.GetDirectoryName(outputSaveParams.FileName);
							if (directory != null && directory != "")
							{
								lastDirectoryUsed = directory;
							}
						}
					}
					catch (Exception e)
					{
						Debug.Print(e.Message);
						GuiWidget.BreakInDebugger();
					}
					callback(outputSaveParams);
				}
			);
		}

		public static string LastDirectoryUsed
		{
			get
			{
				if (lastDirectoryUsed == null
					|| lastDirectoryUsed == ""
					|| !Directory.Exists(lastDirectoryUsed))
				{
					return System.Environment.GetFolderPath(System.Environment.SpecialFolder.Personal);
				}

				return lastDirectoryUsed;
			}

			set
			{
				lastDirectoryUsed = value;
			}
		}
	}
}