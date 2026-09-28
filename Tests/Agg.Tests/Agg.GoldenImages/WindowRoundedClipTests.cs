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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.Transform;
using MatterHackers.Agg.UI;
using MatterHackers.RenderGl;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.GoldenImages
{
	/// <summary>
	/// A <see cref="WindowWidget"/> with a <see cref="WindowWidget.CornerRadius"/> cuts whatever its title bar
	/// and client area paint to its rounded corners, like agg-gui's rounded layer clip - on the software
	/// surface and through a GPU retained layer - while its shadow, drawn outside the panel, is not cut.
	/// </summary>
	/// <remarks>
	/// The title bar and the client area are painted solid red so every corner of the panel is red inside
	/// the arc. Before the clip, the panel only padded its sides and bottom and the title bar's square top
	/// corners stuck out past the arc.
	/// </remarks>
	[NotInParallel]
	public class WindowRoundedClipTests
	{
		private const int FrameWidth = 260;
		private const int FrameHeight = 200;

		[Test]
		public async Task SoftwareContentFollowsTheRoundedCorners()
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				var (root, window) = BuildScene(8);
				var image = RenderSoftware(root);
				await AssertCornersAreRounded(image, window, "software");
				root.Close();
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		[Test]
		public async Task GpuContentFollowsTheRoundedCorners()
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);

				// Both with the window itself double-buffered (as the demo has it) and without (as MatterCAD's
				// node editor has it): the panel's rounded layer sits inside the window's own layer in the first.
				foreach (bool windowBuffered in new[] { false, true })
				{
					var (root, window) = BuildScene(8);
					window.DoubleBuffer = windowBuffered;
					var frame = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
					root.OnDrawBackground(frame);
					root.OnDraw(frame);
					var image = await capture.CaptureAsync();
					await Assert.That(capture.Device.LastUncapturedError).IsNull();
					await AssertCornersAreRounded(image, window, $"GPU, window buffered {windowBuffered}");
					root.Close();
				}
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		/// <summary>A square window keeps drawing as it always has: no layer for its panel, and its content
		/// reaches the very corner pixels - on the software surface and on the GPU. A window whose radius goes
		/// back to 0 drops its panel's buffer.</summary>
		[Test]
		[Arguments(false, false)]
		[Arguments(true, false)]
		[Arguments(false, true)]
		public async Task ASquareWindowIsNotClipped(bool gpu, bool wasRounded)
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				var (root, window) = BuildScene(wasRounded ? 8 : 0);
				if (wasRounded)
				{
					RenderSoftware(root);
					await Assert.That(window.TitleBar.Parent.DoubleBuffer).IsTrue();
					window.CornerRadius = 0;
					window.ShadowColor = Color.Transparent;
				}

				ImageBuffer image;
				if (gpu)
				{
					using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
					var frame = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
					root.OnDrawBackground(frame);
					root.OnDraw(frame);
					image = await capture.CaptureAsync();
				}
				else
				{
					image = RenderSoftware(root);
				}

				var panel = PanelBounds(root, window);

				await Assert.That(window.TitleBar.Parent.DoubleBuffer).IsFalse();
				await Assert.That(window.TitleBar.Parent.BackBuffer).IsNull();
				foreach (var (x, y) in new[] { (Left(panel), Bottom(panel)), (Right(panel), Top(panel)) })
				{
					var pixel = image.GetPixel(x, y);
					await Assert.That(IsRed(pixel)).IsTrue().Because($"the square corner at ({x}, {y}) is content, it was {pixel}");
				}

				root.Close();
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		/// <summary>
		/// Under a parent scale well away from 1 (a zoomed-out node editor) a widget is drawn straight onto
		/// the parent rather than through its backbuffer, so the rounded clip cannot apply - the client area
		/// still has to stay inside the arc.
		/// </summary>
		[Test]
		public async Task ContentStaysInsideTheArcWhenTheParentIsScaled()
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				var (root, window) = BuildScene(8, titleBarRed: false);

				// Scaled the way NodeEditor zooms its canvas: a parent's ParentToChildTransform.
				const double scale = 0.8;
				root.RemoveChild(window);
				var zoomed = new GuiWidget(FrameWidth, FrameHeight)
				{
					ParentToChildTransform = Affine.NewScaling(scale),
				};
				window.ClearRemovedFlag();
				zoomed.AddChild(window);
				root.AddChild(zoomed);
				root.PerformLayout();
				var image = RenderSoftware(root);

				var panel = PanelBounds(zoomed, window);
				int left = (int)Math.Round(panel.Left * scale);
				int right = (int)Math.Round(panel.Right * scale) - 1;
				int bottom = (int)Math.Round(panel.Bottom * scale);
				foreach (var (x, name) in new[] { (left, "bottom-left"), (right, "bottom-right") })
				{
					var pixel = image.GetPixel(x, bottom);
					await Assert.That(pixel.red - pixel.green < 40).IsTrue()
						.Because($"the {name} corner pixel at 0.8x lies outside the arc, it was {pixel}");
				}

				// And the probe is on the panel: a few pixels in along the diagonal is the red content.
				var inside = image.GetPixel(left + 4, bottom + 4);
				await Assert.That(IsRed(inside)).IsTrue().Because($"inside the scaled panel is content, it was {inside}");

				root.Close();
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		/// <summary>
		/// The panel's own rounded border is anti-aliased once, by its own rasterizer; the clip must not
		/// attenuate the arc a second time. The ink in the corner through the clip is compared with the same
		/// panel drawn without its buffer (so without the clip).
		/// </summary>
		[Test]
		[Arguments(false)]
		[Arguments(true)]
		public async Task TheBorderArcIsNotCutTwice(bool gpu)
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				async Task<long> CornerInk(bool clipped)
				{
					var root = new GuiWidget(FrameWidth, FrameHeight) { BackgroundColor = Color.White };
					var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(40, 30, 220, 150))
					{
						BackgroundColor = Color.White,
						CornerRadius = 8,
						WindowBorder = 1.5,
						WindowBorderColor = Color.Black,
					};
					root.AddChild(window);
					root.PerformLayout();
					window.TitleBar.Parent.DoubleBuffer = clipped;

					ImageBuffer image;
					if (gpu)
					{
						using var capture = WebGpuOffscreenCapture.Create(FrameWidth, FrameHeight);
						var frame = capture.BeginWidgetFrame(new ColorF(1, 1, 1, 1));
						root.OnDrawBackground(frame);
						root.OnDraw(frame);
						image = await capture.CaptureAsync();
					}
					else
					{
						image = RenderSoftware(root);
					}

					var panel = PanelBounds(root, window);
					long ink = 0;
					for (int y = Bottom(panel); y < Bottom(panel) + 8; y++)
					{
						for (int x = Left(panel); x < Left(panel) + 8; x++)
						{
							ink += 255 - image.GetPixel(x, y).green;
						}
					}

					root.Close();
					return ink;
				}

				long direct = await CornerInk(false);
				long throughClip = await CornerInk(true);
				await Assert.That(throughClip >= direct * 0.93).IsTrue()
					.Because($"the border arc through the clip has {throughClip} ink against {direct} drawn directly");
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		private static (GuiWidget Root, WindowWidget Window) BuildScene(double cornerRadius, bool titleBarRed = true)
		{
			var root = new GuiWidget(FrameWidth, FrameHeight) { BackgroundColor = Color.White };
			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(40, 30, 220, 150))
			{
				BackgroundColor = new Color(200, 220, 240),
				CornerRadius = cornerRadius,
				ShadowColor = cornerRadius > 0 ? new Color(Color.Black, 120) : Color.Transparent,
				ShadowOffset = new Vector2(0, -1.5),
				ShadowBlur = 3.5,
			};
			window.TitleBar.BackgroundColor = titleBarRed ? Color.Red : Color.Transparent;
			window.ClientArea.BackgroundColor = Color.Red;
			root.AddChild(window);
			root.PerformLayout();
			return (root, window);
		}

		private static ImageBuffer RenderSoftware(GuiWidget root)
		{
			var image = new ImageBuffer((int)root.Width, (int)root.Height);
			var graphics2D = image.NewGraphics2D();
			root.OnDrawBackground(graphics2D);
			root.OnDraw(graphics2D);
			return image;
		}

		private static RectangleDouble PanelBounds(GuiWidget root, WindowWidget window)
		{
			var panel = window.TitleBar.Parent;
			return panel.TransformToParentSpace(root, panel.LocalBounds);
		}

		private static int Left(RectangleDouble r) => (int)Math.Round(r.Left);

		private static int Bottom(RectangleDouble r) => (int)Math.Round(r.Bottom);

		private static int Right(RectangleDouble r) => (int)Math.Round(r.Right) - 1;

		private static int Top(RectangleDouble r) => (int)Math.Round(r.Top) - 1;

		private static bool IsRed(Color pixel) => pixel.red > 230 && pixel.green < 25 && pixel.blue < 25;

		/// <summary>
		/// At each corner of a radius 8 panel, stepping in along the diagonal from the corner pixel: 1 pixel in
		/// is well outside the arc (its centre is 1.2 pixels past it) and must show what is behind the panel,
		/// 3 in is well inside and must be the content's red, and the arc between is anti-aliased.
		/// Then the shadow just outside the panel's bottom edge must still be there.
		/// </summary>
		private static async Task AssertCornersAreRounded(ImageBuffer image, WindowWidget window, string what)
		{
			var panel = PanelBounds(window.Parent, window);
			await Assert.That(Math.Abs(panel.Left - Math.Round(panel.Left)) < 1e-9 && Math.Abs(panel.Bottom - Math.Round(panel.Bottom)) < 1e-9).IsTrue()
				.Because($"the probes assume a whole-pixel panel, it was {panel}");

			var corners = new (int X, int Y, int StepX, int StepY, string Name)[]
			{
				(Left(panel), Bottom(panel), 1, 1, "bottom-left"),
				(Right(panel), Bottom(panel), -1, 1, "bottom-right"),
				(Left(panel), Top(panel), 1, -1, "top-left"),
				(Right(panel), Top(panel), -1, -1, "top-right"),
			};

			foreach (var (x, y, stepX, stepY, name) in corners)
			{
				var outside = image.GetPixel(x + stepX, y + stepY);
				var inside = image.GetPixel(x + 3 * stepX, y + 3 * stepY);

				await Assert.That(outside.red - outside.green < 40).IsTrue()
					.Because($"{what}: just outside the {name} arc shows the canvas or shadow, not content, it was {outside}");

				// Along the edges too: the outermost row and column 2 pixels from the corner are 1.3 pixels
				// past the arc - where a square-cornered title bar would still paint.
				foreach (var (ex, ey) in new[] { (x + 2 * stepX, y), (x, y + 2 * stepY) })
				{
					var alongEdge = image.GetPixel(ex, ey);
					await Assert.That(alongEdge.red - alongEdge.green < 40).IsTrue()
						.Because($"{what}: ({ex}, {ey}) on the {name} corner's edge is past the arc, it was {alongEdge}");
				}

				await Assert.That(IsRed(inside)).IsTrue()
					.Because($"{what}: just inside the {name} arc is the content's red, it was {inside}");

				// Anti-aliased: somewhere in the corner's 8 x 8 square there are pixels redder than the
				// background but not all the way to red. Only at the top: the title bar reaches the panel's
				// edge and so the arc, while the client area is padded to keep its square corner on the arc
				// (see WindowWidget.CornerRadius) and the clip has nothing there to cut.
				if (stepY > 0)
				{
					continue;
				}

				int blended = 0;
				for (int i = 0; i < 8; i++)
				{
					for (int j = 0; j < 8; j++)
					{
						var pixel = image.GetPixel(x + (i * stepX), y + (j * stepY));
						if (pixel.red - pixel.green > 40 && pixel.green > 25)
						{
							blended++;
						}
					}
				}

				await Assert.That(blended).IsGreaterThanOrEqualTo(3)
					.Because($"{what}: the {name} arc edge is a blend of content and background: {string.Join(" ", Enumerable.Range(0, 8).Select(j => string.Join(",", Enumerable.Range(0, 8).Select(i => image.GetPixel(x + (i * stepX), y + (j * stepY))))))}");
			}

			var belowBottom = image.GetPixel((int)panel.Center.X, Bottom(panel) - 2);
			await Assert.That(belowBottom.red < 245 && Math.Abs(belowBottom.red - belowBottom.green) < 10).IsTrue()
				.Because($"{what}: the shadow below the panel is still drawn, it was {belowBottom}");
		}
	}
}
