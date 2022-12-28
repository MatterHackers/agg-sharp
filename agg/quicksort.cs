/*
Copyright (c) 2014, Lars Brubaker
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

namespace MatterHackers.Agg
{
	public class QuickSortCellAa
	{
		public QuickSortCellAa()
		{
		}

		public void Sort(PixelCellAa[] dataToSort)
		{
			Sort(dataToSort, 0, (int)(dataToSort.Length - 1));
		}

		public void Sort(PixelCellAa[] dataToSort, int beg, int end)
		{
			if (end == beg)
			{
				return;
			}
			else
			{
				int pivot = GetPivotPoint(dataToSort, beg, end);
				if (pivot > beg)
				{
					Sort(dataToSort, beg, pivot - 1);
				}

				if (pivot < end)
				{
					Sort(dataToSort, pivot + 1, end);
				}
			}
		}

		private int GetPivotPoint(PixelCellAa[] dataToSort, int begPoint, int endPoint)
		{
			int pivot = begPoint;
			int m = begPoint + 1;
			int n = endPoint;
			while ((m < endPoint)
				&& dataToSort[pivot].x >= dataToSort[m].x)
			{
				m++;
			}

			while ((n > begPoint) && (dataToSort[pivot].x <= dataToSort[n].x))
			{
				n--;
			}
			while (m < n)
			{
				PixelCellAa temp = dataToSort[m];
				dataToSort[m] = dataToSort[n];
				dataToSort[n] = temp;

				while ((m < endPoint) && (dataToSort[pivot].x >= dataToSort[m].x))
				{
					m++;
				}

				while ((n > begPoint) && (dataToSort[pivot].x <= dataToSort[n].x))
				{
					n--;
				}
			}
			if (pivot != n)
			{
				PixelCellAa temp2 = dataToSort[n];
				dataToSort[n] = dataToSort[pivot];
				dataToSort[pivot] = temp2;
			}
			return n;
		}
	}

	public class QuickSortRangeAdaptorUint
	{
		public QuickSortRangeAdaptorUint()
		{
		}

		public void Sort(VectorPodRangeAdaptor dataToSort)
		{
			Sort(dataToSort, 0, (int)(dataToSort.Size() - 1));
		}

		public void Sort(VectorPodRangeAdaptor dataToSort, int beg, int end)
		{
			if (end == beg)
			{
				return;
			}
			else
			{
				int pivot = GetPivotPoint(dataToSort, beg, end);
				if (pivot > beg)
				{
					Sort(dataToSort, beg, pivot - 1);
				}

				if (pivot < end)
				{
					Sort(dataToSort, pivot + 1, end);
				}
			}
		}

		private int GetPivotPoint(VectorPodRangeAdaptor dataToSort, int begPoint, int endPoint)
		{
			int pivot = begPoint;
			int m = begPoint + 1;
			int n = endPoint;
			while ((m < endPoint)
				&& dataToSort[pivot] >= dataToSort[m])
			{
				m++;
			}

			while ((n > begPoint) && (dataToSort[pivot] <= dataToSort[n]))
			{
				n--;
			}
			while (m < n)
			{
				int temp = dataToSort[m];
				dataToSort[m] = dataToSort[n];
				dataToSort[n] = temp;

				while ((m < endPoint) && (dataToSort[pivot] >= dataToSort[m]))
				{
					m++;
				}

				while ((n > begPoint) && (dataToSort[pivot] <= dataToSort[n]))
				{
					n--;
				}
			}
			if (pivot != n)
			{
				int temp2 = dataToSort[n];
				dataToSort[n] = dataToSort[pivot];
				dataToSort[pivot] = temp2;
			}
			return n;
		}
	}
}