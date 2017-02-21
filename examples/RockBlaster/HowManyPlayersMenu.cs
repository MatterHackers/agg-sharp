using Gaming.Game;
using Gaming.Graphics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;

namespace RockBlaster
{
	/// <summary>
	/// Description of HowManyPlayersMenu.
	/// </summary>
	public class HowManyPlayersMenu : GuiWidget
	{
		public delegate void StartOnePlayerGameEventHandler(GuiWidget button);

		public event StartOnePlayerGameEventHandler StartOnePlayerGame;

		public delegate void StartTwoPlayerGameEventHandler(GuiWidget button);

		public event StartTwoPlayerGameEventHandler StartTwoPlayerGame;

		public delegate void StartFourPlayerGameEventHandler(GuiWidget button);

		public event StartFourPlayerGameEventHandler StartFourPlayerGame;

		public delegate void CancelMenuEventHandler(GuiWidget button);

		public event CancelMenuEventHandler CancelMenu;

		public HowManyPlayersMenu(RectangleDouble bounds)
		{
			BoundsRelativeToParent = bounds;
			GameImageSequence onePlayerButtonSequence = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "OnePlayerButton");
			Button onePlayerGameButton = new Button(270, 310, new ButtonViewThreeImage(onePlayerButtonSequence.GetImageByIndex(0), onePlayerButtonSequence.GetImageByIndex(1), onePlayerButtonSequence.GetImageByIndex(2)));
			AddChild(onePlayerGameButton);
			onePlayerGameButton.Click += OnStartOnePlayerGameButton;

			GameImageSequence twoPlayerButtonSequence = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "TwoPlayerButton");
			Button twoPlayerGameButton = new Button(400, 310, new ButtonViewThreeImage(twoPlayerButtonSequence.GetImageByIndex(0), twoPlayerButtonSequence.GetImageByIndex(1), twoPlayerButtonSequence.GetImageByIndex(2)));
			AddChild(twoPlayerGameButton);
			twoPlayerGameButton.Click += OnStartTwoPlayerGameButton;

			GameImageSequence fourPlayerButtonSequence = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "FourPlayerButton");
			Button fourPlayerGameButton = new Button(530, 310, new ButtonViewThreeImage(fourPlayerButtonSequence.GetImageByIndex(0), fourPlayerButtonSequence.GetImageByIndex(1), fourPlayerButtonSequence.GetImageByIndex(2)));
			AddChild(fourPlayerGameButton);
			fourPlayerGameButton.Click += OnStartFourPlayerGameButton;

			GameImageSequence cancelButtonSequence = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "NumPlayersCancelButton");
			Button cancelGameButton = new Button(400, 210, new ButtonViewThreeImage(cancelButtonSequence.GetImageByIndex(0), cancelButtonSequence.GetImageByIndex(1), cancelButtonSequence.GetImageByIndex(2)));
			AddChild(cancelGameButton);
			cancelGameButton.Click += OnCancelMenuButton;
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			GameImageSequence menuBackground = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "NumPlayersSelectBackground");
			graphics2D.Render(menuBackground.GetImageByIndex(0), 0, 0);

			base.OnDraw(graphics2D);
		}

		private void OnStartOnePlayerGameButton(object sender, EventArgs mouseEvent)
		{
			if (StartOnePlayerGame != null)
			{
				StartOnePlayerGame(this);
			}
		}

		private void OnStartTwoPlayerGameButton(object sender, EventArgs mouseEvent)
		{
			if (StartTwoPlayerGame != null)
			{
				StartTwoPlayerGame(this);
			}
		}

		private void OnStartFourPlayerGameButton(object sender, EventArgs mouseEvent)
		{
			if (StartFourPlayerGame != null)
			{
				StartFourPlayerGame(this);
			}
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