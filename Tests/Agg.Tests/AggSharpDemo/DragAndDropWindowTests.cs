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
	// The GUI demo's Drag and Drop window (agg-gui's interaction/drag_and_drop.rs).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class DragAndDropWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Drag and Drop");

		private static DragAndDropWindow Build(DemoTheme demoTheme = null)
		{
			var window = (DragAndDropWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme ?? new DemoTheme());
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();
			return window;
		}

		/// <summary>The middle of item <paramref name="row"/> in column <paramref name="column"/>, board-local.</summary>
		private static Vector2 ItemCenter(DragAndDropBoard board, int column, int row) =>
			new Vector2(board.ColumnBounds(column).Center.X, DragAndDropBoard.ItemBottom(board.Height, row) + 13 * GuiWidget.DeviceScale);

		[Test]
		public async Task BuildsTheBoardAndDrawsInBothThemes()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = Build(demoTheme);

			await Assert.That(window.Name).IsEqualTo("Drag and Drop Content");
			await Assert.That(window.FindDescendant("Drag and Drop Board")).IsSameReferenceAs(window.Board);
			await Assert.That(window.FindDescendant("Drag and Drop Source Link")).IsNotNull();
			await Assert.That(window.Board.Height).IsGreaterThan(150);

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		[Test]
		public async Task SameColumnDropAdjustsForTheRemovedRow()
		{
			var board = Build().Board;
			board.Move((0, 1), (0, 3));
			await Assert.That(board.Columns[0]).IsEquivalentTo(new[] { "Item A", "Item C", "Item B", "Item D" });
		}

		[Test]
		public async Task CrossColumnDropKeepsTheDestinationRow()
		{
			var board = Build().Board;
			board.Move((0, 1), (1, 1));
			await Assert.That(board.Columns[0]).IsEquivalentTo(new[] { "Item A", "Item C", "Item D" });
			await Assert.That(board.Columns[1]).IsEquivalentTo(new[] { "Item E", "Item B", "Item F", "Item G" });
		}

		[Test]
		public async Task AClickWithoutMovingDoesNotDrag()
		{
			var board = Build().Board;
			Vector2 a = ItemCenter(board, 0, 0);
			board.OnMouseDown(new MouseEventArgs(MouseButtons.Left, 1, a.X, a.Y, 0));
			await Assert.That(board.DragSource).IsEqualTo(((int, int)?)(0, 0));
			board.OnMouseMove(new MouseEventArgs(MouseButtons.Left, 0, a.X + 1, a.Y, 0));
			await Assert.That(board.DragActive).IsFalse();
			board.OnMouseUp(new MouseEventArgs(MouseButtons.Left, 0, a.X + 1, a.Y, 0));

			await Assert.That(board.Columns[0].First()).IsEqualTo("Item A");
			await Assert.That(board.DragSource).IsNull();
		}

		[Test]
		public async Task DraggingAnItemToAnotherColumnMovesIt()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (DragAndDropWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var systemWindow = new SystemWindow(600, 400) { Name = "Drag and Drop Test Window" };
			systemWindow.AddChild(window);

			await AutomationRunner.ShowWindowAndExecuteTests(systemWindow, async testRunner =>
			{
				DragAndDropBoard board = window.Board;
				Vector2 from = ItemCenter(board, 0, 0);

				// Below the last item of Column C appends to it.
				var to = new Vector2(board.ColumnBounds(2).Center.X, 20);
				testRunner.DragByName("Drag and Drop Board", offset: new Point2D((int)from.X, (int)from.Y), origin: AutomationRunner.ClickOrigin.LowerLeft);
				testRunner.DropByName("Drag and Drop Board", offset: new Point2D((int)to.X, (int)to.Y), origin: AutomationRunner.ClickOrigin.LowerLeft);
				testRunner.WaitFor(() => board.Columns[2].Count == 5);

				await Assert.That(board.Columns[0].First()).IsEqualTo("Item B");
				await Assert.That(board.Columns[2].Last()).IsEqualTo("Item A");
				await Assert.That(board.DragActive).IsFalse();
				testRunner.MarkTestComplete();
			});
		}
	}
}
