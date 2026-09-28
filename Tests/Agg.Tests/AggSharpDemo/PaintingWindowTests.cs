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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class PaintingWindowTests
	{
		private static PaintingWindow Build()
		{
			var spec = GuiDemoSpecs.All.First(s => s.Title == "Painting");
			var window = (PaintingWindow)GuiDemoSpecs.CreateContent(spec, new DemoTheme());
			var host = new GuiWidget(spec.DefaultWidth, spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return window;
		}

		[Test]
		public async Task BuildsTheStrokeEditorAndCanvas()
		{
			var window = Build();

			await Assert.That(window.FindDescendant("Painting Canvas")).IsSameReferenceAs(window.Canvas);
			await Assert.That(window.FindDescendant("Painting Stroke Width")).IsSameReferenceAs(window.WidthDrag);
			await Assert.That(window.FindDescendant("Painting Clear")).IsSameReferenceAs(window.ClearButton);
			await Assert.That(window.Swatches.Count).IsEqualTo(5);
			await Assert.That(window.FindDescendant("Painting Swatch 4")).IsSameReferenceAs(window.Swatches[4]);
			await Assert.That(window.Swatches[0].Selected).IsTrue();
			await Assert.That(window.Canvas.Height).IsGreaterThan(50);

			// The toolbar sits above the canvas (Y-up).
			await Assert.That(window.ClearButton.TransformToScreenSpace(window.ClearButton.LocalBounds).Bottom)
				.IsGreaterThanOrEqualTo(window.Canvas.TransformToScreenSpace(window.Canvas.LocalBounds).Top);

			// The hint sits inside the window's padding.
			var hint = window.Descendants<TextWidget>().First(t => t.Text == "Paint with your mouse/touch!");
			await Assert.That(hint.TransformToScreenSpace(hint.LocalBounds).Left).IsGreaterThanOrEqualTo(window.Padding.Left);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task DraggingPaintsAStrokeThatSwatchAndClearChange()
		{
			var window = Build();
			var canvas = window.Canvas;

			canvas.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 10, 10, 0));
			canvas.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 10, 10, 0));
			canvas.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 40, 30, 0));
			canvas.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, 40, 30, 0));
			canvas.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, 60, 60, 0));

			// The duplicate point and the move after release are not recorded.
			await Assert.That(canvas.Strokes.Count).IsEqualTo(1);
			await Assert.That(canvas.Strokes[0].Count).IsEqualTo(2);
			Vector2 end = PaintingCanvas.ToScreen(canvas.Strokes[0][1], canvas.Width, canvas.Height);
			await Assert.That((end - new Vector2(40, 30)).Length).IsLessThan(1e-9);

			// A wide stroke covers the pixel on the line fully rather than anti-aliasing it.
			window.WidthDrag.Value = 6;
			await Assert.That(canvas.StrokeWidth).IsEqualTo(6);
			var image = new ImageBuffer((int)canvas.Width, (int)canvas.Height);
			canvas.OnDraw(image.NewGraphics2D());
			await Assert.That(image.GetPixel(25, 20)).IsEqualTo(PaintingCanvas.DefaultStrokeColor);

			window.Swatches[1].OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 5, 5, 0));
			await Assert.That(canvas.StrokeColor).IsEqualTo(window.Swatches[1].Color);
			await Assert.That(window.Swatches[0].Selected).IsFalse();

			window.ClearButton.InvokeClick();
			await Assert.That(canvas.Strokes.Count).IsEqualTo(0);
		}

		[Test]
		public async Task NormalizedPointsRoundTripAndRescale()
		{
			foreach (var (w, h) in new[] { (300.0, 300.0), (512.0, 384.0), (200.0, 500.0) })
			{
				var p = new Vector2(w * .3, h * .7);
				Vector2 back = PaintingCanvas.ToScreen(PaintingCanvas.FromScreen(p, w, h), w, h);
				await Assert.That((back - p).Length).IsLessThan(1e-9);
			}

			// A point keeps its fractional place after an aspect-preserving resize.
			Vector2 n = PaintingCanvas.FromScreen(new Vector2(90, 60), 300, 200);
			await Assert.That((PaintingCanvas.ToScreen(n, 600, 400) - new Vector2(180, 120)).Length).IsLessThan(1e-9);
			await Assert.That(PaintingCanvas.SquareProportions(400, 200)).IsEqualTo(new Vector2(2, 1));
			await Assert.That(PaintingCanvas.SquareProportions(200, 400)).IsEqualTo(new Vector2(1, 2));
		}
	}
}
