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

			ImageSequence cancelButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "NumPlayersCancelButton");
			Button cancelGameButton = new Button(400, 200, new ButtonViewThreeImage(cancelButtonSequence.GetImageByIndex(0), cancelButtonSequence.GetImageByIndex(1), cancelButtonSequence.GetImageByIndex(2)));
			AddChild(cancelGameButton);
			cancelGameButton.Click += OnCancelMenuButton;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ImageSequence menuBackground = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "CreditsScreen");
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