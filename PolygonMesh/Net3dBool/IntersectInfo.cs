// Copyright 2006 Herre Kuijpers - <herre@xs4all.nl>
//
// This source file(s) may be redistributed, altered and customized
// by any means PROVIDING the authors name and all copyright
// notices remain intact.
// THIS SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
// EXPRESS OR IMPLIED. USE IT AT YOUR OWN RISK. THE AUTHOR ACCEPTS NO
// LIABILITY FOR ANY DATA DAMAGE/LOSS THAT THIS PRODUCT MAY CAUSE.
//-----------------------------------------------------------------------
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

using MatterHackers.VectorMath;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;

namespace Net3dBool
{
	/// <summary>
	/// this is a data class that stores all relevant information about a ray-shape intersection
	/// this information is to be filled in by every custom implemented shape type in the Intersect method.
	/// this information is used to determine the color at the intersection point
	/// </summary>
	public class IntersectInfo
	{
		public IntersectionType hitType;
		public IPrimitive closestHitObject;
		public Vector3 hitPosition;
		public Vector3 normalAtHit;
		public double distanceToHit;

		public IntersectInfo()
		{
			distanceToHit = double.MaxValue;
		}

		public IntersectInfo(IntersectInfo copyInfo)
		{
			this.hitType = copyInfo.hitType;
			this.closestHitObject = copyInfo.closestHitObject;
			this.hitPosition = copyInfo.hitPosition;
			this.normalAtHit = copyInfo.normalAtHit;
			this.distanceToHit = copyInfo.distanceToHit;
		}

		public static void SaveOutLists(List<IntersectInfo> allPrimary, List<IntersectInfo> allSubtract)
		{
			FileStream file = new FileStream("BadSubtract.txt", FileMode.Create, FileAccess.Write);
			StreamWriter writer = new StreamWriter(file);

			writer.WriteLine(allPrimary.Count.ToString());
			foreach (IntersectInfo info in allPrimary)
			{
				writer.WriteLine(info.hitType.ToString() + ", " + info.distanceToHit.ToString());
			}

			writer.WriteLine(allSubtract.Count.ToString());
			foreach (IntersectInfo info in allSubtract)
			{
				writer.WriteLine(info.hitType.ToString() + ", " + info.distanceToHit.ToString());
			}

			writer.Close();
			file.Close();
		}

		public static void ReadInLists(List<IntersectInfo> allPrimary, List<IntersectInfo> allSubtract)
		{
			FileStream file = new FileStream("BadSubtract.txt", FileMode.Open, FileAccess.Read);
			StreamReader reader = new StreamReader(file);

			ReadInList(allPrimary, reader);
			ReadInList(allSubtract, reader);

			reader.Close();
			file.Close();
		}

		public static void ReadInList(List<IntersectInfo> listToPopulate, StreamReader reader)
		{
			int count;
			int.TryParse(reader.ReadLine(), NumberStyles.Number, null, out count);
			for (int i = 0; i < count; i++)
			{
				IntersectInfo info = new IntersectInfo();
				string line = reader.ReadLine();
				string[] strings = line.Split(',');
				if (strings[0] == IntersectionType.FrontFace.ToString())
				{
					info.hitType = IntersectionType.FrontFace;
				}
				else
				{
					if (strings[0] != IntersectionType.BackFace.ToString())
					{
						throw new Exception("Has to be back or front.");
					}
					info.hitType = IntersectionType.BackFace;
				}
				double.TryParse(strings[1], NumberStyles.Number, null, out info.distanceToHit);
				listToPopulate.Add(info);
			}
		}

