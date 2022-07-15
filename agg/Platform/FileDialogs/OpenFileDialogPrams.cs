using System;

namespace MatterHackers.Agg.Platform
{
	public class OpenFileDialogParams : FileDialogParams
	{
		public bool MultiSelect { get; set; }

		/// <summary>
		/// These are the parameters passed to an open file dialog
		/// </summary>
		/// <param name="fileTypeFilter">The following are complete examples of valid Filter string values: "All Files|*.*", "Word Documents|*.doc|All Files|*.*"</param>
		/// <param name="initialDirectory">Where to start</param>
		/// <param name="multiSelect">Allow more than one file selection</param>
		/// <param name="title">What are we opening</param>
		/// <param name="actionButtonLabel">The text on the 'Open' button</param>
		public OpenFileDialogParams(string fileTypeFilter, string initialDirectory = "", bool multiSelect = false, string title = "", string actionButtonLabel = "")
			: base(fileTypeFilter, initialDirectory, title, actionButtonLabel)
		{
			this.MultiSelect = multiSelect;
		}
	}
}