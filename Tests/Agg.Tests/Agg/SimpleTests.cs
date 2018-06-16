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

using MatterHackers.Agg.VertexSource;
using Newtonsoft.Json;
using NUnit.Framework;
using System;
using System.Globalization;
using System.Text.RegularExpressions;

namespace MatterHackers.Agg.Tests
{
	[TestFixture, Category("Agg.SimpleTests")]
	public class SimpleTests
	{
		public static bool GetNextNumberSameResult(String source, int startIndex, double expectedValue)
		{
			int startIndexNew = startIndex;
			double newNumber = agg_basics.ParseDouble(source, ref startIndexNew, true);
			int startIndexOld = startIndex;
			double oldNumber = double.Parse(source.Substring(startIndexOld).Replace(" ", ""));
			if (Math.Abs(newNumber - oldNumber) > .0001
				|| Math.Abs(expectedValue - oldNumber) > .0001)
			{
				return false;
			}

			return true;
		}

		[Test]
		public void JsonSerializeVertexStorage()
		{
			var test1Control = new VertexStorage();
			test1Control.MoveTo(10, 11);
			test1Control.LineTo(100, 11);
			test1Control.LineTo(100, 110);
			test1Control.ClosePolygon();
			string jsonData = JsonConvert.SerializeObject(test1Control);
			var test1Result = JsonConvert.DeserializeObject<VertexStorage>(jsonData);
			Assert.AreEqual(test1Control.Count, test1Result.Count);

			var control = test1Control.Vertices().GetEnumerator();
			var result = test1Result.Vertices().GetEnumerator();
			for(int i=0; i<test1Control.Count; i++)
			{
				control.MoveNext();
				result.MoveNext();
				var controlVertex = control.Current;
				var resultVertex = result.Current;
				Assert.AreEqual(controlVertex.command, resultVertex.command);
				Assert.AreEqual(controlVertex.position, resultVertex.position);
			}
		}

		[Test]
		public void GetNextNumberWorks()
		{
			Assert.IsTrue(GetNextNumberSameResult("1234", 0, 1234));
			Assert.IsTrue(GetNextNumberSameResult("1234 15", 5, 15));
			Assert.IsTrue(GetNextNumberSameResult("-1234", 0, -1234));
			Assert.IsTrue(GetNextNumberSameResult("- 1234", 0, -1234));
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
				Color a = new Color(10, 11, 12);
				Color b = new Color(10, 11, 12);
				Assert.IsTrue(a.GetHashCode() == b.GetHashCode());
			}
			{
				ColorF a = new ColorF(10, 11, 12);
				ColorF b = new ColorF(10, 11, 12);
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