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
			CreateButton(UI.HAnchor.Left | UI.HAnchor.Center, UI.VAnchor.Bottom | UI.VAnchor.Center);
			CreateButton(UI.HAnchor.Center | UI.HAnchor.Right, UI.VAnchor.Center | UI.VAnchor.Top);
		}

		private void CreateButton(HAnchor hAnchor, VAnchor vAnchor)
		{
			Button anchorButton = new Button(hAnchor.ToString() + " - " + vAnchor.ToString());
			anchorButton.BackgroundColor = Color.Red;
			anchorButton.HAnchor = hAnchor;
			anchorButton.VAnchor = vAnchor;
			anchorButton.Margin = new BorderDouble(offset);
			AddChild(anchorButton);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			graphics2D.Line(Width / 2, 0, Width / 2, Height, Color.Red);
			graphics2D.Line(0, Height / 2, Width, Height / 2, Color.Red);
			base.OnDraw(graphics2D);
		}
	}
}