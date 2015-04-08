using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	public class FileDropEventArgs : EventArgs
	{
		public List<string> DroppedFiles;
		private double x;
		private double y;

		public double X { get { return x; } }

		public double Y { get { return y; } }

		public FileDropEventArgs(List<string> droppedFiles, double x, double y)
		{
			this.x = x;
			this.y = y;
			this.DroppedFiles = FileDialog.ResolveFilePaths(droppedFiles).ToList();
		}

		public bool AcceptDrop { get; set; }
	}
}