using System;

namespace MatterHackers.Agg.UI
{
	public class SaveFileDialogParams : FileDialogParams
	{
		/// The following are examples of valid Filter string values:
		///   Word Documents|*.doc
		///   All Files|*.*
		///   Word Documents|*.doc|Excel Worksheets|*.xls|PowerPoint Presentations|*.ppt|Office Files|*.doc;*.xls;*.ppt|All Files|*.*
		public SaveFileDialogParams(string fileTypeFilter, string initialDirectory = "", string title = "", string actionButtonLabel = "")
			: base(fileTypeFilter, initialDirectory, title, actionButtonLabel)
		{
			if (InitialDirectory == "")
			{
				InitialDirectory = FileDialog.LastDirectoryUsed;
			}
		}
	}
}