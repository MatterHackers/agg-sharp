using MatterHackers.VectorMath;
using NUnit.Framework;
using System;

namespace MatterHackers.Agg.Image
{
	[TestFixture, Category("Agg.Image")]
	public class ImageTests
	{
		private bool ClearAndCheckImage(ImageBuffer image, Color color)
		{
			image.NewGraphics2D().Clear(color);

			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					if (image.GetPixel(x, y) != color)
					{
						return false;
					}
				}
			}

			return true;
		}

		private bool ClearAndCheckImageFloat(ImageBufferFloat image, ColorF color)
		{
			image.NewGraphics2D().Clear(color);

			switch (image.BitDepth)
			{
				case 128:
					for (int y = 0; y < image.Height; y++)
					{
						for (int x = 0; x < image.Width; x++)
						{
							ColorF pixelColor = image.GetPixel(x, y);
							if (pixelColor != color)
							{
								return false;
							}
						}
					}
					break;

				default:
					throw new NotImplementedException();
			}

			return true;
		}

		[Test]
		public void ColorHTMLTranslations()
		{
			Assert.AreEqual(new Color("#FFFFFFFF"), new Color(255, 255, 255, 255));
			Assert.AreEqual(new Color("#FFF"), new Color(255, 255, 255, 255));
			Assert.AreEqual(new Color("#FFFF"), new Color(255, 255, 255, 255));
			Assert.AreEqual(new Color("#FFFFFF"), new Color(255, 255, 255, 255));

			Assert.AreEqual(new Color("#FFFFFFA1"), new Color(255, 255, 255, 161));
			Assert.AreEqual(new Color("#A1FFFFFF"), new Color(161, 255, 255, 255));
			Assert.AreEqual(new Color("#FFA1FFFF"), new Color(255, 161, 255, 255));
			Assert.AreEqual(new Color("#FFFFA1FF"), new Color(255, 255, 161, 255));

			Assert.AreEqual(new Color("#A1FFFF"), new Color(161, 255, 255, 255));
		}

		[Test]
		public void ClearTests()
		{
			ImageBuffer clearSurface24 = new ImageBuffer(50, 50, 24, new BlenderBGR());
			Assert.IsTrue(ClearAndCheckImage(clearSurface24, Color.White), "Clear 24 to white");
			Assert.IsTrue(ClearAndCheckImage(clearSurface24, Color.Black), "Clear 24 to black");

			ImageBuffer clearSurface32 = new ImageBuffer(50, 50);
			Assert.IsTrue(ClearAndCheckImage(clearSurface32, Color.White), "Clear 32 to white");
			Assert.IsTrue(ClearAndCheckImage(clearSurface32, Color.Black), "Clear 32 to black");
			Assert.IsTrue(ClearAndCheckImage(clearSurface32, new Color(0, 0, 0, 0)), "Clear 32 to nothing");

			ImageBufferFloat clearSurface3ComponentFloat = new ImageBufferFloat(50, 50, 128, new BlenderBGRAFloat());
			Assert.IsTrue(ClearAndCheckImageFloat(clearSurface3ComponentFloat, ColorF.White), "Clear float to white");
			Assert.IsTrue(ClearAndCheckImageFloat(clearSurface3ComponentFloat, ColorF.Black), "Clear float to black");
			Assert.IsTrue(ClearAndCheckImageFloat(clearSurface3ComponentFloat, new ColorF(0, 0, 0, 0)), "Clear float to nothing");
		}

		public void ContainsTests()
		{
			// look for 24 bit
			{
				ImageBuffer imageToSearch = new ImageBuffer(150, 150, 24, new BlenderBGR());
				imageToSearch.NewGraphics2D().Circle(new Vector2(100, 100), 3, Color.Red);
				ImageBuffer circleToFind = new ImageBuffer(10, 10, 24, new BlenderBGR());
				circleToFind.NewGraphics2D().Circle(new Vector2(5, 5), 3, Color.Red);
				Assert.IsTrue(imageToSearch.Contains(circleToFind), "We should be able to find the circle.");

				ImageBuffer squareToFind = new ImageBuffer(10, 10, 24, new BlenderBGR());
				squareToFind.NewGraphics2D().FillRectangle(4, 4, 8, 8, Color.Red);
				Assert.IsTrue(!imageToSearch.Contains(squareToFind), "We should be not find a square.");
			}

			// look for 32 bit
			{
				ImageBuffer imageToSearch = new ImageBuffer(150, 150);
				imageToSearch.NewGraphics2D().Circle(new Vector2(100, 100), 3, Color.Red);
				ImageBuffer circleToFind = new ImageBuffer(10, 10);
				circleToFind.NewGraphics2D().Circle(new Vector2(5, 5), 3, Color.Red);
				Assert.IsTrue(imageToSearch.Contains(circleToFind), "We should be able to find the circle.");

				ImageBuffer squareToFind = new ImageBuffer(10, 10);
				squareToFind.NewGraphics2D().FillRectangle(4, 4, 8, 8, Color.Red);
				Assert.IsTrue(!imageToSearch.Contains(squareToFind), "We should be not find a square.");
			}
		}
	}
}