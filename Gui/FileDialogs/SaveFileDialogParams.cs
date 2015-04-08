using System;

namespace MatterHackers.Agg.UI
{
	public class SaveFileDialogParams : FileDialogParams
	{
		/// <summary>
		///
		/// </summary>
		/// <param name="fileTypeFilter"></param>
		/// The following are complete examples of valid Filter string values:
		/// Word Documents|*.doc
		/// Excel Worksheets|*.xls
		/// PowerPoint Presentations|*.ppt
		/// Office Files|*.doc;*.xls;*.ppt
		/// All Files|*.*
		/// Word Documents|*.doc|Excel Worksheets|*.xls|PowerPoint Presentations|*.ppt|Office Files|*.doc;*.xls;*.ppt|All Files|*.*
		/// <param name="initialDirectory"></param>
		public SaveFileDialogParams(String fileTypeFilter, String initialDirectory = "", string title = "", string actionButtonLabel = "")
			: base(fileTypeFilter, initialDirectory, title, actionButtonLabel)
		{
			if (InitialDirectory == "")
			{
				InitialDirectory = FileDialog.LastDirectoryUsed;
			}
		}
	}
}