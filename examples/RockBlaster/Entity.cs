/*
 * Created by SharpDevelop.
 * User: Lars Brubaker
 * Date: 10/13/2007
 * Time: 12:08 PM
 *
 * To change this template use Tools | Options | Coding | Edit Standard Headers.
 */

using Gaming.Game;
using MatterHackers.Agg;
using MatterHackers.VectorMath;
using System;

namespace RockBlaster
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

		#endregion GameObjectStuff

		private static int s_GameHeight;
		private static int s_GameWidth;

		protected double m_Radius;
		protected double m_Damage = 0;
		protected double m_MaxDamage = 1;

		[GameDataVector2D("Position")]
		protected Vector2 m_Position;

		protected Vector2 m_Velocity;

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

		public Entity(double radius)
		{
			m_Radius = radius;
			m_Velocity = new Vector2(60, 120);
		}

		public Vector2 Position
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

		public Vector2 Velocity
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

		public virtual void Update(double numSecondsPassed)
		{
			m_Position += m_Velocity * numSecondsPassed;
			if (m_Position.x > GameWidth)
			{
				m_Position.x -= GameWidth;
			}
			if (m_Position.x < 0)
			{
				m_Position.x += GameWidth;
			}
			if (m_Position.y > GameHeight)
			{
				m_Position.y -= GameHeight;
			}
			if (m_Position.y < 0)
			{
				m_Position.y += GameHeight;
			}
		}

		public virtual void Draw(Graphics2D destRenderer)
		{
			DoDraw(destRenderer);

			MirrorAsNeeded(destRenderer);
		}

		protected abstract void DoDraw(Graphics2D destRenderer);

		private void MirrorOnY(Graphics2D destRenderer)
		{
			if (Position.y < Radius)
			{
				Vector2 oldPosition = Position;
				oldPosition.y += GameHeight;
				Position = oldPosition;
				this.DoDraw(destRenderer);
			}
			else if (Position.y > GameHeight - Radius)
			{
				Vector2 oldPosition = Position;
				oldPosition.y -= GameHeight;
				Position = oldPosition;
				this.DoDraw(destRenderer);
			}
		}

		public virtual double GiveDamage()
		{
			return 0.0;
		}

		private void MirrorOnX(Graphics2D destRenderer)
		{
			if (Position.x < Radius)
			{
				Vector2 oldPosition = Position;
				oldPosition.x += GameWidth;
				Position = oldPosition;
				DoDraw(destRenderer);
			}
			else if (Position.x > GameWidth - Radius)
			{
				Vector2 oldPosition = Position;
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

		public void MirrorAsNeeded(Graphics2D destRenderer)
		{
			Vector2 oldPosition = Position;
			MirrorOnX(destRenderer);
			MirrorOnY(destRenderer);
			MirrorOnX(destRenderer);
			Position = oldPosition;
		}
	}
}