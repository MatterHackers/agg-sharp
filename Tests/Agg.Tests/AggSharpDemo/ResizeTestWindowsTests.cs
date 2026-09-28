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
using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The six Window Resize Test windows and the window behaviour flags their specs carry: auto-size, window
	// scroll and a height held to the content.
	// Builds every demo window, About included: new DemoTheme() writes ThemeConfig.Current and the About
	// window's MarkdownWidget writes MarkdownWidget.Theme.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class ResizeTestWindowsTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		private static (DemoWindowHost Host, WindowWidget Window, GuiWidget Content) Open(string title)
		{
			var canvas = new GuiWidget(1200, 900);
			var host = new DemoWindowHost(canvas);
			DemoSpec spec = Spec(title);
			host.SetOpen(spec, true);
			WindowWidget window = host.GetWindow(spec);
			GuiWidget content = window.Descendants().First(w => w.Name == spec.ContentName);
			return (host, window, content);
		}

		private static Vector2 Chrome(WindowWidget window) => window.Size - window.ClientArea.Size;

		[Test]
		public async Task OnlyTheResizeTestSpecsCarryWindowBehaviourFlags()
		{
			// Every other window keeps the host's plain resizable window, but for the two app_builder.rs sets flags
			// on (Frame, Mobile Keyboard - DemoWindowFlagsTests).
			foreach (DemoSpec spec in GuiDemoSpecs.All.Where(s => s.Group != "Window Resize Test" && s.Title != "Frame" && s.Title != "Mobile Keyboard"))
			{
				await Assert.That(spec.AutoSize || spec.VerticalScroll || spec.FitHeightToContent || !spec.Resizable).IsFalse();
			}

			await Assert.That(Spec("↔ auto-sized").AutoSize).IsTrue();
			await Assert.That(Spec("↔ resizable + scroll").VerticalScroll).IsTrue();
			await Assert.That(Spec("↔ resizable without scroll").FitHeightToContent).IsTrue();
		}

		[Test]
		public async Task AnAutoSizedWindowIsItsContentsSizeAndFollowsTheResizeArea()
		{
			var (_, window, content) = Open("↔ auto-sized");

			await Assert.That(window.ClientArea.Width).IsEqualTo(content.Width).Within(0.5);
			await Assert.That(window.ClientArea.Height).IsEqualTo(content.Height).Within(0.5);
			await Assert.That(window.Children.OfType<GrabControl>().Any(g => g.Visible)).IsFalse()
				.Because("the user cannot resize an auto-sized window; its content sizes it");

			double top = window.Position.Y + window.Height;
			var resizeArea = content.Descendants<ResizeArea>().Single();
			double widthBefore = window.Width;
			double heightBefore = window.Height;
			resizeArea.Size = resizeArea.Size + new Vector2(100, 50);

			await Assert.That(window.Width).IsEqualTo(widthBefore + 100).Within(0.5);
			await Assert.That(window.Height).IsEqualTo(heightBefore + 50).Within(0.5);
			await Assert.That(window.Position.Y + window.Height).IsEqualTo(top).Within(0.5)
				.Because("the window grows down from its title bar");
		}

		[Test]
		public async Task AHeightFittedWindowCannotBeResizedTallerOrShorterThanItsContent()
		{
			var (_, window, content) = Open("↔ resizable without scroll");
			double fitted = content.Height + Chrome(window).Y;
			await Assert.That(window.Height).IsEqualTo(fitted).Within(0.5);

			// Height drags are clamped by the floor and ceiling.
			window.Height = fitted + 200;
			await Assert.That(window.Height).IsEqualTo(fitted).Within(0.5);
			window.Height = fitted - 100;
			await Assert.That(window.Height).IsEqualTo(fitted).Within(0.5);

			// Narrowing re-wraps the text taller, and the window follows it.
			window.Width = window.Width - 100;
			await Assert.That(content.Height + Chrome(window).Y).IsGreaterThan(fitted);
			await Assert.That(window.Height).IsEqualTo(content.Height + Chrome(window).Y).Within(0.5);
		}

		[Test]
		public async Task AWindowWithVerticalScrollScrollsItsContentWhenShort()
		{
			var (_, window, content) = Open("↔ resizable + scroll");
			var scroll = window.ClientArea.Children.OfType<ScrollableWidget>().Single();
			await Assert.That(content.Parent?.Parent).IsEqualTo(scroll);

			window.Height = 150 * GuiWidget.DeviceScale;
			await Assert.That(content.Height).IsGreaterThan(scroll.Height);
			await Assert.That(scroll.VerticalScrollBar.Visible).IsTrue();
		}

		[Test]
		[Arguments("↔ auto-sized")]
		[Arguments("↔ resizable + scroll")]
		[Arguments("↔ resizable + embedded scroll")]
		[Arguments("↔ resizable without scroll")]
		[Arguments("↔ resizable with TextEdit")]
		[Arguments("↔ freely resized")]
		public async Task EachWindowBuildsAndDrawsOffscreen(string title)
		{
			var (_, window, content) = Open(title);
			await Assert.That(content.Descendants<TextWidget>().Any(t => t.Text.StartsWith("Coming soon"))).IsFalse();

			var image = new ImageBuffer((int)Math.Ceiling(window.Width), (int)Math.Ceiling(window.Height));
			window.OnDraw(image.NewGraphics2D());
			await Assert.That(content.Width).IsGreaterThan(0);
			await Assert.That(content.Height).IsGreaterThan(0);
		}
	}
}
