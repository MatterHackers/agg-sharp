/*
 * Created by SharpDevelop.
 * User: Lars Brubaker
 * Date: 10/13/2007
 * Time: 12:08 PM
 * 
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using System;
using System.Runtime.Serialization;

using AGG;
using AGG.VertexSource;
using AGG.Image;

using Gaming.Math;
using Gaming.Game;

namespace CTFA
{
	/// <summary>
	/// Description of Entity.
	/// </summary>
    public abstract class Entity : GameObject
	{
        #region GameObjectStuff
	    public Entity()
	    {
	    }
	    public static new GameObject Load(String PathName)
	    {
	        return GameObject.Load(PathName);
	    }
        #endregion

		private static int s_GameHeight;
		private static int s_GameWidth;

		protected double m_Radius;
		protected double m_Damage = 0;
        protected double m_MaxDamage = 1;

        protected ImageBuffer levelMap;

        [GameDataVector2D("Position")]
		protected Vector2D m_Position;
		protected Vector2D m_Velocity;

        public bool hasKey = false;
		
		public double MaxDamage
		{
			get 
			{
				return m_MaxDamage;
			}
		}
		
		public double Damage
		{
			get
			{
				return m_Damage;
			}
			set
			{
				m_Damage = value;
			}
		}

		public static int GameWidth
		{
			get
			{
				return s_GameWidth;
			}
			set
			{
				s_GameWidth = value;
			}
		}
				
		public static int GameHeight
		{
			get
			{
				return s_GameHeight;
			}
			set
			{
				s_GameHeight = value;
			}
		}

        public Entity(double radius, Playfield in_playfield)
		{
            levelMap = in_playfield.LevelMap;
			m_Radius = radius;
			m_Velocity = new Vector2D(60,120);
		}

        public Vector2D Position
        {
            get
            {
                return m_Position;
            }
            set
            {
                m_Position = value;
            }
        }

        public Vector2D Velocity
        {
            get
            {
                return m_Velocity;
            }
            set
            {
                m_Velocity = value;
            }
        }

        public double Radius
        {
        	get
        	{
        		return m_Radius;
        	}
        }

        private bool IsCollision(Vector2D newPosition)
        {
            int offset;
            byte[] buffer = levelMap.GetBuffer(out offset);

            int xOnMap = ((int)(newPosition.x + .5)) / 16;
            int yOnMap = ((int)(newPosition.y + .5)) / 16;
            offset = levelMap.GetBufferOffsetXY(xOnMap, yOnMap);
            if (buffer[offset] == 0
                || !hasKey && buffer[offset] == 1)
            {
                return true;
            }

            return false;
        }

		public virtual void Update(double numSecondsPassed)
		{
            Vector2D newPosition = m_Position + m_Velocity * numSecondsPassed;

            if (newPosition.x < GameWidth-1 && newPosition.x > 0
                && newPosition.y < GameHeight-1 && newPosition.y > 0)
            {
                if (IsCollision(newPosition))
                {
                    // first try to negate the x and check again
                    m_Velocity.x = -m_Velocity.x;
                    newPosition = m_Position + m_Velocity * numSecondsPassed;
                    if (IsCollision(newPosition))
                    {
                        // there is still a collision, negate y and try again
                        m_Velocity.x = -m_Velocity.x; // first put x back
                        m_Velocity.y = -m_Velocity.y;
                        newPosition = m_Position + m_Velocity * numSecondsPassed;
                        if (IsCollision(newPosition))
                        {
                            // still a collision try negating both
                            m_Velocity.x = -m_Velocity.x; // y is negating just need x
                            newPosition = m_Position + m_Velocity * numSecondsPassed;
                            if (IsCollision(newPosition))
                            {
                                // still a collision, don't let the player go here
                                newPosition = m_Position;
                                m_Velocity.Zero();
                            }
                        }
                    }
                }
            }

            if (newPosition.x > GameWidth)
			{
                m_Velocity.x = -m_Velocity.x;
                newPosition.x = GameWidth;
			}
            if (newPosition.x < 0)
			{
                m_Velocity.x = -m_Velocity.x;
                newPosition.x = 0;
			}
            if (newPosition.y > GameHeight)
			{
                m_Velocity.y = -m_Velocity.y;
                newPosition.y = GameHeight;
			}
            if (newPosition.y < 0)
			{
                m_Velocity.y = -m_Velocity.y;
                newPosition.y = 0;
			}

            m_Position = newPosition;
        }

		public virtual void Draw(RendererBase destRenderer)
        {
			DoDraw(destRenderer);
            
            MirrorAsNeeded(destRenderer);
        }

        protected abstract void DoDraw(RendererBase destRenderer);

        private void MirrorOnY(RendererBase destRenderer)
		{
            if(Position.y < Radius)
            {
            	Vector2D oldPosition = Position;
            	oldPosition.y += GameHeight;
            	Position = oldPosition;
            	this.DoDraw(destRenderer);
            }
            else if (Position.y > GameHeight - Radius)
            {
            	Vector2D oldPosition = Position;
                oldPosition.y -= GameHeight;
            	Position = oldPosition;
            	this.DoDraw(destRenderer);
            }
		}
		
		public virtual double GiveDamage()
		{
			return 0.0;
		}

        private void MirrorOnX(RendererBase destRenderer)
		{
            if(Position.x < Radius)
            {
            	Vector2D oldPosition = Position;
            	oldPosition.x += GameWidth;
            	Position = oldPosition;
            	DoDraw(destRenderer);
            }
            else if (Position.x > GameWidth - Radius)
            {
            	Vector2D oldPosition = Position;
            	oldPosition.x -= GameWidth;
            	Position = oldPosition;
            	DoDraw(destRenderer);
            }			
		}

        public virtual void TakeDamage(double DamageToTake, Player playerThatDeliveredDamage)
		{
			Damage = Damage + DamageToTake;
		}

        public virtual void Destroying()
		{
			
		}

        public void MirrorAsNeeded(RendererBase destRenderer)
		{
	    	Vector2D oldPosition = Position;
            MirrorOnX(destRenderer);
            MirrorOnY(destRenderer);
            MirrorOnX(destRenderer);
	    	Position = oldPosition;
		}
	}
}
