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
    public class TextureMaterial : MaterialAbstract
    {
        public ImageBuffer Texture;
        public Graphics2D textureGraphics2D;
        public double Density;

        public TextureMaterial(ImageBuffer texture, double reflection, double transparency, double gloss, double density)
        {
            this.Reflection = reflection;
            this.Transparency = transparency;
            this.Gloss = gloss;
            this.Density = density;
            this.Texture = texture;
        }

        public override bool HasTexture
        {
            get { return true; }
        }

        public override RGBA_Floats GetColor(double u, double v)
        {
            // map u, v to [0,2];
            u = WrapUp(u * Density) + 1;
            v = WrapUp(v * Density) + 1;

            // calculate exact position in texture
            double nu1 = u * Texture.Width / 2;
            double nv1 = v * Texture.Width / 2;

            // calculate fractions
            double fu = nu1 - Math.Floor(nu1);
            double fv = nv1 - Math.Floor(nv1);
            double w1 = (1 - fu) * (1 - fv);
            double w2 = fu * (1 - fv);
            double w3 = (1 - fu) * fv;
            double w4 = fu * fv;

            int nu2 = (int)(Math.Floor(nu1)) % Texture.Width;
            int nv2 = (int)(Math.Floor(nv1)) % Texture.Height;
            int nu3 = (int)(Math.Floor(nu1+1)) % Texture.Width;
            int nv3 = (int)(Math.Floor(nv1+1)) % Texture.Height;

            RGBA_Floats c1 = Texture.GetPixel(nu2, nv2).GetAsRGBA_Floats();
            RGBA_Floats c2 = Texture.GetPixel(nu3, nv2).GetAsRGBA_Floats();
            RGBA_Floats c3 = Texture.GetPixel(nu2, nv3).GetAsRGBA_Floats();
            RGBA_Floats c4 = Texture.GetPixel(nu3, nv3).GetAsRGBA_Floats();
            return c1 * w1 + c2 * w2 + c3 * w3 + c4 * w4;
        }
    }
}
