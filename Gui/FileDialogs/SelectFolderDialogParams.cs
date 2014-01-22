using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

namespace MatterHackers.Agg.UI
{
    public class SelectFolderDialogParams
    {
        public enum RootFolderTypes { MyComputer };

        public String Description { get; set; }
        public RootFolderTypes RootFolder { get; set; }
        public bool ShowNewFolderButton { get; set; }

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
        
        public SelectFolderDialogParams(String description, RootFolderTypes rootFolder = RootFolderTypes.MyComputer, bool showNewFolderButton = true, string title = "", string actionButtonLabel = "")
        {
            this.Description = description;
            this.RootFolder = rootFolder;
            this.ShowNewFolderButton = showNewFolderButton;
            this.Title = title;
            this.ActionButtonLabel = actionButtonLabel;
        }
    }
}
