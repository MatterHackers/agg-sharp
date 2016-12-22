using System;

namespace MatterHackers.Agg.Platform
{
	public class SelectFolderDialogParams
	{
		public enum RootFolderTypes { MyComputer };

		public string Description { get; set; }

		public RootFolderTypes RootFolder { get; set; }

		public string FolderPath { get; set; }

		public bool ShowNewFolderButton { get; set; }

		/// <summary>
		/// The title of the dialog window. If not set will show 'Open' or 'Save' as appropriate
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// This does not show on Windows (but does on mac.
		/// </summary>
		public string ActionButtonLabel { get; set; }

		public SelectFolderDialogParams(string description, RootFolderTypes rootFolder = RootFolderTypes.MyComputer, bool showNewFolderButton = true, string title = "", string actionButtonLabel = "")
		{
			this.Description = description;
			this.RootFolder = rootFolder;
			this.ShowNewFolderButton = showNewFolderButton;
			this.Title = title;
			this.ActionButtonLabel = actionButtonLabel;
		}
	}
}
