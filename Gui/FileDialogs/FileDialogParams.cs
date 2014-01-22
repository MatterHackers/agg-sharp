using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public abstract class FileDialogParams
    {
        String fileTypeFilter; // standard winforms format
        String initialDirectory;

        String fileName;
        String[] fileNames = null;

        public int FilterIndex
        {
            get;
            set;
        }

        /// <summary>
        /// The title of the dialog window. If not set will show 'Open' or 'Save' as appropriate
        /// </summary>
        public string Title
        {
            get;
            set;
        }

        /// <summary>
        /// This does not show on Windows (but does on mac.
        /// </summary>
        public string ActionButtonLabel
        {
            get;
            set;
        }

        /// <summary>
        /// The following are complete examples of valid Filter string values:
        /// Word Documents|*.doc
        /// Excel Worksheets|*.xls
        /// PowerPoint Presentations|*.ppt
        /// Office Files|*.doc;*.xls;*.ppt
        /// All Files|*.*
        /// Word Documents|*.doc|Excel Worksheets|*.xls|PowerPoint Presentations|*.ppt|Office Files|*.doc;*.xls;*.ppt|All Files|*.*
        /// </summary>
        public String Filter
        {
            get { return fileTypeFilter; }
            set { fileTypeFilter = value; }
        }

        public String InitialDirectory
        {
            get { return initialDirectory; }
            set { initialDirectory = value; }
        }

        public String FileName
        {
            get { return fileName; }
            set { fileName = value; }
        }

        public String[] FileNames
        {
            get { return fileNames; }
            set { fileNames = value; }
        }

        public FileDialogParams(String fileTypeFilter, String initialDirectory, string title, string actionButtonLabel)
        {
            this.Filter = fileTypeFilter;
            this.InitialDirectory = initialDirectory;
            this.Title = title;
            this.ActionButtonLabel = actionButtonLabel;
        }
    }
}
