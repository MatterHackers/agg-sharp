using System;

namespace MatterHackers.Agg.UI
{
	public abstract class FileDialogParams
	{
		public FileDialogParams(String fileTypeFilter, String initialDirectory, string title, string actionButtonLabel)
		{
			this.Filter = fileTypeFilter;
			this.InitialDirectory = initialDirectory;
			this.Title = title;
			this.ActionButtonLabel = actionButtonLabel;
		}

		public int FilterIndex { get; set; }

		/// <summary>
		/// The title of the dialog window. If not set will show 'Open' or 'Save' as appropriate
		/// </summary>
		public string Title { get; set; }

		/// <summary>
		/// This does not show on Windows (but does on Mac)
		/// </summary>
		public string ActionButtonLabel { get; set; }

		/// <summary>
		/// The following are complete examples of valid Filter string values:
		/// All Files|*.*
		/// Word Documents|*.doc|All Files|*.*
		/// </summary>
		public string Filter { get; set; }

		public string InitialDirectory { get; set; }

		public string FileName { get; set; }

		public string[] FileNames { get; set; }
	}
}
