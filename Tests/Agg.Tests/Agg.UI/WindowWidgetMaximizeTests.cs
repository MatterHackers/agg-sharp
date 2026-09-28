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
	/// A <see cref="WindowWidget.Maximizable"/> window fills its parent from the title bar's maximize button or
	/// a double-click on the bar, and the same again restores it, as agg-gui's Window does.
	/// </summary>
	/// <remarks>Keyless <c>[NotInParallel]</c>: the window's chrome is sized from the process-wide DeviceScale.</remarks>
	[NotInParallel]
	public class WindowWidgetMaximizeTests
	{
		private static (SystemWindow Host, WindowWidget Window) Build(bool maximizable = true)
		{
			var host = new SystemWindow(800, 600);
			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(100, 100, 400, 350))
			{
				Maximizable = maximizable,
			};
			window.AddTitleBar("Window", () => { });
			host.AddChild(window);
			host.PerformLayout();
			return (host, window);
		}

		private static void DoubleClickTitleBar(SystemWindow host, WindowWidget window)
		{
			// the left part of the bar, clear of the buttons at its right hand end
			Vector2 at = window.TitleBar.TransformToScreenSpace(new Vector2(window.TitleBar.Width / 3, window.TitleBar.Height / 2));
			host.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, at.X, at.Y, 0));
			host.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, at.X, at.Y, 0));
			host.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 2, at.X, at.Y, 0));
			host.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, at.X, at.Y, 0));
		}

		[Test]
		public async Task OffByDefaultWithNoButton()
		{
			var window = new WindowWidget(new ThemeConfig(), new RectangleDouble(0, 0, 100, 100));
			window.AddTitleBar("Window", () => { });
			await Assert.That(window.Maximizable).IsFalse();
			await Assert.That(window.FindDescendant("Window Maximize Button").Visible).IsFalse();

			(SystemWindow host, WindowWidget plain) = Build(maximizable: false);
			Vector2 position = plain.Position;
			DoubleClickTitleBar(host, plain);
			await Assert.That(plain.Maximized).IsFalse();
			await Assert.That(plain.Position).IsEqualTo(position);
		}

		[Test]
		public async Task DoubleClickMaximizesAndRestores()
		{
			(SystemWindow host, WindowWidget window) = Build();
			Vector2 position = window.Position;
			Vector2 size = window.Size;

			DoubleClickTitleBar(host, window);
			await Assert.That(window.Maximized).IsTrue();
			RectangleDouble visible = window.Children.First(c => !(c is GrabControl)).TransformToParentSpace(window, window.Children.First(c => !(c is GrabControl)).LocalBounds);
			visible.Offset(window.Position);
			await Assert.That(visible.Left).IsEqualTo(0).Within(.001);
			await Assert.That(visible.Bottom).IsEqualTo(0).Within(.001);
			await Assert.That(visible.Width).IsEqualTo(host.Width).Within(.001);
			await Assert.That(visible.Height).IsEqualTo(host.Height).Within(.001);
			await Assert.That(window.Children.OfType<GrabControl>().All(g => !g.Visible)).IsTrue();

			// it keeps filling the parent as the parent resizes
			host.Size = new Vector2(1000, 700);
			await Assert.That(window.Width).IsEqualTo(1000 + (size.X - 300)).Within(.001);

			DoubleClickTitleBar(host, window);
			await Assert.That(window.Maximized).IsFalse();
			await Assert.That(window.Position).IsEqualTo(position);
			await Assert.That(window.Size).IsEqualTo(size);
			await Assert.That(window.Children.OfType<GrabControl>().All(g => g.Visible)).IsTrue();
		}

		[Test]
		public async Task ButtonTogglesAndAMaximizedWindowDoesNotDrag()
		{
			(SystemWindow host, WindowWidget window) = Build();
			GuiWidget button = window.FindDescendant("Window Maximize Button");
			await Assert.That(button.Visible).IsTrue();

			int changes = 0;
			window.MaximizedChanged += (s, e) => changes++;
			button.InvokeClick();
			await Assert.That(window.Maximized).IsTrue();
			await Assert.That(window.RestoreBounds.Left).IsEqualTo(100 - 5).Within(.001);

			Vector2 maximizedAt = window.Position;
			Vector2 at = window.TitleBar.TransformToScreenSpace(new Vector2(window.TitleBar.Width / 3, window.TitleBar.Height / 2));
			host.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, at.X, at.Y, 0));
			host.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, at.X - 40, at.Y - 40, 0));
			host.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, at.X - 40, at.Y - 40, 0));
			await Assert.That(window.Position).IsEqualTo(maximizedAt);

			button.InvokeClick();
			await Assert.That(window.Maximized).IsFalse();
			await Assert.That(changes).IsEqualTo(2);
		}
	}
}
