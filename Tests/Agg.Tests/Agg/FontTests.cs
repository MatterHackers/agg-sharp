using MatterHackers.Agg.Image;
using Xunit;
using System.Collections.Generic;

namespace MatterHackers.Agg.Font
{
	//[TestFixture, Category("Agg.Font")]
	public class FontTests
	{
		[StaFact]
		public void CanPrintTests()
		{
			// Invoke DrawString with a carriage return. If any part of the font pipeline throws, this test fails
			ImageBuffer testImage = new ImageBuffer(300, 300);
			testImage.NewGraphics2D().DrawString("\r", 30, 30);
		}

		[StaFact]
		public void TextWrappingTest()
		{
			EnglishTextWrapping englishWrapping = new EnglishTextWrapping(8);
			List<string> wrappedLines = englishWrapping.WrapSingleLineOnWidth("Layers or MM", 30);
			Assert.True(wrappedLines.Count == 3);
			Assert.True(wrappedLines[0] == "Layer");
			Assert.True(wrappedLines[1] == "s or");
			Assert.True(wrappedLines[2] == "MM");
		}
	}
}