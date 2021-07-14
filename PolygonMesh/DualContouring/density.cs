using MatterHackers.VectorMath;
using System;
using static System.Math;

// A collection of signed distance field functions
namespace DualContouring
{
    public interface ISdf
	{
        double Sdf(Vector3 p);

        AxisAlignedBoundingBox Bounds { get; }
    }

    public class Sphere : ISdf
    {
        public double Radius;

        public double Sdf(Vector3 p)
        {
            return p.Length - Radius;
        }

        public AxisAlignedBoundingBox Bounds => new AxisAlignedBoundingBox(-Radius, -Radius, -Radius, Radius, Radius, Radius);
    }
    public class Box : ISdf
    {
        private Vector3 halfSize;

        public Vector3 Size 
        { 
            get => halfSize * 2;
            set => halfSize = value / 2;
        }

        public double Sdf(Vector3 p)
        {
            Vector3 q = p.Abs() - halfSize;
            return Vector3.ComponentMax(q, 0.0).Length + Min(Max(q.X, Max(q.Y, q.Z)), 0.0);
        }

        public AxisAlignedBoundingBox Bounds => new AxisAlignedBoundingBox(-halfSize, halfSize);
    }
}