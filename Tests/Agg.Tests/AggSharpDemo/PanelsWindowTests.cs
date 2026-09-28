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
using MatterHackers.Agg.UI;
using MatterHackers.AggSharpDemo.GuiDemo;
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Layout;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Panels window (agg-gui's panels_demo in demo-ui/src/windows/interaction.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class PanelsWindowTests
	{
		private static DemoSpec PanelsSpec => GuiDemoSpecs.All.First(s => s.Title == "Panels");

		private static (GuiWidget Page, PanelsWindow Window) Build(double width = 600, double height = 400)
		{
			var page = new GuiWidget(width * GuiWidget.DeviceScale, height * GuiWidget.DeviceScale);
			GuiWidget content = GuiDemoSpecs.CreateContent(PanelsSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (page, (PanelsWindow)content);
		}

		[Test]
		public async Task BuildsEveryPanelNamedInEguiGeometry()
		{
			(_, PanelsWindow window) = Build();
			await Assert.That(window.Name).IsEqualTo("Panels Content");

			foreach (string title in PanelsLayout.Titles)
			{
				await Assert.That(window.FindDescendant($"Panels {title}")).IsNotNull();
			}

			foreach (string name in new[] { "Panels Top Separator", "Panels Left Separator", "Panels Right Separator", "Panels Source Link" })
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull();
			}

			// agg-gui's panels_match_egui_order_with_y_up_geometry, at 600 by 400.
			double s = GuiWidget.DeviceScale;
			var expected = new[]
			{
				new RectangleDouble(0, 288, 600, 400),
				new RectangleDouble(0, 0, 150, 284),
				new RectangleDouble(450, 0, 600, 284),
				new RectangleDouble(154, 0, 446, 52),
				new RectangleDouble(154, 56, 446, 284),
			};
			for (int i = 0; i < 5; i++)
			{
				RectangleDouble bounds = window.Panel(PanelsLayout.Titles[i]).BoundsRelativeToParent;
				await Assert.That(bounds).IsEqualTo(new RectangleDouble(expected[i].Left * s, expected[i].Bottom * s, expected[i].Right * s, expected[i].Top * s));
			}
		}

		[Test]
		public async Task DraggingTheLeftSeparatorWidensTheLeftPanel()
		{
			(GuiWidget page, PanelsWindow window) = Build();
			double s = GuiWidget.DeviceScale;

			// Press in the gap right of the 150-wide left panel and drag it 30 to the right.
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 152 * s, 100 * s, 0));
			page.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 180 * s, 100 * s, 0));
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 180 * s, 100 * s, 0));

			await Assert.That(window.PanelLayout.LeftWidth).IsEqualTo(180).Within(1e-9);
			await Assert.That(window.Panel("Left Panel").Width).IsEqualTo(180 * s).Within(1e-9);
			await Assert.That(window.Panel("Central Panel").BoundsRelativeToParent.Left).IsEqualTo(184 * s).Within(1e-9);

			// The side is held to 200 however far it is dragged.
			page.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, 182 * s, 100 * s, 0));
			page.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, 400 * s, 100 * s, 0));
			page.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, 400 * s, 100 * s, 0));
			await Assert.That(window.PanelLayout.LeftWidth).IsEqualTo(PanelsLayout.SideMaximumWidth);
		}
	}
}
