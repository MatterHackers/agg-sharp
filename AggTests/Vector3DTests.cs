using MatterHackers.VectorMath;
using NUnit.Framework;

namespace MatterHackers.Agg.Tests
{
	[TestFixture]
	public class Vector3Tests
	{
		[Test]
		public void VectorAdditionAndSubtraction()
		{
			Vector3 Point1 = new Vector3();
			Point1 = new Vector3(1, 1, 1);

			Vector3 Point2 = new Vector3();
			Point2 = new Vector3(2, 2, 2);

			Vector3 Point3 = new Vector3();
			Point3 = Vector3.Add(Point1, Point2);
			Assert.IsTrue(Point3 == new Vector3(3, 3, 3));

			Point3 = Point1 - Point2;
			Assert.IsTrue(Point3 == new Vector3(-1, -1, -1));

			Point3 += Point1;
			Assert.IsTrue(Point3 == new Vector3(0, 0, 0));

			Point3 += Point2;
			Assert.IsTrue(Point3 == new Vector3(2, 2, 2));

			Point3 = new Vector3(3, -4, 5);
			Assert.IsTrue(Point3.Length > 7.07 && Point3.Length < 7.08);

			Vector3 InlineOpLeftSide = new Vector3(5.0f, -3.0f, .0f);
			Vector3 InlineOpRightSide = new Vector3(-5.0f, 4.0f, 1.0f);
			Assert.IsTrue(InlineOpLeftSide + InlineOpRightSide == new Vector3(.0f, 1.0f, 1.0f));

			Assert.IsTrue(InlineOpLeftSide - InlineOpRightSide == new Vector3(10.0f, -7.0f, -1.0f));
		}

		[Test]
		public void ScalarMultiplication()
		{
			Vector3 ScalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			Assert.IsTrue(ScalarMultiplicationArgument * -.5 == -new Vector3(2.5f, 2.0f, 1.5f));
			Assert.IsTrue(-.5 * ScalarMultiplicationArgument == -new Vector3(2.5f, 2.0f, 1.5f));
			Assert.IsTrue(5 * ScalarMultiplicationArgument == new Vector3(25.0f, 20.0f, 15.0f));

			Vector3 Point3 = new Vector3(2, 3, 4);
			Point3 *= 6;
			Assert.IsTrue(Point3.Equals(new Vector3(12, 18, 24), .01f));
		}

		[Test, Category("FixNeeded")]
		public void ScalarDivision()
		{
			Vector3 ScalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			Assert.IsTrue(ScalarMultiplicationArgument / 2 == new Vector3(2.5f, 2.0f, 1.5f));
			Assert.IsTrue(2 / ScalarMultiplicationArgument == new Vector3(2.5f, 2.0f, 1.5f));

			Vector3 Point3 = new Vector3(12, 18, 24);
			Point3 /= 6;
			Assert.IsTrue(Point3.Equals(new Vector3(2, 3, 4), .01f));
		}

		[Test]
		public void DotProduct()
		{
			Vector3 Test1 = new Vector3(10, 1, 2);
			Vector3 Test2 = new Vector3(1, 0, 0);
			double DotResult = Vector3.Dot(Test2, Test1);
			Assert.IsTrue(DotResult == 10);
		}

		[Test]
		public void CrossProduct()
		{
			Vector3 Test1 = new Vector3(10, 0, 0);
			Vector3 Test2 = new Vector3(1, 1, 0);
			Vector3 CrossResult = Vector3.Cross(Test2, Test1);
			Assert.IsTrue(CrossResult.X == 0);
			Assert.IsTrue(CrossResult.Y == 0);
			Assert.IsTrue(CrossResult.Z < 0);
		}

		[Test]
		public void Normalize()
		{
			Vector3 Point3 = new Vector3(3, -4, 5);
			Point3.Normalize();
			Assert.IsTrue(Point3.Length > 0.99 && Point3.Length < 1.01);
		}
	}
}