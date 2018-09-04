using MatterHackers.VectorMath;
using NUnit.Framework;
using System;

namespace MatterHackers.Agg.Tests
{
	[TestFixture]
	public class Vector2DTests
	{
		[Test]
		public void ArithmaticOperations()
		{
			Vector2 Point1 = new Vector2(1, 1);

			Vector2 Point2 = new Vector2(2, 2);

			Vector2 Point3 = Point1 + Point2;
			Assert.IsTrue(Point3 == new Vector2(3, 3));

			Point3 = Point1 - Point2;
			Assert.IsTrue(Point3 == new Vector2(-1, -1));

			Point3 += Point1;
			Assert.IsTrue(Point3 == new Vector2(0, 0));

			Point3 += Point2;
			Assert.IsTrue(Point3 == new Vector2(2, 2));

			Point3 *= 6;
			Assert.IsTrue(Point3 == new Vector2(12, 12));

			Vector2 InlineOpLeftSide = new Vector2(5, -3);
			Vector2 InlineOpRightSide = new Vector2(-5, 4);
			Assert.IsTrue(InlineOpLeftSide + InlineOpRightSide == new Vector2(.0f, 1));

			Assert.IsTrue(InlineOpLeftSide - InlineOpRightSide == new Vector2(10.0f, -7));
		}

		[Test]
		public void GetLengthAndNormalize()
		{
			Vector2 Point3 = new Vector2(3, -4);
			Assert.IsTrue(Point3.Length > 4.999f && Point3.Length < 5.001f);

			Point3.Normalize();
			Assert.IsTrue(Point3.Length > 0.99f && Point3.Length < 1.01f);
		}

		[Test, Ignore("FixNeeded")]
		public void ScalerOperations()
		{
			Vector2 ScalarMultiplicationArgument = new Vector2(5.0f, 4.0f);
			Assert.IsTrue(ScalarMultiplicationArgument * -.5 == new Vector2(-2.5f, -2));
			Assert.IsTrue(ScalarMultiplicationArgument / 2 == new Vector2(2.5f, 2));
			Assert.IsTrue(2 / ScalarMultiplicationArgument == new Vector2(2.5f, 2));
			Assert.IsTrue(5 * ScalarMultiplicationArgument == new Vector2(25, 20));
		}

		[Test]
		public void CrossProduct()
		{
			Random Rand = new Random();
			Vector2 TestVector2D1 = new Vector2(Rand.NextDouble() * 1000, Rand.NextDouble() * 1000);
			Vector2 TestVector2D2 = new Vector2(Rand.NextDouble() * 1000, Rand.NextDouble() * 1000);
			double Cross2D = Vector2.Cross(TestVector2D1, TestVector2D2);

			Vector3 TestVector31 = new Vector3(TestVector2D1.X, TestVector2D1.Y, 0);
			Vector3 TestVector32 = new Vector3(TestVector2D2.X, TestVector2D2.Y, 0);
			Vector3 Cross3D = Vector3.Cross(TestVector31, TestVector32);

			Assert.IsTrue(Cross3D.Z == Cross2D);
		}

		[Test]
		public void DotProduct()
		{
			Random Rand = new Random();
			Vector2 TestVector2D1 = new Vector2(Rand.NextDouble() * 1000, Rand.NextDouble() * 1000);
			Vector2 TestVector2D2 = new Vector2(Rand.NextDouble() * 1000, Rand.NextDouble() * 1000);
			double Cross2D = Vector2.Dot(TestVector2D1, TestVector2D2);

			Vector3 TestVector31 = new Vector3(TestVector2D1.X, TestVector2D1.Y, 0);
			Vector3 TestVector32 = new Vector3(TestVector2D2.X, TestVector2D2.Y, 0);
			double Cross3D = Vector3.Dot(TestVector31, TestVector32);

			Assert.IsTrue(Cross3D == Cross2D);
		}

		[Test]
		public void LengthAndDistance()
		{
			Random Rand = new Random();
			Vector2 Test1 = new Vector2(Rand.NextDouble() * 1000, Rand.NextDouble() * 1000);
			Vector2 Test2 = new Vector2(Rand.NextDouble() * 1000, Rand.NextDouble() * 1000);
			Vector2 Test3 = Test1 + Test2;
			double Distance1 = Test2.Length;
			double Distance2 = (Test1 - Test3).Length;

			Assert.IsTrue(Distance1 < Distance2 + .001f && Distance1 > Distance2 - .001f);
		}
	}
}