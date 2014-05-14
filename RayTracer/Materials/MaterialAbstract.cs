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

using MatterHackers.Agg;
using MatterHackers.Agg.Image;

namespace MatterHackers.RayTracer
{
    public abstract class MaterialAbstract
    {
        private double gloss;
        private double transparency;
        private double reflection;
        private double refraction;

        public double Reflection
        {
            get { return reflection; }
            set { reflection = value; }
        }

        public double Refraction
        {
            get { return refraction; }
            set { refraction = value; }
        }

        public double Transparency
        {
            get { return transparency; }
            set { transparency = value; }
        }

        public double Gloss
        {
            get { return gloss; }
            set { gloss = value; }
        }

        public abstract bool HasTexture { get; }
        public abstract RGBA_Floats GetColor(double u, double v);

        public MaterialAbstract()
        {
            gloss = 2; // set a realistic value by default
            transparency = 0; // opaque by default
            reflection = 0; // no reflection by default
            refraction = 0.50; // default refraction for now
        }

        /// <summary>
        /// wraps any value up in the inteval [-1,1] in a rotational manner
        /// e.g. 1.7 -> -0.3
        /// e.g. -1.1 -> 0.9
        /// e.g. -2.3 -> -0.3
        /// </summary>
        /// <param name="t"></param>
        /// <returns></returns>
        protected double WrapUp(double t)
        {
            t = t % 2.0;
            if (t < -1) t = t + 2.0;
            if (t >= 1) t -= 2.0;
            return t;
        }
    }
}
