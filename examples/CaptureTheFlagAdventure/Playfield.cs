//#define USE_GLSL
using System;
using System.Collections;
using System.Collections.Generic;
using System.Xml;
using System.Xml.Schema;
using System.Xml.Serialization;

using AGG;
using AGG.Image;
using AGG.VertexSource;
using AGG.UI;

using Gaming.Math;
using Gaming.Game;
using Gaming.Graphics;

using Tao.OpenGl;

namespace CTFA
{
    public class Sword : Entity
    {
        protected override void DoDraw(RendererBase destRenderer)
        {
            ImageBuffer swordImage = ((ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "Sword")).GetImageByIndex(0);
            destRenderer.Render(swordImage, m_Position.x, m_Position.y);
        }
    }

    public class Key : Entity
    {
        protected override void DoDraw(RendererBase destRenderer)
        {
            ImageBuffer keyImage = ((ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "Keys")).GetImageByIndex(4);
            destRenderer.Render(keyImage, m_Position.x, m_Position.y);
        }
    }

    public class Shield : Entity
    {
        protected override void DoDraw(RendererBase destRenderer)
        {
            ImageBuffer shieldImage = ((ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "Shield")).GetImageByIndex(0);
            destRenderer.Render(shieldImage, m_Position.x, m_Position.y);
        }
    }

    public class Playfield : GameObject
    {
        private List<Entity> sequenceEntityList = new List<Entity>();

        [GameData("PlayerList")]
        private List<Player> playerList = new List<Player>();

        ImageBuffer levelMap;
        public Sword sword;
        public Key key;
        Vector2D keyStart;
        public Shield shield;


        #region GameObjectStuff
        public Playfield()
        {
        }
        public static new GameObject Load(String PathName)
        {
            return GameObject.Load(PathName);
        }
        #endregion

        public List<Player> PlayerList
        {
            get
            {
                return playerList;
            }
        }

        public List<Entity> SequenceEntityList
        {
            get
            {
                return sequenceEntityList;
            }
        }

        public ImageBuffer LevelMap
        {
            get
            {
                return levelMap = ((ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "LevelMap")).GetImageByIndex(0);
            }
        }

        internal void StartNewGame()
        {
            playerList.Add(new Player(0, Keys.H, Keys.J, Keys.K, Keys.U, this));
            playerList[0].Position = new Vector2D(Entity.GameWidth / 4 * 3, Entity.GameHeight / 4 * 3);

            playerList.Add(new Player(1, Keys.Oemcomma, Keys.OemPeriod, Keys.OemQuestion, Keys.L, this));
            playerList[1].Position = new Vector2D(Entity.GameWidth / 4 * 3, Entity.GameHeight / 4);

            playerList.Add(new Player(2, Keys.A, Keys.S, Keys.D, Keys.W, this));
            playerList[2].Position = new Vector2D(Entity.GameWidth / 4, Entity.GameHeight / 4 * 3);

            playerList.Add(new Player(3, Keys.C, Keys.V, Keys.B, Keys.F, this));
            playerList[3].Position = new Vector2D(Entity.GameWidth / 4, Entity.GameHeight / 4);

            PlaceObjectsOnLevel();
        }

        void PlaceObjectsOnLevel()
        {
            ImageBuffer levelMap = LevelMap;
            int offset;
            byte[] buffer = levelMap.GetBuffer(out offset);

            for (int y = 0; y < levelMap.Height(); y++)
            {
                for (int x = 0; x < levelMap.Width(); x++)
                {
                    offset = levelMap.GetBufferOffsetXY(x, y);
                    switch (buffer[offset])
                    {
                        case 220:
                            // this is the sword.
                            sword = new Sword();
                            sword.Position = new Vector2D(x * 16 + 8, y * 16 + 8);
                            break;

                        case 170:
                            // this is the key.
                            key = new Key();
                            key.Position = new Vector2D(x * 16 + 8, y * 16 + 8);
                            keyStart = key.Position;
                            break;

                        case 2:
                            // this is the red player.
                            playerList[0].Position = new Vector2D(x * 16 + 8, y * 16 + 8);
                            break;

                        case 35:
                            // this is the green player.
                            playerList[1].Position = new Vector2D(x * 16 + 8, y * 16 + 8);
                            break;

                        case 251:
                            // this is the blue player.
                            playerList[2].Position = new Vector2D(x * 16 + 8, y * 16 + 8);
                            break;

                        case 5:
                            // this is the yellow player.
                            playerList[3].Position = new Vector2D(x * 16 + 8, y * 16 + 8);
                            break;

                        case 248:
                            // this is the shield.
                            shield = new Shield();
                            shield.Position = new Vector2D(x * 16 + 8, y * 16 + 8);
                            break;

                        default:
                            break;
                    }
                }
            }
        }

        protected void RemoveDeadStuff(List<Entity> listToRemoveFrom)
        {
            List<Entity> RemoveList = new List<Entity>();

            foreach (Entity aEntity in listToRemoveFrom)
            {
                if (aEntity.Damage >= aEntity.MaxDamage)
                {
                    RemoveList.Add(aEntity);
                }
            }

            foreach (Entity aEntity in RemoveList)
            {
                aEntity.Destroying();
                listToRemoveFrom.Remove(aEntity);
            }
        }

        public void Update(double NumSecondsPassed)
        {
            foreach (SequenceEntity aSequenceEntity in sequenceEntityList)
            {
                aSequenceEntity.Update(NumSecondsPassed);
            }

            foreach (Player aPlayer in playerList)
            {
                aPlayer.Update(NumSecondsPassed);
                {
                    int offset;
                    byte[] buffer = levelMap.GetBuffer(out offset);
                    Vector2D newPosition = aPlayer.Position;

                    int xOnMap = ((int)(newPosition.x + .5)) / 16;
                    int yOnMap = ((int)(newPosition.y + .5)) / 16;
                    offset = levelMap.GetBufferOffsetXY(xOnMap, yOnMap);
                    if (aPlayer.hasKey && buffer[offset] == 43)
                    {
                        aPlayer.m_Score++;
                        aPlayer.hasKey = false;
                        aPlayer.entityHolding = null;
                        key.Position = keyStart;
                    }
                }
            }

            RemoveDeadStuff(sequenceEntityList);
        }
    }
}
