using System;
using System.Collections.Generic;

using Gaming.Game;
using Gaming.Graphics;

using AGG;
using AGG.Image;
using AGG.VertexSource;
using AGG.UI;
using AGG.Transform;

using Tao.OpenGl;

namespace CTFA
{
    public class CTFAGame : Gaming.Game.GamePlatform
    {
    	MainMenu m_MainMenu;
        CreditsMenu m_CreditsMenu;
    	PlayfieldView m_Playfield;
        public CTFAGame(ImageFormats format)
            : base(1.0f/60.0f, 5, format)
        {
        }

        public override void OnInitialize()
        {
            base.OnInitialize();

            String PathToUse = "GameData";
            if (!System.IO.Directory.Exists(PathToUse))
            {
                PathToUse = "../../GameData";
                if (!System.IO.Directory.Exists(PathToUse))
                {
                    PathToUse = "../../../../CTFA/GameData";
                    if (!System.IO.Directory.Exists(PathToUse))
                    {
                    }
                }
            }

            DataAssetTree DataFolderTree = new DataAssetTree(PathToUse);
            DataAssetCache.Instance.SetAssetTree(DataFolderTree);

            m_MainMenu = new MainMenu(Bounds);
            AddChild(m_MainMenu);
            m_MainMenu.SendToBack();
            m_MainMenu.StartGame += new MainMenu.StartGameEventHandler(StartGame);
            m_MainMenu.ShowCredits +=new MainMenu.ShowCreditsEventHandler(ShowCredits);
            m_MainMenu.ExitGame += new MainMenu.ExitGameEventHandler(ExitGame);

            m_CreditsMenu = new CreditsMenu(Bounds);
            AddChild(m_CreditsMenu);
            m_CreditsMenu.SendToBack();
            m_CreditsMenu.CancelMenu += new CreditsMenu.CancelMenuEventHandler(BackToMainMenu);

            m_Playfield = new PlayfieldView(Bounds);
            AddChild(m_Playfield);
            m_Playfield.SendToBack();
            m_Playfield.Menu += new PlayfieldView.MenuEventHandler(EndGame);
            m_Playfield.Visible = false;

            Entity.GameWidth = (int)m_Playfield.BackgroundImage.Width();
            Entity.GameHeight = (int)m_Playfield.BackgroundImage.Height();

            MakeMenuVisibleHideOthers(m_MainMenu);
        }

        void MakeMenuVisibleHideOthers(GUIWidget whichWindow)
        {
            m_CreditsMenu.Visible = false;
            m_MainMenu.Visible = false;
            m_Playfield.Visible = false;

            whichWindow.Visible = true;
        }

        public void StartGame(GUIWidget widget)
        {
            StartNewGame(widget);
        }

        public void ShowCredits(GUIWidget widget)
        {
            MakeMenuVisibleHideOthers(m_CreditsMenu);
        }

        public void StartNewGame(GUIWidget widget)
        {
            m_Playfield.StartNewGame();
            MakeMenuVisibleHideOthers(m_Playfield);
        }

        public void BackToMainMenu(GUIWidget widget)
        {
            MakeMenuVisibleHideOthers(m_MainMenu);
        }

        public void ExitGame(GUIWidget widget)
        {
        	Close();
        }
        
        public void EndGame(GUIWidget widget)
        {
            MakeMenuVisibleHideOthers(m_MainMenu);
        }

        public override void OnDraw(RendererBase rendererToDrawWith)
        {
            this.ShowFrameRate = false;
            base.OnDraw(rendererToDrawWith);
        }

        public override void OnKeyDown(AGG.UI.KeyEventArgs keyEvent)
		{
        	if(keyEvent.KeyCode == Keys.F6)
        	{
        		// launch the editor
        	}
        	
			base.OnKeyDown(keyEvent);
		}

		public override void OnKeyUp(AGG.UI.KeyEventArgs keyEvent)
		{
			base.OnKeyUp(keyEvent);
		}

        public override void OnUpdate(double NumSecondsPassed)
        {
        	if(m_Playfield.Visible)
        	{
        		m_Playfield.OnUpdate(NumSecondsPassed);
        	}
            
            base.OnUpdate(NumSecondsPassed);
        }

        public static void StartDemo()
        {
            CTFAGame app = new CTFAGame(ImageFormats.pix_format_rgba32);
            app.Caption = "Rock blaster is a game a lot like Asteroids.";

            int GameWidth = 800;
            int GameHeight = 600;
            if (app.init(GameWidth, GameHeight, WindowFlags.UseOpenGL))
            //if (app.init(GameWidth, GameHeight, WindowFlags.None))
            {
                app.run();
            }
        }

        [STAThread]
        public static void Main(string[] args)
        {
            StartDemo();
        }
    };
}
