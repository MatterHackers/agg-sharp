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

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.Image;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Interaction;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Scene window (agg-gui's scene_demo.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class SceneWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Scene");

		private static SceneWindow Build(DemoTheme demoTheme = null)
		{
			var window = (SceneWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme ?? new DemoTheme());
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return window;
		}

		[Test]
		public async Task BuildsTheSceneAndDrawsInBothThemes()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = Build(demoTheme);

			await Assert.That(window.Name).IsEqualTo("Scene Content");
			foreach (string name in new[] { "Scene Pan Zoom", "Scene Canvas", "Scene Reset View", "Scene Rect Readout", "Scene Increment", "Scene Zero", "Scene Clicks", "Scene Text Field" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			// The view starts fitted, so the readout shows the whole 420 x 300 canvas in view.
			await Assert.That(window.Scene.Height).IsGreaterThan(50);
			await Assert.That(window.Readout.Text).StartsWith("scene_rect: [x ");
			RectangleDouble visible = window.Scene.SceneRect;
			await Assert.That(visible.Width).IsGreaterThanOrEqualTo(window.Canvas.Width - 1e-6);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			await Assert.That(window.Scene.BackgroundColor).IsEqualTo(demoTheme.Palette.BackgroundColor);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task ResetViewUndoesAPanAndZoom()
		{
			var window = Build();
			SceneTransform fitted = window.Scene.Transform;
			string fittedReadout = window.Readout.Text;
			window.Scene.ZoomAt(new Vector2(10, 10), 0.3);
			window.Scene.Pan(new Vector2(25, 5));
			await Assert.That(window.Readout.Text).IsNotEqualTo(fittedReadout);

			window.ResetButton.InvokeClick();
			await Assert.That(window.Scene.Transform.Zoom).IsEqualTo(fitted.Zoom);
			await Assert.That(window.Readout.Text).IsEqualTo(fittedReadout);
		}

		[Test]
		public async Task TheIncrementButtonStillClicksAfterZoomingAndPanning()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (SceneWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var systemWindow = new SystemWindow(600, 400) { Name = "Scene Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				ScenePanZoom scene = window.Scene;

				// Drag the empty lower-right of the canvas to pan, then zoom out about the scene's centre.
				Vector2 empty = scene.Transform.SceneToScreen(new Vector2(window.Canvas.Width - 10, 10));
				testRunner.DragByName("Scene Pan Zoom", offset: new Point2D((int)empty.X, (int)empty.Y), origin: AutomationRunner.ClickOrigin.LowerLeft);
				testRunner.DropByName("Scene Pan Zoom", offset: new Point2D((int)empty.X - 30, (int)empty.Y + 20), origin: AutomationRunner.ClickOrigin.LowerLeft);
				testRunner.WaitFor(() => !scene.Panning);
				scene.ZoomAt(new Vector2(scene.Width / 2, scene.Height / 2), scene.Transform.Zoom * 0.8);

				testRunner.ClickByName("Scene Increment");
				testRunner.WaitFor(() => window.Canvas.Clicks == 1);
				testRunner.ClickByName("Scene Increment");
				testRunner.WaitFor(() => window.Canvas.Clicks == 2);
				await Assert.That(((TextWidget)window.FindDescendant("Scene Clicks")).Text).IsEqualTo("Clicks: 2");

				testRunner.ClickByName("Scene Zero");
				testRunner.WaitFor(() => window.Canvas.Clicks == 0);
				await Assert.That(window.Canvas.Clicks).IsEqualTo(0);
				testRunner.MarkTestComplete();
			});
		}
	}
}
