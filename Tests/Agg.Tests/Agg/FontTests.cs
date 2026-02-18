using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using System.Collections.Generic;

namespace Agg.Tests.Agg
{
    [MhTestFixture("Agg.Font")]
    public class FontTests
    {
        [MhTest]
        public void CanPrintTests()
        {
            // Invoke DrawString with a carriage return. If any part of the font pipeline throws, this test fails
            ImageBuffer testImage = new ImageBuffer(300, 300);
            testImage.NewGraphics2D().DrawString("\r", 30, 30);
        }

        [MhTest]
        public void TextWrappingTest()
        {
            EnglishTextWrapping englishWrapping = new EnglishTextWrapping(8);
            List<string> wrappedLines = englishWrapping.WrapSingleLineOnWidth("Layers or MM", 30);
            MhAssert.True(wrappedLines.Count == 3);
            MhAssert.True(wrappedLines[0] == "Layer");
            MhAssert.True(wrappedLines[1] == "s or");
            MhAssert.True(wrappedLines[2] == "MM");
        }
    }
}