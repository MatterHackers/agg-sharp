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
using MatterHackers.AggSharpDemo.GuiDemo.Windows.Tests;
using MatterHackers.GuiAutomation;
using MatterHackers.VectorMath;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI demo's Cursor Test window (agg-gui's windows/tests/basic/controls.rs cursor_test).
	[NotInParallel(new[] { nameof(AutomationRunner.ShowWindowAndExecuteTests), nameof(MatterHackers.Agg.UI.ThemeConfig.Current) })]
	public class CursorTestWindowTests
	{
		private static DemoSpec Spec => GuiDemoSpecs.All.First(s => s.Title == "Cursor Test");

		[Test]
		public async Task BuildsOneNamedRowPerAggGuiCursor()
		{
			var demoTheme = new DemoTheme(ThemePreference.Light);
			var window = (CursorTestWindow)GuiDemoSpecs.CreateContent(Spec, demoTheme);
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			await Assert.That(window.Name).IsEqualTo("Cursor Test Content");

			// agg-gui's ALL_CURSORS has 35 entries, and each is a distinct cursor.
			await Assert.That(CursorTestWindow.AllCursors.Count).IsEqualTo(35);
			await Assert.That(CursorTestWindow.AllCursors.Select(c => c.Cursor).Distinct().Count()).IsEqualTo(35);
			for (int i = 0; i < CursorTestWindow.AllCursors.Count; i++)
			{
				(string name, Cursors cursor) = CursorTestWindow.AllCursors[i];
				GuiWidget row = window.FindDescendant("Cursor Test " + name);
				await Assert.That(row).IsSameReferenceAs(window.Rows[i]);
				await Assert.That(row.Cursor).IsEqualTo(cursor);
			}

			var image = new ImageBuffer((int)window.Width, (int)window.Height);
			window.OnDraw(image.NewGraphics2D());
			demoTheme.SetPreference(ThemePreference.Dark);
			await Assert.That(window.BackgroundColor).IsEqualTo(demoTheme.Palette.PanelFill);
			window.OnDraw(image.NewGraphics2D());
		}

		/// <summary>Hovering a row puts the pointer over a widget whose cursor is that row's - the whole demo.</summary>
		[Test]
		public async Task HoveringARowPutsThePointerOverThatRow()
		{
			var window = (CursorTestWindow)GuiDemoSpecs.CreateContent(Spec, new DemoTheme(ThemePreference.Light));
			var host = new GuiWidget(Spec.DefaultWidth, Spec.DefaultHeight);
			host.AddChild(window);
			host.PerformLayout();

			GuiWidget grab = window.FindDescendant("Cursor Test Grab");
			Vector2 center = grab.TransformToParentSpace(host, grab.LocalBounds.Center);
			host.OnMouseMove(new MouseEventArgs(MouseButtons.None, 0, center.X, center.Y, 0));

			await Assert.That(grab.UnderMouseState).IsNotEqualTo(UnderMouseState.NotUnderMouse);
			await Assert.That(grab.Cursor).IsEqualTo(Cursors.Grab);
			await Assert.That(window.FindDescendant("Cursor Test Grabbing").UnderMouseState).IsEqualTo(UnderMouseState.NotUnderMouse);
		}
	}
}
