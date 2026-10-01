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

The views and conclusions contained in the software and documentation are those
of the authors and should not be interpreted as representing official policies,
either expressed or implied, of the FreeBSD Project.
*/

using System.Linq;
using System.Threading.Tasks;
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// Lars's report: with the Code Example window lying over the System window, turning the wheel over Code Example
	// scrolled System behind it. The wheel belongs to the window on top; WheelOcclusionRoutingTests covers the rule.
	// new DemoTheme() writes ThemeConfig.Current.
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)]
	public class DemoWindowWheelOcclusionTests
	{
		private static DemoSpec Spec(string title) => GuiDemoSpecs.All.First(s => s.Title == title);

		[Test]
		public async Task TheWheelOverCodeExampleDoesNotScrollTheSystemWindowBehindIt()
		{
			var canvas = new GuiWidget(DemoWindowHost.DefaultCanvasWidth, DemoWindowHost.DefaultCanvasHeight);
			var host = new DemoWindowHost(canvas);
			DemoSpec system = Spec("System");
			DemoSpec codeExample = Spec("Code Example");
			host.SetOpen(system, true, fade: false);
			host.SetOpen(codeExample, true, fade: false);
			host.Raise(codeExample);
			canvas.PerformLayout();

			WindowWidget systemWindow = host.GetWindow(system);
			ScrollableWidget scroller = systemWindow.Descendants<ScrollableWidget>()
				.First(s => ShownIn(s, systemWindow) && s.ScrollArea.Height > s.Height + 1);
			Vector2 point = scroller.TransformToParentSpace(canvas, scroller.LocalBounds.Center);

			// Lay Code Example's title bar over that point: nothing there takes the wheel, so it is left unconsumed
			// on its way back up - which is where it used to leak to the window behind.
			WindowWidget codeWindow = host.GetWindow(codeExample);
			Vector2 titleCenter = codeWindow.TitleBar.TransformToParentSpace(canvas, codeWindow.TitleBar.LocalBounds.Center);
			codeWindow.Position += point - titleCenter;
			canvas.PerformLayout();
			await Assert.That(codeWindow.BoundsRelativeToParent.Contains(point)).IsTrue();
			await Assert.That(canvas.Children.Last()).IsEqualTo(codeWindow);

			Vector2 start = scroller.ScrollPosition;
			canvas.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, -120));
			await Assert.That(scroller.ScrollPosition).IsEqualTo(start);

			// With Code Example out of the way the same wheel does scroll System - the point was a live one.
			codeWindow.Position = new Vector2(canvas.Width + 100, 0);
			canvas.PerformLayout();
			canvas.OnMouseWheel(new MouseEventArgs(MouseButtons.None, 0, point.X, point.Y, -120));
			await Assert.That(scroller.ScrollPosition).IsNotEqualTo(start);
		}

		/// <summary>Whether <paramref name="widget"/> and everything between it and <paramref name="window"/> is
		/// shown (a hidden tab page's scroll view is not under the pointer).</summary>
		private static bool ShownIn(GuiWidget widget, GuiWidget window)
		{
			for (GuiWidget w = widget; w != null && w != window; w = w.Parent)
			{
				if (!w.Visible || w.Width <= 0 || w.Height <= 0)
				{
					return false;
				}
			}

			return true;
		}
	}
}
