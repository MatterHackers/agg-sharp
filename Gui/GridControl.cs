using System.Collections.Generic;

namespace MatterHackers.Agg.UI
{
	public enum GridGrowBehavior { FixedSize, AddRows, AddColumns };

	public enum SizeBehavior { AbsolutePixels, SameAsPeers, PercentOfParent };

	public class GridControl : LayoutPanel
	{
#if false
        class columnOrRowData
        {
            SizeBehavior sizeBehavior;
        }
#endif

		private List<SizeBehavior> columnSizeBehavior = new List<SizeBehavior>();
		private List<SizeBehavior> rowSizeBehavior = new List<SizeBehavior>();

		public int RowCount { get; set; }

		public int ColumnCount { get; set; }

		public GridGrowBehavior GrowBehavior { get; set; }

		public GridControl()
		{
			RowCount = 0;
			ColumnCount = 0;

			GrowBehavior = GridGrowBehavior.AddRows;
		}

		public override void AddChild(GuiWidget child, int indexInChildrenList = -1)
		{
			base.AddChild(child, indexInChildrenList);
		}
	}
}