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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo;
using MatterHackers.AggSharpDemo.GuiDemo;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// At DeviceScale 2 (a Retina mac, a browser at devicePixelRatio 2) the GUI demo's frame is laid out in
	// design units, as its text is: the sidebar, its rows, the top bar and the windows' default sizes are all
	// twice their 1x device-pixel sizes, and no sidebar label outgrows its row.
	// Keyless: GuiWidget.DeviceScale is read by nearly every UI test.
	[NotInParallel]
	public class GuiDemoShellDeviceScaleTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		private sealed class Measured
		{
			public double SidebarWidth;
			public double TopBarHeight;
			public double RowHeight;
			public double GroupHeaderHeight;
			public double WindowWidth;
			public double WindowHeight;
			public double LionWindowWidth;
			public double BackendPanelWidth;
			public List<string> ClippedLabels = new List<string>();
			public List<string> ClippedIcons = new List<string>();
		}

		/// <summary>The widget's bounds in <paramref name="ancestor"/>'s coordinates.</summary>
		private static RectangleDouble BoundsIn(GuiWidget widget, GuiWidget ancestor)
		{
			RectangleDouble bounds = widget.LocalBounds;
			for (GuiWidget w = widget; w != ancestor; w = w.Parent)
			{
				bounds.Offset(w.Position);
			}

			return bounds;
		}

		/// <summary>Builds the shell at <paramref name="scale"/> on a page big enough (in design units) that
		/// nothing is clamped, and measures it.</summary>
		private static Measured BuildAt(double scale)
		{
			double saved = GuiWidget.DeviceScale;
			GuiWidget.DeviceScale = scale;
			try
			{
				var page = new GuiWidget(1800 * scale, 1000 * scale);
				var shell = new GuiDemoShell();
				page.AddChild(shell);
				shell.BackendPanel.Visible = true;
				page.PerformLayout();

				var measured = new Measured
				{
					SidebarWidth = shell.Sidebar.Width,
					TopBarHeight = shell.TopBar.Height,
					RowHeight = shell.Sidebar.RowOf(Spec("Sliders")).Height,
					GroupHeaderHeight = shell.Sidebar.HeaderOf("Widgets").Height,
					WindowWidth = shell.Windows.GetVisibleRect(Spec("Widget Gallery")).Value.Width,
					WindowHeight = shell.Windows.GetVisibleRect(Spec("Widget Gallery")).Value.Height,
					LionWindowWidth = shell.Windows.GetVisibleRect(Spec("Lion")).Value.Width,
					BackendPanelWidth = shell.BackendPanel.Width,
				};

				// Every label's text must lie inside the button (row) that holds it.
				foreach (ThemedTextButton row in shell.Sidebar.Descendants<ThemedTextButton>().Where(b => b.Visible))
				{
					foreach (TextWidget text in row.Descendants<TextWidget>().Where(t => t.Visible && t.Text.Length > 0))
					{
						RectangleDouble inRow = BoundsIn(text, row);
						if (inRow.Bottom < row.LocalBounds.Bottom - .5 || inRow.Top > row.LocalBounds.Top + .5)
						{
							measured.ClippedLabels.Add($"{row.Name} '{text.Text}' {inRow} in {row.LocalBounds}");
						}
					}
				}

				// Every row's icon glyph is drawn in IconBounds, which must lie inside the row (a row clips what
				// it draws) and before the row's text.
				foreach (IconTextButton row in shell.Sidebar.Descendants<IconTextButton>().Where(b => b.Visible && b.IconGlyph != null))
				{
					RectangleDouble icon = row.IconBounds;
					RectangleDouble rowBounds = row.LocalBounds;
					double textLeft = row.Descendants<TextWidget>().Min(t => BoundsIn(t, row).Left);
					if (icon.Left < rowBounds.Left || icon.Right > rowBounds.Right
						|| icon.Bottom < rowBounds.Bottom || icon.Top > rowBounds.Top
						|| icon.Right > textLeft)
					{
						measured.ClippedIcons.Add($"{row.Name} icon {icon} in {rowBounds}, text from {textLeft}");
					}
				}

				page.Close();
				return measured;
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}

		[Test]
		public async Task ShellGeometryDoublesAtDeviceScaleTwo()
		{
			Measured one = BuildAt(1);
			Measured two = BuildAt(2);

			await Assert.That(two.SidebarWidth).IsEqualTo(one.SidebarWidth * 2);
			await Assert.That(two.TopBarHeight).IsEqualTo(one.TopBarHeight * 2);
			await Assert.That(two.RowHeight).IsEqualTo(one.RowHeight * 2);
			await Assert.That(two.GroupHeaderHeight).IsEqualTo(one.GroupHeaderHeight * 2);
			await Assert.That(two.BackendPanelWidth).IsEqualTo(one.BackendPanelWidth * 2);
			await Assert.That(two.WindowWidth).IsEqualTo(one.WindowWidth * 2);
			await Assert.That(two.WindowHeight).IsEqualTo(one.WindowHeight * 2);
			await Assert.That(two.LionWindowWidth).IsEqualTo(one.LionWindowWidth * 2);
			await Assert.That(one.ClippedLabels).IsEmpty();
			await Assert.That(two.ClippedLabels).IsEmpty();
			await Assert.That(one.ClippedIcons).IsEmpty();
			await Assert.That(two.ClippedIcons).IsEmpty();
		}

		// The browser head builds the app in a SystemWindow of its constructor size (1200 x 800) and only then
		// learns the canvas's backing size (device pixels) and devicePixelRatio. The default windows must
		// still open inside the canvas, from its top, as they do when the window starts at its final size.
		[Test]
		public async Task DefaultWindowsOpenAtTheCanvasTopAfterTheBrowserSizesTheCanvas()
		{
			double saved = GuiWidget.DeviceScale;
			GuiWidget.DeviceScale = 2;
			try
			{
				var window = new SystemWindow(1200, 800);
				var app = new AggSharpDemoApp(AggSharpDemoApp.GuiDemoName);
				window.AddChild(app);
				window.PerformLayout();

				// BrowserSystemWindow.ApplyBackingSize: the canvas at dpr 2, then the display scale.
				window.SetBoundsFromPlatform(2800, 1800);
				window.SetDisplayScale(2);
				UiThread.InvokePendingActions();
				window.PerformLayout();

				GuiDemoShell shell = app.Descendants<GuiDemoShell>().Single();
				GuiWidget canvas = shell.Canvas;
				var openSpecs = GuiDemoSpecs.All.Where(s => shell.Windows.IsOpen(s)).ToList();
				await Assert.That(openSpecs).IsNotEmpty();

				double highestTop = double.MinValue;
				foreach (DemoSpec spec in openSpecs)
				{
					RectangleDouble rect = shell.Windows.GetVisibleRect(spec).Value;
					bool inside = rect.Left >= 0 && rect.Bottom >= 0 && rect.Right <= canvas.Width && rect.Top <= canvas.Height;
					await Assert.That(inside).IsTrue().Because($"'{spec.Title}' at {rect} should be inside the {canvas.Width} x {canvas.Height} canvas");
					await Assert.That(rect.Width).IsEqualTo(spec.DefaultWidth * 2).Because($"'{spec.Title}' should open at its default size");
					highestTop = System.Math.Max(highestTop, rect.Top);
				}

				// The 1x tiling starts TileOrigin (20 design units) below the canvas top.
				await Assert.That(highestTop).IsEqualTo(canvas.Height - 20 * 2);

				window.Close();
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
				UiThread.ResetForTests();
			}
		}

		// An AGG demo keeps its C++ size in design units: at DeviceScale 2 it is drawn at least 2 device
		// pixels per demo pixel, even in a view too small to fit it, so it looks the size it does at 1x.
		[Test]
		public async Task AggDemoIsDrawnAtLeastAtTheDeviceScale()
		{
			double saved = GuiWidget.DeviceScale;
			GuiWidget.DeviceScale = 2;
			try
			{
				var view = new AggDemoView(new MatterHackers.AggSharpDemo.Demos.LionDemo())
				{
					Width = 500,
					Height = 500,
				};

				await Assert.That(view.Layout.Scale).IsEqualTo(2);
			}
			finally
			{
				GuiWidget.DeviceScale = saved;
			}
		}
	}
}
