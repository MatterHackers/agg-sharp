/*
Copyright (c) 2026, Lars Brubaker
All rights reserved.
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
	// The GUI demo's page-wide keys (agg-gui app_builder.rs global key handler), pressed on the system window
	// with nothing focused, as a user who has just clicked the canvas would.
	[NotInParallel(new[] { SharedStateKeys.ThemeConfigCurrent, SharedStateKeys.MarkdownWidget })]
	public class DemoShortcutsTests
	{
		private static (SystemWindow Host, GuiDemoShell Shell) LoadedShell()
		{
			var host = new SystemWindow(1000, 700);
			var shell = new GuiDemoShell(new DemoTheme());
			host.AddChild(shell);
			host.PerformLayout();
			shell.OnLoad(null);
			return (host, shell);
		}

		private static void Press(SystemWindow host, Keys key)
		{
			host.OnKeyDown(new KeyEventArgs(key | Keys.Control | Keys.Shift));
		}

		[Test]
		public async Task CtrlShiftOOrganizesTheWindows()
		{
			(SystemWindow host, GuiDemoShell shell) = LoadedShell();
			DemoSpec spec = shell.Windows.ZOrder.First();
			RectangleDouble tile = shell.Windows.GetVisibleRect(spec).Value;
			shell.Windows.GetWindow(spec).Position += new Vector2(37, -21);

			Press(host, Keys.O);
			await Assert.That(shell.Windows.GetVisibleRect(spec).Value).IsEqualTo(tile);
			host.Close();
		}

		[Test]
		public async Task CtrlShiftRClosesEveryDemoWindowAndPlainRDoesNot()
		{
			(SystemWindow host, GuiDemoShell shell) = LoadedShell();
			await Assert.That(shell.Windows.ZOrder.Count).IsGreaterThan(0);

			host.OnKeyDown(new KeyEventArgs(Keys.R | Keys.Shift));
			await Assert.That(shell.Windows.ZOrder.Count).IsGreaterThan(0);

			Press(host, Keys.R);
			await Assert.That(GuiDemoSpecs.All.Any(shell.Windows.IsOpen)).IsFalse();
			host.Close();
		}

		[Test]
		public async Task CtrlShiftDWritesADrawReportListingTheOpenWindows()
		{
			(SystemWindow host, GuiDemoShell shell) = LoadedShell();
			string report = null;
			shell.DrawReported += (s, text) => report = text;

			Press(host, Keys.D);
			await Assert.That(report).IsNotNull();
			await Assert.That(report).Contains("draw report");
			await Assert.That(report).Contains("scheduled UI callbacks");
			await Assert.That(report).Contains(shell.Windows.ZOrder.Last().Title);
			host.Close();
		}
	}
}
