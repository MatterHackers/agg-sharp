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

using NUnit.Framework;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MatterHackers.Agg.Tests
{
	[TestFixture, Category("Agg.SimpleTests")]
	public class SimpleTests
	{
		private static Regex numberRegex = new Regex(@"[-+]?[0-9]*\.?[0-9]+");

		private static double GetNextNumberOld(String source, ref int startIndex)
		{
			Match numberMatch = numberRegex.Match(source, startIndex);
			String returnString = numberMatch.Value;
			startIndex = numberMatch.Index + numberMatch.Length;
			double returnVal;
			double.TryParse(returnString, NumberStyles.Number, CultureInfo.InvariantCulture, out returnVal);
			return returnVal;
		}

		public static bool GetNextNumberSameResult(String source, int startIndex, double expectedValue)
		{
			int startIndexNew = startIndex;
			double newNumber = agg_basics.ParseDouble(source, ref startIndexNew, true);
			int startIndexOld = startIndex;
			double oldNumber = GetNextNumberOld(source, ref startIndexOld);
			if (Math.Abs(newNumber - oldNumber) > .0001
				|| Math.Abs(expectedValue - oldNumber) > .0001
				|| startIndexNew != startIndexOld)
			{
				return false;
			}

			return true;
		}

		[Test]
		public void GetNextNumberWorks()
		{
			Assert.IsTrue(GetNextNumberSameResult("1234", 0, 1234));
			Assert.IsTrue(GetNextNumberSameResult("1234 15", 5, 15));
			Assert.IsTrue(GetNextNumberSameResult("-1234", 0, -1234));
			Assert.IsTrue(GetNextNumberSameResult("+1234", 0, 1234));
			Assert.IsTrue(GetNextNumberSameResult("1234.3", 0, 1234.3));
			Assert.IsTrue(GetNextNumberSameResult("1234.354", 0, 1234.354));
			Assert.IsTrue(GetNextNumberSameResult("1234.354212", 0, 1234.354212));
			Assert.IsTrue(GetNextNumberSameResult("0.123", 0, .123));
			Assert.IsTrue(GetNextNumberSameResult(".123", 0, .123));
		}

		[Test]
		public void TestGetHashCode()
		{
			{
				RGBA_Bytes a = new RGBA_Bytes(10, 11, 12);
				RGBA_Bytes b = new RGBA_Bytes(10, 11, 12);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				RGBA_Floats a = new RGBA_Floats(10, 11, 12);
				RGBA_Floats b = new RGBA_Floats(10, 11, 12);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				BorderDouble a = new BorderDouble(10, 11, 12, 13);
				BorderDouble b = new BorderDouble(10, 11, 12, 13);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				Point2D a = new Point2D(10, 11);
				Point2D b = new Point2D(10, 11);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				RectangleDouble a = new RectangleDouble(10, 11, 12, 13);
				RectangleDouble b = new RectangleDouble(10, 11, 12, 13);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				RectangleInt a = new RectangleInt(10, 11, 12, 13);
				RectangleInt b = new RectangleInt(10, 11, 12, 13);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
		}
	}
}