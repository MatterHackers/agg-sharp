using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using System.Collections.Generic;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace Agg.Tests.Agg
{
    
    public class FontTests
    {
        [Test]
        public async Task CanPrintTests()
        {
            // Invoke DrawString with a carriage return. If any part of the font pipeline throws, this test fails
            ImageBuffer testImage = new ImageBuffer(300, 300);
            testImage.NewGraphics2D().DrawString("\r", 30, 30);
        }

        [Test]
        public async Task TextWrappingTest()
        {
            EnglishTextWrapping englishWrapping = new EnglishTextWrapping(8);
            List<string> wrappedLines = englishWrapping.WrapSingleLineOnWidth("Layers or MM", 30);
            Assert.IsTrue(wrappedLines.Count == 3);
            Assert.IsTrue(wrappedLines[0] == "Layer");
            Assert.IsTrue(wrappedLines[1] == "s or");
            Assert.IsTrue(wrappedLines[2] == "MM");
        }
    }
}
