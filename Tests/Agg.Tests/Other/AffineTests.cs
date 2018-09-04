using MatterHackers.Agg.Transform;
using NUnit.Framework;

namespace MatterHackers.Agg.Tests
{
	[TestFixture]
	public class AffineTests
	{
		[Test]
		public void invert_test()
		{
			Affine a = Affine.NewIdentity();
			a.translate(10, 10);
			Affine b = new Affine(a);
			b.invert();

			double x = 100;
			double y = 100;
			double newx = x;
			double newy = y;

			a.transform(ref newx, ref newy);
			b.transform(ref newx, ref newy);
			Assert.AreEqual(x, newx, .001);
			Assert.AreEqual(y, newy, .001);
		}

		[Test]
		public void transform_test()
		{
			Affine a = Affine.NewIdentity();
			a.translate(10, 20);

			double x = 10;
			double y = 20;
			double newx = 0;
			double newy = 0;

			a.transform(ref newx, ref newy);
			Assert.AreEqual(x, newx, .001);
			Assert.AreEqual(y, newy, .001);
		}
	}
}