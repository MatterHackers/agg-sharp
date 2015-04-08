//----------------------------------------------------------------------------
// Anti-Grain Geometry - Version 2.4
// Copyright (C) 2002-2005 Maxim Shemanarev (http://www.antigrain.com)
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
//
// Solving simultaneous equations
//
//----------------------------------------------------------------------------
using System;

namespace MatterHackers.Agg
{
	//============================================================matrix_pivot
	//template<uint Rows, uint Cols>
	public static class matrix_pivot
	{
		private static void swap_arrays_index1(double[,] a1, uint a1Index0, double[,] a2, uint a2Index0)
		{
			int Cols = a1.GetLength(1);
			if (a2.GetLength(1) != Cols)
			{
				throw new System.FormatException("a1 and a2 must have the same second dimension.");
			}
			for (int i = 0; i < Cols; i++)
			{
				double tmp = a1[a1Index0, i];
				a1[a1Index0, i] = a2[a2Index0, i];
				a2[a2Index0, i] = tmp;
			}
		}

		public static int pivot(double[,] m, uint row)
		{
			int k = (int)(row);
			double max_val, tmp;

			max_val = -1.0;
			int i;
			int Rows = m.GetLength(0);
			for (i = (int)row; i < Rows; i++)
			{
				if ((tmp = Math.Abs(m[i, row])) > max_val && tmp != 0.0)
				{
					max_val = tmp;
					k = i;
				}
			}

			if (m[k, row] == 0.0)
			{
				return -1;
			}

			if (k != (int)(row))
			{
				swap_arrays_index1(m, (uint)k, m, row);
				return k;
			}
			return 0;
		}
	};

	//===============================================================simul_eq
	//template<uint Size, uint RightCols>
	internal struct simul_eq
	{
		public static bool solve(double[,] left,
						  double[,] right,
						  double[,] result)
		{
			if (left.GetLength(0) != 4
				|| right.GetLength(0) != 4
				|| left.GetLength(1) != 4
				|| result.GetLength(0) != 4
				|| right.GetLength(1) != 2
				|| result.GetLength(1) != 2)
			{
				throw new System.FormatException("left right and result must all be the same size.");
			}
			double a1;
			int Size = right.GetLength(0);
			int RightCols = right.GetLength(1);

			double[,] tmp = new double[Size, Size + RightCols];

			for (int i = 0; i < Size; i++)
			{
				for (int j = 0; j < Size; j++)
				{
					tmp[i, j] = left[i, j];
				}
				for (int j = 0; j < RightCols; j++)
				{
					tmp[i, Size + j] = right[i, j];
				}
			}

			for (int k = 0; k < Size; k++)
			{
				if (matrix_pivot.pivot(tmp, (uint)k) < 0)
				{
					return false; // Singularity....
				}

				a1 = tmp[k, k];

				for (int j = k; j < Size + RightCols; j++)
				{
					tmp[k, j] /= a1;
				}

				for (int i = k + 1; i < Size; i++)
				{
					a1 = tmp[i, k];
					for (int j = k; j < Size + RightCols; j++)
					{
						tmp[i, j] -= a1 * tmp[k, j];
					}
				}
			}

			for (int k = 0; k < RightCols; k++)
			{
				int m;
				for (m = (int)(Size - 1); m >= 0; m--)
				{
					result[m, k] = tmp[m, Size + k];
					for (int j = m + 1; j < Size; j++)
					{
						result[m, k] -= tmp[m, j] * result[j, k];
					}
				}
			}
			return true;
		}
	};
}