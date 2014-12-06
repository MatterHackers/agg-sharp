using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class OpenFileDialogParams : FileDialogParams
    {
        public bool MultiSelect { get; set; }

        /// <summary>
        /// These are the parameters passed to an open file dialog
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
        /// <param name="multiSelect"></param>
        public OpenFileDialogParams(String fileTypeFilter, String initialDirectory = "", bool multiSelect = false, string title = "", string actionButtonLabel = "")
            : base(fileTypeFilter, initialDirectory, title, actionButtonLabel)
        {
            if (InitialDirectory == "")
            {
                InitialDirectory = FileDialog.LastDirectoryUsed;
            }
            this.MultiSelect = multiSelect;
        }
    }
}
