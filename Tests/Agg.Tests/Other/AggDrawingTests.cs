/*
Copyright (c) 2023, Lars Brubaker
All rights reserved.

Redistribution and use in source and binary forms, with or without
modification, are permitted provided that the following conditions are met:

1. Redistributions of source code must retain the above copyright notice, this
   list of conditions and the following disclaimer.
2. Redistributions in binary form must reproduce the above copyright notice,
   this list of conditions and the following disclaimer in the documentation
   and/or other materials provided with the distribution.

THIS SOFTWARE IS PROVIDED BY THE COPYRIGHT HOLDERS AND CONTRIBUTORS "AS IS" AND
ANY EXPRESS OR IMPLIED WARRANTIES, INCLUDING, BUT NOT LIMITED TO, THE IMPLIED
WARRANTIES OF MERCHANTABILITY AND FITNESS FOR A PARTICULAR PURPOSE ARE
DISCLAIMED. IN NO EVENT SHALL THE COPYRIGHT OWNER OR CONTRIBUTORS BE LIABLE FOR
ANY DIRECT, INDIRECT, INCIDENTAL, SPECIAL, EXEMPLARY, OR CONSEQUENTIAL DAMAGES
(INCLUDING, BUT NOT LIMITED TO, PROCUREMENT OF SUBSTITUTE GOODS OR SERVICES;
LOSS OF USE, DATA, OR PROFITS; OR BUSINESS INTERRUPTION) HOWEVER CAUSED AND
ON ANY THEORY OF LIABILITY, WHETHER IN CONTRACT, STRICT LIABILITY, OR TORT
(INCLUDING NEGLIGENCE OR OTHERWISE) ARISING IN ANY WAY OUT OF THE USE OF THIS
SOFTWARE, EVEN IF ADVISED OF THE POSSIBILITY OF SUCH DAMAGE.

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.VertexSource;
using System.IO;
using System;
using Agg.Tests.Agg;

namespace MatterHackers.Agg.Tests
{
    //
    [MhTestFixture("Opens Winforms Window")]
    public class AggDrawingTests
	{
		public static void RunAllTests()
		{
			AggDrawingTests tests = new AggDrawingTests();
			tests.DrawCircle();
			tests.DrawCurve3();
			tests.DrawCurve4();
			tests.DrawString();
			tests.StrokedShape();
		}

		private void CheckTestAgainstControl(ImageBuffer testImage, string testTypeString)
		{
            var testContext = new TestContext();

            Directory.SetCurrentDirectory(testContext.CurrentContext.TestDirectory);

			// there is an assumption that we got to save valid images at least once.
			string controlFileTga = testTypeString + " Control.tga";
			string imageFolder = "ControlImages";
			string testFailPathAndFileName = Path.Combine(imageFolder, testTypeString + " Test Fail.tga");
			ImageBuffer controlImage = new ImageBuffer();
			if (!Directory.Exists(imageFolder))
			{
				Directory.CreateDirectory(imageFolder);
			}
			string controlPathAndFileName = Path.Combine(imageFolder, controlFileTga);
			if (File.Exists(controlPathAndFileName))
			{
				ImageTgaIO.LoadImageData(controlImage, controlPathAndFileName);

				bool testIsSameAsControl = controlImage.Equals(testImage);
				if (!testIsSameAsControl)
				{
					// this image will be in the current output folder inside of imageFolder
					ImageTgaIO.Save(testImage, testFailPathAndFileName);
				}
				else if (File.Exists(testFailPathAndFileName))
				{
					// we don't want to have these confounding our results.
					File.Delete(testFailPathAndFileName);
				}

				MhAssert.True(testIsSameAsControl);
				// If you want to create new control images select SetNextStatement to inside the else condition to create them.
			}
			else
			{
				ImageTgaIO.Save(testImage, controlPathAndFileName);
			}
		}

		private void CheckTestAgainstControl(IVertexSource testVertexSource, string testTypeString)
		{
			// there is an assumption that we got to save valid vertex lists at least once.
			string controlFileTxt = testTypeString + " Control.Txt";
			string vertexSourceFolder = "ControlVertexSources";
			VertexStorage controlVertexSource = new VertexStorage();
			if (!Directory.Exists(vertexSourceFolder))
			{
				Directory.CreateDirectory(vertexSourceFolder);
			}
			string controlPathAndFileName = Path.Combine(vertexSourceFolder, controlFileTxt);
			if (File.Exists(controlPathAndFileName))
			{
				VertexSourceIO.Load(controlVertexSource, controlPathAndFileName);

				// this test the old vertex getting code
				{
					string testOldToOldFailPathAndFileName = Path.Combine(vertexSourceFolder, testTypeString + " Test Old Fail.Txt");
					bool testOldToOldIsSameAsControl = controlVertexSource.Equals(testVertexSource, oldStyle: true);
					if (!testOldToOldIsSameAsControl)
					{
						// this VertexSource will be in the current output folder inside of VertexSourceFolder
						VertexSourceIO.Save(testVertexSource, testOldToOldFailPathAndFileName, oldStyle: true);
					}
					else if (File.Exists(testOldToOldFailPathAndFileName))
					{
						// we don't want to have these confounding our results.
						File.Delete(testOldToOldFailPathAndFileName);
					}

					MhAssert.True(testOldToOldIsSameAsControl);
				}

				// this test the new vertex generator code
				if (true)
				{
					string testOldToNewFailPathAndFileName = Path.Combine(vertexSourceFolder, testTypeString + " Test New Fail.Txt");
					bool testOldToNewIsSameAsControl = controlVertexSource.Equals(testVertexSource, oldStyle: false);
					if (!testOldToNewIsSameAsControl)
					{
						// this VertexSource will be in the current output folder inside of VertexSourceFolder
						VertexSourceIO.Save(testVertexSource, testOldToNewFailPathAndFileName, oldStyle: false);
					}
					else if (File.Exists(testOldToNewFailPathAndFileName))
					{
						// we don't want to have these confounding our results.
						File.Delete(testOldToNewFailPathAndFileName);
					}

					MhAssert.True(testOldToNewIsSameAsControl);
				}
				// If you want to create new control VertexSources select SetNextStatement to inside the else condition to create them.
			}
			else
			{
				VertexSourceIO.Save(testVertexSource, controlPathAndFileName);
			}
		}

        [MhTest]
        public void DrawCircle()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			testImage.NewGraphics2D().Clear(Color.White);
			testImage.NewGraphics2D().Circle(30, 50, 20, Color.Magenta);
			testImage.NewGraphics2D().Circle(70, 50, 20, Color.Cyan);
			testImage.NewGraphics2D().Circle(50, 30.3, 20, Color.Indigo);
			testImage.NewGraphics2D().Circle(50, 70.3, 20, Color.Orange);
			testImage.NewGraphics2D().Circle(50, 50, 20, Color.Yellow);

			CheckTestAgainstControl(testImage, "DrawCicle");
			CheckTestAgainstControl(new Ellipse(0, 0, 20, 20), "ShapeCicle");
		}

        [MhTest]
        public void DrawCurve3()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			testImage.NewGraphics2D().Clear(Color.White);
			testImage.NewGraphics2D().Render(new Curve3(10, 10, 50, 90, 90, 90), Color.Black);

			CheckTestAgainstControl(testImage, "DrawCurve3");
			CheckTestAgainstControl(new Curve3(10, 10, 50, 90, 90, 90), "ShapeCurve3");
		}

        [MhTest]
        public void DrawCurve4()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			testImage.NewGraphics2D().Clear(Color.White);
			testImage.NewGraphics2D().Render(new Curve4(10, 50, 25, 10, 75, 90, 90, 50), Color.Black);

			CheckTestAgainstControl(testImage, "DrawCurve4");
			CheckTestAgainstControl(new Curve4(10, 50, 25, 10, 75, 90, 90, 50), "ShapeCurve4");
		}

        [MhTest]
        public void DrawString()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			testImage.NewGraphics2D().DrawString("Test", 30, 50, color: Color.Magenta, justification: Justification.Center);
			testImage.NewGraphics2D().DrawString("Test", 70, 50, color: Color.Cyan, justification: Justification.Center);
			testImage.NewGraphics2D().DrawString("Test", 50, 30.3, color: Color.Indigo, justification: Justification.Center);
			testImage.NewGraphics2D().DrawString("Test", 50, 70.3, color: Color.Orange, justification: Justification.Center);
			testImage.NewGraphics2D().DrawString("Test", 50, 50, color: Color.Yellow, justification: Justification.Center);

			CheckTestAgainstControl(testImage, "DrawString");

			TypeFacePrinter stringPrinterA = new TypeFacePrinter("A");
			stringPrinterA.TypeFaceStyle.FlattenCurves = false;
			CheckTestAgainstControl(stringPrinterA, "ShapeStringANotFlattened");
			stringPrinterA.TypeFaceStyle.FlattenCurves = true;
			CheckTestAgainstControl(stringPrinterA, "ShapeStringAFlattened");

			TypeFacePrinter stringPrintere = new TypeFacePrinter("e");
			stringPrintere.TypeFaceStyle.FlattenCurves = false;
			CheckTestAgainstControl(stringPrintere, "ShapeStringeNotFlattened");
			stringPrintere.TypeFaceStyle.FlattenCurves = true;
			CheckTestAgainstControl(stringPrintere, "ShapeStringeFlattened");

			TypeFacePrinter stringPrinterAe = new TypeFacePrinter("Ae");
			stringPrinterAe.TypeFaceStyle.FlattenCurves = false;
			CheckTestAgainstControl(stringPrinterAe, "ShapeStringAeNotFlattened");
			stringPrinterAe.TypeFaceStyle.FlattenCurves = true;
			CheckTestAgainstControl(stringPrinterAe, "ShapeStringAeFlattened");

			TypeFacePrinter stringPrinterTest = new TypeFacePrinter("Test");
			stringPrinterTest.TypeFaceStyle.FlattenCurves = false;
			CheckTestAgainstControl(stringPrinterTest, "ShapeStringTestNotFlattened");
			stringPrinterTest.TypeFaceStyle.FlattenCurves = true;
			CheckTestAgainstControl(stringPrinterTest, "ShapeStringTestFlattened");
		}

        [MhTest]
        public void StrokedShape()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			RoundedRect rect = new RoundedRect(20, 20, 80, 80, 5);
			Stroke rectOutline = new Stroke(rect, 1);
			testImage.NewGraphics2D().Render(rectOutline, Color.White);

			CheckTestAgainstControl(testImage, "DrawStroked");
			CheckTestAgainstControl(rectOutline, "ShapeStroked");
		}
	}
}