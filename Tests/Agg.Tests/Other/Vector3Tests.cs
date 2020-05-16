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
			var point1 = default(Vector3);
			point1 = new Vector3(1, 1, 1);

			var point2 = default(Vector3);
			point2 = new Vector3(2, 2, 2);

			var point3 = default(Vector3);
			point3 = Vector3.Add(point1, point2);
			Assert.IsTrue(point3 == new Vector3(3, 3, 3));

			point3 = point1 - point2;
			Assert.IsTrue(point3 == new Vector3(-1, -1, -1));

			point3 += point1;
			Assert.IsTrue(point3 == new Vector3(0, 0, 0));

			point3 += point2;
			Assert.IsTrue(point3 == new Vector3(2, 2, 2));

			point3 = new Vector3(3, -4, 5);
			Assert.IsTrue(point3.Length > 7.07 && point3.Length < 7.08);

			var inlineOpLeftSide = new Vector3(5.0f, -3.0f, .0f);
			var inlineOpRightSide = new Vector3(-5.0f, 4.0f, 1.0f);
			Assert.IsTrue(inlineOpLeftSide + inlineOpRightSide == new Vector3(.0f, 1.0f, 1.0f));

			Assert.IsTrue(inlineOpLeftSide - inlineOpRightSide == new Vector3(10.0f, -7.0f, -1.0f));
		}

		[Test]
		public void ScalarMultiplication()
		{
			var scalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			Assert.IsTrue(scalarMultiplicationArgument * -.5 == -new Vector3(2.5f, 2.0f, 1.5f));
			Assert.IsTrue(-.5 * scalarMultiplicationArgument == -new Vector3(2.5f, 2.0f, 1.5f));
			Assert.IsTrue(5 * scalarMultiplicationArgument == new Vector3(25.0f, 20.0f, 15.0f));

			var point3 = new Vector3(2, 3, 4);
			point3 *= 6;
			Assert.IsTrue(point3.Equals(new Vector3(12, 18, 24), .01f));
		}

		[Test]
		public void ScalarDivision()
		{
			var scalarMultiplicationArgument = new Vector3(5.0f, 4.0f, 3.0f);
			Assert.IsTrue(scalarMultiplicationArgument / 2 == new Vector3(2.5f, 2.0f, 1.5f));

			var point3 = new Vector3(12, 18, 24);
			point3 /= 6;
			Assert.IsTrue(point3.Equals(new Vector3(2, 3, 4), .01f));
		}

		[Test]
		public void DotProduct()
		{
			var test1 = new Vector3(10, 1, 2);
			var test2 = new Vector3(1, 0, 0);
			double dotResult = Vector3Ex.Dot(test2, test1);
			Assert.IsTrue(dotResult == 10);
		}

		[Test]
		public void CrossProduct()
		{
			var test1 = new Vector3(10, 0, 0);
			var test2 = new Vector3(1, 1, 0);
			Vector3 crossResult = Vector3Ex.Cross(test2, test1);
			Assert.IsTrue(crossResult.X == 0);
			Assert.IsTrue(crossResult.Y == 0);
			Assert.IsTrue(crossResult.Z < 0);
		}

		[Test]
		public void Normalize()
		{
			var point3 = new Vector3(3, -4, 5);
			point3.Normalize();
			Assert.IsTrue(point3.Length > 0.99 && point3.Length < 1.01);
		}
	}
}