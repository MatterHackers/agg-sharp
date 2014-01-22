using System;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using AGG;
using AGG.Image;
using AGG.VertexSource;
using AGG.Transform;
using AGG.UI;

using Gaming.Math;
using Gaming.Game;
using Gaming.Graphics;

namespace CTFA
{
    public class PlayfieldView : GUIWidget
    {
        internal class PlayerView
        {
            rect_d screenWindow;

            public PlayerView(rect_d inScreenWindow)
            {
                screenWindow = inScreenWindow;
            }

            public void SetRendererPreDraw(ImageBuffer background, RendererBase rendererToDrawWith, Player playerToCenterOn)
            {
                rendererToDrawWith.PushTransform();
                Vector2D windowCenter = new Vector2D(screenWindow.Left + (screenWindow.Right - screenWindow.Left) / 2, screenWindow.Bottom + (screenWindow.Top - screenWindow.Bottom) / 2);
                Vector2D playerPos = playerToCenterOn.Position;
                Vector2D playfieldOffset = windowCenter - playerPos;
                if (playfieldOffset.x > screenWindow.Left)
                {
                    playfieldOffset.x = screenWindow.Left;
                }
                if (playfieldOffset.x < -background.Width() + screenWindow.Right)
                {
                    playfieldOffset.x = -background.Width() + screenWindow.Right;
                }
                if (playfieldOffset.y > screenWindow.Bottom)
                {
                    playfieldOffset.y = screenWindow.Bottom;
                }
                if (playfieldOffset.y < -background.Height() + screenWindow.Top)
                {
                    playfieldOffset.y = -background.Height() + screenWindow.Top;
                }
                Affine translation = Affine.NewTranslation(playfieldOffset);
                rendererToDrawWith.SetTransform(rendererToDrawWith.GetTransform() * translation);
                rendererToDrawWith.SetClippingRect(screenWindow);
            }

            public void SetRendererPostDraw(RendererBase rendererToDrawWith)
            {
                rendererToDrawWith.SetClippingRect(new rect_d(0, 0, 800, 600));
                rendererToDrawWith.PopTransform();
            }
        }

        private ImageSequence backgroundImageSequence;
        private ImageBuffer backgroundImage;
        Playfield playfield;
        const int numPlayers = 4;
        PlayerView[] playerViews = new PlayerView[numPlayers];

        public delegate void MenuEventHandler(GUIWidget button);
        public event MenuEventHandler Menu;

        public PlayfieldView(rect_d bounds)
        {
            Bounds = bounds;

            backgroundImageSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "Map1Background");
            backgroundImage = backgroundImageSequence.GetImageByIndex(0);

            ImageSequence menuButtonSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "MenuButtonFromGame");
            ButtonWidget menuButton = new ButtonWidget(400, 12, new ThreeImageButtonView(menuButtonSequence.GetImageByIndex(0), menuButtonSequence.GetImageByIndex(1), menuButtonSequence.GetImageByIndex(2)));
            AddChild(menuButton);
            menuButton.ButtonClick += new ButtonWidget.ButtonEventHandler(EscapeMenu);

