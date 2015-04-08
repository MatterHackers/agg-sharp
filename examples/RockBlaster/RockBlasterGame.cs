//#define USE_OPENGL

using Gaming.Game;
using MatterHackers.Agg;
using MatterHackers.Agg.UI;
using System;

namespace RockBlaster
{
	public class RockBlasterGame : GamePlatform
	{
		private MainMenu mainMenu;
		private HowManyPlayersMenu howManyPlayersMenu;
		private CreditsMenu creditsMenu;
		private PlayfieldView playfield;

		public RockBlasterGame(double width, double height)
			: base(30, 5, width, height)
		{
		}

		public void Initialize()
		{
			String PathToUse = "GameData";
			if (!System.IO.Directory.Exists(PathToUse))
			{
				PathToUse = "../../GameData";
				if (!System.IO.Directory.Exists(PathToUse))
				{
					PathToUse = "../../../../RockBlaster/GameData";
					if (!System.IO.Directory.Exists(PathToUse))
					{
					}
				}
			}

			var DataFolderTree = new DataAssetTree(PathToUse);
			DataAssetCache.Instance.SetAssetTree(DataFolderTree);

			Entity.GameWidth = (int)Width;
			Entity.GameHeight = (int)Height;

			mainMenu = new MainMenu(BoundsRelativeToParent);
			AddChild(mainMenu);
			mainMenu.SendToBack();
			mainMenu.StartGame += new MainMenu.StartGameEventHandler(StartGame);
			mainMenu.ShowCredits += new MainMenu.ShowCreditsEventHandler(ShowCredits);
			mainMenu.ExitGame += new MainMenu.ExitGameEventHandler(ExitGame);

			howManyPlayersMenu = new HowManyPlayersMenu(BoundsRelativeToParent);
			AddChild(howManyPlayersMenu);
			howManyPlayersMenu.SendToBack();
			howManyPlayersMenu.StartOnePlayerGame += new HowManyPlayersMenu.StartOnePlayerGameEventHandler(StartOnePlayerGame);
			howManyPlayersMenu.StartTwoPlayerGame += new HowManyPlayersMenu.StartTwoPlayerGameEventHandler(StartTwoPlayerGame);
			howManyPlayersMenu.StartFourPlayerGame += new HowManyPlayersMenu.StartFourPlayerGameEventHandler(StartFourPlayerGame);
			howManyPlayersMenu.CancelMenu += new HowManyPlayersMenu.CancelMenuEventHandler(BackToMainMenu);

			creditsMenu = new CreditsMenu(BoundsRelativeToParent);
			AddChild(creditsMenu);
			creditsMenu.SendToBack();
			creditsMenu.CancelMenu += new CreditsMenu.CancelMenuEventHandler(BackToMainMenu);

			playfield = new PlayfieldView(BoundsRelativeToParent);
			AddChild(playfield);
			playfield.SendToBack();
			playfield.Menu += new PlayfieldView.MenuEventHandler(EndGame);
			playfield.Visible = false;

			MakeMenuVisibleHideOthers(mainMenu);
		}

		private void MakeMenuVisibleHideOthers(GuiWidget whichWindow)
		{
			creditsMenu.Visible = false;
			howManyPlayersMenu.Visible = false;
			mainMenu.Visible = false;
			playfield.Visible = false;

			whichWindow.Visible = true;
		}

		public void StartGame(GuiWidget widget)
		{
			MakeMenuVisibleHideOthers(howManyPlayersMenu);
		}

		public void ShowCredits(GuiWidget widget)
		{
			MakeMenuVisibleHideOthers(creditsMenu);
		}

		public void StartOnePlayerGame(GuiWidget widget)
		{
			playfield.StartOnePlayerGame();
			MakeMenuVisibleHideOthers(playfield);
		}

		public void StartTwoPlayerGame(GuiWidget widget)
		{
			playfield.StartTwoPlayerGame();
			MakeMenuVisibleHideOthers(playfield);
		}

		public void StartFourPlayerGame(GuiWidget widget)
		{
			playfield.StartFourPlayerGame();
			MakeMenuVisibleHideOthers(playfield);
		}

		public void BackToMainMenu(GuiWidget widget)
		{
			MakeMenuVisibleHideOthers(mainMenu);
		}

		public void ExitGame(GuiWidget widget)
		{
			Close();
		}

		public void EndGame(GuiWidget widget)
		{
			MakeMenuVisibleHideOthers(mainMenu);
		}

		public override void OnDraw(Graphics2D graphics2D)
		{
			ShowFrameRate = true;
			base.OnDraw(graphics2D);
		}

		public override void OnKeyDown(KeyEventArgs keyEvent)
		{
			if (keyEvent.KeyCode == Keys.F6)
			{
				// launch the editor
			}

			base.OnKeyDown(keyEvent);
		}

		public override void OnKeyUp(KeyEventArgs keyEvent)
		{
			base.OnKeyUp(keyEvent);
		}

		private bool haveInitialized = false;

		public override void OnUpdate(double numSecondsPassed)
		{
			if (!haveInitialized)
			{
				haveInitialized = true;
				Initialize();
			}

			if (playfield.Visible)
			{
				playfield.OnUpdate(numSecondsPassed);
			}

			base.OnUpdate(numSecondsPassed);
		}

		[STAThread]
		public static void Main(string[] args)
		{
			RockBlasterGame rockBlaster = new RockBlasterGame(800, 600);

			rockBlaster.Title = "Rock Blaster";
			rockBlaster.ShowAsSystemWindow();
		}
	}
}