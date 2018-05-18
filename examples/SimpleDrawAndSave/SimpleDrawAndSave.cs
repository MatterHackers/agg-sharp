// Much of the ui to the drawing functions still needs to be C#'ed and cleaned up.  A lot of
// it still follows the originall agg function names.  I have been cleaning these up over time
// and intend to do much more refactoring of these things over the long term.

using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using System;

namespace MatterHackers.Agg
{
	public class SimpleDrawAndSave
	{
		[STAThread]
		public static void Main(string[] args)
		{
			// first we will show how to use the simple drawing functions in graphics 2D
			{
				ImageBuffer simpleImage = new ImageBuffer(640, 480, 32, new BlenderBGRA());
				Graphics2D simpleImageGraphics2D = simpleImage.NewGraphics2D();
				// clear the image to white
				simpleImageGraphics2D.Clear(Color.White);
				// draw a circle
				simpleImageGraphics2D.Circle(50, 50, 30, Color.Blue);
				// draw a line
				simpleImageGraphics2D.Line(10, 100, 520, 50, new Color(20, 200, 200));
				// draw a filled box
				simpleImageGraphics2D.FillRectangle(60, 260, 200, 280, Color.Yellow);
				// and an outline around it
				simpleImageGraphics2D.Rectangle(60, 260, 200, 280, Color.Magenta);
				// draw some text
				simpleImageGraphics2D.DrawString("A Simple Example", 300, 400, 20);

				// and save this image out
				ImageTgaIO.Save(simpleImage, "SimpleDrawAndSave.tga");
			}

			// now we will we will show how to use the render function to draw more complex things
			{
				ImageBuffer lessSimpleImage = new ImageBuffer(640, 480, 32, new BlenderBGRA());
				Graphics2D lessSimpleImageGraphics2D = lessSimpleImage.NewGraphics2D();
				// clear the image to white
				lessSimpleImageGraphics2D.Clear(Color.White);
				// draw a circle
				Ellipse ellipseTest = new Ellipse(0, 0, 100, 50);
				for (double angleDegrees = 0; angleDegrees < 180; angleDegrees += 22.5)
				{
					VertexSourceApplyTransform rotatedTransform = new VertexSourceApplyTransform(ellipseTest, Affine.NewRotation(MathHelper.DegreesToRadians(angleDegrees)));
					VertexSourceApplyTransform rotatedAndTranslatedTransform = new VertexSourceApplyTransform(rotatedTransform, Affine.NewTranslation(lessSimpleImage.Width / 2, 150));
					lessSimpleImageGraphics2D.Render(rotatedAndTranslatedTransform, Color.Yellow);
					Stroke ellipseOutline = new Stroke(rotatedAndTranslatedTransform, 3);
					lessSimpleImageGraphics2D.Render(ellipseOutline, Color.Blue);
				}

				// and a little polygon
				VertexStorage littlePoly = new VertexStorage();
				littlePoly.MoveTo(50, 50);
				littlePoly.LineTo(150, 50);
				littlePoly.LineTo(200, 200);
				littlePoly.LineTo(50, 150);
				littlePoly.LineTo(50, 50);
				lessSimpleImageGraphics2D.Render(littlePoly, Color.Cyan);

				// draw some text
				TypeFacePrinter textPrinter = new TypeFacePrinter("Printing from a printer", 30, justification: Justification.Center);
				IVertexSource translatedText = new VertexSourceApplyTransform(textPrinter, Affine.NewTranslation(new Vector2(lessSimpleImage.Width / 2, lessSimpleImage.Height / 4 * 3)));
				lessSimpleImageGraphics2D.Render(translatedText, Color.Red);
				Stroke strokedText = new Stroke(translatedText);
				lessSimpleImageGraphics2D.Render(strokedText, Color.Black);

				IVertexSource rotatedText = new VertexSourceApplyTransform(textPrinter, Affine.NewRotation(MathHelper.DegreesToRadians(90)));
				IVertexSource rotatedTranslatedText = new VertexSourceApplyTransform(rotatedText, Affine.NewTranslation(new Vector2(40, lessSimpleImage.Height / 2)));
				lessSimpleImageGraphics2D.Render(rotatedTranslatedText, Color.Black);

				// and save this image out
				ImageTgaIO.Save(lessSimpleImage, "LessSimpleDrawAndSave.tga");
			}
		}
	}
}