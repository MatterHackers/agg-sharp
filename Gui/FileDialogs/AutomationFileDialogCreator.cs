using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	internal class AutomationFileDialogCreator : FileDialogCreator
	{
		public override bool OpenFileDialog(OpenFileDialogParams openParams, Action<OpenFileDialogParams> callback)
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

		public override bool SaveFileDialog(SaveFileDialogParams saveParams, Action<SaveFileDialogParams> callback)
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

		public override bool SelectFolderDialog(SelectFolderDialogParams folderParams, Action<SelectFolderDialogParams> callback)
		{
			throw new NotImplementedException();
		}

		public override string ResolveFilePath(string path) => path;
	}
}