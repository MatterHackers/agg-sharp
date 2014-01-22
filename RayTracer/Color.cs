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

using AGG;
using AGG.Image;

namespace RayTracer
{
	/// <summary>
	/// Color class.
	/// </summary>
	public class Color
	{
        /// <summary>
        /// the parts of the Color
        /// </summary>
        public double Red;
        public double Green;
        public double Blue;
        
        public Color()
		{
			// reset Color to black
            Red = 0;
            Green = 0;
            Blue = 0;
		}

		public Color(double r, double g, double b)
		{
			// copy to members
			Red=r;
			Green=g;
			Blue=b;
		}

		public Color(Color col)
		{
			// copy over members
			Red=col.Red;
			Green=col.Green;
			Blue=col.Blue;
		}


		/// Arithmetic operators
		
		static public Color operator + (Color c1,Color c2)
		{
			Color result=new Color();

			result.Red=c1.Red+c2.Red;
			result.Green=c1.Green+c2.Green;
			result.Blue=c1.Blue+c2.Blue;

			return result;
		}

		static public Color operator - (Color c1,Color c2)
		{
			Color result=new Color();

			result.Red=c1.Red-c2.Red;
			result.Green=c1.Green-c2.Green;
			result.Blue=c1.Blue-c2.Blue;

			return result;
		}

		static public Color operator * (Color c1,Color c2)
		{
			Color result=new Color();

			result.Red=c1.Red*c2.Red;
			result.Green=c1.Green*c2.Green;
			result.Blue=c1.Blue*c2.Blue;

			return result;
		}

		static public Color operator * (Color col,double f)
		{
			Color result=new Color();

			result.Red=col.Red*f;
			result.Green=col.Green*f;
			result.Blue=col.Blue*f;

			return result;
		}

        static public Color operator /(Color col, double f)
        {
            Color result = new Color();

            result.Red = col.Red / f;
            result.Green = col.Green / f;
            result.Blue = col.Blue / f;

            return result;
        }

		/// limit the Color values to 0.0-0.1
		public void Limit()
		{
			Red = (Red>0.0) ? ( (Red>1.0) ? 1.0f : Red ) : 0.0f;
			Green = (Green>0.0) ? ( (Green>1.0) ? 1.0f : Green ) : 0.0f;
			Blue = (Blue>0.0) ? ( (Blue>1.0) ? 1.0f : Blue ) : 0.0f;
		}

		/// switch Color to black
		public void ToBlack()
		{
            Red = 0;
            Green = 0;
            Blue = 0;
		}

        /// <summary>
        /// calculates the distance per component and returns the sum
        /// </summary>
        /// <param name="color"></param>
        /// <returns></returns>
        public double Distance(Color color)
        {
            double dist = Math.Abs(Red - color.Red) + Math.Abs(Green - color.Green) + Math.Abs(Blue - color.Blue);
            return dist;
        }
        /// <summary>
        /// blends two colors together.
        /// </summary>
        /// <param name="other">the other color to blend</param>
        /// <param name="weight">weight of the other color, value between [0,1]</param>
        /// <returns></returns>
        public Color Blend(Color other, double weight)
        {
            Color result = new Color(this);
            result = this * (1 - weight) + other * weight;
            return result;
        }

        public Drawing.Color ToArgb()
        {
            return Drawing.Color.FromArgb((int)(Red * 255), (int)(Green * 255), (int)(Blue * 255));
        }

	}
}
