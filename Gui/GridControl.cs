using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;

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

        List<SizeBehavior> columnSizeBehavior = new List<SizeBehavior>();
        List<SizeBehavior> rowSizeBehavior = new List<SizeBehavior>();

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
