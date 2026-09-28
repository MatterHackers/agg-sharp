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

using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.RasterizerScanline;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's floating windows: built on first open, open/close/raise through the host, and the
	// title bar's close button going through the same path.
	// Builds every demo window, About included: new DemoTheme() writes ThemeConfig.Current and the About
	// window's MarkdownWidget writes MarkdownWidget.Theme.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class DemoWindowHostTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		private static (GuiWidget Canvas, DemoWindowHost Host) CreateHost()
		{
			var canvas = new GuiWidget(1000, 700);
			return (canvas, new DemoWindowHost(canvas));
		}

		[Test]
		public async Task OpensTheOpenByDefaultSpecsAndBuildsNoOthers()
		{
			var (canvas, host) = CreateHost();

			var expected = GuiDemoSpecs.All.Where(s => s.OpenByDefault).ToList();
			await Assert.That(expected.Count).IsGreaterThan(0);
			await Assert.That(host.ZOrder).IsEquivalentTo(expected, CollectionOrdering.Matching);
			await Assert.That(canvas.Children.OfType<WindowWidget>().Count()).IsEqualTo(expected.Count);

			foreach (DemoSpec spec in GuiDemoSpecs.All)
			{
				await Assert.That(host.IsOpen(spec)).IsEqualTo(spec.OpenByDefault);
				await Assert.That(host.GetWindow(spec) != null).IsEqualTo(spec.OpenByDefault);
			}
		}

		[Test]
		public async Task OpenCloseAndRaiseReorderTheCanvasAndFireOpenChanged()
		{
			var (canvas, host) = CreateHost();
			var changes = new List<DemoSpec>();
			host.OpenChanged += (s, spec) => changes.Add(spec);

			DemoSpec sliders = Spec("Sliders");
			DemoSpec textEdit = Spec("TextEdit");
			await Assert.That(host.GetWindow(sliders)).IsNull();

			host.SetOpen(sliders, true);
			host.SetOpen(textEdit, true);
			WindowWidget slidersWindow = host.GetWindow(sliders);
			await Assert.That(slidersWindow.DoubleBuffer).IsTrue();
			await Assert.That(slidersWindow.Descendants().Any(w => w.Name == sliders.ContentName)).IsTrue();
			await Assert.That(host.ZOrder.TakeLast(2)).IsEquivalentTo(new[] { sliders, textEdit }, CollectionOrdering.Matching);
			await Assert.That(changes).IsEquivalentTo(new[] { sliders, textEdit }, CollectionOrdering.Matching);

			// Opening an open window changes nothing and says nothing.
			host.SetOpen(textEdit, true);
			await Assert.That(changes.Count).IsEqualTo(2);

			host.Raise(sliders);
			await Assert.That(host.ZOrder.Last()).IsEqualTo(sliders);
			await Assert.That(canvas.Children.Last()).IsEqualTo(slidersWindow);

			host.SetOpen(sliders, false);
			await Assert.That(host.IsOpen(sliders)).IsFalse();
			await Assert.That(slidersWindow.Parent).IsNull();
			await Assert.That(host.ZOrder.Contains(sliders)).IsFalse();
			await Assert.That(changes.Last()).IsEqualTo(sliders);

			// Reopening reuses the window it built, keeping where the user left it, and puts it on top.
			slidersWindow.Position = new VectorMath.Vector2(123, 45);
			host.SetOpen(sliders, true);
			await Assert.That(host.GetWindow(sliders)).IsEqualTo(slidersWindow);
			await Assert.That(slidersWindow.Position).IsEqualTo(new VectorMath.Vector2(123, 45));
			await Assert.That(host.ZOrder.Last()).IsEqualTo(sliders);
			await Assert.That(changes.Count).IsEqualTo(4);
		}

		[Test]
		public async Task PressingAWindowRaisesIt()
		{
			var (canvas, host) = CreateHost();
			DemoSpec sliders = Spec("Sliders");
			DemoSpec textEdit = Spec("TextEdit");
			host.SetOpen(sliders, true);
			host.SetOpen(textEdit, true);
			canvas.PerformLayout();

			// Sliders is tiled clear of TextEdit, so a press in its middle lands on it alone.
			WindowWidget window = host.GetWindow(sliders);
			var center = window.Position + window.Size / 2;
			canvas.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));
			canvas.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, center.X, center.Y, 0));

			await Assert.That(host.ZOrder.Last()).IsEqualTo(sliders);
		}

		[Test]
		public async Task TheTitleBarCloseButtonClosesTheWindow()
		{
			var (_, host) = CreateHost();
			DemoSpec gallery = GuiDemoSpecs.All.First(s => s.OpenByDefault);
			var changes = new List<DemoSpec>();
			host.OpenChanged += (s, spec) => changes.Add(spec);

			GuiWidget closeButton = host.GetWindow(gallery).TitleBar.Descendants().First(w => w.ToolTipText == "Close");
			closeButton.InvokeClick();

			await Assert.That(host.IsOpen(gallery)).IsFalse();
			await Assert.That(changes).IsEquivalentTo(new[] { gallery }, CollectionOrdering.Matching);
		}

		[Test]
		public async Task ThemeChangesRecolourWindowsAndTheirComingSoonText()
		{
			var demoTheme = new DemoTheme(ThemePreference.Dark);
			var canvas = new GuiWidget(1000, 700);
			var host = new DemoWindowHost(canvas, demoTheme);
			// Screen Share is deferred, so it stays a placeholder while the other windows are built.
			DemoSpec placeholder = Spec("Screen Share");
			host.SetOpen(placeholder, true);
			WindowWidget window = host.GetWindow(placeholder);
			TextWidget comingSoon = window.Descendants<TextWidget>().Single(w => w.Name == placeholder.ContentName);

			await Assert.That(window.BackgroundColor).IsEqualTo(DemoPalette.Dark.WindowFill);
			await Assert.That(window.TitleBarColor).IsEqualTo(DemoPalette.Dark.WindowTitleFill);
			await Assert.That(comingSoon.TextColor).IsEqualTo(DemoPalette.Dark.TextColor);

			demoTheme.SetPreference(ThemePreference.Light);

			await Assert.That(window.BackgroundColor).IsEqualTo(DemoPalette.Light.WindowFill);
			await Assert.That(window.TitleBarColor).IsEqualTo(DemoPalette.Light.WindowTitleFill);
			await Assert.That(window.WindowBorderColor).IsEqualTo(DemoPalette.Light.WindowStroke);
			await Assert.That(window.TitleBar.Descendants<TextWidget>().All(t => t.TextColor == DemoPalette.Light.TextColor)).IsTrue();
			await Assert.That(comingSoon.TextColor).IsEqualTo(DemoPalette.Light.TextColor);

			// Closing the page lets go of the theme: a later change reaches none of it.
			host.SetOpen(placeholder, false);
			canvas.Close();
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.HasBeenClosed).IsTrue();
			await Assert.That(window.BackgroundColor).IsEqualTo(DemoPalette.Light.WindowFill);
			await Assert.That(comingSoon.TextColor).IsEqualTo(DemoPalette.Light.TextColor);
		}

		[Test]
		public async Task ClosingAWindowReleasesItsRetainedLayer()
		{
			var (canvas, host) = CreateHost();
			canvas.PerformLayout();
			DemoSpec lion = Spec("Lion");
			WindowWidget window = host.GetWindow(lion);

			// A surface with retained layers, as the GPU one is: the window paints into a layer of its own.
			var surface = new LayerSurface(new ImageBuffer(1000, 700));
			canvas.OnDraw(surface);
			// One layer per window on the canvas; Lion's is the widest (520 against 360).
			await Assert.That(surface.Layers.Count).IsGreaterThan(1);
			FakeLayer layer = surface.Layers.OrderByDescending(l => l.Width).First();
			await Assert.That(layer.Width).IsGreaterThanOrEqualTo((int)window.Width);
			await Assert.That(surface.Layers.Any(l => l.Disposed)).IsFalse();

			host.SetOpen(lion, false);
			await Assert.That(layer.Disposed).IsTrue();
			await Assert.That(surface.Layers.Count(l => l.Disposed)).IsEqualTo(1);
			await Assert.That(window.DoubleBuffer).IsFalse();

			// Reopening buffers it again and paints a fresh layer.
			host.SetOpen(lion, true);
			await Assert.That(window.DoubleBuffer).IsTrue();
			int before = surface.Layers.Count;
			canvas.OnDraw(surface);
			await Assert.That(surface.Layers.Count).IsGreaterThan(before);
		}

		/// <summary>A CPU surface that answers retained layers, so a double-buffered widget takes the
		/// GpuTexture path without a GPU; it records every layer it hands out.</summary>
		private class LayerSurface : ImageGraphics2D
		{
			public LayerSurface(ImageBuffer image)
			{
				this.Initialize(new ImageClippingProxy(image), new ScanlineRasterizer());
				this.ScanlineCache = new ScanlineCachePacked8();
			}

			public List<FakeLayer> Layers { get; } = new List<FakeLayer>();

			public override bool SupportsRetainedLayers => true;

			public override IRetainedLayer CreateRetainedLayer()
			{
				var layer = new FakeLayer(this);
				this.Layers.Add(layer);
				return layer;
			}

			public override bool RenderRetainedLayer(IRetainedLayer layer, double x, double y, double opacity = 1) => true;
		}

		private class FakeLayer : IRetainedLayer
		{
			private readonly Graphics2D owner;

			public FakeLayer(Graphics2D owner) => this.owner = owner;

			public bool Disposed { get; private set; }

			public int Width { get; private set; }

			public int Height { get; private set; }

			public int DrawCount { get; private set; }

			public IRetainedLayerPaint Begin(int width, int height)
			{
				this.Width = width;
				this.Height = height;
				this.DrawCount++;
				return new FakePaint(new ImageBuffer(width, height).NewGraphics2D());
			}

			public bool BelongsTo(Graphics2D destination) => destination == this.owner;

			public bool NeedsRepaintFor(Graphics2D destination) => false;

			public void Dispose() => this.Disposed = true;
		}

		private class FakePaint : IRetainedLayerPaint
		{
			public FakePaint(Graphics2D graphics) => this.Graphics = graphics;

			public Graphics2D Graphics { get; }

			public void Dispose()
			{
			}
		}

		[Test]
		public async Task WindowsOpenedBeforeLayoutHangFromTheTopOfTheLaidOutCanvas()
		{
			var canvas = new GuiWidget();
			var host = new DemoWindowHost(canvas);
			DemoSpec first = GuiDemoSpecs.All[0];
			await Assert.That(host.IsOpen(first)).IsTrue();

			canvas.Size = new VectorMath.Vector2(1000, 900);
			WindowWidget window = host.GetWindow(first);

			// tile_rect puts spec 0 at 20 from the left and 20 below the top; the widget is its visible card
			// plus the grab border all round.
			double grab = 5 * GuiWidget.DeviceScale;
			await Assert.That(window.Position.X).IsEqualTo(20 - grab);
			await Assert.That(window.Position.Y + window.Height - grab).IsEqualTo(900 - 20.0);
		}

		[Test]
		public async Task EveryTiledWindowFitsANarrowCanvas()
		{
			// The canvas the demo gets between the app's demo list and the sidebar in a 1200-wide window.
			var canvas = new GuiWidget(780, 700);
			var host = new DemoWindowHost(canvas);
			var bounds = new RectangleDouble(0, 0, 780, 700);

			foreach (DemoSpec spec in GuiDemoSpecs.All.Where(s => s.OpenByDefault))
			{
				await Assert.That(bounds.Contains(host.GetVisibleRect(spec).Value)).IsTrue().Because($"'{spec.Title}' opens off the canvas");
			}

			foreach (DemoSpec spec in GuiDemoSpecs.All)
			{
				host.SetOpen(spec, true);
				host.GetWindow(spec).Position = new VectorMath.Vector2(2000, 2000);
			}

			host.Organize();
			foreach (DemoSpec spec in GuiDemoSpecs.All)
			{
				await Assert.That(bounds.Contains(host.GetVisibleRect(spec).Value)).IsTrue().Because($"'{spec.Title}' is tiled off the canvas");
			}
		}

		[Test]
		public async Task AWideCanvasKeepsAggGuisFourColumnPitch()
		{
			// specs.rs: WIN_ORIGIN_X + col * (WIN_W + WIN_GAP_X), four columns.
			for (int index = 0; index < 5; index++)
			{
				RectangleDouble tile = DemoWindowHost.TileRect(index, 1600, 900, 360, 290);
				await Assert.That(tile.Left).IsEqualTo(20 + index % 4 * 380.0);
			}
		}
	}
}
