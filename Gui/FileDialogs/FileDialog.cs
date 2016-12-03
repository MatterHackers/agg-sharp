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

	internal class AggFileDialogCreator : FileDialogCreator
	{
		internal AggFileDialogCreator()
		{
		}

		public override bool OpenFileDialog(OpenFileDialogParams openParams, OpenFileDialogDelegate callback)
		{
			ShowFileDialog((fileText) =>
			{
				if (fileText.Length > 2)
				{
					string[] files = fileText.Split(';', ' ').Select(f => f.Trim('\"')).ToArray();
					openParams.FileName = files[0];
					openParams.FileNames = files;
				}
				UiThread.RunOnIdle(() => callback?.Invoke(openParams));
			});

			return true;
		}

		public override bool SaveFileDialog(SaveFileDialogParams saveParams, SaveFileDialogDelegate callback)
		{
			ShowFileDialog((fileText) =>
			{
				if (fileText.Length > 2)
				{
					string[] files = fileText.Split(';', ' ').Select(f => f.Trim('\"')).ToArray();
					saveParams.FileName = files[0];
					saveParams.FileNames = files;
				}
				UiThread.RunOnIdle(() => callback?.Invoke(saveParams));
			});

			return true;
		}

		private static void ShowFileDialog(Action<string> dialogClosedHandler)
		{
			var systemWindow = new SystemWindow(600, 200)
			{
				Title = "TestAutomation File Input",
				BackgroundColor = RGBA_Bytes.DarkGray
			};

			var warningLabel = new TextWidget("This dialog should not appear outside of automation tests.\nNotify technical support if visible", pointSize: 15, textColor: RGBA_Bytes.Pink)
			{
				Margin = new BorderDouble(20),
				VAnchor = VAnchor.ParentTop,
				HAnchor = HAnchor.ParentLeftRight
			};
			systemWindow.AddChild(warningLabel);

			var fileNameInput = new TextEditWidget(pixelWidth: 400)
			{
				VAnchor = VAnchor.ParentCenter,
				HAnchor = HAnchor.ParentLeftRight,
				Margin = new BorderDouble(30, 15)
			};
			fileNameInput.EnterPressed += (s, e) => systemWindow.CloseOnIdle();
			systemWindow.AddChild(fileNameInput);

			systemWindow.Load += (s, e) => fileNameInput.Focus();
			systemWindow.Closed += (s, e) =>
			{
				dialogClosedHandler(fileNameInput.Text);
			};

			systemWindow.ShowAsSystemWindow();
		}

		public override bool SelectFolderDialog(SelectFolderDialogParams folderParams, SelectFolderDialogDelegate callback)
		{
			throw new NotImplementedException();
		}

		public override string ResolveFilePath(string path) => path;
	}

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
						fileDialogCreatorPlugin = new AggFileDialogCreator();
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
			// Only perform Mac file reference resoltion when the string starts with the expected token
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