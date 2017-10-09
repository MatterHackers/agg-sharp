using System;
using System.Diagnostics;
using System.IO;
using Gtk;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform
{
	public class GtkFileDialogProvider : IFileDialogProvider
	{
		public string LastDirectoryUsed { get; private set; }

		// Resolve not needed on non-Mac platforms
		public string ResolveFilePath(string path) => path;

		public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
		{
			WinformsSystemWindow.ShowingSystemDialog = true;
			Gtk.Application.Init();

			Gtk.FileChooserDialog fc =
				new Gtk.FileChooserDialog(openParams.Title,
					null,
					FileChooserAction.Open,
					"Cancel", ResponseType.Cancel,
					"Open", ResponseType.Accept);

			if (openParams.InitialDirectory != null)
			{
				fc.SetCurrentFolder(openParams.InitialDirectory);
			}
			fc.SelectMultiple = openParams.MultiSelect;

			Gtk.FileFilter filter = new Gtk.FileFilter();
			filter.Name = openParams.Filter.Split('|')[0];
			string[] extensions = openParams.Filter.Split ('|') [1].Split (';');
			foreach (string e in extensions) {
				filter.AddPattern(e.ToLower());
				filter.AddPattern(e.ToUpper());
			}
			fc.AddFilter(filter);

			// fc.Show();
			// fc.Present();
			fc.KeepAbove = true;

			if (fc.Run() == (int)ResponseType.Accept)
			{
                this.LastDirectoryUsed = Path.GetDirectoryName(fc.Filename);
				openParams.FileNames = fc.Filenames;
				openParams.FileName = fc.Filename;
				UiThread.RunOnIdle((state) =>
					{
						OpenFileDialogParams openParamsIn = state as OpenFileDialogParams;
						if (openParamsIn != null)
						{
							callback(openParamsIn);
						}
					}, openParams);
			}
				
			fc.Destroy();
			while (Gtk.Application.EventsPending()) {
				Gtk.Main.Iteration();
			}

			WinformsSystemWindow.ShowingSystemDialog = false;

			return true;
		}


		public bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
		{
			WinformsSystemWindow.ShowingSystemDialog = true;
			Gtk.Application.Init();

			Gtk.FileChooserDialog fc =
				new Gtk.FileChooserDialog(folderParams.Description,
					null,
					FileChooserAction.SelectFolder,
					"Cancel", ResponseType.Cancel,
					"Open", ResponseType.Accept);

			fc.KeepAbove = true;

			if (fc.Run() == (int)ResponseType.Accept)
			{
				folderParams.FolderPath = fc.Filename;
				UiThread.RunOnIdle(() =>
				{
					callback(folderParams);
				});
			}

			fc.Destroy();
			while (Gtk.Application.EventsPending()) {
				Gtk.Main.Iteration();
			}

			WinformsSystemWindow.ShowingSystemDialog = false;

			return true;
		}

		public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
		{
			WinformsSystemWindow.ShowingSystemDialog = true;
			Gtk.Application.Init();

			Gtk.FileChooserDialog fc = 
				new Gtk.FileChooserDialog(saveParams.Title,
					null,
					FileChooserAction.Save,
					"Cancel", ResponseType.Cancel,
					"Save", ResponseType.Accept);

			Gtk.FileFilter filter = new Gtk.FileFilter();
			filter.Name = saveParams.Filter.Split('|')[0];
			string[] extensions = saveParams.Filter.Split('|')[1].Split(';');
			foreach (string e in extensions) {
				filter.AddPattern(e.ToLower());
			}
			fc.AddFilter(filter);

			//Set default filename and add file extension
			fc.CurrentName = saveParams.FileName + extensions[0].TrimStart('*');
            if (saveParams.InitialDirectory != null) {
				fc.SetCurrentFolder(saveParams.InitialDirectory);
			}

			fc.KeepAbove = true;

			if (fc.Run() == (int)ResponseType.Accept)
			{
                this.LastDirectoryUsed = Path.GetDirectoryName(fc.Filename);
				saveParams.FileName = fc.Filename;
				UiThread.RunOnIdle(() =>
				{
					callback(saveParams);
				});
			}

			fc.Destroy();
			while (Gtk.Application.EventsPending()) {
				Gtk.Main.Iteration();
			}

			WinformsSystemWindow.ShowingSystemDialog = false;

			return true;
		}

		/// <summary>
		/// Opens a shell window to the requested path
		/// </summary>
		/// <param name="fileToShow">The path to open</param>
		public void ShowFileInFolder(string fileToShow)
		{
            string file = Path.GetFullPath(fileToShow);
            string directory = Path.GetDirectoryName(fileToShow);

            switch (GetDefaultFileManager())
            {
                // File managers that support a selection
                case "org.gnome.Nautilus.desktop":
					Process.Start("nautilus", $"-w -s \"{file}\"");
                    break;
                case "dolphin.desktop":
                    Process.Start("dolphin", $"--select \"{file}\"");
                    break;
                // File managers that don't
                default:
                    // Tunar
                    // Nemo
                    // Caja
                    Process.Start(directory);
                    break;
            }

		}

        string GetDefaultFileManager()
        {
            // Ask XDG for the default application for opening directories
            ProcessStartInfo psi = new ProcessStartInfo();
            psi.FileName = "/usr/bin/xdg-mime";
            psi.UseShellExecute = false;
            psi.Arguments = "query default inode/directory";
            psi.RedirectStandardOutput = true;
            string desktopFile = "";

            try
            {
				Process p = Process.Start(psi);
				desktopFile = p.StandardOutput.ReadToEnd();
				p.WaitForExit();
				p.Close();
            }
            catch (System.ComponentModel.Win32Exception e) {}

            return desktopFile.Trim();
        }
	}
}
