/*
 * Created by SharpDevelop.
 * User: Lars Brubaker
 * Date: 10/13/2007
 * Time: 12:07 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */
using System;
using System.Runtime.Serialization;
using System.Collections.Generic;

using AGG;
using AGG.VertexSource;
using AGG.Transform;
using AGG.UI;
using AGG.Image;

using Gaming.Math;
using Gaming.Game;
using Gaming.Audio;
using Gaming.Graphics;

namespace CTFA
{
    public class PlayerStyleSheet : GameObject
	{
        #region GameObjectStuff
	    public PlayerStyleSheet()
	    {
	    }
	    public static new GameObject Load(String PathName)
	    {
	        return GameObject.Load(PathName);
	    }
        #endregion

		[GameDataNumberAttribute("ShipThrust")]
		public double ThrustAcceleration = 1500;
        [GameDataNumberAttribute("Friction")]
        public double Friction = .85;
        [GameDataNumberAttribute("DamageOnCollide")]
        public double DamageOnCollide = 100;
        
        [GameData("FireSound")]
        public AssetReference<Sound> FireSoundReference = new AssetReference<Sound>("PlayerSmallShot");
    }

	/// <summary>
	/// Description of Player.
	/// </summary>
    public class Player : Entity
	{
        #region GameObjectStuff
	    public Player()
	    {
	    }
	    public static new GameObject Load(String PathName)
	    {
	        return GameObject.Load(PathName);
	    }
        #endregion

        [GameData("StyleSheet")]
        public AssetReference<PlayerStyleSheet> m_PlayerStyleSheetReference = new AssetReference<PlayerStyleSheet>();

        [GameData("IntArrayTest")]
        public int[] m_IntArray = new int[] { 0, 1, 23, 234 };

        private static int[] s_DefaultList = new int[] { 0, 1, 23, 234 };
        [GameDataList("IntListTest")]
        public List<int> m_IntList = new List<int>(s_DefaultList);
        
        Vector2D moveDir;
        Vector2D acceleration;

        public int m_Score;
        Keys leftKey;
        Keys downKey;
        Keys rightKey;
        Keys upKey;
        public int playerIndex;
        Playfield playfieldOn;
        public Entity entityHolding;

        public Player(int in_playerIndex, Keys in_leftKey, Keys in_rightKey,
            Keys in_downKey, Keys in_upKey, Playfield in_playfield)
			: base(26, in_playfield)
		{
            playfieldOn = in_playfield;
            playerIndex = in_playerIndex;
            leftKey = in_leftKey;
            downKey = in_rightKey;
            rightKey = in_downKey;
            upKey = in_upKey;

            int playerSequenceIndex = GetPlayerIndex();
            m_Radius = 8;
			Position = new Vector2D(GameWidth/2, GameHeight/2);
			m_Velocity.Zero();
        }

        int GetPlayerIndex()
        {
            return playerIndex;
        }

        protected override void DoDraw(RendererBase destRenderer)
		{
            int playerSequenceIndex = GetPlayerIndex();
            ImageBuffer playerImage = ((ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "Players")).GetImageByIndex(playerSequenceIndex);
            destRenderer.Render(playerImage, m_Position.x, m_Position.y);
        }

        public void DrawScore(RendererBase destRenderer)
        {
            int playerSequenceIndex = GetPlayerIndex();
            ImageSequence scoreSequence = (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "ScoreNumbers");
            string score = m_Score.ToString();
            int x = 43;
            int y = 577;
            switch (playerSequenceIndex)
            {
                case 0:
                    break;

                case 1:
                    x = 700;
                    break;

                case 2:
                    x = 45;
                    y = 5;
                    break;

                case 3:
                    x = 700;
                    y = 5;
                    break;

                default:
                    break;
            }

            for (int i = 0; i < score.Length; i++)
            {
                int digit = (int)(score[i] - '0');
                ImageBuffer numberImage = scoreSequence.GetImageByIndex(digit);
                destRenderer.Render(numberImage, x, y);
                x += numberImage.Width();
            }
        }

        public void KeyDown(KeyEventArgs keyEvent)
		{
            if (keyEvent.KeyCode == leftKey)
            {
                moveDir.x = -1;
            }
            else
            {
                if (keyEvent.KeyCode == rightKey)
                {
                    moveDir.x = 1;
                }
            }
            if (keyEvent.KeyCode == downKey)
            {
                moveDir.y = -1;
            }
            else
            {
                if (keyEvent.KeyCode == upKey)
                {
                    moveDir.y = 1;
                }
            }
        }

        public void KeyUp(KeyEventArgs keyEvent)
        {
            if (keyEvent.KeyCode == leftKey ||
                keyEvent.KeyCode == rightKey)
            {
                moveDir.x = 0;
            }

            if (keyEvent.KeyCode == downKey || keyEvent.KeyCode == upKey)
            {
                moveDir.y = 0;
            }
        }

        bool IsTouching(Entity otherEntity)
        {
            Vector2D delta = Position - otherEntity.Position;
            if (delta.GetLength() < 16)
            {
                return true;
            }

            return false;
        }
		
		public override void Update(double numSecondsPassed)
		{
            acceleration = moveDir;

            if (acceleration.x != 0 || acceleration.y != 0)
            {
                acceleration.Normalize();
                acceleration *= m_PlayerStyleSheetReference.Instance.ThrustAcceleration;
            }

			m_Velocity += acceleration * numSecondsPassed;
            m_Velocity *= m_PlayerStyleSheetReference.Instance.Friction;

            if (IsTouching(playfieldOn.key))
            {
                entityHolding = playfieldOn.key;
                hasKey = true;
            }

            if (entityHolding != null)
            {
                Vector2D newPosition = Position;
                newPosition.x += 8;
                entityHolding.Position = newPosition;
            }

			base.Update(numSecondsPassed);
		}

		public void Respawn()
		{
			Random rand = new Random();
			Position = new Vector2D(rand.NextDouble() * GameWidth, rand.NextDouble() * GameHeight);
            m_Velocity.Zero();			
		}

		public override double GiveDamage()
		{
			Respawn();
			return m_PlayerStyleSheetReference.Instance.DamageOnCollide;
		}
    }
}
