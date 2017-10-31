using MatterHackers.Agg.UI;

namespace MatterHackers.Agg
{
	public class ButtonAnchorTestPage : TabPage
	{
		private const int offset = 20;

		public ButtonAnchorTestPage()
			: base("Button Anchor Tests")
		{
			CreateButton(UI.HAnchor.Left, UI.VAnchor.Bottom);
			CreateButton(UI.HAnchor.Center, UI.VAnchor.Bottom);
			CreateButton(UI.HAnchor.Right, UI.VAnchor.Bottom);

			CreateButton(UI.HAnchor.Left, UI.VAnchor.Center);
			CreateButton(UI.HAnchor.Center, UI.VAnchor.Center);
			CreateButton(UI.HAnchor.Right, UI.VAnchor.Center);

			CreateButton(UI.HAnchor.Left, UI.VAnchor.Top);
			CreateButton(UI.HAnchor.Center, UI.VAnchor.Top);
			CreateButton(UI.HAnchor.Right, UI.VAnchor.Top);
		}

		private void CreateButton(HAnchor hAnchor, VAnchor vAnchor)
		{
			Button anchorButton = new Button(hAnchor.ToString() + " " + vAnchor.ToString());
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