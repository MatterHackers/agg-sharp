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
using MatterHackers.GuiAutomation;
using TUnit.Assertions;
using TUnit.Assertions.Extensions;
using TUnit.Core;

namespace MatterHackers.Agg.Tests.AggSharpDemo
{
	// The GUI Demo page's frame, as agg-gui lays it out: a 26 px bar on top, a 220 px sidebar on the right,
	// and the canvas taking the rest.
	// GuiDemoShell installs its theme as ThemeConfig.Current, which other tests set and read.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })] // the shell can build the About window's MarkdownWidget
	public class GuiDemoShellTests
	{
		/// <summary>The widget's bounds in the shell's coordinates (y up).</summary>
		private static RectangleDouble BoundsInShell(GuiWidget widget, GuiWidget shell)
		{
			RectangleDouble bounds = widget.LocalBounds;
			for (GuiWidget w = widget; w != shell; w = w.Parent)
			{
				bounds.Offset(w.Position);
			}

			return bounds;
		}

		[Test]
		public async Task TopBarSidebarAndCanvasTileThePage()
		{
			var page = new GuiWidget(1000, 700);
			var shell = new GuiDemoShell();
			page.AddChild(shell);
			page.PerformLayout();

			await Assert.That(shell.LocalBounds).IsEqualTo(new RectangleDouble(0, 0, 1000, 700));
			await Assert.That(BoundsInShell(shell.TopBar, shell)).IsEqualTo(new RectangleDouble(0, 674, 1000, 700));
			await Assert.That(BoundsInShell(shell.Sidebar, shell)).IsEqualTo(new RectangleDouble(780, 0, 1000, 674));
			await Assert.That(BoundsInShell(shell.Canvas, shell)).IsEqualTo(new RectangleDouble(0, 0, 780, 674));

			// Resizing the page keeps the bar and sidebar fixed and gives the change to the canvas.
			page.Size = new VectorMath.Vector2(1400, 900);
			page.PerformLayout();
			await Assert.That(BoundsInShell(shell.TopBar, shell)).IsEqualTo(new RectangleDouble(0, 874, 1400, 900));
			await Assert.That(BoundsInShell(shell.Sidebar, shell)).IsEqualTo(new RectangleDouble(1180, 0, 1400, 874));
			await Assert.That(BoundsInShell(shell.Canvas, shell)).IsEqualTo(new RectangleDouble(0, 0, 1180, 874));
		}

		// View > Window Snapping is agg-gui's global snap flag: it turns the canvas's window snapping on and off.
		[Test]
		public async Task WindowSnappingMenuTogglesTheCanvasSnapping()
		{
			var shell = new GuiDemoShell();
			await Assert.That(shell.Windows.Snap.Enabled).IsTrue();
			await Assert.That(shell.Canvas.Children.Contains(shell.Windows.Snap.Overlay)).IsTrue();

			var view = shell.TopBar.Menus[1].SubMenuItems();
			view.Single(item => item.AutomationName == "view.snap").Action();
			await Assert.That(shell.Windows.Snap.Enabled).IsFalse();

			view.Single(item => item.AutomationName == "view.snap").Action();
			await Assert.That(shell.Windows.Snap.Enabled).IsTrue();
		}

		[Test]
		public async Task The3DAnimationWindowsOwnSelectorIsItsOnlySsaaSetting()
		{
			var shell = new GuiDemoShell();
			var spec = GuiDemoSpecs.All.First(s => s.Title == "3D Animation");
			await Assert.That(shell.ThreeDAnimation).IsNull().Because("the window is built when first opened");

			// It opens at the bar grid's own default, nothing else seeding it.
			shell.Windows.SetOpen(spec, true);
			var window = shell.ThreeDAnimation;
			await Assert.That(window).IsNotNull();
			await Assert.That(window.SsaaFactor).IsEqualTo(MatterHackers.AggSharpDemo.GuiDemo.Windows.Graphics.ThreeDAnimationWindow.DefaultSsaaFactor);

			// Its selector drives its bars, and the choice survives a close and reopen.
			window.SsaaButtons[3].InvokeClick();
			await Assert.That(window.BarGrid.SsaaFactor).IsEqualTo(4);
			shell.Windows.SetOpen(spec, false);
			shell.Windows.SetOpen(spec, true);
			await Assert.That(shell.ThreeDAnimation).IsSameReferenceAs(window);
			await Assert.That(window.SsaaFactor).IsEqualTo(4);
		}

		[Test]
		public async Task ANarrowPageHidesTheSidebarBehindTheDrawerButton()
		{
			var page = new GuiWidget(600, 500);
			var shell = new GuiDemoShell(new DemoTheme());
			page.AddChild(shell);
			page.PerformLayout();

			// shell.rs: below 720 the sidebar takes no room until the drawer opens, then 300 wide
			await Assert.That(shell.IsNarrow).IsTrue();
			await Assert.That(shell.Sidebar.Visible).IsFalse();
			await Assert.That(shell.TopBar.SidebarDrawerButton.Visible).IsTrue();

			shell.TopBar.SidebarDrawerButton.InvokeClick();
			await Assert.That(shell.Sidebar.Visible).IsTrue();
			await Assert.That(shell.Sidebar.Width).IsEqualTo(GuiDemoShell.MobileSidebarWidth);

			// Widened past the breakpoint, it docks at 220 and the button goes
			page.Width = 1000;
			page.PerformLayout();
			await Assert.That(shell.IsNarrow).IsFalse();
			await Assert.That(shell.Sidebar.Visible).IsTrue();
			await Assert.That(shell.Sidebar.Width).IsEqualTo(GuiDemoShell.SidebarWidth);
			await Assert.That(shell.TopBar.SidebarDrawerButton.Visible).IsFalse();

			// Narrow again, the drawer is as the user left it (open)
			page.Width = 500;
			page.PerformLayout();
			await Assert.That(shell.Sidebar.Visible).IsTrue();
			await Assert.That(shell.Sidebar.Width).IsEqualTo(GuiDemoShell.MobileSidebarWidth);
			shell.TopBar.SidebarDrawerButton.InvokeClick();
			await Assert.That(shell.Sidebar.Visible).IsFalse();
		}

		[Test]
		[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
		public async Task ClickingTheDrawerButtonOpensTheSidebarOnANarrowWindow()
		{
			var shell = new GuiDemoShell(new DemoTheme(ThemePreference.Dark));
			var window = new SystemWindow(600, 500) { Name = "GuiDemo Narrow Test Window" };
			window.AddChild(shell);

			await AutomationRunner.ShowWindowAndExecuteTests(window, async testRunner =>
			{
				await Assert.That(shell.Sidebar.Visible).IsFalse();
				testRunner.ClickByName("GuiDemo Sidebar Toggle");
				testRunner.WaitFor(() => shell.Sidebar.Visible);
				await Assert.That(shell.Sidebar.Visible).IsTrue();
				testRunner.ClickByName("Sidebar Sliders");
				testRunner.WaitFor(() => shell.Windows.IsOpen(GuiDemoSpecs.All.First(s => s.Title == "Sliders")));
				testRunner.MarkTestComplete();
			});
		}
	}
}
