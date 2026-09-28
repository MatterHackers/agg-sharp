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
	public class BezierWindowTests
	{
		private static BezierWindow Build()
		{
			var spec = GuiDemoSpecs.All.First(s => s.Title == "Bézier Curve");
			var window = (BezierWindow)GuiDemoSpecs.CreateContent(spec, new DemoTheme());
			var host = new GuiWidget(spec.DefaultWidth, spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return window;
		}

		[Test]
		public async Task BuildsTheDegreeRadiosAndCanvas()
		{
			var window = Build();

			await Assert.That(window.FindDescendant("Bezier Quadratic")).IsSameReferenceAs(window.QuadraticRadio);
			await Assert.That(window.FindDescendant("Bezier Cubic")).IsSameReferenceAs(window.CubicRadio);
			await Assert.That(window.FindDescendant("Bezier Canvas")).IsSameReferenceAs(window.Canvas);
			await Assert.That(window.CubicRadio.Checked).IsTrue();
			await Assert.That(window.Canvas.Height).IsGreaterThan(50);

			// Drawing through Graphics2D must not throw for either degree.
			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			window.QuadraticRadio.Checked = true;
			await Assert.That(window.Canvas.Cubic).IsFalse();
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task DraggingAControlPointMovesIt()
		{
			var canvas = Build().Canvas;
			Vector2 p0 = canvas.Points[0];

			canvas.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, p0.X + 2, p0.Y, 0));
			await Assert.That(canvas.Dragging).IsEqualTo(0);
			canvas.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, p0.X + 20, p0.Y + 20, 0));
			canvas.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, p0.X + 20, p0.Y + 20, 0));

			await Assert.That(canvas.Points[0]).IsEqualTo(new Vector2(p0.X + 20, p0.Y + 20));
			await Assert.That(canvas.Dragging).IsEqualTo(-1);

			// In quadratic mode the fourth point cannot be grabbed.
			canvas.Cubic = false;
			await Assert.That(canvas.Nearest(canvas.Points[3])).IsEqualTo(-1);
			await Assert.That(canvas.CurveBounds().Contains(canvas.Points[0])).IsTrue();
		}
	}
}
