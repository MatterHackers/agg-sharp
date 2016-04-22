using System;

namespace MatterHackers.Agg.UI
{
	public class FlowLayoutWidget : GuiWidget
	{
		private LayoutEngineFlow layoutEngine;

		public FlowLayoutWidget(FlowDirection direction = FlowDirection.LeftToRight, HAnchor hAnchor = HAnchor.FitToChildren, VAnchor vAnchor = VAnchor.FitToChildren)
			: base(hAnchor, vAnchor)
		{
			layoutEngine = new LayoutEngineFlow(direction);
			LayoutEngine = layoutEngine;
		}

		public FlowDirection FlowDirection
		{
			get
			{
				return layoutEngine.FlowDirection;
			}

			set
			{
				layoutEngine.FlowDirection = value;
			}
		}
	}
}