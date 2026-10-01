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
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// agg-gui's window.rs fades a window in when it opens and out when it closes: 0.18 s, eased out (cubic), the
	// window drawn until the fade-out ends. The host's clock is replaced so the fade is stepped, not waited for.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class DemoWindowFadeTests
	{
		// The fade's midpoint in time: ease-out cubic is 1 - 0.5^3 of the way there.
		private const long HalfMs = 90;

		private const double EasedHalf = 0.875;

		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		private static (GuiWidget Canvas, DemoWindowHost Host, FakeClock Clock) CreateHost()
		{
			var canvas = new GuiWidget(1000, 700);
			var clock = new FakeClock { NowMs = 1000 };
			return (canvas, new DemoWindowHost(canvas, clockMs: () => clock.NowMs), clock);
		}

		[Test]
		public async Task FadeLastsAggGuisVisibilityFadeSecs()
		{
			await Assert.That(DemoWindowHost.FadeMs).IsEqualTo(180);
		}

		[Test]
		public async Task WindowsOpenAtStartAreFullyShownWithoutAFade()
		{
			var (_, host, _) = CreateHost();
			foreach (DemoSpec spec in GuiDemoSpecs.DefaultOpen)
			{
				await Assert.That(host.GetWindow(spec).BackbufferOpacity).IsEqualTo(1);
			}

			await Assert.That(host.IsFading).IsFalse();
		}

		[Test]
		public async Task OpeningFadesTheWindowIn()
		{
			var (canvas, host, clock) = CreateHost();
			DemoSpec sliders = Spec("Sliders");

			host.SetOpen(sliders, true);
			WindowWidget window = host.GetWindow(sliders);
			await Assert.That(window.Parent).IsEqualTo(canvas);
			await Assert.That(host.IsOpen(sliders)).IsTrue();
			await Assert.That(window.BackbufferOpacity).IsEqualTo(0);
			await Assert.That(host.IsFading).IsTrue();

			clock.NowMs += HalfMs;
			host.StepFades();
			await Assert.That(window.BackbufferOpacity).IsEqualTo(EasedHalf).Within(1e-9);

			clock.NowMs += HalfMs;
			host.StepFades();
			await Assert.That(window.BackbufferOpacity).IsEqualTo(1);
			await Assert.That(host.IsFading).IsFalse();
			await Assert.That(window.DoubleBuffer).IsTrue();
		}

		[Test]
		public async Task ClosingFadesTheWindowOutAndOnlyThenTakesItOffTheCanvas()
		{
			var (canvas, host, clock) = CreateHost();
			DemoSpec spec = GuiDemoSpecs.DefaultOpen.First();
			WindowWidget window = host.GetWindow(spec);

			host.SetOpen(spec, false);

			// Closed at once as far as anything asking is concerned; still drawn while it fades.
			await Assert.That(host.IsOpen(spec)).IsFalse();
			await Assert.That(host.ZOrder.Contains(spec)).IsFalse();
			await Assert.That(window.Parent).IsEqualTo(canvas);
			await Assert.That(window.BackbufferOpacity).IsEqualTo(1);

			clock.NowMs += HalfMs;
			host.StepFades();
			await Assert.That(window.BackbufferOpacity).IsEqualTo(1 - EasedHalf).Within(1e-9);
			await Assert.That(window.Parent).IsEqualTo(canvas);

			clock.NowMs += HalfMs;
			host.StepFades();
			await Assert.That(window.Parent).IsNull();
			await Assert.That(window.DoubleBuffer).IsFalse();
			await Assert.That(host.IsFading).IsFalse();
		}

		[Test]
		public async Task ReopeningWhileFadingOutTurnsTheFadeAroundFromWhereItIs()
		{
			var (canvas, host, clock) = CreateHost();
			DemoSpec spec = GuiDemoSpecs.DefaultOpen.First();
			WindowWidget window = host.GetWindow(spec);

			host.SetOpen(spec, false);
			clock.NowMs += HalfMs;
			host.StepFades();

			host.SetOpen(spec, true);
			await Assert.That(host.IsOpen(spec)).IsTrue();
			await Assert.That(host.ZOrder.Last()).IsEqualTo(spec);
			await Assert.That(window.BackbufferOpacity).IsEqualTo(1 - EasedHalf).Within(1e-9);

			// Long past when the fade-out would have ended: it was turned around, so the window stays.
			clock.NowMs += 1000;
			host.StepFades();
			await Assert.That(window.Parent).IsEqualTo(canvas);
			await Assert.That(window.BackbufferOpacity).IsEqualTo(1);
		}

		[Test]
		public async Task DrawingTheCanvasStepsTheFadeAndStopsAskingForFramesOnceItEnds()
		{
			var (canvas, host, clock) = CreateHost();
			DemoSpec sliders = Spec("Sliders");
			host.SetOpen(sliders, true);
			WindowWidget window = host.GetWindow(sliders);
			var image = new ImageBuffer(1000, 700);

			clock.NowMs += HalfMs;
			int invalidations = 0;
			canvas.Invalidated += (s, e) => invalidations++;
			canvas.OnDraw(image.NewGraphics2D());
			await Assert.That(window.BackbufferOpacity).IsEqualTo(EasedHalf).Within(1e-9);
			await Assert.That(invalidations).IsGreaterThan(0);

			clock.NowMs += HalfMs;
			canvas.OnDraw(image.NewGraphics2D());
			await Assert.That(window.BackbufferOpacity).IsEqualTo(1);

			// Settled: a further frame changes nothing, so nothing asks for another.
			invalidations = 0;
			clock.NowMs += HalfMs;
			canvas.OnDraw(image.NewGraphics2D());
			await Assert.That(invalidations).IsEqualTo(0);
		}

		[Test]
		public async Task AFadeFrameCompositesTheWindowWithoutRepaintingItsContent()
		{
			var (canvas, host, clock) = CreateHost();
			DemoSpec sliders = Spec("Sliders");
			host.SetOpen(sliders, true);
			WindowWidget window = host.GetWindow(sliders);
			int contentPaints = 0;
			window.AfterDraw += (s, e) => contentPaints++;
			var image = new ImageBuffer(1000, 700);

			clock.NowMs += 30;
			canvas.OnDraw(image.NewGraphics2D());
			int afterFirstFrame = contentPaints;
			await Assert.That(afterFirstFrame).IsGreaterThan(0);

			clock.NowMs += 30;
			canvas.OnDraw(image.NewGraphics2D());
			await Assert.That(window.BackbufferOpacity).IsGreaterThan(0).And.IsLessThan(1);
			await Assert.That(contentPaints).IsEqualTo(afterFirstFrame);
		}

		[Test]
		public async Task WindowsRestoredFromSavedStateAppearWithoutAFade()
		{
			var clock = new FakeClock { NowMs = 1000 };
			var store = new MemoryStore();
			DemoSpec sliders = Spec("Sliders");
			DemoSpec closedDefault = GuiDemoSpecs.DefaultOpen.First();

			GuiDemoShell first = LaidOutShell(store, clock);
			first.Windows.SetOpen(sliders, true);
			first.Windows.SetOpen(closedDefault, false);
			clock.NowMs += 1000;
			first.Windows.StepFades();
			first.Persistence.SaveNow();

			GuiDemoShell next = LaidOutShell(store, clock);
			await Assert.That(next.Windows.IsFading).IsFalse();
			await Assert.That(next.Windows.GetWindow(sliders).BackbufferOpacity).IsEqualTo(1);
			await Assert.That(next.Windows.GetWindow(sliders).Parent).IsEqualTo(next.Canvas);
			await Assert.That(next.Windows.IsOpen(closedDefault)).IsFalse();
			await Assert.That(next.Windows.GetWindow(closedDefault)?.Parent).IsNull();
		}

		[Test]
		public async Task AClosingWindowLetsClicksAndHoverThroughWhileItFades()
		{
			var (canvas, host, _) = CreateHost();
			DemoSpec spec = GuiDemoSpecs.DefaultOpen.First();
			WindowWidget window = host.GetWindow(spec);
			RectangleDouble bounds = window.BoundsRelativeToParent;
			double x = bounds.Center.X;
			double y = bounds.Center.Y;
			int windowDowns = 0;
			window.MouseDown += (s, e) => windowDowns++;

			host.SetOpen(spec, false);
			await Assert.That(window.Parent).IsEqualTo(canvas);

			canvas.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, x, y, 0));
			await Assert.That(window.UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);
			canvas.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
			canvas.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
			await Assert.That(windowDowns).IsEqualTo(0);

			// Reopened, it takes the mouse again.
			host.SetOpen(spec, true);
			canvas.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, x, y, 0));
			canvas.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
			canvas.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, x, y, 0));
			await Assert.That(windowDowns).IsEqualTo(1);
		}

		[Test]
		public async Task AClosingWindowTakesNoMouseWheel()
		{
			var (canvas, host, _) = CreateHost();
			DemoSpec spec = GuiDemoSpecs.DefaultOpen.First();
			WindowWidget window = host.GetWindow(spec);
			RectangleDouble bounds = window.BoundsRelativeToParent;
			int windowWheels = 0;
			window.MouseWheel += (s, e) => windowWheels++;

			canvas.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, bounds.Center.X, bounds.Center.Y, 120));
			await Assert.That(windowWheels).IsEqualTo(1);

			host.SetOpen(spec, false);
			canvas.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, bounds.Center.X, bounds.Center.Y, 120));
			await Assert.That(windowWheels).IsEqualTo(1);
		}

		[Test]
		public async Task AClosingWindowGivesUpKeyboardFocus()
		{
			var (_, host, _) = CreateHost();
			DemoSpec textEdit = Spec("TextEdit");
			host.SetOpen(textEdit, true);
			WindowWidget window = host.GetWindow(textEdit);
			GuiWidget field = window.Descendants().First(w => w.CanFocus && w.CanSelect);
			field.Focus();
			await Assert.That(window.ContainsFocus).IsTrue();

			host.SetOpen(textEdit, false);
			await Assert.That(window.ContainsFocus).IsFalse();
			await Assert.That(field.Focused).IsFalse();
		}

		[Test]
		public async Task AClosingWindowIsNotSnappedTo()
		{
			var (canvas, host, _) = CreateHost();
			DemoSpec target = Spec("TextEdit");
			DemoSpec moving = Spec("Sliders");
			host.SetOpen(target, true);
			host.SetOpen(moving, true);
			host.RestoreRect(target, new RectangleDouble(50, 200, 250, 400));
			WindowWidget window = host.GetWindow(moving);
			double targetRight = SnapCoordinator.PanelBounds(host.GetWindow(target)).Right;

			// Dragged to 3 px right of the target's right edge: snapped onto it while the target is open, as
			// window.rs's snap_glue snaps only to windows still asked to be visible.
			double DragLeftEdgeNear()
			{
				host.RestoreRect(moving, new RectangleDouble(400, 150, 600, 300));
				var titleBar = window.TitleBar;
				var mouse = titleBar.TransformToScreenSpace(new VectorMath.Vector2(titleBar.Width / 2, titleBar.Height / 2));
				canvas.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, mouse.X, mouse.Y, 0));
				mouse.X += targetRight + 3 - SnapCoordinator.PanelBounds(window).Left;
				canvas.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, mouse.X, mouse.Y, 0));
				double left = SnapCoordinator.PanelBounds(window).Left;
				canvas.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, mouse.X, mouse.Y, 0));
				return left;
			}

			await Assert.That(DragLeftEdgeNear()).IsEqualTo(targetRight).Within(0.001);

			host.SetOpen(target, false);
			await Assert.That(host.GetWindow(target).Parent).IsEqualTo(canvas);
			await Assert.That(DragLeftEdgeNear()).IsEqualTo(targetRight + 3).Within(0.001);
		}

		private static GuiDemoShell LaidOutShell(IDemoStateStore store, FakeClock clock)
		{
			var page = new GuiWidget(1000, 700);
			var shell = new GuiDemoShell(new DemoTheme(), store, () => clock.NowMs);
			page.AddChild(shell);
			page.PerformLayout();
			return shell;
		}

		private class FakeClock
		{
			public long NowMs { get; set; }
		}

		private class MemoryStore : IDemoStateStore
		{
			private string json;

			public string Load() => this.json;

			public void Save(string json) => this.json = json;

			public void Clear() => this.json = null;
		}
	}
}
