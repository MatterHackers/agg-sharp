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

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

using Gaming.Math;
using Gaming.Game;
using Gaming.Audio;
using Gaming.Graphics;

namespace RockBlaster
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

        [GameDataNumberAttribute("TurnRate")]
		public double TurnRate = 6;
		[GameDataNumberAttribute("ShipThrust")]
		public double ThrustAcceleration = 600;
        [GameDataNumberAttribute("Friction")]
        public double Friction = .99;
        [GameDataNumberAttribute("DistanceToFrontOfShip")]
        public double DistanceToFrontOfShip = 10;
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
        
        [GameDataNumberAttribute("Rotation")] // This is for save game
        public double m_Rotation = Math.PI/2;

        [GameDataList("BulletList")]
        public List<Entity> m_BulletList = new List<Entity>();
        
        Vector2 m_Acceleration;

        public Player m_LastPlayerToShot;
        public int m_Score;
        int m_JoyStickIndex;
		bool m_TurningLeft;
		bool m_TurningRight;
		bool m_Thrusting;
        bool m_FiredBullet;
        bool m_FireKeyDown;
        Keys leftKey;
        Keys rightKey;
        Keys thrustKey;
        Keys fireKey;
        int playerIndex;

        public Player(int in_playerIndex, int joyStickIndex, Keys in_leftKey, Keys in_rightKey,
            Keys in_thrustKey, Keys in_fireKey)
			: base(26)
		{
            playerIndex = in_playerIndex;
            leftKey = in_leftKey;
            rightKey = in_rightKey;
            thrustKey = in_thrustKey;
            fireKey = in_fireKey;

            int playerSequenceIndex = GetPlayerIndex();
            GameImageSequence playerShip = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "Player" + (playerSequenceIndex + 1).ToString() + "Ship");
            m_Radius = playerShip.GetImageByIndex(0).Width/2;
            m_JoyStickIndex = joyStickIndex;
			Position = new Vector2(GameWidth/2, GameHeight/2);
			m_Velocity = Vector2.Zero;
        }

        int GetPlayerIndex()
        {
            return playerIndex;
        }

        protected override void DoDraw(Graphics2D destRenderer)
		{
            int playerSequenceIndex = GetPlayerIndex();
            GameImageSequence playerShip = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "Player" + (playerSequenceIndex + 1).ToString() + "Ship");
            destRenderer.Render(playerShip.GetImageByRatio(m_Rotation / (2*Math.PI)), m_Position.x, m_Position.y);
        }

        internal void DrawBullets(Graphics2D destRenderer)
        {
            int playerSequenceIndex = GetPlayerIndex();
            GameImageSequence bulletImage = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "Player" + (playerSequenceIndex + 1).ToString() + "Bullet");
            foreach (Bullet aBullet in m_BulletList)
            {
                destRenderer.Render(bulletImage.GetImageByIndex(0), aBullet.Position.x, aBullet.Position.y, aBullet.Velocity.GetAngle0To2PI(), 1, 1);
            }
        }

        public void DrawScore(Graphics2D destRenderer)
        {
            int playerSequenceIndex = GetPlayerIndex();
            GameImageSequence scoreSequence = (GameImageSequence)DataAssetCache.Instance.GetAsset(typeof(GameImageSequence), "ScoreNumbers");
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
                x += numberImage.Width;
            }
        }

        public void KeyDown(KeyEventArgs keyEvent)
		{
			if(keyEvent.KeyCode == leftKey)
			{
				m_TurningLeft = true;
			}
			if(keyEvent.KeyCode == rightKey)
			{
				m_TurningRight = true;
			}
			if(keyEvent.KeyCode == thrustKey)
			{
				m_Thrusting = true;
			}
			if(keyEvent.KeyCode == fireKey)
			{
                m_FireKeyDown = true;
			}
		}

        public void KeyUp(KeyEventArgs keyEvent)
		{
			if(keyEvent.KeyCode == leftKey)
			{
				m_TurningLeft = false;
			}
			if(keyEvent.KeyCode == rightKey)
			{
				m_TurningRight = false;
			}
			if(keyEvent.KeyCode == thrustKey)
			{
				m_Thrusting = false;
			}
            if (keyEvent.KeyCode == fireKey)
            {
                m_FireKeyDown = false;
            }
		}
		
		public override void Update(double numSecondsPassed)
		{
#if false
            Joystick joyStick = new Joystick(m_JoyStickIndex);
            joyStick.Read();
            bool joyThrusting = false;
            if (m_JoyStickIndex != -1)
            {
                Vector2D joyDir;
                joyDir.x = joyStick.xAxis1; joyDir.y = joyStick.yAxis1;
                if (joyDir.GetLength() > .2)
                {
                    Vector2D shipDir;
                    shipDir.x = Math.Cos(m_Rotation); shipDir.y = Math.Sin(m_Rotation);
                    double deltaAngle = shipDir.GetDeltaAngle(joyDir);
                    double maxAnglePerUpdate = Math.PI / 22;
                    if (deltaAngle > maxAnglePerUpdate) deltaAngle = maxAnglePerUpdate;
                    if (deltaAngle < -maxAnglePerUpdate) deltaAngle = -maxAnglePerUpdate;
                    m_Rotation += deltaAngle;
                    double stickAngle = Math.Atan2(joyDir.y, joyDir.x);
                    if (joyDir.GetLength() > .8)
                    {
                        joyThrusting = true;
                    }
                }
            }
#endif
            if (m_Thrusting)// || joyThrusting)
            {
                m_Acceleration = new Vector2(Math.Cos(m_Rotation) * m_PlayerStyleSheetReference.Instance.ThrustAcceleration, Math.Sin(m_Rotation) * m_PlayerStyleSheetReference.Instance.ThrustAcceleration);
            }
            else
            {
                m_Acceleration = Vector2.Zero;
            }
            if (m_TurningLeft)
            {
                m_Rotation += m_PlayerStyleSheetReference.Instance.TurnRate * numSecondsPassed;
            }
            if (m_TurningRight)
            {
                m_Rotation -= m_PlayerStyleSheetReference.Instance.TurnRate * numSecondsPassed;
			}
            if (m_FireKeyDown)// || joyStick.button1)
            {
                if (!m_FiredBullet)
                {
                    double bulletVelocity = 320;
                    // WIP: have a weapon and tell it to fire.
                    // set something to fire down
                    Vector2 DirectionVector = new Vector2(Math.Cos(m_Rotation), Math.Sin(m_Rotation));
                    m_BulletList.Add(new Bullet(Position + DirectionVector * m_PlayerStyleSheetReference.Instance.DistanceToFrontOfShip,
                        m_Velocity + DirectionVector * bulletVelocity));
                    m_FiredBullet = true;
                    m_PlayerStyleSheetReference.Instance.FireSoundReference.Instance.PlayAnAvailableCopy();
                }
            }
            else
            {
                m_FiredBullet = false;
            }

            m_Rotation = MathHelper.Range0ToTau(m_Rotation);
			
			m_Velocity += m_Acceleration * numSecondsPassed;
            m_Velocity *= m_PlayerStyleSheetReference.Instance.Friction;

            foreach (Bullet aBullet in m_BulletList)
            {
                aBullet.Update(numSecondsPassed);
            }

			base.Update(numSecondsPassed);
		}

		public void Respawn()
		{
            m_LastPlayerToShot = null;
			Random rand = new Random();
			Position = new Vector2(rand.NextDouble() * GameWidth, rand.NextDouble() * GameHeight);
            m_Velocity = Vector2.Zero;			
		}

		public override double GiveDamage()
		{
			// The player just hit something.
            if (m_LastPlayerToShot != null)
            {
                m_LastPlayerToShot.m_Score += 1;
            }
			Respawn();
			return m_PlayerStyleSheetReference.Instance.DamageOnCollide;
		}
    }
}
