using System;
using System.Linq;
using MatterHackers.Agg.UI;

namespace MatterHackers.Agg.Platform
{
	public class AutomationDialogProvider : IFileDialogProvider
	{
		static readonly char[] multipleFileSeparators = new char[] { ';' };

		public string LastDirectoryUsed { get; private set; }

		public bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
		{
			ShowFileDialog((fileText) =>
			{
				if (fileText.Length > 2)
				{
					string[] files = fileText.Split(multipleFileSeparators).Select(f => f.Trim('\"')).ToArray();
					openParams.FileName = files[0];
					openParams.FileNames = files;
				}
				UiThread.RunOnIdle(() => callback?.Invoke(openParams));
			});

			return true;
		}

		public bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
		{
			ShowFileDialog((fileText) =>
			{
				if (fileText.Length > 2)
				{
					string[] files = fileText.Split(multipleFileSeparators).Select(f => f.Trim('\"')).ToArray();
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
				BackgroundColor = Color.DarkGray
			};

			var warningLabel = new TextWidget("This dialog should not appear outside of automation tests.\nNotify technical support if visible", pointSize: 15, textColor: Color.Pink)
			{
				Margin = new BorderDouble(20),
				VAnchor = VAnchor.Top,
				HAnchor = HAnchor.Stretch
			};
			systemWindow.AddChild(warningLabel);

			var fileNameInput = new TextEditWidget(pixelWidth: 400)
			{
				VAnchor = VAnchor.Center,
				HAnchor = HAnchor.Stretch,
				Margin = new BorderDouble(30, 15),
				Name = "Automation Dialog TextEdit"
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

		public bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
		{
			throw new NotImplementedException();
		}

		public string ResolveFilePath(string path) => path;

		public void ShowFileInFolder(string fileName)
		{
		}
	}
}
