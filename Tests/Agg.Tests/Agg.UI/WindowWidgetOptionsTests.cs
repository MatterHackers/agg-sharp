/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// The runtime options of a <see cref="WindowWidget"/> (title, resizable, collapsible, auto-size) change the
	/// live window, and a window that sets none of them behaves as it always has.
	/// </summary>
	/// <remarks>Keyless <c>[NotInParallel]</c>: the window's chrome is sized from the process-wide DeviceScale.</remarks>
	[NotInParallel]
	public class WindowWidgetOptionsTests
	{
		private static (WindowWidget Window, GuiWidget Parent) Build()
		{
			var parent = new GuiWidget(800, 600);
			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(100, 100, 400, 350));
			window.AddTitleBar("Original", null);
			parent.AddChild(window);
			parent.PerformLayout();
			return (window, parent);
		}

		private static bool GrabControlsShown(WindowWidget window) => window.Children.OfType<GrabControl>().All(g => g.Visible);

		private static bool GrabControlsHidden(WindowWidget window) => window.Children.OfType<GrabControl>().All(g => !g.Visible);

		[Test]
		public async Task DefaultsAreUnchanged()
		{
			(WindowWidget window, GuiWidget _) = Build();
			await Assert.That(window.Resizable).IsTrue();
			await Assert.That(window.Collapsible).IsFalse();
			await Assert.That(window.Collapsed).IsFalse();
			await Assert.That(window.AutoSize).IsFalse();
			await Assert.That(window.Title).IsEqualTo("Original");
			await Assert.That(GrabControlsShown(window)).IsTrue();
			await Assert.That(window.FindDescendant("Window Collapse Button").Visible).IsFalse();
		}

		[Test]
		public async Task TitleChangesTheTitleBarText()
		{
			(WindowWidget window, GuiWidget _) = Build();
			window.Title = "Renamed";
			await Assert.That(window.TitleBar.Descendants<TextWidget>().Any(t => t.Text == "Renamed")).IsTrue();
			await Assert.That(window.TitleBar.Descendants<TextWidget>().Any(t => t.Text == "Original")).IsFalse();
		}

		[Test]
		public async Task ResizableTogglesTheGrabHandles()
		{
			(WindowWidget window, GuiWidget _) = Build();
			window.Resizable = false;
			await Assert.That(GrabControlsHidden(window)).IsTrue();
			window.Resizable = true;
			await Assert.That(GrabControlsShown(window)).IsTrue();
		}

		[Test]
		public async Task CollapsingFoldsToTheTitleBarKeepingTheTopEdge()
		{
			(WindowWidget window, GuiWidget _) = Build();
			window.Collapsible = true;
			GuiWidget chevron = window.FindDescendant("Window Collapse Button");
			await Assert.That(chevron.Visible).IsTrue();

			double top = window.Position.Y + window.Height;
			double height = window.Height;
			chevron.InvokeClick();
			await Assert.That(window.Collapsed).IsTrue();
			await Assert.That(window.ClientArea.Visible).IsFalse();
			await Assert.That(window.Height).IsLessThan(window.TitleBar.Height * 2);
			await Assert.That(window.Position.Y + window.Height).IsEqualTo(top).Within(.001);
			await Assert.That(GrabControlsHidden(window)).IsTrue();

			chevron.InvokeClick();
			await Assert.That(window.Collapsed).IsFalse();
			await Assert.That(window.Height).IsEqualTo(height).Within(.001);
			await Assert.That(window.Position.Y + window.Height).IsEqualTo(top).Within(.001);
			await Assert.That(GrabControlsShown(window)).IsTrue();

			// No longer collapsible: the chevron goes and a folded window unfolds.
			window.Collapsed = true;
			window.Collapsible = false;
			await Assert.That(chevron.Visible).IsFalse();
			await Assert.That(window.Collapsed).IsFalse();
			await Assert.That(window.Height).IsEqualTo(height).Within(.001);
		}

		[Test]
		public async Task AutoSizeFitsTheHeightToTheContentAndFollowsIt()
		{
			(WindowWidget window, GuiWidget _) = Build();
			var content = new GuiWidget(100, 40) { VAnchor = VAnchor.Absolute | VAnchor.Top };
			window.ClientArea.AddChild(content);
			double top = window.Position.Y + window.Height;
			double width = window.Width;

			window.AutoSize = true;
			await Assert.That(window.ClientArea.Height).IsEqualTo(40).Within(.5);
			await Assert.That(window.Position.Y + window.Height).IsEqualTo(top).Within(.001);
			await Assert.That(window.Width).IsEqualTo(width);
			await Assert.That(GrabControlsHidden(window)).IsTrue();

			content.Height = 120;
			await Assert.That(window.ClientArea.Height).IsEqualTo(120).Within(.5);
			await Assert.That(window.Position.Y + window.Height).IsEqualTo(top).Within(.001);

			window.AutoSize = false;
			await Assert.That(GrabControlsShown(window)).IsTrue();
		}
	}
}
