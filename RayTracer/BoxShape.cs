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
    public class BoxShape : BaseShape
    {
        public Vector UpperLeftFront;
        public Vector LowerRightBack;

        public BoxShape(Vector UpperLeftFront, Vector LowerRightBack, IMaterial material)
        {
            this.UpperLeftFront = UpperLeftFront;
            this.LowerRightBack = LowerRightBack;
            Position = (this.UpperLeftFront + this.LowerRightBack) / 2;
            Material = material;
            
        }

        public override IntersectInfo Intersect(Ray ray)
        {
            IntersectInfo best = new IntersectInfo();
            best.Distance = double.MaxValue;
            IntersectInfo info = null;

            //check each side of the box
            // this is quite a crude implementation and can be optimized

            Vector P1 = null;
            Vector P2 = null;
            Vector P3 = null;
            if (ray.Direction.z > 0)
            {
                //front
                P1 = UpperLeftFront;
                P2 = new Vector(LowerRightBack.x, LowerRightBack.y, P1.z);
                P3 = new Vector(P1.x, P2.y, P1.z);
                info = IntersectSlab(ray, P1, P2, P3);
                if (info.IsHit && info.Distance < best.Distance)
                    best = info;
            }
            else
            {
                //backside
                P1 = new Vector(UpperLeftFront.x, UpperLeftFront.y, LowerRightBack.z);
                P2 = LowerRightBack;
                P3 = new Vector(P1.x, P2.y, P1.z);
                info = IntersectSlab(ray, P1, P2, P3);
                if (info.IsHit && info.Distance < best.Distance)
                    best = info;
            }

            if (ray.Direction.x < 0)
            {
                //right side
                P1 = new Vector(LowerRightBack.x, UpperLeftFront.y, UpperLeftFront.z);
                P2 = LowerRightBack;
                P3 = new Vector(P1.x, P2.y, P1.z);
                info = IntersectSlab(ray, P1, P2, P3);
                if (info.IsHit && info.Distance < best.Distance)
                    best = info;
            }
            else
            {
                //left side
                P1 = UpperLeftFront;
                P2 = new Vector(UpperLeftFront.x, LowerRightBack.y, LowerRightBack.z);
                P3 = new Vector(P1.x, P2.y, P1.z);
                info = IntersectSlab(ray, P1, P2, P3);
                if (info.IsHit && info.Distance < best.Distance)
                    best = info;
            }

            if (ray.Direction.y < 0)
            {
                //top side
                P1 = UpperLeftFront;
                P2 = new Vector(LowerRightBack.x, UpperLeftFront.y, LowerRightBack.z);
                P3 = new Vector(P2.x, P1.y, P1.z);
                info = IntersectSlab(ray, P1, P2, P3);
                if (info.IsHit && info.Distance < best.Distance)
                    best = info;
            }
            else
            {
                //bottom side
                P1 = new Vector(UpperLeftFront.x, LowerRightBack.y, UpperLeftFront.z);
                P2 = LowerRightBack;
                P3 = new Vector(P2.x, P1.y, P1.z);
                info = IntersectSlab(ray, P1, P2, P3);
                if (info.IsHit && info.Distance < best.Distance)
                    best = info;
            }
            return best;

        }

        private IntersectInfo IntersectSlab(Ray ray, Vector P1, Vector P2, Vector P3)
        {
            Vector N = (P1 - P3).Cross(P2 - P3).Normalize();
            double d = N.Dot(P1);
            IntersectInfo info = new IntersectInfo();
            double Vd = N.Dot(ray.Direction);
            if (Vd == 0) return info; // no intersection

            double t = -(N.Dot(ray.Position) - d) / Vd;

            if (t <= 0) return info;
            Vector hit = ray.Position + ray.Direction * t;

            if ((hit.x < P1.x || hit.x > P2.x) && (P1.x != P2.x)) return info;
            if ((hit.y < P3.y || hit.y > P1.y) && (P1.y != P2.y)) return info;
            //if ((hit.z < P1.z || hit.z > P3.z) && (P1.z != P3.z)) return info;
            if ((hit.z < P1.z || hit.z > P2.z) && (P1.z != P2.z)) return info;


            info.Element = this;
            info.IsHit = true;
            info.Position = hit;
            info.Normal = N;// *-1;
            info.Distance = t;

            if (Material.HasTexture)
            {
                //Vector vecU = new Vector(hit.y - Position.y, hit.z - Position.z, Position.x-hit.x);
                Vector vecU = new Vector((P1.y + P2.y) / 2 - Position.y, (P1.z + P2.z) / 2 - Position.z, Position.x - (P1.x + P2.x) / 2).Normalize();
                Vector vecV = vecU.Cross((P1+P2)/2 - Position).Normalize();

                double u = info.Position.Dot(vecU);
                double v = info.Position.Dot(vecV);
                info.Color = Material.GetColor(u, v);
            }
            else
                info.Color = Material.GetColor(0, 0);

            return info;
        }

        public override string ToString()
        {
            return string.Format("Box ({0},{1},{2})-({3},{4},{5})", UpperLeftFront.x, UpperLeftFront.y, UpperLeftFront.z, LowerRightBack.x, LowerRightBack.y, LowerRightBack.z);
        }
    }
}
