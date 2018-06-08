/*
Copyright (c) 2015, Lars Brubaker
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

using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;

namespace MatterHackers.PolygonMesh.Csg
{
	public class SortedVector2
	{
		private List<IndexAndPosition> sorted = new List<IndexAndPosition>();
		private IndexAndPositionSorter sorter = new IndexAndPositionSorter();

		public SortedVector2()
		{
		}

		public void Add(int index, Vector2 position)
		{
			sorted.Add(new IndexAndPosition(index, position));
		}

		public int FindClosetIndex(Vector2 position, out double bestDistanceSquared, int indexToSkip = -1)
		{
			var testPos = new IndexAndPosition(0, position);
			int index = Math.Min(sorted.Count - 1, sorted.BinarySearch(testPos, sorter));
			if (index < 0)
			{
				index = Math.Min(sorted.Count - 1, ~index);
			}
			var bestIndex = index;
			bestDistanceSquared = double.MaxValue;
			if (sorted[bestIndex].Index != indexToSkip)
			{
				bestDistanceSquared = (sorted[index].Position - position).LengthSquared;
			}
			// we have the starting index now get all the vertices that are close enough starting from here
			for (int i = 0; i < sorted.Count; i++)
			{
				bool checkedX = false;
				var currentIndex = index + i;
				var prevIndex = index - i - 1;
				if (currentIndex < sorted.Count
					&& Math.Pow(Math.Abs(sorted[currentIndex].Position.X - position.X), 2) <= bestDistanceSquared)
				{
					checkedX = true;
					var distSquared = (sorted[currentIndex].Position - position).LengthSquared;
					if (distSquared < bestDistanceSquared
						&& sorted[currentIndex].Index != indexToSkip)
					{
						bestDistanceSquared = distSquared;
						bestIndex = currentIndex;
					}
				}

				if (prevIndex >= 0
					&& Math.Pow(Math.Abs(sorted[prevIndex].Position.X - position.X), 2) <= bestDistanceSquared)
				{
					checkedX = true;
					var distSquared = (sorted[prevIndex].Position - position).LengthSquared;
					if (distSquared < bestDistanceSquared
						&& sorted[prevIndex].Index != indexToSkip)
					{
						bestDistanceSquared = distSquared;
						bestIndex = prevIndex;
					}
				}

				if (bestDistanceSquared == 0
					|| !checkedX)
				{
					break;
				}
			}

			return sorted[bestIndex].Index;
		}

		public void Remove(int index)
		{
			for (int i = 0; i < sorted.Count; i++)
			{
				if (sorted[i].Index == index)
				{
					sorted.RemoveAt(i);
					return;
				}
			}

			throw new Exception();
		}

		public void Sort()
		{
			sorted.Sort(sorter);
		}
	}
}