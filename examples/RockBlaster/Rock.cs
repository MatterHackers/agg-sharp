using System;
using System.Collections.Generic;

using MatterHackers.Agg;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;

using Gaming.Game;
using Gaming.Math;
using Gaming.Graphics;
using Gaming.Audio;

namespace RockBlaster
{
    public class Rock : Entity
    {
        public static double MinSplitRadius = 15;
        double playRatio;
        public double scaleRatio;
        double baseDamagePerPixelWide;

        List<Entity> m_RockList;

        Player m_LastPlayerThatDeliveredDamage;
        Random rand = new Random();
        double maxVelocity = 300;

        public Rock(List<Entity> rockList, double in_baseDamagePerPixelWide)
            : this(rockList, 1, in_baseDamagePerPixelWide)
        {
        }

        public Rock(List<Entity> rockList, double inScaleRatio, double in_baseDamagePerPixelWide)
            : base(10)
        {
            baseDamagePerPixelWide = in_baseDamagePerPixelWide;
            scaleRatio = inScaleRatio;
            m_Radius = GetImageSequence().GetImageByIndex(0).Width * scaleRatio * .5;
            m_RockList = rockList;
            m_MaxDamage = 10;
            m_Position = new Vector2(0, rand.NextDouble() * 600);
            m_Velocity = new Vector2(rand.NextDouble() * maxVelocity - maxVelocity / 2, rand.NextDouble() * maxVelocity - maxVelocity/2);
        }

        ImageSequence GetImageSequence()
        {
            return (ImageSequence)DataAssetCache.Instance.GetAsset(typeof(ImageSequence), "Asteroid 3");
        }

        protected override void DoDraw(Graphics2D destRenderer)
        {
            ImageSequence rockShip = GetImageSequence();
            destRenderer.Render(rockShip.GetImageByRatio(playRatio), m_Position.x, m_Position.y, 0, scaleRatio, scaleRatio);
        }

        public override void Update(double numSecondsPassed)
        {
            ImageSequence rockShip = GetImageSequence();
            playRatio += (numSecondsPassed * rockShip.FramePerSecond) / rockShip.NumFrames;
            if (playRatio > 1) playRatio = 0;
            base.Update(numSecondsPassed);
        }

        public override void TakeDamage(double DamageToTake, Player playerThatDeliveredDamage)
        {
            m_LastPlayerThatDeliveredDamage = playerThatDeliveredDamage;
            ((Sound)DataAssetCache.Instance.GetAsset(typeof(Sound), "AsteroidHit")).PlayAnAvailableCopy();
            base.TakeDamage(DamageToTake, playerThatDeliveredDamage);
        }

        public override void Destroying()
        {
            ((Sound)DataAssetCache.Instance.GetAsset(typeof(Sound), "AsteroidExplosion")).PlayAnAvailableCopy();
            if (Radius > MinSplitRadius)
            {
                m_LastPlayerThatDeliveredDamage.m_Score += (int)(baseDamagePerPixelWide * this.Radius);

                Random rand = new Random();
                Rock newRock = new Rock(m_RockList, scaleRatio * .5, baseDamagePerPixelWide);
                newRock.Position = this.Position;
                newRock.m_MaxDamage = this.MaxDamage / 2;
                m_RockList.Add(newRock);
                newRock = new Rock(m_RockList, scaleRatio * .5, baseDamagePerPixelWide);
                newRock.Position = this.Position;
                newRock.m_MaxDamage = this.MaxDamage / 2;
                newRock.m_Velocity = new Vector2(rand.NextDouble() * maxVelocity - maxVelocity / 2, rand.NextDouble() * maxVelocity - maxVelocity/2);
                m_RockList.Add(newRock);
            }
        }
    }
}
