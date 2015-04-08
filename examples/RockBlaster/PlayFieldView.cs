using Gaming.Game;
using Gaming.Graphics;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;

namespace RockBlaster
{
	public class PlayfieldView : GuiWidget
	{
		private Playfield m_Playfield;

		public delegate void MenuEventHandler(GuiWidget button);

		public event MenuEventHandler Menu;

		public PlayfieldView(RectangleDouble bounds)
		{
			BoundsRelativeToParent = bounds;

			GameImageSequence menuButtonSequence = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "MenuButtonFromGame");
			Button menuButton = new Button(400, 12, new ButtonViewThreeImage(menuButtonSequence.GetImageByIndex(0), menuButtonSequence.GetImageByIndex(1), menuButtonSequence.GetImageByIndex(2)));
			AddChild(menuButton);
			menuButton.Click += new EventHandler(EscapeMenu);
		}

		private void EscapeMenu(object sender, EventArgs mouseEvent)
		{
			if (Menu != null)
			{
				Menu(this);
			}
		}

		public void StartOnePlayerGame()
		{
			Focus();

			m_Playfield = new Playfield();

			m_Playfield.StartOnePlayerGame();

			//m_Playfield.SaveXML("Test");
		}

		internal void StartTwoPlayerGame()
		{
			Focus();

			m_Playfield = new Playfield();

			m_Playfield.StartTwoPlayerGame();
		}

		internal void StartFourPlayerGame()
		{
			Focus();

			m_Playfield = new Playfield();

			m_Playfield.StartFourPlayerGame();
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			GameImageSequence background = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "GameBackground");
			graphics2D.Render(background.GetImageByIndex(0), 0, 0);

			m_Playfield.Draw(graphics2D);

			base.OnDraw(graphics2D);
		}

		public override void OnKeyDown(MatterHackers.Agg.UI.KeyEventArgs keyEvent)
		{
			foreach (Player aPlayer in m_Playfield.PlayerList)
			{
				aPlayer.KeyDown(keyEvent);
			}

			if (keyEvent.Control && keyEvent.KeyCode == Keys.S)
			{
				m_Playfield.SaveXML("TestSave");
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnKeyUp(MatterHackers.Agg.UI.KeyEventArgs keyEvent)
		{
			foreach (Player aPlayer in m_Playfield.PlayerList)
			{
				aPlayer.KeyUp(keyEvent);
			}
			base.OnKeyUp(keyEvent);
		}

		public void OnUpdate(double NumSecondsPassed)
		{
			m_Playfield.Update(NumSecondsPassed);
		}
	}
}