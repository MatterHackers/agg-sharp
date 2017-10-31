using Gaming.Game;
using MatterHackers.Agg;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

namespace RockBlaster
{
	public class BulletStyleSheet : GameObject
	{
		[GameDataNumberAttribute("TurnRate")]
		public double TurnRate = 4;

		[GameDataNumberAttribute("Ship Thrust")]
		public double ThrustAcceleration = 10;

		[GameDataNumberAttribute("Friction")]
		public double Friction = .99;

		[GameDataNumberAttribute("DamageOnCollide")]
		public double DamageOnCollide = 10;

		[GameDataNumberAttribute("NumSecondsToLive")]
		public double NumSecondsToLive = 2;
	}

	/// <summary>
	/// Description of Player.
	/// </summary>
	public class Bullet : Entity
	{
		//[GameDataFromAssetTree("StyleSheet")];
		protected BulletStyleSheet m_BulletStyleSheet;

		protected Ellipse ellipseShape;
		private double numSocendsUpdated;

		[GameDataNumberAttribute("Rotation")] // This is for save game
		protected double m_Rotation;

		public Bullet(Vector2 position, Vector2 velocity)
			: base(3)
		{
			Position = position;
			ellipseShape = new Ellipse(0, 0, 3, 3);
			m_Velocity = velocity;

			m_BulletStyleSheet = new BulletStyleSheet();
		}

		protected override void DoDraw(Graphics2D destRenderer)
		{
			Affine Final = Affine.NewIdentity();
			Final *= Affine.NewRotation(m_Rotation);
			Final *= Affine.NewTranslation(m_Position.X, m_Position.Y);
			var TransformedShip = new VertexSourceApplyTransform(ellipseShape, Final);
			destRenderer.Render(TransformedShip, new Color(.9, .4, .2, 1));
		}

		public override void Update(double numSecondsPassed)
		{
			base.Update(numSecondsPassed);
			numSocendsUpdated += numSecondsPassed;
			if (numSocendsUpdated > m_BulletStyleSheet.NumSecondsToLive)
			{
				TakeDamage(MaxDamage, null);
			}
		}

		public override double GiveDamage()
		{
			TakeDamage(MaxDamage, null);
			return m_BulletStyleSheet.DamageOnCollide;
		}
	}
}