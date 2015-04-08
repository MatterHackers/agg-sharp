using System;

namespace MatterHackers.Agg.UI
{
	public class FlowLayoutWidget : LayoutPanel
	{
		public FlowDirection FlowDirection
		{
			get
			{
				LayoutEngineFlow flowLayout = LayoutEngine as LayoutEngineFlow;
				if (flowLayout != null)
				{
					return flowLayout.FlowDirection;
				}

				throw new Exception("Don't change the LayoutEngine on a FlowLayoutWidget.");
			}

			set
			{
				LayoutEngineFlow flowLayout = LayoutEngine as LayoutEngineFlow;
				if (flowLayout == null)
				{
					throw new Exception("Don't change the LayoutEngine on a FlowLayoutWidget.");
				}
				flowLayout.FlowDirection = value;
			}
		}

		new public static BorderDouble DefaultPadding = new BorderDouble(0);
		new public static BorderDouble DefaultMargin = new BorderDouble(0);

		public FlowLayoutWidget(UI.FlowDirection dirrection = UI.FlowDirection.LeftToRight,
			HAnchor hAnchor = HAnchor.FitToChildren, VAnchor vAnchor = VAnchor.FitToChildren)
			: base(hAnchor, vAnchor)
		{
			Padding = DefaultPadding;
			Margin = DefaultMargin;

			LayoutEngine = new LayoutEngineFlow(dirrection);
		}
	}
}