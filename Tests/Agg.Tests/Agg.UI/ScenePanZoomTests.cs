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
using System.Threading.Tasks;
using MatterHackers.Agg.Transform;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.UI.Tests
{
	// agg-gui's scene/transform.rs and scene/events.rs tests: the pan/zoom maths and the ScenePanZoom gestures.
	public class ScenePanZoomTests
	{
		private const double MinZoom = 0.1;
		private const double MaxZoom = 2;

		private static async Task AssertNear(Vector2 actual, Vector2 expected)
		{
			await Assert.That(Math.Abs(actual.X - expected.X)).IsLessThan(1e-9);
			await Assert.That(Math.Abs(actual.Y - expected.Y)).IsLessThan(1e-9);
		}

		[Test]
		public async Task ScreenSceneRoundTrips()
		{
			var t = new SceneTransform(1.5, new Vector2(10, 20));
			var p = new Vector2(3, 4);
			await AssertNear(t.ScreenToScene(t.SceneToScreen(p)), p);
		}

		[Test]
		public async Task ZoomAtKeepsTheScenePointUnderTheCursor()
		{
			var t = new SceneTransform(2, new Vector2(5, 5));
			var cursor = new Vector2(100, 80);
			Vector2 before = t.ScreenToScene(cursor);
			t = t.ZoomAt(cursor, 0.5, MinZoom, MaxZoom);
			await AssertNear(t.ScreenToScene(cursor), before);
			await Assert.That(t.Zoom).IsEqualTo(0.5);
		}

		[Test]
		public async Task ZoomIsClampedToTheRange()
		{
			var t = SceneTransform.Identity.ZoomAt(new Vector2(50, 50), 10, MinZoom, MaxZoom);
			await Assert.That(t.Zoom).IsEqualTo(2.0);
			t = t.ZoomAt(new Vector2(50, 50), 0.0001, MinZoom, MaxZoom);
			await Assert.That(t.Zoom).IsEqualTo(0.1);
			// A range given backwards is still a range.
			await Assert.That(SceneTransform.ClampZoom(5, MaxZoom, MinZoom)).IsEqualTo(2.0);
		}

		[Test]
		public async Task PanShiftsTheVisibleRectTheOtherWay()
		{
			SceneTransform t = SceneTransform.Identity;
			RectangleDouble before = t.VisibleSceneRect(200, 150);
			await Assert.That(before).IsEqualTo(new RectangleDouble(0, 0, 200, 150));
			RectangleDouble after = t.Pan(new Vector2(30, -10)).VisibleSceneRect(200, 150);
			await Assert.That(after.Left).IsEqualTo(before.Left - 30);
			await Assert.That(after.Bottom).IsEqualTo(before.Bottom + 10);
		}

		[Test]
		public async Task FitCentresTheContentAndClampsTheZoom()
		{
			var content = new RectangleDouble(0, 0, 100, 50);
			SceneTransform t = SceneTransform.Fit(content, 200, 200, MinZoom, MaxZoom);
			await Assert.That(t.Zoom).IsEqualTo(2.0);
			await AssertNear(t.SceneToScreen(content.Center), new Vector2(100, 100));

			await Assert.That(SceneTransform.Fit(new RectangleDouble(0, 0, 1, 1), 1000, 1000, MinZoom, MaxZoom).Zoom).IsEqualTo(2.0);
		}

		[Test]
		public async Task ToAffineMatchesSceneToScreen()
		{
			var t = new SceneTransform(1.75, new Vector2(12, -7));
			Affine m = t.ToAffine();
			foreach (var p in new[] { Vector2.Zero, new Vector2(3, 4), new Vector2(-10, 25) })
			{
				double x = p.X, y = p.Y;
				m.Transform(ref x, ref y);
				await AssertNear(new Vector2(x, y), t.SceneToScreen(p));
			}
		}

		/// <summary>A 400 x 400 scene over an inert 100 x 100 content with one button at (10, 10).</summary>
		private static (ScenePanZoom scene, GuiWidget content, GuiWidget button) BuildScene()
		{
			var content = new GuiWidget(100, 100);
			var button = new GuiWidget(20, 20) { Name = "Scene Button", HAnchor = HAnchor.Absolute, VAnchor = VAnchor.Absolute, OriginRelativeParent = new Vector2(10, 10) };
			content.AddChild(button);
			var scene = new ScenePanZoom(content) { HAnchor = HAnchor.Absolute, VAnchor = VAnchor.Absolute };
			scene.LocalBounds = new RectangleDouble(0, 0, 400, 400);
			return (scene, content, button);
		}

		private static void Press(GuiWidget widget, double x, double y, int clicks = 1) => widget.OnMouseDown(new MouseEventArgs(MouseButtons.Left, clicks, x, y, 0));

		private static void Move(GuiWidget widget, double x, double y) => widget.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, x, y, 0));

		private static void Release(GuiWidget widget, double x, double y) => widget.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, x, y, 0));

		[Test]
		public async Task StartsFittedToTheContent()
		{
			var (scene, _, _) = BuildScene();
			await Assert.That(scene.Transform.Zoom).IsEqualTo(2.0);
			await AssertNear(scene.Transform.SceneToScreen(new Vector2(50, 50)), new Vector2(200, 200));
		}

		[Test]
		public async Task DraggingTheBackgroundPansAndAPressAfterADragDoesNotReset()
		{
			var (scene, _, _) = BuildScene();
			SceneTransform fitted = scene.Transform;
			int changes = 0;
			scene.SceneRectChanged += (s, e) => changes++;

			Press(scene, 200, 200);
			await Assert.That(scene.Panning).IsTrue();
			Move(scene, 250, 260);
			Release(scene, 250, 260);
			await AssertNear(scene.Transform.Offset, fitted.Offset + new Vector2(50, 60));
			await Assert.That(changes).IsGreaterThan(0);

			// A quick press after the drag is not a double-click reset.
			Press(scene, 250, 260, clicks: 2);
			Move(scene, 300, 260);
			Release(scene, 300, 260);
			await AssertNear(scene.Transform.Offset, fitted.Offset + new Vector2(100, 60));
		}

		[Test]
		public async Task DoubleClickOnTheBackgroundResetsTheView()
		{
			var (scene, _, _) = BuildScene();
			SceneTransform fitted = scene.Transform;
			scene.Pan(new Vector2(40, 30));

			// Sub-tolerance jitter still counts as a click.
			Press(scene, 380, 380);
			Release(scene, 380, 380);
			Press(scene, 380, 380, clicks: 2);
			Move(scene, 382, 381);
			Release(scene, 382, 381);
			await AssertNear(scene.Transform.Offset, fitted.Offset);
		}

		[Test]
		public async Task TheWheelZoomsAboutTheCursorWithinTheRange()
		{
			var (scene, _, _) = BuildScene();
			var cursor = new Vector2(120, 300);
			Vector2 sceneUnderCursor = scene.Transform.ScreenToScene(cursor);

			scene.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, cursor.X, cursor.Y, -120));
			await Assert.That(scene.Transform.Zoom).IsLessThan(2.0);
			await AssertNear(scene.Transform.ScreenToScene(cursor), sceneUnderCursor);

			for (int i = 0; i < 100; i++)
			{
				scene.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, cursor.X, cursor.Y, -120));
			}

			await Assert.That(scene.Transform.Zoom).IsEqualTo(MinZoom);
		}

		[Test]
		public async Task AHostedWidgetStillTakesPressesUnderThePanAndZoom()
		{
			var (scene, _, button) = BuildScene();
			scene.ZoomAt(Vector2.Zero, 1.5);
			scene.Pan(new Vector2(-30, 17));
			int presses = 0;
			button.MouseDown += (s, e) => presses++;

			Vector2 onButton = scene.Transform.SceneToScreen(new Vector2(20, 20));
			Press(scene, onButton.X, onButton.Y);
			await Assert.That(presses).IsEqualTo(1);
			await Assert.That(scene.Panning).IsFalse();
			Release(scene, onButton.X, onButton.Y);
		}

		[Test]
		public async Task ATwoFingerMovePinchesAboutTheMidpoint()
		{
			var (scene, _, _) = BuildScene();
			double startZoom = scene.Transform.Zoom;
			scene.ZoomAt(Vector2.Zero, 1);
			Vector2 sceneAtMiddle = scene.Transform.ScreenToScene(new Vector2(200, 200));

			scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, new[] { new Vector2(150, 200), new Vector2(250, 200) }, 0, null));
			scene.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, new[] { new Vector2(125, 200), new Vector2(275, 200) }, 0, null));
			await Assert.That(scene.Transform.Zoom).IsEqualTo(1.5);
			await AssertNear(scene.Transform.ScreenToScene(new Vector2(200, 200)), sceneAtMiddle);
			await Assert.That(startZoom).IsEqualTo(2.0);
		}
	}
}
