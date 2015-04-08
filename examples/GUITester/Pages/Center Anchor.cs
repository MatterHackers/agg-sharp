using MatterHackers.Agg.UI;

namespace MatterHackers.Agg
{
	public class AnchorCenterButtonsTestPAge : TabPage
	{
		private const int offset = 2;

		public AnchorCenterButtonsTestPAge()
			: base("Center Anchor")
		{
			//CreateButton(UI.HAnchor.Left | UI.HAnchor.Right, UI.VAnchor.Bottom | UI.VAnchor.Top);

			//CreateButton(UI.HAnchor.Left | UI.HAnchor.Center, UI.VAnchor.Top | UI.VAnchor.Center);
			CreateButton(UI.HAnchor.ParentLeft | UI.HAnchor.ParentCenter, UI.VAnchor.ParentBottom | UI.VAnchor.ParentCenter);
			CreateButton(UI.HAnchor.ParentCenter | UI.HAnchor.ParentRight, UI.VAnchor.ParentCenter | UI.VAnchor.ParentTop);
		}

		private void CreateButton(HAnchor hAnchor, VAnchor vAnchor)
		{
			Button anchorButton = new Button(hAnchor.ToString() + " - " + vAnchor.ToString());
			anchorButton.BackgroundColor = RGBA_Bytes.Red;
			anchorButton.HAnchor = hAnchor;
			anchorButton.VAnchor = vAnchor;
			anchorButton.Margin = new BorderDouble(offset);
			AddChild(anchorButton);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Line(Width / 2, 0, Width / 2, Height, RGBA_Bytes.Red);
			graphics2D.Line(0, Height / 2, Width, Height / 2, RGBA_Bytes.Red);
			base.OnDraw(graphics2D);
		}
	}
}