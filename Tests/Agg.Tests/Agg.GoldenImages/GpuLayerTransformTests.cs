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
*/

using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.Agg.VertexSource;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// A retained GPU layer composites through the whole graphics transform - rotation, shear and flips as well
	/// as translation and scale - the contract <see cref="Graphics2D.Render(IImageByte, double, double, double, double, double)"/>
	/// documents for an image draw. Rendered on the real WebGPU device and compared with the same content drawn
	/// straight onto the frame under the same transform.
	/// </summary>
	[NotInParallel]
	public class GpuLayerTransformTests
	{
		private const int FrameWidth = 128;
		private const int FrameHeight = 64;

		private static readonly ColorF Background = new ColorF(0.2f, 0.3f, 0.4f, 1);

		/// <summary>Opaque, translucent and anti-aliased ink that is not symmetric, so a lost turn, a flip or a
		/// shift each shows.</summary>
		private class InkWidget : GuiWidget
		{
			public InkWidget()
				: base(40, 30)
			{
				this.BackgroundColor = new Color(255, 255, 255, 160);
			}

			public override void OnDraw(Graphics2D graphics2D)
			{
				graphics2D.FillRectangle(2, 2, 14, 10, Color.Red);
				graphics2D.Render(new Ellipse(this.Width - 10, this.Height - 9, 7, 6), new Color(0, 160, 0, 200));
				graphics2D.Line(0, 0, this.Width, this.Height, Color.Black, 1.5);
				base.OnDraw(graphics2D);
			}
		}

		public static Affine[] Transforms()
		{
			return new[]
			{
				// A turn: the case that used to keep only the translation and the diagonal.
				Affine.NewRotation(MathHelper.DegreesToRadians(30)) * Affine.NewTranslation(64, 8),
				// A y-flip about a whole-pixel line.
				Affine.NewScaling(1, -1) * Affine.NewTranslation(20, 50),
				// A shear.
				Affine.NewSkewing(0.3, 0) * Affine.NewTranslation(30, 16),
			};
		}

		/// <summary>
		/// A widget backbuffer in <see cref="BackbufferMode.GpuTexture"/> composited under a turned, flipped or
		/// sheared transform lands where the widget drawn unbuffered under that transform does.
		/// </summary>
		[Test]
		[MethodDataSource(nameof(Transforms))]
		public async Task AWidgetLayerCompositesThroughTheWholeTransform(Affine transform)
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var widget = new InkWidget();

			var frame = capture.BeginWidgetFrame(Background);
			frame.SetTransform(transform);
			widget.OnDrawBackground(frame);
			widget.OnDraw(frame);
			var expected = await capture.CaptureAsync();

			// The first frame paints the layer (and composites it where the test does not look); the second
			// composites the clean layer under the transform.
			var backbuffer = new WidgetBackbuffer(widget);
			try
			{
				frame = capture.BeginWidgetFrame(Background);
				backbuffer.PaintAndComposite(frame, Affine.NewIdentity());
				await Assert.That(backbuffer.Mode).IsEqualTo(BackbufferMode.GpuTexture);

				frame = capture.BeginWidgetFrame(Background);
				frame.SetTransform(transform);
				backbuffer.CompositeOnto(frame, Vector2.Zero, 1, 1);
				var actual = await capture.CaptureAsync();
				await Assert.That(capture.Device.LastUncapturedError).IsNull();

				await AssertMatchesWithinResampling(expected, actual, $"a widget layer under {transform}");
			}
			finally
			{
				backbuffer.ReleaseLayer();
			}
		}

		/// <summary>
		/// The rounded clip of a layer composite turns and shears with the layer: the clipped layer matches a
		/// rounded rectangle filled straight onto the frame under the same transform.
		/// </summary>
		[Test]
		[MethodDataSource(nameof(Transforms))]
		public async Task ARoundedLayerClipFollowsTheWholeTransform(Affine transform)
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var ink = new Color(220, 40, 30);
			var clip = new RectangleDouble(0, 0, 40, 30);
			const double Radius = 8;

			var frame = capture.BeginWidgetFrame(Background);
			frame.SetTransform(transform);
			frame.Render(new RoundedRect(clip, Radius), ink);
			var expected = await capture.CaptureAsync();

			using var target = new GpuRenderTarget(capture.Gl);
			frame = capture.BeginWidgetFrame(Background);
			using (var paint = target.BeginDraw(40, 30))
			{
				paint.Graphics.FillRectangle(0, 0, 40, 30, ink);
			}

			frame.SetTransform(transform);
			target.Composite(frame, 0, 0, 1, clip, Radius);
			var actual = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();

			await AssertMatchesWithinResampling(expected, actual, $"a rounded layer clip under {transform}");
		}

		/// <summary>Transforms a parent can draw a double-buffered child under: a small turn (inside the old
		/// 0.95..1.05 scale gate), a large one, and a y-flip - each set on the surface's graphics, and each as
		/// the <see cref="GuiWidget.ParentToChildTransform"/> of a canvas the child sits on.</summary>
		public static IEnumerable<(Affine Transform, bool ThroughCanvas)> TreeTransforms()
		{
			var transforms = new[]
			{
				Affine.NewRotation(MathHelper.DegreesToRadians(3)) * Affine.NewTranslation(30, 2),
				Affine.NewRotation(MathHelper.DegreesToRadians(30)) * Affine.NewTranslation(50, -4),
				Affine.NewScaling(1, -1) * Affine.NewTranslation(0, 60),
			};

			foreach (var throughCanvas in new[] { false, true })
			{
				foreach (var transform in transforms)
				{
					yield return (transform, throughCanvas);
				}
			}
		}

		/// <summary>The tree to draw, and the transform to set on the surface's graphics before drawing it.</summary>
		private static (GuiWidget Root, Affine SurfaceTransform) BuildTree(bool doubleBuffer, Affine transform, bool throughCanvas)
		{
			var root = new GuiWidget(FrameWidth, FrameHeight);
			var parent = root;
			if (throughCanvas)
			{
				parent = new GuiWidget();
				root.AddChild(parent);
				parent.LocalBounds = new RectangleDouble(0, 0, FrameWidth, FrameHeight);
				parent.ParentToChildTransform = transform;
			}

			var child = new InkWidget { DoubleBuffer = doubleBuffer };
			parent.AddChild(child);
			child.OriginRelativeParent = new Vector2(10, 12);
			return (root, throughCanvas ? Affine.NewIdentity() : transform);
		}

		/// <summary>
		/// A double-buffered child drawn by its parent under a turn or a flip lands where the same child drawn
		/// unbuffered does, on a software surface (the RGBA backbuffer).
		/// </summary>
		[Test]
		[MethodDataSource(nameof(TreeTransforms))]
		public async Task ABufferedChildOnSoftwareFollowsItsParentsTransform(Affine transform, bool throughCanvas)
		{
			ImageBuffer Draw(bool doubleBuffer)
			{
				var (root, surfaceTransform) = BuildTree(doubleBuffer, transform, throughCanvas);
				var image = new ImageBuffer(FrameWidth, FrameHeight);
				var graphics = image.NewGraphics2D();
				graphics.Clear(Background.ToColor());
				graphics.SetTransform(surfaceTransform);
				root.OnDraw(graphics);
				root.Close();
				return image;
			}

			var expected = Draw(doubleBuffer: false);
			await AssertSomethingDrawn(expected, transform);
			await AssertMatchesWithinResampling(expected, Draw(doubleBuffer: true), $"a buffered child on software under {transform}", 1);
		}

		/// <summary>
		/// The GPU twin of <see cref="ABufferedChildOnSoftwareFollowsItsParentsTransform"/>: the retained-layer
		/// backbuffer under a turn or a flip.
		/// </summary>
		[Test]
		[MethodDataSource(nameof(TreeTransforms))]
		public async Task ABufferedChildOnTheGpuFollowsItsParentsTransform(Affine transform, bool throughCanvas)
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);

			async Task<ImageBuffer> Draw(bool doubleBuffer)
			{
				var (root, surfaceTransform) = BuildTree(doubleBuffer, transform, throughCanvas);
				var frame = capture.BeginWidgetFrame(Background);
				frame.SetTransform(surfaceTransform);
				root.OnDraw(frame);
				var image = await capture.CaptureAsync();
				await Assert.That(capture.Device.LastUncapturedError).IsNull();
				root.Close();
				return image;
			}

			var expected = await Draw(doubleBuffer: false);
			await AssertSomethingDrawn(expected, transform);
			await AssertMatchesWithinResampling(expected, await Draw(doubleBuffer: true), $"a buffered child on the GPU under {transform}", 1);
		}

		/// <summary>Guards the comparisons above against a child clipped away entirely, which would match trivially.</summary>
		private static async Task AssertSomethingDrawn(ImageBuffer image, Affine transform)
		{
			var background = image.GetPixel(0, 0);
			int inked = 0;
			for (int y = 0; y < image.Height; y++)
			{
				for (int x = 0; x < image.Width; x++)
				{
					inked += image.GetPixel(x, y) != background ? 1 : 0;
				}
			}

			await Assert.That(inked).IsGreaterThan(400).Because($"the child should show under {transform}");
		}

		/// <summary>
		/// Each pixel of <paramref name="actual"/> lies, channel by channel, within the range its 3x3 neighbourhood
		/// spans in <paramref name="expected"/>, give or take <paramref name="tolerance"/>. A turned layer is
		/// resampled and its quad's edge is not anti-aliased, so it cannot match the vector draw pixel for pixel,
		/// but a misplaced layer is many pixels off and lands far outside that range.
		/// </summary>
		private static async Task AssertMatchesWithinResampling(ImageBuffer expected, ImageBuffer actual, string what, int tolerance = 8)
		{
			int worst = 0;
			int worstX = 0;
			int worstY = 0;
			for (int y = 0; y < expected.Height; y++)
			{
				for (int x = 0; x < expected.Width; x++)
				{
					var a = actual.GetPixel(x, y);
					int delta = Math.Max(
						Math.Max(OutsideNeighbourhood(expected, x, y, a.red, c => c.red), OutsideNeighbourhood(expected, x, y, a.green, c => c.green)),
						OutsideNeighbourhood(expected, x, y, a.blue, c => c.blue));
					if (delta > worst)
					{
						worst = delta;
						worstX = x;
						worstY = y;
					}
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(tolerance)
				.Because($"{what}: worst channel {worst} outside the expected neighbourhood at ({worstX}, {worstY}), expected {expected.GetPixel(worstX, worstY)} got {actual.GetPixel(worstX, worstY)}");
		}

		private static int OutsideNeighbourhood(ImageBuffer expected, int x, int y, int value, Func<Color, int> channel)
		{
			int min = 255;
			int max = 0;
			for (int dy = -1; dy <= 1; dy++)
			{
				for (int dx = -1; dx <= 1; dx++)
				{
					int nx = Math.Clamp(x + dx, 0, expected.Width - 1);
					int ny = Math.Clamp(y + dy, 0, expected.Height - 1);
					int c = channel(expected.GetPixel(nx, ny));
					min = Math.Min(min, c);
					max = Math.Max(max, c);
				}
			}

			return value < min ? min - value : value > max ? value - max : 0;
		}
	}
}
