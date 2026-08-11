/*
Copyright (c) 2026, Lars Brubaker
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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.VertexSource;
using System.Collections.Generic;
using System.IO;
using System;
using Agg.Tests.Agg;
using System.Threading.Tasks;

namespace MatterHackers.Agg.Tests
{
	/// <summary>
	/// Pixel-exact drawing regressions: every test here renders something and compares it byte for byte
	/// against a checked-in control image (or control vertex list), so any change to the rasterizer, the
	/// curve flattener or the text pipeline has to be noticed and deliberately adopted.
	/// </summary>
	/// <remarks>
	/// The whole class is <c>[NotInParallel]</c> because the text tests drive two process-wide statics -
	/// <see cref="TypeFacePrinter.SnapBaselinesToWholePixels"/> and <see cref="LcdRenderSettings.Enabled"/> -
	/// and so do <c>TypeFacePrinterSnapBaselineTests</c> and <c>TypeFacePrinterLcdTests</c>, which already
	/// follow the convention of a keyless <c>[NotInParallel]</c> plus a restoring <c>finally</c>. This class
	/// did not, and a pixel-exact text test that renders while another test has the snap flag inverted fails:
	/// dropping the attribute makes both <see cref="DrawString"/> and
	/// <see cref="DrawStringCoversEverySnapAndLcdCombination"/> fail in a full-suite run, reliably enough to
	/// see in two runs out of two. Each test also sets both statics explicitly and restores them, so a control
	/// image can never depend on whatever the ambient default happened to be.
	/// </remarks>
	[NotInParallel]
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

		/// <summary>Folder name holding the control images, both in the tree and beside the test binary.</summary>
		private const string ControlImageFolderName = "ControlImages";

		private static string locatedControlImageDirectory;

		/// <summary>
		/// Compares a rendered image against its checked-in control image, failing if they differ or if no
		/// control image exists.
		/// </summary>
		/// <remarks>
		/// The control images are <b>checked-in goldens</b> (<c>TestData\ControlImages</c>), not artifacts the
		/// test may create for itself. This used to work the other way round: the controls lived only in the
		/// output folder and a missing one was written from whatever had just been rendered, which meant a
		/// fresh checkout silently certified its own output and an intentional rendering change could leave a
		/// stale control failing runs long afterwards - the whole-pixel baseline snapping added to
		/// <see cref="TypeFacePrinter"/> did exactly that to <see cref="DrawString"/>, months later.
		/// <para>
		/// A mismatch (or a missing control) writes what was actually rendered to
		/// <c>ControlImageFailures\&lt;name&gt; Test Fail.tga</c> beside the test binary and names the path in
		/// the failure message. To adopt a deliberate rendering change: look at that image, satisfy yourself it
		/// is right, then copy it over <c>TestData\ControlImages\&lt;name&gt; Control.tga</c> and commit it, so
		/// the change is reviewed rather than absorbed.
		/// </para>
		/// </remarks>
		private async Task CheckTestAgainstControl(ImageBuffer testImage, string testTypeString)
		{
			string controlFileTga = testTypeString + " Control.tga";
			string controlPathAndFileName = Path.Combine(ControlImageDirectory(), controlFileTga);
			string testFailPathAndFileName = Path.Combine(
				AppContext.BaseDirectory,
				"ControlImageFailures",
				testTypeString + " Test Fail.tga");

			if (!File.Exists(controlPathAndFileName))
			{
				SaveForInspection(testImage, testFailPathAndFileName);
				await Assert.That(false).IsTrue().Because(
					$"there is no control image for '{testTypeString}'. Expected '{controlPathAndFileName}'."
					+ $" What this run rendered has been written to '{testFailPathAndFileName}' - check that it"
					+ $" is correct, then copy it there as '{controlFileTga}' and commit it.");
				return;
			}

			var controlImage = new ImageBuffer();
			ImageTgaIO.LoadImageData(controlImage, controlPathAndFileName);

			bool testIsSameAsControl = controlImage.Equals(testImage);
			if (!testIsSameAsControl)
			{
				SaveForInspection(testImage, testFailPathAndFileName);
			}
			else if (File.Exists(testFailPathAndFileName))
			{
				// we don't want to have these confounding our results.
				File.Delete(testFailPathAndFileName);
			}

			await Assert.That(testIsSameAsControl).IsTrue().Because(
				$"'{testTypeString}' must render exactly as '{controlPathAndFileName}'. This run rendered"
				+ $" '{testFailPathAndFileName}' instead - if that is the new correct output, copy it over the"
				+ " control image and commit it.");
		}

		/// <summary>
		/// Writes <paramref name="image"/> where a human can look at it, replacing anything already there so a
		/// previous run's output cannot be mistaken for this one's.
		/// </summary>
		private static void SaveForInspection(ImageBuffer image, string pathAndFileName)
		{
			Directory.CreateDirectory(Path.GetDirectoryName(pathAndFileName));
			if (File.Exists(pathAndFileName))
			{
				File.Delete(pathAndFileName);
			}

			ImageTgaIO.Save(image, pathAndFileName);
		}

		/// <summary>
		/// Finds <c>TestData\ControlImages</c> by walking up from the test binary to the repository root, and
		/// only falls back to the copy the csproj puts beside the binary if no parent directory has one.
		/// </summary>
		/// <remarks>
		/// Preferring the tree is the same choice - for the same reason - as
		/// <c>LcdFixtureManifest.LocateFixtureDirectory</c>: the output copy exists so the exe still runs from
		/// a bare <c>bin\</c>, but <c>PreserveNewest</c> only compares timestamps, so a copy that did not
		/// happen would leave <c>bin\</c> shadowing the checked-in goldens and the tests would keep passing
		/// against images nobody can see in a diff. That shadowing is not hypothetical here: this test class
		/// spent a year comparing against exactly such an invisible copy.
		/// </remarks>
		private static string ControlImageDirectory()
		{
			if (locatedControlImageDirectory != null)
			{
				return locatedControlImageDirectory;
			}

			var candidates = new List<string>();
			string probe = Path.GetDirectoryName(AppContext.BaseDirectory.TrimEnd(Path.DirectorySeparatorChar));
			for (int up = 0; up < 6 && probe != null; up++)
			{
				candidates.Add(Path.Combine(probe, "TestData", ControlImageFolderName));
				probe = Path.GetDirectoryName(probe);
			}

			string besideTheBinary = Path.Combine(AppContext.BaseDirectory, ControlImageFolderName);
			candidates.Add(besideTheBinary);

			foreach (string candidate in candidates)
			{
				if (Directory.Exists(candidate))
				{
					locatedControlImageDirectory = candidate;
					return locatedControlImageDirectory;
				}
			}

			// Nothing to compare against: report it against the output folder, which is where the failure
			// message will then tell the caller to look.
			locatedControlImageDirectory = besideTheBinary;

			return locatedControlImageDirectory;
		}

		private async Task CheckTestAgainstControl(IVertexSource testVertexSource, string testTypeString)
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

					await Assert.That(testOldToOldIsSameAsControl).IsTrue();
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

					await Assert.That(testOldToNewIsSameAsControl).IsTrue();
				}
				// If you want to create new control VertexSources select SetNextStatement to inside the else condition to create them.
			}
			else
			{
				VertexSourceIO.Save(testVertexSource, controlPathAndFileName);
			}
		}

        [Test]
        public async Task DrawCircle()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			testImage.NewGraphics2D().Clear(Color.White);
			testImage.NewGraphics2D().Circle(30, 50, 20, Color.Magenta);
			testImage.NewGraphics2D().Circle(70, 50, 20, Color.Cyan);
			testImage.NewGraphics2D().Circle(50, 30.3, 20, Color.Indigo);
			testImage.NewGraphics2D().Circle(50, 70.3, 20, Color.Orange);
			testImage.NewGraphics2D().Circle(50, 50, 20, Color.Yellow);

			await CheckTestAgainstControl(testImage, "DrawCicle");
			await CheckTestAgainstControl(new Ellipse(0, 0, 20, 20), "ShapeCicle");
		}

        [Test]
        public async Task DrawCurve3()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			testImage.NewGraphics2D().Clear(Color.White);
			testImage.NewGraphics2D().Render(new Curve3(10, 10, 50, 90, 90, 90), Color.Black);

			await CheckTestAgainstControl(testImage, "DrawCurve3");
			await CheckTestAgainstControl(new Curve3(10, 10, 50, 90, 90, 90), "ShapeCurve3");
		}

        [Test]
        public async Task DrawCurve4()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			testImage.NewGraphics2D().Clear(Color.White);
			testImage.NewGraphics2D().Render(new Curve4(10, 50, 25, 10, 75, 90, 90, 50), Color.Black);

			await CheckTestAgainstControl(testImage, "DrawCurve4");
			await CheckTestAgainstControl(new Curve4(10, 50, 25, 10, 75, 90, 90, 50), "ShapeCurve4");
		}

		/// <summary>
		/// The ordinary text path at the shipping defaults - baselines snapped, LCD off - plus the glyph
		/// outlines themselves, flattened and not.
		/// </summary>
		/// <remarks>
		/// Both statics are set explicitly rather than left at their defaults so this control image cannot
		/// depend on what some other test left behind. The snap-off and LCD-on rasters are covered by
		/// <see cref="DrawStringCoversEverySnapAndLcdCombination"/>.
		/// </remarks>
		[Test]
        public async Task DrawString()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			bool wasLcdEnabled = LcdRenderSettings.Enabled;
			try
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				LcdRenderSettings.Enabled = false;

				ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
				testImage.NewGraphics2D().DrawString("Test", 30, 50, color: Color.Magenta, justification: Justification.Center);
				testImage.NewGraphics2D().DrawString("Test", 70, 50, color: Color.Cyan, justification: Justification.Center);
				testImage.NewGraphics2D().DrawString("Test", 50, 30.3, color: Color.Indigo, justification: Justification.Center);
				testImage.NewGraphics2D().DrawString("Test", 50, 70.3, color: Color.Orange, justification: Justification.Center);
				testImage.NewGraphics2D().DrawString("Test", 50, 50, color: Color.Yellow, justification: Justification.Center);

				await CheckTestAgainstControl(testImage, "DrawString");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Enabled = wasLcdEnabled;
			}

			TypeFacePrinter stringPrinterA = new TypeFacePrinter("A");
			stringPrinterA.TypeFaceStyle.FlattenCurves = false;
			await CheckTestAgainstControl(stringPrinterA, "ShapeStringANotFlattened");
			stringPrinterA.TypeFaceStyle.FlattenCurves = true;
			await CheckTestAgainstControl(stringPrinterA, "ShapeStringAFlattened");

			TypeFacePrinter stringPrintere = new TypeFacePrinter("e");
			stringPrintere.TypeFaceStyle.FlattenCurves = false;
			await CheckTestAgainstControl(stringPrintere, "ShapeStringeNotFlattened");
			stringPrintere.TypeFaceStyle.FlattenCurves = true;
			await CheckTestAgainstControl(stringPrintere, "ShapeStringeFlattened");

			TypeFacePrinter stringPrinterAe = new TypeFacePrinter("Ae");
			stringPrinterAe.TypeFaceStyle.FlattenCurves = false;
			await CheckTestAgainstControl(stringPrinterAe, "ShapeStringAeNotFlattened");
			stringPrinterAe.TypeFaceStyle.FlattenCurves = true;
			await CheckTestAgainstControl(stringPrinterAe, "ShapeStringAeFlattened");

			TypeFacePrinter stringPrinterTest = new TypeFacePrinter("Test");
			stringPrinterTest.TypeFaceStyle.FlattenCurves = false;
			await CheckTestAgainstControl(stringPrinterTest, "ShapeStringTestNotFlattened");
			stringPrinterTest.TypeFaceStyle.FlattenCurves = true;
			await CheckTestAgainstControl(stringPrinterTest, "ShapeStringTestFlattened");
		}

		/// <summary>
		/// Text can be rasterized four ways - whole-pixel baseline snapping on or off, crossed with LCD
		/// subpixel coverage on or off - and each of the four is pinned to its own control image.
		/// </summary>
		/// <remarks>
		/// One control per combination is the point: the snap controls <i>where</i> a fractional baseline lands
		/// and LCD controls <i>what</i> is written per channel, so a change to either shows up as exactly two
		/// of the four failing, which says which feature moved. The sample deliberately mixes whole and
		/// fractional Y - a text run at y = 50 is unaffected by the snap, so without the 30.3 and 70.3 runs the
		/// snap-on and snap-off controls would be the same image and half this matrix would prove nothing.
		/// <para>
		/// The pairwise assertions at the end are what keep the controls honest. Four images compared against
		/// four files would still pass if some future change made a flag do nothing at all - the controls would
		/// have been blessed identical and stay identical. Requiring the four to differ from each other, and
		/// requiring chroma exactly where LCD is on, means each control is known to be its own raster.
		/// </para>
		/// <para>
		/// The destination is opaque white with a premultiplied blender because that is what LCD subpixel
		/// coverage is valid against (see <c>ImageGraphics2D.CanCompositeLcd</c>); it is used for all four so
		/// the flags are the only thing that varies. Black ink on white is also the only way the chroma check
		/// means anything - a coloured run has channel differences of its own.
		/// </para>
		/// </remarks>
		[Test]
		public async Task DrawStringCoversEverySnapAndLcdCombination()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			bool wasLcdEnabled = LcdRenderSettings.Enabled;
			try
			{
				ImageBuffer snapped = RenderTextSample(snapBaselines: true, lcdSubpixel: false);
				ImageBuffer unsnapped = RenderTextSample(snapBaselines: false, lcdSubpixel: false);
				ImageBuffer lcdSnapped = RenderTextSample(snapBaselines: true, lcdSubpixel: true);
				ImageBuffer lcdUnsnapped = RenderTextSample(snapBaselines: false, lcdSubpixel: true);

				await CheckTestAgainstControl(snapped, "DrawString Snap");
				await CheckTestAgainstControl(unsnapped, "DrawString NoSnap");
				await CheckTestAgainstControl(lcdSnapped, "DrawString Lcd Snap");
				await CheckTestAgainstControl(lcdUnsnapped, "DrawString Lcd NoSnap");

				await Assert.That(snapped.Equals(unsnapped)).IsFalse()
					.Because("the baseline snap has to move the fractional-Y runs");
				await Assert.That(lcdSnapped.Equals(lcdUnsnapped)).IsFalse()
					.Because("the baseline snap has to move them on the LCD path too");
				await Assert.That(lcdSnapped.Equals(snapped)).IsFalse()
					.Because("the LCD raster has to differ from the ordinary anti-aliased one");
				await Assert.That(lcdUnsnapped.Equals(unsnapped)).IsFalse()
					.Because("and that must not depend on the snap");

				await Assert.That(HasChroma(lcdSnapped)).IsTrue()
					.Because("LCD text is per-channel coverage, so black on white must carry chroma");
				await Assert.That(HasChroma(lcdUnsnapped)).IsTrue()
					.Because("LCD text is per-channel coverage, so black on white must carry chroma");
				await Assert.That(HasChroma(snapped)).IsFalse()
					.Because("black on white through the ordinary path is neutral gray");
				await Assert.That(HasChroma(unsnapped)).IsFalse()
					.Because("black on white through the ordinary path is neutral gray");
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
				LcdRenderSettings.Enabled = wasLcdEnabled;
			}
		}

		/// <summary>
		/// The same three text runs - two on fractional baselines, one on a whole one - rendered under a given
		/// combination of baseline snapping and LCD subpixel coverage.
		/// </summary>
		/// <remarks>
		/// <see cref="LcdMaskCache"/> is cleared first so a raster cached by an earlier combination cannot be
		/// what this one paints; the cache keys on the toggles' epoch, but a test whose whole job is telling
		/// the four rasters apart should not be resting on that.
		/// </remarks>
		private static ImageBuffer RenderTextSample(bool snapBaselines, bool lcdSubpixel)
		{
			TypeFacePrinter.SnapBaselinesToWholePixels = snapBaselines;
			LcdRenderSettings.Enabled = lcdSubpixel;
			LcdMaskCache.Clear();

			var testImage = new ImageBuffer(100, 100, 32, new BlenderPreMultBGRA());
			Graphics2D graphics = testImage.NewGraphics2D();
			graphics.Clear(new Color(255, 255, 255, 255));
			graphics.DrawString("Test", 50, 30.3, color: Color.Black, justification: Justification.Center);
			graphics.DrawString("Test", 50, 50, color: Color.Black, justification: Justification.Center);
			graphics.DrawString("Test", 50, 70.3, color: Color.Black, justification: Justification.Center);

			return testImage;
		}

		/// <summary>
		/// Whether any pixel's channels differ from each other - the defining mark of subpixel coverage. Every
		/// run compared with this is black on white, so nothing else could introduce a channel difference.
		/// </summary>
		private static bool HasChroma(ImageBuffer image)
		{
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					Color pixel = image.GetPixel(x, y);
					if (pixel.red != pixel.green || pixel.green != pixel.blue)
					{
						return true;
					}
				}
			}

			return false;
		}

        [Test]
        public async Task StrokedShape()
		{
			ImageBuffer testImage = new ImageBuffer(100, 100, 32, new BlenderBGRA());
			RoundedRect rect = new RoundedRect(20, 20, 80, 80, 5);
			Stroke rectOutline = new Stroke(rect, 1);
			testImage.NewGraphics2D().Render(rectOutline, Color.White);

			await CheckTestAgainstControl(testImage, "DrawStroked");
			await CheckTestAgainstControl(rectOutline, "ShapeStroked");
		}
	}
}
