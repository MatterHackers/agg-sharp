using MatterHackers.Agg.Font;
using MatterHackers.Agg.UI;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg
{
	public class AnchorTestsPage : TabPage
	{
		private TextWidget anchorLeft;
		private TextWidget anchorLeftCentered;
		private TextWidget anchorRight;
		private TextWidget anchorLeftRight;
		private TextWidget anchorLeftRightCenter;
		private TextWidget anchorLeftRightRight;

		private TextWidget anchorBottom;
		private TextWidget anchorTop;
		private TextWidget anchorBottomTop;

		private TextWidget anchorAll;

		public AnchorTestsPage()
			: base("Text Anchor Tests")
		{
			Name = "Anchor Tests Page";
			SuspendLayout();
			// add our controls after we are attached to our parent
			anchorLeft = new TextWidget("Left Anchor", 12);
			AddChild(anchorLeft);
			//anchorLeft.DebugShowBounds = true;

			anchorAll = new TextWidget("All Anchor");
			anchorAll.Margin = new BorderDouble(20);
			AddChild(anchorAll);
			//anchorAll.DebugShowBounds = true;

			anchorLeftCentered = new TextWidget("Left Anchor - Centered", 12, justification: Justification.Center);
			AddChild(anchorLeftCentered);
			//anchorLeftCentered.DebugShowBounds = true;

			anchorRight = new TextWidget("Right Anchor", 12);
			AddChild(anchorRight);
			//anchorRight.DebugShowBounds = true;

			anchorLeftRight = new TextWidget("Left Right Anchor", 12);
			AddChild(anchorLeftRight);
			//anchorLeftRight.DebugShowBounds = true;

			anchorLeftRightCenter = new TextWidget("L R Anchor - Centered", 12, justification: Justification.Center);
			AddChild(anchorLeftRightCenter);
			//anchorLeftRightCenter.DebugShowBounds = true;

			anchorLeftRightRight = new TextWidget("L R Anchor - Right", 12, justification: Justification.Right);
			AddChild(anchorLeftRightRight);
			//anchorLeftRightRight.DebugShowBounds = true;

			anchorBottom = new TextWidget("Bottom Anchor", 12);
			AddChild(anchorBottom);
			//anchorBottom.DebugShowBounds = true;

			anchorTop = new TextWidget("Top Anchor", 12);
			AddChild(anchorTop);
			//anchorTop.DebugShowBounds = true;

			anchorBottomTop = new TextWidget("Bottom Top Anchor", 12);
			AddChild(anchorBottomTop);
			//anchorBottomTop.DebugShowBounds = true;
			anchorBottomTop.VAnchor = VAnchor.ParentTop | VAnchor.ParentBottom;
			ResumeLayout();
			PerformLayout();
		}

		public override void OnParentChanged(EventArgs e)
		{
			base.OnParentChanged(e);

			int leftDist = 150;
			int rightDist = 320;

			anchorLeft.OriginRelativeParent = new Vector2(leftDist, 290);
			anchorLeft.Margin = new BorderDouble(leftDist, 0, 0, 0);
			anchorLeft.HAnchor = HAnchor.ParentLeft;

			anchorAll.OriginRelativeParent = new Vector2(leftDist, 330);
			anchorAll.AnchorAll();

			anchorLeftCentered.OriginRelativeParent = new Vector2(leftDist, 250);
			anchorLeftCentered.HAnchor = HAnchor.ParentLeft;

			anchorRight.OriginRelativeParent = new Vector2(leftDist, 210);
			anchorRight.HAnchor = HAnchor.ParentRight;

			anchorLeftRight.OriginRelativeParent = new Vector2(leftDist, 170);
			anchorLeftRight.HAnchor = HAnchor.ParentRight | HAnchor.ParentLeft;

			anchorLeftRightCenter.OriginRelativeParent = new Vector2(leftDist, 130);
			anchorLeftRightCenter.HAnchor = HAnchor.ParentRight | HAnchor.ParentLeft;

			anchorLeftRightRight.OriginRelativeParent = new Vector2(leftDist, 100);
			anchorLeftRightRight.HAnchor = HAnchor.ParentRight | HAnchor.ParentLeft;

			anchorBottom.OriginRelativeParent = new Vector2(rightDist, 290);
			anchorBottom.VAnchor = VAnchor.ParentBottom;

			anchorTop.OriginRelativeParent = new Vector2(rightDist, 250);
			anchorTop.VAnchor = VAnchor.ParentTop;

			//anchorBottomTop.OriginRelativeParent = new Vector2(rightDist, 210);
			//anchorBottomTop.AnchorFlags = AnchorFlags.Top | AnchorFlags.Bottom;
		}
	}
}