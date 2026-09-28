/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Widgets;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// app_builder.rs's per-window flags: Frame auto-sizes to its content, Mobile Keyboard holds its height to
	// its content and cannot be resized - both keep their own scroll area, which then has nothing to scroll.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class DemoWindowFlagsTests
	{
		private static (GuiWidget Canvas, WindowWidget Window, GuiWidget Content) Open(string title)
		{
			var canvas = new GuiWidget(1400, 1000);
			var host = new DemoWindowHost(canvas);
			DemoSpec spec = GuiDemoSpecs.All.First(s => s.Title == title);
			host.SetOpen(spec, true);
			canvas.PerformLayout();
			WindowWidget window = host.GetWindow(spec);
			return (canvas, window, window.Descendants().First(w => w.Name == spec.ContentName));
		}

		[Test]
		public async Task FrameAutoSizesToItsRowAndCannotBeResized()
		{
			(GuiWidget canvas, WindowWidget window, GuiWidget content) = Open("Frame");
			var frame = (FrameWindow)content;
			GuiWidget row = frame.FittedContent;

			await Assert.That(window.Resizable).IsFalse();
			await Assert.That(window.Children.OfType<GrabControl>().All(g => !g.Visible)).IsTrue();
			await Assert.That(window.ClientArea.Width).IsEqualTo(row.Width).Within(.5);
			await Assert.That(window.ClientArea.Height).IsEqualTo(row.Height).Within(.5);
			await Assert.That(frame.ScrollArea.VerticalScrollBar.Visible).IsFalse();
			canvas.Close();
		}

		[Test]
		public async Task MobileKeyboardHoldsItsHeightToItsColumnAndCannotBeResized()
		{
			(GuiWidget canvas, WindowWidget window, GuiWidget content) = Open("Mobile Keyboard");
			var keyboard = (MobileKeyboardWindow)content;
			GuiWidget column = keyboard.FittedContent;

			await Assert.That(window.Resizable).IsFalse();
			await Assert.That(window.Children.OfType<GrabControl>().All(g => !g.Visible)).IsTrue();
			await Assert.That(window.ClientArea.Height).IsEqualTo(column.Height + column.Margin.Height).Within(.5);

			// The scroll area stays in the tree (it lifts the focused field above the keyboard) but fits exactly.
			await Assert.That(keyboard.VerticalScrollBar.Visible).IsFalse();
			await Assert.That(column.Parents<ScrollableWidget>().Contains(keyboard)).IsTrue();
			canvas.Close();
		}
	}
}
