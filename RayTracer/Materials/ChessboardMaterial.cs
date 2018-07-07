// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------

using MatterHackers.Agg;

namespace MatterHackers.RayTracer
{
	public class ChessboardMaterial : MaterialAbstract
	{
		/// <summary>
		/// E.g. the represents the black squares on the chessboard
		/// </summary>
		public ColorF ColorEven;

		/// <summary>
		/// represents the white squares on the chessboard
		/// </summary>
		public ColorF ColorOdd;

		/// <summary>
		/// Density indicates the size of the squares and therefore
		/// the number of squares displayed
		/// the value must be > 0
		/// </summary>
		public double Density;

		public ChessboardMaterial(ColorF coloreven, ColorF colorodd, double reflection, double transparency, double gloss, double density)
		{
			this.ColorEven = coloreven;
			this.ColorOdd = colorodd;
			this.Reflection = reflection;
			this.Transparency = transparency;
			this.Gloss = gloss;
			this.Density = density;
		}

		public override bool HasTexture
		{
			get { return true; }
		}

		public override ColorF GetColor(double u, double v)
		{
			double t = WrapUp(u) * WrapUp(v);

			if (t < 0.0)
				return ColorEven;
			else
				return ColorOdd;
		}
	}
}