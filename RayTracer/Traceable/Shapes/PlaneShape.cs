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
using System.Collections;
using System.Collections.Generic;
using System.Text;

using MatterHackers.Agg;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;

namespace MatterHackers.RayTracer
{
    public class PlaneShape : BaseShape
    {
        Plane plane;
        public RGBA_Floats OddColor;

        public PlaneShape(Vector3 planeNormal, double distanceFromOrigin, MaterialAbstract material)
        {
            plane = new Plane(planeNormal, distanceFromOrigin);
            Material = material;
            
        }

        public PlaneShape(Vector3 planeNormal, double distanceFromOrigin, RGBA_Floats color, RGBA_Floats oddcolor, double reflection, double transparency)
        {
            plane.planeNormal = planeNormal;
            plane.distanceToPlaneFromOrigin = distanceFromOrigin;
            //Color = color;
            OddColor = oddcolor;
            //Transparency = transparency;
            //Reflection = reflection;
        }

        public override double GetSurfaceArea()
        {
            return double.PositiveInfinity;
        }

        public override AxisAlignedBoundingBox GetAxisAlignedBoundingBox()
        {
            return new AxisAlignedBoundingBox(Vector3.NegativeInfinity, Vector3.PositiveInfinity);
        }

        public override RGBA_Floats GetColor(IntersectInfo info)
        {
            if (Material.HasTexture)
            {
                Vector3 Position = plane.planeNormal;
                Vector3 vecU = new Vector3(Position.y, Position.z, -Position.x);
                Vector3 vecV = Vector3.Cross(vecU, plane.planeNormal);

                double u = Vector3.Dot(info.hitPosition, vecU);
                double v = Vector3.Dot(info.hitPosition, vecV);
                return Material.GetColor(u, v);
            }
            else
            {
                return Material.GetColor(0, 0);
            }
        }

        public override double GetIntersectCost()
        {
            return 350;
        }

        public override IntersectInfo GetClosestIntersection(Ray ray)
        {
            bool inFront;
            double distanceToHit = plane.GetDistanceToIntersection(ray, out inFront);
            if (distanceToHit > 0)
            {
                IntersectInfo info = new IntersectInfo();
                info.closestHitObject = this;
                info.hitType = IntersectionType.FrontFace;
                info.hitPosition = ray.origin + ray.direction * distanceToHit;
                info.normalAtHit = plane.planeNormal;
                info.distanceToHit = distanceToHit;

                return info;
            }

            return null;
        }

        public override int FindFirstRay(RayBundle rayBundle, int rayIndexToStartCheckingFrom)
        {
            throw new NotImplementedException();
        }

        public override IEnumerable IntersectionIterator(Ray ray)
        {
            throw new NotImplementedException();
        }

        public override string ToString()
        {
            return string.Format("Sphere {0}x+{1}y+{2}z+{3}=0)", plane.planeNormal.x, plane.planeNormal.y, plane.planeNormal.z, plane.distanceToPlaneFromOrigin);
        }
    }
}
