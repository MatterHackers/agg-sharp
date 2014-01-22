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
    /// element in a scene
    /// </summary>
    public interface IShape
    {
        /// <summary>
        /// indicates the position of the element
        /// </summary>
        Vector Position { get; set; }

        /// <summary>
        /// specifies the ambient and diffuse color of the element
        /// </summary>
        IMaterial Material { get; set; }

        /// <summary>
        /// this method is to be implemented by each element seperately. This is the core
        /// function of each element, to determine the intersection with a ray.
        /// </summary>
        /// <param name="ray">the ray that intersects with the element</param>
        /// <returns></returns>
        IntersectInfo Intersect(Ray ray);
    }

    public class Shapes : List<IShape>
    {
    }


    public abstract class BaseShape : IShape
    {
        #region IShape Members
        private Vector position;
        private IMaterial material;

        public IMaterial Material
        {
            get { return material; }
            set { material = value; }
        }

        public Vector Position
        {
            get { return position; }
            set { position = value; }
        }

        public BaseShape()
        {
            position = new Vector(0,0,0);
            Material = new SolidMaterial(new Color(1, 0, 1), 0, 0, 0);
        }

        public abstract IntersectInfo Intersect(Ray ray);
        #endregion
    }
}
