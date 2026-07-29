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

using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg;
using MatterHackers.Agg.Font;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Platform;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace Agg.Tests.Agg
{
	/// <summary>
	/// Covers <see cref="TypeFacePrinter.SnapBaselinesToWholePixels"/>: each text line's baseline Y
	/// is rounded to a whole pixel (the AGG truetype_test_02_win `(y + 0.5).floor()` convention) so
	/// horizontal stems stay crisp, while horizontal subpixel positioning is left alone.
	/// </summary>
	/// <remarks>
	/// Every test here mutates the process-wide <see cref="TypeFacePrinter.SnapBaselinesToWholePixels"/>
	/// flag, so they all use a keyless <c>[NotInParallel]</c> and restore the flag in a finally block.
	/// A constraint key would only serialize these tests against each other - any other text rendering
	/// test in the assembly could still race the flag while one of these holds it false.
	/// </remarks>
	public class TypeFacePrinterSnapBaselineTests
	{
		/// <summary>
		/// A fractional device-space Y translation must not move the text off whole pixels - the
		/// snap in Render() has to absorb it. The flag-off case is the negative control that proves
		/// this test can actually see the difference.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task FractionalDeviceTranslationYIsSnapped()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				ImageBuffer snappedAtWholePixel = RenderThroughTransformTranslation(0.0);
				ImageBuffer snappedAtFraction = RenderThroughTransformTranslation(0.4);
				await Assert.That(snappedAtWholePixel.Equals(snappedAtFraction, 0)).IsTrue();

				TypeFacePrinter.SnapBaselinesToWholePixels = false;
				ImageBuffer rawAtWholePixel = RenderThroughTransformTranslation(0.0);
				ImageBuffer rawAtFraction = RenderThroughTransformTranslation(0.4);
				await Assert.That(rawAtWholePixel.Equals(rawAtFraction, 0)).IsFalse();
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		/// <summary>
		/// Graphics2D.DrawString feeds its y argument in as the printer's Origin, which is baked into
		/// the vertex output rather than carried by the transform, so the local snap has to cover it.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task FractionalDrawStringYIsSnapped()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				ImageBuffer snappedAtWholePixel = DrawStringAt(8.0);
				ImageBuffer snappedAtFraction = DrawStringAt(8.4);
				await Assert.That(snappedAtWholePixel.Equals(snappedAtFraction, 0)).IsTrue();

				TypeFacePrinter.SnapBaselinesToWholePixels = false;
				ImageBuffer rawAtWholePixel = DrawStringAt(8.0);
				ImageBuffer rawAtFraction = DrawStringAt(8.4);
				await Assert.That(rawAtWholePixel.Equals(rawAtFraction, 0)).IsFalse();
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		/// <summary>
		/// Snapping must be applied to the unsnapped running line offset, not compounded line over
		/// line. With a 12.6 pixel em the baselines must land at 0, -13, -25, -38 (the snap of
		/// 0, -12.6, -25.2, -37.8) - never at 0, -13, -26, -39.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task MultiLineBaselinesSnapWithoutAccumulatingDrift()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = true;

				// 9.45pt at 96/72 pixels per point is a 12.6 pixel em, so every line advance is fractional.
				var typeFaceStyle = new StyledTypeFace(AggContext.DefaultFont, 9.45);
				await Assert.That(typeFaceStyle.EmSizeInPixels).IsEqualTo(12.6).Within(1e-9);

				List<VertexData> oneLine = DrawnVertices("H", typeFaceStyle);
				List<VertexData> fourLines = DrawnVertices("H\nH\nH\nH", typeFaceStyle);
				await Assert.That(fourLines.Count).IsEqualTo(oneLine.Count * 4);

				double[] expectedBaselines = new double[] { 0, -13, -25, -38 };
				for (int line = 0; line < expectedBaselines.Length; line++)
				{
					for (int vertex = 0; vertex < oneLine.Count; vertex++)
					{
						double baselineForVertex = fourLines[(line * oneLine.Count) + vertex].Position.Y
							- oneLine[vertex].Position.Y;
						await Assert.That(baselineForVertex).IsEqualTo(expectedBaselines[line]).Within(1e-9);
					}
				}
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		/// <summary>
		/// Text that is already whole-pixel aligned must render byte for byte the same with the flag
		/// on as with it off - snapping may only move text that was off-pixel to begin with.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task AlreadyAlignedTextIsUnchangedByTheFlag()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = false;
				ImageBuffer withoutSnapping = RenderThroughTransformTranslation(0.0);

				TypeFacePrinter.SnapBaselinesToWholePixels = true;
				ImageBuffer withSnapping = RenderThroughTransformTranslation(0.0);

				await Assert.That(withoutSnapping.Equals(withSnapping, 0)).IsTrue();
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		/// <summary>
		/// The same true device baseline must rasterize on the same pixel no matter how the Y is split
		/// between the printer's Origin and the graphics transform. Snapping happens in two places
		/// (Vertices() rounds the local baseline, Render() nudges for the device translation), so the
		/// two roundings must not add up - all three of these are a true device baseline of 9.2.
		/// </summary>
		[Test]
		[NotInParallel]
		public async Task SameDeviceBaselineSnapsIdenticallyHoweverYIsSplit()
		{
			bool wasSnapping = TypeFacePrinter.SnapBaselinesToWholePixels;
			try
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = true;

				ImageBuffer splitAcrossBoth = RenderWithYSplit(originY: 8.6, translationY: 0.6);
				ImageBuffer allInOrigin = RenderWithYSplit(originY: 9.2, translationY: 0.0);
				ImageBuffer allInTransform = RenderWithYSplit(originY: 0.0, translationY: 9.2);

				await Assert.That(allInOrigin.Equals(allInTransform, 0)).IsTrue();
				await Assert.That(splitAcrossBoth.Equals(allInOrigin, 0)).IsTrue();
			}
			finally
			{
				TypeFacePrinter.SnapBaselinesToWholePixels = wasSnapping;
			}
		}

		/// <summary>
		/// Renders "Hxy" with its baseline at <paramref name="originY"/> + <paramref name="translationY"/>
		/// in device space, splitting that total between the printer Origin (baked into the vertices)
		/// and a translation-only transform (applied by the rasterizer). X is held constant.
		/// </summary>
		private static ImageBuffer RenderWithYSplit(double originY, double translationY)
		{
			var image = new ImageBuffer(80, 24);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			graphics2D.SetTransform(Affine.NewTranslation(0, translationY));

			var printer = new TypeFacePrinter("Hxy", 12, new Vector2(4, originY));
			printer.Render(graphics2D, Color.Black);

			return image;
		}

		/// <summary>
		/// Renders "Hxy" with a whole-pixel baseline (Baseline.Text puts it at 0) through a
		/// translation-only transform whose Y carries the supplied fraction.
		/// </summary>
		private static ImageBuffer RenderThroughTransformTranslation(double fractionalY)
		{
			var image = new ImageBuffer(80, 24);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			graphics2D.SetTransform(Affine.NewTranslation(4, 8 + fractionalY));

			var printer = new TypeFacePrinter("Hxy", 12);
			printer.Render(graphics2D, Color.Black);

			return image;
		}

		private static ImageBuffer DrawStringAt(double y)
		{
			var image = new ImageBuffer(80, 24);
			Graphics2D graphics2D = image.NewGraphics2D();
			graphics2D.Clear(Color.White);
			graphics2D.DrawString("Hxy", 4, y, color: Color.Black);

			return image;
		}

		private static List<VertexData> DrawnVertices(string text, StyledTypeFace typeFaceStyle)
		{
			return new TypeFacePrinter(text, typeFaceStyle)
				.Vertices()
				.Where(vertex => vertex.Command != FlagsAndCommand.Stop)
				.ToList();
		}
	}
}
