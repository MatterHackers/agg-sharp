//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
//
// C# port by: Lars Brubaker
//                  larsbrubaker@gmail.com
// Copyright (C) 2007
//
// Permission to copy, use, modify, sell and distribute this software
// is granted provided this copyright notice appears in all copies.
// This software is provided "as is" without express or implied
// warranty, and with no claim as to its suitability for any purpose.
//
//----------------------------------------------------------------------------
// Contact: mcseem@antigrain.com
//          mcseemagg@yahoo.com
//          http://www.antigrain.com
//----------------------------------------------------------------------------

//#ifndef AGG_GAMMA_LUT_INCLUDED
//#define AGG_GAMMA_LUT_INCLUDED

//#include <math.h>
//#include "agg_basics.h"

using System;

namespace MatterHackers.Agg
{
	public class GammaLookUpTable
	{
		private double m_gamma;
		private byte[] m_dir_gamma;
		private byte[] m_inv_gamma;

		private enum gamma_scale_e
		{
			gamma_shift = 8,
			gamma_size = 1 << gamma_shift,
			gamma_mask = gamma_size - 1
		};

		public GammaLookUpTable()
		{
			m_gamma = (1.0);
			m_dir_gamma = new byte[(int)gamma_scale_e.gamma_size];
			m_inv_gamma = new byte[(int)gamma_scale_e.gamma_size];
		}

		public GammaLookUpTable(double gamma)
		{
			m_gamma = gamma;
			m_dir_gamma = new byte[(int)gamma_scale_e.gamma_size];
			m_inv_gamma = new byte[(int)gamma_scale_e.gamma_size];
			SetGamma(m_gamma);
		}

		public void SetGamma(double g)
		{
			m_gamma = g;

			for (uint i = 0; i < (uint)gamma_scale_e.gamma_size; i++)
			{
				m_dir_gamma[i] = (byte)agg_basics.uround(Math.Pow(i / (double)gamma_scale_e.gamma_mask, m_gamma) * (double)gamma_scale_e.gamma_mask);
			}

			double inv_g = 1.0 / g;
			for (uint i = 0; i < (uint)gamma_scale_e.gamma_size; i++)
			{
				m_inv_gamma[i] = (byte)agg_basics.uround(Math.Pow(i / (double)gamma_scale_e.gamma_mask, inv_g) * (double)gamma_scale_e.gamma_mask);
			}
		}

		public double GetGamma()
		{
			return m_gamma;
		}

		public byte dir(int v)
		{
			return m_dir_gamma[v];
		}

		public byte inv(int v)
		{
			return m_inv_gamma[v];
		}
	};
}