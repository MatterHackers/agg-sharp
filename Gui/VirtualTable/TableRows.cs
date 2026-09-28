/*
Copyright (c) 2026, Lars Brubaker, MatterHackers, Inc.
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System;
using System.Collections.Generic;
using System.Linq;

namespace MatterHackers.Agg.UI
{
	/// <summary>
	/// The rows of a <see cref="VirtualTable"/>, in design units measured top down (agg-gui's TableRows): either
	/// <see cref="Count"/> rows of one height, or one height per row. Only heights are held - the table asks its
	/// cell painter for the content of the rows it shows, so a hundred thousand rows cost next to nothing.
	/// </summary>
	public sealed class TableRows
	{
		private readonly double homogeneousHeight;

		// Running tops of heterogeneous rows (tops[Count] is the total), so finding a row is a binary search.
		private readonly double[] tops;

		private TableRows(int count, double height, double[] tops)
		{
			this.Count = count;
			this.homogeneousHeight = height;
			this.tops = tops;
		}

		public static TableRows Empty { get; } = Homogeneous(0, 18);

		public int Count { get; }

		public double TotalHeight => this.tops == null ? this.Count * this.homogeneousHeight : this.tops[this.Count];

		public static TableRows Homogeneous(int count, double height) => new TableRows(Math.Max(0, count), height, null);

		public static TableRows Heterogeneous(IEnumerable<double> heights)
		{
			double[] array = heights.ToArray();
			var tops = new double[array.Length + 1];
			for (int i = 0; i < array.Length; i++)
			{
				tops[i + 1] = tops[i] + array[i];
			}

			return new TableRows(array.Length, 0, tops);
		}

		public double HeightAt(int row)
		{
			if (row < 0 || row >= this.Count)
			{
				return 0;
			}

			return this.tops == null ? this.homogeneousHeight : this.tops[row + 1] - this.tops[row];
		}

		/// <summary>How far below the top of the first row <paramref name="row"/> starts (clamped to 0..Count).</summary>
		public double TopOf(int row)
		{
			row = Math.Max(0, Math.Min(row, this.Count));
			return this.tops == null ? row * this.homogeneousHeight : this.tops[row];
		}

		/// <summary>The row at <paramref name="topDownY"/>, or -1 above the first row or below the last.</summary>
		public int RowAt(double topDownY)
		{
			if (topDownY < 0 || topDownY >= this.TotalHeight)
			{
				return -1;
			}

			return this.FirstVisibleRow(topDownY);
		}

		/// <summary>The first row whose bottom is below <paramref name="topDownY"/>; <see cref="Count"/> when none is.</summary>
		public int FirstVisibleRow(double topDownY)
		{
			if (this.tops == null)
			{
				if (this.homogeneousHeight <= 0)
				{
					return 0;
				}

				return (int)Math.Min(this.Count, Math.Max(0, Math.Floor(topDownY / this.homogeneousHeight)));
			}

			int low = 0;
			int high = this.Count;
			while (low < high)
			{
				int mid = (low + high) / 2;
				if (this.tops[mid + 1] > topDownY)
				{
					high = mid;
				}
				else
				{
					low = mid + 1;
				}
			}

			return low;
		}
	}
}
