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
//#define USE_UNSAFE // no real code for this yet

using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MatterHackers.Agg
{
	public delegate void ReportProgressRatio(double progress0To1, string processingState, out bool continueProcessing);

	static public class agg_basics
	{
		//----------------------------------------------------------filling_rule_e
		public enum filling_rule_e
		{
			fill_non_zero,
			fill_even_odd
		}

		public static void memcpy(Byte[] dest, int destIndex, Byte[] source, int sourceIndex, int Count)
		{
#if USE_UNSAFE
#else
			for (int i = 0; i < Count; i++)
			{
				dest[destIndex + i] = source[sourceIndex + i];
			}
#endif
		}

		public static double ParseDouble(String source, bool fastSimpleNumbers)
		{
			int startIndex = 0;
			return ParseDouble(source, ref startIndex, fastSimpleNumbers);
		}

		//private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+");
		private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+([eE][-+]?[0-9]+)?");

		private static double GetNextNumber(String source, ref int startIndex)
		{
			Match numberMatch = numberRegex.Match(source, startIndex);
			String returnString = numberMatch.Value;
			startIndex = numberMatch.Index + numberMatch.Length;
			double returnVal;
			double.TryParse(returnString, NumberStyles.Number, CultureInfo.InvariantCulture, out returnVal);
			return returnVal;
		}

		public static double ParseDouble(String source, ref int startIndex, bool fastSimpleNumbers)
		{
#if true
			if (fastSimpleNumbers)
			{
				return ParseDoubleFast(source, ref startIndex);
			}
			return GetNextNumber(source, ref startIndex);
#else
			int startIndexNew = startIndex;
			double newNumber = agg_basics.ParseDoubleFast(source, ref startIndexNew);
			int startIndexOld = startIndex;
			double oldNumber = GetNextNumber(source, ref startIndexOld);
			if (Math.Abs(newNumber - oldNumber) > .0001
				|| startIndexNew != startIndexOld)
			{
				int a = 0;
			}

			startIndex = startIndexNew;
			return newNumber;
#endif
		}

		private static double ParseDoubleFast(String source, ref int startIndex)
		{
			int length = source.Length;
			bool negative = false;
			long currentIntPart = 0;
			int fractionDigits = 0;
			long currentFractionPart = 0;
			bool foundNumber = false;

			// find the number start
			while (startIndex < length)
			{
				char next = source[startIndex];
				if (next == '.' || next == '-' || next == '+' || (next >= '0' && next <= '9'))
				{
					if (next == '.')
					{
                        break;
					}
					if (next == '-')
					{
						negative = true;
					}
					else if (next == '+')
					{
						// this does nothing but lets us get to the else for numbers
					}
					else
					{
						currentIntPart = next - '0';
						foundNumber = true;
					}
					startIndex++;
					break;
				}
				startIndex++;
			}
			// accumulate the int part
			while (startIndex < length)
			{
				char next = source[startIndex];
				if (next >= '0' && next <= '9')
				{
					currentIntPart = (currentIntPart * 10) + next - '0';
					foundNumber = true;
				}
				else if (next == '.')
				{
					foundNumber = true;
					startIndex++;
					// parse out the fractional part
					while (startIndex < length)
					{
						char nextFraction = source[startIndex];
						if (nextFraction >= '0' && nextFraction <= '9')
						{
							fractionDigits++;
							currentFractionPart = (currentFractionPart * 10) + nextFraction - '0';
						}
						else // we are done
						{
							break;
						}
						startIndex++;
					}
					break;
				}
				else if(!foundNumber && next == ' ')
				{
					// happy to skip spaces
				}
				else // we are done
				{
					break;
				}
				startIndex++;
			}

			if (fractionDigits > 0)
			{
				double fractionNumber = currentIntPart + (currentFractionPart / (Math.Pow(10.0, fractionDigits)));
				if (negative)
				{
					return -fractionNumber;
				}
				return fractionNumber;
			}
			else
			{
				if (negative)
				{
					return -currentIntPart;
				}
				return currentIntPart;
			}
		}

		public static long ComputeHash(byte[] data)
		{
			unchecked
			{
				const long p = 1099511628211;
				long hash = (long)14695981039346656037;

				for (int i = 0; i < data.Length; i++)
				{
					hash = (hash ^ data[i]) * p;
				}

				hash += hash << 13;
				hash ^= hash >> 7;
				hash += hash << 3;
				hash ^= hash >> 17;
				hash += hash << 5;
				return hash;
			}
		}

		public static void memcpy(int[] dest, int destIndex, int[] source, int sourceIndex, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				dest[destIndex + i] = source[sourceIndex + i];
			}
		}

		public static void memcpy(float[] dest, int destIndex, float[] source, int sourceIndex, int count)
		{
			for (int i = 0; i < count; i++)
			{
				dest[destIndex++] = source[sourceIndex++];
			}
		}

		public static void memmove(Byte[] dest, int destIndex, Byte[] source, int sourceIndex, int Count)
		{
			if (source != dest
				|| destIndex < sourceIndex)
			{
				memcpy(dest, destIndex, source, sourceIndex, Count);
			}
			else
			{
				throw new Exception("this code needs to be tested");
				/*
				for (int i = Count-1; i > 0; i--)
				{
					dest[destIndex + i] = source[sourceIndex + i];
				}
				 */
			}
		}

		public static void memmove(int[] dest, int destIndex, int[] source, int sourceIndex, int Count)
		{
			if (source != dest
				|| destIndex < sourceIndex)
			{
				memcpy(dest, destIndex, source, sourceIndex, Count);
			}
			else
			{
				throw new Exception("this code needs to be tested");
				/*
				for (int i = Count-1; i > 0; i--)
				{
					dest[destIndex + i] = source[sourceIndex + i];
				}
				 */
			}
		}

		public static void memmove(float[] dest, int destIndex, float[] source, int sourceIndex, int Count)
		{
			if (source != dest
				|| destIndex < sourceIndex)
			{
				memcpy(dest, destIndex, source, sourceIndex, Count);
			}
			else
			{
				throw new Exception("this code needs to be tested");
				/*
				for (int i = Count-1; i > 0; i--)
				{
					dest[destIndex + i] = source[sourceIndex + i];
				}
				 */
			}
		}

		public static void memset(int[] dest, int destIndex, int Val, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				dest[destIndex + i] = Val;
			}
		}

		public static void memset(Byte[] dest, int destIndex, byte ByteVal, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				dest[destIndex + i] = ByteVal;
			}
		}

		public static void MemClear(int[] dest, int destIndex, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				dest[destIndex + i] = 0;
			}
		}

		public static void MemClear(Byte[] dest, int destIndex, int Count)
		{
			for (int i = 0; i < Count; i++)
			{
				dest[destIndex + i] = 0;
			}
			/*
			// dword align to dest
			while (((int)pDest & 3) != 0
				&& Count > 0)
			{
				*pDest++ = 0;
				Count--;
			}

			int NumLongs = Count / 4;

			while (NumLongs-- > 0)
			{
				*((int*)pDest) = 0;

				pDest += 4;
			}

			switch (Count & 3)
			{
				case 3:
					pDest[2] = 0;
					goto case 2;
				case 2:
					pDest[1] = 0;
					goto case 1;
				case 1:
					pDest[0] = 0;
					break;
			}
			 */
		}

		public static bool is_equal_eps(double v1, double v2, double epsilon)
		{
			return Math.Abs(v1 - v2) <= (double)(epsilon);
		}

		//------------------------------------------------------------------deg2rad
		public static double deg2rad(double deg)
		{
			return deg * Math.PI / 180.0;
		}

		//------------------------------------------------------------------rad2deg
		public static double rad2deg(double rad)
		{
			return rad * 180.0 / Math.PI;
		}

		public static int iround(double v)
		{
			unchecked
			{
				return (int)((v < 0.0) ? v - 0.5 : v + 0.5);
			}
		}

		public static int iround(double v, int saturationLimit)
		{
			if (v < (double)(-saturationLimit)) return -saturationLimit;
			if (v > (double)(saturationLimit)) return saturationLimit;
			return iround(v);
		}

		public static int uround(double v)
		{
			return (int)(uint)(v + 0.5);
		}

		public static int ufloor(double v)
		{
			return (int)(uint)(v);
		}

		public static int uceil(double v)
		{
			return (int)(uint)(Math.Ceiling(v));
		}

		//----------------------------------------------------poly_subpixel_scale_e
		// These constants determine the subpixel accuracy, to be more precise,
		// the number of bits of the fractional part of the coordinates.
		// The possible coordinate capacity in bits can be calculated by formula:
		// sizeof(int) * 8 - poly_subpixel_shift, i.e, for 32-bit integers and
		// 8-bits fractional part the capacity is 24 bits.
		public enum poly_subpixel_scale_e
		{
			poly_subpixel_shift = 8,                      //----poly_subpixel_shift
			poly_subpixel_scale = 1 << poly_subpixel_shift, //----poly_subpixel_scale
			poly_subpixel_mask = poly_subpixel_scale - 1,  //----poly_subpixel_mask
		}
	}
}