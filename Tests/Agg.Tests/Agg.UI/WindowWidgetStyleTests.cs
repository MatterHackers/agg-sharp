/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.LcdCoverage;
using MatterHackers.Agg.VertexSource;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// A <see cref="WindowWidget"/> can draw itself as a rounded card with a soft drop shadow
	/// (<see cref="WindowWidget.CornerRadius"/>, <see cref="WindowWidget.ShadowColor"/>), and one that is not
	/// asked to draws exactly as it always has.
	/// </summary>
	/// <remarks>
	/// <see cref="GuiWidget.DeviceScale"/> is process wide, so these are keyless <c>[NotInParallel]</c> and
	/// restore the previous value in a finally.
	/// </remarks>
	[NotInParallel]
	public class WindowWidgetStyleTests
	{
		private static readonly Color Background = new Color(200, 220, 240);
		private static readonly Color Parent = Color.White;

		/// <summary>
		/// AddTitleBar puts MatterCAD's mh.png icon in front of the title, but that icon ships with MatterCAD,
		/// not agg-sharp: any other app (the agg-sharp demo, these tests) threw "Bad icon load" in a debug build.
		/// Without the icon the title bar is built without it.
		/// </summary>
		[Test]
		public async Task AddTitleBarWorksWithoutMatterCadsIcon()
		{
			var closed = false;
			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(0, 0, 200, 100));
			window.AddTitleBar("Title", () => closed = true);

			window.TitleBar.Descendants().First(w => w.ToolTipText == "Close").InvokeClick();
			await Assert.That(closed).IsTrue();
		}

		[Test]
		[Arguments(1.0)]
		[Arguments(2.0)]
		public async Task ARoundedWindowHasARoundCornerAndAShadowBelowRight(double deviceScale)
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = deviceScale;
				var (image, panel) = Render(window =>
				{
					window.CornerRadius = 8;
					window.ShadowColor = new Color(Color.Black, 120);
					window.ShadowOffset = new Vector2(0, -1.5);
					window.ShadowBlur = 3.5;
				});

				var center = image.GetPixel((int)panel.Center.X, (int)panel.Center.Y);
				await Assert.That(center).IsEqualTo(Background).Because($"the middle of the window is its background at {deviceScale}x");

				// The very corner pixel of the panel's bounding box is outside the rounded shape.
				var corner = image.GetPixel((int)panel.Left, (int)panel.Top - 1);
				await Assert.That(corner.Red0To255 > 240 && corner.Green0To255 > 240 && corner.Blue0To255 > 240).IsTrue()
					.Because($"the top-left corner at {deviceScale}x has to show the parent through the rounding, it was {corner}");

				// Just outside the bottom and right edges, near the bottom-right corner, the shadow falls, darker
				// than the background. (The very corner is rounded off, so the probes stand clear of the arc.)
				var belowBottom = image.GetPixel((int)(panel.Right - 16 * deviceScale), (int)(panel.Bottom - 2 * deviceScale));
				var rightOfRight = image.GetPixel((int)(panel.Right + 1 * deviceScale), (int)(panel.Bottom + 16 * deviceScale));
				foreach (var (pixel, where) in new[] { (belowBottom, "below the bottom edge"), (rightOfRight, "right of the right edge") })
				{
					await Assert.That(pixel.Red0To255 < Background.Red0To255).IsTrue()
						.Because($"just {where} has to be in a shadow darker than the window's background at {deviceScale}x, it was {pixel}");
				}
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		[Test]
		public async Task AnUnstyledWindowDrawsItsSquareCornerAsBefore()
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				var (image, panel) = Render(window => { });

				var corner = image.GetPixel((int)panel.Left, (int)panel.Top - 1);
				await Assert.That(corner).IsEqualTo(Background)
					.Because($"an unstyled window keeps its square background corner, it was {corner}");
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		/// <summary>
		/// The shadow is drawn in stacked layers the size of the whole window, and the background is one more
		/// window-sized fill, but under an opaque background only a thin ring around the edge needs a real fill:
		/// the middle is one flat colour. On an LCD backbuffer every fill costs a 3x raster, a filter and a
		/// per-pixel composite over its whole area, so filling the middle made a GUI demo window's first paint
		/// cost about eight window-sized fills, and froze the browser page for two seconds.
		/// </summary>
		[Test]
		public async Task AnOpaqueWindowOnlyFillsTheRingAroundItsFlatMiddle()
		{
			var saved = GuiWidget.DeviceScale;
			try
			{
				GuiWidget.DeviceScale = 1;
				var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(0, 0, 400, 300))
				{
					BackgroundColor = Background,
					CornerRadius = 8,
					ShadowColor = new Color(Color.Black, 80),
					ShadowOffset = new Vector2(0.5, -1.5),
					ShadowBlur = 3.5,
				};
				window.PerformLayout();

				var graphics2D = new FillAreaCountingGraphics2D(new LcdBuffer((int)window.Width, (int)window.Height));
				window.OnDrawBackground(graphics2D);

				// Four shadow layers and the background, each filled only in bands a radius and a little wide around
				// the edge, come to about half the window's area together; filling each one whole was nearly five.
				double windowArea = window.Width * window.Height;
				await Assert.That(graphics2D.FilledArea).IsLessThan(windowArea)
					.Because($"only the edge of the opaque window should be filled; {graphics2D.FilledArea / windowArea:0.00} window areas were filled");
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		/// <summary>
		/// A rounded window clips its panel, and the clip keeps every glyph in it greyscale (see
		/// <c>WidgetBackbuffer.ClipRadius</c>), so the window's own buffer holds nothing subpixel text could
		/// use. Painting it as LCD coverage anyway ran the whole window - shadow, background and the panel's
		/// pixels - through the CPU LCD pipeline instead of a GPU layer, and turning LCD text on by default
		/// froze the GUI demo's first frame for two seconds. A square window can still hold subpixel text.
		/// </summary>
		[Test]
		public async Task ARoundedWindowIsNotBufferedAsLcdCoverage()
		{
			bool wasEnabled = LcdRenderSettings.Enabled;
			try
			{
				LcdRenderSettings.Enabled = true;
				var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(0, 0, 200, 100))
				{
					BackgroundColor = Background,
					DoubleBuffer = true,
				};
				Graphics2D capable = new ImageBuffer(300, 200).NewGraphics2D();

				await Assert.That(window.ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.LcdCoverage)
					.Because("a square window's text can be subpixel, so its buffer keeps the LCD planes");

				window.CornerRadius = 8;
				await Assert.That(window.ResolveBackbufferMode(capable)).IsEqualTo(BackbufferMode.Rgba)
					.Because("nothing in a rounded window can be subpixel, so LCD planes are only cost");
			}
			finally
			{
				LcdRenderSettings.Enabled = wasEnabled;
			}
		}

		/// <summary>Sums the pixel area each fill covers (its bounds inside the clip), which is what an LCD fill costs.</summary>
		private class FillAreaCountingGraphics2D : LcdBufferGraphics2D
		{
			public FillAreaCountingGraphics2D(LcdBuffer buffer)
				: base(buffer)
			{
			}

			public double FilledArea { get; private set; }

			protected override void RenderVertexSource(IVertexSource vertexSource, IColorType colorType)
			{
				var bounds = new VertexSourceApplyTransform(vertexSource, this.GetTransform()).GetBounds();
				if (bounds.IntersectWithRectangle(this.GetClippingRect()))
				{
					this.FilledArea += bounds.Width * bounds.Height;
				}

				base.RenderVertexSource(vertexSource, colorType);
			}
		}

		/// <summary>
		/// Draws a window with <see cref="Background"/> on a white parent and returns the image and the
		/// visible panel's rectangle (inside the grab border) in image pixels.
		/// </summary>
		private static (ImageBuffer image, RectangleDouble panel) Render(System.Action<WindowWidget> style)
		{
			var scale = GuiWidget.DeviceScale;
			var parent = new GuiWidget(300 * scale, 300 * scale)
			{
				BackgroundColor = Parent,
			};
			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(50 * scale, 50 * scale, 250 * scale, 200 * scale))
			{
				BackgroundColor = Background,
				WindowBorder = 0,
			};
			style(window);
			parent.AddChild(window);
			parent.PerformLayout();

			var image = new ImageBuffer((int)parent.Width, (int)parent.Height);
			var graphics2D = image.NewGraphics2D();
			parent.OnDrawBackground(graphics2D);
			parent.OnDraw(graphics2D);

			var panel = window.TitleBar.Parent.TransformToParentSpace(parent, window.TitleBar.Parent.LocalBounds);
			return (image, panel);
		}
	}
}
