using MatterHackers.Agg.Image;
using NUnit.Framework;

namespace MatterHackers.Agg.Font
{
	[TestFixture, Category("Agg.Font")]
	public class FontTests
	{
		[Test]
		public void CanPrintTests()
		{
			ImageBuffer testImage = new ImageBuffer(300, 300, 32, new BlenderBGRA());
			testImage.NewGraphics2D().DrawString("\r", 30, 30);
			Assert.IsTrue(true, "We can print only a \\r");
		}
	}
}