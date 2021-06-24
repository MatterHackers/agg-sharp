
using MatterHackers.VectorMath;
using System;

namespace DualContouring
{
	public static class glm
    {
        public static double Sphere(Vector3 worldPosition, Vector3 origin, double radius)
        {
            return (worldPosition - origin).Length - radius;
        }

        public static double Cuboid(Vector3 worldPosition, Vector3 origin, Vector3 halfDimensions)
        {
            Vector3 local_pos = worldPosition - origin;
            Vector3 pos = local_pos;

            Vector3 d = new Vector3(Math.Abs(pos.X), Math.Abs(pos.Y), Math.Abs(pos.Z)) - halfDimensions;
            double m = Math.Max(d.X, Math.Max(d.Y, d.Z));
            return Math.Min(m, (d.Length > 0 ? d : Vector3.Zero).Length);
        }

        public static double FractalNoise(int octaves, double frequency, double lacunarity, double persistence, Vector2 position)
        {
            double SCALE = 1.0f / 128.0f;
            Vector2 p = position * SCALE;
            double noise = 0.0f;

            double amplitude = 1.0f;
            p *= frequency;

            for (int i = 0; i < octaves; i++)
            {
                noise += Perlin.perlin(p.X, p.Y, 0) * amplitude;
                p *= lacunarity;
                amplitude *= persistence;
            }

            // move into [0, 1] range
            return 0.5f + (0.5f * noise);
        }


        public static double Density_Func(Vector3 worldPosition)
        {
            double MAX_HEIGHT = 20.0f;
            double noise = FractalNoise(4, 0.5343f, 2.2324f, 0.68324f, new Vector2(worldPosition.X, worldPosition.Z));
            double terrain = worldPosition.Y - (MAX_HEIGHT * noise);

            double cube = Cuboid(worldPosition, new Vector3(-4.0f, 10.0f, -4.0f), new Vector3(12.0f, 12.0f, 12.0f));
            double sphere = Sphere(worldPosition, new Vector3(15.0f, 2.5f, 1.0f), 16.0f);

            return Math.Max(-cube, Math.Min(sphere, terrain));
        }
    }
}