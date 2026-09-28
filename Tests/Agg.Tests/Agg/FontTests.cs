using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using System.Collections.Generic;
using System.Threading.Tasks;
using TUnit.Assertions;
using TUnit.Core;

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
            await Assert.That(wrappedLines.Count).IsEqualTo(3);
            await Assert.That(wrappedLines[0]).IsEqualTo("Layer");
            await Assert.That(wrappedLines[1]).IsEqualTo("s or");
            await Assert.That(wrappedLines[2]).IsEqualTo("MM");
        }

        /// <summary>
        /// GetOffset memoises each character's advance; a new TypeFaceStyle (TextWidget.PointSize does this) must not
        /// keep answering with the advances of the old size.
        /// </summary>
        [Test]
        public async Task GetOffsetRemeasuresAfterTheTypeFaceStyleChanges()
        {
            var printer = new TypeFacePrinter("Hello", new StyledTypeFace(LiberationSansFont.Instance, 12));
            printer.GetOffset(0, 4, out var small);

            printer.TypeFaceStyle = new StyledTypeFace(LiberationSansFont.Instance, 24);
            printer.GetOffset(0, 4, out var large);

            await Assert.That(large.X).IsEqualTo(small.X * 2).Within(1e-9);
        }
    }
}
