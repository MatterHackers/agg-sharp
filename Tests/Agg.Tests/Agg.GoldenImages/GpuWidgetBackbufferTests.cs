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
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
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
	/// Double-buffered widgets on a GPU surface keep their pixels in a retained GPU texture
	/// (<see cref="BackbufferMode.GpuTexture"/>), rendered on the real WebGPU device and compared with the
	/// same widget drawn straight onto the frame.
	/// </summary>
	[NotInParallel]
	public class GpuWidgetBackbufferTests
	{
		private const int FrameWidth = 128;
		private const int FrameHeight = 64;

		private static readonly ColorF Background = new ColorF(0.2f, 0.3f, 0.4f, 1);

		/// <summary>Counts its paints and draws opaque, translucent and anti-aliased ink, so a lost alpha,
		/// a flip or a shift each shows.</summary>
		private class PaintCountingWidget : GuiWidget
		{
			public PaintCountingWidget(double width, double height)
				: base(width, height)
			{
				this.BackgroundColor = new Color(255, 255, 255, 160);
			}

			public int PaintCount { get; private set; }

			public Action<Graphics2D> ExtraPaint { get; set; }

			public override void OnDraw(Graphics2D graphics2D)
			{
				this.PaintCount++;
				graphics2D.FillRectangle(2, 2, 14, 10, Color.Red);
				graphics2D.Render(new Ellipse(this.Width - 10, this.Height - 9, 7, 6), new Color(0, 160, 0, 200));
				graphics2D.Line(0, 0, this.Width, this.Height, Color.Black, 1.5);
				this.ExtraPaint?.Invoke(graphics2D);
				base.OnDraw(graphics2D);
			}
		}

		private static (GuiWidget Root, PaintCountingWidget Child) BuildTree(bool doubleBuffer, double width = 40)
		{
			var root = new GuiWidget(FrameWidth, FrameHeight);
			var child = new PaintCountingWidget(width, 30)
			{
				DoubleBuffer = doubleBuffer,
			};
			root.AddChild(child);
			child.OriginRelativeParent = new Vector2(10, 12);
			return (root, child);
		}

		private static async Task<ImageBuffer> RenderAsync(WebGpuOffscreenCapture capture, GuiWidget root, Action<Graphics2DGpu> afterDraw = null, Action beforeDraw = null)
		{
			var frame = capture.BeginWidgetFrame(Background);
			beforeDraw?.Invoke();
			root.OnDrawBackground(frame);
			root.OnDraw(frame);
			afterDraw?.Invoke(frame);
			var image = await capture.CaptureAsync();
			await Assert.That(capture.Device.LastUncapturedError).IsNull();
			return image;
		}

		private static async Task AssertImagesClose(ImageBuffer expected, ImageBuffer actual, string what, int tolerance = 1)
		{
			int worst = 0;
			int worstX = 0;
			int worstY = 0;
			for (int y = 0; y < expected.Height; y++)
			{
				for (int x = 0; x < expected.Width; x++)
				{
					var a = expected.GetPixel(x, y);
					var b = actual.GetPixel(x, y);
					int delta = Math.Max(Math.Max(Math.Abs(a.red - b.red), Math.Abs(a.green - b.green)), Math.Abs(a.blue - b.blue));
					if (delta > worst)
					{
						worst = delta;
						worstX = x;
						worstY = y;
					}
				}
			}

			await Assert.That(worst).IsLessThanOrEqualTo(tolerance)
				.Because($"{what}: worst channel delta {worst} at ({worstX}, {worstY}), expected {expected.GetPixel(worstX, worstY)} got {actual.GetPixel(worstX, worstY)}");
		}

		[Test]
		public async Task ABufferedWidgetOnTheGpuMatchesTheUnbufferedWidget()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);

			var (plainRoot, _) = BuildTree(doubleBuffer: false);
			var expected = await RenderAsync(capture, plainRoot);

			var (root, child) = BuildTree(doubleBuffer: true);
			var actual = await RenderAsync(capture, root);

			await Assert.That(child.ResolveBackbufferMode(new Graphics2DGpu(capture.Gl, FrameWidth, FrameHeight, 1)))
				.IsEqualTo(BackbufferMode.GpuTexture);
			await Assert.That(child.BackBuffer).IsNull()
				.Because("the pixels live in the GPU texture, and there is no CPU copy of them");
			await AssertImagesClose(expected, actual, "a GPU backbuffered widget");
			root.Close();
		}

		[Test]
		public async Task ACleanWidgetIsCompositedWithoutRepaintingAndInvalidatingRepaints()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var (plainRoot, plainChild) = BuildTree(doubleBuffer: false);
			var expected = await RenderAsync(capture, plainRoot);

			var (root, child) = BuildTree(doubleBuffer: true);
			await RenderAsync(capture, root);
			await Assert.That(child.PaintCount).IsEqualTo(1);

			var retained = await RenderAsync(capture, root);
			await Assert.That(child.PaintCount).IsEqualTo(1)
				.Because("a clean widget draws its retained texture and paints nothing");
			await AssertImagesClose(expected, retained, "the retained texture on a later frame");

			child.Invalidate();
			await RenderAsync(capture, root);
			await Assert.That(child.PaintCount).IsEqualTo(2);

			// Resizing repaints at the new size rather than stretching the old picture.
			plainChild.Width = 60;
			child.Width = 60;
			expected = await RenderAsync(capture, plainRoot);
			var resized = await RenderAsync(capture, root);
			await Assert.That(child.PaintCount).IsEqualTo(3);
			await AssertImagesClose(expected, resized, "the resized widget");
			root.Close();
		}

		[Test]
		public async Task AFadedWidgetOnTheGpuCompositesThroughItsLayer()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var (root, child) = BuildTree(doubleBuffer: true);
			child.BackbufferOpacity = 0.5;

			var faded = await RenderAsync(capture, root);
			await Assert.That(child.ResolveBackbufferMode(new Graphics2DGpu(capture.Gl, FrameWidth, FrameHeight, 1)))
				.IsEqualTo(BackbufferMode.GpuTexture);

			// The red square (off the black diagonal), opaque in the layer, lands half way between red and the background.
			var pixel = faded.GetPixel(10 + 4, 12 + 8);
			await Assert.That(Math.Abs(pixel.red - ((255 + 51) / 2)) <= 2 && Math.Abs(pixel.blue - (102 / 2)) <= 2).IsTrue()
				.Because($"the faded red square is r{pixel.red} g{pixel.green} b{pixel.blue}");
			root.Close();
		}

		/// <summary>
		/// A widget inside a GPU layer that reaches for <see cref="Graphics2D.DestImage"/> gets a CPU buffer
		/// the size of its layer, which is drawn into the layer - as a software backbuffer would give it - and
		/// the window's own CPU layer is left alone.
		/// </summary>
		[Test]
		public async Task DestImageInsideAGpuLayerLandsInTheLayer()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var (root, child) = BuildTree(doubleBuffer: true);
			child.ExtraPaint = graphics =>
			{
				var dest = graphics.DestImage;
				for (int y = 20; y < 24; y++)
				{
					for (int x = 20; x < 24; x++)
					{
						dest.SetPixel(x, y, Color.Magenta);
					}
				}
			};

			bool windowHadCpuLayer = true;
			var image = await RenderAsync(capture, root, frame => windowHadCpuLayer = frame.HasCpuLayer);

			await Assert.That(child.BackBuffer).IsNull()
				.Because("the widget was painted into its GPU layer, not a software backbuffer");
			await Assert.That(windowHadCpuLayer).IsFalse();
			var pixel = image.GetPixel(10 + 21, 12 + 21);
			await Assert.That(pixel.red == 255 && pixel.green == 0 && pixel.blue == 255).IsTrue()
				.Because($"the DestImage pixel is {pixel}");
			root.Close();
		}

		/// <summary>
		/// A styled <see cref="WindowWidget"/> that opts into double buffering - rounded corners, a soft
		/// shadow and a title bar colour - composites through its GPU layer exactly as it paints directly. The
		/// shadow is drawn inside the window's own bounds (its grab border), so the layer needs no outset.
		/// </summary>
		[Test]
		public async Task AStyledWindowWidgetRendersThroughItsGpuLayer()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth * 2, FrameWidth);

			(GuiWidget Root, WindowWidget Window) BuildWindow(bool doubleBuffer)
			{
				var root = new GuiWidget(FrameWidth * 2, FrameWidth) { BackgroundColor = Color.White };
				var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(30, 20, 200, 110))
				{
					BackgroundColor = new Color(200, 220, 240),
					CornerRadius = 8,
					ShadowColor = new Color(Color.Black, 120),
					ShadowOffset = new Vector2(0, -1.5),
					ShadowBlur = 3.5,
					TitleBarColor = new Color(90, 120, 200),
					DoubleBuffer = doubleBuffer,
				};
				root.AddChild(window);
				root.PerformLayout();
				return (root, window);
			}

			var (plainRoot, _) = BuildWindow(doubleBuffer: false);
			var expected = await RenderAsync(capture, plainRoot);

			var (root, window) = BuildWindow(doubleBuffer: true);
			var actual = await RenderAsync(capture, root);

			await Assert.That(window.ResolveBackbufferMode(new Graphics2DGpu(capture.Gl, capture.Width, capture.Height, 1)))
				.IsEqualTo(BackbufferMode.GpuTexture);
			await Assert.That(window.BackBuffer).IsNull();
			// 2, not 1: at the rounded corners the background, border and title bar each lay a partial halo on
			// the same pixel, and the layer stores that stack in 8 bits before compositing it - one more
			// rounding than drawing direct, worth up to a level on a pixel only ~20% covered.
			await AssertImagesClose(expected, actual, "a styled window through its GPU layer", tolerance: 2);
			plainRoot.Close();
			root.Close();
		}

		/// <summary>
		/// The straight-alpha draws - a translucent pixel-aligned <see cref="Graphics2D.FillRectangle(double, double, double, double, IColorType)"/>
		/// and a partly transparent image - over the widget's opaque background and over transparent parts of
		/// the layer. Inside a premultiplied layer their alpha has to accumulate as a + dstA(1 - a); blending
		/// alpha by SrcAlpha as well (a² + dstA(1 - a)) leaves the layer see-through where the widget is
		/// opaque, and the frame behind shows through.
		/// </summary>
		[Test]
		[Arguments(255)]
		[Arguments(0)]
		public async Task TranslucentFillsAndImagesInsideALayerMatchTheUnbufferedWidget(int backgroundAlpha)
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);

			var image = new ImageBuffer(12, 12, 32, new BlenderBGRA());
			for (int y = 0; y < 12; y++)
			{
				for (int x = 0; x < 12; x++)
				{
					image.SetPixel(x, y, new Color(20 * x, 200, 20 * y, 20 * (x + y) + 10));
				}
			}

			(GuiWidget Root, PaintCountingWidget Child) Build(bool doubleBuffer)
			{
				var (root, child) = BuildTree(doubleBuffer);
				child.BackgroundColor = new Color(240, 240, 250, backgroundAlpha);
				child.ExtraPaint = graphics =>
				{
					graphics.FillRectangle(16, 4, 36, 20, new Color(0, 0, 255, 100));
					graphics.Render(image, 22, 14);
				};
				return (root, child);
			}

			var (plainRoot, _) = Build(doubleBuffer: false);
			var expected = await RenderAsync(capture, plainRoot);

			var (bufferedRoot, _) = Build(doubleBuffer: true);
			var actual = await RenderAsync(capture, bufferedRoot);

			await AssertImagesClose(expected, actual, $"translucent fills and images in a layer, background alpha {backgroundAlpha}");
			bufferedRoot.Close();
		}

		/// <summary>
		/// A layer is painted at the coordinate scale of the moment, so moving between the window (1) and a
		/// supersampled capture (above 1) repaints it: a clean 1x layer inside the capture would be magnified
		/// and blurry, and one painted during the capture would stay at the capture's resolution afterwards.
		/// </summary>
		[Test]
		public async Task ACoordinateScaleChangeRepaintsTheLayer()
		{
			using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
			var (root, child) = BuildTree(doubleBuffer: true);

			await RenderAsync(capture, root);
			await RenderAsync(capture, root);
			await Assert.That(child.PaintCount).IsEqualTo(1);

			try
			{
				await RenderAsync(capture, root, beforeDraw: () => capture.Context.CoordinateScale = 2);
				await Assert.That(child.PaintCount).IsEqualTo(2)
					.Because("the 1x layer is not sharp at coordinate scale 2");
			}
			finally
			{
				capture.Context.CoordinateScale = 1;
			}

			var image = await RenderAsync(capture, root);
			await Assert.That(child.PaintCount).IsEqualTo(3)
				.Because("the layer painted at scale 2 is twice the window's resolution");

			var (plainRoot, _) = BuildTree(doubleBuffer: false);
			await AssertImagesClose(await RenderAsync(capture, plainRoot), image, "the layer back at scale 1");
			root.Close();
		}

		[Test]
		public async Task SoftwareSurfacesKeepTheRgbaBackbuffer()
		{
			var (root, child) = BuildTree(doubleBuffer: true);
			var image = new ImageBuffer(FrameWidth, FrameHeight);
			var graphics = image.NewGraphics2D();
			await Assert.That(graphics.SupportsRetainedLayers).IsFalse();
			await Assert.That(child.ResolveBackbufferMode(graphics)).IsEqualTo(BackbufferMode.Rgba);

			root.OnDraw(graphics);
			await Assert.That(child.BackBuffer).IsNotNull();
			await Assert.That(child.PaintCount).IsEqualTo(1);
		}
	}
}
