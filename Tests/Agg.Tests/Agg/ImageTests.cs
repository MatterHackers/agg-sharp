using Agg.Tests.Agg;
using MatterHackers.VectorMath;
using System;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.Image
{
	
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
		public async Task ColorHTMLTranslations()
		{
			await Assert.That(new Color("#FFFFFFFF")).IsEqualTo(new Color(255, 255, 255, 255));
			await Assert.That(new Color("#FFF")).IsEqualTo(new Color(255, 255, 255, 255));
			await Assert.That(new Color("#FFFF")).IsEqualTo(new Color(255, 255, 255, 255));
			await Assert.That(new Color("#FFFFFF")).IsEqualTo(new Color(255, 255, 255, 255));

			await Assert.That(new Color("#FFFFFFA1")).IsEqualTo(new Color(255, 255, 255, 161));
			await Assert.That(new Color("#A1FFFFFF")).IsEqualTo(new Color(161, 255, 255, 255));
			await Assert.That(new Color("#FFA1FFFF")).IsEqualTo(new Color(255, 161, 255, 255));
			await Assert.That(new Color("#FFFFA1FF")).IsEqualTo(new Color(255, 255, 161, 255));

			await Assert.That(new Color("#A1FFFF")).IsEqualTo(new Color(161, 255, 255, 255));
		}

		[Test]
		public async Task ClearTests()
		{
			ImageBuffer clearSurface24 = new ImageBuffer(50, 50, 24, new BlenderBGR());
			await Assert.That(ClearAndCheckImage(clearSurface24, Color.White)).IsTrue(); //, "Clear 24 to white");
			await Assert.That(ClearAndCheckImage(clearSurface24, Color.Black)).IsTrue(); //, "Clear 24 to black");

			ImageBuffer clearSurface32 = new ImageBuffer(50, 50);
			await Assert.That(ClearAndCheckImage(clearSurface32, Color.White)).IsTrue(); //, "Clear 32 to white");
			await Assert.That(ClearAndCheckImage(clearSurface32, Color.Black)).IsTrue(); //, "Clear 32 to black");
			await Assert.That(ClearAndCheckImage(clearSurface32, new Color(0, 0, 0, 0))).IsTrue(); //, "Clear 32 to nothing");

			ImageBufferFloat clearSurface3ComponentFloat = new ImageBufferFloat(50, 50, 128, new BlenderBGRAFloat());
			await Assert.That(ClearAndCheckImageFloat(clearSurface3ComponentFloat, ColorF.White)).IsTrue(); //, "Clear float to white");
			await Assert.That(ClearAndCheckImageFloat(clearSurface3ComponentFloat, ColorF.Black)).IsTrue(); //, "Clear float to black");
			await Assert.That(ClearAndCheckImageFloat(clearSurface3ComponentFloat, new ColorF(0, 0, 0, 0))).IsTrue(); //, "Clear float to nothing");
		}

		[Test]
		public async Task ContainsTests()
		{
			// look for 24 bit
			{
				ImageBuffer imageToSearch = new ImageBuffer(150, 150, 24, new BlenderBGR());
				imageToSearch.NewGraphics2D().Circle(new Vector2(100, 100), 3, Color.Red);
				ImageBuffer circleToFind = new ImageBuffer(10, 10, 24, new BlenderBGR());
				circleToFind.NewGraphics2D().Circle(new Vector2(5, 5), 3, Color.Red);
				await Assert.That(imageToSearch.Contains(circleToFind)).IsTrue(); //, "We should be able to find the circle.");

				ImageBuffer squareToFind = new ImageBuffer(10, 10, 24, new BlenderBGR());
				squareToFind.NewGraphics2D().FillRectangle(4, 4, 8, 8, Color.Red);
				await Assert.That(imageToSearch.Contains(squareToFind)).IsFalse(); //, "We should be not find a square.");
			}

			// look for 32 bit
			{
				ImageBuffer imageToSearch = new ImageBuffer(150, 150);
				imageToSearch.NewGraphics2D().Circle(new Vector2(100, 100), 3, Color.Red);
				ImageBuffer circleToFind = new ImageBuffer(10, 10);
				circleToFind.NewGraphics2D().Circle(new Vector2(5, 5), 3, Color.Red);
				await Assert.That(imageToSearch.Contains(circleToFind)).IsTrue(); //, "We should be able to find the circle.");

				ImageBuffer squareToFind = new ImageBuffer(10, 10);
				squareToFind.NewGraphics2D().FillRectangle(4, 4, 8, 8, Color.Red);
				await Assert.That(imageToSearch.Contains(squareToFind)).IsFalse(); //, "We should be not find a square.");
			}
		}
	}
}
