// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
using System;
using System.Collections.Generic;
using System.Text;

namespace RayTracer
{
    /// <summary>
    /// a sphere is one of the most basic shapes you will find in any raytracer application.
    /// why? simply because it is relatively easy and quick to determine an intersection between a 
    /// line (ray) and a sphere.
    /// Additionally it is ideal to try out special effects like reflection and refraction on spheres.
    /// </summary>
    public class SphereShape : BaseShape
    {
        public double R;
        public SphereShape(Vector pos, double r, IMaterial material)
        {
            R = r;
            Position = pos;
            Material = material;
        }

        #region IShape Members

        /// <summary>
        /// This implementation of intersect uses the fastest ray-sphere intersection algorithm I could find
        /// on the internet.
        /// </summary>
        /// <param name="ray"></param>
        /// <returns></returns>
        public override IntersectInfo Intersect(Ray ray)
        {
            IntersectInfo info = new IntersectInfo();
            info.Element = this;

            Vector dst = ray.Position - this.Position;
            double B = dst.Dot(ray.Direction);
            double C = dst.Dot(dst) - (R * R);
            double D = B * B - C;

            if (D > 0) // yes, that's it, we found the intersection!
            {
                info.IsHit = true;
                info.Distance = -B - (double)Math.Sqrt(D);
                info.Position = ray.Position + ray.Direction * info.Distance;
                info.Normal = (info.Position - Position).Normalize();

                if (Material.HasTexture)
                {
                    Vector vn = new Vector(0, 1, 0).Normalize(); // north pole / up
                    Vector ve = new Vector(0, 0, 1).Normalize(); // equator / sphere orientation
                    Vector vp = (info.Position - Position).Normalize() ; //points from center of sphere to intersection 
                    
                    double phi = Math.Acos(-vp.Dot(vn));
                    double v = (phi*2 / Math.PI)-1;

                    double sinphi = ve.Dot(vp) / Math.Sin(phi);
                    sinphi = sinphi < -1 ? -1 : sinphi > 1 ? 1 : sinphi;
                    double theta = Math.Acos(sinphi)*2 / Math.PI;

                    double u;

                    if (vn.Cross(ve).Dot(vp) > 0)
                        u = theta;
                    else
                        u = 1 - theta;

                    // alternative but worse implementation
                    //double u = Math.Atan2(vp.x, vp.z);
                    //double v = Math.Acos(vp.y);
                    info.Color = this.Material.GetColor(u, v);
                }
                else
                {
                    // skip uv calculation, just get the color
                    info.Color = this.Material.GetColor(0, 0);
                }
            }
            else
            {
                info.IsHit = false;
            }
            return info;
        }
        #endregion

        public override string ToString()
        {
            return string.Format("Sphere ({0},{1},{2}) Radius: {3}", Position.x, Position.y, Position.z, R);
        }

    }
}