		public static void Subtract(List<IntersectInfo> allPrimary, List<IntersectInfo> allSubtract, List<IntersectInfo> result)
		{
			bool readList = false;
#if false
            if (File.Exists("BadSubtract.txt"))
            {
                ReadInLists(allPrimary, allSubtract);
                readList = true;
            }
#endif

			result.Clear();
			foreach (IntersectInfo info in allPrimary)
			{
				result.Add(info);
			}

			try
			{
				int nextSubtractIndex = 0;
				do
				{
					int regionStartIndex;
					int regionEndIndex;
					if (GetRemoveRegion(allSubtract, nextSubtractIndex, out regionStartIndex, out regionEndIndex))
					{
						SubtractRemoveRegion(result, allSubtract[regionStartIndex], allSubtract[regionEndIndex]);

						nextSubtractIndex = regionEndIndex + 1;
					}
					else
					{
						return;
					}
				} while (true);
			}
			catch (Exception e)
			{
				if (!readList)
				{
					SaveOutLists(allPrimary, allSubtract);
				}
				throw e;
			}
		}

		private static void SubtractRemoveRegion(List<IntersectInfo> removeFrom, IntersectInfo startRemoveInfo, IntersectInfo endRemoveInfo)
		{
			if (startRemoveInfo.hitType != IntersectionType.FrontFace || endRemoveInfo.hitType != IntersectionType.BackFace)
			{
				throw new Exception("These should always be set right.");
			}

			double insideCount = 0;
			for (int primaryIndex = 0; primaryIndex < removeFrom.Count; primaryIndex++)
			{
				IntersectInfo primaryInfo = removeFrom[primaryIndex];
				if (primaryInfo.hitType == IntersectionType.FrontFace)
				{
					insideCount++;

					if (primaryInfo.distanceToHit < (startRemoveInfo.distanceToHit - Ray.sameSurfaceOffset))
					{
						// We are in front of the remove start. If the next backface is behind the remove start, add a back face at the remove start.
						// there should always be a back face so it should be safe to + 1 this index.  I will let the bounds checker get it because it will throw an assert.  If not I would throw one instead.
						if (removeFrom[primaryIndex + 1].distanceToHit > (startRemoveInfo.distanceToHit - Ray.sameSurfaceOffset))
						{
							IntersectInfo newBackFace = new IntersectInfo(startRemoveInfo);
							newBackFace.hitType = IntersectionType.BackFace;
							removeFrom.Insert(primaryIndex + 1, newBackFace); // it goes after the current index
							primaryIndex++;
						}
					}
					else if (primaryInfo.distanceToHit >= (startRemoveInfo.distanceToHit - Ray.sameSurfaceOffset) && primaryInfo.distanceToHit < (endRemoveInfo.distanceToHit + Ray.sameSurfaceOffset))
					{
						// the front face is within the remove so remove it.
						removeFrom.Remove(primaryInfo);
						// need to check the same index again.
						primaryIndex--;
					}
					else if (primaryInfo.distanceToHit >= (endRemoveInfo.distanceToHit + Ray.sameSurfaceOffset))
					{
						// we have gone past the remove region just return.
						return;
					}
				}
				else if (primaryInfo.hitType == IntersectionType.BackFace)
				{
					if (insideCount == 0)
					{
						throw new Exception("You should not have a back face without a matching front face.");
					}
					insideCount--;
					if (insideCount == 0)
					{
						if (primaryInfo.distanceToHit > (startRemoveInfo.distanceToHit - Ray.sameSurfaceOffset) && primaryInfo.distanceToHit < (endRemoveInfo.distanceToHit + Ray.sameSurfaceOffset))
						{
							// the back face is within the remove so remove it.
							removeFrom.Remove(primaryInfo);
							// need to check the same index again.
							primaryIndex--;
						}
						else if (primaryInfo.distanceToHit >= (endRemoveInfo.distanceToHit + Ray.sameSurfaceOffset))
						{
							// the back face is past the remove distance.
							// Add the remove back face as a front face to the primary
							// We should be guaranteed that the front face is within the remove distance because if it was we should have returned above.
							IntersectInfo newFrontFace = new IntersectInfo(endRemoveInfo);
							newFrontFace.hitType = IntersectionType.FrontFace;
							removeFrom.Insert(primaryIndex, newFrontFace);
							primaryIndex++;
						}
					}
					else
					{
						if (primaryInfo.distanceToHit > (startRemoveInfo.distanceToHit - Ray.sameSurfaceOffset) && primaryInfo.distanceToHit < (endRemoveInfo.distanceToHit + Ray.sameSurfaceOffset))
						{
							// the back face is within the remove so remove it.
							removeFrom.Remove(primaryInfo);
							// need to check the same index again.
							primaryIndex--;
						}
					}
				}
				else
				{
					throw new Exception("There should be no 'none's in the hit types.");
				}
			}
		}

