using MatterHackers.VectorMath;
using NUnit.Framework;
using System;
using System.Collections.Generic;

namespace MatterHackers.Agg.Tests
{
	[TestFixture]
	public class Vector2Tests
	{
		[Test]
		public void ArithmaticOperations()
		{
			var point1 = new Vector2(1, 1);

			var point2 = new Vector2(2, 2);

			Vector2 point3 = point1 + point2;
			Assert.IsTrue(point3 == new Vector2(3, 3));

			point3 = point1 - point2;
			Assert.IsTrue(point3 == new Vector2(-1, -1));

			point3 += point1;
			Assert.IsTrue(point3 == new Vector2(0, 0));

			point3 += point2;
			Assert.IsTrue(point3 == new Vector2(2, 2));

			point3 *= 6;
			Assert.IsTrue(point3 == new Vector2(12, 12));

			var inlineOpLeftSide = new Vector2(5, -3);
			var inlineOpRightSide = new Vector2(-5, 4);
			Assert.IsTrue(inlineOpLeftSide + inlineOpRightSide == new Vector2(.0f, 1));

			Assert.IsTrue(inlineOpLeftSide - inlineOpRightSide == new Vector2(10.0f, -7));
		}

		[Test]
		public void GetLengthAndNormalize()
		{
			var point3 = new Vector2(3, -4);
			Assert.IsTrue(point3.Length > 4.999f && point3.Length < 5.001f);

			point3.Normalize();
			Assert.IsTrue(point3.Length > 0.99f && point3.Length < 1.01f);
		}

		[Test]
		public void GetPositionAtTests()
		{
			var line1 = new List<Vector2>()
			{
				new Vector2(10, 3),
				new Vector2(20, 3),
				new Vector2(20, 13),
				new Vector2(10, 13)
			};

			Assert.AreEqual(30, line1.PolygonLength(false));

			// open segments should also give correct values
			Assert.AreEqual(new Vector2(13, 3), line1.GetPositionAt(3, false));
			Assert.AreEqual(new Vector2(10, 13), line1.GetPositionAt(33, false), "Open so return the end");
			Assert.AreEqual(new Vector2(10, 13), line1.GetPositionAt(33 + 22 * 10, false), "Open so return the end");
			Assert.AreEqual(new Vector2(10, 3), line1.GetPositionAt(-2, false), "Negative so return the start");
			Assert.AreEqual(new Vector2(10, 3), line1.GetPositionAt(-2 + -23 * 10, false), "Negative so return the start");

			Assert.AreEqual(40, line1.PolygonLength(true));

			// closed loops should wrap correctly
			var error = .000001;
			Assert.AreEqual(new Vector2(13, 3), line1.GetPositionAt(3));
			Assert.IsTrue(new Vector2(13, 3).Equals(line1.GetPositionAt(43), error), "Closed loop so we should go back to the beginning");
			Assert.IsTrue(new Vector2(13, 3).Equals(line1.GetPositionAt(43 + 22 * 40), error), "Closed loop so we should go back to the beginning");
			Assert.IsTrue(new Vector2(10, 5).Equals(line1.GetPositionAt(-2), error), "Negative values are still valid");
			Assert.IsTrue(new Vector2(10, 5).Equals(line1.GetPositionAt(-2 + 23 * 40), error), "Negative values are still valid");
		}

		[Test, Ignore("FixNeeded")]
		public void ScalerOperations()
		{
			var scalarMultiplicationArgument = new Vector2(5.0f, 4.0f);
			Assert.IsTrue(scalarMultiplicationArgument * -.5 == new Vector2(-2.5f, -2));
			Assert.IsTrue(scalarMultiplicationArgument / 2 == new Vector2(2.5f, 2));
			Assert.IsTrue(2 / scalarMultiplicationArgument == new Vector2(2.5f, 2));
			Assert.IsTrue(5 * scalarMultiplicationArgument == new Vector2(25, 20));
		}

		[Test]
		public void CrossProduct()
		{
			var rand = new Random();
			var testVector2D1 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			var testVector2D2 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			double cross2D = Vector2.Cross(testVector2D1, testVector2D2);

			var testVector31 = new Vector3(testVector2D1.X, testVector2D1.Y, 0);
			var testVector32 = new Vector3(testVector2D2.X, testVector2D2.Y, 0);
			Vector3 cross3D = Vector3Ex.Cross(testVector31, testVector32);

			Assert.IsTrue(cross3D.Z == cross2D);
		}

		[Test]
		public void DotProduct()
		{
			var rand = new Random();
			var testVector2D1 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			var testVector2D2 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			double cross2D = Vector2.Dot(testVector2D1, testVector2D2);

			var testVector31 = new Vector3(testVector2D1.X, testVector2D1.Y, 0);
			var testVector32 = new Vector3(testVector2D2.X, testVector2D2.Y, 0);
			double cross3D = Vector3Ex.Dot(testVector31, testVector32);

			Assert.IsTrue(cross3D == cross2D);
		}

		[Test]
		public void LengthAndDistance()
		{
			var rand = new Random();
			var test1 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			var test2 = new Vector2(rand.NextDouble() * 1000, rand.NextDouble() * 1000);
			Vector2 test3 = test1 + test2;
			double distance1 = test2.Length;
			double distance2 = (test1 - test3).Length;

			Assert.IsTrue(distance1 < distance2 + .001f && distance1 > distance2 - .001f);
		}
	}
}