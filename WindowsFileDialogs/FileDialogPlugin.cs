using System;
using System.IO;
using System.Windows.Forms;

using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.WindowsFileDialogs
{
    public class FileDialogPlugin : FileDialogCreator
    {
        public override bool OpenFileDialog(OpenFileDialogParams openParams, OpenFileDialogDelegate callback)
        {
            Stream stream = OpenFileDialog(ref openParams);
            if (stream != null)
            {
                stream.Close();
            }
            UiThread.RunOnIdle((object state) =>
            {
                callback(openParams);
            });
            return true;
        }

        public override Stream OpenFileDialog(ref OpenFileDialogParams openParams)
        {
            WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = true;
            openParams.FileName = "";
            openParams.FileNames = null;
            Stream myStream = null;
            OpenFileDialog openFileDialog1 = new OpenFileDialog();

            openFileDialog1.InitialDirectory = openParams.InitialDirectory;
            openFileDialog1.Filter = openParams.Filter;
            openFileDialog1.Multiselect = openParams.MultiSelect;
            openFileDialog1.Title = openParams.Title;

            openFileDialog1.FilterIndex = openParams.FilterIndex;
            openFileDialog1.RestoreDirectory = true;

            if (openFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    openParams.FileNames = openFileDialog1.FileNames;
                    if ((myStream = openFileDialog1.OpenFile()) != null && !openParams.MultiSelect)
                    {
                        openParams.FileName = openFileDialog1.FileName;
                        WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = false;
                        return myStream;
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Should use StyledMessageBox but can't take dependency against the MatterControl assembly
                    System.Windows.Forms.MessageBox.Show("Error: Could not read file from disk. Original error: " + ex.Message);
                }
            }

            WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = false;
            return null;
        }

        public override bool SelectFolderDialog(SelectFolderDialogParams folderParams, SelectFolderDialogDelegate callback)
        {
            SelectFolderDialog(ref folderParams);
            UiThread.RunOnIdle((object state) =>
            {
                callback(folderParams);
            });
            return true;
        }

        public override string SelectFolderDialog(ref SelectFolderDialogParams folderParams)
        {
            WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = true;

            FolderBrowserDialog folderBrowserDialog = new FolderBrowserDialog();
            folderBrowserDialog.Description = folderParams.Description;
            switch (folderParams.RootFolder)
            {
                case SelectFolderDialogParams.RootFolderTypes.MyComputer:
                    folderBrowserDialog.RootFolder = Environment.SpecialFolder.MyComputer;
                    break;

                default:
                    throw new NotImplementedException();
            }
            folderBrowserDialog.ShowNewFolderButton = folderParams.ShowNewFolderButton;

            folderBrowserDialog.ShowDialog();

            WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = false;
            return folderBrowserDialog.SelectedPath;
        }

        public override bool SaveFileDialog(SaveFileDialogParams saveParams, SaveFileDialogDelegate callback)
        {
            Stream stream = SaveFileDialog(ref saveParams);
            if (stream != null)
            {
                stream.Close();
            }

            UiThread.RunOnIdle((object state) =>
            {
                callback(saveParams);
            });

            return true;
        }

        public override Stream SaveFileDialog(ref SaveFileDialogParams saveParams)
        {
            WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = true;
            SaveFileDialogParams SaveFileDialogDialogParams;
            Stream SaveFileDialogStreamToSaveTo = null;
            SaveFileDialogDialogParams = saveParams;

            Stream myStream = null;
            SaveFileDialog saveFileDialog1 = new SaveFileDialog();

            saveFileDialog1.InitialDirectory = SaveFileDialogDialogParams.InitialDirectory;
            saveFileDialog1.Filter = saveParams.Filter;
            saveFileDialog1.FilterIndex = saveParams.FilterIndex;
            saveFileDialog1.RestoreDirectory = true;
            saveFileDialog1.AddExtension = true;
            saveFileDialog1.FileName = saveParams.FileName;

            saveFileDialog1.Title = saveParams.Title;
            saveFileDialog1.ShowHelp = false;
            saveFileDialog1.OverwritePrompt = true;
            saveFileDialog1.CheckPathExists = true;
            saveFileDialog1.SupportMultiDottedExtensions = true;
            saveFileDialog1.ValidateNames = false;

            if (saveFileDialog1.ShowDialog() == DialogResult.OK)
            {
                try
                {
                    if ((myStream = saveFileDialog1.OpenFile()) != null)
                    {
                        SaveFileDialogDialogParams.FileName = saveFileDialog1.FileName;
                        SaveFileDialogStreamToSaveTo = myStream;
                    }
                }
                catch (Exception ex)
                {
                    // TODO: Should use StyledMessageBox but can't take dependency against the MatterControl assembly
                    System.Windows.Forms.MessageBox.Show("Error: Could not create file for saving. Original error: " + ex.Message);
                }
            }

            WidgetForWindowsFormsAbstract.MainWindowsFormsWindow.ShowingSystemDialog = false;
            return SaveFileDialogStreamToSaveTo;
        }
    }
}
