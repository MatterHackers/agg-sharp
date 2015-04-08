using Gaming.Game;
using Gaming.Graphics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;

namespace RockBlaster
{
	/// <summary>
	/// Description of CreditsMenu.
	/// </summary>
	public class CreditsMenu : GuiWidget
	{
		public delegate void CancelMenuEventHandler(GuiWidget button);

		public event CancelMenuEventHandler CancelMenu;

		public CreditsMenu(RectangleDouble bounds)
		{
			BoundsRelativeToParent = bounds;

			GameImageSequence cancelButtonSequence = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "NumPlayersCancelButton");
			Button cancelGameButton = new Button(400, 200, new ButtonViewThreeImage(cancelButtonSequence.GetImageByIndex(0), cancelButtonSequence.GetImageByIndex(1), cancelButtonSequence.GetImageByIndex(2)));
			AddChild(cancelGameButton);
			cancelGameButton.Click += new EventHandler(OnCancelMenuButton);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			GameImageSequence menuBackground = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "CreditsScreen");
			graphics2D.Render(menuBackground.GetImageByIndex(0), 0, 0);

			base.OnDraw(graphics2D);
		}

		private void OnCancelMenuButton(object sender, EventArgs mouseEvent)
		{
			if (CancelMenu != null)
			{
				CancelMenu(this);
			}
		}
	}
}