            playerViews[0] = new PlayerView(new rect_d(400, 300, 800, 600));
            playerViews[1] = new PlayerView(new rect_d(400, 0, 800, 300));
            playerViews[2] = new PlayerView(new rect_d(0, 300, 400, 600));
            playerViews[3] = new PlayerView(new rect_d(0, 0, 400, 300));
        }

        public ImageBuffer BackgroundImage
        {
            get
            {
                return backgroundImage;
            }
        }

        private void EscapeMenu(object sender, MouseEventArgs mouseEvent)
        {
            if (Menu != null)
            {
                Menu(this);
            }
        }

        internal void StartNewGame()
        {
            Focus();

            playfield = new Playfield();

            playfield.StartNewGame();
        }

        bool haveDrawnWalls = false;
        public override void OnDraw(RendererBase rendererToDrawWith)
        {
            ImageBuffer levelMap = playfield.LevelMap;
            int offset;
            byte[] buffer = levelMap.GetBuffer(out offset);

            if (!haveDrawnWalls)
            {
                RendererBase backgroundRenderer = BackgroundImage.NewRenderer();
                rect_i boundsI = BackgroundImage.GetBoundingRect();
                rect_d bounds = new rect_d(boundsI.Left, boundsI.Bottom, boundsI.Right, boundsI.Top);
                backgroundRenderer.SetClippingRect(bounds);
                ImageSequence wallTileSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "WallTile");
                for (int y = 0; y < levelMap.Height(); y++)
                {
                    for (int x = 0; x < levelMap.Width(); x++)
                    {
                        if (buffer[levelMap.GetBufferOffsetXY(x, y)] == 0)
                        {
                            int index = 0;
                            // what type of wall
                            if (x < levelMap.Width() -1 
                                && buffer[levelMap.GetBufferOffsetXY(x + 1, y + 0)] == 0)
                            {
                                index |= 8;
                            }

                            if (y < levelMap.Height() -1 
                                && buffer[levelMap.GetBufferOffsetXY(x + 0, y + 1)] == 0)
                            {
                                index |= 4;
                            }

                            if (x > 0
                                && buffer[levelMap.GetBufferOffsetXY(x - 1, y + 0)] == 0)
                            {
                                index |= 2;
                            }

                            if (y > 0
                                && buffer[levelMap.GetBufferOffsetXY(x + 0, y - 1)] == 0)
                            {
                                index |= 1;
                            }

                            backgroundRenderer.Render(wallTileSequence.GetImageByIndex(index), x * 16, y * 16);
                        }
                    }
                }
                haveDrawnWalls = true;
            }

            //for (int i = 0; i < 1; i++)
            for (int i = 0; i < numPlayers; i++)
            {
                playerViews[i].SetRendererPreDraw(BackgroundImage, rendererToDrawWith, playfield.PlayerList[i]);

                rendererToDrawWith.Render(BackgroundImage, 0, 0);

                foreach (SequenceEntity aSequenceEntity in playfield.SequenceEntityList)
                {
                    aSequenceEntity.Draw(rendererToDrawWith);
                }

                foreach (Player aPlayer in playfield.PlayerList)
                {
                    aPlayer.Draw(rendererToDrawWith);
                }

                playfield.sword.Draw(rendererToDrawWith);
                playfield.key.Draw(rendererToDrawWith);
                playfield.shield.Draw(rendererToDrawWith);

                playerViews[i].SetRendererPostDraw(rendererToDrawWith);
            }

            ImageSequence hud = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), (playfield.PlayerList.Count).ToString() + "PlayerHUD");
            rendererToDrawWith.Render(hud.GetImageByIndex(0), 400, 300);

            foreach (Player aPlayer in playfield.PlayerList)
            {
                aPlayer.DrawScore(rendererToDrawWith);
            }

            rendererToDrawWith.Line(0.5, 300.5, 800.5, 300.5, new RGBA_Bytes(255, 20, 20));
            rendererToDrawWith.Line(400.5, 0.5, 400.5, 600.5, new RGBA_Bytes(255, 20, 20));

            base.OnDraw(rendererToDrawWith);
        }

        public override void OnKeyDown(AGG.UI.KeyEventArgs keyEvent)
        {
            foreach (Player aPlayer in playfield.PlayerList)
            {
                aPlayer.KeyDown(keyEvent);
            }

            if (keyEvent.Control && keyEvent.KeyCode == Keys.S)
            {
                playfield.SaveXML("TestSave");
            }

            base.OnKeyDown(keyEvent);
        }

        public override void OnKeyUp(AGG.UI.KeyEventArgs keyEvent)
        {
            foreach (Player aPlayer in playfield.PlayerList)
            {
                aPlayer.KeyUp(keyEvent);
            }
            base.OnKeyUp(keyEvent);
        }

        public void OnUpdate(double NumSecondsPassed)
        {
            playfield.Update(NumSecondsPassed);
        }
    }
}
