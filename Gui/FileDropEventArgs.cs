using System;
using System.Collections.Generic;
using System.Linq;
using MatterHackers.Agg.Platform;

namespace MatterHackers.Agg.UI
{
	public class FileDropEventArgs : EventArgs
	{
		public List<string> DroppedFiles;

		public double X { get; }

		public double Y { get; }

		public FileDropEventArgs(List<string> droppedFiles, double x, double y)
		{
			this.X = x;
			this.Y = y;
			this.DroppedFiles = droppedFiles.Select(path => AggContext.FileDialogs.ResolveFilePath(path)).ToList();
		}

		public bool AcceptDrop { get; set; }
	}
}