		private static bool GetRemoveRegion(List<IntersectInfo> subtractList, int startIndex, out int regionStartIndex, out int regionEndIndex)
		{
			regionStartIndex = startIndex;
			regionEndIndex = startIndex;
			double insideCount = 0;
			int subtractCount = subtractList.Count;
			for (int subtractIndex = startIndex; subtractIndex < subtractCount; subtractIndex++)
			{
				IntersectInfo subtractInfo = subtractList[subtractIndex];
				if (subtractInfo.hitType == IntersectionType.FrontFace)
				{
					if (insideCount == 0)
					{
						regionStartIndex = subtractIndex;
					}
					insideCount++;
				}
				else if (subtractInfo.hitType == IntersectionType.BackFace)
				{
					if (insideCount == 0)
					{
						throw new Exception("You should not have a back face without a matching front face.");
					}
					insideCount--;
					if (insideCount == 0)
					{
						// let's check that there is not another entry aligned exactly with this exit
						int nextIndex = subtractIndex + 1;
						if (nextIndex >= subtractCount || subtractList[subtractIndex].distanceToHit + Ray.sameSurfaceOffset < subtractList[nextIndex].distanceToHit)
						{
							// we have our subtract region
							regionEndIndex = subtractIndex;
							return true;
						}
					}
				}
				else
				{
					throw new Exception("There should be no 'none's in the hit types.");
				}
			}

			return false;
		}

#if false
        private static bool IsInside(List<IntersectInfo> listToCheck, double distance)
        {
            int insideCount = 0;
            int conut = listToCheck.Count;
            for (int index = 0; index < conut; index++)
            {
                IntersectInfo info = listToCheck[index];
                if (info.hitType == HitType.FrontFace)
                {
                    if (insideCount == 0)
                    {
                        startSubtract = info.distanceToHit;
                        // add all the elements from the last end to the new start
                    }
                    insideCount++;
                }
                else if (info.hitType == HitType.BackFace)
                {
                    insideCount--;
                    if (insideCount == 0)
                    {
                        if (IsInside(allPrimary, info.distanceToHit))
                        {
                            result.Add(info);
                        }
                        lastSubtractEnd = info.distanceToHit;
                        // add all the front face points between lastSubtractEnd and startSubtract
                    }
                }
                else
                {
                    throw new Exception("There should be no 'none's in the hit types.");
                }
            }

            return false;
        }
#endif
	}

	/// <summary>
	/// A comparision function to sort Intersect Infos on distance.
	/// Can be used to sort Lists.
	/// </summary>
	public class CompareIntersectInfoOnDistance : IComparer<IntersectInfo>
	{
		public CompareIntersectInfoOnDistance()
		{
		}

		public int Compare(IntersectInfo a, IntersectInfo b)
		{
			if (a == null || b == null)
			{
				throw new Exception();
			}

			double axisCenterA = a.distanceToHit;
			double axisCenterB = b.distanceToHit;

			if (axisCenterA > axisCenterB)
			{
				return 1;
			}
			else if (axisCenterA < axisCenterB)
			{
				return -1;
			}
			if (a.hitType != b.hitType)
			{
				if (a.hitType == IntersectionType.FrontFace && b.hitType == IntersectionType.BackFace)
				{
					return -1;
				}
				else if (a.hitType == IntersectionType.BackFace && b.hitType == IntersectionType.FrontFace)
				{
					return 1;
				}
				else
				{
					throw new Exception();
				}
			}
			return 0;
		}
	}
}