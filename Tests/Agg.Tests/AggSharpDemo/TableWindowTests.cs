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
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Enums;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Table window (agg-gui's table_demo in demo-ui/src/windows/text_demos/table_demo.rs).
	[NotInParallel(SharedStateKeys.ThemeConfigCurrent)] // new DemoTheme() writes ThemeConfig.Current
	public class TableWindowTests
	{
		private static DemoSpec TableSpec => GuiDemoSpecs.All.First(s => s.Title == "Table");

		private static (GuiWidget Page, TableWindow Window) Build()
		{
			var page = new GuiWidget(720 * GuiWidget.DeviceScale, 560 * GuiWidget.DeviceScale);
			GuiWidget content = GuiDemoSpecs.CreateContent(TableSpec);
			page.AddChild(content);
			page.PerformLayout();
			return (page, (TableWindow)content);
		}

		[Test]
		public async Task BuildsEveryNamedControlAndTwentyManualRows()
		{
			(_, TableWindow window) = Build();
			await Assert.That(window.Name).IsEqualTo("Table Content");
			foreach (string name in new[]
			{
				"Table Striped", "Table Overline", "Table Resizable", "Table Clickable", "Table Type 0", "Table Type 1", "Table Type 2",
				"Table Num Rows", "Table Scroll To Row", "Table Reset", "Table Grid", "Table Source Link",
			})
			{
				await Assert.That(window.FindDescendant(name)).IsNotNull().Because(name);
			}

			await Assert.That(window.Grid.Rows.Count).IsEqualTo(TableWindow.ManualRowCount);
			await Assert.That(window.Grid.Height).IsGreaterThan(100 * GuiWidget.DeviceScale);

			// Thousands of rows of the same height: the slider's 10,000.
			((RadioButton)window.FindDescendant("Table Type 1")).Checked = true;
			await Assert.That(window.Grid.Rows.Count).IsEqualTo(10_000);
			await Assert.That(window.FindDescendant("Table Num Rows").Parent.Visible).IsTrue();
		}

		[Test]
		public async Task ClickingARowSelectsItAndTheRowHeaderReversesTheOrder()
		{
			(_, TableWindow window) = Build();
			VirtualTable grid = window.Grid;
			double s = GuiWidget.DeviceScale;

			// The middle of the first row, in the "Clipped text" column.
			var firstRow = new Vector2(100 * s, grid.Height - (grid.HeaderHeight + 10) * s);
			grid.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, firstRow.X, firstRow.Y, 0));
			grid.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, firstRow.X, firstRow.Y, 0));
			await Assert.That(window.Selection).IsEquivalentTo(new[] { 0 }, CollectionOrdering.Matching);

			// The "Row" header flips the order: the first slot now shows row 19, and row 0's selection follows it.
			var header = new Vector2(10 * s, grid.Height - 10 * s);
			grid.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, header.X, header.Y, 0));
			grid.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 1, header.X, header.Y, 0));
			await Assert.That(window.Reversed).IsTrue();
			await Assert.That(window.DisplayIndex(0)).IsEqualTo(19);
			await Assert.That(grid.IsRowSelected(19)).IsTrue();
		}
	}
}
