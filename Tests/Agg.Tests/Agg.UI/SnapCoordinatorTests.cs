/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
*/

using System;
using System.Linq;
using System.Reflection;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	/// <summary>
	/// <see cref="SnapCoordinator"/> driven through the real title bar and grab handle drags: a window let go
	/// a few pixels from another lands flush against it with a guide showing why, a resized edge does the
	/// same, turning snapping off leaves the window where the mouse put it, and the guides only exist while
	/// the button is down.
	/// </summary>
	public class SnapCoordinatorTests
	{
		// comfortably inside SnapEngine.DefaultThreshold (8)
		private const double NearMiss = 3;

		[Test]
		public async Task TitleBarDragNearAnotherWindowSnapsFlushAndShowsAGuideUntilRelease()
		{
			var scene = new Scene();
			RectangleDouble fixedPanel = SnapCoordinator.PanelBounds(scene.Fixed);

			var mouse = scene.PressTitleBar(scene.Moving);
			mouse = scene.MoveMouse(mouse, fixedPanel.Right + NearMiss - SnapCoordinator.PanelBounds(scene.Moving).Left, 0);

			RectangleDouble snapped = SnapCoordinator.PanelBounds(scene.Moving);
			await Assert.That(snapped.Left).IsEqualTo(fixedPanel.Right).Within(0.001)
				.Because($"a window let go {NearMiss} px from another's right edge sits flush against it");
			await Assert.That(scene.Coordinator.Guides.Any(g => g.Kind == SnapGuideKind.VLine && g.At == fixedPanel.Right)).IsTrue()
				.Because("the snap is explained by a vertical guide on the shared edge");
			await Assert.That(scene.Coordinator.Overlay.Guides.Count).IsGreaterThan(0);

			// the overlay paints the guide over the windows: the shared edge is cyan on the pixel beside it
			var image = new ImageBuffer(800, 600);
			scene.SystemWindow.OnDraw(image.NewGraphics2D());
			var guidePixel = image.GetPixel((int)fixedPanel.Right, (int)((snapped.Bottom + snapped.Top) / 2));
			await Assert.That((int)guidePixel.blue).IsGreaterThan(guidePixel.red + 100)
				.Because($"the alignment guide is drawn in agg-gui's cyan, found {guidePixel}");

			// a further move keeps the snap - it does not accumulate the nudge
			mouse = scene.MoveMouse(mouse, 0, 1);
			await Assert.That(SnapCoordinator.PanelBounds(scene.Moving).Left).IsEqualTo(fixedPanel.Right).Within(0.001);

			scene.SystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, mouse.X, mouse.Y, 0));
			await Assert.That(scene.Coordinator.Guides.Count).IsEqualTo(0)
				.Because("the guides are only shown while the window is being dragged");
			await Assert.That(SnapCoordinator.PanelBounds(scene.Moving).Left).IsEqualTo(fixedPanel.Right).Within(0.001)
				.Because("letting go leaves the window where it snapped");
		}

		[Test]
		public async Task ResizingAnEdgeNearAnotherWindowSnapsOnlyThatEdge()
		{
			var scene = new Scene();
			RectangleDouble movingPanel = SnapCoordinator.PanelBounds(scene.Moving);
			RectangleDouble fixedPanel = SnapCoordinator.PanelBounds(scene.Fixed);

			// drag the fixed window's right edge to within a near miss of the moving window's left
			var grab = scene.Fixed.Children.OfType<GrabControl>().First(g => g.Edge == ResizeEdge.East);
			var mouse = grab.TransformToScreenSpace(new Vector2(grab.Width / 2, grab.Height / 2));
			scene.SystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, mouse.X, mouse.Y, 0));
			mouse = scene.MoveMouse(mouse, movingPanel.Left - NearMiss - fixedPanel.Right, 0);

			RectangleDouble resized = SnapCoordinator.PanelBounds(scene.Fixed);
			await Assert.That(resized.Right).IsEqualTo(movingPanel.Left).Within(0.001)
				.Because($"the right edge dragged to {NearMiss} px short of the other window snaps onto it");
			await Assert.That(resized.Left).IsEqualTo(fixedPanel.Left).Within(0.001)
				.Because("a resize from the right edge never moves the left one");
			await Assert.That(scene.Coordinator.Guides.Count).IsGreaterThan(0);

			scene.SystemWindow.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, mouse.X, mouse.Y, 0));
			await Assert.That(scene.Coordinator.Guides.Count).IsEqualTo(0);
		}

		[Test]
		public async Task DisabledLeavesTheWindowWhereTheMousePutsIt()
		{
			var scene = new Scene();
			scene.Coordinator.Enabled = false;
			RectangleDouble fixedPanel = SnapCoordinator.PanelBounds(scene.Fixed);

			var mouse = scene.PressTitleBar(scene.Moving);
			scene.MoveMouse(mouse, fixedPanel.Right + NearMiss - SnapCoordinator.PanelBounds(scene.Moving).Left, 0);

			await Assert.That(SnapCoordinator.PanelBounds(scene.Moving).Left).IsEqualTo(fixedPanel.Right + NearMiss).Within(0.001)
				.Because("with snapping off the window follows the mouse exactly");
			await Assert.That(scene.Coordinator.Guides.Count).IsEqualTo(0);
		}

		// Regression: the pointer leaving the platform window arrives as a buttonless move, which ends the title
		// bar's drag without a mouse up - the guides were left painted until the next drag.
		[Test]
		public async Task ADragEndedByAButtonlessMoveClearsTheGuides()
		{
			var scene = new Scene();
			RectangleDouble fixedPanel = SnapCoordinator.PanelBounds(scene.Fixed);

			var mouse = scene.PressTitleBar(scene.Moving);
			scene.MoveMouse(mouse, fixedPanel.Right + NearMiss - SnapCoordinator.PanelBounds(scene.Moving).Left, 0);
			await Assert.That(scene.Coordinator.Guides.Count).IsGreaterThan(0);

			scene.SystemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, -10, -10, 0));
			await Assert.That(scene.Coordinator.Guides.Count).IsEqualTo(0)
				.Because("a drag that ends without a mouse up still clears its guides");
		}

		// agg-gui snaps and then clamps to the canvas (widgets/window/events.rs), so a snap to a window that pokes
		// out of the top can never pull the dragged window out after it.
		[Test]
		public async Task ASnapCannotPushAWindowAboveTheContainerTop()
		{
			var scene = new Scene();
			double containerTop = scene.Moving.Parent.Height;

			// the fixed window's panel top pokes out of the container, within snapping distance of its top
			scene.Fixed.Position += new Vector2(0, containerTop + NearMiss - SnapCoordinator.PanelBounds(scene.Fixed).Top);

			// drag the moving window as far up as the clamp lets it go, putting its panel top near the fixed one's
			var mouse = scene.PressTitleBar(scene.Moving);
			scene.MoveMouse(mouse, 0, 400);

			await Assert.That(scene.Moving.Position.Y + scene.Moving.Height).IsLessThanOrEqualTo(containerTop + 0.001)
				.Because("the clamp to the container runs after the snap, so the window's top stays inside it");
		}

		[Test]
		public async Task AClosedWindowIsDetached()
		{
			var scene = new Scene();
			TitleBarWidget titleBar = scene.Moving.TitleBar;
			await Assert.That(SubscriberCount(titleBar, "DragMoved")).IsEqualTo(1);

			scene.Moving.Close();

			await Assert.That(SubscriberCount(titleBar, "DragMoved")).IsEqualTo(0)
				.Because("closing a window lets go of it, so short-lived windows do not pile up in the coordinator");
			await Assert.That(SubscriberCount(titleBar, "DragEnded")).IsEqualTo(0);
			foreach (GrabControl grab in scene.Moving.Children.OfType<GrabControl>())
			{
				await Assert.That(SubscriberCount(grab, "DragMoved")).IsEqualTo(0);
			}
		}

		// The minimum size can stop a resize short of where the snap put the edge; the guide would then mark an
		// alignment that is not there.
		[Test]
		public async Task AResizeStoppedByTheMinimumSizeShowsNoGuides()
		{
			var scene = new Scene();
			WindowWidget window = scene.Fixed;
			RectangleDouble start = SnapCoordinator.PanelBounds(window);
			double minimumWidth = window.Width - 60;
			window.MinimumSize = new Vector2(minimumWidth, window.MinimumSize.Y);

			// drag the left edge in to 2 px wider than the minimum, with another window's right edge 5 px further in
			double draggedLeft = start.Left + 58;
			scene.Moving.Position += new Vector2(draggedLeft + 5 - SnapCoordinator.PanelBounds(scene.Moving).Right, 0);

			// the other window now overlaps this one's left edge; put this one on top so the press finds its handle
			window.BringToFront();

			var grab = window.Children.OfType<GrabControl>().First(g => g.Edge == ResizeEdge.West);
			var mouse = grab.TransformToScreenSpace(new Vector2(grab.Width / 2, grab.Height / 2));
			scene.SystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, mouse.X, mouse.Y, 0));
			scene.MoveMouse(mouse, 58, 0);

			RectangleDouble resized = SnapCoordinator.PanelBounds(window);
			await Assert.That(window.Width).IsEqualTo(minimumWidth).Within(0.001)
				.Because("the snap asked for a width under the minimum, which stops it");
			await Assert.That(resized.Right).IsEqualTo(start.Right).Within(0.001)
				.Because("the stopped resize leaves the other edge where it was");
			await Assert.That(scene.Coordinator.Guides.Count).IsEqualTo(0)
				.Because("the window did not reach the snap, so there is no alignment to show");
		}

		private static int SubscriberCount(object instance, string eventName)
		{
			FieldInfo field = instance.GetType().GetField(eventName, BindingFlags.NonPublic | BindingFlags.Instance);
			var handlers = (Delegate)field.GetValue(instance);
			return handlers?.GetInvocationList().Length ?? 0;
		}

		/// <summary>Two windows floating side by side in a container, both attached to one coordinator.</summary>
		private class Scene
		{
			public Scene()
			{
				SystemWindow = new SystemWindow(800, 600);
				var container = new GuiWidget()
				{
					HAnchor = HAnchor.Stretch,
					VAnchor = VAnchor.Stretch,
				};
				SystemWindow.AddChild(container);
				Coordinator = new SnapCoordinator(container);

				Fixed = new WindowWidget(new ThemeConfig(), new RectangleDouble(50, 200, 250, 400));
				Moving = new WindowWidget(new ThemeConfig(), new RectangleDouble(400, 150, 600, 300));
				container.AddChild(Fixed);
				container.AddChild(Moving);
				Coordinator.Attach(Fixed);
				Coordinator.Attach(Moving);
				SystemWindow.PerformLayout();
			}

			public SystemWindow SystemWindow { get; }

			public SnapCoordinator Coordinator { get; }

			public WindowWidget Fixed { get; }

			public WindowWidget Moving { get; }

			public Vector2 PressTitleBar(WindowWidget window)
			{
				var titleBar = window.TitleBar;
				var mouse = titleBar.TransformToScreenSpace(new Vector2(titleBar.Width / 2, titleBar.Height / 2));
				SystemWindow.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, mouse.X, mouse.Y, 0));
				return mouse;
			}

			public Vector2 MoveMouse(Vector2 mouse, double dx, double dy)
			{
				mouse += new Vector2(dx, dy);
				SystemWindow.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mouse.X, mouse.Y, 0));
				return mouse;
			}
		}
	}
}
